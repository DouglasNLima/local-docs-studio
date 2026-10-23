using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.TreemapDiagrams;

namespace LensDocsStudio.Core.Tests;

public class TreemapVisualDraftTests
{
    [Theory, InlineData("\n"), InlineData("\r\n")]
    public async Task Quoted_field_patches_keep_exact_decimal_lexemes_tabs_comments_and_Unicode(string nl)
    {
        var source = string.Join(nl, "%% exact Ω", "treemap-beta", "\"Parent\"", "\t\"Same\" : 030.500  ", " \"Same\": 20", " \"Zero\": 0", "");
        var draft = new TreemapVisualDraft(source); var key = draft.Model.Nodes[1].Identity; Assert.Equal(50.5, draft.Model.Nodes[0].ComputedValue); Assert.Null(draft.Model.Nodes[0].ExplicitValue);
        var result = await draft.UpdateAsync(key, "Edited café", "030.500"); Assert.True(result.Success, result.Error); Assert.Equal(source.Replace("\t\"Same\"", "\t\"Edited café\"", StringComparison.Ordinal), draft.Source);
        draft.Select(key); draft.Undo(); Assert.Equal(source, draft.Source); draft.Redo(); Assert.Equal(key, draft.Selection); Assert.True((await draft.ValidateAsync()).Success);
    }
    [Fact]
    public async Task Zero_value_nodes_remain_keyboard_editable_and_gain_only_actual_native_tiles()
    {
        var draft = new TreemapVisualDraft("treemap-beta\n\"Hidden\": 0\n\"Visible\": 2\n"); var key = draft.Model.Nodes[0].Identity;
        Assert.Single(TreemapVisualDraft.Correspondence(draft.Model, new DiagramService().Render(draft.Source).Svg!));
        var result = await draft.UpdateAsync(key, "Hidden", "1.2500"); Assert.True(result.Success, result.Error); Assert.Equal(1.25, draft.Model.Nodes[0].ComputedValue); Assert.Contains("1.2500", draft.Source);
        Assert.Equal(2, TreemapVisualDraft.Correspondence(draft.Model, new DiagramService().Render(draft.Source).Svg!).Count);
    }
    [Fact]
    public async Task Parent_totals_remain_derived_and_are_never_silently_written_to_source()
    {
        var draft = new TreemapVisualDraft("treemap-beta\n\"Group\"\n  \"A\": 1\n  \"B\": 2\n"); var result = await draft.UpdateAsync(draft.Model.Nodes[1].Identity, "A", "4.50"); Assert.True(result.Success, result.Error); Assert.Equal(6.5, draft.Model.Nodes[0].ComputedValue); Assert.Null(draft.Model.Nodes[0].ValueToken); Assert.Contains("\"Group\"\n", draft.Source);
        var current = draft.Source; result = await draft.UpdateAsync(draft.Model.Nodes[0].Identity, "Group", "6.5"); Assert.False(result.Success); Assert.Equal(current, draft.Source);
    }
    [Fact]
    public async Task Complete_sections_move_remove_and_reparent_without_changing_unintended_membership()
    {
        const string source = "treemap-beta\n%% first Ω\n\"First\"\n  \"A\": 1\n\"Second\"\n  \"B\": 2\n"; var draft = new TreemapVisualDraft(source); var first = draft.Model.Nodes[0].Identity; var a = draft.Model.Nodes[1].Identity; var second = draft.Model.Nodes[2].Identity;
        var result = await draft.MoveAsync(second, -1); Assert.True(result.Success, result.Error); Assert.Equal(second, draft.Model.Nodes[0].Identity); Assert.Contains("%% first Ω\n\"First\"\n  \"A\": 1", draft.Source);
        result = await draft.ReparentAsync(a, second); Assert.True(result.Success, result.Error); Assert.Equal(second, draft.Model.Hierarchy.Single(r => r.Identity == a).Parent); Assert.Equal(3, draft.Model.Nodes.Single(n => n.Identity == second).ComputedValue);
        result = await draft.DeleteAsync(first); Assert.True(result.Success, result.Error); Assert.DoesNotContain("first Ω", draft.Source); Assert.Contains(draft.Model.Nodes, n => n.Identity == a);
        result = await draft.AddAsync(null, "New section", null); Assert.True(result.Success, result.Error); var parent = draft.Model.Nodes[^1].Identity;
        result = await draft.AddAsync(parent, "New leaf", "000.25"); Assert.True(result.Success, result.Error); Assert.Equal(.25, draft.Model.Nodes.Single(n => n.Identity == parent).ComputedValue); Assert.Contains("000.25", draft.Source);
    }
    [Fact]
    public async Task Tab_space_membership_is_one_character_and_mixed_structural_reencoding_is_protected()
    {
        const string source = "treemap-beta\n\"Group\"\n\t\"A\": 1\n \"B\": 2\n"; var draft = new TreemapVisualDraft(source); Assert.Equal(draft.Model.Hierarchy[1].Parent, draft.Model.Hierarchy[2].Parent); Assert.Equal(3, draft.Model.Nodes[0].ComputedValue);
        var result = await draft.MoveAsync(draft.Model.Nodes[1].Identity, 1); Assert.False(result.Success); Assert.Contains("one character", result.Error); Assert.Equal(source, draft.Source);
    }
    [Fact]
    public async Task Raw_fallback_is_source_only_without_reconstructing_an_incomplete_grammar()
    {
        const string source = "treemap-beta\nraw fallback Ω\n \"A\": 1\n"; var draft = new TreemapVisualDraft(source); Assert.False(draft.Model.Nodes[0].Proved);
        var result = await draft.UpdateAsync(draft.Model.Nodes[0].Identity, "Changed", ""); Assert.False(result.Success); Assert.Equal(source, draft.Source);
        result = await draft.UpdateAsync(draft.Model.Nodes[1].Identity, "Edited", "1"); Assert.True(result.Success, result.Error); Assert.Contains("raw fallback Ω", draft.Source);
    }
    [Theory, InlineData("-1"), InlineData("NaN"), InlineData("Infinity"), InlineData("1e2"), InlineData("1."), InlineData(".5"), InlineData("0.1234567890123456"), InlineData("999999999999999999999999999999999999999999"), InlineData("")]
    public async Task Unsupported_new_numeric_forms_retain_last_valid_draft_and_history(string value)
    {
        const string source = "treemap-beta\n\"A\": 1\n"; var draft = new TreemapVisualDraft(source); var result = await draft.UpdateAsync(draft.Model.Nodes[0].Identity, "A", value); Assert.False(result.Success); Assert.Equal(source, draft.Source); Assert.False(draft.CanUndo);
    }
    [Fact]
    public async Task Duplicate_labels_have_distinct_source_identities_and_no_guessed_pointer_tiles()
    {
        var draft = new TreemapVisualDraft("treemap-beta\n\"Same\": 1\n\"Same\": 2\n"); Assert.Empty(TreemapVisualDraft.Correspondence(draft.Model, new DiagramService().Render(draft.Source).Svg!));
        var key = draft.Model.Nodes[1].Identity; var result = await draft.UpdateAsync(key, "Second", "2"); Assert.True(result.Success, result.Error); Assert.Equal("Same", draft.Model.Nodes[0].Label); Assert.Equal(key, draft.Model.Nodes[1].Identity);
    }
}
