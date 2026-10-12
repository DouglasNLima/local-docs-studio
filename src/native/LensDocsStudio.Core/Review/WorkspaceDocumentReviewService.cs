using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Workspace;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LensDocsStudio.Core.Review;

/// <summary>Checks referenced workspace documents without following network links or writing source.</summary>
public sealed class WorkspaceDocumentReviewService(DocumentStore documents, string? excludedDirectory = null)
{
    public const int MaximumTargets = 100;
    public const int MaximumTargetCharacters = 16 * 1024 * 1024;
    private sealed record Target(HashSet<string>? Anchors, string? Problem = null, bool Unchecked = false);

    public async Task<DocumentReviewResult> AnalysePathAsync(string source, string sourcePath, string workspaceRoot,
        IReadOnlyDictionary<string, string>? editedDocuments = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WorkspacePath workspace;
        try { workspace = new(workspaceRoot); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            var result = await Task.Run(() => new DocumentReviewService().Analyse(source,
                Path.GetExtension(sourcePath).ToLowerInvariant() is ".mmd" or ".mermaid", cancellationToken), cancellationToken);
            var note = new DocumentReviewIssue("Workspace links", ReviewSeverity.Warning, "The workspace is unavailable or unsafe to inspect.",
                "Only current-document checks ran. Reopen the workspace before checking its links.", 0, 0, 1, 1);
            return result with { Issues = new[] { note }.Concat(result.Issues).Take(DocumentReviewService.MaximumIssues).ToArray(),
                Limited = result.Limited || result.Issues.Count == DocumentReviewService.MaximumIssues, WorkspaceLinks = WorkspaceLinkReviewState.Incomplete };
        }
        return await AnalyseAsync(source, sourcePath, workspace, editedDocuments, cancellationToken);
    }

    public async Task<DocumentReviewResult> AnalyseAsync(string source, string sourcePath, WorkspacePath workspace,
        IReadOnlyDictionary<string, string>? editedDocuments = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (source.Length > DocumentReviewService.MaximumSourceCharacters) throw new IOException("Document review supports up to 4 million source characters.");
        sourcePath = workspace.Resolve(Path.GetRelativePath(workspace.Root, Path.GetFullPath(sourcePath)));
        var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in editedDocuments ?? new Dictionary<string, string>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = Path.GetFullPath(pair.Key);
            if (!WorkspacePath.Contains(workspace.Root, path) || excludedDirectory is not null && WorkspacePath.ContainsOrEquals(excludedDirectory, path)) continue;
            path = workspace.Resolve(Path.GetRelativePath(workspace.Root, path));
            if (!overrides.TryAdd(path, pair.Value)) throw new IOException("More than one open draft has the same review path.");
        }
        var standalone = Path.GetExtension(sourcePath).ToLowerInvariant() is ".mmd" or ".mermaid";
        var prepared = await Task.Run(() =>
        {
            var parsed = new MarkdownService().Parse(source, standalone);
            return (Parsed: parsed, Review: new DocumentReviewService().AnalyseParsed(parsed, standalone, cancellationToken));
        }, cancellationToken);
        var issues = prepared.Review.Issues.ToList(); var limited = prepared.Review.Limited; var complete = true;
        var lineStarts = DocumentReviewService.SourceLines(source, cancellationToken);
        var targets = new Dictionary<string, Target>(StringComparer.OrdinalIgnoreCase);
        long characters = 0; var targetBudgetReached = false;
        WikiLinkIndex? wikiIndex = null; var wikiScanIncomplete = false;
        if (prepared.Parsed.Syntax.Descendants<WikiLinkInline>().Any())
        {
            var listing = await new WorkspaceService().EnumerateAsync(workspace, cancellationToken, excludedDirectory: excludedDirectory);
            IEnumerable<string> Paths(IEnumerable<WorkspaceEntry> entries)
            {
                foreach (var entry in entries)
                    if (entry.IsDirectory) { foreach (var path in Paths(entry.Children)) yield return path; }
                    else if (WorkspaceService.IsDocument(entry.Path)) yield return Path.GetRelativePath(workspace.Root, entry.Path).Replace('\\', '/');
            }
            wikiIndex = new(Paths(listing.Entries).Append(Path.GetRelativePath(workspace.Root, sourcePath).Replace('\\', '/')));
            wikiScanIncomplete = listing.Warnings.Count > 0; if (wikiScanIncomplete) complete = false;
        }
        foreach (var link in prepared.Parsed.Syntax.Descendants().OfType<Inline>().Where(node => node is LinkInline or WikiLinkInline))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string reference;
            if (link is WikiLinkInline wiki)
            {
                var resolved = wikiIndex!.Resolve(wiki.Target, Path.GetRelativePath(workspace.Root, sourcePath).Replace('\\', '/'));
                if (resolved.Path is null)
                {
                    Add(wiki, resolved.Error ?? "This wikilink could not be resolved.",
                        resolved.Candidates.Count > 0 ? "Choose an explicit workspace path: " + string.Join(", ", resolved.Candidates.Take(5))
                            : "Choose an existing workspace document." + (wikiScanIncomplete ? " Some folders are unavailable." : ""), wikiScanIncomplete);
                    continue;
                }
                reference = MarkdownLinks.RelativeReference(sourcePath, workspace.Resolve(resolved.Path))
                    + (resolved.Fragment.Length > 0 ? "#" + Uri.EscapeDataString(resolved.Fragment) : "");
            }
            else if (link is LinkInline { IsImage: false, Url: { Length: > 0 } url } && !url.StartsWith('#')) reference = url;
            else continue;
            if (Uri.TryCreate(reference, UriKind.Absolute, out var external))
            {
                if (external.Scheme is not ("https" or "http" or "mailto"))
                    Add(link, "This link uses a scheme that native document navigation does not open.", "Use a relative workspace document link or an explicit HTTP, HTTPS or mail link.", true);
                continue;
            }
            string path;
            try { path = workspace.ResolveLink(sourcePath, reference); }
            catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
            {
                Add(link, "This local link could not be checked within the workspace.", "Use a relative path inside the open workspace; junctions and ambiguous paths are not followed.", true); continue;
            }
            if (excludedDirectory is not null && WorkspacePath.ContainsOrEquals(excludedDirectory, path))
            { Add(link, "This link points into private application state and was not checked.", "Keep documentation links within portable workspace content.", true); continue; }
            if (!WorkspaceService.IsDocument(path)) continue;
            if (!targets.TryGetValue(path, out var target))
            {
                if (targets.Count == MaximumTargets)
                {
                    limited = true; Add(link, "Workspace link review reached its 100-document limit.", "Review a smaller set of linked documents; remaining links were not checked.", true); break;
                }
                target = await ReadTarget(path); targets.Add(path, target);
            }
            if (target.Problem is not null)
            {
                Add(link, target.Problem, target.Unchecked ? "Open the target to inspect the cause, then run review again." : "Load or create the intended document, or update this link.", target.Unchecked);
                if (targetBudgetReached) break;
                continue;
            }
            var hash = reference.IndexOf('#');
            if (hash >= 0 && hash + 1 < reference.Length && !target.Anchors!.Contains(Uri.UnescapeDataString(reference[(hash + 1)..])))
                Add(link, "The linked section is missing from its workspace document.", "Choose an existing heading or named anchor in the target's current text.", false);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return prepared.Review with
        {
            Issues = issues.OrderBy(issue => issue.Severity).ThenBy(issue => issue.Start).ToArray(), Limited = limited,
            WorkspaceLinks = complete ? WorkspaceLinkReviewState.Complete : WorkspaceLinkReviewState.Incomplete
        };

        async Task<Target> ReadTarget(string path)
        {
            try
            {
                string text;
                if (path.Equals(sourcePath, StringComparison.OrdinalIgnoreCase)) text = source;
                else if (overrides.TryGetValue(path, out var edited)) text = edited;
                else text = (await documents.ReadAsync(path, cancellationToken)).Text;
                characters += text.Length;
                if (characters > MaximumTargetCharacters)
                { limited = true; complete = false; targetBudgetReached = true; return new(null, "Workspace link review reached its 16-million-character limit; remaining links were not checked.", true); }
                if (text.Length > DocumentReviewService.MaximumSourceCharacters)
                { complete = false; return new(null, "The linked document exceeds the review source limit and was not checked.", true); }
                var targetDocument = path.Equals(sourcePath, StringComparison.OrdinalIgnoreCase) ? prepared.Parsed
                    : await Task.Run(() => new MarkdownService().Parse(text, Path.GetExtension(path).ToLowerInvariant() is ".mmd" or ".mermaid"), cancellationToken);
                return new(targetDocument.Targets.Select(anchor => anchor.Id).ToHashSet(StringComparer.Ordinal));
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                if (Directory.Exists(workspace.Root)) return new(null, "The linked workspace document is missing.");
                complete = false; return new(null, "The workspace is unavailable; this link was not checked.", true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.DecoderFallbackException)
            { complete = false; return new(null, "The linked document could not be read safely and was not checked.", true); }
        }
        void Add(Inline link, string message, string suggestion, bool uncheckedTarget)
        {
            if (uncheckedTarget) complete = false;
            if (issues.Count == DocumentReviewService.MaximumIssues) { limited = true; return; }
            var start = Math.Clamp(link.Span.Start, 0, source.Length); var length = Math.Clamp(link.Span.Length, 0, source.Length - start);
            var line = lineStarts.BinarySearch(start); if (line < 0) line = ~line - 1;
            issues.Add(new("Workspace links", ReviewSeverity.Warning, message, suggestion, start, length, line + 1, start - lineStarts[line] + 1));
        }
    }
}
