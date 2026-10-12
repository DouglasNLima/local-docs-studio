using System.Text.Json;
using LensDocsStudio.Core.Documents;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Editing;

namespace LensDocsStudio.Core.Workspace;

public sealed record DocumentLinkChange(DocumentSnapshot Original, string UpdatedText, int ChangedLinks);
public sealed record WorkspaceMovePlan(DocumentSnapshot Source, string Destination, string UpdatedSource,
    IReadOnlyList<DocumentLinkChange> OtherDocuments, IReadOnlyList<string> Warnings);
public sealed record WorkspaceMoveJournal(int SchemaVersion, string Workspace, WorkspaceMovePlan Plan, DateTimeOffset CreatedAt);
public sealed record WorkspaceMoveResult(DocumentSnapshot Moved, IReadOnlyList<DocumentSnapshot> UpdatedDocuments);

/// <summary>Plans source-preserving link edits and retains all original text before a multi-file move.</summary>
public sealed class WorkspaceMoveService(DocumentStore documents, string recoveryDirectory)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private string RecoveryDirectory { get; } = Path.GetFullPath(recoveryDirectory);

    public async Task<WorkspaceMovePlan> PlanAsync(WorkspacePath workspace, string sourcePath, string relativeDestination, CancellationToken cancellationToken = default)
    {
        sourcePath = workspace.Resolve(Path.GetRelativePath(workspace.Root, sourcePath));
        var destination = workspace.Resolve(relativeDestination);
        if (sourcePath.Equals(destination, StringComparison.OrdinalIgnoreCase)) throw new IOException("Choose a different document filename or folder.");
        if (!WorkspaceService.IsDocument(destination)) throw new IOException("Keep a supported Markdown, Mermaid or text extension.");
        if (File.Exists(destination) || Directory.Exists(destination)) throw new IOException("The destination already exists; no document will be replaced.");
        if (!Directory.Exists(Path.GetDirectoryName(destination))) throw new IOException("Create the destination folder before moving the document.");
        var source = await documents.ReadAsync(sourcePath, cancellationToken);
        var listing = await new WorkspaceService().EnumerateAsync(workspace, cancellationToken);
        var wikiIndex = new WikiLinkIndex(Flatten(listing.Entries).Select(entry => Path.GetRelativePath(workspace.Root, entry.Path).Replace('\\', '/')));
        var warnings = listing.Warnings.ToList();
        var changes = new List<DocumentLinkChange>();
        var updatedSource = source.Text;
        long totalCharacters = 0;
        foreach (var path in Flatten(listing.Entries).Select(e => e.Path).Append(sourcePath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            DocumentSnapshot snapshot;
            try { snapshot = path.Equals(sourcePath, StringComparison.OrdinalIgnoreCase) ? source : await documents.ReadAsync(path, cancellationToken); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.DecoderFallbackException)
            { warnings.Add("Links could not be checked in " + Path.GetRelativePath(workspace.Root, path) + ": " + ex.Message); continue; }
            totalCharacters += snapshot.Text.Length;
            if (totalCharacters > 64 * 1024 * 1024) throw new IOException("Workspace link planning is limited to 64 million source characters.");
            var movingSource = path.Equals(sourcePath, StringComparison.OrdinalIgnoreCase);
            var replacements = new List<(MarkdownLink Link, string Reference)>();
            foreach (var link in await Task.Run(() => MarkdownLinks.Read(snapshot.Text), cancellationToken))
            {
                if (Uri.TryCreate(link.Reference, UriKind.Absolute, out _) || link.Reference.StartsWith('#')) continue;
                try
                {
                    var target = workspace.ResolveLink(path, link.Reference);
                    var pointsToMoved = target.Equals(sourcePath, StringComparison.OrdinalIgnoreCase);
                    if (!movingSource && !pointsToMoved) continue;
                    var fragment = link.Reference.Contains('#') ? "#" + link.Reference.Split('#', 2)[1] : "";
                    replacements.Add((link, MarkdownLinks.RelativeReference(movingSource ? destination : path, pointsToMoved ? destination : target) + fragment));
                }
                catch (IOException) { warnings.Add("An out-of-workspace or unsupported link was left unchanged in " + Path.GetRelativePath(workspace.Root, path)); }
            }
            var wikiLinks = Path.GetExtension(path).ToLowerInvariant() is ".mmd" or ".mermaid" ? []
                : await Task.Run(() => WikiLinks.Read(snapshot.Text), cancellationToken);
            foreach (var wiki in wikiLinks)
            {
                var resolved = wikiIndex.Resolve(wiki.Target, Path.GetRelativePath(workspace.Root, path).Replace('\\', '/'));
                if (resolved.Path is null)
                {
                    warnings.Add("An unresolved or ambiguous wikilink was left unchanged in " + Path.GetRelativePath(workspace.Root, path));
                    continue;
                }
                var target = workspace.Resolve(resolved.Path);
                var pointsToMoved = target.Equals(sourcePath, StringComparison.OrdinalIgnoreCase);
                if (!movingSource && !pointsToMoved) continue;
                var nextPath = Path.GetRelativePath(workspace.Root, pointsToMoved ? destination : target).Replace('\\', '/');
                var reference = WikiLinkCompletionService.EscapeTarget(nextPath) + (resolved.Fragment.Length > 0 ? "#" + Uri.EscapeDataString(resolved.Fragment) : "");
                replacements.Add((new(wiki.Target, false, wiki.TargetSpan.Start, wiki.TargetSpan.Length), reference));
            }
            var updated = MarkdownLinks.Rewrite(snapshot.Text, replacements);
            if (movingSource) updatedSource = updated;
            else if (updated != snapshot.Text) changes.Add(new(snapshot, updated, replacements.Count));
        }
        return new(source, destination, updatedSource, changes, warnings.Distinct().ToArray());
    }

    public async Task<WorkspaceMoveResult> ApplyAsync(WorkspacePath workspace, WorkspaceMovePlan plan, bool updateLinks, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            workspace.Resolve(Path.GetRelativePath(workspace.Root, plan.Source.Path));
            workspace.Resolve(Path.GetRelativePath(workspace.Root, plan.Destination));
            if (File.Exists(plan.Destination) || Directory.Exists(plan.Destination)) throw new IOException("The destination appeared after planning; nothing was replaced.");
            var changes = updateLinks ? plan.OtherDocuments : [];
            foreach (var snapshot in changes.Select(c => c.Original).Prepend(plan.Source))
            {
                workspace.Resolve(Path.GetRelativePath(workspace.Root, snapshot.Path));
                if ((await documents.ReadAsync(snapshot.Path, cancellationToken)).Fingerprint != snapshot.Fingerprint)
                    throw new DocumentConflictException("A document changed after the move was planned: " + Path.GetRelativePath(workspace.Root, snapshot.Path));
            }
            Directory.CreateDirectory(RecoveryDirectory);
            var journal = Path.Combine(RecoveryDirectory, Guid.NewGuid().ToString("N") + ".move.json");
            var selected = updateLinks ? plan : plan with { UpdatedSource = plan.Source.Text, OtherDocuments = [] };
            var recoveryCharacters = (long)selected.Source.Text.Length + (selected.Source.Text == selected.UpdatedSource ? 0 : selected.UpdatedSource.Length) +
                selected.OtherDocuments.Sum(c => (long)c.Original.Text.Length + c.UpdatedText.Length);
            if (recoveryCharacters > 32 * 1024 * 1024) throw new IOException("This move exceeds the recovery text limit. Move without link updates or reduce the affected workspace.");
            var recoveryBytes = await Task.Run(() => JsonSerializer.SerializeToUtf8Bytes(new WorkspaceMoveJournal(1, workspace.Root, selected, DateTimeOffset.UtcNow)), cancellationToken);
            if (recoveryBytes.Length > 128 * 1024 * 1024) throw new IOException("This move exceeds the recovery record limit. No files have been changed.");
            await DocumentStore.WriteDurableAsync(journal, recoveryBytes, cancellationToken);
            var completed = new List<(DocumentSnapshot Original, DocumentSnapshot Saved)>();
            var moved = false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                WorkspacePath.RejectReparsePoints(plan.Source.Path); WorkspacePath.RejectReparsePoints(plan.Destination);
                File.Move(plan.Source.Path, plan.Destination, false); moved = true;
                var current = plan.Source with { Path = plan.Destination };
                // Recheck the moved file before any write: concurrent external edits must survive.
                if ((await documents.ReadAsync(plan.Destination, cancellationToken)).Fingerprint != current.Fingerprint)
                    throw new DocumentConflictException("The moved file was modified externally; its current contents have been preserved.");
                if (selected.UpdatedSource != current.Text)
                {
                    var saved = await documents.SaveAsync(current.Path, selected.UpdatedSource, current, cancellationToken);
                    completed.Add((current, saved)); current = saved;
                }
                var updated = new List<DocumentSnapshot>();
                foreach (var change in changes)
                {
                    var saved = await documents.SaveAsync(change.Original.Path, change.UpdatedText, change.Original, cancellationToken);
                    completed.Add((change.Original, saved)); updated.Add(saved);
                }
                File.Delete(journal);
                return new(current, updated);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                var restored = true;
                foreach (var (original, saved) in completed.AsEnumerable().Reverse())
                {
                    try { await documents.SaveAsync(original.Path, original.Text, saved, CancellationToken.None); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { restored = false; }
                }
                if (moved)
                {
                    try
                    {
                        WorkspacePath.RejectReparsePoints(plan.Source.Path); WorkspacePath.RejectReparsePoints(plan.Destination);
                        if (File.Exists(plan.Source.Path) || (await documents.ReadAsync(plan.Destination)).Fingerprint != plan.Source.Fingerprint) restored = false;
                        else File.Move(plan.Destination, plan.Source.Path, false);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { restored = false; }
                }
                if (restored) { File.Delete(journal); throw; }
                throw new IOException("The move could not be fully reversed because files changed or became unavailable. Current files were retained, and original and planned text are in " + journal, error);
            }
        }
        finally { gate.Release(); }
    }

    private static IEnumerable<WorkspaceEntry> Flatten(IEnumerable<WorkspaceEntry> entries) => entries.SelectMany(e => e.IsDirectory ? Flatten(e.Children) : [e]);
}
