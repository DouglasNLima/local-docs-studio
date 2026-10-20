using LensDocsStudio.Core.GanttDiagrams;
using LensDocsStudio.Core.SourceAuthoring;

namespace LensDocsStudio.Core.Tests;

public class GanttVisualTests
{
    private static void Success(SourceAuthoringResult result) => Assert.True(result.Success, result.Error);
    [Theory, InlineData("\n"), InlineData("\r\n")]
    public async Task Exact_source_IDs_comments_Unicode_and_no_op_survive_local_task_changes(string newline)
    {
        var source = string.Join(newline, "%% preserved Ω", "gantt title Café", "  dateFormat YYYY-MM-DD", "", "  section Guides", "  %% task attachment", "  Draft 😀 : original_id, 2026-10-20, 0003d", "  Review : 2026-10-25, 2d", "");
        var draft = new GanttVisualDraft(source); var task = draft.Model.Tasks[0]; draft.Select(task.Identity); Success(await draft.ValidateAsync());
        Success(await draft.UpdateAsync(task.Identity, task.Label, task.Start, task.Duration)); Assert.False(draft.CanUndo); Assert.Equal(source, draft.Source);
        Success(await draft.UpdateAsync(task.Identity, "Updated 😀", "2026-10-21", "0003"));
        Assert.Equal(source.Replace("Draft 😀", "Updated 😀", StringComparison.Ordinal).Replace("2026-10-20", "2026-10-21", StringComparison.Ordinal), draft.Source); Assert.Equal("original_id", draft.Model.Tasks[0].Id);
        draft.Undo(); Assert.Equal(source, draft.Source); Assert.Equal(task.Identity, draft.Selection); draft.Redo(); Assert.Equal(task.Identity, draft.Selection);
    }
    [Theory, InlineData("2026-02-30", "1"), InlineData("2026-13-01", "1"), InlineData("2026-2-01", "1"), InlineData("", "1"), InlineData("2026-10-20", ""), InlineData("2026-10-20", "0"), InlineData("2026-10-20", "-1"), InlineData("2026-10-20", "1.5"), InlineData("2026-10-20", "73001")]
    public async Task Invalid_intermediate_or_impossible_temporal_input_keeps_source_selection_and_history(string date, string duration)
    {
        var draft = new GanttVisualDraft(GanttVisualDraft.Starter); var task = draft.Model.Tasks[0]; draft.Select(task.Identity);
        Assert.False((await draft.UpdateAsync(task.Identity, task.Label, date, duration)).Success); Assert.Equal(GanttVisualDraft.Starter, draft.Source); Assert.Equal(task.Identity, draft.Selection); Assert.False(draft.CanUndo);
        Success(await draft.UpdateAsync(task.Identity, task.Label, "2028-02-29", "1"));
    }
    [Theory, InlineData("Follow : after a, 2d"), InlineData("Implicit : 2d"), InlineData("Milestone : milestone, m, after a, 0d"), InlineData("Status : done, b, 2026-10-21, 1d")]
    public async Task Dependencies_and_implicit_schedule_protect_dates_removal_insertion_and_movement_but_allow_labels(string advanced)
    {
        var source = "gantt\n  section Plan\n  First : a, 2026-10-20, 2d\n  " + advanced + "\n"; var draft = new GanttVisualDraft(source); var first = draft.Model.Tasks[0]; var other = draft.Model.Tasks[1];
        Success(await draft.ValidateAsync()); Assert.False((await draft.DeleteAsync(first.Identity)).Success); Assert.False((await draft.AddTaskAsync(first.Section)).Success); Assert.False((await draft.MoveAsync(first.Identity, 1)).Success);
        Assert.False((await draft.UpdateAsync(first.Identity, first.Label, "2026-10-21", "2")).Success); Success(await draft.UpdateAsync(other.Identity, "Updated", other.Start, other.Duration));
        Assert.Equal(source.Replace(other.Label, "Updated", StringComparison.Ordinal), draft.Source);
    }
    [Theory, InlineData("dateFormat DD/MM/YYYY", "20/10/2026"), InlineData("excludes weekends", "2026-10-20"), InlineData("axisFormat %d", "2026-10-20"), InlineData("unknown retained", "2026-10-20")]
    public async Task Incompatible_formats_calendars_and_unknown_directives_protect_temporal_operations(string directive, string date)
    {
        var source = "gantt\n  " + directive + "\n  First : a, " + date + ", 2d\n"; var draft = new GanttVisualDraft(source); var task = draft.Model.Tasks[0];
        Assert.NotNull(GanttVisualDraft.TemporalReason(draft.Model, task)); Success(await draft.UpdateAsync(task.Identity, "Safe label", task.Start, task.Duration));
        Assert.Equal(source.Replace("First", "Safe label", StringComparison.Ordinal), draft.Source); Assert.False((await draft.DeleteAsync(task.Identity)).Success);
    }
    [Fact] public async Task Task_and_section_groups_move_with_attached_comments_and_precise_selection_history()
    {
        var source = "gantt\n  dateFormat YYYY-MM-DD\n  %% section A\n  section A\n  %% task one\n  Same : a, 2026-10-20, 1d\n  %% task two\n  Same : b, 2026-10-21, 1d\n  %% section B\n  section B\n  Other : c, 2026-10-22, 1d\n";
        var draft = new GanttVisualDraft(source); var second = draft.Model.Tasks[1]; draft.Select(second.Identity); Success(await draft.MoveAsync(second.Identity, -1));
        Assert.Contains("%% task two\n  Same : b, 2026-10-21, 1d\n  %% task one\n  Same : a", draft.Source); Assert.Equal(second.Identity, draft.Model.Tasks[0].Identity);
        draft.Undo(); Assert.Equal(source, draft.Source); Assert.Equal(second.Identity, draft.Selection);
        var section = draft.Model.Sections[1]; draft.Select(section.Identity); Success(await draft.MoveAsync(section.Identity, -1)); Assert.Contains("%% section B\n  section B\n  Other", draft.Source); Assert.Equal(section.Identity, draft.Model.Sections[0].Identity);
        draft.Undo(); Assert.Equal(source, draft.Source); draft.Redo(); Assert.Equal(section.Identity, draft.Selection);
    }
    [Fact] public async Task Section_delete_cannot_discard_intervening_directives_and_task_move_cannot_cross_them()
    {
        var source = "gantt\n  section A\n  First : 2026-10-20, 1d\n  dateFormat YYYY-MM-DD\n  Second : 2026-10-21, 1d\n"; var draft = new GanttVisualDraft(source);
        Assert.False((await draft.MoveAsync(draft.Model.Tasks[0].Identity, 1)).Success); Assert.False((await draft.DeleteAsync(draft.Model.Sections[0].Identity)).Success); Assert.Equal(source, draft.Source);
        Success(await draft.SetSectionAsync(draft.Model.Sections[0].Identity, "Changed")); Assert.Contains("dateFormat YYYY-MM-DD", draft.Source);
    }
    [Fact] public async Task Simple_add_remove_title_and_section_changes_remain_source_span_edits()
    {
        var source = "gantt\n  Task : original, 2026-10-20, 1d\n"; var draft = new GanttVisualDraft(source); Success(await draft.SetTitleAsync("Delivery Ω"));
        Success(await draft.AddSectionAsync()); var section = draft.Model.Sections.Single(); Success(await draft.SetSectionAsync(section.Identity, "Review")); Success(await draft.AddTaskAsync(section.Identity));
        Assert.Equal("original", draft.Model.Tasks[0].Id); Assert.Null(draft.Model.Tasks[1].Id); Success(await draft.DeleteAsync(section.Identity)); Assert.Single(draft.Model.Tasks);
        draft.Undo(); Assert.Equal(2, draft.Model.Tasks.Count); Assert.Single(draft.Model.Sections);
    }
    [Fact] public async Task Malformed_explicit_dates_are_invalid_until_repaired_in_the_draft()
    {
        var draft = new GanttVisualDraft("gantt\n  First : a, 2026-02-30, 1d\n"); Assert.False((await draft.ValidateAsync()).Success); var task = draft.Model.Tasks[0];
        Success(await draft.UpdateAsync(task.Identity, task.Label, "2026-02-28", "1"));
    }
    [Fact] public async Task An_unreferenced_explicit_suffix_task_can_change_dates_without_rewriting_earlier_dependencies()
    {
        var source = "gantt\n  First : a, 2026-10-20, 1d\n  Dependent : after a, 1d\n  Independent : free, 2026-10-23, 2d\n"; var draft = new GanttVisualDraft(source); var task = draft.Model.Tasks[^1];
        Assert.Null(GanttVisualDraft.TemporalReason(draft.Model, task));
        Success(await draft.UpdateAsync(task.Identity, task.Label, "2026-10-24", "3")); Assert.Equal(source.Replace("2026-10-23, 2d", "2026-10-24, 3d", StringComparison.Ordinal), draft.Source);
        Assert.False((await draft.DeleteAsync(draft.Model.Tasks[0].Identity)).Success);
    }
    [Theory, InlineData("YYYY-MM-DD"), InlineData("yyyy-MM-dd")]
    public async Task Compatible_dateFormat_tokens_remain_unchanged_and_use_the_same_pinned_dates(string format)
    {
        var source = "gantt\n  dateFormat " + format + "\n  First : a, 2026-10-20, 1d\n"; var draft = new GanttVisualDraft(source); var task = draft.Model.Tasks[0];
        Success(await draft.UpdateAsync(task.Identity, task.Label, "2026-10-21", "2")); Assert.Contains("dateFormat " + format, draft.Source);
        Assert.Equal(new LensDocsStudio.Core.Rendering.DiagramService().Render(draft.Source.Replace(format, "YYYY-MM-DD", StringComparison.Ordinal)).Svg, new LensDocsStudio.Core.Rendering.DiagramService().Render(draft.Source).Svg);
    }
}
