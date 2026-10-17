// Copied only into the immutable PR #30 Core test checkout by disposable CI.
using LensDocsStudio.Core.Mermaid;

namespace LensDocsStudio.Core.Tests;

public sealed class MermaidPartialBaselineTests
{
    [Fact]
    public async Task Verified_PR30_baseline_rejects_independent_node_and_connection_edits_beside_a_chain()
    {
        const string source = "flowchart LR\n%% Preserve this exact comment\nA[Capture] --> B[Review] --> C[Done]\nD[Independent]\nE[Notify] -->|Next| F[Archive]\n";
        var draft = new MermaidVisualDraft(source); Assert.False(draft.Model.CanEdit);
        draft.Select(["node:D"]);
        var node = await draft.ExecuteAsync(new UpdateMermaidNode("D", "Changed", FlowNodeShape.Process, "D"));
        Assert.False(node.Success); Assert.Equal(source, draft.Source); Assert.False(draft.CanUndo);
        var edge = await draft.ExecuteAsync(new UpdateMermaidConnection("edge-2", "E", "F", "Changed", FlowConnectionType.Directed));
        Assert.False(edge.Success); Assert.Equal(source, draft.Source); Assert.False(draft.CanUndo);
        Assert.Equal(["node:D"], draft.Selection);
    }
}
