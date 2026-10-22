using LensDocsStudio.Core.ArchitectureDiagrams;
using LensDocsStudio.Core.SourceAuthoring;

namespace LensDocsStudio.Core.Tests;

public class ArchitectureSourceAuthoringTests
{
    private static void Pass(SourceAuthoringResult result) => Assert.True(result.Success, result.Error);
    [Theory, InlineData("\n"), InlineData("\r\n")]
    public async Task Unique_ID_rename_patches_all_membership_and_connection_references_and_keeps_neighbours(string newline)
    {
        var source = ("%% untouched Ω\ngroup placeholder\n").Replace("group placeholder\n", ArchitectureVisualDraft.Starter, StringComparison.Ordinal).Replace("\n", newline, StringComparison.Ordinal);
        var draft = new ArchitectureVisualDraft(source); var group = draft.Model.Nodes[0]; var keys = draft.Text.Lines.Select(l => l.Key).ToArray();
        Pass(await draft.UpdateNodeAsync(group.Identity, "renamed", group.Label, group.Icon, group.Parent));
        Assert.Equal(source.Replace("group platform", "group renamed", StringComparison.Ordinal).Replace(" in platform", " in renamed", StringComparison.Ordinal), draft.Source);
        Assert.Equal(keys, draft.Text.Lines.Select(l => l.Key)); draft.Undo(); Assert.Equal(source, draft.Source); draft.Redo(); Assert.Contains("group renamed", draft.Source);
    }
    [Fact]
    public async Task Labels_icons_ports_and_arrowheads_are_exact_independent_token_patches()
    {
        var draft = new ArchitectureVisualDraft("architecture-beta\n service a[Same]  \n service b(server)[Same]\n a:R -- L:b\n"); var node = draft.Model.Nodes[0];
        Pass(await draft.UpdateNodeAsync(node.Identity, node.Id, "New Ω", "database", "")); Assert.Contains(" service a(database)[New Ω]  \n", draft.Source);
        var edge = draft.Model.Edges[0]; Pass(await draft.UpdateEdgeAsync(edge.Identity, "a", "b", "T", "B", "<-->")); Assert.Contains(" a:T <--> B:b\n", draft.Source);
        Pass(await draft.UpdateEdgeAsync(edge.Identity, "a", "b", "", "", "-->")); Assert.Contains(" a --> b\n", draft.Source);
        Pass(await draft.UpdateNodeAsync(node.Identity, "renamed", "New Ω", "", "")); Assert.Contains(" service renamed[New Ω]  \n", draft.Source); Assert.Contains(" renamed --> b", draft.Source);
    }
    [Fact]
    public async Task Membership_uses_in_clauses_and_moves_do_not_reindent_or_regenerate_declarations()
    {
        var draft = new ArchitectureVisualDraft("architecture-beta\ngroup g[Group]\n\tservice a[A]\n  %% B Ω\n  service b[B] in g\n"); var first = draft.Model.Nodes[1];
        Pass(await draft.UpdateNodeAsync(first.Identity, "a", "A", "", "g")); Assert.Contains("\tservice a[A] in g\n", draft.Source);
        Pass(await draft.MoveAsync(first.Identity, 1)); Assert.Contains("  %% B Ω\n  service b[B] in g\n\tservice a[A] in g\n", draft.Source); Assert.Equal(first.Identity, draft.Model.Nodes[^1].Identity);
    }
    [Theory, InlineData("service a[Duplicate]\n"), InlineData("a:R --> L:missing\n"), InlineData("{g}:R -- L:a\n"), InlineData("group inner[Inner] in g\n"), InlineData("unknown a\n")]
    public async Task Ambiguous_namespace_hierarchy_endpoints_and_unknown_source_protect_topology(string suffix)
    {
        var draft = new ArchitectureVisualDraft("architecture-beta\ngroup g[G]\nservice a[A] in g\n" + suffix); var original = draft.Source;
        Assert.False(draft.Model.TopologySafe); Assert.False((await draft.AddNodeAsync("service", "newId", "New")).Success); Assert.False((await draft.DeleteAsync(draft.Model.Nodes[0].Identity)).Success); Assert.Equal(original, draft.Source);
    }
    [Fact]
    public async Task Connected_or_nonempty_deletion_never_cascades_and_empty_groups_and_junctions_are_authorable()
    {
        var draft = new ArchitectureVisualDraft("architecture-beta\n"); Pass(await draft.AddNodeAsync("group", "g", "Group")); Pass(await draft.AddNodeAsync("service", "a", "A", parent: "g")); Pass(await draft.AddNodeAsync("junction", "j", "", parent: "g")); Pass(await draft.AddEdgeAsync("a", "j"));
        var original = draft.Source; Assert.False((await draft.DeleteAsync(draft.Model.Nodes[0].Identity)).Success); Assert.False((await draft.DeleteAsync(draft.Model.Nodes[1].Identity)).Success); Assert.Equal(original, draft.Source);
        Pass(await draft.DeleteAsync(draft.Model.Edges[0].Identity)); Pass(await draft.DeleteAsync(draft.Model.Nodes[2].Identity)); Pass(await draft.DeleteAsync(draft.Model.Nodes[1].Identity)); Pass(await draft.DeleteAsync(draft.Model.Nodes[0].Identity)); Assert.Equal("architecture-beta\n", draft.Source);
    }
    [Fact]
    public async Task Node_order_crosses_only_proved_independent_connections_without_rewriting_the_middle()
    {
        var draft = new ArchitectureVisualDraft(ArchitectureVisualDraft.Starter); Pass(await draft.AddNodeAsync("service", "added", "Added")); var added = draft.Source; var key = draft.Model.Nodes[^1].Identity;
        var edges = draft.Model.Edges.Select(e => draft.Text.Line(e.Identity).Text).ToArray(); Pass(await draft.MoveAsync(key, -1)); Assert.Equal(edges, draft.Model.Edges.Select(e => draft.Text.Line(e.Identity).Text)); draft.Undo(); Assert.Equal(added, draft.Source); draft.Redo(); Pass(await draft.DeleteAsync(key)); Assert.Equal(ArchitectureVisualDraft.Starter, draft.Source);
    }
    [Theory, InlineData("Bad]label"), InlineData("A<br>B"), InlineData("bad\nline")]
    public async Task Unproved_label_encoding_keeps_the_last_valid_private_source(string label)
    { var draft = new ArchitectureVisualDraft("architecture-beta\nservice a[A]\n"); Assert.False((await draft.UpdateNodeAsync(draft.Model.Nodes[0].Identity, "a", label, "", "")).Success); Assert.Equal(draft.OriginalSource, draft.Source); Assert.False(draft.CanUndo); }
}
