using LensDocsStudio.Core.QuadrantDiagrams;
using LensDocsStudio.Core.SourceAuthoring;

namespace LensDocsStudio.Core.Tests;

public class QuadrantVisualTests
{
    private static void Success(SourceAuthoringResult result) => Assert.True(result.Success, result.Error);
    [Theory, InlineData("\n"), InlineData("\r\n")]
    public async Task No_op_and_local_label_edit_keep_raw_coordinates_comments_and_unicode(string nl)
    {
        var source = string.Join(nl, "%% café 😀", "quadrantChart title Header", "  title Effective", "  x-axis Only left", "  Same: [.5000000000000000001, 9]", "", "  %% attached", "  Same: [-2, 0.5]", "");
        var draft = new QuadrantVisualDraft(source); var p = draft.Model.Points[0]; draft.Select(p.Identity);
        Success(await draft.ValidateAsync()); Success(await draft.UpdateAsync(p.Identity, p.Label, p.X, p.Y)); Assert.Equal(source, draft.Source); Assert.False(draft.CanUndo);
        Success(await draft.UpdateAsync(p.Identity, "Changed Ω", p.X, p.Y)); Assert.Equal(source.Replace("  Same: [.5", "  Changed Ω: [.5", StringComparison.Ordinal), draft.Source);
        Assert.Equal(p.Identity, draft.Selection); draft.Undo(); Assert.Equal(source, draft.Source); draft.Redo(); Assert.Contains("Changed Ω", draft.Source);
    }
    [Fact] public async Task Duplicate_and_coincident_points_keep_order_coordinates_and_identity_through_moves()
    {
        var source = "quadrantChart\n  %% one\n  Same: [.5, .5]\n  %% two\n  Same: [0.500, 0.500]\n  Limit: [1, 0]\n";
        var draft = new QuadrantVisualDraft(source); var p = draft.Model.Points[1]; draft.Select(p.Identity); Success(await draft.MoveAsync(p.Identity, -1));
        Assert.Equal(p.Identity, draft.Model.Points[0].Identity); Assert.Equal("0.500", draft.Model.Points[0].X); Assert.StartsWith("quadrantChart\n  %% two\n", draft.Source);
        draft.Undo(); Assert.Equal(source, draft.Source); Assert.Equal(p.Identity, draft.Selection); draft.Redo(); Assert.Equal(p.Identity, draft.Model.Points[0].Identity);
    }
    [Theory, InlineData(""), InlineData("1."), InlineData("1,5"), InlineData("NaN"), InlineData("Infinity"), InlineData("1.01"), InlineData("-0.1"), InlineData("1e-1")]
    public async Task Changed_coordinate_must_be_finite_complete_and_in_range(string value)
    {
        var draft = new QuadrantVisualDraft(QuadrantVisualDraft.Starter); var p = draft.Model.Points[0]; draft.Select(p.Identity);
        Assert.False((await draft.UpdateAsync(p.Identity, "Pending", value, p.Y)).Success); Assert.Equal(QuadrantVisualDraft.Starter, draft.Source); Assert.Equal(p.Identity, draft.Selection); Assert.False(draft.CanUndo);
        Success(await draft.UpdateAsync(p.Identity, p.Label, "0", "1"));
    }
    [Fact] public async Task Explicit_coordinate_correction_is_local_and_other_clamped_lexeme_survives()
    {
        var source = "quadrantChart\n  A: [9, -2]\n  B: [.5, .50]\n"; var draft = new QuadrantVisualDraft(source); var p = draft.Model.Points[0];
        Assert.Equal(1, p.EffectiveX); Assert.Equal(0, p.EffectiveY); Success(await draft.UpdateAsync(p.Identity, "A", "0.125000", p.Y));
        Assert.Equal(source.Replace("[9,", "[0.125000,", StringComparison.Ordinal), draft.Source);
    }
    [Fact] public async Task Last_declarations_win_and_editing_retains_preceding_forms_and_directive_order()
    {
        var source = "quadrantChart showData title Header\n title First\n x-axis First --> Other\n quadrant-1 Earlier\n title Last\n x-axis Only\n quadrant-1 Final\n A: [.5, .5]\n";
        var draft = new QuadrantVisualDraft(source); Success(await draft.ValidateAsync()); Assert.Equal("Last", draft.Model.Title!.Value); Assert.Equal("Only", draft.Model.Effective("x-axis")!.Value);
        Success(await draft.SetTitleAsync("New")); Success(await draft.SetDeclarationAsync("x-axis", "Left --> Right")); Success(await draft.SetDeclarationAsync("quadrant-1", "Invest"));
        Assert.Equal(source.Replace("title Last", "title New", StringComparison.Ordinal).Replace("x-axis Only", "x-axis Left --> Right", StringComparison.Ordinal).Replace("quadrant-1 Final", "quadrant-1 Invest", StringComparison.Ordinal), draft.Source);
    }
    [Fact] public async Task Unknown_source_protects_crossing_moves_but_allows_local_edits()
    {
        var source = "quadrantChart\n A: [.25, .75]\n classDef preserved bytes\n B: [.75, .25]\n"; var draft = new QuadrantVisualDraft(source); var p = draft.Model.Points[1];
        Assert.Single(draft.Model.Protected); Assert.False((await draft.MoveAsync(p.Identity, -1)).Success); Success(await draft.UpdateAsync(p.Identity, "Safe", p.X, p.Y)); Assert.Contains("classDef preserved bytes", draft.Source);
    }
    [Theory, InlineData("A:B"), InlineData("A\nB"), InlineData("A\rB"), InlineData("")]
    public async Task Unrepresentable_new_names_do_not_create_extra_elements(string value)
    {
        var draft = new QuadrantVisualDraft(QuadrantVisualDraft.Starter); Assert.False((await draft.AddAsync(value, ".5", ".5")).Success); Assert.Equal(QuadrantVisualDraft.Starter, draft.Source);
    }
    [Fact] public async Task Add_delete_and_cancelled_validation_preserve_bounded_private_history()
    {
        var draft = new QuadrantVisualDraft(QuadrantVisualDraft.Starter); Success(await draft.AddAsync("New", "0", "1")); var added = draft.Source; var p = draft.Model.Points[^1]; draft.Select(p.Identity);
        Success(await draft.DeleteAsync(p.Identity)); Assert.Equal(QuadrantVisualDraft.Starter, draft.Source); draft.Undo(); Assert.Equal(added, draft.Source); Assert.Equal(p.Identity, draft.Selection);
        using var cancel = new CancellationTokenSource(); cancel.Cancel(); Assert.False((await draft.UpdateAsync(p.Identity, "Cancelled", p.X, p.Y, cancel.Token)).Success); Assert.Equal(added, draft.Source);
    }
}
