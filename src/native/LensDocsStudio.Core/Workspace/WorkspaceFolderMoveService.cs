using System.Security.Cryptography;
using System.Text.Json;
using LensDocsStudio.Core.Documents;
using LensDocsStudio.Core.Editing;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LensDocsStudio.Core.Workspace;

public sealed record FolderMoveEntry(string RelativePath, bool IsDirectory, long Length, string Fingerprint);
public sealed record FolderDocumentChange(DocumentSnapshot Original, string Destination, string UpdatedText, int ChangedLinks);
public sealed record WorkspaceFolderMovePlan(string Source, string Destination, IReadOnlyList<FolderMoveEntry> Entries,
    IReadOnlyList<FolderDocumentChange> Documents, IReadOnlyList<string> Warnings);
public sealed record WorkspaceFolderMoveJournal(int SchemaVersion, string Workspace, WorkspaceFolderMovePlan Plan, DateTimeOffset CreatedAt);
public sealed record WorkspaceFolderMoveResult(IReadOnlyList<DocumentSnapshot> Documents);

/// <summary>Moves an entire reviewed folder, including assets, with guarded document edits and retained recovery text.</summary>
public sealed class WorkspaceFolderMoveService(DocumentStore documents, string recoveryDirectory, string? excludedDirectory = null)
{
    public const int MaximumEntries = 5000;
    public const long MaximumBytes = 256L * 1024 * 1024;
    private readonly SemaphoreSlim gate = new(1, 1);
    private string RecoveryDirectory { get; } = Path.GetFullPath(recoveryDirectory);

    public async Task<WorkspaceFolderMovePlan> PlanAsync(WorkspacePath workspace, string sourcePath, string relativeDestination, CancellationToken cancellationToken = default)
    {
        var source = workspace.Resolve(Path.GetRelativePath(workspace.Root, sourcePath)); var destination = workspace.Resolve(relativeDestination);
        if (Path.GetDirectoryName(source)!.Equals(Path.GetDirectoryName(destination), StringComparison.OrdinalIgnoreCase))
            destination = Path.Combine(Path.GetDirectoryName(source)!, Path.GetFileName(destination));
        ValidateMove(workspace, source, destination);
        var entries = await InventoryAsync(source, cancellationToken);
        var listing = await new WorkspaceService().EnumerateAsync(workspace, cancellationToken, excludedDirectory: excludedDirectory);
        if (listing.Warnings.Count > 0) throw new IOException("The workspace could not be fully checked for folder links. No files were moved. " + listing.Warnings[0]);
        var paths = Flatten(listing.Entries).Concat(entries.Where(entry => !entry.IsDirectory).Select(entry => Path.Combine(source, entry.RelativePath)))
            .Where(WorkspaceService.IsDocument).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (paths.Length > 1000) throw new IOException("Folder link review is limited to 1,000 workspace documents.");
        var wiki = new WikiLinkIndex(paths.Select(path => Path.GetRelativePath(workspace.Root, path).Replace('\\', '/')));
        var changes = new List<FolderDocumentChange>(); var warnings = new List<string>(); long characters = 0;
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested(); ValidatePublicPath(workspace, path);
            var snapshot = await documents.ReadAsync(path, cancellationToken);
            characters += snapshot.Text.Length;
            if (characters > 32 * 1024 * 1024) throw new IOException("Folder link review is limited to 32 million source characters.");
            var relocated = Relocate(path, source, destination); var moving = path != relocated;
            var replacements = new List<(MarkdownLink Link, string Reference)>();
            if (Path.GetExtension(path).ToLowerInvariant() is not (".mmd" or ".mermaid"))
            {
                var rawHtml = await Task.Run(() => new MarkdownService().Parse(snapshot.Text).Syntax.Descendants()
                    .Select(node => node is HtmlBlock block ? block.Lines.ToString() : node is HtmlInline inline ? inline.Tag : "")
                    .Any(html => System.Text.RegularExpressions.Regex.IsMatch(html, @"\b(?:href|src)\s*=", System.Text.RegularExpressions.RegexOptions.IgnoreCase)), cancellationToken);
                if (rawHtml) Warn("Raw HTML references need manual review after a folder move: " + Path.GetRelativePath(workspace.Root, path));
                foreach (var link in await Task.Run(() => MarkdownLinks.Read(snapshot.Text), cancellationToken))
                {
                    if (Uri.TryCreate(link.Reference, UriKind.Absolute, out _) || link.Reference.StartsWith('#')) continue;
                    try
                    {
                        var target = workspace.ResolveLink(path, link.Reference); ValidatePublicPath(workspace, target);
                        var nextTarget = Relocate(target, source, destination);
                        if (!moving && target == nextTarget) continue;
                        var fragment = link.Reference.Contains('#') ? "#" + link.Reference.Split('#', 2)[1] : "";
                        replacements.Add((link, MarkdownLinks.RelativeReference(relocated, nextTarget) + fragment));
                    }
                    catch (IOException) { Warn("An unsupported or private link remains unchanged in " + Path.GetRelativePath(workspace.Root, path)); }
                }
                foreach (var link in await Task.Run(() => WikiLinks.Read(snapshot.Text), cancellationToken))
                {
                    var resolved = wiki.Resolve(link.Target, Path.GetRelativePath(workspace.Root, path).Replace('\\', '/'));
                    if (resolved.Path is null) { Warn("An unresolved or ambiguous wikilink remains unchanged in " + Path.GetRelativePath(workspace.Root, path)); continue; }
                    var target = workspace.Resolve(resolved.Path); var nextTarget = Relocate(target, source, destination);
                    if (!moving && target == nextTarget) continue;
                    var reference = WikiLinkCompletionService.EscapeTarget(Path.GetRelativePath(workspace.Root, nextTarget).Replace('\\', '/'))
                        + (resolved.Fragment.Length == 0 ? "" : "#" + Uri.EscapeDataString(resolved.Fragment));
                    replacements.Add((new(link.Target, false, link.TargetSpan.Start, link.TargetSpan.Length), reference));
                }
            }
            var actual = replacements.Where(change => change.Link.Reference != change.Reference).ToArray();
            var updated = MarkdownLinks.Rewrite(snapshot.Text, actual);
            if (moving || updated != snapshot.Text) changes.Add(new(snapshot, relocated, updated, actual.Select(change => (change.Link.Start, change.Link.Length)).Distinct().Count()));
        }
        foreach (var change in changes.Where(change => WorkspacePath.Contains(source, change.Original.Path)))
        {
            var entry = entries.Single(entry => entry.RelativePath.Equals(Path.GetRelativePath(source, change.Original.Path), StringComparison.OrdinalIgnoreCase));
            if (entry.Fingerprint != change.Original.Fingerprint) throw new DocumentConflictException("A folder document changed during review. Review the move again.");
        }
        return new(source, destination, entries, changes, warnings.Distinct().ToArray());
        void Warn(string warning) { if (warnings.Count < 50) warnings.Add(warning); }
    }

    public async Task<WorkspaceFolderMoveResult> ApplyAsync(WorkspacePath workspace, WorkspaceFolderMovePlan plan, bool updateLinks, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            ValidateMove(workspace, plan.Source, plan.Destination);
            if (!plan.Entries.SequenceEqual(await InventoryAsync(plan.Source, cancellationToken))) throw new DocumentConflictException("The folder changed after review. Review the move again.");
            var selected = updateLinks ? plan.Documents : plan.Documents.Where(change => WorkspacePath.Contains(plan.Source, change.Original.Path))
                .Select(change => change with { UpdatedText = change.Original.Text, ChangedLinks = 0 }).ToArray();
            foreach (var change in selected)
            {
                ValidatePublicPath(workspace, change.Original.Path); ValidatePublicPath(workspace, change.Destination);
                if (change.Destination != Relocate(change.Original.Path, plan.Source, plan.Destination)) throw new IOException("The folder move has an inconsistent document destination.");
                if ((await documents.ReadAsync(change.Original.Path, cancellationToken)).Fingerprint != change.Original.Fingerprint)
                    throw new DocumentConflictException("A linked document changed after folder review: " + Path.GetRelativePath(workspace.Root, change.Original.Path));
            }
            if (selected.Sum(change => (long)change.Original.Text.Length + change.UpdatedText.Length) > 32 * 1024 * 1024)
                throw new IOException("The folder move exceeds its recovery text limit. No files were changed.");
            var journalBytes = await Task.Run(() => JsonSerializer.SerializeToUtf8Bytes(new WorkspaceFolderMoveJournal(1, workspace.Root,
                plan with { Documents = selected }, DateTimeOffset.UtcNow)), cancellationToken);
            if (journalBytes.Length > 128 * 1024 * 1024) throw new IOException("The folder move exceeds its recovery record limit. No files were changed.");
            Directory.CreateDirectory(RecoveryDirectory);
            var journal = Path.Combine(RecoveryDirectory, Guid.NewGuid().ToString("N") + ".folder-move.json");
            await DocumentStore.WriteDurableAsync(journal, journalBytes, cancellationToken);
            var moved = false; var completed = new List<(DocumentSnapshot Original, DocumentSnapshot Saved)>();
            try
            {
                cancellationToken.ThrowIfCancellationRequested(); ValidateMove(workspace, plan.Source, plan.Destination);
                Directory.Move(plan.Source, plan.Destination); moved = true;
                if (!plan.Entries.SequenceEqual(await InventoryAsync(plan.Destination, cancellationToken))) throw new DocumentConflictException("Folder contents changed during the move; current files were retained.");
                var updated = new List<DocumentSnapshot>();
                foreach (var change in selected)
                {
                    var current = change.Original with { Path = change.Destination };
                    if (current.Text != change.UpdatedText)
                    {
                        var saved = await documents.SaveAsync(current.Path, change.UpdatedText, current, cancellationToken);
                        completed.Add((current, saved)); current = saved;
                    }
                    updated.Add(current);
                }
                File.Delete(journal); return new(updated);
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
                        WorkspacePath.RejectReparsePoints(plan.Source); WorkspacePath.RejectReparsePoints(plan.Destination);
                        if (DestinationCollides(plan.Destination, plan.Source) || !plan.Entries.SequenceEqual(await InventoryAsync(plan.Destination, CancellationToken.None))) restored = false;
                        else Directory.Move(plan.Destination, plan.Source);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { restored = false; }
                }
                if (restored) { File.Delete(journal); throw; }
                throw new IOException("The folder move could not be fully reversed. Current files were retained; original and planned document text remains in " + journal, error);
            }
        }
        finally { gate.Release(); }
    }

    public static string Relocate(string path, string source, string destination) => WorkspacePath.ContainsOrEquals(source, path)
        ? Path.GetFullPath(Path.Combine(destination, Path.GetRelativePath(source, path))) : path;
    private void ValidateMove(WorkspacePath workspace, string source, string destination)
    {
        ValidatePublicPath(workspace, source); ValidatePublicPath(workspace, destination);
        if (!Directory.Exists(source)) throw new IOException("The selected folder is no longer available.");
        if (!IsCaseOnlyRename(source, destination) && (WorkspacePath.ContainsOrEquals(source, destination) || WorkspacePath.ContainsOrEquals(destination, source)))
            throw new IOException("Choose a separate destination outside the moved folder.");
        if (DestinationCollides(source, destination)) throw new IOException("The destination already exists; folders are never merged or replaced.");
        if (!Directory.Exists(Path.GetDirectoryName(destination))) throw new IOException("Create the destination's parent folder before reviewing the move.");
        if (excludedDirectory is not null && WorkspacePath.ContainsOrEquals(source, excludedDirectory)) throw new IOException("A folder containing private application state cannot be moved.");
    }
    private void ValidatePublicPath(WorkspacePath workspace, string path)
    {
        workspace.Resolve(Path.GetRelativePath(workspace.Root, path));
        if (excludedDirectory is not null && WorkspacePath.ContainsOrEquals(excludedDirectory, path)) throw new IOException("Private application state is excluded from folder moves.");
    }
    private static bool IsCaseOnlyRename(string source, string destination) => source.Equals(destination, StringComparison.OrdinalIgnoreCase)
        && !Path.GetFileName(source).Equals(Path.GetFileName(destination), StringComparison.Ordinal);
    private static bool DestinationCollides(string source, string destination)
    {
        if (File.Exists(destination)) return true;
        if (!Directory.Exists(destination)) return false;
        if (!IsCaseOnlyRename(source, destination)) return true;
        // Directory.Exists sees the source alias on Windows; an actual destination entry still collides.
        return Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(destination)!, Path.GetFileName(destination))
            .Any(path => Path.GetFileName(path).Equals(Path.GetFileName(destination), StringComparison.Ordinal));
    }
    private static async Task<IReadOnlyList<FolderMoveEntry>> InventoryAsync(string root, CancellationToken cancellationToken)
    {
        var entries = new List<FolderMoveEntry>(); var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long bytes = 0;
        async Task Walk(string directory, int depth)
        {
            cancellationToken.ThrowIfCancellationRequested(); WorkspacePath.RejectReparsePoints(directory);
            if (depth > 32) throw new IOException("The selected folder exceeds the 32-level review limit.");
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                cancellationToken.ThrowIfCancellationRequested(); WorkspacePath.RejectReparsePoints(path);
                if (entries.Count == MaximumEntries) throw new IOException("The selected folder exceeds the 5,000-entry review limit.");
                var relative = Path.GetRelativePath(root, path);
                if (!names.Add(relative)) throw new IOException("The folder contains names that differ only by case; review them separately before moving it.");
                if (Directory.Exists(path)) { entries.Add(new(relative, true, 0, "")); await Walk(path, depth + 1); }
                else
                {
                    await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
                    if (file.Length > MaximumBytes - bytes) throw new IOException("The selected folder exceeds the 256 MB review limit.");
                    bytes += file.Length;
                    entries.Add(new(relative, false, file.Length, Convert.ToHexString(await SHA256.HashDataAsync(file, cancellationToken))));
                }
            }
        }
        await Walk(root, 0); return entries.OrderBy(entry => entry.RelativePath, StringComparer.Ordinal).ToArray();
    }
    private static IEnumerable<string> Flatten(IEnumerable<WorkspaceEntry> entries) => entries.SelectMany(entry => entry.IsDirectory ? Flatten(entry.Children) : [entry.Path]);
}
