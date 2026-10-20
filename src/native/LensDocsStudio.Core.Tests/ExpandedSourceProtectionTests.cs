using LensDocsStudio.Core.GanttDiagrams;
using LensDocsStudio.Core.PieDiagrams;
using LensDocsStudio.Core.Mindmaps;

namespace LensDocsStudio.Core.Tests;

public class ExpandedSourceProtectionTests
{
    [Fact] public async Task Pinned_initialisation_directives_remain_exact_while_safe_local_labels_are_editable()
    {
        const string prefix = "%% retained\r\n%%{init: {'theme': 'base'}}%%\r\n";
        var pie = new PieVisualDraft(prefix + PieVisualDraft.Starter.ReplaceLineEndings("\r\n")); var entry = pie.Model.Entries[0]; Assert.True((await pie.UpdateAsync(entry.Identity, "Updated", entry.Number)).Success); Assert.StartsWith(prefix, pie.Source);
        var gantt = new GanttVisualDraft(prefix + GanttVisualDraft.Starter.ReplaceLineEndings("\r\n")); var task = gantt.Model.Tasks[0]; Assert.True((await gantt.UpdateAsync(task.Identity, "Updated", task.Start, task.Duration)).Success); Assert.StartsWith(prefix, gantt.Source); Assert.NotNull(GanttVisualDraft.TemporalReason(gantt.Model, task));
        var mindmap = new MindmapVisualDraft(prefix + MindmapVisualDraft.Starter.ReplaceLineEndings("\r\n")); Assert.True((await mindmap.UpdateAsync(mindmap.Model.Nodes[1].Identity, "Updated")).Success); Assert.StartsWith(prefix, mindmap.Source);
    }
    [Fact] public async Task Ambiguously_trailing_comments_protect_flat_row_removal_without_losing_bytes()
    {
        var pie = new PieVisualDraft("pie\n  \"A\" : 1\n%% trailing or next?\n\n  \"B\" : 2\n"); var original = pie.Source;
        Assert.False((await pie.DeleteAsync(pie.Model.Entries[0].Identity)).Success); Assert.Equal(original, pie.Source);
        var gantt = new GanttVisualDraft("gantt\n  A : 2026-10-20, 1d\n%% trailing or next?\n\n  B : 2026-10-21, 1d\n"); original = gantt.Source;
        Assert.False((await gantt.DeleteAsync(gantt.Model.Tasks[0].Identity)).Success); Assert.Equal(original, gantt.Source);
    }
    [Fact] public async Task Stale_and_cancelled_validation_cannot_commit_queued_changes_or_leak_other_drafts()
    {
        var pie = new PieVisualDraft(PieVisualDraft.Starter); var independent = new PieVisualDraft(PieVisualDraft.Starter); var started = new TaskCompletionSource(); var release = new TaskCompletionSource();
        pie.ValidatePresentation = async (_, _, _) => { started.SetResult(); await release.Task; };
        var row = pie.Model.Entries[0]; pie.Select(row.Identity); var operation = pie.UpdateAsync(row.Identity, "Queued", row.Number); await started.Task;
        pie.Undo(); release.SetResult(); Assert.False((await operation).Success); Assert.Equal(PieVisualDraft.Starter, pie.Source); Assert.Equal(PieVisualDraft.Starter, independent.Source); Assert.False(independent.CanUndo);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var mindmap = new MindmapVisualDraft(MindmapVisualDraft.Starter); Assert.False((await mindmap.UpdateAsync(mindmap.Model.Nodes[0].Identity, "Cancelled", cancelled.Token)).Success); Assert.Equal(MindmapVisualDraft.Starter, mindmap.Source);
    }
    [Fact] public async Task Identical_Pie_row_movement_keeps_occurrence_history_even_when_source_bytes_match()
    {
        var source = "pie\n  \"Same\" : 1\n  \"Same\" : 1\n"; var pie = new PieVisualDraft(source); var row = pie.Model.Entries[1]; pie.Select(row.Identity);
        Assert.True((await pie.MoveAsync(row.Identity, -1)).Success); Assert.Equal(source, pie.Source); Assert.Equal(row.Identity, pie.Model.Entries[0].Identity); Assert.True(pie.CanUndo);
        pie.Undo(); Assert.Equal(row.Identity, pie.Model.Entries[1].Identity); pie.Redo(); Assert.Equal(row.Identity, pie.Model.Entries[0].Identity);
    }
}
