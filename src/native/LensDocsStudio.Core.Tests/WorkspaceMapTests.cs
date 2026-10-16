using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.Workspace;
using Markdig.Syntax;

namespace LensDocsStudio.Core.Tests;

public sealed class WorkspaceMapTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "lens-map-" + Guid.NewGuid().ToString("N"));
    private string State => Path.Combine(root, "private-state");
    private WorkspaceMapService Service => new(new DocumentStore(Path.Combine(State, "writes")));
    public WorkspaceMapTests() { Directory.CreateDirectory(root); }
    private async Task<string> Write(string relative, string text)
    {
        var path = Path.Combine(root, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, text); return path;
    }
    [Fact] public async Task Map_uses_current_drafts_for_titles_sections_links_and_backlinks_without_writes()
    {
        var home = await Write("README.md", "# Saved home"); var guide = await Write("docs/Guide.md", "# Old heading");
        await Write("diagram.mmd", "flowchart LR\nA[[Guide]] --> B");
        const string draft = "---\ntitle: Current title\n---\n# Overview\n\n[[docs/Guide#new-heading|Read guide]] [Missing](missing.md) [Remote](https://example.test) ![Image](logo.png) `[[Literal]]`";
        var map = await Service.BuildAsync(new(root), new Dictionary<string, string> { [home] = draft, [guide] = "# New heading\n\n[Back](../README.md#overview)" }, State);
        Assert.True(map.Complete); Assert.Empty(map.Warnings); Assert.Equal(3, map.Documents.Count); Assert.Equal(3, map.Links.Count);
        var node = Assert.Single(map.Documents, node => node.Path == "README.md"); Assert.Equal("Current title", node.Title); Assert.Equal(1, node.Incoming); Assert.Equal(2, node.Outgoing); Assert.Equal(1, node.Issues);
        var wiki = Assert.Single(map.Links, link => link.Kind == "Wikilink"); Assert.Equal(WorkspaceMapLinkState.Resolved, wiki.State); Assert.Equal("docs/Guide.md", wiki.To);
        Assert.Equal("[[docs/Guide#new-heading|Read guide]]", draft.Substring(wiki.Start, wiki.Length));
        Assert.Equal(WorkspaceMapLinkState.Missing, Assert.Single(map.Links, link => link.Reference == "missing.md").State);
        Assert.Equal("# Saved home", await File.ReadAllTextAsync(home)); Assert.Equal("# Old heading", await File.ReadAllTextAsync(guide)); Assert.False(Directory.Exists(State));
    }
    [Fact] public async Task Map_reports_ambiguous_names_missing_sections_and_unsafe_paths()
    {
        await Write("README.md", "# Home\n\n[[Summary]] [Absent](one/Summary.md#absent) [Escape](../outside.md) [Scheme](file:///private.md)");
        await Write("one/Summary.md", "# First"); await Write("two/Summary.md", "# Second");
        var map = await Service.BuildAsync(new(root));
        Assert.True(map.Complete); Assert.Equal(4, map.Links.Count);
        Assert.Single(map.Links, link => link.State == WorkspaceMapLinkState.Ambiguous);
        Assert.Single(map.Links, link => link.State == WorkspaceMapLinkState.Missing && link.Message.Contains("section", StringComparison.Ordinal));
        Assert.Equal(2, map.Links.Count(link => link.State == WorkspaceMapLinkState.Unsupported));
    }
    [Fact] public async Task Private_state_and_unreadable_documents_do_not_become_confirmed_missing_targets()
    {
        await Write("README.md", "# Home\n\n[Private](private-state/secret.md) [Bad](bad.md)");
        await Write("private-state/secret.md", "# Private"); await File.WriteAllBytesAsync(Path.Combine(root, "bad.md"), [0xff, 0xfe, 0x12]);
        var map = await Service.BuildAsync(new(root), excludedDirectory: State);
        Assert.False(map.Complete); Assert.Single(map.Documents);
        Assert.Single(map.Links, link => link.State == WorkspaceMapLinkState.Unchecked);
        Assert.Single(map.Links, link => link.State == WorkspaceMapLinkState.Unsupported);
        Assert.DoesNotContain(map.Documents, node => node.Path.Contains("private-state", StringComparison.Ordinal));
        Assert.False(Directory.Exists(Path.Combine(State, "writes")));
    }
    [Fact] public async Task Oversized_sources_are_reported_and_cancellation_does_not_write()
    {
        await Write("large.md", new string('a', WorkspaceMapService.MaximumDocumentCharacters + 1)); await Write("small.md", "# Small");
        var map = await Service.BuildAsync(new(root)); Assert.False(map.Complete); Assert.Single(map.Documents);
        Assert.Contains(map.Warnings, warning => warning.Contains("source limit", StringComparison.Ordinal));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service.BuildAsync(new(root), cancellationToken: cancelled.Token));
        Assert.False(Directory.Exists(State));
    }
    [Fact] public async Task Diagram_uses_safe_labels_and_distinct_document_connections()
    {
        await Write("README.md", "# Home\n\n[[Café [review] document.md|First]] [Second](Caf%C3%A9%20%5Breview%5D%20document.md)");
        await Write("Café [review] document.md", "# Review");
        var map = await Service.BuildAsync(new(root));
        var graph = WorkspaceMapService.Diagram(map);
        Assert.Equal(2, graph.Documents); Assert.Equal(1, graph.Connections); Assert.False(graph.Limited);
        Assert.DoesNotContain("[review]", graph.Source); Assert.Contains("Café", graph.Source);
        var rendered = new DiagramService().Render(graph.Source); Assert.True(rendered.Success, rendered.Error);
        Assert.DoesNotContain("javascript:", rendered.Svg, StringComparison.OrdinalIgnoreCase);
    }
    [Fact] public void Large_diagram_is_bounded_and_keeps_its_selected_document()
    {
        var nodes = Enumerable.Range(0, 100).Select(index => new WorkspaceMapDocument("page-" + index.ToString("D3") + ".md", "Page", "Markdown", "", 0, 0, 0)).ToArray();
        var links = nodes.Take(99).Select(node => new WorkspaceMapLink(nodes[^1].Path, node.Path, node.Path, "", "", "Markdown", 0, 1, WorkspaceMapLinkState.Resolved, "Resolved")).ToArray();
        var map = new WorkspaceMapResult(root, nodes, links, [], true);
        var graph = WorkspaceMapService.Diagram(map, nodes[^1].Path);
        Assert.Equal(60, graph.Documents); Assert.True(graph.Limited); Assert.Contains(nodes[^1].Path, graph.Source);
        Assert.True(new DiagramService().Render(graph.Source).Success);
    }
    [Fact] public async Task Partial_folder_catalogue_does_not_confirm_missing_or_unique_wiki_targets()
    {
        var deep = string.Join('/', Enumerable.Repeat("deep", 34));
        await Write(deep + "/Hidden.md", "# Hidden"); await Write("Guide.md", "# Guide");
        await Write("README.md", "[[Hidden]] [[Guide]] [Hidden] (ignored) [Deep](" + deep + "/Hidden.md)");
        var map = await Service.BuildAsync(new(root));
        Assert.False(map.Complete); Assert.Equal(3, map.Links.Count);
        Assert.All(map.Links, link => Assert.Equal(WorkspaceMapLinkState.Unchecked, link.State));
        Assert.Contains(map.Warnings, warning => warning.Contains("safety limit", StringComparison.Ordinal));
    }
    [Fact] public async Task Ordinary_links_use_catalogue_case_and_diagram_connections_are_unique()
    {
        await Write("Guide.md", "# Guide"); await Write("README.md", "[One](guide.md) [Two](Guide.md)");
        var map = await Service.BuildAsync(new(root));
        Assert.All(map.Links, link => Assert.Equal("Guide.md", link.To));
        Assert.Equal(1, WorkspaceMapService.Diagram(map).Connections);
    }
    [Fact] public async Task Excluded_build_files_remain_unchecked_and_private_workspaces_are_refused()
    {
        await Write("README.md", "[Build notes](bin/notes.md)"); await Write("bin/notes.md", "# Private build notes");
        await Write("private-state/session.md", "# Private draft");
        var map = await Service.BuildAsync(new(root), excludedDirectory: State);
        Assert.Single(map.Documents); Assert.Equal(WorkspaceMapLinkState.Unchecked, Assert.Single(map.Links).State);
        await Assert.ThrowsAsync<IOException>(() => Service.BuildAsync(new(State), excludedDirectory: State));
        Assert.False(Directory.Exists(Path.Combine(State, "writes")));
    }
    [Fact] public async Task Markdown_report_preserves_sections_and_passive_metadata_without_source_changes()
    {
        var home = await Write("README.md", "---\ntitle: '<script>bad</script> | [active](https://example.test)'\n---\n# Home\n\n[[Guide#section|Guide]]");
        await Write("Guide.md", "# Section"); var before = await File.ReadAllTextAsync(home);
        var map = await Service.BuildAsync(new(root)); var report = WorkspaceMapService.Markdown(map);
        Assert.Contains("Guide.md#section", report);
        var parsed = new MarkdownService().Parse(report);
        Assert.Empty(parsed.Syntax.Descendants().OfType<Markdig.Syntax.Inlines.LinkInline>());
        Assert.Empty(parsed.Syntax.Descendants().OfType<Markdig.Syntax.Inlines.HtmlInline>());
        var diagram = Assert.Single(parsed.Syntax.Descendants().OfType<FencedCodeBlock>());
        Assert.True(new DiagramService().Render(diagram.Lines.ToString()).Success);
        Assert.Equal(before, await File.ReadAllTextAsync(home)); Assert.False(Directory.Exists(State));
    }
    [Fact] public void Report_discloses_link_and_diagram_limits_and_partial_scan_notes()
    {
        var nodes = Enumerable.Range(0, 61).Select(index => new WorkspaceMapDocument("page-" + index + ".md", "Page", "Markdown", "", 0, 0, 0)).ToArray();
        var links = Enumerable.Range(0, 2001).Select(index => new WorkspaceMapLink(nodes[0].Path, "missing-" + index, null, "", "", "Markdown", 0, 1, WorkspaceMapLinkState.Missing, "Missing")).ToArray();
        var report = WorkspaceMapService.Markdown(new(root, nodes, links, ["Unreadable folder"], false));
        Assert.Contains("Partial workspace scan", report); Assert.Contains("60 documents", report); Assert.Contains("2,000 links", report);
        Assert.Contains("Unreadable folder", report); Assert.Contains("missing-1999", report); Assert.DoesNotContain("missing-2000", report);
    }
    public void Dispose() => Directory.Delete(root, true);
}
