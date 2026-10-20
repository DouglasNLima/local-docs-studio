using LensDocsStudio.Core.KanbanDiagrams;
using LensDocsStudio.Core.SourceAuthoring;

namespace LensDocsStudio.Core.Tests;

public class KanbanSourceAuthoringTests
{
    private static void Pass(SourceAuthoringResult r) => Assert.True(r.Success, r.Error);
    [Theory, InlineData("\n"), InlineData("\r\n")]
    public async Task Exact_metadata_title_and_local_card_edits_preserve_configuration_comments_and_unknown_keys(string nl)
    {
        var source = string.Join(nl, "---", "title: Config", "---", "%% café Ω", "kanban", " title Effective", "  todo[To do]", "    same[Guide Ω]@{ assigned: old, ASSIGNED: 'Author', unknown: \"keep\", ticket: DOC-1, priority: High }", "    same[Duplicate]", "  done[Done]", "");
        var d = new KanbanVisualDraft(source); Pass(await d.ValidateAsync()); var row = d.Model.Cards[0]; d.Select(row.Identity);
        Pass(await d.UpdateAsync(row.Identity, row.Id, "Authored Ω", "New author", "DOC-1", "High"));
        Assert.Equal(source.Replace("Guide Ω", "Authored Ω", StringComparison.Ordinal).Replace("'Author'", "'New author'", StringComparison.Ordinal), d.Source); Assert.Equal(row.Identity, d.Selection);
        d.Undo(); Assert.Equal(source, d.Source); d.Redo(); Assert.Contains("assigned: old, ASSIGNED: 'New author', unknown: \"keep\"", d.Source);
        Pass(await d.SetTitleAsync("New title")); Assert.Contains("title: Config" + nl, d.Source); Assert.Contains(" title New title" + nl, d.Source);
    }
    [Fact] public async Task Clearing_last_repeated_metadata_keeps_earlier_lexemes_without_revealing_their_values()
    {
        var d = new KanbanVisualDraft("kanban\n  A\n    id[Card]@{ assigned: old, ASSIGNED: new }\n"); var r = d.Model.Cards[0];
        Pass(await d.UpdateAsync(r.Identity, r.Id, r.Label, "", "", "")); Assert.Contains("assigned: old, ASSIGNED: ''", d.Source); Assert.Equal("", d.Model.Cards[0].Meta("assigned"));
    }
    [Fact] public async Task Bare_and_bracket_forms_remain_coupled_and_title_like_labels_are_rejected()
    {
        var d = new KanbanVisualDraft("kanban\n  Bare\n    [Card]\n"); var row = d.Model.Cards[0];
        Assert.False((await d.UpdateAsync(row.Identity, "Different", "New", "", "", "")).Success); Assert.False((await d.UpdateAsync(row.Identity, "title Changed", "title Changed", "", "", "")).Success);
        Pass(await d.UpdateAsync(row.Identity, "New Ω", "New Ω", "", "", "")); Assert.Contains("    [New Ω]\n", d.Source); Assert.Equal("bracket", d.Model.Cards[0].Form);
    }
    [Fact] public async Task Column_move_carries_complete_proved_cards_and_comments_with_stable_duplicate_occurrences()
    {
        const string source = "kanban\n  %% column A\n  a[A]\n    %% first\n    same[Same]\n    same[Same]\n  b[B]\n    other[Other]\n";
        var d = new KanbanVisualDraft(source); var col = d.Model.Columns[0]; var keys = d.Model.Cards.Take(2).Select(c => c.Identity).ToArray(); d.Select(col.Identity);
        Pass(await d.MoveAsync(col.Identity, 1)); Assert.EndsWith("  %% column A\n  a[A]\n    %% first\n    same[Same]\n    same[Same]\n", d.Source); Assert.Equal(keys, d.Model.Cards.TakeLast(2).Select(c => c.Identity)); d.Undo(); Assert.Equal(source, d.Source); Assert.Equal(col.Identity, d.Selection); d.Redo(); Assert.Equal(col.Identity, d.Model.Columns[1].Identity);
    }
    [Fact] public async Task Move_card_changes_only_intended_membership_and_necessary_indentation()
    {
        const string source = "kanban\n\ta[A]\n\t\t%% card comment\n\t\tx[Card Ω]\n\tb[B]\n\t\t\ty[Unchanged]\n";
        var d = new KanbanVisualDraft(source); var card = d.Model.Cards[0]; var destination = d.Model.Columns[1]; d.Select(card.Identity);
        Pass(await d.MoveCardAsync(card.Identity, destination.Identity)); Assert.Equal(destination.Identity, d.Model.Cards.Single(c => c.Identity == card.Identity).Column); Assert.Contains("\t\t\ty[Unchanged]\n\t\t\t%% card comment\n\t\t\tx[Card Ω]\n", d.Source); d.Undo(); Assert.Equal(source, d.Source);
    }
    [Fact] public async Task Deletion_never_discards_cards_without_explicit_complete_removal()
    {
        var d = new KanbanVisualDraft(KanbanVisualDraft.Starter); var col = d.Model.Columns[0]; Assert.False((await d.DeleteAsync(col.Identity)).Success); Assert.Equal(d.OriginalSource, d.Source);
        Pass(await d.DeleteAsync(col.Identity, true)); Assert.Single(d.Model.Columns); Assert.Single(d.Model.Cards); d.Undo(); Assert.Equal(d.OriginalSource, d.Source);
    }
    [Theory, InlineData("kanban\n  A\n    Card\n B\n"), InlineData("kanban\n  A\n    Card\n    %% trailing\n\n  B\n")]
    public async Task Mixed_indentation_and_ambiguous_comments_protect_structure_but_allow_safe_labels(string source)
    {
        var d = new KanbanVisualDraft(source); var col = d.Model.Columns[0]; Assert.False((await d.MoveAsync(col.Identity, 1)).Success); Assert.Equal(source, d.Source);
        var card = d.Model.Cards[0]; Pass(await d.UpdateAsync(card.Identity, "Edited", "Edited", "", "", "")); Assert.Contains("Edited", d.Source);
    }
    [Fact] public async Task Partial_metadata_is_preserved_and_independent_label_edit_remains_available()
    {
        var d = new KanbanVisualDraft("kanban\n  A\n    id[Card]@{ assigned: 'broken, unknown ??? }\n"); var row = d.Model.Cards[0];
        Assert.False(row.MetadataSafe); Assert.False((await d.UpdateAsync(row.Identity, row.Id, row.Label, "New", "", "")).Success); Pass(await d.UpdateAsync(row.Identity, row.Id, "Safe", row.Meta("assigned"), row.Meta("ticket"), row.Meta("priority"))); Assert.Contains("@{ assigned: 'broken, unknown ??? }", d.Source);
    }
    [Fact] public async Task Empty_board_can_gain_columns_and_cards_without_generated_ID_changes_to_existing_source()
    {
        var d = new KanbanVisualDraft("kanban\n"); Pass(await d.ValidateAsync()); Pass(await d.AddColumnAsync("todo", "To do")); Pass(await d.AddCardAsync(d.Model.Columns[0].Identity, "card", "Guide Ω")); Assert.Equal("card", d.Model.Cards[0].Id); Assert.False((await d.AddColumnAsync("card", "Collision")).Success);
    }
    [Theory, InlineData("assigned: 'unclosed"), InlineData("assigned: \"unclosed"), InlineData("assigned:")]
    public async Task Unsupported_or_partial_quotation_protects_metadata_even_when_native_parser_falls_back_to_bare_value(string metadata)
    {
        var d = new KanbanVisualDraft("kanban\n  A\n    id[Card]@{ " + metadata + " }\n"); var r = d.Model.Cards[0]; Pass(await d.ValidateAsync()); Assert.False(r.MetadataSafe);
        Assert.False((await d.UpdateAsync(r.Identity, r.Id, r.Label, "New", "", "")).Success); Assert.Equal(d.OriginalSource, d.Source);
        Pass(await d.UpdateAsync(r.Identity, r.Id, "Safe label", r.Meta("assigned"), r.Meta("ticket"), r.Meta("priority"))); Assert.Contains(metadata, d.Source);
    }
    [Fact] public async Task New_comment_like_IDs_cannot_silently_create_ignored_columns_or_cards()
    {
        var d = new KanbanVisualDraft(KanbanVisualDraft.Starter); Assert.False((await d.AddColumnAsync("%%hidden", "Column")).Success); Assert.False((await d.AddCardAsync(d.Model.Columns[0].Identity, "%%hidden", "Card")).Success); Assert.Equal(d.OriginalSource, d.Source);
    }
}
