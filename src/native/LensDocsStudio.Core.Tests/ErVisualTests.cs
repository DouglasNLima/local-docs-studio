using LensDocsStudio.Core.ErDiagrams;
using LensDocsStudio.Core.Rendering;
using Mermaider.Models;

namespace LensDocsStudio.Core.Tests;

public class ErVisualTests
{
    public const string Source = "erDiagram\n    %% Preserve this comment\n    CUSTOMER {\n        string customer_id PK\n        string email UK \"Contact email\"\n        string email UK \"Repeated row\"\n    }\n    ORDER {\n        string order_id PK\n        string customer_id FK\n    }\n    CUSTOMER ||--o{ ORDER : places\n    CUSTOMER |o..o{ CUSTOMER : parent\n    ORDER ||--|{ CUSTOMER : returns\n    CUSTOMER ||--o{ ORDER : places\n";
    private readonly ErDiagramSourceParser parser = new();
    [Fact] public void Complete_source_matches_the_pinned_renderer_and_actual_layout_order()
    {
        var model = parser.Parse(Source); var render = new DiagramService().Render(Source);
        ErSemanticValidation.ValidateRendering(model, render);
        Assert.Equal(ErCompatibility.Editable, model.Compatibility);
        Assert.Equal(new[] { "places", "returns", "places", "parent" }, ErSemanticValidation.RelationshipOrder(model, render.ErLayout!).Select(item => item.Label));
        Assert.Equal(4, model.Relationships.Select(item => item.Identity).Distinct().Count());
        Assert.Equal(3, model.Entity("CUSTOMER").Attributes.Select(item => item.Identity).Distinct().Count());
    }
    public static IEnumerable<object[]> Cardinalities() => Enum.GetValues<ErCardinality>().SelectMany(first => Enum.GetValues<ErCardinality>().SelectMany(second => new[] { true, false }.Select(identifying => new object[] { first, second, identifying })));
    [Theory, MemberData(nameof(Cardinalities))]
    public async Task Every_cardinality_pair_and_relationship_type_retains_endpoint_semantics(ErCardinality first, ErCardinality second, bool identifying)
    {
        var draft = new ErVisualDraft(Source); var relationship = draft.Model.Relationships[0];
        var result = await draft.ExecuteAsync(new UpdateErRelationship(relationship.Identity, relationship.Entity1, relationship.Entity2, first, second, identifying, "edited"));
        Assert.True(result.Success, result.Error); var edited = draft.Model.Relationship(relationship.Identity);
        Assert.Equal(first, edited.Cardinality1); Assert.Equal(second, edited.Cardinality2); Assert.Equal(identifying, edited.Identifying);
        Assert.Equal("edited", edited.Label); Assert.Equal("places", draft.Model.Relationships[3].Label);
        draft.Undo(); Assert.Equal(Source, draft.Source); draft.Redo(); Assert.Equal(edited, draft.Model.Relationship(relationship.Identity));
    }
    [Theory, InlineData("TD"), InlineData("TB"), InlineData("LR"), InlineData("BT"), InlineData("RL")]
    public void Direction_matches_the_pinned_semantics(string direction)
    { var source = "erDiagram\n direction " + direction + "\n A {\n }\n"; var model = parser.Parse(source); ErSemanticValidation.ValidateRendering(model, new DiagramService().Render(source)); Assert.Equal(direction, model.Direction.ToString()); }
    [Theory, InlineData("PK"), InlineData("FK"), InlineData("UK"), InlineData("PK FK"), InlineData("UK PK FK"), InlineData("pk fk uk"), InlineData("")]
    public void Space_separated_markers_and_quoted_comments_are_completely_recognised(string keys)
    { var source = "erDiagram\n A[\"A label\"] {\n string id " + keys + " \"Comment\"\n }\n"; var model = parser.Parse(source); Assert.False(model.HasUnknownDependencies); ErSemanticValidation.ValidateRendering(model, new DiagramService().Render(source)); }
    [Theory, InlineData("string email PK, FK"), InlineData("string email UK unknown"), InlineData("string email \"ok\" extra"), InlineData("unknown"), InlineData("string email PK\tFK")]
    public async Task Unknown_entity_content_is_preserved_and_cannot_create_history(string content)
    {
        var source = "erDiagram\n A {\n " + content + "\n }\n B {\n string id PK\n }\n"; var draft = new ErVisualDraft(source);
        Assert.True(draft.Model.HasUnknownDependencies); var identity = draft.HistoryIdentity;
        var result = await draft.ExecuteAsync(new DeleteErEntity("A")); Assert.False(result.Success); Assert.Equal(source, draft.Source); Assert.Equal(identity, draft.HistoryIdentity);
        result = await draft.ExecuteAsync(new UpdateErEntity("B", "B", "Independent label")); Assert.True(result.Success, result.Error); Assert.Contains(content, draft.Source);
    }
    [Theory, InlineData("direction sideways"), InlineData("A -->> B"), InlineData("unrecognised setting")]
    public async Task Unknown_global_source_protects_reference_changes_but_allows_exact_independent_label(string unknown)
    {
        var source = "erDiagram\n A {\n }\n " + unknown + "\n"; var draft = new ErVisualDraft(source);
        Assert.False((await draft.ExecuteAsync(new UpdateErEntity("A", "Renamed", "A"))).Success); Assert.Equal(source, draft.Source);
        var result = await draft.ExecuteAsync(new UpdateErEntity("A", "A", "New label")); Assert.True(result.Success, result.Error); Assert.Contains(unknown, draft.Source);
    }
    [Fact] public async Task Attribute_edit_reorder_duplicate_and_delete_keep_exact_private_occurrences()
    {
        var draft = new ErVisualDraft(Source); var rows = draft.Model.Entity("CUSTOMER").Attributes; var first = rows[1].Identity; var second = rows[2].Identity;
        var result = await draft.ExecuteAsync(new UpdateErAttribute(second, "int", "email", new[] { ErKeyType.PK, ErKeyType.FK }, "Edited second row")); Assert.True(result.Success, result.Error);
        Assert.Equal("Contact email", draft.Model.Attribute(first).Comment); Assert.Equal("Edited second row", draft.Model.Attribute(second).Comment);
        Assert.True((await draft.ExecuteAsync(new MoveErAttribute(second, -1))).Success); Assert.Equal(second, draft.Model.Entity("CUSTOMER").Attributes[1].Identity);
        Assert.True((await draft.ExecuteAsync(new DuplicateErAttribute(second))).Success); Assert.Equal(4, draft.Model.Entity("CUSTOMER").Attributes.Count);
        Assert.True((await draft.ExecuteAsync(new DeleteErAttribute(second))).Success); Assert.DoesNotContain(draft.Model.Entity("CUSTOMER").Attributes, item => item.Identity == second);
        draft.Undo(); Assert.Equal(second, draft.Model.Entity("CUSTOMER").Attributes[1].Identity);
        for (var i = 0; i < 3; i++) draft.Undo(); Assert.Equal(Source, draft.Source); Assert.Equal(second, draft.Model.Entity("CUSTOMER").Attributes[2].Identity);
    }
    [Fact] public async Task Entity_rename_updates_self_parallel_and_all_reference_tokens_only()
    {
        var draft = new ErVisualDraft(Source); var result = await draft.ExecuteAsync(new UpdateErEntity("CUSTOMER", "CLIENT", "Client")); Assert.True(result.Success, result.Error);
        Assert.DoesNotContain("CUSTOMER", draft.Source); Assert.Contains("%% Preserve this comment", draft.Source); Assert.Equal(2, draft.Model.Relationships.Count(item => item.Entity1 == "CLIENT" && item.Entity2 == "ORDER"));
        Assert.Equal("CLIENT", draft.Model.Relationships[1].Entity2);
        Assert.False((await draft.ExecuteAsync(new DeleteErEntity("CLIENT"))).Success);
    }
    [Fact] public async Task Implicit_entities_gain_blocks_without_changing_existing_relationships()
    {
        var source = "erDiagram\n A ||--o{ B : places\n"; var draft = new ErVisualDraft(source); var identity = draft.Model.Relationships[0].Identity;
        Assert.False((await draft.ExecuteAsync(new DeleteErRelationship(identity))).Success);
        var result = await draft.ExecuteAsync(new AddErAttribute("A", "string", "id", new[] { ErKeyType.PK })); Assert.True(result.Success, result.Error); Assert.False(draft.Model.Entity("A").Implicit);
        Assert.Equal(identity, draft.Model.Relationships[0].Identity); Assert.StartsWith(source, draft.Source);
        result = await draft.ExecuteAsync(new UpdateErEntity("B", "B", "Orders")); Assert.True(result.Success, result.Error);
        Assert.True((await draft.ExecuteAsync(new DeleteErRelationship(identity))).Success); Assert.Equal(2, draft.Model.Entities.Count);
    }
    [Fact] public async Task Reverse_keeps_cardinalities_with_their_entities_and_distinguishes_repeated_loops()
    {
        var draft = new ErVisualDraft(Source + "    CUSTOMER |o..o{ CUSTOMER : parent\n"); var selected = draft.Model.Relationships[1];
        var result = await draft.ExecuteAsync(new ReverseErRelationship(selected.Identity)); Assert.True(result.Success, result.Error);
        Assert.Equal(selected.Cardinality2, draft.Model.Relationship(selected.Identity).Cardinality1); Assert.Equal(selected.Cardinality1, draft.Model.Relationship(selected.Identity).Cardinality2);
        Assert.Equal(selected.Cardinality1, draft.Model.Relationships[^1].Cardinality1); ErSemanticValidation.ValidateRendering(draft.Model, new DiagramService().Render(draft.Source));
    }
    [Theory, InlineData("bad id"), InlineData("A\n}"), InlineData("1A"), InlineData("A[bad]")]
    public async Task Invalid_ids_and_injection_are_atomic(string id)
    { var draft = new ErVisualDraft(Source); var result = await draft.ExecuteAsync(new AddErEntity(id, "Example")); Assert.False(result.Success); Assert.Equal(Source, draft.Source); Assert.Equal(1, draft.HistoryCount); }
    [Fact] public async Task Cancelled_and_stale_native_validation_cannot_commit()
    {
        var entered = new TaskCompletionSource(); var release = new TaskCompletionSource();
        var draft = new ErVisualDraft(Source, validatePresentation: async (_, _) => { entered.SetResult(); await release.Task; });
        var operation = draft.ExecuteAsync(new AddErEntity("NEW", "New")); await entered.Task; draft.Undo(); release.SetResult(); Assert.False((await operation).Success); Assert.Equal(Source, draft.Source); Assert.Equal(1, draft.HistoryCount);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); Assert.False((await new ErVisualDraft(Source).ExecuteAsync(new AddErEntity("NEW", "New"), cancellation.Token)).Success);
    }
    [Fact] public async Task Mixed_line_endings_comments_and_combining_characters_remain_byte_exact()
    {
        var source = "erDiagram\n    %% Leading comment\r\n    A[\"Café\"] {\r\n        string id UK PK \"café\"\n        %% row comment\r\n        string id\n    }\r\n"; var draft = new ErVisualDraft(source);
        var row = draft.Model.Entity("A").Attributes[0]; var result = await draft.ExecuteAsync(new UpdateErAttribute(row.Identity, "int", row.Name, row.Keys, row.Comment)); Assert.True(result.Success, result.Error);
        Assert.Equal(source.Replace("string id UK", "int id UK"), draft.Source); draft.Undo(); Assert.Equal(source, draft.Source);
    }
    [Fact] public async Task Pinned_renderer_rejection_of_a_leading_comment_cannot_mutate_history()
    { var source = "%% Before header\nerDiagram\n A {\n }\n"; var draft = new ErVisualDraft(source); Assert.False((await draft.ExecuteAsync(new UpdateErEntity("A", "A", "Label"))).Success); Assert.Equal(source, draft.Source); Assert.Equal(1, draft.HistoryCount); }
    [Fact] public async Task Bounded_history_restores_selection_and_invalidates_redo()
    {
        var draft = new ErVisualDraft("erDiagram\n A {\n }\n"); draft.Select("entity:A");
        for (var i = 0; i < 105; i++) Assert.True((await draft.ExecuteAsync(new UpdateErEntity("A", "A", "Label " + i))).Success);
        Assert.Equal(100, draft.HistoryCount); draft.Undo(); Assert.Equal("entity:A", draft.Selection); Assert.True(draft.CanRedo);
        Assert.True((await draft.ExecuteAsync(new UpdateErEntity("A", "A", "Branch"))).Success); Assert.False(draft.CanRedo);
    }
}
