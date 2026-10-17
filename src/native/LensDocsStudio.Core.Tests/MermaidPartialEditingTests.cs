using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Core.Tests;

public sealed class MermaidPartialEditingTests
{
    private const string Chain = "    A[\"Capture\"] --> B[\"Review\"] --> C[\"Complete\"]";
    private const string Source = "flowchart LR\n    %% Preserve this exact comment\n" + Chain + "\n    D[\"Independent step\"]\n    E[Notify] -->|Next| F[Archive]\n";

    [Fact]
    public async Task Independent_label_shape_and_connection_apply_proofs_preserve_the_complete_chain_and_history()
    {
        var draft = new MermaidVisualDraft(Source); draft.Select(["node:D"]);
        Assert.Equal(MermaidCompatibility.PartiallyEditable, draft.Model.Compatibility); Assert.False(draft.Model.CanEdit);
        Assert.True(draft.Model.HasCompleteSourceMap); Assert.False(draft.CanApply);
        Assert.True(draft.Model.Capabilities.Query(MermaidOperationKind.NodeLabel, "D").Permitted);
        await Success(draft, new UpdateMermaidNode("D", "Café e\u0301 😀", FlowNodeShape.Decision, "D"));
        Assert.Equal(Source.Replace("D[\"Independent step\"]", "D{\"Café e\u0301 😀\"}", StringComparison.Ordinal), draft.Source);
        Assert.True(draft.CanApply); Assert.True((await draft.ValidateForApplyAsync()).Success);
        var edgeId = draft.Model.Edges[^1].Id; draft.Select([edgeId]);
        await Success(draft, new UpdateMermaidConnection(edgeId, "E", "F", "Independent", FlowConnectionType.Directed));
        Assert.Contains(Chain, draft.Source); Assert.Contains("%% Preserve this exact comment\n", draft.Source);
        Assert.Equal(["edge-0", "edge-1", "edge-2"], draft.Model.Edges.Select(edge => edge.Id));
        var changed = draft.Source; draft.Undo(); draft.Undo(); Assert.Equal(Source, draft.Source);
        Assert.False(draft.CanApply); Assert.False(draft.CanUndo); Assert.Equal(["node:D"], draft.Selection);
        draft.Redo(); draft.Redo(); Assert.Equal(changed, draft.Source); Assert.Equal([edgeId], draft.Selection);
    }

    [Fact]
    public async Task Chain_properties_use_exact_tokens_but_structural_changes_remain_protected()
    {
        var draft = new MermaidVisualDraft(Source);
        await Success(draft, new UpdateMermaidNode("B", "Validate", FlowNodeShape.Process, "B"));
        Assert.Equal(Source.Replace("\"Review\"", "\"Validate\"", StringComparison.Ordinal), draft.Source);
        await Success(draft, new UpdateMermaidConnection("edge-0", "A", "B", "Accepted", FlowConnectionType.Directed));
        Assert.Contains("A[\"Capture\"] -->|Accepted| B[\"Validate\"] --> C[\"Complete\"]", draft.Source);
        foreach (var operation in new MermaidEditOperation[] { new UpdateMermaidNode("B", "Validate", FlowNodeShape.Process, "Renamed"),
            new DeleteMermaidNodes(["B"]), new MoveMermaidNode("B", null), new DeleteMermaidConnection("edge-0"),
            new UpdateMermaidConnection("edge-0", "D", "B", "Accepted", FlowConnectionType.Directed) }) await Denied(draft, operation);
    }

    [Theory] [InlineData("0")] [InlineData("0, 2")] [InlineData("default")]
    public async Task Link_styles_allow_properties_but_block_edge_order_mutation(string indices)
    {
        var draft = new MermaidVisualDraft(Source + "    linkStyle " + indices + " stroke:#123456,stroke-width:2px\n");
        Assert.Equal(MermaidCompatibility.PartiallyEditable, draft.Model.Compatibility);
        Assert.True(draft.Model.Capabilities.HasOrdinalStyles);
        await Success(draft, new UpdateMermaidNode("D", "Changed", FlowNodeShape.Process, "D"));
        await Success(draft, new UpdateMermaidConnection("edge-2", "E", "F", "Safe", FlowConnectionType.Dotted));
        await Denied(draft, new AddMermaidConnection("D", "E")); await Denied(draft, new DeleteMermaidConnection("edge-2"));
        await Denied(draft, new DeleteMermaidNodes(["E"]));
        Assert.Contains("linkStyle " + indices + " stroke:#123456,stroke-width:2px\n", draft.Source);
    }

    [Theory] [InlineData("A\nA[Later]")] [InlineData("A[First]\nA[Later]")]
    public async Task First_occurrence_stays_authoritative_and_independent_nodes_remain_editable(string declarations)
    {
        var source = "flowchart LR\n" + declarations + "\nA --> B --> C\nD[Independent]\n";
        var draft = new MermaidVisualDraft(source); var label = draft.Model.Node("A").Label;
        Assert.Equal(declarations.StartsWith("A\n", StringComparison.Ordinal) ? "A" : "First", label);
        await Success(draft, new UpdateMermaidNode("D", "Changed", FlowNodeShape.Process, "D"));
        Assert.Equal(label, draft.Model.Node("A").Label); Assert.Contains(declarations, draft.Source);
        if (declarations.StartsWith("A[", StringComparison.Ordinal)) await Denied(draft, new UpdateMermaidNode("A", "Unsafe", FlowNodeShape.Process, "A"));
        else { await Success(draft, new UpdateMermaidNode("A", "Explicit", FlowNodeShape.Process, "A")); Assert.Contains("A[\"Explicit\"]\nA[Later]", draft.Source); }
    }

    [Fact]
    public async Task Nested_membership_classes_and_custom_style_properties_remain_exact()
    {
        var source = Source + "subgraph Parent[Parent]\nsubgraph Child[Child]\ndirection BT\nG[Grouped]\nend\nend\n"
            + "classDef shared fill:#FAFAFA,stroke:#123456\nclass D,G shared\nstyle G stroke-width:2px,stroke-dasharray:4 2\n";
        var draft = new MermaidVisualDraft(source);
        await Success(draft, new UpdateMermaidNode("G", "Grouped safely", FlowNodeShape.Rounded, "G"));
        await Success(draft, new SetMermaidStyle("G", "#F8F9FB", "#234567", "#111111"));
        Assert.Equal("Child", draft.Model.Node("G").SubgraphId); Assert.Equal("Parent", draft.Model.Subgraph("Child").ParentId);
        Assert.Equal("BT", draft.Model.EffectiveDirection("Child")); Assert.Contains("class D,G shared\n", draft.Source);
        Assert.Contains("style G stroke-width:2px,stroke-dasharray:4 2\n", draft.Source);
        var graph = Mermaider.MermaidRenderer.Parse(draft.Source); Assert.Equal("2px", graph.NodeStyles["G"]["stroke-width"]);
        Assert.Equal("#FAFAFA", graph.ClassDefs["shared"]["fill"]); Assert.False(graph.NodeStyles.ContainsKey("D"));
    }

    [Theory]
    [InlineData("click A \"https://example.invalid\"")]
    [InlineData("%%{init: {'theme':'dark'}}%%")]
    [InlineData("A@{ shape: rect }")]
    [InlineData("unknownDirective D")]
    public async Task Unknown_or_global_dependencies_fail_closed_even_for_independent_spans(string statement)
    {
        var draft = new MermaidVisualDraft(Source + statement + "\n"); Assert.False(draft.Model.HasSafeTransformations);
        await Denied(draft, new UpdateMermaidNode("D", "Changed", FlowNodeShape.Process, "D"));
        await Denied(draft, new AddMermaidNode("New", "New", FlowNodeShape.Process));
        Assert.False((await draft.ValidateAndCommitAsync(Source.Replace("Independent step", "Unproven", StringComparison.Ordinal))).Success);
        Assert.Equal(Source + statement + "\n", draft.Source);
    }

    [Fact]
    public async Task Manual_partial_source_is_distinct_from_operation_proofs_and_rejections_are_atomic()
    {
        var draft = new MermaidVisualDraft(Source); draft.Select(["node:D"]);
        Assert.False((await draft.ValidateAndCommitAsync(Source.Replace("Independent step", "Manual", StringComparison.Ordinal))).Success);
        await Denied(draft, new UpdateMermaidNode("D", "\ud800", FlowNodeShape.Process, "D"));
        await Denied(draft, new UpdateMermaidNode("D", "Changed", FlowNodeShape.Process, "A"));
        await Denied(draft, new UpdateMermaidConnection("edge-2", "Missing", "F", "Invalid", FlowConnectionType.Directed));
        var result = await draft.ExecuteBatchAsync([new UpdateMermaidNode("D", "Valid first step", FlowNodeShape.Process, "D"), new DeleteMermaidNodes(["A"])]);
        Assert.False(result.Success); Assert.Equal(Source, draft.Source); Assert.False(draft.CanUndo); Assert.Equal(["node:D"], draft.Selection);
        Assert.Throws<ArgumentException>(() => MermaidPatches.Apply(Source, [new(new(0, 10), "stale text", "x")]));
        Assert.Throws<ArgumentException>(() => MermaidPatches.Apply(Source, [new(new(0, 10), Source[..10], "x"), new(new(2, 10), Source[2..12], "y")]));
        Assert.Throws<ArgumentException>(() => MermaidPatches.Apply(Source, [new(new(2, 0), "", "x"), new(new(2, 0), "", "y")]));
    }

    public static IEnumerable<object[]> Seeds => Enumerable.Range(0, 48).Select(seed => new object[] { seed });
    [Theory] [MemberData(nameof(Seeds))]
    public async Task Deterministic_generated_partial_edits_preserve_all_unrequested_bytes(int seed)
    {
        var random = new Random(seed); var newline = seed % 3 == 0 ? "\r\n" : "\n"; var indent = seed % 2 == 0 ? "\t" : "   ";
        var target = $"D[\"Independent {seed} café e\u0301\"]";
        var lines = new List<string> { "flowchart LR", "%% seed " + seed, "A[Start] -->|Next| B[Review] --> C[Done]", target, "E[Notify]", "F[Archive]" };
        if (seed % 2 == 0) lines.Add("E -.->|Same| F");
        lines.Add("E -.->|Same| F");
        if (seed % 4 == 0) { lines.Add("classDef shared fill:#FAFAFA,stroke:#123456"); lines.Add("class D,E shared"); }
        // Shuffle independent declarations without changing the chain's internal order.
        var at = random.Next(2, lines.Count); lines.Remove(target); lines.Insert(Math.Min(at, lines.Count), target);
        var source = string.Join(newline, lines.Select((line, index) => index == 0 ? line : indent + line)) + (seed % 5 == 0 ? "" : newline);
        if (seed % 7 == 0) source = source.Replace("%% seed " + seed + newline, "%% seed " + seed + "\n", StringComparison.Ordinal);
        var draft = new MermaidVisualDraft(source); Assert.Equal(MermaidCompatibility.PartiallyEditable, draft.Model.Compatibility);
        var changedLabel = "Changed " + seed + " Ω 😀";
        await Success(draft, new UpdateMermaidNode("D", changedLabel, FlowNodeShape.Process, "D"));
        Assert.Equal(source.Replace(target, "D[\"" + changedLabel + "\"]", StringComparison.Ordinal), draft.Source);
        var edge = draft.Model.Edges[^1]; var beforeEdge = draft.Source;
        await Success(draft, new UpdateMermaidConnection(edge.Id, "E", "F", "Specific " + seed, FlowConnectionType.Dotted));
        Assert.Equal("Specific " + seed, draft.Model.Edge(edge.Id).Label);
        Assert.Contains("A[Start] -->|Next| B[Review] --> C[Done]", draft.Source);
        draft.Undo(); Assert.Equal(beforeEdge, draft.Source); draft.Undo(); Assert.Equal(source, draft.Source);
        Assert.False(draft.CanUndo); Assert.False(draft.CanApply);
    }

    private static async Task Success(MermaidVisualDraft draft, MermaidEditOperation operation)
    { var result = await draft.ExecuteAsync(operation); Assert.True(result.Success, result.Error); Assert.True(new DiagramService().Render(draft.Source).Success); }
    private static async Task Denied(MermaidVisualDraft draft, MermaidEditOperation operation)
    {
        var source = draft.Source; var history = draft.HistoryIdentities.ToArray(); var selection = draft.Selection.ToArray();
        var result = await draft.ExecuteAsync(operation); Assert.False(result.Success); Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.Equal(source, draft.Source); Assert.Equal(history, draft.HistoryIdentities); Assert.Equal(selection, draft.Selection);
    }

    [Theory]
    [InlineData("-- Accepted -->", "Accepted", FlowConnectionType.Directed)]
    [InlineData("-. Retry .->", "Retry", FlowConnectionType.Dotted)]
    [InlineData("== Saved ==>", "Saved", FlowConnectionType.Thick)]
    [InlineData("<-- Both -->", "Both", FlowConnectionType.Bidirectional)]
    [InlineData("-- \"Quoted text\" -->", "Quoted text", FlowConnectionType.Directed)]
    public async Task Pinned_inline_label_forms_preserve_tokens_until_a_type_change_requires_a_local_replacement(string arrow, string label, FlowConnectionType type)
    {
        var source = "flowchart LR\nA[Start] " + arrow + " B[End]\nC[Keep] --> D[Keep]\n";
        var draft = new MermaidVisualDraft(source); Assert.True(draft.Model.HasCompleteSourceMap,
            draft.Model.Diagnostic + " " + System.Text.Json.JsonSerializer.Serialize(Mermaider.MermaidRenderer.Parse(source)));
        var edge = draft.Model.Edges[0]; Assert.True(edge.InlineLabel); Assert.Equal(label, edge.Label); Assert.Equal(type, edge.Type);
        await Success(draft, new UpdateMermaidConnection(edge.Id, "A", "B", "Updated", type));
        Assert.Equal(source[..edge.LabelSpan!.Value.Start] + "Updated" + source[edge.LabelSpan.Value.End..], draft.Source);
        await Success(draft, new UpdateMermaidConnection(edge.Id, "A", "B", "Updated", FlowConnectionType.Undirected));
        Assert.Contains("A[Start] ---|Updated| B[End]", draft.Source); Assert.Contains("C[Keep] --> D[Keep]\n", draft.Source);
        draft.Undo(); draft.Undo(); Assert.Equal(source, draft.Source);
    }

    [Fact]
    public async Task Advanced_synthetic_lab_supports_independent_edits_and_exact_source_undo()
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "native-mermaid-visual", "advanced-flow-error-log-lab.mmd"));
        var draft = new MermaidVisualDraft(source); Assert.Equal(MermaidCompatibility.PartiallyEditable, draft.Model.Compatibility);
        await Success(draft, new UpdateMermaidNode("IndependentStep", "Reviewed synthetic step", FlowNodeShape.Process, "IndependentStep"));
        var edge = draft.Model.Edges.Single(edge => edge.Source.Id == "Notify" && edge.Target.Id == "Archive");
        await Success(draft, new UpdateMermaidConnection(edge.Id, "Notify", "Archive", "Archive verified", edge.Type));
        Assert.Equal("Independent", draft.Model.Node("IndependentStep").SubgraphId);
        Assert.True((await draft.ValidateForApplyAsync()).Success);
        var reopened = new MermaidVisualDraft(draft.Source); Assert.Equal(draft.Source, reopened.Source);
        Assert.Equal("Reviewed synthetic step", reopened.Model.Node("IndependentStep").Label);
        draft.Undo(); draft.Undo(); Assert.Equal(source, draft.Source);
    }

    [Theory]
    [InlineData("-->", FlowConnectionType.Directed)] [InlineData("---", FlowConnectionType.Undirected)]
    [InlineData("-.->", FlowConnectionType.Dotted)] [InlineData("==>", FlowConnectionType.Thick)] [InlineData("<-->", FlowConnectionType.Bidirectional)]
    public async Task Every_supported_pipe_form_preserves_quoted_labels_parallel_identities_and_the_other_statement(string token, FlowConnectionType type)
    {
        var source = "flowchart LR\r\nA[Start] --> B[Review] --> C[Done]\nD[Notify]\r\nE[Archive]\n"
            + "D " + token + "|\"Next café\"| E\r\nD " + token + "|\"Next café\"| E\n";
        var draft = new MermaidVisualDraft(source); Assert.Equal(MermaidCompatibility.PartiallyEditable, draft.Model.Compatibility);
        var target = draft.Model.Edges[3]; Assert.Equal("Next café", target.Label); var span = target.LabelSpan!.Value;
        await Success(draft, new UpdateMermaidConnection(target.Id, "D", "E", "Changed Ω", type));
        Assert.Equal(source[..span.Start] + "Changed Ω" + source[span.End..], draft.Source);
        Assert.Equal("Next café", draft.Model.Edge("edge-2").Label); Assert.Equal("Changed Ω", draft.Model.Edge("edge-3").Label);
        draft.Undo(); Assert.Equal(source, draft.Source);
    }

    [Fact]
    public async Task A_node_named_like_a_private_edge_cannot_transfer_its_capabilities_to_that_edge()
    {
        const string source = "flowchart LR\nA --> B --> C\nedge-0[Independent]\nD[Other]\n";
        var draft = new MermaidVisualDraft(source);
        Assert.True(draft.Model.Capabilities.Query(MermaidOperationKind.NodeId, "edge-0").Permitted);
        Assert.False(draft.Model.Capabilities.Query(MermaidOperationKind.Reconnect, "edge-0").Permitted);
        await Denied(draft, new UpdateMermaidConnection("edge-0", "D", "B", "", FlowConnectionType.Directed));
        await Success(draft, new UpdateMermaidNode("edge-0", "Updated independent", FlowNodeShape.Process, "edge-0"));
        Assert.Equal(source.Replace("edge-0[Independent]", "edge-0[\"Updated independent\"]", StringComparison.Ordinal), draft.Source);
    }

    [Fact]
    public async Task Fully_editable_group_flattening_preserves_child_and_direct_member_order()
    {
        const string source = "flowchart LR\nsubgraph Parent[Parent]\nBefore[Before]\nsubgraph Removed[Removed]\nMember[Member]\nsubgraph Child[Child]\nNested[Nested]\nend\nend\nAfter[After]\nsubgraph Sibling[Sibling]\nOther[Other]\nend\nend\nBefore --> Member\nMember --> After\n";
        var draft = new MermaidVisualDraft(source); Assert.True(draft.Model.CanEdit, draft.Model.Diagnostic);
        await Success(draft, new DeleteMermaidSubgraph("Removed"));
        var graph = Mermaider.MermaidRenderer.Parse(draft.Source); var parent = Assert.Single(graph.Subgraphs);
        Assert.Equal(["Before", "Member", "After"], parent.NodeIds); Assert.Equal(["Child", "Sibling"], parent.Children.Select(group => group.Id));
        Assert.Equal("Parent", draft.Model.Subgraph("Child").ParentId);
        draft.Undo(); Assert.Equal(source, draft.Source);
    }
}
