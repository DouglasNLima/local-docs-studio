using LensDocsStudio.Core.Documents;
using System.Text.Json;

namespace LensDocsStudio.Core.Workspace;

/// <summary>Exclusive writes with a durable pre-write recovery record. Existing files retain their identity and ACLs.</summary>
public sealed class DocumentStore(string recoveryDirectory)
{
    public const int MaximumDocumentBytes = 32 * 1024 * 1024;
    private readonly SemaphoreSlim writeGate = new(1, 1);
    public string RecoveryDirectory { get; } = Path.GetFullPath(recoveryDirectory);

    public async Task<DocumentSnapshot> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        path = Path.GetFullPath(path);
        WorkspacePath.RejectReparsePoints(path);
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
        if (file.Length > MaximumDocumentBytes) throw new IOException("The document exceeds the 32 MB editing limit.");
        var bytes = new byte[checked((int)file.Length)];
        await file.ReadExactlyAsync(bytes, cancellationToken);
        return DocumentSnapshot.FromBytes(path, bytes);
    }

    public async Task<DocumentSnapshot> SaveAsync(string path, string text, DocumentSnapshot? expected,
        CancellationToken cancellationToken = default)
    {
        path = Path.GetFullPath(path);
        WorkspacePath.RejectReparsePoints(path);
        if (expected is not null && !string.Equals(path, expected.Path, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The saved snapshot belongs to another document.");
        var bytes = DocumentSnapshot.Encode(text, expected?.Utf8Bom ?? false);
        if (bytes.Length > MaximumDocumentBytes) throw new IOException("The document exceeds the 32 MB editing limit.");
        await writeGate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(RecoveryDirectory);
            // Recovery holds both versions before any mutation. No background task deletes a user's draft.
            var journal = Path.Combine(RecoveryDirectory, Guid.NewGuid().ToString("N") + ".pending.json");
            WorkspacePath.RejectReparsePoints(path);
            if (expected is null)
            {
                var staged = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    await WriteDurableAsync(staged, bytes, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    File.Move(staged, path, false);
                }
                finally { if (File.Exists(staged)) File.Delete(staged); }
            }
            else
            {
                // Deny concurrent writers and replacement/rename while checking and committing.
                await using var target = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 65536, FileOptions.Asynchronous);
                if (target.Length > MaximumDocumentBytes) throw new DocumentConflictException("The file changed outside Lens Docs Studio.");
                var original = new byte[checked((int)target.Length)];
                await target.ReadExactlyAsync(original, cancellationToken);
                if (DocumentSnapshot.Hash(original) != expected.Fingerprint)
                    throw new DocumentConflictException("The file changed outside Lens Docs Studio. Reload it or save your edits to a new file.");
                var record = JsonSerializer.SerializeToUtf8Bytes(new PendingWrite(1, path, original, bytes, DateTimeOffset.UtcNow));
                await WriteDurableAsync(journal, record, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    // Cancellation is honoured before commit, never in the middle of a source-file write.
                    target.Position = 0;
                    await target.WriteAsync(bytes, CancellationToken.None);
                    target.SetLength(bytes.Length);
                    target.Flush(true);
                }
                catch
                {
                    try
                    {
                        target.Position = 0;
                        await target.WriteAsync(original, CancellationToken.None);
                        target.SetLength(original.Length);
                        target.Flush(true);
                    }
                    catch { /* The durable journal retains both versions for explicit recovery. */ }
                    throw;
                }
                File.Delete(journal);
            }
            return DocumentSnapshot.FromBytes(path, bytes);
        }
        finally { writeGate.Release(); }
    }

    internal static async Task WriteDurableAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous);
        await file.WriteAsync(bytes, cancellationToken);
        file.Flush(true);
    }

    public async Task<IReadOnlyList<PendingWrite>> ReadPendingWritesAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(RecoveryDirectory)) return [];
        var result = new List<PendingWrite>();
        foreach (var file in Directory.EnumerateFiles(RecoveryDirectory, "*.pending.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (new FileInfo(file).Length > 100 * 1024 * 1024) continue;
            var record = JsonSerializer.Deserialize<PendingWrite>(await File.ReadAllBytesAsync(file, cancellationToken));
            if (record is { SchemaVersion: 1 }) result.Add(record);
        }
        return result;
    }
}

public sealed record PendingWrite(int SchemaVersion, string Path, byte[] Original, byte[] Intended, DateTimeOffset CreatedAt);
