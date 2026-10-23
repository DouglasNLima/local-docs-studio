using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.VennDiagrams;

namespace LensDocsStudio.Core.Tests;

public class VennVisualDraftTests
{
    [Theory, InlineData("\n"), InlineData("\r\n")]
    public async Task Atomic_unique_ID_rename_preserves_all_reference_quotes_comments_labels_sizes_and_neighbours(string nl)
    {
        var source = string.Join(nl, "%% retained Ω", "venn-beta", "set \"A\"[\"Authors Ω\"]: 010.50", "set B", "union A,  \"B\"[\"Shared\"]: 002.00", "union \"A\", B", "");
        var draft = new VennVisualDraft(source); var key = draft.Model.Sets[0].Identity; draft.Select(key);
        var result = await draft.UpdateSetAsync(key, "New_A", "Authors Ω", "010.50"); Assert.True(result.Success, result.Error);
        Assert.Equal(source.Replace("set \"A\"", "set \"New_A\"", StringComparison.Ordinal).Replace("union A,", "union New_A,", StringComparison.Ordinal).Replace("union \"A\",", "union \"New_A\",", StringComparison.Ordinal), draft.Source); Assert.Equal(key, draft.Selection);
        draft.Undo(); Assert.Equal(source, draft.Source); draft.Redo(); Assert.Equal(2, draft.Model.Unions.SelectMany(u => u.References).Count(r => r.Value == "New_A"));
    }
    [Fact]
    public async Task Absent_label_rename_requires_explicit_display_semantics_and_preserves_last_valid_draft()
    {
        const string source = "venn-beta\nset A\nset B\nunion A,B\n"; var draft = new VennVisualDraft(source); var key = draft.Model.Sets[0].Identity;
        var result = await draft.UpdateSetAsync(key, "New", "A", ""); Assert.False(result.Success); Assert.Contains("absent label", result.Error); Assert.Equal(source, draft.Source);
        result = await draft.UpdateSetAsync(key, "New", "New", "", renameDefaultLabel: true); Assert.True(result.Success, result.Error); Assert.Null(draft.Model.Sets[0].LabelToken); Assert.Equal("New", draft.Model.Sets[0].Label);
        draft.Undo(); result = await draft.MakeLabelExplicitAsync(key); Assert.True(result.Success, result.Error); Assert.Equal("A", draft.Model.Sets[0].LabelToken?.Value);
        result = await draft.UpdateSetAsync(key, "New", "A", ""); Assert.True(result.Success, result.Error); Assert.Equal("A", draft.Model.Sets[0].Label); Assert.Contains("set New[\"A\"]", draft.Source);
    }
    [Fact]
    public async Task Size_metadata_round_trips_without_changing_any_native_circle_geometry()
    {
        var draft = new VennVisualDraft("venn-beta\nset A[\"A\"]: 1\nset B[\"B\"]: 999\nunion A,B[\"Both\"]: 2\n"); var original = new DiagramService().Render(draft.Source).Svg;
        var result = await draft.UpdateSetAsync(draft.Model.Sets[0].Identity, "A", "A", "000.25"); Assert.True(result.Success, result.Error); Assert.Contains("000.25", draft.Source); Assert.Equal(original, new DiagramService().Render(draft.Source).Svg);
        result = await draft.UpdateUnionAsync(draft.Model.Unions[0].Identity, draft.Model.Unions[0].MembersToken.Raw, "Both", "55.500"); Assert.True(result.Success, result.Error); Assert.Equal(original, new DiagramService().Render(draft.Source).Svg);
    }
    [Fact]
    public async Task Connected_removal_requires_explicit_proved_atomic_union_removal_and_is_one_private_undo()
    {
        const string source = "venn-beta\n%% set Ω\nset A\nset B\n%% union Ω\nunion A,B[\"Both\"]\n"; var draft = new VennVisualDraft(source); var key = draft.Model.Sets[0].Identity;
        var result = await draft.DeleteAsync(key); Assert.False(result.Success); Assert.Equal(source, draft.Source); Assert.False(draft.CanUndo);
        result = await draft.DeleteAsync(key, removeConnected: true); Assert.True(result.Success, result.Error); Assert.Empty(draft.Model.Unions); Assert.Equal("B", draft.Model.Sets.Single().Id); Assert.DoesNotContain("union Ω", draft.Source); Assert.DoesNotContain("set Ω", draft.Source);
        draft.Undo(); Assert.Equal(source, draft.Source); Assert.False(draft.CanUndo);
    }
    [Fact]
    public async Task Simple_sets_unions_and_order_changes_use_exact_occurrences()
    {
        var draft = new VennVisualDraft("venn-beta\nset A[\"Alpha\"]\nset B\n"); var a = draft.Model.Sets[0].Identity; var b = draft.Model.Sets[1].Identity;
        var result = await draft.MoveAsync(b, -1); Assert.True(result.Success, result.Error); Assert.Equal(new[] { b, a }, draft.Model.Sets.Select(s => s.Identity));
        result = await draft.AddSetAsync("C", "Gamma Ω"); Assert.True(result.Success, result.Error); result = await draft.AddUnionAsync("A, C", "Both Ω"); Assert.True(result.Success, result.Error);
        var union = draft.Model.Unions.Single(); result = await draft.UpdateUnionAsync(union.Identity, "B, C", "Changed", ""); Assert.True(result.Success, result.Error); Assert.Equal(new[] { "B", "C" }, draft.Model.Unions.Single().References.Select(r => r.Value));
    }
    [Theory, InlineData("set A\nset A\nunion A,A"), InlineData("set A\nset B\nunion A, missing"), InlineData("set A\nset B\nunion A,A"), InlineData("set \"A,B\"\nset B\nunion \"A,B\",B"), InlineData("set A\nset B\nunknown Ω")]
    public async Task Unknown_duplicate_missing_or_ambiguous_ID_grammar_protects_atomic_operations(string body)
    {
        var source = "venn-beta\n" + body + "\n"; var draft = new VennVisualDraft(source); Assert.False(draft.Model.StructureSafe);
        var result = await draft.AddSetAsync("C"); Assert.False(result.Success); Assert.Equal(source, draft.Source); Assert.False(draft.CanUndo);
    }
    [Theory, InlineData("-1"), InlineData("NaN"), InlineData("1e2"), InlineData("1."), InlineData("99999999999999999999999999999")]
    public async Task Invalid_pending_metadata_never_changes_native_source(string size)
    {
        const string source = "venn-beta\nset A[\"Alpha\"]: 1\n"; var draft = new VennVisualDraft(source); var result = await draft.UpdateSetAsync(draft.Model.Sets[0].Identity, "A", "Alpha", size); Assert.False(result.Success); Assert.Equal(source, draft.Source);
    }
    [Fact]
    public async Task Unlabelled_union_remains_a_keyboard_occurrence_without_invented_intersection_geometry()
    {
        var draft = new VennVisualDraft("venn-beta\nset A\nset B\nunion A,B\n"); Assert.True((await draft.ValidateAsync()).Success); Assert.Single(draft.Model.Unions); Assert.Null(draft.Model.Unions[0].LabelToken);
        Assert.Equal(2, VennVisualDraft.Correspondence(draft.Model, new DiagramService().Render(draft.Source).Svg!).Count);
    }
}
