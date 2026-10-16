using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Core.Tests;

public sealed class MermaidReconnectionTests
{
    [Theory]
    [InlineData(false, "\n")]
    [InlineData(true, "\r\n")]
    public async Task Reconnecting_one_identical_parallel_edge_retains_order_labels_styles_and_history(bool sourceEndpoint, string newline)
    {
        var source = string.Join(newline, new[] { "flowchart LR", "    %% Retain Unicode: café Ω", "    A[Start]", "    B[Review]", "    C[Alternative]",
            "    A -.->|Next| B", "    A -.->|Next| B", "    classDef review fill:#FAFAFA,stroke:#333333;", "    class B review", "" });
        var draft = new MermaidVisualDraft(source);
        Assert.True(draft.Model.CanEdit, draft.Model.Diagnostic);
        var first = draft.Model.Edges[0].Id; var second = draft.Model.Edges[1].Id;
        var result = await draft.ExecuteAsync(new UpdateMermaidConnection(second, sourceEndpoint ? "C" : "A", sourceEndpoint ? "B" : "C", "Next", FlowConnectionType.Dotted));
        Assert.True(result.Success, result.Error);
        Assert.Equal(new[] { first, second }, draft.Model.Edges.Select(edge => edge.Id));
        Assert.Equal("A", draft.Model.Edge(first).Source.Id); Assert.Equal("B", draft.Model.Edge(first).Target.Id);
        Assert.Equal(sourceEndpoint ? "C" : "A", draft.Model.Edge(second).Source.Id);
        Assert.Equal(sourceEndpoint ? "B" : "C", draft.Model.Edge(second).Target.Id);
        Assert.All(draft.Model.Edges, edge => { Assert.Equal("Next", edge.Label); Assert.Equal(FlowConnectionType.Dotted, edge.Type); });
        Assert.Contains("%% Retain Unicode: café Ω" + newline, draft.Source);
        Assert.Contains("classDef review fill:#FAFAFA,stroke:#333333;" + newline, draft.Source);
        Assert.Contains("class B review" + newline, draft.Source);
        Assert.True(new DiagramService().Render(draft.Source).Success);
        var changed = draft.Source;
        draft.Undo(); Assert.Equal(source, draft.Source); Assert.False(draft.CanUndo);
        draft.Redo(); Assert.Equal(changed, draft.Source); Assert.Equal(second, draft.Model.Edges[1].Id);
    }

    [Fact]
    public async Task Unknown_endpoint_and_protected_source_reject_without_a_private_history_entry()
    {
        foreach (var source in new[] { "flowchart LR\nA -->|Keep| B\n", "flowchart LR\nA --> B --> C\n" })
        {
            var draft = new MermaidVisualDraft(source);
            var result = await draft.ExecuteAsync(new UpdateMermaidConnection("edge-0", "A", "Missing", "Keep", FlowConnectionType.Directed));
            Assert.False(result.Success); Assert.Equal(source, draft.Source);
            Assert.False(draft.IsDirty); Assert.False(draft.CanUndo); Assert.False(draft.CanRedo);
        }
    }
}
