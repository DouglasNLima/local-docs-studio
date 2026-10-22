using LensDocsStudio.Core.C4Diagrams;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;

namespace LensDocsStudio.Core.Tests;

public class C4SourceAuthoringTests
{
    private static void Pass(SourceAuthoringResult result) => Assert.True(result.Success, result.Error);
    [Theory, InlineData("C4Context"), InlineData("C4Container"), InlineData("C4Component")]
    public async Task Each_C4_variant_keeps_its_level_and_exact_positional_slots(string variant)
    {
        var draft = new C4VisualDraft(variant + "\nPerson(p,\"Person\",\"Person description\")\nSystem(s,\"System\",\"System description\")\nContainer(c,\"Container\",\"Technology\",\"Details\")\nComponent(k,\"Component\",\"Library\",\"Description\")\n");
        Assert.Equal("Person description", draft.Model.Nodes[0].Description); Assert.Equal("", draft.Model.Nodes[0].Technology); Assert.Equal("System description", draft.Model.Nodes[1].Description);
        Assert.Equal("Technology", draft.Model.Nodes[2].Technology); Assert.Equal("Details", draft.Model.Nodes[2].Description);
        var node = draft.Model.Nodes[2]; Pass(await draft.UpdateNodeAsync(node.Identity, node.Alias, "Container Ω", "", "New description"));
        Assert.Contains("Container(c,\"Container Ω\",\"\",\"New description\")", draft.Source); Assert.StartsWith(variant + "\n", draft.Source); Pass(await draft.ValidateAsync());
    }
    [Theory, InlineData("\n"), InlineData("\r\n")]
    public async Task Rename_preserves_Back_keyword_argument_order_comments_quotes_and_untouched_neighbours(string newline)
    {
        var source = ("%% unchanged Ω\n" + C4VisualDraft.Starter).Replace("\n", newline, StringComparison.Ordinal); var draft = new C4VisualDraft(source); var node = draft.Model.Nodes.Single(n => n.Alias == "docs");
        Pass(await draft.UpdateNodeAsync(node.Identity, "guide", node.Label, node.Technology, node.Description));
        Assert.Equal(source.Replace("System(docs,", "System(guide,", StringComparison.Ordinal).Replace("Rel(reader, docs,", "Rel(reader, guide,", StringComparison.Ordinal).Replace("Rel_Back(archive, docs,", "Rel_Back(archive, guide,", StringComparison.Ordinal), draft.Source);
        Assert.Equal("guide", draft.Model.Relations[1].Source); Assert.Equal("archive", draft.Model.Relations[1].Target); draft.Undo(); Assert.Equal(source, draft.Source); draft.Redo(); Assert.Equal("guide", draft.Model.Nodes.Single(n => n.Identity == node.Identity).Alias);
    }
    [Fact]
    public async Task Named_empty_and_extra_arguments_remain_exact_during_proved_independent_field_edits()
    {
        const string source = "C4Container\n Container(a, \"A\", , $tags=\"kept\", \"\", \"Description\", \"extra\")  \nUpdateElementStyle(a, $bgColor=\"orange\")\n";
        var draft = new C4VisualDraft(source); var node = draft.Model.Nodes[0]; Assert.False(draft.Model.TopologySafe);
        Pass(await draft.UpdateNodeAsync(node.Identity, "a", "A, Ω", "", "")); Assert.Equal(source.Replace("\"A\"", "\"A, Ω\"", StringComparison.Ordinal).Replace("\"Description\"", "\"\"", StringComparison.Ordinal), draft.Source);
        var before = draft.Source; Assert.False((await draft.UpdateNodeAsync(node.Identity, "renamed", "A, Ω", "", "")).Success); Assert.Equal(before, draft.Source);
    }
    [Fact]
    public async Task Absent_slots_are_added_as_explicit_empty_arguments_without_shifting_following_values()
    {
        var draft = new C4VisualDraft("C4Component\nComponent(a, \"A\")\n"); var node = draft.Model.Nodes[0];
        Pass(await draft.UpdateNodeAsync(node.Identity, "a", "A", "", "Details")); Assert.Equal("C4Component\nComponent(a, \"A\", \"\", \"Details\")\n", draft.Source);
        Pass(await draft.UpdateNodeAsync(node.Identity, "a", "A", "", "")); Assert.Contains("\"\", \"\"", draft.Source);
    }
    [Fact]
    public async Task Complete_boundary_moves_preserve_brace_form_comment_groups_line_keys_and_children()
    {
        const string source = "C4Context\n%% group Ω\nBoundary(g,\"Group\")\n{\n  %% attached A\n  System(a,\"A\")\n}\nPerson(b,\"B\")\n";
        var draft = new C4VisualDraft(source); var group = draft.Model.Nodes[0]; var child = draft.Model.Nodes[1];
        Pass(await draft.MoveAsync(group.Identity, 1)); Assert.Equal("C4Context\nPerson(b,\"B\")\n%% group Ω\nBoundary(g,\"Group\")\n{\n  %% attached A\n  System(a,\"A\")\n}\n", draft.Source); Assert.Equal(group.Identity, draft.Model.Nodes[1].Identity); Assert.Equal(group.Identity, draft.Model.Nodes.Single(n => n.Identity == child.Identity).ParentIdentity);
        Pass(await draft.MoveMembershipAsync(child.Identity, "")); Assert.EndsWith("  %% attached A\n  System(a,\"A\")\n", draft.Source); Assert.Null(draft.Model.Nodes.Single(n => n.Identity == child.Identity).ParentIdentity);
        Pass(await draft.MoveMembershipAsync(child.Identity, group.Identity)); Assert.Contains("{\n  %% attached A\n  System(a,\"A\")\n}\n", draft.Source);
    }
    [Theory, InlineData("C4Dynamic"), InlineData("C4Deployment")]
    public async Task Advanced_C4_variants_retain_source_and_cannot_be_relabelled_supported(string variant)
    { var draft = new C4VisualDraft(variant + "\nComponent(a,\"A\")\n"); Assert.False((await draft.ValidateAsync()).Success); Assert.Equal(draft.OriginalSource, draft.Source); Assert.NotNull(draft.ValidationError); }
    [Theory, InlineData("Boundary(g,\"Open\") {\nPerson(a,\"A\")\n"), InlineData("Boundary(g,\"Outer\") {\nBoundary(inner,\"Inner\") {\nPerson(a,\"A\")\n}\n}\n"), InlineData("Person(a,\"A\")\nPerson(a,\"Hidden\")\n"), InlineData("Person(a,\"A\")\nRel(a,missing)\n"), InlineData("Deployment_Node(n,\"Node\") {\n}\n")]
    public async Task Incomplete_nested_duplicate_missing_and_deployment_source_protects_structural_operations(string body)
    { var draft = new C4VisualDraft("C4Context\n" + body); Assert.False(draft.Model.TopologySafe); Assert.False((await draft.AddNodeAsync("Person", "newId", "New")).Success); Assert.Equal(draft.OriginalSource, draft.Source); }
    [Theory, InlineData("Rel_D(a,b,\"Label\")"), InlineData("RelIndex(1,a,b,\"Label\")"), InlineData("Rel(a,b)\nRel(a,b)"), InlineData("Rel(a,a)"), InlineData("Rel(a,b)\nRel(b,a)")]
    public async Task Direction_hints_indices_and_unproved_parallel_self_or_cyclic_topology_are_preserved(string relations)
    { var draft = new C4VisualDraft("C4Context\nPerson(a,\"A\")\nSystem(b,\"B\")\n" + relations + "\n"); Assert.False(draft.Model.TopologySafe); Assert.False((await draft.DeleteAsync(draft.Model.Nodes[0].Identity)).Success); Assert.Equal(draft.OriginalSource, draft.Source); }
    [Fact]
    public async Task New_BiRel_and_Back_relationships_use_effective_endpoints_and_connected_deletion_is_rejected()
    {
        var draft = new C4VisualDraft("C4Container\nPerson(a,\"A\")\nContainer(b,\"B\",\"WPF\",\"Details\")\n"); Pass(await draft.AddRelationAsync("Rel_Back", "a", "b", "Uses", "Local")); Assert.Contains("Rel_Back(b, a, \"Uses\", \"Local\")", draft.Source);
        var before = draft.Source; Assert.False((await draft.DeleteAsync(draft.Model.Nodes[0].Identity)).Success); Assert.Equal(before, draft.Source);
        Pass(await draft.DeleteAsync(draft.Model.Relations[0].Identity)); Pass(await draft.AddRelationAsync("BiRel", "a", "b")); Assert.Equal("BiRel", draft.Model.Relations[0].Call.Keyword.Value);
    }
    [Fact]
    public async Task Proved_last_wins_C4_fields_remain_editable_without_revealing_an_older_alias()
    {
        var draft = new C4VisualDraft("C4Context\nPerson(a,\"Hidden\",\"Old\")\nSystem(a,\"Effective\",\"Details\")\n"); var effective = draft.Model.Nodes[1];
        Pass(await draft.UpdateNodeAsync(effective.Identity, "a", "Edited Ω", "", "")); Assert.Equal("C4Context\nPerson(a,\"Hidden\",\"Old\")\nSystem(a,\"Edited Ω\",\"\")\n", draft.Source);
        Assert.False((await draft.UpdateNodeAsync(draft.Model.Nodes[0].Identity, "a", "Unexpected", "", "Old")).Success); Assert.False((await draft.DeleteAsync(effective.Identity)).Success);
    }
    [Fact]
    public async Task Effective_title_edit_keeps_earlier_header_and_body_declarations_exact()
    { const string source = "C4Context title \"Earlier\"\ntitle \"Older\"\ntitle \"Effective\"\nPerson(a,\"A\")\n"; var draft = new C4VisualDraft(source); Pass(await draft.SetTitleAsync("New Ω")); Assert.Equal(source.Replace("\"Effective\"", "\"New Ω\"", StringComparison.Ordinal), draft.Source); }
    [Fact]
    public async Task C4_ordering_preserves_proved_relationship_rows_and_protected_index_inspection_uses_actual_slots()
    {
        var draft = new C4VisualDraft(C4VisualDraft.Starter); Pass(await draft.AddNodeAsync("System", "added", "Added")); var added = draft.Source; var key = draft.Model.Nodes[^1].Identity;
        var relations = draft.Model.Relations.Select(r => draft.Text.Line(r.Identity).Text).ToArray(); Pass(await draft.MoveAsync(key, -1)); Assert.Equal(relations, draft.Model.Relations.Select(r => draft.Text.Line(r.Identity).Text)); draft.Undo(); Assert.Equal(added, draft.Source); draft.Redo(); Pass(await draft.DeleteAsync(key)); Assert.Equal(C4VisualDraft.Starter, draft.Source);
        var indexed = new C4VisualDraft("C4Context\nPerson(a,\"A\")\nSystem(b,\"B\")\nRelIndex(9,a,b,\"Label\",\"Protocol\")\n"); Assert.Equal("a", indexed.Model.Relations[0].Source); Assert.Equal("b", indexed.Model.Relations[0].Target); Assert.Equal("Label", indexed.Model.Relations[0].Label); Assert.False(indexed.Model.TopologySafe);
    }
    [Theory, InlineData("A, B Ω"), InlineData("A \"quoted\" value")]
    public async Task New_C4_escaping_is_proved_against_actual_public_native_renderer_text(string label)
    {
        var draft = new C4VisualDraft("C4Container\nContainer(a,\"A\",\"WPF\",\"Details\")\n"); Pass(await draft.UpdateNodeAsync(draft.Model.Nodes[0].Identity, "a", label, "WPF", "Details"));
        var rendered = new DiagramService().Render(draft.Source); var shape = Assert.Single(SourceDeclarations.Groups(rendered.Svg!, "c4-element")); Assert.Contains(SourceDeclarations.TextValue(label), SourceDeclarations.VisibleText(shape));
    }
    [Theory, InlineData("C4Context\n", "Person(p{0},\"P\")\n"), InlineData("block\n", "p{0}[P]\n")]
    public void Existing_500_element_resource_budget_fails_closed_before_new_native_layout_paths(string header, string declaration)
    { var source = header + string.Concat(Enumerable.Range(0, 501).Select(i => string.Format(System.Globalization.CultureInfo.InvariantCulture, declaration, i))); var rendered = new DiagramService().Render(source); Assert.False(rendered.Success); Assert.Equal(source, rendered.Source); Assert.Contains("500-element", rendered.Error); }
    [Fact]
    public void Existing_64_level_recursion_resource_budget_is_enforced_before_C4_tree_layout()
    { var source = "C4Context\n" + string.Concat(Enumerable.Range(0, 65).Select(i => "Boundary(b" + i + ",\"B\") {\n")) + string.Concat(Enumerable.Repeat("}\n", 65)); var rendered = new DiagramService().Render(source); Assert.False(rendered.Success); Assert.Equal(source, rendered.Source); Assert.Contains("64-level", rendered.Error); }
}
