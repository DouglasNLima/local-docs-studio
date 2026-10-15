using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Core.Tests;

public sealed class MermaidVisualTests
{
    private const string Styled = "flowchart LR\r\n    %% Keep this comment\r\n    A[\"Capture Error\"] -->|Success| B[\"Diagnostics\"]\r\n    class A control\r\n    classDef control fill:#E8F3FA,stroke:#007F79;\r\n";

    [Fact]
    public async Task Rename_changes_only_the_requested_label_span()
    {
        var draft = new MermaidVisualDraft(Styled); AssertEditable(draft.Model);
        Assert.Equal("LR", draft.Model.Direction); Assert.Equal(2, draft.Model.Nodes.Count);
        Assert.Equal("Success", Assert.Single(draft.Model.Edges).Label);
        await Execute(draft, new UpdateMermaidNode("B", "Review", FlowNodeShape.Process, "B"));
        Assert.Equal(Styled.Replace("\"Diagnostics\"", "\"Review\"", StringComparison.Ordinal), draft.Source);
        Assert.True(new DiagramService().Render(draft.Source).Success);
    }

    [Theory]
    [InlineData("\n")] [InlineData("\r\n")]
    public async Task Add_rename_shape_id_style_and_delete_preserve_unrelated_source(string newline)
    {
        var source = Styled.Replace("\r\n", newline, StringComparison.Ordinal);
        var draft = new MermaidVisualDraft(source);
        await Execute(draft, new AddMermaidNode("C", "Publish", FlowNodeShape.Decision));
        await Execute(draft, new AddMermaidConnection("B", "C", "Yes"));
        await Execute(draft, new UpdateMermaidNode("C", "Published", FlowNodeShape.Terminal, "Final"));
        Assert.Contains("B -->|Yes| Final", draft.Source);
        await Execute(draft, new SetMermaidClass("Final", "control"));
        await Execute(draft, new SetMermaidStyle("Final", "#FAFAFA", "#123456", "#111111"));
        await Execute(draft, new UpdateMermaidNode("Final", "Published", FlowNodeShape.Terminal, "Output"));
        Assert.Contains("class Output control", draft.Source); Assert.Contains("style Output fill:", draft.Source);
        await Execute(draft, new DeleteMermaidNodes(["Output"]));
        Assert.DoesNotContain("Output", draft.Source); Assert.StartsWith(source, draft.Source);
        Assert.Equal(source.Contains('\r'), draft.Source.Contains('\r'));
    }

    [Fact]
    public async Task Deleting_inline_nodes_retains_the_other_endpoint_and_shared_class_definition()
    {
        var draft = new MermaidVisualDraft(Styled);
        await Execute(draft, new DeleteMermaidNodes(["A"]));
        Assert.Equal("B", Assert.Single(draft.Model.Nodes).Id); Assert.Empty(draft.Model.Edges);
        Assert.Contains("B[\"Diagnostics\"]", draft.Source); Assert.Contains("classDef control", draft.Source);
        Assert.Contains("%% Keep this comment", draft.Source);
    }

    [Fact]
    public async Task Connections_can_change_endpoints_reverse_relabel_and_delete_without_losing_implicit_nodes()
    {
        var draft = new MermaidVisualDraft("graph TD\nA -->|Yes| B\nC((Done))\n"); AssertEditable(draft.Model);
        await Execute(draft, new UpdateMermaidConnection("edge-0", "C", "A", "No", FlowConnectionType.Dotted));
        Assert.Equal(3, draft.Model.Nodes.Count); Assert.Contains("C -.->|No| A", draft.Source);
        await Execute(draft, new UpdateMermaidConnection("edge-0", "A", "C", "Confirmed", FlowConnectionType.Bidirectional));
        Assert.Equal("A", draft.Model.Edges[0].Source.Id); Assert.Equal("C", draft.Model.Edges[0].Target.Id);
        await Execute(draft, new DeleteMermaidConnection("edge-0"));
        Assert.Equal(3, draft.Model.Nodes.Count); Assert.Empty(draft.Model.Edges);
    }

    [Theory]
    [InlineData(FlowNodeShape.Process)] [InlineData(FlowNodeShape.Decision)] [InlineData(FlowNodeShape.Rounded)]
    [InlineData(FlowNodeShape.Terminal)] [InlineData(FlowNodeShape.Circle)] [InlineData(FlowNodeShape.DoubleCircle)]
    [InlineData(FlowNodeShape.Subroutine)] [InlineData(FlowNodeShape.Cylinder)] [InlineData(FlowNodeShape.Hexagon)]
    public async Task Every_supported_shape_round_trips_through_the_established_renderer(FlowNodeShape shape)
    {
        var draft = new MermaidVisualDraft("flowchart TB\nA[Start]\n");
        await Execute(draft, new UpdateMermaidNode("A", "Finish", shape, "A"));
        Assert.Equal(shape, draft.Model.Node("A").Shape);
    }

    [Theory]
    [InlineData("TD")] [InlineData("TB")] [InlineData("BT")] [InlineData("LR")] [InlineData("RL")]
    public async Task Direction_changes_only_the_header_token(string direction)
    {
        var draft = new MermaidVisualDraft(Styled);
        await Execute(draft, new ChangeMermaidDirection(direction));
        Assert.Equal(Styled.Replace("flowchart LR", "flowchart " + direction, StringComparison.Ordinal), draft.Source);
    }

    [Fact]
    public async Task Repeated_visible_labels_use_ids_and_keep_the_other_node_identical()
    {
        const string source = "flowchart TD\nFirst[Same] --> Second[Same]\n";
        var draft = new MermaidVisualDraft(source);
        await Execute(draft, new UpdateMermaidNode("Second", "Different", FlowNodeShape.Process, "Second"));
        Assert.Equal(source.Replace("Second[Same]", "Second[\"Different\"]", StringComparison.Ordinal), draft.Source);
    }

    [Fact]
    public async Task Nested_groups_support_rename_direction_move_and_flatten_without_removing_comments()
    {
        const string source = "flowchart TD\nsubgraph Parent[Pipeline]\n  %% group comment\n  subgraph Child[Capture]\n    A[Input]\n  end\nend\nB[Output]\nA --> B\n";
        var draft = new MermaidVisualDraft(source); AssertEditable(draft.Model);
        Assert.Equal("Parent", draft.Model.Subgraph("Child").ParentId);
        await Execute(draft, new RenameMermaidSubgraph("Child", "Review"));
        await Execute(draft, new ChangeMermaidDirection("LR", "Child"));
        await Execute(draft, new MoveMermaidNode("B", "Child"));
        Assert.Equal("Child", draft.Model.Node("B").SubgraphId);
        await Execute(draft, new MoveMermaidNode("B", null));
        Assert.Null(draft.Model.Node("B").SubgraphId);
        await Execute(draft, new DeleteMermaidSubgraph("Child"));
        Assert.Equal(2, draft.Model.Nodes.Count); Assert.Single(draft.Model.Subgraphs);
        Assert.Contains("%% group comment", draft.Source); Assert.Contains("A --> B", draft.Source);
        await Execute(draft, new AddMermaidSubgraph("New", "New group", "Parent"));
        await Execute(draft, new AddMermaidNode("C", "Review", FlowNodeShape.Process, "New"));
        Assert.Equal("New", draft.Model.Node("C").SubgraphId);
        await Execute(draft, new DeleteMermaidSubgraph("New", true));
        Assert.DoesNotContain(draft.Model.Nodes, node => node.Id == "C");
    }

    [Fact]
    public async Task Moving_a_node_with_internal_connections_rejects_ambiguous_membership_without_history()
    {
        const string source = "flowchart TD\nsubgraph G[Group]\nA --> B\nend\n";
        var draft = new MermaidVisualDraft(source); AssertEditable(draft.Model);
        var result = await draft.ExecuteAsync(new MoveMermaidNode("A", null));
        Assert.False(result.Success); Assert.Contains("connections outside subgraphs", result.Error);
        Assert.Equal(source, draft.Source); Assert.False(draft.CanUndo);
    }

    [Fact]
    public async Task History_restores_exact_source_and_selection_and_discards_redo_after_a_new_edit()
    {
        var draft = new MermaidVisualDraft(Styled); draft.Select(["node:B"]);
        await Execute(draft, new UpdateMermaidNode("B", "Review", FlowNodeShape.Process, "B")); var renamed = draft.Source;
        await Execute(draft, new DeleteMermaidNodes(["B"])); Assert.Empty(draft.Selection);
        draft.Undo(); Assert.Equal(renamed, draft.Source); Assert.Equal(["node:B"], draft.Selection);
        draft.Undo(); Assert.Equal(Styled, draft.Source); Assert.False(draft.IsDirty);
        draft.Redo(); Assert.Equal(renamed, draft.Source);
        var result = await draft.ExecuteAsync(new UpdateMermaidNode("B", "Review", FlowNodeShape.Process, "A"));
        Assert.False(result.Success); Assert.True(draft.CanRedo); Assert.Equal(renamed, draft.Source);
        await Execute(draft, new ChangeMermaidDirection("BT")); Assert.False(draft.CanRedo);
    }

    [Theory]
    [InlineData("A --> B --> C", "Chained")]
    [InlineData("A --> B\nclick A \"https://example.com\"", "protected")]
    [InlineData("A --> B\nlinkStyle 0 stroke:red", "protected")]
    [InlineData("A@{ shape: rect }", "protected")]
    [InlineData("A[First]\nA[Second]", "Repeated")]
    [InlineData("%%{init: {'theme': 'dark'}}%%\nA --> B", "directives")]
    public async Task Unverified_constructs_remain_verbatim_and_cannot_be_transformed(string body, string reason)
    {
        var source = "flowchart TD\n" + body; var draft = new MermaidVisualDraft(source);
        Assert.False(draft.Model.CanEdit); Assert.Contains(reason, draft.Model.Diagnostic, StringComparison.OrdinalIgnoreCase);
        var result = await draft.ExecuteAsync(new AddMermaidNode("New", "New", FlowNodeShape.Process));
        Assert.False(result.Success); Assert.Equal(source, draft.Source); Assert.False(draft.CanUndo);
    }

    [Fact]
    public async Task Labels_escape_syntax_without_executing_html_or_creating_extra_nodes()
    {
        const string label = "Café 😀 e\u0301 \"[x](y)|{z}\\ <script> #34; &amp;\nNext";
        var draft = new MermaidVisualDraft("flowchart TD\nA[Start]\n");
        await Execute(draft, new UpdateMermaidNode("A", label, FlowNodeShape.Process, "A"));
        Assert.Single(draft.Model.Nodes); Assert.Equal(label, draft.Model.Node("A").Label);
        Assert.DoesNotContain("<script>", draft.Source);
        await Execute(draft, new AddMermaidNode("B", "End", FlowNodeShape.Terminal));
        await Execute(draft, new AddMermaidConnection("A", "B", label));
        Assert.Equal(label, Assert.Single(draft.Model.Edges).Label);
    }

    [Fact]
    public async Task Invalid_source_labels_duplicate_ids_and_overlapping_patches_fail_without_a_commit()
    {
        var draft = new MermaidVisualDraft(Styled);
        Assert.False((await draft.ExecuteAsync(new AddMermaidNode("A", "Duplicate", FlowNodeShape.Process))).Success);
        Assert.False((await draft.ExecuteAsync(new AddMermaidNode("click", "Reserved", FlowNodeShape.Process))).Success);
        Assert.False((await draft.ExecuteAsync(new UpdateMermaidNode("B", new string('x', 4097), FlowNodeShape.Process, "B"))).Success);
        Assert.False((await draft.ExecuteAsync(new UpdateMermaidNode("B", "\ud800", FlowNodeShape.Process, "B"))).Success);
        Assert.Equal(Styled, draft.Source); Assert.False(draft.CanUndo);
        Assert.Throws<ArgumentException>(() => MermaidPatches.Apply("abcdef", [new(new(1, 3), "bcd", "x"), new(new(2, 2), "cd", "y")]));
        Assert.False(new MermaidSourceParser().Parse("flowchart TD\nsubgraph G[Group]\nA[B]").CanEdit);
        Assert.False(new MermaidSourceParser().Parse(new string('x', 100001)).CanEdit);
    }

    [Fact]
    public async Task Cancelled_validation_keeps_history_and_source_intact()
    {
        var draft = new MermaidVisualDraft(Styled); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => draft.ExecuteAsync(new ChangeMermaidDirection("TD"), cancellation.Token));
        Assert.Equal(Styled, draft.Source); Assert.False(draft.CanUndo);
    }

    private static void AssertEditable(MermaidDiagramModel model) => Assert.True(model.CanEdit, model.Diagnostic);
    private static async Task Execute(MermaidVisualDraft draft, MermaidEditOperation operation)
    { var result = await draft.ExecuteAsync(operation); Assert.True(result.Success, result.Error); AssertEditable(draft.Model); }
}
