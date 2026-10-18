using LensDocsStudio.Core.Content;
using LensDocsStudio.Core.Parsing;

namespace LensDocsStudio.Core.Tests;

public sealed class NativeHelpGuideTests
{
    [Fact] public void Bundled_guide_has_complete_independent_topics_with_unique_heading_targets()
    {
        Assert.Equal(15, NativeHelpGuide.Topics.Count);
        Assert.Equal(15, NativeHelpGuide.Topics.Select(topic => topic.Anchor).Distinct().Count());
        foreach (var topic in NativeHelpGuide.Topics)
        {
            var parsed = new MarkdownService().Parse(topic.Source);
            var heading = Assert.Single(parsed.Headings, heading => heading.Level == 2);
            Assert.Equal(topic.Title, heading.Title); Assert.Equal(topic.Anchor, heading.Anchor);
            Assert.True(topic.Source.Length > 400);
        }
    }
    [Fact] public void Mermaid_visual_authoring_has_an_independent_searchable_source_preservation_topic()
    {
        var topic = Assert.Single(NativeHelpGuide.Search("Mermaid visual authoring"));
        Assert.Equal("mermaid-visual-authoring", topic.Anchor);
        Assert.Contains("Visual Edit", topic.Source); Assert.Contains("Edit source", topic.Source);
        Assert.Contains("protected visual inspection", topic.Source); Assert.Contains("one ordinary Undo action", topic.Source);
        Assert.Contains("positions last", topic.Source); Assert.Contains("protected", topic.Source);
    }
    [Fact] public void Class_visual_authoring_has_a_searchable_portable_source_and_classifier_limit_topic()
    {
        var topic = Assert.Single(NativeHelpGuide.Search("Class Diagram visual authoring"));
        Assert.Equal("class-diagram-visual-authoring", topic.Anchor);
        Assert.Contains("Insert → Class diagram visually", topic.Source); Assert.Contains("one Undo action", topic.Source);
        Assert.Contains("static/abstract classifiers", topic.Source); Assert.Contains("protected", topic.Source);
        Assert.Contains("Canonical", topic.Source, StringComparison.OrdinalIgnoreCase);
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
