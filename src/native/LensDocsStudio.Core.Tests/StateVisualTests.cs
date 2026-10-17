using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.StateDiagrams;
using Mermaider;
using Xunit;

namespace LensDocsStudio.Core.Tests;

public sealed class StateVisualTests
{
    private const string Simple = "stateDiagram-v2\n    %% Keep this comment.\n    [*] --> Draft\n    Draft --> Review : Submit\n    Review --> Draft : Changes\n    Review --> [*]\n";
    private static async Task Success(StateVisualDraft draft, StateEditOperation operation)
    { var result = await draft.ExecuteAsync(operation); Assert.True(result.Success, result.Error); }
    private static async Task Rejected(StateVisualDraft draft, StateEditOperation operation)
    {
        var source = draft.Source; var model = draft.Model; var history = draft.HistoryIdentity; var selection = draft.Selection;
        var result = await draft.ExecuteAsync(operation); Assert.False(result.Success); Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.Equal(source, draft.Source); Assert.Same(model, draft.Model); Assert.Equal(history, draft.HistoryIdentity); Assert.Equal(selection, draft.Selection);
    }
    [Theory]
    [InlineData("stateDiagram-v2")]
    [InlineData("stateDiagram")]
    public async Task Complete_private_workflow_preserves_unrelated_bytes_and_distinct_history(string header)
    {
        var original = Simple.Replace("stateDiagram-v2", header); var draft = new StateVisualDraft(original);
        var returnEdge = draft.Model.Transitions[2].Identity; draft.Select(returnEdge);
        await Success(draft, new UpdateStateNode("Draft", "Draft", "Local draft"));
        Assert.Contains("    state \"Local draft\" as Draft\n    [*] --> Draft", draft.Source);
        Assert.Contains("    %% Keep this comment.\n", draft.Source);
        await Success(draft, new UpdateStateTransition(returnEdge, "Review", "Draft", "Please revise"));
        await Success(draft, new AddStateNode("Archived", "Archived locally"));
        await Success(draft, new AddStateTransition("Review", "Archived", "Archive"));
        await Success(draft, new AddStateTransition("Archived", "Archived", "Check again"));
        var final = draft.Source; var identity = draft.HistoryIdentity;
        draft.Undo(); Assert.DoesNotContain("Archived --> Archived", draft.Source); draft.Redo(); Assert.Equal(final, draft.Source); Assert.Equal(identity, draft.HistoryIdentity);
        Assert.Equal(returnEdge, draft.Selection); Assert.Equal(original, draft.OriginalSource);
        Assert.True((await draft.ValidateAsync()).Success);
        while (draft.CanUndo) draft.Undo(); Assert.Equal(original, draft.Source);
        while (draft.CanRedo) draft.Redo(); Assert.Equal(final, draft.Source);
    }
    [Fact]
    public async Task Identical_transition_occurrences_never_share_identity()
    {
        var draft = new StateVisualDraft("stateDiagram-v2\nstate \"A\" as A\nstate \"B\" as B\nA --> B : Same\nA --> B : Same\nB --> A\nA --> A\n");
        var first = draft.Model.Transitions[0].Identity; var second = draft.Model.Transitions[1].Identity; Assert.NotEqual(first, second);
        await Success(draft, new UpdateStateTransition(second, "A", "B", "Second"));
        Assert.Equal("Same", draft.Model.Transition(first).Label); Assert.Equal("Second", draft.Model.Transition(second).Label);
        await Success(draft, new UpdateStateTransition(second, "B", "A", "Second"));
        await Success(draft, new DeleteStateTransition(first)); Assert.Equal(second, draft.Model.Transitions[0].Identity);
        draft.Undo(); Assert.Equal(first, draft.Model.Transitions[0].Identity);
    }
    [Fact]
    public async Task Implicit_state_endpoint_changes_cannot_silently_delete_or_reorder_unrelated_states()
    {
        var draft = new StateVisualDraft("stateDiagram-v2\nA --> B\nstate \"C\" as C\n");
        await Rejected(draft, new UpdateStateTransition(draft.Model.Transitions[0].Identity, "A", "C", ""));
        await Rejected(draft, new DeleteStateTransition(draft.Model.Transitions[0].Identity));
    }
    [Fact]
    public async Task Scope_sensitive_pseudo_states_and_nested_hierarchy_are_verified()
    {
        var source = "stateDiagram-v2\nstate \"Independent\" as Independent\n[*] --> Independent\nstate Outer {\n[*] --> A\nstate Inner {\n[*] --> B\nB --> [*]\n}\nA --> [*]\n}\n";
        var draft = new StateVisualDraft(source); Assert.Equal(5, draft.Model.PseudoNodes.Count);
        Assert.True((await draft.ValidateAsync()).Success);
        await Success(draft, new UpdateStateNode("Independent", "Independent", "Independent label"));
        await Success(draft, new UpdateStateGroupLabel("Outer", "Outer review"));
        Assert.Contains("state \"Outer review\" as Outer {", draft.Source);
        await Rejected(draft, new UpdateStateNode("Independent", "Renamed", "Independent label"));
        await Rejected(draft, new AddStateTransition("Independent", "A"));
    }
    [Fact]
    public async Task Pseudo_state_connections_are_not_globally_renamable_states()
    {
        var draft = new StateVisualDraft("stateDiagram-v2\nstate \"A\" as A\nstate \"B\" as B\n");
        await Success(draft, new AddStateTransition("[*]", "A")); await Success(draft, new AddStateTransition("B", "[*]"));
        Assert.Equal(2, draft.Model.PseudoNodes.Count);
        await Rejected(draft, new UpdateStateTransition(draft.Model.Transitions[0].Identity, "A", draft.Model.PseudoNodes[0].Identity, ""));
        await Success(draft, new DeleteStateTransition(draft.Model.Transitions[0].Identity)); Assert.Single(draft.Model.PseudoNodes);
        await Success(draft, new DeleteStateTransition(draft.Model.Transitions[0].Identity)); Assert.Empty(draft.Model.PseudoNodes);
    }
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public async Task Unicode_comments_and_line_endings_survive_exact_patches(string newline)
    {
        var source = "stateDiagram-v2\n\t%% Café é 日本語\n\tstate \"Draft\" as A\n\tstate \"Same\" as B\n\tA --> B : Submit\n".ReplaceLineEndings(newline);
        var draft = new StateVisualDraft(source); await Success(draft, new UpdateStateNode("A", "A", "Café é 日本語 \"quoted\" #65;"));
        Assert.Contains("\t%% Café é 日本語" + newline, draft.Source);
        Assert.Contains("\tstate \"Same\" as B" + newline + "\tA --> B : Submit" + newline, draft.Source);
        Assert.Equal("Café é 日本語 \"quoted\" #65;", draft.Model.Node("A").Label);
        draft.Undo(); Assert.Equal(source, draft.Source);
    }
    [Fact]
    public async Task Independent_declared_states_can_be_renamed_duplicated_and_deleted()
    {
        var draft = new StateVisualDraft("stateDiagram-v2\nstate \"Same\" as A\nstate \"Same\" as B\nA --> B\n");
        await Success(draft, new UpdateStateNode("B", "C", "Same")); Assert.Equal("C", draft.Model.Transitions[0].Destination);
        await Success(draft, new DuplicateStateNode("C", "Copy")); await Success(draft, new DeleteStateNode("Copy"));
        await Rejected(draft, new DeleteStateNode("C"));
        await Success(draft, new DeleteStateTransition(draft.Model.Transitions[0].Identity)); await Success(draft, new DeleteStateNode("C"));
    }
    [Theory]
    [InlineData("state \"First\" as A\nstate \"Duplicate\" as A\n", "A")]
    [InlineData("state \"A\" as A\nunknown directive\n", "A")]
    [InlineData("state \"A\" as A\nstate Outer {\nA --> A\n}\n", "A")]
    public async Task Ambiguous_or_unknown_dependencies_reject_edits_atomically(string body, string id)
    { var draft = new StateVisualDraft("stateDiagram-v2\n" + body); draft.Select("state:" + id); await Rejected(draft, new UpdateStateNode(id, id, "Changed")); }
    [Fact]
    public async Task Known_protected_notes_and_styles_survive_independent_label_edits()
    {
        var source = "stateDiagram-v2\nstate \"A\" as A\nstate \"B\" as B\nA --> B : Submit\nnote right of A : Keep exactly\nclassDef quiet fill:#fafafa\nclass A quiet\n";
        var draft = new StateVisualDraft(source); await Success(draft, new UpdateStateNode("B", "B", "Review"));
        await Success(draft, new UpdateStateTransition(draft.Model.Transitions[0].Identity, "A", "B", "Edited"));
        Assert.EndsWith("note right of A : Keep exactly\nclassDef quiet fill:#fafafa\nclass A quiet\n", draft.Source);
        await Rejected(draft, new UpdateStateNode("A", "Renamed", "A")); await Rejected(draft, new AddStateNode("C", "C"));
    }
    [Theory]
    [InlineData("bad id")]
    [InlineData("A\nB")]
    [InlineData("_start")]
    [InlineData("123")]
    [InlineData("B")]
    public async Task Invalid_or_duplicate_identifiers_leave_private_state_untouched(string id)
    { var draft = new StateVisualDraft("stateDiagram-v2\nstate \"A\" as A\nstate \"B\" as B\n"); await Rejected(draft, new AddStateNode(id, "Label")); }
    [Fact]
    public async Task Invalid_delimiters_cancellation_and_unknown_destinations_are_atomic()
    {
        var draft = new StateVisualDraft(Simple); var original = draft.Source;
        Assert.False((await draft.ValidateSourceAsync(Simple + "}\n")).Success); Assert.Equal(original, draft.Source);
        await Rejected(draft, new AddStateTransition("Draft", "Missing"));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.False((await draft.ExecuteAsync(new AddStateNode("Added", "Added"), cancellation.Token)).Success);
        Assert.Equal(1, draft.HistoryCount); Assert.Equal(original, draft.Source);
        await Rejected(draft, new UpdateStateNode("Draft", "Draft", "Invalid\0"));
    }
    [Fact]
    public void Expected_text_patches_reject_stale_and_overlapping_source()
    {
        Assert.Throws<ArgumentException>(() => MermaidPatches.Apply("abc", [new(new(0, 1), "x", "z")]));
        Assert.Throws<ArgumentException>(() => MermaidPatches.Apply("abc", [new(new(0, 2), "ab", "z"), new(new(1, 1), "b", "z")]));
    }
    [Fact]
    public async Task Configuration_directives_are_not_inert_comments()
    {
        var draft = new StateVisualDraft("%%{init: {'theme':'dark'}}%%\nstateDiagram-v2\nstate \"A\" as A\nA --> A\n");
        Assert.True(draft.Model.HasUnknownDependencies); await Rejected(draft, new UpdateStateNode("A", "A", "Changed"));
    }
    [Fact]
    public async Task Hyphenated_existing_state_labels_use_the_verified_description_grammar()
    {
        var draft = new StateVisualDraft("stateDiagram-v2\nA --> review-state : Submit\nreview-state --> A\n");
        await Success(draft, new UpdateStateNode("review-state", "review-state", "Local review")); Assert.Contains("review-state : Local review", draft.Source);
    }
    [Fact]
    public async Task No_op_does_not_add_history_and_same_text_after_undo_has_a_new_branch_identity()
    {
        var draft = new StateVisualDraft("stateDiagram-v2\nstate \"A\" as A\n");
        await Success(draft, new UpdateStateNode("A", "A", "A")); Assert.Equal(1, draft.HistoryCount);
        await Success(draft, new UpdateStateNode("A", "A", "First")); var identity = draft.HistoryIdentity;
        draft.Undo(); await Success(draft, new UpdateStateNode("A", "A", "First")); Assert.NotEqual(identity, draft.HistoryIdentity); Assert.False(draft.CanRedo);
    }
    [Fact]
    public async Task Stale_in_flight_validation_cannot_replace_a_newer_source_revision()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var draft = new StateVisualDraft("stateDiagram-v2\nstate \"A\" as A\n", validatePresentation: async (_, token) => { entered.SetResult(); await release.Task.WaitAsync(token); });
        var original = draft.Source; var validation = draft.ValidateSourceAsync("stateDiagram-v2\nstate \"B\" as B\n");
        await entered.Task; draft.Undo(); release.SetResult(); var result = await validation; Assert.False(result.Success); Assert.Equal(original, draft.Source); Assert.Equal(1, draft.HistoryCount);
    }
    [Fact]
    public async Task Failed_native_presentation_validation_cannot_commit_private_source_or_history()
    {
        var draft = new StateVisualDraft("stateDiagram-v2\nstate \"A\" as A\n", validatePresentation: (_, _) => throw new InvalidDataException("Synthetic native conversion rejection"));
        draft.Select("state:A"); await Rejected(draft, new UpdateStateNode("A", "A", "Changed")); Assert.Equal(1, draft.HistoryCount);
    }
    [Fact]
    public async Task Empty_supported_source_can_start_with_an_independent_state()
    { var draft = new StateVisualDraft("stateDiagram-v2\n"); await Success(draft, new AddStateNode("Draft", "Draft")); Assert.Single(draft.Model.Nodes); }
    [Fact]
    public async Task Composite_headings_preserve_the_pinned_literal_break_tag_semantics()
    {
        var draft = new StateVisualDraft("stateDiagram-v2\nstate \"Outer<br/>literal\" as Outer {\nA --> A\n}\n");
        Assert.True((await draft.ValidateAsync()).Success); await Success(draft, new UpdateStateGroupLabel("Outer", "Local composite")); Assert.Contains("state \"Local composite\" as Outer {", draft.Source);
    }
    [Fact]
    public async Task Bounded_private_history_retains_the_immutable_original_and_valid_redo_branch()
    {
        var original = "stateDiagram-v2\n%% " + new string('x', 89_000) + "\nstate \"Original\" as A\n"; var draft = new StateVisualDraft(original);
        for (var i = 0; i < 101; i++) await Success(draft, new UpdateStateNode("A", "A", "Revision " + i));
        Assert.InRange(draft.HistoryCount, 2, 89); Assert.Equal(original, draft.OriginalSource); var final = draft.Source;
        while (draft.CanUndo) draft.Undo(); while (draft.CanRedo) draft.Redo(); Assert.Equal(final, draft.Source);
    }
    [Fact]
    public async Task Implicit_unindented_state_can_rename_and_materialise_its_label_in_one_exact_edit()
    {
        var draft = new StateVisualDraft("stateDiagram-v2\nDraft --> Review\nReview --> Draft\n");
        await Success(draft, new UpdateStateNode("Draft", "LocalDraft", "Local draft"));
        Assert.Equal("stateDiagram-v2\n    state \"Local draft\" as LocalDraft\nLocalDraft --> Review\nReview --> LocalDraft\n", draft.Source);
        draft.Undo(); Assert.Equal(draft.OriginalSource, draft.Source);
    }
    [Fact]
    public void Pathological_statement_counts_are_bounded_without_discarding_source()
    {
        var source = "stateDiagram-v2\n" + string.Join('\n', Enumerable.Repeat("unrecognised directive", 3000)); var model = new StateDiagramSourceParser().Parse(source);
        Assert.False(model.IsValid); Assert.Equal(source, model.Source); Assert.InRange(model.Statements.Count, 1, 1000); Assert.False(StateCapabilityAnalysis.For(model, new AddStateNode("A", "A")).Allowed);
    }
    [Theory]
    [InlineData("stateDiagram-v2\nA --> B\n", 2, 1, 0)]
    [InlineData("stateDiagram\nstate \"Alias\" as A\nA --> A : Again\n", 1, 1, 0)]
    [InlineData("stateDiagram-v2\nstate C <<choice>>\nstate F <<fork>>\nstate J <<join>>\nC --> F\nF --> J\n", 3, 2, 0)]
    [InlineData("stateDiagram-v2\nstate Outer {\n[*] --> A\nA --> [*]\n}\n", 3, 2, 1)]
    [InlineData("stateDiagram-v2\nA : Description\nA --> A : Internal\n", 1, 1, 0)]
    public void Actual_pinned_parser_and_rendered_source_model_agree(string source, int nodes, int edges, int groups)
    {
        var graph = MermaidRenderer.Parse(source); Assert.Equal(nodes, graph.Nodes.Count); Assert.Equal(edges, graph.Edges.Count); Assert.Equal(groups, graph.Subgraphs.Count);
        var model = new StateDiagramSourceParser().Parse(source); StateSemanticValidation.ValidateRendering(model, new DiagramService().Render(source));
    }
}
