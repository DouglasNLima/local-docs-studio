using LensDocsStudio.Core.Documents;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Editing;

namespace LensDocsStudio.Core.Workspace;

/// <summary>Persists referenced local images before a Save as commits its Markdown.</summary>
public sealed class AssetTransferService
{
    public async Task<PreparedAssetTransfer> PrepareAsync(string text, string sourcePath, WorkspacePath sourceWorkspace,
        string targetPath, WorkspacePath targetWorkspace, CancellationToken cancellationToken = default, bool forceCopyImages = false, bool preserveDocumentLinks = false,
        string? excludedDirectory = null)
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
                // An unsaved document's asset staging directory is not the origin of its ordinary links.
                if (preserveDocumentLinks && !link.IsImage) continue;
                var original = sourceWorkspace.ResolveLink(sourcePath, link.Reference);
                string destination;
                if (WorkspacePath.Contains(targetWorkspace.Root, original) && (!link.IsImage || !forceCopyImages)) destination = original;
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
            if (!preserveDocumentLinks && Path.GetExtension(sourcePath).ToLowerInvariant() is not (".mmd" or ".mermaid"))
            {
                var wikiLinks = WikiLinks.Read(text);
                if (wikiLinks.Count > 0)
                {
                    var listing = await new WorkspaceService().EnumerateAsync(sourceWorkspace, cancellationToken, excludedDirectory: excludedDirectory);
                    IEnumerable<string> Paths(IEnumerable<WorkspaceEntry> entries)
                    {
                        foreach (var entry in entries)
                            if (entry.IsDirectory) { foreach (var path in Paths(entry.Children)) yield return path; }
                            else if (WorkspaceService.IsDocument(entry.Path)) yield return Path.GetRelativePath(sourceWorkspace.Root, entry.Path).Replace('\\', '/');
                    }
                    var index = new WikiLinkIndex(Paths(listing.Entries));
                    foreach (var wiki in wikiLinks)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (wiki.Target.StartsWith('#')) continue;
                        var result = index.Resolve(wiki.Target, Path.GetRelativePath(sourceWorkspace.Root, sourcePath).Replace('\\', '/'));
                        if (result.Path is null) continue;
                        var original = sourceWorkspace.Resolve(result.Path);
                        if (!WorkspacePath.Contains(targetWorkspace.Root, original)) { transfer.UncopiedDocumentLinks.Add("[[" + wiki.Target + "]]"); continue; }
                        var reference = WikiLinkCompletionService.EscapeTarget(Path.GetRelativePath(targetWorkspace.Root, original).Replace('\\', '/'))
                            + (result.Fragment.Length > 0 ? "#" + Uri.EscapeDataString(result.Fragment) : "");
                        changes.Add((new(wiki.Target, false, wiki.TargetSpan.Start, wiki.TargetSpan.Length), reference));
                    }
                }
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
