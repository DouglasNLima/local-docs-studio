using System.Text.Json;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Core.Exports;

/// <summary>Commits exports with conflict detection and a durable recovery record containing both versions.</summary>
public sealed class ExportFileStore(string recoveryDirectory)
{
    public const int MaximumBytes = 512 * 1024 * 1024;

    public static async Task<byte[]?> ReadExistingAsync(string path, CancellationToken cancellationToken = default)
    {
        WorkspacePath.RejectReparsePoints(path);
        if (!File.Exists(path)) return null;
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
        if (file.Length > MaximumBytes) throw new IOException("The export destination exceeds the 512 MB replacement limit. Choose a new filename.");
        var bytes = new byte[checked((int)file.Length)];
        await file.ReadExactlyAsync(bytes, cancellationToken);
        return bytes;
    }

    public async Task WriteAsync(string path, byte[] bytes, byte[]? expected, CancellationToken cancellationToken = default)
    {
        path = Path.GetFullPath(path);
        WorkspacePath.RejectReparsePoints(path);
        if (bytes.Length > MaximumBytes || expected?.Length > MaximumBytes) throw new IOException("The export exceeds the 512 MB limit.");
        if (expected is null)
        {
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await DocumentStore.WriteDurableAsync(temporary, bytes, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporary, path, false);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return;
        }
        await using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 65536, true);
        if (file.Length != expected.Length) throw new IOException("The export destination changed. Choose a new filename.");
        var current = new byte[expected.Length];
        await file.ReadExactlyAsync(current, cancellationToken);
        if (!current.AsSpan().SequenceEqual(expected)) throw new IOException("The export destination changed. Choose a new filename.");
        WorkspacePath.RejectReparsePoints(recoveryDirectory);
        Directory.CreateDirectory(recoveryDirectory);
        var journal = Path.Combine(recoveryDirectory, Guid.NewGuid().ToString("N") + ".pending.json");
        await DocumentStore.WriteDurableAsync(journal, JsonSerializer.SerializeToUtf8Bytes(new PendingWrite(1, path, expected, bytes, DateTimeOffset.UtcNow)), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            file.Position = 0; await file.WriteAsync(bytes); file.SetLength(bytes.Length); file.Flush(true);
        }
        catch
        {
            try { file.Position = 0; await file.WriteAsync(expected); file.SetLength(expected.Length); file.Flush(true); }
            catch { /* The durable journal retains both versions for explicit recovery. */ }
            throw;
        }
        File.Delete(journal);
    }
}
