using LensDocsStudio.Core.PacketDiagrams;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Core.TreemapDiagrams;
using LensDocsStudio.Core.TreeViewDiagrams;
using LensDocsStudio.Core.VennDiagrams;

namespace LensDocsStudio.Core.Tests;

public class ExpansionViPendingSourceTests
{
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
