using LensDocsStudio.Core.RadarCharts;
using LensDocsStudio.Core.SankeyDiagrams;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Core.XyCharts;

namespace LensDocsStudio.Core.Tests;

public class ChartSourceAuthoringTests
{
    private static void Pass(SourceAuthoringResult result) => Assert.True(result.Success, result.Error);
    [Fact] public async Task Radar_dropped_tokens_have_proved_read_only_canonical_positions_without_changing_source()
    {
        const string source = "radar-beta\n axis A,B,C\n curve malformed{1,invalid,C:3}\n curve simple{3,2,1}\n"; var draft = new RadarVisualDraft(source); Pass(await draft.ValidateAsync());
        var semantic = RadarSourceModel.SemanticValues(draft.Model.Curves[0]); Assert.Equal(new[] { "1", "C:3" }, semantic.Select(v => v.Token.Raw)); Assert.Equal(draft.Model.Curves[0].Values.Items[2].Identity, semantic[1].Token.Identity);
        var safe = draft.Model.Curves[1].Values.Items[0]; Pass(await draft.UpdateValueAsync(safe.Identity, "4.000")); Assert.Contains("curve malformed{1,invalid,C:3}", draft.Source); Assert.False((await draft.UpdateValueAsync(semantic[1].Token.Identity, "4")).Success);
    }
    [Theory, InlineData("5 --> 5"), InlineData("10 --> -10")]
    public async Task Historical_XY_equal_and_reversed_ranges_remain_exact_during_safe_local_edits(string range)
    {
        var source = "xychart\n x-axis [A,B]\n y-axis " + range + "\n line [1.000,2e0]\n"; var draft = new XyVisualDraft(source); Pass(await draft.ValidateAsync());
        Pass(await draft.UpdateSeriesAsync(draft.Model.Series[0].Identity, "Named Ω")); Assert.Contains("y-axis " + range, draft.Source); Assert.Contains("[1.000,2e0]", draft.Source); Assert.False((await draft.SetRangeAsync("5", "5")).Success);
    }
    [Theory, InlineData("0"), InlineData("-1"), InlineData("-1e-2")]
    public async Task New_Sankey_weights_must_remain_positive_without_clamping(string weight)
    {
        var draft = new SankeyVisualDraft(SankeyVisualDraft.Starter); var link = draft.Model.Links[0]; Assert.False((await draft.UpdateAsync(link.Identity, link.Source, link.Target, weight)).Success); Assert.False((await draft.AddAsync("X", "Y", weight)).Success); Assert.Equal(draft.OriginalSource, draft.Source); Assert.False(draft.CanUndo);
    }
    [Fact] public async Task Invariant_chart_numeric_spelling_is_preserved_under_a_comma_decimal_UI_culture()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
            var xy = new XyVisualDraft(XyVisualDraft.Starter); Pass(await xy.UpdateValueAsync(xy.Model.Series[0].Values.Items[0].Identity, "+3.7500e+0")); Assert.Contains("+3.7500e+0", xy.Source);
            var radar = new RadarVisualDraft(RadarVisualDraft.Starter); Pass(await radar.UpdateValueAsync(radar.Model.Curves[0].Values.Items[0].Identity, "+3.7500e+0")); Assert.Contains("+3.7500e+0", radar.Source);
            var sankey = new SankeyVisualDraft(SankeyVisualDraft.Starter); var link = sankey.Model.Links[0]; Pass(await sankey.UpdateAsync(link.Identity, link.Source, link.Target, "+3.7500e+0")); Assert.Contains("+3.7500e+0", sankey.Source);
            Assert.False((await xy.UpdateValueAsync(xy.Model.Series[0].Values.Items[0].Identity, "1,5")).Success);
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = original; }
    }
    [Fact] public async Task Proven_quoted_category_and_curve_names_retain_delimiters_without_invented_escapes()
    {
        var xy = new XyVisualDraft(XyVisualDraft.Starter); var category = xy.Model.Categories!.Categories!.Items[0]; Pass(await xy.UpdateCategoryAsync(category.Identity, "[quoted], \"Ω\"")); Assert.Equal("[quoted], \"Ω\"", xy.Model.Categories.Categories.Items[0].Value);
        var radar = new RadarVisualDraft(RadarVisualDraft.Starter); var curve = radar.Model.Curves[0]; Pass(await radar.UpdateCurveAsync(curve.Identity, "a,b{c}[d]", "Label, {braces} [brackets]")); Assert.Equal("a,b{c}[d]", radar.Model.Curves[0].Id); Assert.Equal("Label, {braces} [brackets]", radar.Model.Curves[0].Label);
        Assert.False((await radar.UpdateCurveAsync(curve.Identity, "quote\"", "Pending")).Success);
    }
    [Fact] public async Task Invalid_Radar_range_non_finite_values_and_excessive_ticks_are_protected_before_rendering()
    {
        foreach (var suffix in new[] { " min 5\n max 5\n", " max 0\n", " ticks 999999999\n", " curve invalid{1,Infinity,3}\n" })
        {
            var draft = new RadarVisualDraft("radar-beta\n axis A,B,C\n curve c{1,2,3}\n" + suffix); Assert.NotNull(draft.ValidationError); Assert.False((await draft.ValidateAsync()).Success); Assert.Equal(draft.OriginalSource, draft.Source);
        }
        var corrected = new RadarVisualDraft("radar-beta\n axis A,B,C\n max 0\n curve c{1,2,3}\n"); Pass(await corrected.SetRangeAsync("0", "10"));
        var keyed = new RadarVisualDraft("radar-beta\n axis A,B,C\n curve c{C:10,A:20,B:30}\n"); Assert.Equal(30, keyed.Model.AutomaticMaximum);
    }
    [Fact] public async Task Sankey_integral_node_label_overflow_is_a_concrete_protected_pinned_limitation()
    {
        var draft = new SankeyVisualDraft("sankey-beta\n A,B,1e20\n"); Assert.Contains("2^63", draft.ValidationError); Assert.False((await draft.ValidateAsync()).Success); Assert.Equal(draft.OriginalSource, draft.Source);
        var ordinary = new SankeyVisualDraft(SankeyVisualDraft.Starter); var link = ordinary.Model.Links[0]; Assert.False((await ordinary.UpdateAsync(link.Identity, link.Source, link.Target, "1e20")).Success); Assert.Equal(ordinary.OriginalSource, ordinary.Source);
    }
    [Fact] public async Task Byte_identical_duplicate_vector_move_still_records_private_occurrence_history()
    {
        const string source = "xychart\n x-axis [ Same, Same]\n bar [ 1, 1]\n"; var draft = new XyVisualDraft(source); var category = draft.Model.Categories!.Categories!.Items[1]; var value = draft.Model.Series[0].Values.Items[1]; draft.Select(category.Identity);
        Pass(await draft.MoveAsync(category.Identity, -1)); Assert.Equal(source, draft.Source); Assert.True(draft.CanUndo); Assert.Equal(category.Identity, draft.Model.Categories.Categories.Items[0].Identity); Assert.Equal(value.Identity, draft.Model.Series[0].Values.Items[0].Identity);
        draft.Undo(); Assert.Equal(category.Identity, draft.Model.Categories.Categories.Items[1].Identity); Assert.Equal(value.Identity, draft.Model.Series[0].Values.Items[1].Identity); draft.Redo(); Assert.Equal(category.Identity, draft.Model.Categories.Categories.Items[0].Identity);
    }
    [Fact] public async Task Radar_axis_moves_across_unambiguous_simple_declarations_keep_every_curve_position()
    {
        const string source = "radar-beta\n axis a,b\n axis c,d\n curve c{1,2,3,4}\n"; var draft = new RadarVisualDraft(source); var axis = draft.Model.Axes[1]; draft.Select(axis.Identity);
        Pass(await draft.MoveAsync(axis.Identity, 1)); Assert.Equal(axis.Identity, draft.Model.Axes[2].Identity); Assert.Equal(new[] { "1", "3", "2", "4" }, draft.Model.Curves[0].Values.Items.Select(v => v.Raw)); draft.Undo(); Assert.Equal(source, draft.Source);
        var protectedSource = source.Replace(" axis c,d", " %% ambiguous group\n axis c,d", StringComparison.Ordinal); var guarded = new RadarVisualDraft(protectedSource); Assert.False((await guarded.MoveAsync(guarded.Model.Axes[1].Identity, 1)).Success); Assert.Equal(protectedSource, guarded.Source);
    }
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
