using System.Text.Json;
using LensDocsStudio.Core.Documents;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Core.State;

public sealed record SavedDocumentSnapshot(int SchemaVersion, DateTimeOffset CreatedAt, string Reason,
    RecoveredDocument Document, string PortableText, bool PortableAssets, IReadOnlyList<string> Warnings);
public sealed record DocumentSnapshotEntry(string Path, DateTimeOffset CreatedAt, string Name, string Reason, bool Legacy = false)
{
    public string Label => Name + " — " + CreatedAt.ToLocalTime().ToString("g") + " — " + Reason;
}
public sealed record DocumentSnapshotListing(IReadOnlyList<DocumentSnapshotEntry> Entries, IReadOnlyList<string> Warnings);

/// <summary>Immutable local recovery copies. Reading a record never authorises a write to its recorded source path.</summary>
public sealed class DocumentSnapshotStore(string stateDirectory)
{
    private readonly string stateDirectory = Path.GetFullPath(stateDirectory);
    public string DirectoryPath => Path.Combine(stateDirectory, "snapshots");
    private const int MaximumRecordBytes = 128 * 1024 * 1024;

    public async Task<DocumentSnapshotEntry> CaptureAsync(RecoveredDocument document, string reason,
        string? sourcePath = null, WorkspacePath? sourceWorkspace = null, CancellationToken cancellationToken = default)
    {
        ValidateDocument(document);
        var folder = Path.Combine(DirectoryPath, Guid.NewGuid().ToString("N"));
        WorkspacePath.RejectReparsePoints(folder);
        Directory.CreateDirectory(folder);
        var portableText = document.Text;
        var portableAssets = false;
        var warnings = new List<string>();
        PreparedAssetTransfer? transfer = null;
        try
        {
            if (sourcePath is not null && sourceWorkspace is not null)
            {
                try
                {
                    transfer = await new AssetTransferService().PrepareAsync(document.Text, sourcePath, sourceWorkspace,
                        Path.Combine(folder, "snapshot.md"), new(folder), cancellationToken, forceCopyImages: true);
                    portableText = transfer.Text; portableAssets = true;
                    if (transfer.UncopiedDocumentLinks.Count != 0) warnings.Add("Document links refer to the original workspace; review them when saving a recovered copy.");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { warnings.Add("The source text is retained, but linked images could not all be copied: " + ex.Message); }
            }
            var record = new SavedDocumentSnapshot(1, DateTimeOffset.UtcNow, reason, document, portableText, portableAssets, warnings);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(record);
            if (bytes.Length > MaximumRecordBytes) throw new IOException("The snapshot exceeds the recovery record limit.");
            var path = Path.Combine(folder, "record.json");
            await DocumentStore.WriteDurableAsync(path, bytes, cancellationToken);
            transfer?.Commit();
            return Entry(path, record);
        }
        finally { if (transfer is not null) await transfer.DisposeAsync(); }
    }

    public async Task<DocumentSnapshotListing> ListAsync(CancellationToken cancellationToken = default)
    {
        var entries = new List<DocumentSnapshotEntry>(); var warnings = new List<string>();
        var paths = Directory.Exists(DirectoryPath) ? Directory.EnumerateDirectories(DirectoryPath).Select(p => Path.Combine(p, "record.json")) : [];
        var legacy = Directory.Exists(stateDirectory) ? Directory.EnumerateFiles(stateDirectory, "snapshot-*.json") : [];
        var candidates = paths.Concat(legacy).Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).ToArray();
        foreach (var path in candidates.Take(500))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var isLegacy = Path.GetDirectoryName(path) == stateDirectory;
                var record = await ReadAsync(path, isLegacy, cancellationToken);
                entries.Add(Entry(path, record, isLegacy));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
            { warnings.Add("Snapshot retained but unavailable: " + Path.GetFileName(Path.GetDirectoryName(path)) + ". " + ex.Message); }
        }
        if (candidates.Length > 500) warnings.Add("Showing the latest 500 snapshots. Older snapshots remain in native state.");
        return new(entries.OrderByDescending(e => e.CreatedAt).ToArray(), warnings);
    }

    public Task<SavedDocumentSnapshot> ReadAsync(DocumentSnapshotEntry entry, CancellationToken cancellationToken = default) => ReadAsync(entry.Path, entry.Legacy, cancellationToken);

    private async Task<SavedDocumentSnapshot> ReadAsync(string path, bool legacy, CancellationToken cancellationToken)
    {
        path = Path.GetFullPath(path);
        if (legacy ? Path.GetDirectoryName(path) != stateDirectory || !Path.GetFileName(path).StartsWith("snapshot-", StringComparison.Ordinal) || !path.EndsWith(".json", StringComparison.Ordinal)
            : !WorkspacePath.Contains(DirectoryPath, path) || Path.GetFileName(path) != "record.json" || Path.GetDirectoryName(Path.GetDirectoryName(path)) != DirectoryPath)
            throw new IOException("The snapshot is outside native recovery storage.");
        WorkspacePath.RejectReparsePoints(path);
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
        if (file.Length > MaximumRecordBytes) throw new IOException("The snapshot exceeds the recovery reading limit.");
        SavedDocumentSnapshot record;
        if (legacy)
        {
            var document = await JsonSerializer.DeserializeAsync<RecoveredDocument>(file, cancellationToken: cancellationToken) ?? throw new IOException("The old snapshot is empty.");
            record = new(1, File.GetLastWriteTimeUtc(path), "Before reload (earlier preview)", document, document.Text, false, ["This older snapshot contains source text; image files remain in the original workspace."]);
        }
        else record = await JsonSerializer.DeserializeAsync<SavedDocumentSnapshot>(file, cancellationToken: cancellationToken) ?? throw new IOException("The snapshot is empty.");
        if (record is not { SchemaVersion: 1, Document: not null, PortableText: not null, Warnings: not null, Reason: not null }) throw new IOException("The snapshot is incomplete or unsupported.");
        ValidateDocument(record.Document);
        if (DocumentSnapshot.Encode(record.PortableText, false).Length > DocumentStore.MaximumDocumentBytes) throw new IOException("The recovered document exceeds the editing limit.");
        return record;
    }

    private static void ValidateDocument(RecoveredDocument document)
    {
        if (document is not { Text: not null } || DocumentSnapshot.Encode(document.Text, document.Utf8Bom).Length > DocumentStore.MaximumDocumentBytes)
            throw new IOException("The snapshot source is missing or exceeds the editing limit.");
    }
    private static DocumentSnapshotEntry Entry(string path, SavedDocumentSnapshot record, bool legacy = false) =>
        new(path, record.CreatedAt, Path.GetFileName(record.Document.Path) ?? record.Document.DisplayName ?? "Untitled.md", record.Reason, legacy);
}
