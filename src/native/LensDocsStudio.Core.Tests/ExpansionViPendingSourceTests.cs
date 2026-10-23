using LensDocsStudio.Core.PacketDiagrams;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Core.TreemapDiagrams;
using LensDocsStudio.Core.TreeViewDiagrams;
using LensDocsStudio.Core.VennDiagrams;

namespace LensDocsStudio.Core.Tests;

public class ExpansionViPendingSourceTests
{
    [Theory, InlineData("packet"), InlineData("treeview"), InlineData("treemap"), InlineData("venn")]
    public async Task Front_matter_init_directive_comments_and_CRLF_remain_exact_through_native_scalar_patches(string family)
    {
        const string prefix = "---\r\ntitle: Preserved metadata Ω\r\n---\r\n%%{init: {\"theme\":\"dark\"}}%%\r\n%% exact retained comment\r\n";
        string original; string changed;
        if (family == "packet") { var draft = new PacketVisualDraft(prefix + "packet title Existing\r\n000-007: \"Before\"\r\n"); original = draft.Source; var result = await draft.UpdateFieldAsync(draft.Model.Fields[0].Identity, "After Ω", "000", "007"); Assert.True(result.Success, result.Error); changed = draft.Source; }
        else if (family == "treeview") { var draft = new TreeViewVisualDraft(prefix + "treeView\r\n\t\"Before\"/\r\n"); original = draft.Source; var result = await draft.UpdateAsync(draft.Model.Nodes[0].Identity, "After Ω", true, "", "", "default"); Assert.True(result.Success, result.Error); changed = draft.Source; }
        else if (family == "treemap") { var draft = new TreemapVisualDraft(prefix + "treemap-beta\r\n\t\"Before\": 000.50\r\n"); original = draft.Source; var result = await draft.UpdateAsync(draft.Model.Nodes[0].Identity, "After Ω", "000.50"); Assert.True(result.Success, result.Error); changed = draft.Source; }
        else { var draft = new VennVisualDraft(prefix + "venn-beta\r\nset A[\"Before\"]: 000.50\r\n"); original = draft.Source; var result = await draft.UpdateSetAsync(draft.Model.Sets[0].Identity, "A", "After Ω", "000.50"); Assert.True(result.Success, result.Error); changed = draft.Source; }
        Assert.Equal(original.Replace("Before", "After Ω", StringComparison.Ordinal), changed); Assert.StartsWith(prefix, changed);
    }
    [Theory, InlineData("packet"), InlineData("treeview"), InlineData("treemap"), InlineData("venn")]
    public async Task Malformed_or_unproved_pending_source_keeps_the_last_valid_draft_and_history(string family)
    {
        ISourceDraft draft = family switch { "packet" => new PacketVisualDraft("packet\n0: \"A\"\n"), "treeview" => new TreeViewVisualDraft("treeView\nfile\n"), "treemap" => new TreemapVisualDraft("treemap-beta\n\"A\": 1\n"), _ => new VennVisualDraft("venn-beta\nset A\nset B\n") };
        var original = draft.Source; var pending = original + (family == "packet" ? "0-: \"Pending\"\n" : family == "treeview" ? "\"Unclosed\n" : family == "treemap" ? "\"Pending\": 1.\n" : "union A, missing\n");
        var result = await draft.ValidateSourceAsync(pending); Assert.False(result.Success); Assert.Contains("Pending source", result.Error); Assert.Equal(original, draft.Source); Assert.False(draft.CanUndo);
    }
    [Fact]
    public async Task Manual_source_validation_preserves_untouched_occurrence_keys_without_matching_duplicate_labels()
    {
        var draft = new PacketVisualDraft("packet\n0: \"Same\"\n1: \"Same\"\n2: \"Tail\"\n"); var keys = draft.Model.Fields.Select(f => f.Identity).ToArray();
        var result = await draft.ValidateSourceAsync(draft.Source.Replace("1: \"Same\"", "1: \"Changed\"", StringComparison.Ordinal)); Assert.True(result.Success, result.Error);
        Assert.Equal(keys[0], draft.Model.Fields[0].Identity); Assert.NotEqual(keys[1], draft.Model.Fields[1].Identity); Assert.Equal(keys[2], draft.Model.Fields[2].Identity);
        draft.Undo(); Assert.Equal(keys, draft.Model.Fields.Select(f => f.Identity));
    }
}
