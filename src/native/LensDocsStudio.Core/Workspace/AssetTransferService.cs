using LensDocsStudio.Core.Documents;
using LensDocsStudio.Core.Parsing;

namespace LensDocsStudio.Core.Workspace;

/// <summary>Persists referenced local images before a Save as commits its Markdown.</summary>
public sealed class AssetTransferService
{
    public async Task<PreparedAssetTransfer> PrepareAsync(string text, string sourcePath, WorkspacePath sourceWorkspace,
        string targetPath, WorkspacePath targetWorkspace, CancellationToken cancellationToken = default)
    {
        sourceWorkspace.Resolve(Path.GetRelativePath(sourceWorkspace.Root, sourcePath));
        targetWorkspace.Resolve(Path.GetRelativePath(targetWorkspace.Root, targetPath));
        var transfer = new PreparedAssetTransfer(text);
        try
        {
            var changes = new List<(MarkdownLink Link, string Reference)>();
            var copied = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            long totalBytes = 0;
            foreach (var link in MarkdownLinks.Read(text))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Uri.TryCreate(link.Reference, UriKind.Absolute, out _) || link.Reference.StartsWith('#')) continue;
                var original = sourceWorkspace.ResolveLink(sourcePath, link.Reference);
                string destination;
                if (WorkspacePath.Contains(targetWorkspace.Root, original)) destination = original;
                else if (!link.IsImage) { transfer.UncopiedDocumentLinks.Add(link.Reference); continue; }
                else if (!copied.TryGetValue(original, out destination!))
                {
                    if (Path.GetExtension(original).ToLowerInvariant() is not (".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp"))
                        throw new IOException("Save as cannot copy this image format: " + link.Reference);
                    WorkspacePath.RejectReparsePoints(original);
                    await using var input = new FileStream(original, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
                    totalBytes += input.Length;
                    if (input.Length > 32 * 1024 * 1024 || totalBytes > 256 * 1024 * 1024) throw new IOException("Linked images exceed the Save as safety limit.");
                    var bytes = new byte[checked((int)input.Length)];
                    await input.ReadExactlyAsync(bytes, cancellationToken);
                    destination = targetWorkspace.Resolve("assets/images/" + Guid.NewGuid().ToString("N") + Path.GetExtension(original).ToLowerInvariant());
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    WorkspacePath.RejectReparsePoints(destination);
                    // Track each new asset; uncertain partial or externally changed files are retained.
                    transfer.Register(destination, bytes);
                    await DocumentStore.WriteDurableAsync(destination, bytes, cancellationToken);
                    copied.Add(original, destination);
                }
                var fragment = link.Reference.Contains('#') ? "#" + link.Reference.Split('#', 2)[1] : "";
                changes.Add((link, MarkdownLinks.RelativeReference(targetPath, destination) + fragment));
            }
            transfer.Text = MarkdownLinks.Rewrite(text, changes);
            return transfer;
        }
        catch { await transfer.DisposeAsync(); throw; }
    }
}

public sealed class PreparedAssetTransfer(string text) : IAsyncDisposable
{
    private readonly List<(string Path, string Fingerprint)> created = [];
    private bool committed;
    public string Text { get; internal set; } = text;
    public IReadOnlyList<string> CreatedPaths => created.Select(c => c.Path).ToArray();
    public List<string> UncopiedDocumentLinks { get; } = [];
    internal void Register(string path, byte[] bytes) => created.Add((path, DocumentSnapshot.Hash(bytes)));
    public void Commit() => committed = true;
    public async ValueTask DisposeAsync()
    {
        if (committed) return;
        foreach (var (path, fingerprint) in created)
        {
            try
            {
                WorkspacePath.RejectReparsePoints(path);
                if (File.Exists(path) && new FileInfo(path).Length <= 32 * 1024 * 1024 &&
                    DocumentSnapshot.Hash(await File.ReadAllBytesAsync(path)) == fingerprint) File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* Retain uncertain files; never remove an externally edited asset. */ }
        }
    }
}
