using LensDocsStudio.Core.PieDiagrams;
using LensDocsStudio.Core.SourceAuthoring;

namespace LensDocsStudio.Core.Tests;

public class PieVisualTests
{
    private static void Success(SourceAuthoringResult result) => Assert.True(result.Success, result.Error);
    [Theory, InlineData("\n"), InlineData("\r\n")]
    public async Task No_op_and_local_edit_preserve_untouched_bytes_and_decimal_precision(string newline)
    {
        var source = string.Join(newline, "%% café 😀", "pie showData title Mix", "", "    \"Guides : [α]\" : 0001.23000000000000000001", "    %% attached reference", "    \"Reference\" : 2.50", "");
        var draft = new PieVisualDraft(source); var row = draft.Model.Entries[0]; draft.Select(row.Identity);
        Success(await draft.ValidateAsync()); Success(await draft.UpdateAsync(row.Identity, row.Label, row.Number)); Assert.False(draft.CanUndo); Assert.Equal(source, draft.Source);
        Success(await draft.UpdateAsync(row.Identity, "Guides : [β]", row.Number));
        Assert.Equal(source.Replace("[α]", "[β]", StringComparison.Ordinal), draft.Source); Assert.Equal(row.Identity, draft.Selection);
        draft.Undo(); Assert.Equal(source, draft.Source); Assert.Equal(row.Identity, draft.Selection); draft.Redo(); Assert.Contains("[β]", draft.Source);
    }
    [Fact] public async Task Duplicate_labels_keep_separate_occurrences_through_moves_and_undo()
    {
        var source = "pie\n  %% first\n  \"Same\" : 1\n  %% second\n  \"Same\" : 2\n"; var draft = new PieVisualDraft(source);
        var second = draft.Model.Entries[1]; draft.Select(second.Identity); Success(await draft.MoveAsync(second.Identity, -1));
        Assert.Equal("pie\n  %% second\n  \"Same\" : 2\n  %% first\n  \"Same\" : 1\n", draft.Source); Assert.Equal(second.Identity, draft.Model.Entries[0].Identity);
        draft.Undo(); Assert.Equal(source, draft.Source); Assert.Equal(second.Identity, draft.Selection); draft.Redo(); Assert.Equal(second.Identity, draft.Model.Entries[0].Identity);
    }
    [Theory, InlineData(""), InlineData("-1"), InlineData("NaN"), InlineData("Infinity"), InlineData("1."), InlineData("1e2"), InlineData("1,5")]
    public async Task Invalid_intermediate_values_leave_draft_and_selection_recoverable(string number)
    {
        var draft = new PieVisualDraft(PieVisualDraft.Starter); var row = draft.Model.Entries[0]; draft.Select(row.Identity);
        var result = await draft.UpdateAsync(row.Identity, row.Label, number); Assert.False(result.Success); Assert.Equal(PieVisualDraft.Starter, draft.Source); Assert.Equal(row.Identity, draft.Selection); Assert.False(draft.CanUndo);
        Success(await draft.UpdateAsync(row.Identity, row.Label, "0.125"));
    }
    [Theory, InlineData("A\"B"), InlineData("A\rB"), InlineData("")]
    public async Task Labels_the_pinned_parser_cannot_encode_are_rejected_without_statement_injection(string label)
    {
        var draft = new PieVisualDraft(PieVisualDraft.Starter); Assert.False((await draft.AddAsync(label, "1")).Success); Assert.Equal(PieVisualDraft.Starter, draft.Source);
    }
    [Fact] public async Task Label_line_breaks_are_encoded_safely_and_unchanged_escape_spellings_are_retained()
    {
        var source = "pie showData\n  \"First<br/>second\" : 1.25000\n  \"Other\" : 2\n"; var draft = new PieVisualDraft(source); var row = draft.Model.Entries[0];
        Success(await draft.UpdateAsync(row.Identity, "First\nsecond", row.Number)); Assert.Equal(source, draft.Source); Assert.False(draft.CanUndo);
        Success(await draft.UpdateAsync(row.Identity, "New\nline : Ω", row.Number)); Assert.Equal(source.Replace("First<br/>second", "New\\nline : Ω", StringComparison.Ordinal), draft.Source);
        Success(await draft.AddAsync("Third\nline", "3")); Assert.Contains("\"Third\\nline\" : 3", draft.Source);
        Assert.Equal(3, new System.Xml.Linq.XDocument(System.Xml.Linq.XDocument.Parse(new LensDocsStudio.Core.Rendering.DiagramService().Render(draft.Source).Svg!)).Descendants().Count(element => (string?)element.Attribute("class") == "pie-slice"));
    }
    [Fact] public async Task Zero_rows_are_retained_and_empty_or_all_zero_charts_cannot_apply()
    {
        var source = "pie showData\n \"Zero\" : 0.000\n \"Positive\" : 2\n"; var draft = new PieVisualDraft(source); Success(await draft.ValidateAsync());
        Assert.False((await draft.DeleteAsync(draft.Model.Entries[1].Identity)).Success); Assert.Equal(source, draft.Source);
        Assert.False((await new PieVisualDraft("pie\n").ValidateAsync()).Success);
        var empty = new PieVisualDraft("pie\n \"Zero\" : 0\n"); Assert.False((await empty.ValidateAsync()).Success); Success(await empty.AddAsync("Recovery", "1"));
    }
    [Fact] public async Task Unknown_directives_protect_crossing_moves_while_local_edits_remain_safe()
    {
        var source = "pie showData\n \"A\" : 1\n unknown preserved bytes\n \"B\" : 2\n"; var draft = new PieVisualDraft(source); var row = draft.Model.Entries[1];
        Assert.False((await draft.MoveAsync(row.Identity, -1)).Success); Success(await draft.UpdateAsync(row.Identity, "B : Ω", "2"));
        Assert.Equal(source.Replace("\"B\"", "\"B : Ω\"", StringComparison.Ordinal), draft.Source);
    }
    [Fact] public async Task Title_add_remove_and_private_history_keep_exact_source_snapshots()
    {
        var draft = new PieVisualDraft("pie\n \"A\" : 1\n"); Success(await draft.SetTitleAsync("Café : Ω")); var titled = draft.Source;
        Success(await draft.AddAsync("B", "0.1234567890123456789")); var added = draft.Source; var row = draft.Model.Entries[1]; draft.Select(row.Identity);
        Success(await draft.DeleteAsync(row.Identity)); Assert.Equal(titled, draft.Source); draft.Undo(); Assert.Equal(added, draft.Source); Assert.Equal(row.Identity, draft.Selection); draft.Redo(); Assert.Equal(titled, draft.Source);
    }
    [Fact] public async Task Malformed_rows_cannot_be_silently_omitted_by_the_permissive_engine()
    {
        var source = "pie\n \"Valid\" : 1\n \"Broken\" : -4\n"; var draft = new PieVisualDraft(source);
        Assert.False((await draft.ValidateAsync()).Success); Assert.False((await draft.SetTitleAsync("Title")).Success); Assert.Equal(source, draft.Source);
        Success(await draft.ValidateSourceAsync(source.Replace("-4", "4", StringComparison.Ordinal)));
    }
    [Fact] public async Task A_positive_decimal_that_underflows_cannot_be_silently_omitted_as_a_zero_row()
    {
        var tiny = "0." + new string('0', 400) + "1"; var draft = new PieVisualDraft(PieVisualDraft.Starter); var original = draft.Source;
        Assert.False((await draft.AddAsync("Tiny", tiny)).Success); Assert.Equal(original, draft.Source);
        var supplied = new PieVisualDraft("pie\n  \"Positive\" : 1\n  \"Tiny\" : " + tiny + "\n"); Assert.False((await supplied.ValidateAsync()).Success);
    }
}
