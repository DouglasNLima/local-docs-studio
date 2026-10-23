using LensDocsStudio.Core.TreeViewDiagrams;

namespace LensDocsStudio.Core.Tests;

public class TreeViewVisualDraftTests
{
    [Theory, InlineData("\n"), InlineData("\r\n")]
    public async Task Quoted_labels_directory_slashes_tabs_annotations_and_comments_keep_exact_untouched_bytes(string nl)
    {
        var source = string.Join(nl, "%% retained Ω", "treeView-beta", "project/", "\t\"source files\"/ :::highlight ## Local code", "\t\tfile.cs icon(none)", "");
        var draft = new TreeViewVisualDraft(source); var node = draft.Model.Nodes[1]; var result = await draft.UpdateAsync(node.Identity, "edited Ω", true, "Local code", "highlight", "default");
        Assert.True(result.Success, result.Error); Assert.Equal(source.Replace("source files", "edited Ω", StringComparison.Ordinal), draft.Source); Assert.Equal(node.Identity, draft.Model.Nodes[1].Identity);
        draft.Undo(); Assert.Equal(source, draft.Source); draft.Redo(); Assert.Contains("edited Ω", draft.Source); Assert.True((await draft.ValidateAsync()).Success);
    }
    [Fact]
    public async Task Box_mode_basic_fields_preserve_prefix_glyphs_and_structure_stays_protected()
    {
        const string source = "treeView\n├── source/\n│   ┗━━ leaf Ω\n└── other\n"; var draft = new TreeViewVisualDraft(source); Assert.True(draft.Model.BoxMode);
        var result = await draft.UpdateAsync(draft.Model.Nodes[1].Identity, "edited", false, "", "", "default"); Assert.True(result.Success, result.Error); Assert.Equal(source.Replace("leaf Ω", "edited", StringComparison.Ordinal), draft.Source);
        var current = draft.Source; result = await draft.MoveAsync(draft.Model.Nodes[0].Identity, 1); Assert.False(result.Success); Assert.Contains("Box-drawing", result.Error); Assert.Equal(current, draft.Source);
    }
    [Fact]
    public async Task First_body_line_controls_mode_and_later_box_glyphs_are_source_only()
    {
        var draft = new TreeViewVisualDraft("treeView\nroot/\n  └── literal\n"); Assert.False(draft.Model.BoxMode); Assert.False(draft.Model.Nodes[1].Proved);
        var result = await draft.UpdateAsync(draft.Model.Nodes[1].Identity, "Other", false, "", "", "default"); Assert.False(result.Success);
    }
    [Fact]
    public async Task Optional_annotations_add_edit_remove_without_bulk_reconstruction_or_custom_artwork()
    {
        var draft = new TreeViewVisualDraft("treeView\n\"Name\"/  \n"); var key = draft.Model.Nodes[0].Identity;
        var result = await draft.UpdateAsync(key, "Name", true, "Description Ω", "highlight", "hidden"); Assert.True(result.Success, result.Error);
        var node = draft.Model.Nodes.Single(); Assert.Equal("", node.Icon); Assert.Equal("Description Ω", node.DescriptionToken?.Value); Assert.Equal("highlight", node.ClassToken?.Value); Assert.StartsWith("treeView\n\"Name\"/", draft.Source);
        result = await draft.UpdateAsync(key, "Name", false, "", "", "default"); Assert.True(result.Success, result.Error); Assert.Null(draft.Model.Nodes.Single().Icon); Assert.False(draft.Model.Nodes.Single().Directory); Assert.DoesNotContain("icon(", draft.Source); Assert.DoesNotContain(":::" , draft.Source); Assert.DoesNotContain("##", draft.Source);
        var current = draft.Source; result = await draft.UpdateAsync(key, "Name", false, "", "", "server"); Assert.False(result.Success); Assert.Equal(current, draft.Source);
    }
    [Fact]
    public async Task Complete_subtree_add_remove_reorder_and_reparent_prove_all_other_memberships()
    {
        const string source = "treeView\nroot/\n    %% attached Ω\n    first/\n        leaf\n    second/\n"; var draft = new TreeViewVisualDraft(source); var root = draft.Model.Nodes[0].Identity; var first = draft.Model.Nodes[1].Identity; var leaf = draft.Model.Nodes[2].Identity; var second = draft.Model.Nodes[3].Identity;
        var result = await draft.MoveAsync(second, -1); Assert.True(result.Success, result.Error); Assert.Equal(leaf, draft.Model.Nodes[^1].Identity); Assert.Contains("    %% attached Ω\n    first/\n        leaf", draft.Source);
        result = await draft.ReparentAsync(first, second); Assert.True(result.Success, result.Error); Assert.Equal(second, draft.Model.Hierarchy.Single(r => r.Identity == first).Parent); Assert.Equal(first, draft.Model.Hierarchy.Single(r => r.Identity == leaf).Parent);
        result = await draft.ReparentAsync(first, root); Assert.True(result.Success, result.Error); Assert.Equal(root, draft.Model.Hierarchy.Single(r => r.Identity == first).Parent);
        result = await draft.AddAsync(first, "new Ω", false); Assert.True(result.Success, result.Error); Assert.Equal(first, draft.Model.Hierarchy[^1].Parent);
        result = await draft.DeleteAsync(first); Assert.True(result.Success, result.Error); Assert.DoesNotContain(draft.Model.Nodes, n => n.Identity == leaf); Assert.DoesNotContain("attached Ω", draft.Source);
    }
    [Theory, InlineData("\n"), InlineData("  %% uncertain\n")]
    public async Task Ambiguous_blank_or_comment_attachment_protects_subtree_structure(string trivia)
    {
        var source = "treeView\nroot/\n    child/\n" + trivia + "        leaf\n"; var draft = new TreeViewVisualDraft(source); var result = await draft.DeleteAsync(draft.Model.Nodes[1].Identity);
        Assert.False(result.Success); Assert.Equal(source, draft.Source); Assert.False(draft.CanUndo);
    }
    [Fact]
    public async Task Duplicate_labels_keep_occurrence_keys_and_only_selected_source_is_patched()
    {
        var draft = new TreeViewVisualDraft("treeView\nSame\nSame\n"); var key = draft.Model.Nodes[1].Identity; Assert.NotEqual(draft.Model.Nodes[0].Identity, key);
        var result = await draft.UpdateAsync(key, "Second", false, "", "", "default"); Assert.True(result.Success, result.Error); Assert.Equal("Same", draft.Model.Nodes[0].Label); Assert.Equal("Second", draft.Model.Nodes[1].Label);
    }
    [Theory, InlineData("slash/"), InlineData("icon(server)"), InlineData("## hidden"), InlineData(":::highlight"), InlineData("\"quoted\""), InlineData("└── branch")]
    public async Task Unproved_field_grammar_is_rejected_without_erasing_pending_source(string label)
    {
        const string source = "treeView\nfile\n"; var draft = new TreeViewVisualDraft(source); var result = await draft.UpdateAsync(draft.Model.Nodes[0].Identity, label, false, "", "", "default"); Assert.False(result.Success); Assert.Equal(source, draft.Source); Assert.False(draft.CanUndo);
    }
}
