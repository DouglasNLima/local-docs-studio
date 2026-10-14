using LensDocsStudio.Core.Content;
using LensDocsStudio.Core.Parsing;

namespace LensDocsStudio.Core.Tests;

public sealed class NativeHelpGuideTests
{
    [Fact] public void Bundled_guide_has_complete_independent_topics_with_unique_heading_targets()
    {
        Assert.Equal(13, NativeHelpGuide.Topics.Count);
        Assert.Equal(13, NativeHelpGuide.Topics.Select(topic => topic.Anchor).Distinct().Count());
        foreach (var topic in NativeHelpGuide.Topics)
        {
            var parsed = new MarkdownService().Parse(topic.Source);
            var heading = Assert.Single(parsed.Headings, heading => heading.Level == 2);
            Assert.Equal(topic.Title, heading.Title); Assert.Equal(topic.Anchor, heading.Anchor);
            Assert.True(topic.Source.Length > 400);
        }
    }
    [Fact] public void Topic_search_matches_body_text_without_changing_bundled_source()
    {
        var original = NativeHelpGuide.Topics.Select(topic => topic.Source).ToArray();
        var migration = Assert.Single(NativeHelpGuide.Search("  BROWSER LIBRARY  "));
        Assert.Equal("Templates, snippets and browser migration", migration.Title);
        Assert.Contains("Import JSON", migration.Source, StringComparison.Ordinal);
        Assert.Equal(original, NativeHelpGuide.Topics.Select(topic => topic.Source));
        Assert.Equal(NativeHelpGuide.Topics, NativeHelpGuide.Search(null));
        Assert.Empty(NativeHelpGuide.Search("missing-topic-928461"));
        Assert.Empty(NativeHelpGuide.Search(new string('x', 10000)));
    }
    [Fact] public void Guide_describes_recovery_update_consent_and_real_keyboard_commands()
    {
        Assert.Contains(NativeHelpGuide.Search("recovery"), topic => topic.Title == "Saving, recovery and external changes");
        Assert.Contains("Ctrl+,", Assert.Single(NativeHelpGuide.Search("Settings and keyboard shortcuts")).Source, StringComparison.Ordinal);
        var updates = Assert.Single(NativeHelpGuide.Search("Updates and privacy"));
        Assert.Contains("separate user actions", updates.Source, StringComparison.Ordinal);
        Assert.Contains("not yet configured", updates.Source, StringComparison.Ordinal);
    }
}
