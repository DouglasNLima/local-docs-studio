using System.Text;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LensDocsStudio.Core.Workspace;

public enum WorkspaceMapLinkState { Resolved, Missing, Ambiguous, Unchecked, Unsupported }
public sealed record WorkspaceMapDocument(string Path, string Title, string Kind, string Source, int Incoming, int Outgoing, int Issues)
{
    public string AccessibleName => $"{Title} · {Path} · {Incoming} incoming · {Outgoing} outgoing · {Issues} notes";
}
public sealed record WorkspaceMapLink(string From, string Reference, string? To, string Fragment, string Label, string Kind,
    int Start, int Length, WorkspaceMapLinkState State, string Message)
{
    public string AccessibleName => $"{From} → {To ?? Reference} · {Message}";
}
public sealed record WorkspaceMapResult(string Root, IReadOnlyList<WorkspaceMapDocument> Documents, IReadOnlyList<WorkspaceMapLink> Links,
    IReadOnlyList<string> Warnings, bool Complete);
public sealed record WorkspaceMapDiagram(string Source, int Documents, int Connections, bool Limited);

/// <summary>Read-only, bounded document relationships from current drafts and explicit workspace files.</summary>
public sealed class WorkspaceMapService(DocumentStore documents)
{
    public const int MaximumDocuments = 1000;
    public const int MaximumDocumentCharacters = 2 * 1024 * 1024;
    public const int MaximumCharacters = 16 * 1024 * 1024;
    public const int MaximumLinks = 10000;

    public async Task<WorkspaceMapResult> BuildAsync(WorkspacePath workspace, IReadOnlyDictionary<string, string>? edited = null,
        string? excludedDirectory = null, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (excludedDirectory is not null && WorkspacePath.ContainsOrEquals(excludedDirectory, workspace.Root))
            throw new IOException("Private application state cannot be used as a workspace map.");
        var listing = await new WorkspaceService().EnumerateAsync(workspace, cancellationToken, excludedDirectory: excludedDirectory);
        var warnings = listing.Warnings.ToList(); var complete = warnings.Count == 0;
        var catalogueComplete = complete;
        var paths = Flatten(listing.Entries).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        var known = paths.ToDictionary(path => Relative(path), StringComparer.OrdinalIgnoreCase); var wiki = new WikiLinkIndex(known.Keys);
        var drafts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in edited ?? new Dictionary<string, string>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = Path.GetFullPath(item.Key);
            if (!WorkspacePath.Contains(workspace.Root, path) || excludedDirectory is not null && WorkspacePath.ContainsOrEquals(excludedDirectory, path)) continue;
            workspace.Resolve(Relative(path)); if (!drafts.TryAdd(path, item.Value)) throw new IOException("More than one draft has the same map path.");
        }
        var parsed = new Dictionary<string, ParsedDocument>(StringComparer.OrdinalIgnoreCase); var total = 0;
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested(); var relative = Relative(path);
            if (parsed.Count == MaximumDocuments) { Warn("The map reached its 1,000-document limit."); break; }
            progress?.Report("Reading workspace map: " + relative);
            try
            {
                workspace.Resolve(relative);
                var text = drafts.TryGetValue(path, out var current) ? current : (await documents.ReadAsync(path, cancellationToken)).Text;
                if (text.Length > MaximumDocumentCharacters) { Warn("Document exceeds the map source limit: " + relative); continue; }
                if (text.Length > MaximumCharacters - total) { Warn("The map reached its 16-million-character source limit."); break; }
                total += text.Length;
                parsed.Add(relative, await Task.Run(() => new MarkdownService().Parse(text, Path.GetExtension(path).ToLowerInvariant() is ".mmd" or ".mermaid"), cancellationToken));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException)
            { Warn("Document could not be read safely: " + relative); }
        }
        var links = new List<WorkspaceMapLink>();
        foreach (var (from, document) in parsed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var inline in document.Syntax.Descendants().OfType<Inline>().Where(inline => inline is WikiLinkInline or LinkInline { IsImage: false }))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (links.Count == MaximumLinks) { Warn("The map reached its 10,000-link limit."); break; }
                var reference = inline is WikiLinkInline w ? w.Target : ((LinkInline)inline).Url ?? "";
                if (reference.Length == 0) continue;
                string? to = null; var fragment = ""; var state = WorkspaceMapLinkState.Resolved; var message = "Resolved";
                if (inline is WikiLinkInline)
                {
                    var resolved = wiki.Resolve(reference, from); to = resolved.Path; fragment = resolved.Fragment;
                    if (!resolved.Found)
                    {
                        state = resolved.Candidates.Count > 0 ? WorkspaceMapLinkState.Ambiguous : WorkspaceMapLinkState.Missing;
                        message = resolved.Error ?? "The wiki target is missing.";
                    }
                    if (!catalogueComplete && state != WorkspaceMapLinkState.Ambiguous)
                    { state = WorkspaceMapLinkState.Unchecked; message = "The workspace catalogue is incomplete; this wiki target is unconfirmed."; }
                }
                else
                {
                    if (Uri.TryCreate(reference, UriKind.Absolute, out var external))
                    {
                        if (external.Scheme is "http" or "https" or "mailto") continue;
                        state = WorkspaceMapLinkState.Unsupported; message = "This link is not a supported workspace path.";
                    }
                    else
                    {
                        try
                        {
                            var hash = reference.IndexOf('#'); fragment = hash >= 0 ? Uri.UnescapeDataString(reference[(hash + 1)..]) : "";
                            var absolute = reference.StartsWith('#') ? known[from] : workspace.ResolveLink(known[from], reference);
                            if (!WorkspaceService.IsDocument(absolute)) continue;
                            if (excludedDirectory is not null && WorkspacePath.ContainsOrEquals(excludedDirectory, absolute))
                            { state = WorkspaceMapLinkState.Unsupported; message = "Private application state is excluded from the map."; }
                            else to = Relative(absolute);
                        }
                        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
                        { state = WorkspaceMapLinkState.Unsupported; message = "This link could not be resolved within the workspace."; }
                    }
                }
                if (state == WorkspaceMapLinkState.Resolved && to is not null)
                {
                    if (!known.ContainsKey(to))
                    {
                        var omitted = File.Exists(workspace.Resolve(to));
                        state = catalogueComplete && !omitted ? WorkspaceMapLinkState.Missing : WorkspaceMapLinkState.Unchecked;
                        message = omitted ? "The target is outside the map's scanned document catalogue."
                            : catalogueComplete ? "The linked document is missing." : "The workspace catalogue is incomplete; this target is unconfirmed.";
                    }
                    else
                    {
                        to = Relative(known[to]);
                        if (!parsed.TryGetValue(to, out var target)) { state = WorkspaceMapLinkState.Unchecked; message = "The target was not read for this map."; }
                        else if (fragment.Length > 0 && DocumentAnchors.Find(target, fragment) is null) { state = WorkspaceMapLinkState.Missing; message = "The linked section is missing."; }
                    }
                }
                var label = inline is WikiLinkInline alias ? alias.Label : MarkdownService.InlineText((LinkInline)inline);
                links.Add(new(from, reference, to, fragment, Display(label), inline is WikiLinkInline ? "Wikilink" : "Markdown",
                    inline.Span.Start, inline.Span.Length, state, message));
            }
            if (links.Count == MaximumLinks) { Warn("The map reached its 10,000-link limit."); break; }
        }
        var nodes = parsed.Select(pair =>
        {
            var title = pair.Value.Metadata.Title.Length > 0 ? pair.Value.Metadata.Title : pair.Value.Headings.FirstOrDefault()?.Title ?? Path.GetFileNameWithoutExtension(pair.Key);
            return new WorkspaceMapDocument(pair.Key, Display(title), Path.GetExtension(pair.Key).ToLowerInvariant() is ".mmd" or ".mermaid" ? "Mermaid" : "Markdown",
                pair.Value.Source, links.Count(link => link.To?.Equals(pair.Key, StringComparison.OrdinalIgnoreCase) == true && link.From != pair.Key),
                links.Count(link => link.From == pair.Key), links.Count(link => link.From == pair.Key && link.State != WorkspaceMapLinkState.Resolved));
        }).OrderBy(node => node.Path, StringComparer.OrdinalIgnoreCase).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        return new(workspace.Root, nodes, links.ToArray(), warnings.Distinct(StringComparer.Ordinal).Take(100).ToArray(), complete);
        string Relative(string path) => Path.GetRelativePath(workspace.Root, path).Replace('\\', '/');
        void Warn(string warning) { complete = false; if (warnings.Count < 100) warnings.Add(warning); }
    }
    private static IEnumerable<string> Flatten(IEnumerable<WorkspaceEntry> entries)
    {
        foreach (var entry in entries)
            if (entry.IsDirectory) { foreach (var path in Flatten(entry.Children)) yield return path; }
            else if (WorkspaceService.IsDocument(entry.Path)) yield return entry.Path;
    }
    private static string Display(string value)
    {
        value = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return value.Length <= 240 ? value : value[..240];
    }
    public static WorkspaceMapDiagram Diagram(WorkspaceMapResult map, string? selectedPath = null)
    {
        var all = map.Documents.Select(document => document.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var focus = selectedPath is null ? all : map.Links.Where(link => link.From == selectedPath || link.To == selectedPath)
            .SelectMany(link => new[] { link.From, link.To }).OfType<string>().Append(selectedPath).Where(all.Contains).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selected = focus.Order(StringComparer.OrdinalIgnoreCase).Take(60).ToArray();
        if (selectedPath is not null && focus.Contains(selectedPath) && !selected.Contains(selectedPath, StringComparer.OrdinalIgnoreCase))
            selected[^1] = selectedPath;
        var ids = selected.Select((path, index) => (path, id: "doc" + (index + 1))).ToDictionary(item => item.path, item => item.id, StringComparer.OrdinalIgnoreCase);
        var edges = map.Links.Where(link => link.State == WorkspaceMapLinkState.Resolved && link.To is not null && ids.ContainsKey(link.From) && ids.ContainsKey(link.To))
            .Select(link => (link.From, To: link.To!)).Distinct().ToArray();
        var source = new StringBuilder("flowchart LR\n");
        foreach (var path in selected)
        {
            var label = new string(path.Take(100).Select(character => char.IsLetterOrDigit(character) || character is ' ' or '.' or '/' or '-' or '_' ? character : ' ').ToArray());
            source.Append("  ").Append(ids[path]).Append("[\"").Append(label).AppendLine("\"]");
        }
        foreach (var edge in edges.Take(240)) source.Append("  ").Append(ids[edge.From]).Append(" --> ").AppendLine(ids[edge.To]);
        return new(source.ToString(), selected.Length, Math.Min(240, edges.Length), focus.Count > selected.Length || edges.Length > 240);
    }
    public static string Markdown(WorkspaceMapResult map)
    {
        var diagram = Diagram(map);
        var text = new StringBuilder("# Documentation map\n\n");
        text.Append(map.Documents.Count).Append(" documents read. ").AppendLine(map.Complete ? "Workspace scan complete." : "Partial workspace scan; review the notes below.");
        text.AppendLine("\nGenerated from current document drafts and saved files. Source documents are unchanged.");
        text.Append("\nThe diagram contains ").Append(diagram.Documents).Append(" documents and ").Append(diagram.Connections).AppendLine(" distinct connections.");
        if (diagram.Limited) text.AppendLine("The diagram is limited to 60 documents and 240 connections; the document list covers the complete scanned set.");
        text.Append("\n```mermaid\n").Append(diagram.Source).AppendLine("```\n\n## Documents\n\n| Document | Title | Incoming | Outgoing | Notes |\n|---|---|---:|---:|---:|");
        foreach (var node in map.Documents)
            text.Append("| ").Append(Cell(node.Path)).Append(" | ").Append(Cell(node.Title)).Append(" | ").Append(node.Incoming).Append(" | ").Append(node.Outgoing).Append(" | ").Append(node.Issues).AppendLine(" |");
        text.AppendLine("\n## Links\n\n| From | Target | Type | Result |\n|---|---|---|---|");
        foreach (var link in map.Links.Take(2000))
            text.Append("| ").Append(Cell(link.From)).Append(" | ").Append(Cell(link.To is null ? link.Reference : link.To + (link.Fragment.Length == 0 ? "" : "#" + link.Fragment))).Append(" | ").Append(Cell(link.Kind)).Append(" | ").Append(Cell(link.Message)).AppendLine(" |");
        if (map.Links.Count > 2000) text.AppendLine("\nThe report lists the first 2,000 links. Use the native map filters to inspect the remaining links.");
        if (map.Warnings.Count > 0)
        {
            text.AppendLine("\n## Scan notes\n");
            foreach (var warning in map.Warnings) text.Append("- ").AppendLine(Cell(warning));
        }
        if (text.Length > 4 * 1024 * 1024) throw new IOException("The map report exceeds its 4-million-character copy limit. Use a smaller workspace.");
        return text.ToString();
    }
    private static string Cell(string value)
    {
        value = value.Replace("\r", " ").Replace("\n", " ");
        var run = 0; var longest = 0;
        foreach (var character in value) { run = character == '`' ? run + 1 : 0; longest = Math.Max(longest, run); }
        // Code spans prevent imported titles and URLs becoming active Markdown or automatic links.
        var fence = new string('`', longest + 1);
        return fence + " " + value.Replace("|", "\\|") + " " + fence;
    }
}
