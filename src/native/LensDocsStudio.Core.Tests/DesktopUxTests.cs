using System.Text.Json;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Editing;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LensDocsStudio.Core.Tests;

public sealed class DesktopUxTests
{
    [Fact] public void Earlier_preferences_preserve_choices_and_gain_opt_in_synchronisation()
    {
        var preferences = JsonSerializer.Deserialize<UserPreferences>("""{"Theme":"Dark","ShowOutline":false,"AutomaticUpdateChecks":false}""")!;
        Assert.True(WorkspaceSettings.ArePreferencesValid(preferences));
        Assert.Equal("Dark", preferences.Theme); Assert.False(preferences.ShowOutline); Assert.False(preferences.AutomaticUpdateChecks);
        var desktop = preferences.Desktop ?? new();
        Assert.True(desktop.ShowSidebar); Assert.False(desktop.FollowCaret); Assert.False(desktop.SyncScroll); Assert.False(desktop.SyncSelection);
    }
    [Theory] [InlineData(149, 240)] [InlineData(601, 240)] [InlineData(230, 159)] [InlineData(230, 601)] [InlineData(double.NaN, 240)]
    public void Invalid_panel_bounds_do_not_load_as_trusted_preferences(double sidebar, double outline)
        => Assert.False(WorkspaceSettings.ArePreferencesValid(new(Desktop: new(SidebarWidth: sidebar, OutlineWidth: outline))));
    [Fact] public void Panel_and_sync_choices_round_trip_without_losing_other_settings()
    {
        var preferences = new UserPreferences(Theme: "Dark", ShowOutline: false, AutomaticUpdateChecks: false,
            Desktop: new(false, 340, 310, true, false, true));
        var restored = JsonSerializer.Deserialize<UserPreferences>(JsonSerializer.Serialize(preferences))!;
        Assert.Equal(preferences, restored); Assert.True(WorkspaceSettings.ArePreferencesValid(restored));
        var updated = (WorkspaceSettings.From(restored) with { Theme = "Light" }).ApplyTo(restored);
        Assert.Equal(restored.Desktop, updated.Desktop); Assert.False(updated.AutomaticUpdateChecks);
    }
    [Theory] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void Heading_levels_preserve_CRLF_and_unedited_source(int level)
    {
        const string source = "Before\r\n\r\n## Café\r\n\r\nAfter\r\n";
        var edit = new MarkdownEditService().Plan(source, source.IndexOf("Café", StringComparison.Ordinal), 4, "heading:" + level);
        var result = source.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.Replacement);
        var expected = level == 2 ? "Café" : new string('#', level) + " Café";
        Assert.Equal("Before\r\n\r\n" + expected + "\r\n\r\nAfter\r\n", result);
    }
    [Fact] public void Repeated_words_map_to_their_own_formatted_source_occurrences()
    {
        const string source = "action **action** *action* [action](other.md)\n\nCafé 😀 action";
        var parsed = new MarkdownService().Parse(source);
        var literals = parsed.Syntax.Descendants<LiteralInline>().Where(inline => inline.Content.ToString() == "action").OrderBy(inline => inline.Span.Start).ToArray();
        Assert.Equal(3, literals.Length);
        var spans = literals.Select(inline => LiteralSourceMapping.Locate(source, inline.Span.Start, inline.Span.End, inline.Content.ToString())).ToArray();
        Assert.All(spans, span => Assert.NotNull(span));
        Assert.Equal(source.IndexOf("**action", StringComparison.Ordinal) + 2, spans[0]!.Start);
        Assert.Equal(source.IndexOf(" *action*", StringComparison.Ordinal) + 2, spans[1]!.Start);
        Assert.Equal(source.IndexOf("[action", StringComparison.Ordinal) + 1, spans[2]!.Start);
    }
    [Theory] [InlineData("Café")] [InlineData("😀")] [InlineData("é")]
    public void Unicode_offsets_are_exact_UTF16_source_offsets(string literal)
    {
        var source = "first **" + literal + "** last";
        var inline = new MarkdownService().Parse(source).Syntax.Descendants<LiteralInline>().Single(item => item.Content.ToString() == literal);
        Assert.Equal(new LiteralSourceSpan(8, literal.Length), LiteralSourceMapping.Locate(source, inline.Span.Start, inline.Span.End, literal));
    }
    [Fact] public void Ambiguous_or_transformed_text_does_not_fabricate_an_exact_source_selection()
    {
        Assert.Null(LiteralSourceMapping.Locate("&amp;", 0, 4, "& transformed"));
        Assert.Null(LiteralSourceMapping.Locate("action action", 0, 12, "action"));
        Assert.Null(LiteralSourceMapping.Locate("action", 99, 104, "action"));
        Assert.Null(LiteralSourceMapping.Locate("action", 0, 5, "different"));
    }
}
