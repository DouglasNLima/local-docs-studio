using LensDocsStudio.Core.PacketDiagrams;

namespace LensDocsStudio.Core.Tests;

public class PacketVisualDraftTests
{
    [Theory, InlineData("\n"), InlineData("\r\n")]
    public async Task Exact_label_token_patch_retains_unicode_spacing_numeric_lexemes_comments_and_private_history(string nl)
    {
        var source = string.Join(nl, "%% retained Ω", "packet title Earlier", "title Effective", "\t000-015 :  \"Source Ω\"  ", "%% next field", "+020: \"Destination\"", "36: \"Flag\"", "");
        var draft = new PacketVisualDraft(source); var key = draft.Model.Fields[0].Identity; draft.Select(key);
        var result = await draft.UpdateFieldAsync(key, "Edited café", "000", "015"); Assert.True(result.Success, result.Error);
        Assert.Equal(source.Replace("Source Ω", "Edited café", StringComparison.Ordinal), draft.Source); Assert.Equal(key, draft.Selection);
        draft.Undo(); Assert.Equal(source, draft.Source); Assert.Equal(key, draft.Selection); draft.Redo(); Assert.Contains("Edited café", draft.Source);
        Assert.Equal((16, 35), (draft.Model.Fields[1].Start, draft.Model.Fields[1].End));
    }
    [Fact]
    public async Task Relative_cursor_change_is_rejected_atomically_and_last_valid_draft_survives()
    {
        const string source = "packet\n0-7: \"A\"\n+8: \"B\"\n"; var draft = new PacketVisualDraft(source);
        var result = await draft.UpdateFieldAsync(draft.Model.Fields[0].Identity, "Pending", "0", "8"); Assert.False(result.Success); Assert.Contains("relative", result.Error); Assert.Equal(source, draft.Source); Assert.False(draft.CanUndo);
        result = await draft.DeleteAsync(draft.Model.Fields[0].Identity); Assert.False(result.Success); Assert.Equal(source, draft.Source);
        result = await draft.MoveAsync(draft.Model.Fields[1].Identity, -1); Assert.False(result.Success); Assert.Equal(source, draft.Source);
    }
    [Fact]
    public async Task Relative_count_can_change_when_every_other_effective_range_is_unchanged()
    {
        var draft = new PacketVisualDraft("packet\n0-7: \"A\"\n+008: \"B\"\n32: \"C\"\n"); var key = draft.Model.Fields[1].Identity;
        var result = await draft.UpdateFieldAsync(key, "B", "010"); Assert.True(result.Success, result.Error); Assert.Contains("+010:", draft.Source); Assert.Equal((8, 17), (draft.Model.Fields[1].Start, draft.Model.Fields[1].End)); Assert.Equal(32, draft.Model.Fields[2].Start);
    }
    [Fact]
    public async Task Safe_complete_field_moves_keep_attached_comments_and_exact_occurrence_identity()
    {
        const string source = "packet\n%% first Ω\n0-7: \"Same\"\n\n%% second Ω\n16-23: \"Same\"\n"; var draft = new PacketVisualDraft(source); var first = draft.Model.Fields[0].Identity; var second = draft.Model.Fields[1].Identity;
        var result = await draft.MoveAsync(second, -1); Assert.True(result.Success, result.Error); Assert.Equal(new[] { second, first }, draft.Model.Fields.Select(f => f.Identity)); Assert.StartsWith("packet\n%% second Ω\n16-23", draft.Source); Assert.Contains("%% first Ω\n0-7", draft.Source);
        draft.Undo(); Assert.Equal(source, draft.Source); result = await draft.DeleteAsync(first); Assert.True(result.Success, result.Error); Assert.DoesNotContain("first Ω", draft.Source); Assert.Equal(second, draft.Model.Fields.Single().Identity);
    }
    [Theory, InlineData("+0: \"Bad\""), InlineData("4096: \"Bad\""), InlineData("9-8: \"Bad\""), InlineData("+999999999999999999999: \"Bad\"")]
    public async Task Skipped_fields_remain_inspectable_and_protect_structure_without_cursor_movement(string skipped)
    {
        var draft = new PacketVisualDraft("packet\n0-7: \"A\"\n" + skipped + "\n+8: \"B\"\n"); Assert.False(draft.Model.Fields[1].Accepted); Assert.Equal(8, draft.Model.Fields[2].Start);
        var result = await draft.UpdateFieldAsync(draft.Model.Fields[0].Identity, "Edited", "0", "7"); Assert.True(result.Success, result.Error); Assert.Contains(skipped, draft.Source);
        var current = draft.Source; result = await draft.DeleteAsync(draft.Model.Fields[1].Identity); Assert.False(result.Success); Assert.Equal(current, draft.Source);
    }
    [Fact]
    public async Task Duplicate_ranges_are_keyboard_occurrences_without_guessed_pointer_identity()
    {
        var draft = new PacketVisualDraft("packet\n0-7: \"Same\"\n0-7: \"Same\"\n"); Assert.Equal(2, draft.Model.Fields.Select(f => f.Identity).Distinct().Count()); Assert.All(draft.Model.Fields, f => Assert.False(draft.Model.Unambiguous(f)));
        var result = await draft.UpdateFieldAsync(draft.Model.Fields[1].Identity, "Second", "0", "7"); Assert.True(result.Success, result.Error); Assert.Equal("Same", draft.Model.Fields[0].Label); Assert.Equal("Second", draft.Model.Fields[1].Label);
    }
    [Theory, InlineData("2147483648"), InlineData("-1"), InlineData("1.0"), InlineData("1e2"), InlineData("+1"), InlineData(" 1"), InlineData("")]
    public async Task Invalid_pending_integer_never_changes_private_source_or_history(string value)
    {
        const string source = "packet\n0: \"A\"\n"; var draft = new PacketVisualDraft(source); var result = await draft.UpdateFieldAsync(draft.Model.Fields[0].Identity, "A", value);
        Assert.False(result.Success); Assert.Equal(source, draft.Source); Assert.False(draft.CanUndo);
    }
    [Fact]
    public async Task Multirow_native_segments_are_verified_without_guessing_grid_coordinates()
    {
        var draft = new PacketVisualDraft("packet-beta\n0-30: \"A\"\n+4: \"Cross Ω\"\n"); Assert.True((await draft.ValidateAsync()).Success); var segments = PacketVisualDraft.Segments(draft.Model).ToArray();
        Assert.Equal(new[] { (0, 30), (31, 31), (32, 34) }, segments.Select(s => (s.Start, s.End))); Assert.Equal(segments[1].Field.Identity, segments[2].Field.Identity);
    }
    [Fact]
    public async Task Effective_later_title_is_patched_without_revealing_or_rewriting_earlier_title()
    {
        var draft = new PacketVisualDraft("packet title Earlier\ntitle Effective\n0: \"A\"\n"); var result = await draft.SetTitleAsync("Changed Ω"); Assert.True(result.Success, result.Error); Assert.Equal("packet title Earlier\ntitle Changed Ω\n0: \"A\"\n", draft.Source);
    }
    [Fact]
    public async Task Explicit_addition_and_complete_removal_do_not_renumber_existing_fields()
    {
        var draft = new PacketVisualDraft("packet\n0-7: \"A\"\n"); var key = draft.Model.Fields[0].Identity; var result = await draft.AddFieldAsync("relative", "04", "", "B"); Assert.True(result.Success, result.Error); Assert.Contains("+04: \"B\"", draft.Source); Assert.Equal(key, draft.Model.Fields[0].Identity);
        result = await draft.DeleteAsync(draft.Model.Fields[1].Identity); Assert.True(result.Success, result.Error); Assert.Equal("packet\n0-7: \"A\"\n", draft.Source);
    }
    [Fact]
    public async Task Unknown_grammar_and_unproved_quote_escape_remain_protected_and_exact()
    {
        const string source = "packet\n0: \"A\"\nunknown Ω\n"; var draft = new PacketVisualDraft(source); Assert.Single(draft.Model.Protected);
        var result = await draft.UpdateFieldAsync(draft.Model.Fields[0].Identity, "\"unproved\"", "0"); Assert.False(result.Success); Assert.Equal(source, draft.Source);
        result = await draft.AddFieldAsync("bit", "2", "", "B"); Assert.False(result.Success); Assert.Equal(source, draft.Source);
    }
}
