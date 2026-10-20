using LensDocsStudio.Core.Mindmaps;
using LensDocsStudio.Core.SourceAuthoring;

namespace LensDocsStudio.Core.Tests;

public class MindmapVisualTests
{
    private static void Success(SourceAuthoringResult result) => Assert.True(result.Success, result.Error);
    [Theory, InlineData("\n", "  "), InlineData("\r\n", "\t")]
    public async Task Shaped_labels_keep_IDs_delimiters_comments_and_indentation_exactly(string newline, string unit)
    {
        var source = string.Join(newline, "%% retained Ω", "mindmap", unit + "root((Café))", unit + unit + "id[Same]", unit + unit + unit + "Same", unit + unit + "Same", "");
        var draft = new MindmapVisualDraft(source); Success(await draft.ValidateAsync()); var selected = draft.Model.Nodes[1]; draft.Select(selected.Identity);
        Success(await draft.UpdateAsync(selected.Identity, "Same")); Assert.False(draft.CanUndo); Assert.Equal(source, draft.Source);
        Success(await draft.UpdateAsync(selected.Identity, "変更 😀")); Assert.Equal(source.Replace("id[Same]", "id[変更 😀]", StringComparison.Ordinal), draft.Source); Assert.Equal(selected.Identity, draft.Selection);
        draft.Undo(); Assert.Equal(source, draft.Source); Assert.Equal(selected.Identity, draft.Selection); draft.Redo(); Assert.Contains("id[変更 😀]", draft.Source);
    }
    [Fact] public async Task Duplicate_subtrees_move_atomically_with_precise_undo_and_source_occurrence_selection()
    {
        var source = "mindmap\n  Root\n    %% first branch\n    Same\n      One\n    %% second branch\n    Same\n      Two\n";
        var draft = new MindmapVisualDraft(source); var second = draft.Model.Nodes[3]; draft.Select(second.Identity);
        Success(await draft.MoveAsync(second.Identity, -1)); Assert.Equal("mindmap\n  Root\n    %% second branch\n    Same\n      Two\n    %% first branch\n    Same\n      One\n", draft.Source);
        Assert.Equal(second.Identity, draft.Model.Nodes[1].Identity); draft.Undo(); Assert.Equal(source, draft.Source); Assert.Equal(second.Identity, draft.Selection); draft.Redo();
        Success(await draft.UpdateAsync(second.Identity, "Moved")); Assert.Equal("Moved", draft.Model.Nodes[1].Label); Assert.Equal("Same", draft.Model.Nodes[3].Label);
    }
    [Fact] public async Task Identical_source_subtrees_still_have_undoable_occurrence_movement()
    {
        var source = "mindmap\n  Root\n    Same\n    Same\n"; var draft = new MindmapVisualDraft(source); var second = draft.Model.Nodes[2]; draft.Select(second.Identity);
        Success(await draft.MoveAsync(second.Identity, -1)); Assert.Equal(source, draft.Source); Assert.True(draft.CanUndo); Assert.Equal(second.Identity, draft.Model.Nodes[1].Identity);
        draft.Undo(); Assert.Equal(second.Identity, draft.Model.Nodes[2].Identity); draft.Redo(); Assert.Equal(second.Identity, draft.Model.Nodes[1].Identity);
    }
    [Fact] public async Task Reparent_indent_outdent_preserve_descendants_and_prohibit_root_or_cycles()
    {
        var source = "mindmap\n  Root\n    A\n      Child\n    B\n"; var draft = new MindmapVisualDraft(source);
        var root = draft.Model.Nodes[0]; var a = draft.Model.Nodes[1]; var child = draft.Model.Nodes[2]; var b = draft.Model.Nodes[3]; draft.Select(b.Identity);
        Assert.False((await draft.ReparentAsync(root.Identity, b.Identity)).Success); Assert.False((await draft.ReparentAsync(a.Identity, child.Identity)).Success); Assert.False((await draft.DeleteAsync(root.Identity)).Success);
        Success(await draft.IndentAsync(b.Identity, false)); Assert.Equal(a.Identity, draft.Model.Nodes.Single(node => node.Identity == b.Identity).Parent);
        Success(await draft.IndentAsync(b.Identity, true)); Assert.Equal(source, draft.Source); Assert.Equal(b.Identity, draft.Selection);
        Success(await draft.ReparentAsync(a.Identity, b.Identity)); Assert.Equal(b.Identity, draft.Model.Nodes.Single(node => node.Identity == a.Identity).Parent); Assert.Equal(a.Identity, draft.Model.Nodes.Single(node => node.Identity == child.Identity).Parent);
        draft.Undo(); Assert.Equal(source, draft.Source); draft.Redo(); Success(await draft.DeleteAsync(a.Identity)); Assert.DoesNotContain(draft.Model.Nodes, node => node.Identity == a.Identity || node.Identity == child.Identity);
    }
    [Theory, InlineData("root((Root))"), InlineData("root[Root]"), InlineData("root(Root)"), InlineData("root{{Root}}"), InlineData("root))Root(("), InlineData("root)Root(")]
    public async Task Recognised_shapes_preserve_existing_ID_and_shape_when_editing_inner_text(string form)
    {
        var source = "mindmap\n  " + form + "\n    Child\n"; var draft = new MindmapVisualDraft(source);
        Success(await draft.UpdateAsync(draft.Model.Nodes[0].Identity, "Updated")); Assert.Equal(source.Replace("Root", "Updated", StringComparison.Ordinal), draft.Source);
    }
    [Fact] public async Task Metadata_and_ambiguous_comments_protect_local_structure_without_blocking_safe_labels()
    {
        var source = "mindmap\n  Root\n    A\n      ::icon(fa fa-book)\n    B\n"; var draft = new MindmapVisualDraft(source); var a = draft.Model.Nodes[1]; var b = draft.Model.Nodes[^1];
        Success(await draft.UpdateAsync(b.Identity, "Safe")); Assert.Contains("::icon(fa fa-book)", draft.Source); Assert.False((await draft.DeleteAsync(a.Identity)).Success); Assert.False((await draft.MoveAsync(a.Identity, 1)).Success);
        var comment = new MindmapVisualDraft("mindmap\n  Root\n    A\n  %% ambiguous trailing comment\n    B\n"); Assert.False((await comment.DeleteAsync(comment.Model.Nodes[1].Identity)).Success);
    }
    [Fact] public async Task Mixed_indentation_protects_reparenting_and_does_not_normalise_any_bytes()
    {
        var source = "mindmap\n\tRoot\n\t A\n\t\tB\n"; var draft = new MindmapVisualDraft(source); var original = draft.Source;
        Assert.False((await draft.AddAsync(draft.Model.Nodes[1].Identity, true)).Success); Assert.Equal(original, draft.Source);
        Success(await draft.UpdateAsync(draft.Model.Nodes[0].Identity, "Updated")); Assert.Equal(source.Replace("Root", "Updated", StringComparison.Ordinal), draft.Source);
    }
    [Fact] public async Task A_second_root_is_detected_before_the_engine_can_silently_omit_it()
    {
        var source = "mindmap\n  One\n    Child\n  Two\n    Missing\n"; var draft = new MindmapVisualDraft(source);
        Assert.False((await draft.ValidateAsync()).Success); Assert.False((await draft.UpdateAsync(draft.Model.Nodes[0].Identity, "Changed")).Success); Assert.Equal(source, draft.Source);
        Success(await draft.ValidateSourceAsync(source.Replace("  Two\n    Missing", "    Two\n      Missing", StringComparison.Ordinal)));
    }
    [Fact] public async Task Child_and_sibling_insertion_keep_the_single_root_and_can_be_undone_exactly()
    {
        var draft = new MindmapVisualDraft("mindmap\n  Root\n"); var root = draft.Model.Nodes[0];
        Assert.False((await draft.AddAsync(root.Identity, false)).Success); Success(await draft.AddAsync(root.Identity, true, "A")); var a = draft.Model.Nodes[1];
        Success(await draft.AddAsync(a.Identity, false, "B")); Assert.Equal("mindmap\n  Root\n    A\n    B\n", draft.Source); draft.Undo(); Assert.Equal("mindmap\n  Root\n    A\n", draft.Source); draft.Undo(); Assert.Equal("mindmap\n  Root\n", draft.Source);
    }
    [Fact] public async Task Canonical_wrapping_is_verified_without_using_labels_as_node_identity()
    {
        var source = "mindmap\n  Root\n    This is a long branch label that is wrapped by the canonical renderer\n      Leaf\n    This is a long branch label that is wrapped by the canonical renderer\n";
        var draft = new MindmapVisualDraft(source); Success(await draft.ValidateAsync()); Assert.Equal(4, draft.Model.Nodes.Select(node => node.Identity).Distinct().Count());
    }
}
