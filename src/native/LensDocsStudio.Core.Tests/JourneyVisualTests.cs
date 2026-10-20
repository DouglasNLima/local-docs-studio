using LensDocsStudio.Core.JourneyDiagrams;
using LensDocsStudio.Core.SourceAuthoring;

namespace LensDocsStudio.Core.Tests;

public class JourneyVisualTests
{
    private static void Success(SourceAuthoringResult result) => Assert.True(result.Success, result.Error);
    [Theory, InlineData("\n"), InlineData("\r\n")]
    public async Task No_op_and_local_label_edit_preserve_clamped_raw_score_actor_duplicates_and_suffix_forms(string nl)
    {
        var source = string.Join(nl, "%% café 😀", "journey title Header", " TITLE Last", " section Empty", " Same : 009 : Author , Author,  Reviewer  ", " Same: 0", " Same: 3:", "");
        var draft = new JourneyVisualDraft(source); var t = draft.Model.Tasks[0]; draft.Select(t.Identity); Success(await draft.ValidateAsync()); Success(await draft.UpdateAsync(t.Identity, t.Label, t.Score)); Assert.Equal(source, draft.Source); Assert.False(draft.CanUndo);
        Assert.Equal(5, t.EffectiveScore); Assert.Equal(1, draft.Model.Tasks[1].EffectiveScore); Assert.Equal(3, t.Actors.Count);
        Success(await draft.UpdateAsync(t.Identity, "Edited Ω", t.Score)); Assert.Equal(source.Replace(" Same : 009", " Edited Ω : 009", StringComparison.Ordinal), draft.Source); draft.Undo(); Assert.Equal(source, draft.Source); Assert.Equal(t.Identity, draft.Selection);
    }
    [Theory, InlineData(""), InlineData("0"), InlineData("6"), InlineData("2.5"), InlineData("-1"), InlineData("NaN"), InlineData(" 3"), InlineData("03")]
    public async Task New_score_edits_require_complete_integers_one_to_five(string value)
    {
        var draft = new JourneyVisualDraft(JourneyVisualDraft.Starter); var t = draft.Model.Tasks[0]; Assert.False((await draft.UpdateAsync(t.Identity, t.Label, value)).Success); Assert.Equal(JourneyVisualDraft.Starter, draft.Source); Assert.False(draft.CanUndo);
    }
    [Fact] public async Task Explicit_correction_changes_only_the_requested_score_lexeme()
    {
        var source = "journey\n A: 9: Me, Me\n B: 0\n"; var draft = new JourneyVisualDraft(source); var t = draft.Model.Tasks[0]; Success(await draft.UpdateAsync(t.Identity, t.Label, "4")); Assert.Equal(source.Replace("A: 9", "A: 4", StringComparison.Ordinal), draft.Source);
    }
    [Fact] public async Task Actor_edits_are_task_local_ordered_occurrences_with_colons_accepted_after_the_suffix_separator()
    {
        var source = "journey\n Same: 3: Me , Me, Cat\n Same: 4: Me, Cat\n"; var draft = new JourneyVisualDraft(source); var t = draft.Model.Tasks[0]; var actor = t.Actors[1]; draft.Select(actor.Identity);
        Success(await draft.UpdateActorAsync(t.Identity, actor.Identity, "Reviewer: Ω")); Assert.Equal("journey\n Same: 3: Me , Reviewer: Ω, Cat\n Same: 4: Me, Cat\n", draft.Source);
        Success(await draft.MoveAsync(actor.Identity, -1)); Assert.Equal("Reviewer: Ω", draft.Model.Tasks[0].Actors.Single(a => a.Identity == draft.Selection).Name);
        draft.Undo(); draft.Undo(); Assert.Equal(source, draft.Source); Assert.Equal(actor.Identity, draft.Selection);
    }
    [Fact] public async Task Actor_add_remove_retains_absent_versus_explicitly_empty_suffix_and_whitespace()
    {
        var source = "journey\n A: 3\n B: 4:   \n C: 5: Me, Me\n"; var draft = new JourneyVisualDraft(source); var a = draft.Model.Tasks[0]; var b = draft.Model.Tasks[1];
        Success(await draft.AddActorAsync(a.Identity, "Author")); Assert.Contains(" A: 3: Author\n", draft.Source); Success(await draft.DeleteAsync(draft.Model.Tasks[0].Actors[0].Identity)); Assert.Contains(" A: 3: \n", draft.Source);
        Success(await draft.AddActorAsync(b.Identity, "Reviewer")); Assert.Contains(" B: 4:   Reviewer\n", draft.Source); var c = draft.Model.Tasks[2]; Success(await draft.DeleteAsync(c.Actors[0].Identity)); Assert.Contains(" C: 5:  Me\n", draft.Source);
    }
    [Theory, InlineData("A,B"), InlineData("A\nB"), InlineData("A\rB"), InlineData("")]
    public async Task A_new_actor_cannot_inject_multiple_actors_or_source_lines(string value)
    { var draft = new JourneyVisualDraft(JourneyVisualDraft.Starter); Assert.False((await draft.AddActorAsync(draft.Model.Tasks[0].Identity, value)).Success); Assert.Equal(JourneyVisualDraft.Starter, draft.Source); }
    [Fact] public async Task Complete_section_and_task_moves_preserve_source_keys_comments_and_empty_sections()
    {
        var source = "journey\n U: 3\n section Empty\n section A\n %% attached\n Same: 9: Me, Me\n Other: 2\n section B\n Same: 4:\n"; var draft = new JourneyVisualDraft(source); var t = draft.Model.Tasks[1]; draft.Select(t.Identity);
        Success(await draft.MoveAsync(t.Identity, 1)); Assert.Equal(t.Identity, draft.Model.Tasks[2].Identity); Success(await draft.MoveToSectionAsync(t.Identity, draft.Model.Sections[1].Identity)); Assert.Equal(draft.Model.Sections[1].Identity, draft.Model.Tasks.Single(x => x.Identity == t.Identity).Section); Assert.Contains(" %% attached\n Same: 9: Me, Me\n", draft.Source);
        Success(await draft.MoveAsync(draft.Model.Sections[2].Identity, 1)); Assert.Contains(" section B\n Same: 4:\n section A\n Other: 2\n", draft.Source);
        draft.Undo(); draft.Undo(); draft.Undo(); Assert.Equal(source, draft.Source); Assert.Equal(t.Identity, draft.Selection);
    }
    [Fact] public async Task Ignored_overflow_scores_unknown_lines_and_comments_preserve_local_editing_but_protect_crossing()
    {
        var source = "journey\n A: 3\n Ignored: 999999999999999999999999999\n unknown bytes\n B: 9: Me\n"; var draft = new JourneyVisualDraft(source); Success(await draft.ValidateAsync()); Assert.Equal(2, draft.Model.Tasks.Count); Assert.Equal(2, draft.Model.Protected.Count);
        var b = draft.Model.Tasks[1]; Assert.False((await draft.MoveAsync(b.Identity, -1)).Success); Success(await draft.UpdateAsync(b.Identity, "Safe", b.Score)); Assert.Contains("Ignored: 999999999999999999999999999", draft.Source); Assert.Contains("Safe: 9", draft.Source);
    }
    [Theory, InlineData("title Hidden"), InlineData("section Hidden"), InlineData("%% Hidden"), InlineData("A:B")]
    public async Task Task_names_cannot_turn_into_directives_or_separator_injection(string value)
    { var draft = new JourneyVisualDraft(JourneyVisualDraft.Starter); var t = draft.Model.Tasks[0]; Assert.False((await draft.UpdateAsync(t.Identity, value, t.Score)).Success); Assert.Equal(JourneyVisualDraft.Starter, draft.Source); }
    [Fact] public async Task Task_section_removal_and_repeated_titles_have_exact_private_undo()
    {
        var source = "journey title Header\n title First\n title Last\n section A\n A: 3: Me\n section B\n B: 4\n"; var draft = new JourneyVisualDraft(source); Success(await draft.SetTitleAsync("New")); Assert.Contains("title Header\n title First\n title New", draft.Source);
        var section = draft.Model.Sections[1]; draft.Select(section.Identity); Success(await draft.DeleteAsync(section.Identity)); Assert.DoesNotContain("A: 3", draft.Source); draft.Undo(); Assert.Equal(section.Identity, draft.Selection); draft.Undo(); Assert.Equal(source, draft.Source);
    }
}
