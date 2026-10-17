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
    [Theory, InlineData("A[\"Original label\"]", "A[\"New label\"]"), InlineData("A[Original label]", "A[New label]"), InlineData("A", "A[\"New label\"]")]
    public async Task Alias_changes_preserve_declaration_form_and_never_move_authority(string before, string after)
    {
        var source = "erDiagram\r\n    " + before + "  {\r\n        string id PK\n    }\r\n    %% After\n"; var draft = new ErVisualDraft(source);
        var result = await draft.ExecuteAsync(new UpdateErEntity("A", "A", "New label")); Assert.True(result.Success, result.Error); Assert.Equal(source.Replace(before + "  {", after + "  {"), draft.Source);
    }
    [Fact] public async Task Duplicate_delete_and_add_entity_preserve_unrelated_source_and_collision_safety()
    {
        var draft = new ErVisualDraft(Source); Assert.False((await draft.ExecuteAsync(new DuplicateErEntity("CUSTOMER", "ORDER"))).Success); Assert.Equal(Source, draft.Source);
        var result = await draft.ExecuteAsync(new DuplicateErEntity("CUSTOMER", "COPY")); Assert.True(result.Success, result.Error); Assert.Equal(3, draft.Model.Entity("COPY").Attributes.Count); Assert.StartsWith(Source, draft.Source);
        Assert.True((await draft.ExecuteAsync(new DeleteErEntity("COPY"))).Success); Assert.Equal(Source, draft.Source);
        Assert.True((await draft.ExecuteAsync(new AddErEntity("NEW", "New label"))).Success); Assert.Equal("New label", draft.Model.Entity("NEW").Label);
    }
    [Fact] public async Task Reconnection_between_self_and_normal_routes_preserves_source_occurrences_and_cardinalities()
    {
        var draft = new ErVisualDraft(Source); var identity = draft.Model.Relationships[1].Identity; var before = draft.Model.Relationship(identity);
        Assert.True((await draft.ExecuteAsync(new UpdateErRelationship(identity, "CUSTOMER", "ORDER", before.Cardinality1, before.Cardinality2, before.Identifying, before.Label))).Success);
        Assert.Equal(identity, draft.Model.Relationships[1].Identity); Assert.Equal(before.Cardinality1, draft.Model.Relationship(identity).Cardinality1);
        var render = new DiagramService().Render(draft.Source); Assert.Equal(identity, ErSemanticValidation.RelationshipOrder(draft.Model, render.ErLayout!)[1].Identity);
        draft.Undo(); render = new DiagramService().Render(draft.Source); Assert.Equal(identity, ErSemanticValidation.RelationshipOrder(draft.Model, render.ErLayout!)[^1].Identity);
        Assert.True((await draft.ExecuteAsync(new DuplicateErRelationship(identity))).Success); Assert.NotEqual(identity, draft.Model.Relationships[^1].Identity); Assert.Equal(before.Semantic, draft.Model.Relationships[^1].Semantic);
        Assert.True((await draft.ExecuteAsync(new DeleteErRelationship(identity))).Success); Assert.DoesNotContain(draft.Model.Relationships, item => item.Identity == identity);
    }
    [Theory, InlineData("string", "bad name", "comment"), InlineData("bad type", "name", "comment"), InlineData("string", "name", "bad\"comment"), InlineData("string", "name", "line\nbreak"), InlineData("string", "name", "} inject"), InlineData("string", "name", "<br>change")]
    public async Task Invalid_attribute_properties_preserve_exact_source_history_selection_and_redo(string type, string name, string comment)
    {
        var draft = new ErVisualDraft(Source); var identity = draft.Model.Entity("CUSTOMER").Attributes[1].Identity; draft.Select(identity);
        Assert.True((await draft.ExecuteAsync(new UpdateErEntity("ORDER", "ORDER", "Orders"))).Success); draft.Undo(); var history = draft.HistoryIdentity;
        Assert.False((await draft.ExecuteAsync(new UpdateErAttribute(identity, type, name, [], comment))).Success);
        Assert.Equal(Source, draft.Source); Assert.Equal(identity, draft.Selection); Assert.Equal(history, draft.HistoryIdentity); Assert.True(draft.CanRedo);
    }
    [Fact] public async Task Repeated_blocks_and_unknown_suffixes_protect_every_owning_attribute_operation()
    {
        var source = "erDiagram\n A {\n string id\n }\n A[\"Latest alias\"] {\n int id\n }\n"; var draft = new ErVisualDraft(source); var attribute = draft.Model.Entity("A").Attributes[0];
        foreach (var operation in new ErEditOperation[] { new UpdateErEntity("A", "A", "Label"), new DuplicateErEntity("A", "COPY"), new DeleteErEntity("A"), new AddErAttribute("A", "string", "new", []), new DeleteErAttribute(attribute.Identity), new MoveErAttribute(attribute.Identity, 1), new DuplicateErAttribute(attribute.Identity) })
        { Assert.False((await draft.ExecuteAsync(operation)).Success); Assert.Equal(source, draft.Source); Assert.Equal(1, draft.HistoryCount); }
    }
    [Fact] public async Task Native_conversion_failure_and_no_op_leave_history_and_selection_exact()
    {
        var draft = new ErVisualDraft(Source, validatePresentation: (_, _) => throw new System.IO.InvalidDataException("Synthetic native conversion rejection")); var identity = draft.Model.Relationships[0].Identity; draft.Select(identity);
        Assert.False((await draft.ExecuteAsync(new AddErEntity("NEW", "New"))).Success); Assert.Equal(Source, draft.Source); Assert.Equal(1, draft.HistoryCount); Assert.Equal(identity, draft.Selection);
        draft = new ErVisualDraft(Source); var row = draft.Model.Entity("CUSTOMER").Attributes[0]; var result = await draft.ExecuteAsync(new UpdateErAttribute(row.Identity, row.Type, row.Name, row.Keys, row.Comment)); Assert.True(result.Success, result.Error); Assert.Equal(Source, draft.Source); Assert.Equal(1, draft.HistoryCount);
    }
    [Theory, InlineData("A ||--o{ B"), InlineData("A ||--o{ B : 'single quoted'"), InlineData("A ||--o{ B : \"double quoted\""), InlineData("A ||--o{ B : plain label")]
    public async Task Optional_and_quoted_labels_retain_unrelated_notation(string relationship)
    {
        var source = "erDiagram\n A {\n }\n B {\n }\n " + relationship + "\n"; var draft = new ErVisualDraft(source); var current = draft.Model.Relationships.Single();
        var result = await draft.ExecuteAsync(new UpdateErRelationship(current.Identity, "A", "B", current.Cardinality1, current.Cardinality2, current.Identifying, "Updated label")); Assert.True(result.Success, result.Error);
        Assert.Contains("A ||--o{ B : " + (relationship.Contains('\'') ? "'Updated label'" : "\"Updated label\""), draft.Source);
        Assert.True((await draft.ExecuteAsync(new UpdateErRelationship(current.Identity, "A", "B", current.Cardinality1, current.Cardinality2, current.Identifying, ""))).Success); Assert.EndsWith(" A ||--o{ B\n", draft.Source);
    }
    [Theory, InlineData("Line\\nSecond"), InlineData("Line<br/>Second"), InlineData("**Bold** and *italic*"), InlineData("~~Removed~~ <sub>suffix</sub>")]
    public async Task Pinned_comment_and_relationship_formatting_is_verified_without_rewriting_original_tokens(string text)
    {
        var source = "erDiagram\n A {\n string id PK \"" + text + "\"\n }\n B {\n }\n A ||--o{ B : \"" + text + "\"\n"; var draft = new ErVisualDraft(source);
        ErSemanticValidation.ValidateRendering(draft.Model, new DiagramService().Render(source));
        var result = await draft.ExecuteAsync(new UpdateErEntity("B", "B", "Independent")); Assert.True(result.Success, result.Error); Assert.Equal(source.Replace(" B {", " B[\"Independent\"] {"), draft.Source);
    }
    [Fact] public async Task Last_independent_entity_deletion_and_empty_draft_creation_are_reversible()
    {
        var source = "erDiagram\n A {\n }\n"; var draft = new ErVisualDraft(source); var result = await draft.ExecuteAsync(new DeleteErEntity("A")); Assert.True(result.Success, result.Error);
        Assert.Equal("erDiagram\n", draft.Source); Assert.Empty(draft.Model.Entities); draft.Undo(); Assert.Equal(source, draft.Source); draft.Redo();
        result = await draft.ExecuteAsync(new AddErEntity("B", "New entity")); Assert.True(result.Success, result.Error); Assert.Equal("B", draft.Model.Entities.Single().Id);
    }
    [Theory, InlineData("Line\\nSecond"), InlineData("Line<br/>Second"), InlineData("**Bold** and *italic*"), InlineData("~~Removed~~ <sub>suffix</sub>")]
    public async Task Key_and_cardinality_changes_retain_raw_comment_and_label_escaping(string text)
    {
        var source = "erDiagram\r\n A {\n string id UK   \"" + text + "\"\r\n }\n B {\n }\n A ||--o{ B : '" + text + "'\n";
        var draft = new ErVisualDraft(source); var attribute = draft.Model.Entity("A").Attributes.Single();
        var result = await draft.ExecuteAsync(new UpdateErAttribute(attribute.Identity, attribute.Type, attribute.Name, new[] { ErKeyType.UK, ErKeyType.PK }, attribute.Comment)); Assert.True(result.Success, result.Error);
        Assert.Equal(source.Replace("id UK   ", "id UK PK   "), draft.Source);
        var relationship = draft.Model.Relationships.Single(); result = await draft.ExecuteAsync(new UpdateErRelationship(relationship.Identity, "A", "B", ErCardinality.ZeroOne, relationship.Cardinality2, relationship.Identifying, relationship.Label)); Assert.True(result.Success, result.Error);
        Assert.Equal(source.Replace("id UK   ", "id UK PK   ").Replace("A ||--o{ B", "A |o--o{ B"), draft.Source);
    }
    [Fact] public async Task Unsupported_implicit_identity_cannot_be_materialised_by_an_attribute_edit()
    {
        var source = "erDiagram\n BAD-ID ||--o{ OK\n"; var draft = new ErVisualDraft(source);
        var operation = new AddErAttribute("BAD-ID", "string", "id", []);
        Assert.False(ErCapabilityAnalysis.For(draft.Model, operation).Allowed); Assert.False((await draft.ExecuteAsync(operation)).Success);
        Assert.Equal(source, draft.Source); Assert.Equal(1, draft.HistoryCount);
    }
    [Fact] public async Task Moving_byte_identical_attributes_moves_private_identity_and_the_next_edit_targets_that_row()
    {
        var source = "erDiagram\n A {\n string id UK\n string id UK\n }\n"; var draft = new ErVisualDraft(source); var selected = draft.Model.Entity("A").Attributes[1].Identity; draft.Select(selected);
        var moved = await draft.ExecuteAsync(new MoveErAttribute(selected, -1)); Assert.True(moved.Success, moved.Error);
        Assert.Equal(source, draft.Source); Assert.Equal(2, draft.HistoryCount); Assert.Equal(selected, draft.Model.Entity("A").Attributes[0].Identity); Assert.Equal(selected, draft.Selection);
        var edited = await draft.ExecuteAsync(new UpdateErAttribute(selected, "int", "id", new[] { ErKeyType.UK }, "Moved")); Assert.True(edited.Success, edited.Error);
        Assert.Equal("erDiagram\n A {\n int id UK \"Moved\"\n string id UK\n }\n", draft.Source);
        draft.Undo(); Assert.Equal(source, draft.Source); Assert.Equal(selected, draft.Model.Entity("A").Attributes[0].Identity);
        draft.Undo(); Assert.Equal(source, draft.Source); Assert.Equal(selected, draft.Model.Entity("A").Attributes[1].Identity);
        draft.Redo(); Assert.Equal(selected, draft.Model.Entity("A").Attributes[0].Identity); draft.Redo(); Assert.Equal("Moved", draft.Model.Attribute(selected).Comment);
    }
    [Fact] public async Task Unknown_dependencies_cannot_be_bypassed_by_materialising_an_implicit_entity()
    {
        var source = "erDiagram\n A ||--o{ B\n unknown_global_statement\n"; var draft = new ErVisualDraft(source);
        foreach (var operation in new ErEditOperation[] { new AddErAttribute("B", "string", "id", []), new UpdateErEntity("B", "B", "New alias") })
        { Assert.False(ErCapabilityAnalysis.For(draft.Model, operation).Allowed); Assert.False((await draft.ExecuteAsync(operation)).Success); Assert.Equal(source, draft.Source); Assert.Equal(1, draft.HistoryCount); }
    }
    [Fact] public void Canonical_layout_cannot_duplicate_one_entity_while_omitting_another()
    {
        var model = parser.Parse(Source); var render = new DiagramService().Render(Source); var layout = render.ErLayout!;
        Assert.Throws<ArgumentException>(() => ErSemanticValidation.ValidateRendering(model, render with { ErLayout = layout with { Entities = new[] { layout.Entities[0], layout.Entities[0] } } }));
    }
    [Theory, InlineData("A[\"Label \\\"quoted\\\"\"]"), InlineData("A[\"Label\" suffix]")]
    public async Task Quoted_alias_fallbacks_remain_protected_instead_of_being_rewritten_as_bare_aliases(string alias)
    {
        var source = "erDiagram\n " + alias + " {\n string id\n }\n"; var draft = new ErVisualDraft(source);
        Assert.True(draft.Model.HasUnknownDependencies);
        Assert.False((await draft.ExecuteAsync(new UpdateErEntity("A", "A", "Replacement"))).Success); Assert.Equal(source, draft.Source); Assert.Equal(1, draft.HistoryCount);
    }
}
