using System.IO.Compression;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Core.Imports;

public sealed record BundleImportResult(string Root, int Documents, int Assets, IReadOnlyList<string> Warnings);

public sealed class MarkdownBundleService
{
    private const int MaxEntries = 2000;
    private const long MaxEntryBytes = 32 * 1024 * 1024;
    private const long MaxTotalBytes = 256 * 1024 * 1024;
    private static bool IsAsset(string path) => Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp";
    public async Task<BundleImportResult> ImportAsync(string archivePath, string newFolder, CancellationToken cancellationToken = default)
    {
        newFolder = Path.GetFullPath(newFolder);
        WorkspacePath.RejectReparsePoints(newFolder);
        if (Directory.Exists(newFolder) || File.Exists(newFolder)) throw new IOException("Import requires a new folder so existing documents remain untouched.");
        var parent = Path.GetDirectoryName(newFolder)!;
        if (!Directory.Exists(parent)) throw new DirectoryNotFoundException("Select an existing parent folder.");
        var stage = Path.Combine(parent, ".lens-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        var warnings = new List<string>();
        var documents = 0; var assets = 0;
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            if (archive.Entries.Count > MaxEntries) throw new InvalidDataException("The bundle exceeds the 2,000-entry import limit.");
            var workspace = new WorkspacePath(stage);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name = entry.FullName.Replace('\\', '/');
                if (name.StartsWith('/') || name.Split('/').Any(p => p is "." or "..") || name.Contains(':'))
                    throw new InvalidDataException("The ZIP contains an unsafe path.");
                if (((entry.ExternalAttributes >> 16) & 0xf000) == 0xa000) throw new InvalidDataException("Symbolic links are not supported in imported ZIPs.");
                if (name.EndsWith('/')) continue;
                var destination = workspace.Resolve(name);
                if (!names.Add(destination)) throw new InvalidDataException("The ZIP contains duplicate Windows paths.");
                if (name.StartsWith("__MACOSX/", StringComparison.OrdinalIgnoreCase) || name.Split('/').Any(p => p is ".git" or "node_modules")) continue;
                var document = WorkspaceService.IsDocument(name);
                var metadata = name.Equals("lens-artifact-bundle.json", StringComparison.Ordinal);
                if (!document && !IsAsset(name) && !metadata) { warnings.Add("Unsupported bundle entry skipped: " + name); continue; }
                if (entry.Length > MaxEntryBytes || entry.Length > MaxTotalBytes - total) throw new InvalidDataException("The bundle exceeds the import size limit.");
                if (entry.Length > 1_000_000 && entry.CompressedLength > 0 && entry.Length / entry.CompressedLength > 1000)
                    throw new InvalidDataException("The bundle compression ratio exceeds the safety limit.");
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                using var input = entry.Open();
                var bytes = await ReadBoundedAsync(input, Math.Min(MaxEntryBytes, MaxTotalBytes - total), cancellationToken);
                if (bytes.LongLength != entry.Length) throw new InvalidDataException("A ZIP entry does not match its declared size.");
                if (System.IO.Hashing.Crc32.HashToUInt32(bytes) != entry.Crc32) throw new InvalidDataException("A ZIP entry failed its content checksum check.");
                total += bytes.LongLength;
                if (document) _ = Documents.DocumentSnapshot.FromBytes(destination, bytes);
                await File.WriteAllBytesAsync(destination, bytes, cancellationToken);
                if (document) documents++; else if (!metadata) assets++;
            }
            if (documents == 0) throw new InvalidDataException("The bundle contains no supported Markdown, Mermaid or text documents.");
            cancellationToken.ThrowIfCancellationRequested();
            Directory.Move(stage, newFolder);
            return new(newFolder, documents, assets, warnings);
        }
        finally
        {
            // Only the newly allocated sibling staging directory is eligible for cleanup.
            if (Directory.Exists(stage))
            {
                WorkspacePath.RejectReparsePoints(stage);
                Directory.Delete(stage, true);
            }
        }
    }

    public async Task<byte[]> ExportAsync(WorkspacePath workspace, IReadOnlyDictionary<string, string>? editedDocuments = null,
        CancellationToken cancellationToken = default, string? excludedDirectory = null, bool devopsMarkdown = false)
    {
        excludedDirectory = excludedDirectory is null ? null : Path.TrimEndingDirectorySeparator(Path.GetFullPath(excludedDirectory));
        if (excludedDirectory is not null && WorkspacePath.ContainsOrEquals(excludedDirectory, workspace.Root))
            throw new IOException("Choose a document workspace outside the native application's private state folder.");
        var listing = await new WorkspaceService().EnumerateAsync(workspace, cancellationToken, includeImages: true, excludedDirectory: excludedDirectory);
        if (listing.Warnings.Count != 0) throw new IOException("The workspace could not be completely read. " + string.Join(" ", listing.Warnings));
        var files = Files(listing.Entries).ToArray();
        if (files.Length > MaxEntries) throw new IOException("The workspace exceeds the 2,000-entry bundle limit.");
        if (!files.Any(WorkspaceService.IsDocument)) throw new IOException("The workspace contains no supported documents for a Markdown bundle.");
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            long total = 0;
            foreach (var path in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(workspace.Root, path).Replace('\\', '/');
                workspace.Resolve(relative);
                byte[] bytes;
                if (WorkspaceService.IsDocument(path) && editedDocuments?.TryGetValue(path, out var edited) == true)
                {
                    if (new System.Text.UTF8Encoding(false, true).GetByteCount(edited) > MaxEntryBytes) throw new IOException("An edited bundle document exceeds 32 MB.");
                    bytes = Documents.DocumentSnapshot.Encode(edited, false);
                }
                else
                {
                    await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
                    if (input.Length > MaxEntryBytes) throw new IOException("A bundle file exceeds 32 MB.");
                    bytes = await ReadBoundedAsync(input, MaxEntryBytes, cancellationToken);
                }
                if (devopsMarkdown && Path.GetExtension(path).ToLowerInvariant() is ".md" or ".markdown")
                {
                    var snapshot = Documents.DocumentSnapshot.FromBytes(path, bytes);
                    var converted = Exports.DevOpsMarkdownExporter.Convert(snapshot.Text, path, cancellationToken);
                    bytes = Documents.DocumentSnapshot.Encode(converted, snapshot.Utf8Bom);
                    if (bytes.Length > MaxEntryBytes) throw new IOException("A converted Markdown document exceeds 32 MB.");
                }
                if ((total += bytes.Length) > MaxTotalBytes) throw new IOException("The bundle exceeds 256 MB.");
                var entry = archive.CreateEntry(relative, CompressionLevel.Optimal);
                entry.LastWriteTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
                using var target = entry.Open(); await target.WriteAsync(bytes, cancellationToken);
            }
        }
        return stream.ToArray();
    }

    private static IEnumerable<string> Files(IEnumerable<WorkspaceEntry> entries)
    {
        foreach (var entry in entries)
            if (entry.IsDirectory) { foreach (var file in Files(entry.Children)) yield return file; }
            else yield return entry.Path;
    }

    internal static async Task<byte[]> ReadBoundedAsync(Stream stream, long maximumBytes, CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        var buffer = new byte[65536];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) != 0)
        {
            if (output.Length + read > maximumBytes) throw new InvalidDataException("A bundle entry exceeds its size limit.");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }
}
