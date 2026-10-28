using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Core.Arrangement;

public sealed record SavedArrangement(Guid Id, string Document, ArrangementBlockIdentity Block, ArrangementStrategy Strategy,
    IReadOnlyDictionary<string, ArrangementPosition> Positions, int LayoutVariant = 0);
public sealed record ArrangementFile(int SchemaVersion, IReadOnlyList<SavedArrangement> Layouts);
public sealed record ArrangementStoreRead(ArrangementFile File, string? Fingerprint, bool FromBackup = false);

/// <summary>Explicit portable sidecar storage. Reads never create, repair or migrate files.</summary>
public sealed class ArrangementStore(WorkspacePath workspace)
{
    public const string RelativePath = ".lens-docs/diagram-layouts.json";
    public const int MaximumBytes = 2 * 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, MaxDepth = 12,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, PropertyNameCaseInsensitive = false,
        RespectRequiredConstructorParameters = true
    };
    public async Task<ArrangementStoreRead> ReadAsync(CancellationToken token = default)
    {
        RequireCompletedSave();
        var bytes = await ReadBytesAsync(RelativePath, token).ConfigureAwait(false);
        RequireCompletedSave();
        return bytes is null ? new(new(1, []), null) : new(Parse(bytes), Hash(bytes));
    }
    public async Task<ArrangementStoreRead> ReadBackupAsync(CancellationToken token = default)
    {
        var current = await ReadBytesAsync(RelativePath, token).ConfigureAwait(false);
        var backup = await ReadBytesAsync(RelativePath + ".bak", token).ConfigureAwait(false)
            ?? throw new IOException("No arrangement backup is available.");
        return new(Parse(backup), current is null ? null : Hash(current), true);
    }
    public async Task<ArrangementStoreRead> SaveAsync(ArrangementFile value, ArrangementStoreRead expected, CancellationToken token = default)
    {
        Validate(value);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Options);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("The arrangement file exceeds the 2 MB limit.");
        var destination = workspace.Resolve(RelativePath);
        var folder = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(folder); WorkspacePath.RejectReparsePoints(folder);
        // Cooperating windows/processes serialise their compare-and-replace operation.
        var lockPath = workspace.Resolve(RelativePath + ".lock");
        await using var lease = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var pending = PendingJournals();
        if (pending.Length > 0 && !expected.FromBackup) throw InterruptedSave();
        var temporaryRelative = RelativePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var temporary = workspace.Resolve(temporaryRelative);
        try
        {
            if (expected.Fingerprint is null)
            {
                await DocumentStore.WriteDurableAsync(temporary, bytes, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested(); workspace.Resolve(RelativePath);
                File.Move(temporary, destination, false); // Never replace a newly appeared external file.
                ArchiveReviewedJournals(pending);
                return new(value, Hash(bytes));
            }
            // Hold the destination from compare through commit. A separate lock file
            // cannot protect against an external editor. Deny reads too, so readers
            // retry rather than observing an in-progress JSON write.
            await using var target = new FileStream(workspace.Resolve(RelativePath), FileMode.Open, FileAccess.ReadWrite, FileShare.None, 8192, FileOptions.Asynchronous);
            if (target.Length > MaximumBytes) throw new InvalidDataException("The arrangement file exceeds the 2 MB limit.");
            var current = new byte[checked((int)target.Length)];
            await target.ReadExactlyAsync(current, token).ConfigureAwait(false);
            if (Hash(current) != expected.Fingerprint)
                throw new IOException("The arrangement file changed outside this window. Reload its layouts before saving; the external file has been retained.");
            var journalRelative = RelativePath + ".pending-" + Guid.NewGuid().ToString("N") + ".json";
            var journal = workspace.Resolve(journalRelative);
            if (expected.FromBackup)
            {
                var retained = workspace.Resolve(RelativePath + ".unreadable-" + Guid.NewGuid().ToString("N"));
                await DocumentStore.WriteDurableAsync(retained, current, token).ConfigureAwait(false);
            }
            else
            {
                await DocumentStore.WriteDurableAsync(temporary, current, token).ConfigureAwait(false);
                File.Move(temporary, workspace.Resolve(RelativePath + ".bak"), true);
            }
            // Establish recoverable prior bytes before a journal can block normal
            // loading, including cancellation during the first update's journal.
            await DocumentStore.WriteDurableAsync(journal, JsonSerializer.SerializeToUtf8Bytes(new { SchemaVersion = 1, Original = current, Intended = bytes }), token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            try
            {
                // Like document/export commits, cancellation ends before mutation.
                // A crash retains both versions in the journal and the prior backup.
                target.Position = 0; await target.WriteAsync(bytes, CancellationToken.None).ConfigureAwait(false);
                target.SetLength(bytes.Length); target.Flush(true);
            }
            catch
            {
                try { target.Position = 0; await target.WriteAsync(current, CancellationToken.None).ConfigureAwait(false); target.SetLength(current.Length); target.Flush(true); }
                catch { /* Retain the durable original/intended versions for recovery. */ }
                throw;
            }
            ArchiveReviewedJournals(pending);
            File.Delete(workspace.Resolve(journalRelative));
            return new(value, Hash(bytes));
        }
        finally { if (File.Exists(temporary)) { workspace.Resolve(temporaryRelative); File.Delete(temporary); } }
    }
    private static IOException InterruptedSave() => new("An interrupted arrangement save needs review. Load backup explicitly before saving a recovered layout; pending recovery evidence has been retained.");
    private void RequireCompletedSave() { if (PendingJournals().Length > 0) throw InterruptedSave(); }
    private string[] PendingJournals()
    {
        var folder = Path.GetDirectoryName(workspace.Resolve(RelativePath))!;
        if (!Directory.Exists(folder)) return [];
        var pending = Directory.EnumerateFiles(folder, "diagram-layouts.json.pending-*.json").Take(201)
            .Select(path => ".lens-docs/" + Path.GetFileName(path)).ToArray();
        if (pending.Length > 200) throw new IOException("Too many pending arrangement recovery files. Review the workspace metadata before saving.");
        foreach (var relative in pending) workspace.Resolve(relative);
        return pending;
    }
    private void ArchiveReviewedJournals(IEnumerable<string> pending)
    {
        // Explicit backup recovery acknowledges these pre-existing journals. Keep
        // their bytes as evidence; never parse them as save instructions.
        foreach (var relative in pending)
            File.Move(workspace.Resolve(relative), workspace.Resolve(relative.Replace(".pending-", ".reviewed-", StringComparison.Ordinal)), false);
    }
    private async Task<byte[]?> ReadBytesAsync(string relative, CancellationToken token)
    {
        var path = workspace.Resolve(relative);
        if (!File.Exists(path)) return null;
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.Asynchronous);
        if (stream.Length > MaximumBytes) throw new InvalidDataException("The arrangement file exceeds the 2 MB limit.");
        var bytes = new byte[checked((int)stream.Length)]; await stream.ReadExactlyAsync(bytes, token).ConfigureAwait(false); return bytes;
    }
    private ArrangementFile Parse(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes, new() { MaxDepth = 12 });
        RejectDuplicates(document.RootElement);
        var value = JsonSerializer.Deserialize<ArrangementFile>(bytes, Options) ?? throw new InvalidDataException("The arrangement file is empty.");
        Validate(value); return value;
    }
    private static void RejectDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            { if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate arrangement JSON properties are not supported."); RejectDuplicates(property.Value); }
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var child in element.EnumerateArray()) RejectDuplicates(child);
    }
    private void Validate(ArrangementFile file)
    {
        if (file.SchemaVersion != 1 || file.Layouts is null || file.Layouts.Count > 200) throw new InvalidDataException("Unsupported arrangement format or record count.");
        var ids = new HashSet<Guid>();
        foreach (var layout in file.Layouts)
        {
            if (layout is null || layout.Id == Guid.Empty || !ids.Add(layout.Id) || layout.Document is null || layout.Document.Length > 1024
                || layout.Document.Contains('\\') || layout.Document.Split('/').Any(part => part is "" or "." or "..")
                || !WorkspaceService.IsDocument(layout.Document) || layout.Block is null || layout.Block.Family is not ("ER" or "Flowchart")
                || !Digest(layout.Block.SourceFingerprint) || !Digest(layout.Block.ContextFingerprint) || layout.Positions is null)
                throw new InvalidDataException("The arrangement contains invalid document or block identities.");
            workspace.Resolve(layout.Document);
            _ = new ArrangementState(layout.Positions, layout.Strategy, layout.LayoutVariant);
        }
    }
    private static bool Digest(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
