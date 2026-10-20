using LensDocsStudio.Core.RadarCharts;
using LensDocsStudio.Core.SankeyDiagrams;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Core.XyCharts;

namespace LensDocsStudio.Core.Tests;

public class ChartSourceAuthoringTests
{
    private static void Pass(SourceAuthoringResult result) => Assert.True(result.Success, result.Error);
    [Theory, InlineData("\n"), InlineData("\r\n")]
    public async Task XY_local_value_and_category_edits_preserve_exact_neighbours_and_raw_lexemes(string nl)
    {
        var source = string.Join(nl, "%% café Ω", "xychart title Header", " title Effective", " x-axis [\"A,B\", \"say \"\"hello\"\"\"]", " bar \"Same\" [+1.000e0, -2.5000]", " line \"Same\" [1, 2]", "");
        var d = new XyVisualDraft(source); Pass(await d.ValidateAsync()); var value = d.Model.Series[0].Values.Items[1]; d.Select(value.Identity);
        Pass(await d.UpdateValueAsync(value.Identity, "3.75000")); Assert.Equal(source.Replace("-2.5000", "3.75000", StringComparison.Ordinal), d.Source); Assert.Equal(value.Identity, d.Selection);
        d.Undo(); Assert.Equal(source, d.Source); d.Redo(); Assert.Contains("+1.000e0", d.Source);
        var category = d.Model.Categories!.Categories!.Items[0]; Pass(await d.UpdateCategoryAsync(category.Identity, "New, Ω")); Assert.Contains("[\"New, Ω\", \"say \"\"hello\"\"\"]", d.Source);
    }
    [Fact] public async Task XY_aligned_category_move_is_atomic_and_keeps_occurrence_keys_and_numeric_spelling()
    {
        const string source = "xychart\n x-axis [Same, Same, Last]\n bar [1.000, 2e0, -3]\n line [4, 5.0000, 6]\n";
        var d = new XyVisualDraft(source); var category = d.Model.Categories!.Categories!.Items[1]; var value = d.Model.Series[0].Values.Items[1]; d.Select(category.Identity);
        Pass(await d.MoveAsync(category.Identity, -1)); Assert.Equal(category.Identity, d.Model.Categories.Categories.Items[0].Identity); Assert.Equal(value.Identity, d.Model.Series[0].Values.Items[0].Identity);
        Assert.Contains("bar [ 2e0,1.000, -3]", d.Source); Assert.Contains("line [ 5.0000,4, 6]", d.Source);
        d.Undo(); Assert.Equal(source, d.Source); Assert.Equal(category.Identity, d.Selection); d.Redo(); Assert.Equal(category.Identity, d.Model.Categories.Categories.Items[0].Identity);
        Pass(await d.AddCategoryAsync("Explicit", "-7.2500")); Assert.All(d.Model.Series, s => Assert.Equal("-7.2500", s.Values.Items[^1].Raw));
    }
    [Theory, InlineData("bar [1, invalid, 3]"), InlineData("bar [1, 2 \"suffix\", 3]"), InlineData("box [1,2,3,4,5]"), InlineData("bar [1,2]")]
    public async Task XY_coercion_suffix_box_and_ragged_vectors_protect_alignment_without_rewriting(string row)
    {
        var source = "xychart\n x-axis [A,B,C]\n " + row + "\n line [1,2,3]\n"; var d = new XyVisualDraft(source);
        Assert.False((await d.AddCategoryAsync("D", "0")).Success); Assert.Equal(source, d.Source);
        var v = d.Model.Series[1].Values.Items[0]; Pass(await d.UpdateValueAsync(v.Identity, "4.000")); Assert.Contains(row, d.Source);
    }
    [Fact] public async Task XY_mixed_series_order_and_attached_comments_survive_row_operations()
    {
        var source = "xychart horizontal\n x-axis [A,B]\n %% first\n bar [1,2]\n %% second\n line [3,4]\n"; var d = new XyVisualDraft(source); var s = d.Model.Series[1];
        Pass(await d.MoveAsync(s.Identity, -1)); Assert.Equal(s.Identity, d.Model.Series[0].Identity); Assert.Contains("%% second\n line [3,4]\n %% first\n bar", d.Source);
        Pass(await d.SetOrientationAsync(false)); Assert.Contains("xychart vertical", d.Source); d.Undo(); Assert.Contains("xychart horizontal", d.Source);
    }
    [Fact] public async Task XY_range_correction_keeps_earlier_declarations_and_automatic_range_removes_all_explicit_ranges()
    {
        var source = "xychart title First\n title Last\n y-axis \"Old\" -2 --> 2\n y-axis \"Final\" -5 --> 5\n bar [1,2]\n"; var d = new XyVisualDraft(source);
        Pass(await d.SetRangeAsync("-10.000", "+1e1")); Assert.Contains("y-axis \"Old\" -2 --> 2", d.Source); Assert.Contains("\"Final\" -10.000 --> +1e1", d.Source);
        Pass(await d.SetRangeAsync("", "")); Assert.DoesNotContain("-->", d.Source); Assert.Contains("\"Old\"", d.Source); Assert.Contains("\"Final\"", d.Source);
        Assert.False((await d.SetRangeAsync("2", "2")).Success); Pass(await d.SetTitleAsync("Authored Ω")); Assert.Contains("xychart title First", d.Source);
    }
    [Theory, InlineData(""), InlineData("NaN"), InlineData("Infinity"), InlineData("1,5"), InlineData("+"), InlineData("1e"), InlineData("1e9999"), InlineData("1e-9999")]
    public async Task Invalid_pending_numeric_text_never_becomes_zero_or_changes_private_history(string value)
    {
        var xy = new XyVisualDraft(XyVisualDraft.Starter); var x = xy.Model.Series[0].Values.Items[0]; Assert.False((await xy.UpdateValueAsync(x.Identity, value)).Success); Assert.Equal(xy.OriginalSource, xy.Source); Assert.False(xy.CanUndo);
        var radar = new RadarVisualDraft(RadarVisualDraft.Starter); var r = radar.Model.Curves[0].Values.Items[0]; Assert.False((await radar.UpdateValueAsync(r.Identity, value)).Success); Assert.Equal(radar.OriginalSource, radar.Source); Assert.False(radar.CanUndo);
        var sankey = new SankeyVisualDraft(SankeyVisualDraft.Starter); var s = sankey.Model.Links[0]; Assert.False((await sankey.UpdateAsync(s.Identity, s.Source, s.Target, value)).Success); Assert.Equal(sankey.OriginalSource, sankey.Source); Assert.False(sankey.CanUndo);
    }
    [Theory, InlineData("\n"), InlineData("\r\n")]
    public async Task Radar_axis_move_reorders_every_curve_position_atomically_without_rounding(string nl)
    {
        var source = string.Join(nl, "%% Ω", "radar-beta", " title First", " title Last", " axis a[\"Same\"], b[\"Same\"], c[\"Third\"], d[\"Last\"]", " curve first[\"Same\"]{+1e0, 2.000, 3, 4}", " curve second[\"Same\"]{5, 6, 7.00, 8}", "");
        var d = new RadarVisualDraft(source); Pass(await d.ValidateAsync()); var axis = d.Model.Axes[1]; var value = d.Model.Curves[0].Values.Items[1]; d.Select(axis.Identity);
        Pass(await d.MoveAsync(axis.Identity, -1)); Assert.Equal(axis.Identity, d.Model.Axes[0].Identity); Assert.Equal(value.Identity, d.Model.Curves[0].Values.Items[0].Identity);
        Assert.Contains("{ 2.000,+1e0, 3, 4}", d.Source); Assert.Contains("{ 6,5, 7.00, 8}", d.Source); d.Undo(); Assert.Equal(source, d.Source);
        Pass(await d.AddAxisAsync("added", "Explicit", "-2.500")); Assert.All(d.Model.Curves, c => Assert.Equal("-2.500", c.Values.Items[^1].Raw));
        Pass(await d.DeleteAsync(d.Model.Axes[^1].Identity)); Assert.Equal(source, d.Source);
    }
    [Theory, InlineData("1,invalid,3"), InlineData("a:1,b:2,c:3"), InlineData("1,2"), InlineData("1,2,3,4"), InlineData("1,NaN,3")]
    public async Task Radar_dropped_keyed_ragged_and_extra_vectors_are_preserved_and_protect_axis_changes(string values)
    {
        var source = "radar-beta\n axis a,b,c\n curve c{" + values + "}\n"; var d = new RadarVisualDraft(source);
        Assert.False((await d.AddAxisAsync("d", "Fourth", "0")).Success); Assert.Equal(source, d.Source); Assert.False(d.CanUndo);
    }
    [Fact] public async Task Radar_range_directives_use_the_actual_restrictive_grammar_and_auto_maximum()
    {
        var d = new RadarVisualDraft(RadarVisualDraft.Starter); Assert.False((await d.SetRangeAsync("-1", "10")).Success); Assert.False((await d.SetRangeAsync("0", "1e1")).Success);
        Pass(await d.SetRangeAsync("1.000", "12.500")); Assert.Contains("min 1.000", d.Source); Assert.Contains("max 12.500", d.Source);
        Pass(await d.SetRangeAsync("0", "")); Assert.DoesNotContain("max ", d.Source); Assert.Equal(8, d.Model.Maximum); Assert.False((await d.SetRangeAsync("10", "")).Success);
    }
    [Theory, InlineData("comma,name"), InlineData("bracket]"), InlineData("quote\""), InlineData("line\nbreak"), InlineData("brace}"), InlineData("")]
    public async Task Radar_unrepresentable_new_text_stays_pending(string label)
    { var d = new RadarVisualDraft(RadarVisualDraft.Starter); Assert.False((await d.UpdateAxisAsync(d.Model.Axes[0].Identity, "guides", label)).Success); Assert.Equal(d.OriginalSource, d.Source); }
    [Theory, InlineData("\n"), InlineData("\r\n")]
    public async Task Sankey_local_endpoint_edit_preserves_parallel_rows_quoting_precision_and_extra_fields(string nl)
    {
        var source = string.Join(nl, "%% Ω", "sankey-beta", "  \"A, Ω\",\"say \"\"hello\"\"\",+1.500e0,ignored", "  \"A, Ω\",\"say \"\"hello\"\"\",+1.500e0", "");
        var d = new SankeyVisualDraft(source); Pass(await d.ValidateAsync()); var link = d.Model.Links[1]; d.Select(link.Identity);
        Pass(await d.UpdateAsync(link.Identity, link.Source, "New, \"target\"", link.Weight)); Assert.Equal(source[..source.LastIndexOf("  \"A, Ω\"", StringComparison.Ordinal)] + "  \"A, Ω\",\"New, \"\"target\"\"\",+1.500e0" + nl, d.Source);
        Assert.Equal(link.Identity, d.Selection); d.Undo(); Assert.Equal(source, d.Source); d.Redo(); Assert.Contains("ignored", d.Source);
    }
    [Fact] public async Task Sankey_node_rename_is_explicit_global_and_rejects_identity_merges()
    {
        var d = new SankeyVisualDraft(SankeyVisualDraft.Starter); Assert.False((await d.RenameNodeAsync("Draft", "Review")).Success); Assert.Equal(d.OriginalSource, d.Source);
        Pass(await d.RenameNodeAsync("Draft", "Authored, Ω")); Assert.Equal(2, d.Model.Links.Count(l => l.Source == "Authored, Ω")); Assert.DoesNotContain("Draft", d.Source);
        d.Undo(); Assert.Equal(d.OriginalSource, d.Source);
    }
    [Theory, InlineData("0"), InlineData("-1"), InlineData("NaN"), InlineData("Infinity"), InlineData("invalid")]
    public async Task Sankey_ignored_rows_remain_exact_and_cannot_be_silently_repaired(string value)
    {
        var source = "sankey-beta\n A,B," + value + "\n A,B,2.000\n"; var d = new SankeyVisualDraft(source); Assert.Single(d.Model.Protected); var row = d.Model.Links.Single();
        Pass(await d.UpdateAsync(row.Identity, "Changed", row.Target, row.Weight)); Assert.Contains("A,B," + value, d.Source); Assert.False((await d.RenameNodeAsync("B", "Global")).Success);
    }
    [Theory, InlineData("A,A,5\nA,B,3\n"), InlineData("A,B,3\nB,C,3\nC,A,3\n")]
    public async Task Sankey_cycles_and_self_links_stay_exact_and_protect_topology_changes(string rows)
    {
        var d = new SankeyVisualDraft("sankey-beta\n" + rows); Assert.True(d.Model.Cyclic); Assert.False((await d.AddAsync("X", "Y", "1")).Success); Assert.Equal(d.OriginalSource, d.Source);
    }
    [Fact] public async Task Sankey_parallel_link_moves_keep_stable_occurrences_and_comment_attachment()
    {
        var source = "sankey-beta\n %% first\n A,B,1.000\n %% second\n A,B,1e0\n B,C,2\n"; var d = new SankeyVisualDraft(source); var second = d.Model.Links[1]; d.Select(second.Identity);
        Pass(await d.MoveAsync(second.Identity, -1)); Assert.Equal(second.Identity, d.Model.Links[0].Identity); Assert.Contains("%% second\n A,B,1e0\n %% first", d.Source); d.Undo(); Assert.Equal(source, d.Source);
        Assert.False((await d.AddAsync("C", "A", "1")).Success); Assert.Equal(source, d.Source);
    }
    [Fact] public async Task All_three_preserve_configuration_and_comments_while_using_the_ordinary_detector()
    {
        foreach (var starter in new[] { XyVisualDraft.Starter, RadarVisualDraft.Starter, SankeyVisualDraft.Starter })
        {
            var source = "---\ntitle: Metadata\n---\n%% preserved Ω\n\n%%{init: {\"theme\":\"dark\"}}%%\n" + starter;
            ISourceDraft draft = XyVisualDraft.IsXy(source) ? new XyVisualDraft(source) : RadarVisualDraft.IsRadar(source) ? new RadarVisualDraft(source) : new SankeyVisualDraft(source);
            Pass(await draft.ValidateAsync()); Assert.Equal(source, draft.Source);
        }
    }
}
