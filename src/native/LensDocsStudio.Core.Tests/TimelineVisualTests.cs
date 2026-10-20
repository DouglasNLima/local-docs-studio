using LensDocsStudio.Core.TimelineDiagrams;
using LensDocsStudio.Core.SourceAuthoring;

namespace LensDocsStudio.Core.Tests;

public class TimelineVisualTests
{
    private static void Success(SourceAuthoringResult result) => Assert.True(result.Success, result.Error);
    [Theory, InlineData("\n"), InlineData("\r\n")]
    public async Task No_op_and_one_event_edit_preserve_siblings_continuations_trivia_and_unicode(string nl)
    {
        var source = string.Join(nl, "%% café 😀", "timeline title Header", " title Last", " section Empty", " section Named", " Same : First : Second", "  : Third : Fourth", "", " Same : Other", "");
        var draft = new TimelineVisualDraft(source); var p = draft.Model.Periods[0]; var e = p.Events[2]; draft.Select(e.Identity);
        Success(await draft.ValidateAsync()); Success(await draft.UpdateAsync(e.Identity, e.Label)); Assert.Equal(source, draft.Source); Assert.False(draft.CanUndo);
        Success(await draft.UpdateAsync(e.Identity, "Edited Ω")); Assert.Equal(source.Replace("Third", "Edited Ω", StringComparison.Ordinal), draft.Source); Assert.Equal(e.Identity, draft.Selection);
        draft.Undo(); Assert.Equal(source, draft.Source); Assert.Equal(e.Identity, draft.Selection); draft.Redo(); Assert.Contains("Edited Ω", draft.Source); Assert.Equal(3, draft.Model.Sections.Count);
    }
    [Fact] public async Task Period_moves_carry_continuations_and_proved_leading_comments_with_exact_undo()
    {
        var source = "timeline\n  %% first\n  Same : A\n    : B : C\n  %% second\n  Same : D\n    : E\n";
        var draft = new TimelineVisualDraft(source); var p = draft.Model.Periods[1]; draft.Select(p.Identity); Success(await draft.MoveAsync(p.Identity, -1));
        Assert.Equal("timeline\n  %% second\n  Same : D\n    : E\n  %% first\n  Same : A\n    : B : C\n", draft.Source); Assert.Equal(p.Identity, draft.Model.Periods[0].Identity);
        draft.Undo(); Assert.Equal(source, draft.Source); Assert.Equal(p.Identity, draft.Selection); draft.Redo(); Assert.Equal(p.Identity, draft.Model.Periods[0].Identity);
    }
    [Fact] public async Task Event_reordering_between_first_line_and_continuation_keeps_source_style_and_selection()
    {
        var source = "timeline\n  Label : First : Second\n    : Third : Fourth\n"; var draft = new TimelineVisualDraft(source); var e = draft.Model.Periods[0].Events[2]; draft.Select(e.Identity);
        Success(await draft.MoveAsync(e.Identity, -1)); Assert.Equal("timeline\n  Label : First : Third\n    : Second : Fourth\n", draft.Source); Assert.Equal("Third", draft.Model.Periods[0].Events.Single(x => x.Identity == draft.Selection).Label);
        draft.Undo(); Assert.Equal(source, draft.Source); Assert.Equal(e.Identity, draft.Selection);
    }
    [Fact] public async Task Empty_sections_and_unsectioned_periods_survive_group_moves_and_destination_moves()
    {
        var source = "timeline title Header\n U : Unsectioned\n section Empty\n section A\n A : One\n  : Two\n section B\n B : Three\n"; var draft = new TimelineVisualDraft(source); var p = draft.Model.Periods[1]; draft.Select(p.Identity);
        Success(await draft.MoveToSectionAsync(p.Identity, draft.Model.Sections[1].Identity)); Assert.Equal(draft.Model.Sections[1].Identity, draft.Model.Periods.Single(x => x.Identity == p.Identity).Section); Assert.Contains(" section A\n", draft.Source);
        Success(await draft.MoveAsync(draft.Model.Sections[2].Identity, 1)); Assert.Contains(" section B\n B : Three\n section A\n", draft.Source);
        Success(await draft.MoveToSectionAsync(p.Identity, draft.Model.Sections[0].Identity)); Assert.Equal(draft.Model.Sections[0].Identity, draft.Model.Periods.Single(x => x.Identity == p.Identity).Section);
    }
    [Fact] public async Task Section_removal_carries_its_complete_period_event_source_group()
    {
        var source = "timeline\n section A\n A : One : Two\n  : Three\n section B\n B : Four\n"; var draft = new TimelineVisualDraft(source); var s = draft.Model.Sections[1]; draft.Select(s.Identity);
        Success(await draft.DeleteAsync(s.Identity)); Assert.Equal("timeline\n section B\n B : Four\n", draft.Source); draft.Undo(); Assert.Equal(source, draft.Source); Assert.Equal(s.Identity, draft.Selection);
    }
    [Fact] public async Task Event_add_remove_preserves_first_line_distinction_and_empty_section_source()
    {
        var source = "timeline\n section Empty\n section A\n A : One : Two\n  : Three\n"; var draft = new TimelineVisualDraft(source); var p = draft.Model.Periods[0];
        Success(await draft.DeleteAsync(p.Events[0].Identity)); Assert.Contains(" A :  Two\n", draft.Source); p = draft.Model.Periods[0]; Success(await draft.DeleteAsync(p.Events[^1].Identity)); Assert.DoesNotContain(": Three", draft.Source);
        Success(await draft.AddEventAsync(p.Identity)); Assert.Equal(2, draft.Model.Periods[0].Events.Count); Success(await draft.AddPeriodAsync(draft.Model.Sections[1].Identity)); Assert.Equal(2, draft.Model.Periods.Count);
    }
    [Theory, InlineData("A:B"), InlineData("A\nB"), InlineData("A\rB"), InlineData("")]
    public async Task Delimiters_and_line_breaks_are_rejected_with_valid_draft_retained(string value)
    {
        var draft = new TimelineVisualDraft(TimelineVisualDraft.Starter); var p = draft.Model.Periods[0]; Assert.False((await draft.UpdateAsync(p.Events[0].Identity, value)).Success); Assert.Equal(TimelineVisualDraft.Starter, draft.Source);
    }
    [Fact] public async Task Mixed_unknown_orphan_and_trailing_comments_protect_only_affected_structural_operations()
    {
        var source = "timeline\n : orphan\n A : One\n ignored preserved\n  : Two\n B : Three\n %% ambiguous trailing\n\n"; var draft = new TimelineVisualDraft(source); var p = draft.Model.Periods[0];
        Success(await draft.ValidateAsync()); Assert.False((await draft.MoveAsync(p.Identity, 1)).Success); Success(await draft.UpdateAsync(p.Events[1].Identity, "Safe")); Assert.Contains(" ignored preserved\n", draft.Source);
        Assert.False((await draft.DeleteAsync(draft.Model.Periods[1].Identity)).Success);
    }
    [Theory, InlineData("title Hidden"), InlineData("section Hidden"), InlineData("%% Hidden")]
    public async Task Renaming_a_period_cannot_silently_change_it_into_a_directive(string value)
    { var draft = new TimelineVisualDraft(TimelineVisualDraft.Starter); Assert.False((await draft.UpdateAsync(draft.Model.Periods[0].Identity, value)).Success); Assert.Equal(TimelineVisualDraft.Starter, draft.Source); }
    [Fact] public async Task Header_and_repeated_body_title_follow_pinned_precedence_without_rewriting_earlier_declarations()
    {
        var source = "timeline showData title Header\n title First\n A : Event\n title Last\n"; var draft = new TimelineVisualDraft(source); Assert.Equal("Last", draft.Model.Title!.Value);
        Success(await draft.SetTitleAsync("Effective Ω")); Assert.Equal(source.Replace("title Last", "title Effective Ω", StringComparison.Ordinal), draft.Source);
    }
    [Fact] public async Task Removing_the_only_first_line_event_promotes_one_continuation_without_losing_its_siblings()
    {
        var source = "timeline\n  Period : First\n    : Second : Third\n    : Fourth\n"; var draft = new TimelineVisualDraft(source);
        Success(await draft.DeleteAsync(draft.Model.Periods[0].Events[0].Identity)); Assert.Equal("timeline\n  Period : Second\n    :  Third\n    : Fourth\n", draft.Source);
        draft.Undo(); Assert.Equal(source, draft.Source);
    }
}
