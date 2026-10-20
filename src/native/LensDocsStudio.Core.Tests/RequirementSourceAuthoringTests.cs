using LensDocsStudio.Core.RequirementDiagrams;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;

namespace LensDocsStudio.Core.Tests;

public class RequirementSourceAuthoringTests
{
    private static void Pass(SourceAuthoringResult r) => Assert.True(r.Success, r.Error);
    [Theory, InlineData("\n"), InlineData("\r\n")]
    public async Task Display_ID_edits_preserve_symbolic_references_and_every_untouched_lexeme(string nl)
    {
        var source = string.Join(nl, "---", "title: Config", "---", "%% café Ω", "requirementDiagram", "requirement \"guide\" {", "  id : old", "  ID: \"DOC-17\"", "  text: \"Keep Ω\"", "  unknown: untouched", "}", "element studio {", "  type: app", "}", "\"guide\" <- satisfies - studio", "");
        var d = new RequirementVisualDraft(source); Pass(await d.ValidateAsync()); var b = d.Model.Blocks[0]; d.Select(b.Identity);
        Pass(await d.UpdateBlockAsync(b.Identity, b.Kind, new Dictionary<string, string> { ["id"] = "DOC-18" })); Assert.Equal(source.Replace("\"DOC-17\"", "\"DOC-18\"", StringComparison.Ordinal), d.Source); Assert.Equal("guide", d.Model.Relations[0].Target); d.Undo(); Assert.Equal(source, d.Source); d.Redo(); Assert.Equal(b.Identity, d.Selection);
    }
    [Fact] public async Task Atomic_symbolic_rename_updates_both_arrow_forms_without_touching_properties_or_comments()
    {
        const string source = "requirementDiagram\n%% a is a symbolic name\nrequirement a {\nid: a\ntext: a\n}\nelement b {\n}\nelement c {\n}\nb - satisfies -> a\na <- verifies - c\n";
        var d = new RequirementVisualDraft(source); var b = d.Model.Blocks[0]; Pass(await d.RenameAsync(b.Identity, "renamed Ω")); Assert.Contains("requirement renamed Ω {", d.Source); Assert.Contains("b - satisfies -> renamed Ω", d.Source); Assert.Contains("renamed Ω <- verifies - c", d.Source); Assert.Contains("id: a\ntext: a", d.Source); Assert.Contains("%% a is a symbolic name", d.Source); d.Undo(); Assert.Equal(source, d.Source);
    }
    [Fact] public async Task Empty_final_repeated_property_does_not_reveal_old_value()
    {
        var d = new RequirementVisualDraft("requirementDiagram\nrequirement a {\nid: old\nID: \"effective\"\n}\n"); var b = d.Model.Blocks[0]; Pass(await d.UpdateBlockAsync(b.Identity, b.Kind, new Dictionary<string, string> { ["id"] = "" })); Assert.Contains("id: old\nID: \"\"", d.Source); Assert.Equal("", d.Model.Blocks[0].Value("id")); Pass(await d.ValidateAsync());
    }
    [Theory, InlineData("requirement"), InlineData("functionalRequirement"), InlineData("interfaceRequirement"), InlineData("performanceRequirement"), InlineData("physicalRequirement"), InlineData("designConstraint")]
    public async Task All_six_kinds_patch_only_the_selected_kind_token(string kind)
    {
        var d = new RequirementVisualDraft("requirementDiagram\n  requirement a {\n    id: \"1\"\n  }\n"); Pass(await d.UpdateBlockAsync(d.Model.Blocks[0].Identity, kind, new Dictionary<string, string>())); Assert.Equal("requirementDiagram\n  " + kind + " a {\n    id: \"1\"\n  }\n", d.Source);
    }
    [Theory, InlineData("contains"), InlineData("copies"), InlineData("derives"), InlineData("satisfies"), InlineData("verifies"), InlineData("refines"), InlineData("traces")]
    public async Task Seven_relation_types_preserve_reverse_spelling_and_prove_actual_native_endpoints(string type)
    {
        var d = new RequirementVisualDraft("requirementDiagram\ndirection LR\nrequirement a {\n}\nelement b {\n}\nb <- traces - a\n"); var r = d.Model.Relations[0]; Pass(await d.UpdateRelationAsync(r.Identity, "a", "b", type)); Assert.Contains("b <- " + type + " - a", d.Source); Assert.Equal("a", d.Model.Relations[0].Source);
        var rendered = new DiagramService().Render(d.Source); Assert.Single(RequirementVisualDraft.RelationCorrespondence(d.Model, rendered.Svg!));
    }
    [Theory, InlineData("TB"), InlineData("BT"), InlineData("LR"), InlineData("RL")]
    public async Task Direction_uses_actual_native_endpoint_correspondence(string direction)
    {
        var d = new RequirementVisualDraft(RequirementVisualDraft.Starter); Pass(await d.SetHeadingAsync("Documentation traceability", direction)); var render = new DiagramService().Render(d.Source); Assert.Single(RequirementVisualDraft.RelationCorrespondence(d.Model, render.Svg!));
    }
    [Theory, InlineData("requirement a {\nid: first\n}\nrequirement a {\nid: hidden\n}\n"), InlineData("requirement a {\nid: before\nrequirement nested {\nid: hidden\n}\n"), InlineData("requirement a {\nid: unclosed\n")]
    public async Task Duplicate_hidden_nested_and_unclosed_blocks_remain_preserved_and_protected(string body)
    {
        var d = new RequirementVisualDraft("requirementDiagram\n" + body); Pass(await d.ValidateAsync()); var b = d.Model.Blocks[0]; Assert.False((await d.RenameAsync(b.Identity, "unsafe")).Success); Assert.False((await d.DeleteAsync(b.Identity)).Success); Assert.Equal(d.OriginalSource, d.Source);
    }
    [Fact] public async Task Dangling_parallel_self_and_cyclic_topology_is_protected_while_independent_fields_remain_editable()
    {
        foreach (var relations in new[] { "a - traces -> missing\n", "a - traces -> b\na - copies -> b\n", "a - traces -> a\n", "a - traces -> b\nb - copies -> a\n" })
        {
            var d = new RequirementVisualDraft("requirementDiagram\nrequirement a {\nid: old\n}\nelement b {\n}\n" + relations); Pass(await d.ValidateAsync()); Assert.False(d.Model.TopologySafe); Assert.False((await d.RenameAsync(d.Model.Blocks[0].Identity, "new")).Success);
            Pass(await d.UpdateBlockAsync(d.Model.Blocks[0].Identity, "requirement", new Dictionary<string, string> { ["id"] = "new" })); Assert.Contains(relations, d.Source);
        }
    }
    [Fact] public async Task Connected_deletion_requires_explicit_relation_removal_and_new_references_cannot_dangle_or_cycle()
    {
        var d = new RequirementVisualDraft(RequirementVisualDraft.Starter); Assert.False((await d.DeleteAsync(d.Model.Blocks[0].Identity)).Success); Assert.False((await d.AddRelationAsync("guide", "missing", "traces")).Success); Assert.False((await d.AddRelationAsync("guide", "studio", "traces")).Success);
        Pass(await d.DeleteAsync(d.Model.Relations[0].Identity)); Pass(await d.DeleteAsync(d.Model.Blocks[0].Identity)); Assert.Single(d.Model.Blocks); Assert.Empty(d.Model.Relations);
    }
    [Fact] public async Task Invalid_combined_rename_and_property_input_is_atomic_and_retains_last_valid_draft()
    {
        var d = new RequirementVisualDraft(RequirementVisualDraft.Starter); var b = d.Model.Blocks[0]; Assert.False((await d.UpdateBlockAsync(b.Identity, b.Kind, new Dictionary<string, string> { ["id"] = "new", ["risk"] = "unsupported" }, symbolicName: "renamed")).Success); Assert.Equal(d.OriginalSource, d.Source); Assert.False(d.CanUndo);
        Assert.False((await d.RenameAsync(b.Identity, "studio")).Success); Assert.False((await d.RenameAsync(b.Identity, "unproved\"quote")).Success);
    }
    [Fact] public async Task Complete_block_reorder_carries_same_indent_comments_and_retains_occurrence_keys()
    {
        const string source = "requirementDiagram\n%% attached\nrequirement a {\nid: 1\n}\nelement b {\ntype: app\n}\n"; var d = new RequirementVisualDraft(source); var b = d.Model.Blocks[0]; d.Select(b.Identity); Pass(await d.MoveAsync(b.Identity, 1)); Assert.EndsWith("%% attached\nrequirement a {\nid: 1\n}\n", d.Source); Assert.Equal(b.Identity, d.Model.Blocks[1].Identity); d.Undo(); Assert.Equal(source, d.Source); Assert.Equal(b.Identity, d.Selection);
    }
}
