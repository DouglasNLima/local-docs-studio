using LensDocsStudio.Core.GitGraphDiagrams;
using LensDocsStudio.Core.SourceAuthoring;

namespace LensDocsStudio.Core.Tests;

public class GitGraphSourceAuthoringTests
{
    private static void Pass(SourceAuthoringResult r) => Assert.True(r.Success, r.Error);
    [Theory, InlineData("\n"), InlineData("\r\n")]
    public async Task Commit_fields_patch_exact_source_and_preserve_neighbours_comments_and_configuration(string nl)
    {
        var source = string.Join(nl, "---", "title: Config", "---", "%% café Ω", "gitGraph LR:", "\tcommit  id: \"initial Ω\"   tag: \"v1\" type: NORMAL  ", "\tcommit", ""); var d = new GitGraphVisualDraft(source); Pass(await d.ValidateAsync()); var c = d.Model.Commands[0]; d.Select(c.Identity);
        Pass(await d.UpdateAsync(c.Identity, c.Value("id"), "v2", "HIGHLIGHT")); Assert.Equal(source.Replace("\"v1\"", "\"v2\"", StringComparison.Ordinal).Replace("type: NORMAL", "type: HIGHLIGHT", StringComparison.Ordinal), d.Source); Assert.Equal(c.Identity, d.Selection); d.Undo(); Assert.Equal(source, d.Source); d.Redo(); Assert.Equal(c.Identity, d.Model.Commands[0].Identity);
    }
    [Fact] public async Task Branch_context_heads_and_anonymous_counter_match_canonical_parent_links()
    {
        var d = new GitGraphVisualDraft(GitGraphVisualDraft.Starter); Pass(await d.ValidateAsync()); Assert.True(d.Model.Basic); Assert.Equal(new[] { "Start", "1", "2", "Reviewed", "4" }, d.Model.Marks.Select(c => c.Label));
        Assert.Equal("docs", d.Model.Commands[1].BranchAfter); Assert.Equal("docs", d.Model.Commands[2].BranchBefore); Assert.Equal("main", d.Model.CurrentBranch); Assert.Equal(5, d.Model.Links.Count); Assert.Single(d.Model.Links, l => l.Kind == "fork"); Assert.Single(d.Model.Links, l => l.Kind == "merge"); Assert.Equal(3, d.Model.Links.Count(l => l.Kind == "lane"));
        Pass(await d.AppendAsync("commit")); Assert.Equal("5", d.Model.Marks[^1].Label); Assert.EndsWith("    commit\n", d.Source); Assert.DoesNotContain("id: \"5\"", d.Source);
    }
    [Theory, InlineData("TB:"), InlineData("BT:")]
    public async Task Parsed_but_ignored_orientation_protects_topology_while_safe_commit_fields_remain_available(string orientation)
    {
        var d = new GitGraphVisualDraft("gitGraph " + orientation + "\ncommit\n"); Pass(await d.ValidateAsync()); Assert.False((await d.AppendAsync("commit")).Success); Pass(await d.UpdateAsync(d.Model.Commands[0].Identity, "", "tag Ω", "REVERSE")); Assert.StartsWith("gitGraph " + orientation, d.Source);
    }
    [Theory, InlineData("branch docs order: 0009\ncommit\n"), InlineData("branch docs\ncommit\nbranch docs\ncommit\n"), InlineData("checkout unknown\ncommit\n"), InlineData("merge missing\n"), InlineData("cherry-pick id: \"missing\" parent: \"unknown\"\n"), InlineData("unknown command id: \"keep\"\n")]
    public async Task Unproved_stream_forms_remain_exact_and_cannot_gain_stateful_commands(string suffix)
    {
        var source = "gitGraph\ncommit\n" + suffix; var d = new GitGraphVisualDraft(source); Pass(await d.ValidateAsync()); Assert.False(d.Model.Basic); Assert.False((await d.AppendAsync("commit")).Success); Assert.False((await d.DeleteAsync(d.Model.Commands[0].Identity)).Success); Assert.Equal(source, d.Source);
    }
    [Fact] public async Task New_checkout_merge_and_branch_commands_validate_references_without_invented_repairs()
    {
        var d = new GitGraphVisualDraft("gitGraph\ncommit\n"); Assert.False((await d.AppendAsync("checkout", "missing")).Success); Assert.False((await d.AppendAsync("merge", "main")).Success); Assert.False((await d.AppendAsync("branch", "bad name")).Success);
        Pass(await d.AppendAsync("branch", "docs")); Assert.Equal("docs", d.Model.CurrentBranch); Assert.False((await d.AppendAsync("merge", "main")).Success); Pass(await d.AppendAsync("commit", id: "docs-1", tag: "Draft Ω", type: "HIGHLIGHT")); Pass(await d.AppendAsync("switch", "main")); Pass(await d.AppendAsync("merge", "docs", id: "merged")); Pass(await d.ValidateAsync());
        Assert.Equal("main", d.Model.CurrentBranch); Assert.Equal("merge", d.Model.Commands[^1].Kind); Assert.Equal(2, d.Model.Links.Count(l => l.To == d.Model.Commands[^1].Identity));
    }
    [Fact] public async Task Tail_commit_moves_and_deletes_preserve_keys_and_expose_canonical_renumbering()
    {
        const string source = "gitGraph\ncommit id: \"root\"\nbranch docs\ncommit\n%% attached\ncommit tag: \"same\"\ncommit tag: \"same\"\n"; var d = new GitGraphVisualDraft(source); var selected = d.Model.Commands[^1]; d.Select(selected.Identity); Pass(await d.MoveAsync(selected.Identity, -1)); Assert.Equal(selected.Identity, d.Model.Commands[^2].Identity); Assert.Equal("2", d.Model.Commands[^2].Label); d.Undo(); Assert.Equal(source, d.Source); d.Redo(); Assert.Equal(selected.Identity, d.Selection); Pass(await d.DeleteAsync(selected.Identity)); Assert.Equal(3, d.Model.Marks.Count);
        Assert.False((await d.DeleteAsync(d.Model.Commands[0].Identity)).Success);
    }
    [Fact] public async Task Unsupported_quotes_duplicate_IDs_and_partial_attributes_never_rewrite_valid_history()
    {
        var d = new GitGraphVisualDraft("gitGraph\ncommit id: \"same\"\ncommit id: \"same\"\n"); Pass(await d.ValidateAsync()); var c = d.Model.Commands[1]; Pass(await d.UpdateAsync(c.Identity, "same", "tag", "NORMAL")); var before = d.Source;
        Assert.False((await d.UpdateAsync(c.Identity, "bad\"quote", "tag", "NORMAL")).Success); Assert.False((await d.UpdateAsync(c.Identity, "same", "bad\nline", "NORMAL")).Success); Assert.False((await d.UpdateAsync(d.Model.Commands[0].Identity, "same", "", "INVALID")).Success); Assert.Equal(before, d.Source);
        var partial = new GitGraphVisualDraft("gitGraph\ncommit id: \"first\" id: \"second\" strange: keep\n"); Pass(await partial.ValidateAsync()); Assert.False((await partial.UpdateAsync(partial.Model.Commands[0].Identity, "new", "", "NORMAL")).Success); Assert.Equal(partial.OriginalSource, partial.Source);
    }
    [Fact] public async Task Empty_history_can_be_authored_and_clearing_an_explicit_ID_restores_actual_anonymous_counter()
    {
        var d = new GitGraphVisualDraft("gitGraph\n"); Pass(await d.ValidateAsync()); Pass(await d.AppendAsync("commit", id: "first")); Pass(await d.AppendAsync("commit", id: "second")); Pass(await d.UpdateAsync(d.Model.Commands[1].Identity, "", "", "NORMAL")); Assert.Equal("1", d.Model.Marks[1].Label); Assert.DoesNotContain("second", d.Source); Pass(await d.ValidateAsync());
    }
    [Fact] public async Task ID_edits_preserve_untouched_case_insensitive_type_keyword_and_value_lexemes()
    {
        const string source = "gitGraph\ncommit id: \"old\" TYPE: hIgHlIgHt\n"; var d = new GitGraphVisualDraft(source); Pass(await d.ValidateAsync()); Pass(await d.UpdateAsync(d.Model.Commands[0].Identity, "new", "", "HIGHLIGHT")); Assert.Equal(source.Replace("\"old\"", "\"new\"", StringComparison.Ordinal), d.Source);
    }
    [Theory, InlineData("topic\n"), InlineData(" topic "), InlineData("bad name"), InlineData("\"quoted\""), InlineData("main")]
    public async Task New_branch_names_reject_control_whitespace_quotes_and_collisions_without_coercion(string name)
    {
        var d = new GitGraphVisualDraft("gitGraph\ncommit\n"); Assert.NotNull(GitGraphVisualDraft.NewBranchProblem(d.Model, name)); Assert.False((await d.AppendAsync("branch", name)).Success); Assert.Equal(d.OriginalSource, d.Source); Assert.False(d.CanUndo);
    }
    [Fact] public async Task New_Unicode_branch_names_have_actual_native_lane_and_head_correspondence()
    {
        var d = new GitGraphVisualDraft("gitGraph\ncommit\n"); Pass(await d.AppendAsync("branch", "dóc-Ω")); Pass(await d.AppendAsync("commit")); Assert.Equal("dóc-Ω", d.Model.CurrentBranch); Assert.Contains("branch dóc-Ω", d.Source); Pass(await d.ValidateAsync());
    }
}
