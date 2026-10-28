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
        var bytes = await ReadBytesAsync(RelativePath, token).ConfigureAwait(false);
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
        var current = await ReadBytesAsync(RelativePath, token).ConfigureAwait(false);
        if ((current is null ? null : Hash(current)) != expected.Fingerprint)
            throw new IOException("The arrangement file changed outside this window. Reload its layouts before saving; the external file has been retained.");
        var temporaryRelative = RelativePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var temporary = workspace.Resolve(temporaryRelative);
        try
        {
            await DocumentStore.WriteDurableAsync(temporary, bytes, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            workspace.Resolve(RelativePath); workspace.Resolve(RelativePath + ".bak");
            // Recheck after the potentially slow serialisation/flush, before the atomic publication.
            var latest = await ReadBytesAsync(RelativePath, token).ConfigureAwait(false);
            if ((latest is null ? null : Hash(latest)) != expected.Fingerprint) throw new IOException("The arrangement file changed during saving. Reload it before trying again.");
            if (expected.FromBackup && current is not null)
            {
                var retained = workspace.Resolve(RelativePath + ".unreadable-" + Guid.NewGuid().ToString("N"));
                await DocumentStore.WriteDurableAsync(retained, current, token).ConfigureAwait(false);
            }
            if (current is null) File.Move(temporary, destination, false);
            else File.Replace(temporary, destination, workspace.Resolve(RelativePath + ".bak"), true);
            return new(value, Hash(bytes));
        }
        finally { if (File.Exists(temporary)) { workspace.Resolve(temporaryRelative); File.Delete(temporary); } }
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
