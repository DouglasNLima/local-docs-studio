using LensDocsStudio.Core.BlockDiagrams;
using LensDocsStudio.Core.SourceAuthoring;

namespace LensDocsStudio.Core.Tests;

public class BlockSourceAuthoringTests
{
    private static void Pass(SourceAuthoringResult result) => Assert.True(result.Success, result.Error);
    [Theory, InlineData("\n"), InlineData("\r\n")]
    public async Task Multi_token_label_span_and_ID_edits_preserve_all_other_bytes_and_stable_occurrences(string newline)
    {
        var source = "%% unchanged Ω\nblock title Old\ncolumns 03\n  a[\"Same\"]:02  space:01\tb(Same)\na -->|Old Ω| b\n".Replace("\n", newline, StringComparison.Ordinal); var draft = new BlockVisualDraft(source); var a = draft.Model.Tokens[0]; var space = draft.Model.Tokens[1]; var keys = draft.Model.Tokens.Select(t => t.Identity).ToArray();
        Pass(await draft.UpdateTokenAsync(a.Identity, "renamed", "New Ω", "rectangle", "02")); Assert.Equal(source.Replace("a[\"Same\"]", "renamed[\"New Ω\"]", StringComparison.Ordinal).Replace("a -->", "renamed -->", StringComparison.Ordinal), draft.Source); Assert.Equal(keys, draft.Model.Tokens.Select(t => t.Identity));
        Pass(await draft.UpdateTokenAsync(space.Identity, "space", "", "bare", "2")); Assert.Contains("  space:2\tb(Same)", draft.Source); draft.Undo(); Assert.Contains("  space:01\tb(Same)", draft.Source); draft.Undo(); Assert.Equal(source, draft.Source); draft.Redo(); Assert.Contains("renamed[\"New Ω\"]:02", draft.Source);
    }
    [Fact]
    public async Task Repeated_spacers_and_duplicate_labels_keep_keys_when_swapped_and_deleted_on_one_line()
    {
        var draft = new BlockVisualDraft("block\ncolumns 4\na[Same]  space\tspace:01 b[Same]\n"); var tokens = draft.Model.Tokens.ToArray();
        Pass(await draft.MoveAsync(tokens[2].Identity, -1)); Assert.Equal("block\ncolumns 4\na[Same]  space:01\tspace b[Same]\n", draft.Source); Assert.Equal(tokens[2].Identity, draft.Model.Tokens[1].Identity); Assert.Equal(tokens[1].Identity, draft.Model.Tokens[2].Identity);
        Pass(await draft.DeleteAsync(tokens[2].Identity)); Assert.Equal("block\ncolumns 4\na[Same]  \tspace b[Same]\n", draft.Source); Assert.Equal(tokens[1].Identity, draft.Model.Tokens[1].Identity);
        draft.Undo(); Assert.Equal(tokens[2].Identity, draft.Model.Tokens[1].Identity); draft.Undo(); Assert.Equal(tokens.Select(t => t.Identity), draft.Model.Tokens.Select(t => t.Identity));
    }
    [Fact]
    public async Task Cross_line_token_ordering_keeps_inter_token_whitespace_and_occurrence_keys()
    {
        var draft = new BlockVisualDraft("block\ncolumns 3\na[A]  b[B]\n\tc[C] d[D]\n"); var b = draft.Model.Tokens[1]; var c = draft.Model.Tokens[2];
        Pass(await draft.MoveAsync(c.Identity, -1)); Assert.Equal("block\ncolumns 3\na[A]  c[C]\n\tb[B] d[D]\n", draft.Source); Assert.Equal(c.Identity, draft.Model.Tokens[1].Identity); Assert.Equal(b.Identity, draft.Model.Tokens[2].Identity);
    }
    [Theory, InlineData("a[First] a[Hidden]"), InlineData("a[First] ! ignored b[Hidden]"), InlineData("a:0 b[Ignored]"), InlineData("a:999999999999999 b[Ignored]"), InlineData("a[[Composite]]")]
    public async Task First_wins_partial_lines_invalid_spans_and_composite_forms_cannot_reveal_or_rewrite_hidden_source(string body)
    { var draft = new BlockVisualDraft("block\ncolumns 3\n" + body + "\n"); Assert.False(draft.Model.TopologySafe); Assert.False((await draft.UpdateTokenAsync(draft.Model.Tokens[0].Identity, "renamed", "Label", "rectangle", "1")).Success); Assert.False((await draft.DeleteAsync(draft.Model.Tokens[0].Identity)).Success); Assert.Equal(draft.OriginalSource, draft.Source); }
    [Fact]
    public async Task Bare_blocks_keep_coupled_ID_labels_until_an_explicit_shape_conversion()
    {
        var draft = new BlockVisualDraft("block\ncolumns 2\na:01 b[B]\na --> b\n"); var a = draft.Model.Tokens[0]; Assert.False((await draft.UpdateTokenAsync(a.Identity, "a", "New", "bare", "01")).Success);
        Pass(await draft.UpdateTokenAsync(a.Identity, "renamed", "a", "bare", "01")); Assert.Contains("renamed:01 b[B]", draft.Source); Assert.Equal("renamed", draft.Model.Tokens[0].Label); Assert.Contains("renamed --> b", draft.Source);
        Pass(await draft.UpdateTokenAsync(a.Identity, "renamed", "Separate", "rounded", "01")); Assert.Contains("renamed(Separate):01 b[B]", draft.Source);
    }
    [Fact]
    public async Task Proved_first_wins_label_remains_editable_while_hidden_declaration_and_topology_stay_protected()
    {
        var draft = new BlockVisualDraft("block\na[First] a[Hidden]\n"); var first = draft.Model.Tokens[0]; var hidden = draft.Model.Tokens[1];
        Pass(await draft.UpdateTokenAsync(first.Identity, "a", "Edited Ω", "rectangle", "1")); Assert.Equal("block\na[Edited Ω] a[Hidden]\n", draft.Source);
        Assert.False((await draft.UpdateTokenAsync(hidden.Identity, "a", "Unexpected", "rectangle", "1")).Success); Assert.False((await draft.DeleteAsync(first.Identity)).Success); Assert.Equal("block\na[Edited Ω] a[Hidden]\n", draft.Source);
    }
    [Fact]
    public async Task Column_changes_do_not_normalise_or_truncate_historical_span_lexemes()
    { var draft = new BlockVisualDraft("block title Grid\ncolumns 003\na[A]:0002 space b[B]:099\n"); Pass(await draft.SetHeadingAsync("Grid", "2")); Assert.Contains("columns 2\na[A]:0002 space b[B]:099\n", draft.Source); Pass(await draft.UpdateTokenAsync(draft.Model.Tokens[2].Identity, "b", "New", "rectangle", "099")); Assert.Contains("b[New]:099", draft.Source); }
    [Theory, InlineData("0"), InlineData("-1"), InlineData("2.0"), InlineData("2e0"), InlineData("2147483648"), InlineData("4")]
    public async Task New_spans_must_be_complete_positive_integers_within_the_current_grid(string span)
    { var draft = new BlockVisualDraft("block\ncolumns 3\na[A]\n"); Assert.False((await draft.UpdateTokenAsync(draft.Model.Tokens[0].Identity, "a", "A", "rectangle", span)).Success); Assert.Equal(draft.OriginalSource, draft.Source); }
    [Fact]
    public async Task Delimiter_rules_follow_Block_source_form_without_C4_quote_escaping()
    {
        var draft = new BlockVisualDraft("block\ncolumns 2\na[\"A\"] b(B)\n"); var a = draft.Model.Tokens[0]; Assert.False((await draft.UpdateTokenAsync(a.Identity, "a", "A \"quoted\"", "rectangle", "1")).Success);
        var b = draft.Model.Tokens[1]; Pass(await draft.UpdateTokenAsync(b.Identity, "b", "Internal \" quote", "rounded", "1")); Assert.Contains("b(Internal \" quote)", draft.Source); Assert.False((await draft.UpdateTokenAsync(b.Identity, "b", "Closes)", "rounded", "1")).Success);
    }
    [Fact]
    public async Task Existing_link_label_forms_and_connected_deletion_keep_exact_endpoint_source()
    {
        var draft = new BlockVisualDraft("block\ncolumns 2\na[A] b[B]\na -- \"Label\" --> b\n"); var edge = draft.Model.Edges[0]; Pass(await draft.UpdateEdgeAsync(edge.Identity, "a", "b", "New Ω")); Assert.Contains("a -- \"New Ω\" --> b", draft.Source);
        var source = draft.Source; Assert.False((await draft.DeleteAsync(draft.Model.Tokens[0].Identity)).Success); Assert.Equal(source, draft.Source); Pass(await draft.DeleteAsync(edge.Identity)); Pass(await draft.DeleteAsync(draft.Model.Tokens[0].Identity)); Assert.Contains(" b[B]", draft.Source);
    }
    [Fact]
    public async Task Spacers_are_repeatable_occurrences_and_never_real_IDs_or_endpoints()
    {
        var draft = new BlockVisualDraft("block\ncolumns 3\na[A] b[B]\n"); Pass(await draft.AddSpaceAsync("2")); Pass(await draft.AddSpaceAsync("1")); Assert.Equal(2, draft.Model.Tokens.Count(t => t.Space));
        Assert.False((await draft.AddEdgeAsync("a", "space")).Success); Assert.False((await draft.AddBlockAsync("space", "Wrong")).Success); Assert.False((await draft.UpdateTokenAsync(draft.Model.Tokens[2].Identity, "newId", "", "bare", "2")).Success);
    }
}
