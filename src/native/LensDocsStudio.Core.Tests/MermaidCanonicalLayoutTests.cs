using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Rendering;
using Xunit;

namespace LensDocsStudio.Core.Tests;

public sealed class MermaidCanonicalLayoutTests
{
    [Theory]
    [InlineData("LR")]
    [InlineData("RL")]
    [InlineData("TD")]
    [InlineData("BT")]
    public void Canonical_layout_belongs_to_the_cached_preview_render(string direction)
    {
        var source = $"flowchart {direction}\nA[Write] -->|Next| B(Review)\nB --> C{{Approve}}\n";
        var service = new DiagramService(); var result = service.Render(source);
        Assert.True(result.Success, result.Error); var graph = Assert.IsType<Mermaider.Models.PositionedGraph>(result.FlowchartLayout);
        Assert.Equal(3, graph.Nodes.Count); Assert.Equal(2, graph.Edges.Count);
        Assert.Equal("Next", graph.Edges[0].Label);
        Assert.All(graph.Nodes, node => Assert.True(double.IsFinite(node.X) && double.IsFinite(node.Y) && node.Width > 0 && node.Height > 0));
        Assert.All(graph.Edges, edge => Assert.True(edge.Points.Count >= 2));
        Assert.Same(result, service.Render(source));
        Assert.Contains("data-id=\"A\"", result.Svg);
    }

    [Fact]
    public void Protected_chains_keep_the_complete_canonical_graph_and_original_source()
    {
        const string source = "flowchart LR\nA[Write] --> B[Review] --> C[Archive]\nA -.->|Diagnostic| C\n";
        Assert.False(new MermaidSourceParser().Parse(source).CanEdit);
        var result = new DiagramService().Render(source);
        Assert.True(result.Success, result.Error); Assert.Equal(source, result.Source);
        Assert.Equal(3, result.FlowchartLayout!.Nodes.Count); Assert.Equal(3, result.FlowchartLayout.Edges.Count);
    }

    [Fact]
    public async Task Identical_parallel_edges_retain_source_order_without_proprietary_identifiers()
    {
        const string source = "flowchart TD\nA[Same] -->|Same| B[Same]\nA -->|Same| B\n";
        var result = new DiagramService().Render(source); Assert.True(result.Success, result.Error);
        Assert.Equal(2, result.FlowchartLayout!.Edges.Count);
        var draft = new MermaidVisualDraft(source);
        var second = draft.Model.Edges[1];
        Assert.True((await draft.ExecuteAsync(new UpdateMermaidConnection(second.Id, "A", "B", "Changed", second.Type))).Success);
        Assert.Equal("Same", draft.Model.Edges[0].Label); Assert.Equal("Changed", draft.Model.Edges[1].Label);
        draft.Undo(); Assert.Equal(source, draft.Source);
        Assert.DoesNotContain("lens-", draft.Source);
    }

    [Fact]
    public void Nested_groups_share_the_preview_layout_without_flattening_hierarchy()
    {
        const string source = "flowchart LR\nsubgraph Outer[System]\nsubgraph Inner[Worker]\nA[Start] --> B[End]\nend\nend\n";
        var result = new DiagramService().Render(source); Assert.True(result.Success, result.Error);
        var outer = Assert.Single(result.FlowchartLayout!.Groups); Assert.Equal("Outer", outer.Id);
        Assert.Equal("Inner", Assert.Single(outer.Children).Id);
        Assert.Contains(result.FlowchartGroups, group => group.Id == "Inner" && group.ParentId == "Outer" && group.NodeIds.Contains("A") && group.NodeIds.Contains("B"));
    }

    [Fact]
    public async Task Concurrent_themes_do_not_share_mutable_capture_providers()
    {
        var service = new DiagramService();
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(index => Task.Run(() => service.Render($"flowchart LR\nN{index}[Node {index}]", index % 2 == 0))));
        for (var index = 0; index < results.Length; index++) Assert.Equal("N" + index, Assert.Single(results[index].FlowchartLayout!.Nodes).Id);
    }

    [Fact]
    public void Cancellation_rejects_cached_results_without_poisoning_the_render_cache()
    {
        const string source = "flowchart LR\nA[Keep] --> B[Source]";
        var service = new DiagramService(); var original = service.Render(source);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => service.Render(source, cancellationToken: cancelled.Token));
        Assert.Same(original, service.Render(source));
    }
}
