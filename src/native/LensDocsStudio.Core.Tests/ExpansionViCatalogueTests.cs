using LensDocsStudio.Core.Content;
using LensDocsStudio.Core.PacketDiagrams;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Core.TreeViewDiagrams;
using LensDocsStudio.Core.TreemapDiagrams;
using LensDocsStudio.Core.VennDiagrams;

namespace LensDocsStudio.Core.Tests;

public class ExpansionViCatalogueTests
{
    [Theory, InlineData("packet"), InlineData("treeview"), InlineData("treemap"), InlineData("venn")]
    public async Task Native_catalogue_templates_reparse_and_render_through_the_actual_bounded_authoring_path(string family)
    {
        var entry = BuiltInContentCatalogue.Entries.Single(e => e.Id == "mermaid/" + family); Assert.False(entry.IsSnippet); Assert.Equal("mermaid", entry.Format);
        ISourceDraft draft = family switch { "packet" => new PacketVisualDraft(entry.ContentPattern), "treeview" => new TreeViewVisualDraft(entry.ContentPattern), "treemap" => new TreemapVisualDraft(entry.ContentPattern), _ => new VennVisualDraft(entry.ContentPattern) };
        var result = await draft.ValidateAsync(); Assert.True(result.Success, result.Error); Assert.Equal(entry.ContentPattern, draft.Source);
        Assert.Single(BuiltInContentCatalogue.Entries, e => e.Id == "mermaid-snippet/" + family + "-native");
    }
    [Fact]
    public async Task Set_reordering_across_independent_union_rows_retains_exact_references_and_comment_groups()
    {
        const string source = "venn-beta\nset A\nset B\n%% union Ω\nunion A,B\nset C\n"; var draft = new VennVisualDraft(source); var key = draft.Model.Sets[2].Identity;
        var result = await draft.MoveAsync(key, -1); Assert.True(result.Success, result.Error); Assert.Equal(new[] { "A", "C", "B" }, draft.Model.Sets.Select(s => s.Id)); Assert.Contains("%% union Ω\nunion A,B\n", draft.Source);
        draft.Undo(); Assert.Equal(source, draft.Source);
    }
}
