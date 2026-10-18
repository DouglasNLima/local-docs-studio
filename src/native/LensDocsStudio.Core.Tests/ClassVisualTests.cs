using LensDocsStudio.Core.ClassDiagrams;
using LensDocsStudio.Core.Rendering;
using Mermaider.Models;

namespace LensDocsStudio.Core.Tests;

public class ClassVisualTests
{
    public const string Source = "classDiagram\n    %% Keep this comment\n    class Document {\n        +string Title\n        +string Title\n        +Open(path) Document\n        +Open(path) Document\n        -Validate() bool\n    }\n    class MarkdownDocument {\n        +Render() string\n    }\n    Document <|-- MarkdownDocument\n    Document \"1\" *-- \"many\" MarkdownDocument : owns\n    Document <|-- MarkdownDocument\n    Document --> Document : parent\n";
    public const string Starter = ClassDiagramSourceParser.Starter;
    private static void Success(ClassEditResult result) => Assert.True(result.Success, result.Error);
    [Theory, InlineData(Source), InlineData(Starter), InlineData("classDiagram\n"), InlineData("classDiagram\nclass A\n"), InlineData("classDiagram\nA --> B\n"), InlineData("classdiagram\ndirection LR\nclass A~T~ {\n<<interface>>\n~string value$\n#Open*(path) bool\n}\nnote for A \"Keep this note\"\n"), InlineData("classDiagram\nnamespace Library {\nclass A {\n+int id\n}\nclass B\n}\nA --> B\n"), InlineData("classDiagram\nA : +Run(path) bool\nA : -int id\nclass A\nclass A\n"), InlineData("classDiagram\nclass A { <<abstract>> }\n")]
    public void Independent_source_semantics_match_the_pinned_parser_and_single_canonical_layout(string source)
    {
        var model = new ClassDiagramSourceParser().Parse(source); var render = new DiagramService().Render(source);
        ClassDiagramSemanticValidation.ValidateRendering(model, render); Assert.True(model.IsValid);
        Assert.Equal(model.Relationships.Count, render.ClassLayout!.Relationships.Count);
        Assert.Equal(model.Classes.Sum(item => item.Members.Count), model.Classes.SelectMany(item => item.Members).Select(item => item.Identity).Distinct().Count());
    }
    [Fact] public async Task Attribute_changes_preserve_every_unrelated_byte_and_private_occurrence()
    {
        var draft = new ClassDiagramVisualDraft(Source); var cls = draft.Model.Class("Document"); var member = cls.Attributes[1]; draft.Select(member.Identity);
        Success(await draft.ExecuteAsync(new UpdateClassMember(member.Identity, member.Semantic with { Type = "int", Name = "Revision", Visibility = ClassVisibility.Private })));
        var at = Source.IndexOf("+string Title", StringComparison.Ordinal); at = Source.IndexOf("+string Title", at + 1, StringComparison.Ordinal);
        Assert.Equal(Source[..at] + "-int Revision" + Source[(at + "+string Title".Length)..], draft.Source);
        Assert.Equal(cls.Identity, draft.Model.Class("Document").Identity); Assert.Equal(member.Identity, draft.Model.Class("Document").Attributes[1].Identity);
        draft.Undo(); Assert.Equal(Source, draft.Source); Assert.Equal(member.Identity, draft.Selection); draft.Redo(); Assert.Equal("Revision", draft.Model.Member(member.Identity).Semantic.Name);
    }
    [Fact] public async Task Duplicate_methods_remain_independent_through_add_edit_reorder_delete_undo_and_redo()
    {
        var draft = new ClassDiagramVisualDraft(Source); var members = draft.Model.Class("Document").Methods; var first = members[0]; var second = members[1];
        Success(await draft.ExecuteAsync(new UpdateClassMember(second.Identity, second.Semantic with { Params = "path, options", Type = "bool" })));
        Assert.Equal(first.Semantic, draft.Model.Member(first.Identity).Semantic);
        Success(await draft.ExecuteAsync(new MoveClassMember(second.Identity, -1))); Assert.Equal(second.Identity, draft.Model.Class("Document").Methods[0].Identity);
        Success(await draft.ExecuteAsync(new DuplicateClassMember(second.Identity))); Assert.Equal(4, draft.Model.Class("Document").Methods.Count);
        Success(await draft.ExecuteAsync(new DeleteClassMember(second.Identity))); Assert.DoesNotContain(draft.Model.Class("Document").Members, item => item.Identity == second.Identity);
        draft.Undo(); Assert.Equal(second.Identity, draft.Model.Class("Document").Methods[0].Identity);
        draft.Redo(); Assert.Equal(3, draft.Model.Class("Document").Methods.Count);
    }
    public static IEnumerable<object[]> Relationships() => Enum.GetValues<ClassRelationType>().Where(type => type != ClassRelationType.Lollipop).SelectMany(type => Enum.GetValues<ClassMarkerAt>().SelectMany(marker => new string?[] { null, "1" }.SelectMany(first => new string?[] { null, "0..*" }.Select(second => new object?[] { type, marker, first, second }))))!;
    [Theory, MemberData(nameof(Relationships))]
    public async Task Every_supported_relationship_marker_and_cardinality_pair_survives_reverse_and_reconnection(ClassRelationType type, ClassMarkerAt marker, string? first, string? second)
    {
        var draft = new ClassDiagramVisualDraft(Source); var identity = draft.Model.Relationships[0].Identity; var untouched = draft.Model.Relationships[2];
        var value = new ClassRelationship("Document", "MarkdownDocument", type, marker, "edited", first, second);
        Success(await draft.ExecuteAsync(new UpdateClassRelationship(identity, value))); Assert.Equal(value, draft.Model.Relationship(identity).Semantic);
        Success(await draft.ExecuteAsync(new ReverseClassRelationship(identity))); var reversed = draft.Model.Relationship(identity).Semantic;
        Assert.Equal(value.To, reversed.From); Assert.Equal(value.FromCardinality, reversed.ToCardinality); Assert.Equal(value.ToCardinality, reversed.FromCardinality);
        Assert.NotEqual(marker, reversed.MarkerAt); Assert.Equal(untouched.Semantic, draft.Model.Relationship(untouched.Identity).Semantic);
        Success(await draft.ExecuteAsync(new UpdateClassRelationship(identity, reversed with { To = reversed.From }))); Assert.Equal(draft.Model.Relationship(identity).Semantic.From, draft.Model.Relationship(identity).Semantic.To);
        draft.Undo(); Assert.Equal(reversed, draft.Model.Relationship(identity).Semantic);
    }
    [Theory, InlineData(""), InlineData("+"), InlineData("-"), InlineData("#"), InlineData("~")]
    public async Task Visibility_and_optional_types_are_exactly_patchable(string prefix)
    {
        var source = "classDiagram\r\nclass A {\n    " + prefix + "Value\r\n    " + prefix + "Run()\n}\r\n"; var draft = new ClassDiagramVisualDraft(source);
        var attribute = draft.Model.Class("A").Attributes[0]; Success(await draft.ExecuteAsync(new UpdateClassMember(attribute.Identity, attribute.Semantic with { Type = "string", Name = "Title", Visibility = ClassVisibility.Protected })));
        Assert.Contains("    #string Title\r\n", draft.Source);
        var method = draft.Model.Class("A").Methods[0]; Success(await draft.ExecuteAsync(new UpdateClassMember(method.Identity, method.Semantic with { Params = "path", Type = "bool", Visibility = ClassVisibility.Public })));
        Assert.Contains("    +Run(path) bool\n", draft.Source);
        Success(await draft.ExecuteAsync(new UpdateClassMember(attribute.Identity, draft.Model.Member(attribute.Identity).Semantic with { Type = null })));
        Assert.Contains("#Title", draft.Source); draft.Undo(); Assert.Contains("#string Title", draft.Source);
    }
    [Fact] public async Task Implicit_materialisation_preserves_first_occurrence_and_class_identity()
    {
        var source = "classDiagram\nA --> B\n"; var draft = new ClassDiagramVisualDraft(source); var identity = draft.Model.Class("B").Identity;
        Success(await draft.ExecuteAsync(new AddClassMember("B", new(ClassVisibility.Public, "Name", "string"))));
        Assert.StartsWith(source, draft.Source); Assert.False(draft.Model.Class("B").Implicit); Assert.Equal(identity, draft.Model.Class("B").Identity); Assert.Equal(new[] { "A", "B" }, draft.Model.Classes.Select(item => item.Id));
    }
    [Fact] public async Task Rename_updates_all_known_references_and_retains_namespace_notes_generic_and_annotation()
    {
        var source = "classDiagram\nnamespace Library {\nclass A~T~ {\n<<interface>>\n+string name\n}\n}\nA : +Run() bool\nA \"1\" *-- \"many\" B : owns\nnote for A \"A is documented\"\n";
        var draft = new ClassDiagramVisualDraft(source); var identity = draft.Model.Class("A").Identity;
        Success(await draft.ExecuteAsync(new UpdateClass("A", "Document", "T", "interface")));
        Assert.Equal(identity, draft.Model.Class("Document").Identity); Assert.Equal("Document<T>", draft.Model.Class("Document").Label);
        Assert.Equal(new[] { "Document" }, draft.Model.Namespaces[0].ClassIds); Assert.Equal("Document", draft.Model.Notes[0].Semantic.TargetClassId);
        Assert.Contains("A is documented", draft.Source); Assert.Contains("Document : +Run() bool", draft.Source);
        draft.Undo(); Assert.Equal(source, draft.Source);
    }
    [Fact] public async Task Independent_class_lifecycle_does_not_remove_incident_relationships()
    {
        var draft = new ClassDiagramVisualDraft(Source); Assert.False((await draft.ExecuteAsync(new DeleteClass("Document"))).Success); Assert.Equal(Source, draft.Source);
        Success(await draft.ExecuteAsync(new DuplicateClass("Document", "Copy"))); Assert.Equal(5, draft.Model.Class("Copy").Members.Count);
        Success(await draft.ExecuteAsync(new DeleteClass("Copy"))); Assert.Equal(Source, draft.Source);
        Success(await draft.ExecuteAsync(new AddClass("NewClass", "T", "interface"))); Assert.Equal("NewClass<T>", draft.Model.Class("NewClass").Label);
        Success(await draft.ExecuteAsync(new AddClassMember("NewClass", new(ClassVisibility.Public, "Run", "bool", false, false, true, "path"))));
        Success(await draft.ExecuteAsync(new AddClassRelationship(new("Document", "NewClass", ClassRelationType.Inheritance, ClassMarkerAt.From))));
    }
    [Fact] public async Task Implicit_generic_and_annotation_materialisation_and_exact_updates_preserve_relationship_occurrences()
    {
        var source = "classDiagram\nA --> B\n"; var draft = new ClassDiagramVisualDraft(source); var relationship = draft.Model.Relationships[0].Identity;
        Success(await draft.ExecuteAsync(new UpdateClass("B", "B", "T", "interface"))); Assert.StartsWith(source, draft.Source); Assert.Equal(relationship, draft.Model.Relationships[0].Identity);
        Success(await draft.ExecuteAsync(new UpdateClass("B", "B", "Value", "abstract"))); Assert.Contains("class B~Value~", draft.Source); Assert.Contains("<<abstract>>", draft.Source);
        Success(await draft.ExecuteAsync(new UpdateClass("B", "B", null, null))); Assert.DoesNotContain("~Value~", draft.Source); Assert.DoesNotContain("<<abstract>>", draft.Source);
        Assert.Equal(relationship, draft.Model.Relationships[0].Identity); draft.Undo(); Assert.Equal("B<Value>", draft.Model.Class("B").Label);
    }
    [Theory, InlineData("unknown directive"), InlineData("direction sideways"), InlineData("click A call callback()"), InlineData("A -->> B"), InlineData("%%{init: {\"theme\":\"dark\"}}%%")]
    public async Task Ignored_global_statements_fail_closed_without_source_history_or_selection_changes(string unknown)
    {
        var source = Source + unknown + "\n"; var draft = new ClassDiagramVisualDraft(source); var selected = draft.Model.Class("Document").Identity; draft.Select(selected);
        Assert.True(draft.Model.HasUnknownDependencies); Assert.False((await draft.ExecuteAsync(new UpdateClass("Document", "Renamed", null, null))).Success);
        Assert.False((await draft.ExecuteAsync(new AddClass("NewClass"))).Success); Assert.Equal(source, draft.Source); Assert.Equal(1, draft.HistoryCount); Assert.Equal(selected, draft.Selection);
    }
    [Fact] public async Task Unknown_class_block_source_and_comments_prevent_ambiguous_member_reordering()
    {
        var source = "classDiagram\nclass A {\n+string first\n%% attached comment\n+string second\n}\n"; var draft = new ClassDiagramVisualDraft(source);
        Assert.False((await draft.ExecuteAsync(new MoveClassMember(draft.Model.Class("A").Attributes[0].Identity, 1))).Success); Assert.Equal(source, draft.Source);
        source = source.Replace("%% attached comment", "unknown { directive"); draft = new(source);
        Assert.True(draft.Model.HasUnknownDependencies); Assert.False((await draft.ExecuteAsync(new AddClassMember("A", new(ClassVisibility.None, "New")))).Success); Assert.Equal(source, draft.Source);
    }
    [Fact] public async Task Repeated_declarations_and_unsafe_member_tokens_are_protected()
    {
        var draft = new ClassDiagramVisualDraft("classDiagram\nclass A\nclass A\nA : +string Name\n"); Assert.False((await draft.ExecuteAsync(new UpdateClass("A", "B", null, null))).Success);
        draft = new(Source); var member = draft.Model.Class("Document").Methods[0];
        foreach (var value in new[] { member.Semantic with { Params = "path) injected()" }, member.Semantic with { Name = "bad name" }, member.Semantic with { Type = "bool\nclass Injected" }, member.Semantic with { IsStatic = true, IsAbstract = true } })
        { Assert.False((await draft.ExecuteAsync(new UpdateClassMember(member.Identity, value))).Success); Assert.Equal(Source, draft.Source); Assert.Equal(1, draft.HistoryCount); }
    }
    [Fact] public async Task Cancelled_and_superseded_presentation_validation_roll_back_exactly()
    {
        var entered = new TaskCompletionSource(); var release = new TaskCompletionSource();
        var draft = new ClassDiagramVisualDraft(Source, validatePresentation: async (_, _) => { entered.SetResult(); await release.Task; });
        var task = draft.ExecuteAsync(new AddClass("NewClass")); await entered.Task; draft.Undo(); release.SetResult(); Assert.False((await task).Success); Assert.Equal(Source, draft.Source); Assert.Equal(1, draft.HistoryCount);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); draft = new(Source); Assert.False((await draft.ExecuteAsync(new AddClass("NewClass"), cancellation.Token)).Success); Assert.Equal(Source, draft.Source);
    }
    [Theory, InlineData("+string Title$", false, true, false), InlineData("#string Title*", false, false, true), InlineData("+Open$(path) bool", true, true, false), InlineData("+Open*(path) bool", true, false, true)]
    public async Task Portable_static_attributes_are_editable_and_nonportable_pinned_marker_forms_remain_protected(string text, bool method, bool isStatic, bool isAbstract)
    {
        var source = "classDiagram\nclass A {\n" + text + "\n}\n"; var draft = new ClassDiagramVisualDraft(source); var member = draft.Model.Class("A").Members[0];
        Assert.Equal(method, member.Semantic.IsMethod); Assert.Equal(isStatic, member.Semantic.IsStatic); Assert.Equal(isAbstract, member.Semantic.IsAbstract);
        var result = await draft.ExecuteAsync(new UpdateClassMember(member.Identity, member.Semantic with { Visibility = ClassVisibility.Private }));
        if (!method && isStatic) { Success(result); Assert.Equal(source.Replace(text, "-" + text[1..]), draft.Source); }
        else { Assert.False(result.Success); Assert.True(member.Statement.Protected); Assert.Equal(source, draft.Source); Assert.Equal(1, draft.HistoryCount); }
    }
    [Theory, InlineData("+Run()$", "$"), InlineData("+Run() bool$", "bool$"), InlineData("+Run()*", "*"), InlineData("+Run() bool*", "bool*")]
    public async Task Documented_method_classifier_placement_exposes_the_pinned_type_bug_and_is_preserved(string text, string pinnedType)
    {
        var source = "classDiagram\nclass A {\n" + text + "\n}\n"; var draft = new ClassDiagramVisualDraft(source); var member = draft.Model.Class("A").Methods[0];
        Assert.Equal(pinnedType, member.Semantic.Type); Assert.True(member.Statement.Protected); ClassDiagramSemanticValidation.ValidateRendering(draft.Model, new DiagramService().Render(source));
        Assert.False((await draft.ExecuteAsync(new UpdateClassMember(member.Identity, member.Semantic with { Name = "Changed" }))).Success); Assert.Equal(source, draft.Source); Assert.Equal(1, draft.HistoryCount);
    }
    [Fact] public async Task Moving_byte_identical_members_preserves_the_selected_occurrence_and_private_history()
    {
        var source = "classDiagram\nclass A {\n+string Name\n+string Name\n}\n"; var draft = new ClassDiagramVisualDraft(source); var selected = draft.Model.Class("A").Attributes[1].Identity; draft.Select(selected);
        Success(await draft.ExecuteAsync(new MoveClassMember(selected, -1))); Assert.Equal(source, draft.Source); Assert.Equal(2, draft.HistoryCount); Assert.Equal(selected, draft.Model.Class("A").Attributes[0].Identity);
        Success(await draft.ExecuteAsync(new UpdateClassMember(selected, draft.Model.Member(selected).Semantic with { Name = "Moved" })));
        Assert.Equal("Moved", draft.Model.Class("A").Attributes[0].Semantic.Name); Assert.Equal("Name", draft.Model.Class("A").Attributes[1].Semantic.Name);
        draft.Undo(); draft.Undo(); Assert.Equal(selected, draft.Model.Class("A").Attributes[1].Identity); Assert.Equal(selected, draft.Selection); Assert.Equal(source, draft.Source);
        draft.Redo(); draft.Redo(); Assert.Equal("Moved", draft.Model.Class("A").Attributes[0].Semantic.Name);
    }
    [Fact] public async Task Unicode_mixed_line_endings_and_unrelated_tokens_remain_exact()
    {
        var source = "classDiagram\n    %% Café comment\r\n    class Café {\r\n        +string Titre\n        +Lire(chemin) bool\r\n    }\n    class Document\r\n    Café \"1\" *-- Document : possède\n";
        var draft = new ClassDiagramVisualDraft(source); var row = draft.Model.Class("Café").Attributes[0];
        Success(await draft.ExecuteAsync(new UpdateClassMember(row.Identity, row.Semantic with { Type = "int" })));
        Assert.Equal(source.Replace("+string Titre", "+int Titre", StringComparison.Ordinal), draft.Source); draft.Undo(); Assert.Equal(source, draft.Source);
    }
    [Theory, InlineData("A~T,U~"), InlineData("A[\"Display label\"]"), InlineData("A~T[]~")]
    public async Task Broad_pinned_class_ids_do_not_grant_safe_structural_authoring(string id)
    {
        var source = "classDiagram\nclass " + id + "\n"; var draft = new ClassDiagramVisualDraft(source);
        Assert.True(draft.Model.HasUnknownDependencies); Assert.False((await draft.ExecuteAsync(new AddClass("NewClass"))).Success); Assert.Equal(source, draft.Source); Assert.Equal(1, draft.HistoryCount);
    }
    [Fact] public async Task Unknown_local_member_source_allows_an_independent_class_member_edit_without_touching_protected_bytes()
    {
        var source = "classDiagram\nclass A {\nunknown { directive\n}\nclass B {\n+string Name\n}\n"; var draft = new ClassDiagramVisualDraft(source); var member = draft.Model.Class("B").Attributes[0];
        Success(await draft.ExecuteAsync(new UpdateClassMember(member.Identity, member.Semantic with { Name = "Title" }))); Assert.Equal(source.Replace("+string Name", "+string Title", StringComparison.Ordinal), draft.Source);
        Assert.Equal(ClassCompatibility.PartiallyEditable, draft.Model.Compatibility);
    }
    [Fact] public async Task Identical_relationship_duplicate_and_delete_retain_every_other_occurrence()
    {
        var draft = new ClassDiagramVisualDraft(Source); var original = draft.Model.Relationships.ToArray(); var selected = original[2];
        Success(await draft.ExecuteAsync(new DuplicateClassRelationship(selected.Identity))); Assert.Equal(5, draft.Model.Relationships.Count); Assert.NotEqual(selected.Identity, draft.Model.Relationships[^1].Identity);
        Success(await draft.ExecuteAsync(new DeleteClassRelationship(selected.Identity))); Assert.Equal(original[0].Identity, draft.Model.Relationships[0].Identity); Assert.DoesNotContain(draft.Model.Relationships, item => item.Identity == selected.Identity);
        draft.Undo(); Assert.Equal(selected.Identity, draft.Model.Relationships[2].Identity);
    }
    [Fact] public async Task History_is_bounded_and_branching_invalidates_redo()
    {
        var draft = new ClassDiagramVisualDraft("classDiagram\nclass A {\n+int Value\n}\n"); var member = draft.Model.Class("A").Attributes[0]; draft.Select(member.Identity);
        for (var i = 0; i < 105; i++) Success(await draft.ExecuteAsync(new UpdateClassMember(member.Identity, member.Semantic with { Name = "Value" + i })));
        Assert.Equal(100, draft.HistoryCount); draft.Undo(); Assert.Equal(member.Identity, draft.Selection); Assert.True(draft.CanRedo);
        Success(await draft.ExecuteAsync(new UpdateClassMember(member.Identity, member.Semantic with { Name = "Branch" }))); Assert.False(draft.CanRedo);
    }
}
