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
                if (entry.Length > MaxEntryBytes || (total += entry.Length) > MaxTotalBytes) throw new InvalidDataException("The bundle exceeds the import size limit.");
                if (entry.Length > 1_000_000 && entry.CompressedLength > 0 && entry.Length / entry.CompressedLength > 1000)
                    throw new InvalidDataException("The bundle compression ratio exceeds the safety limit.");
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                using var input = entry.Open();
                var bytes = await ReadBoundedAsync(input, MaxEntryBytes, cancellationToken);
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
        CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            var count = 0; long total = 0;
            async Task Walk(string directory)
            {
                foreach (var path in Directory.EnumerateFileSystemEntries(directory).Order(StringComparer.OrdinalIgnoreCase))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) continue;
                    if (Directory.Exists(path))
                    {
                        if (Path.GetFileName(path) is ".git" or "node_modules" or "bin" or "obj") continue;
                        await Walk(path); continue;
                    }
                    if (!WorkspaceService.IsDocument(path) && !IsAsset(path)) continue;
                    if (++count > MaxEntries) throw new IOException("The workspace exceeds the 2,000-entry bundle limit.");
                    var relative = Path.GetRelativePath(workspace.Root, path).Replace('\\', '/');
                    workspace.Resolve(relative);
                    byte[] bytes;
                    if (editedDocuments?.TryGetValue(path, out var edited) == true) bytes = Documents.DocumentSnapshot.Encode(edited, false);
                    else
                    {
                        if (new FileInfo(path).Length > MaxEntryBytes) throw new IOException("A bundle file exceeds 32 MB.");
                        bytes = await File.ReadAllBytesAsync(path, cancellationToken);
                    }
                    if ((total += bytes.Length) > MaxTotalBytes) throw new IOException("The bundle exceeds 256 MB.");
                    var entry = archive.CreateEntry(relative, CompressionLevel.Optimal);
                    entry.LastWriteTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
                    using var target = entry.Open(); await target.WriteAsync(bytes, cancellationToken);
                }
            }
            await Walk(workspace.Root);
        }
        return stream.ToArray();
    }

    internal static async Task<byte[]> ReadBoundedAsync(Stream stream, long maximumBytes, CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        var buffer = new byte[65536];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) != 0)
        {
            if (output.Length + read > maximumBytes) throw new InvalidDataException("A decompressed entry exceeds the import limit.");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }
}
