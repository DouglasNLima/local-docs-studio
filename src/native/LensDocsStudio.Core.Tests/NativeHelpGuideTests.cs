using LensDocsStudio.Core.Content;
using LensDocsStudio.Core.Parsing;

namespace LensDocsStudio.Core.Tests;

public sealed class NativeHelpGuideTests
{
    [Fact] public void Bundled_guide_has_complete_independent_topics_with_unique_heading_targets()
    {
        Assert.Equal(19, NativeHelpGuide.Topics.Count);
        Assert.Equal(19, NativeHelpGuide.Topics.Select(topic => topic.Anchor).Distinct().Count());
        foreach (var topic in NativeHelpGuide.Topics)
        {
            var parsed = new MarkdownService().Parse(topic.Source);
            var heading = Assert.Single(parsed.Headings, heading => heading.Level == 2);
            Assert.Equal(topic.Title, heading.Title); Assert.Equal(topic.Anchor, heading.Anchor);
            Assert.True(topic.Source.Length > 400);
        }
    }
    [Fact] public void Workflow_topic_explains_indentation_symbolic_references_and_stateful_history_protection()
    {
        var topic = Assert.Single(NativeHelpGuide.Search("Kanban, Requirement and GitGraph visual authoring"));
        foreach (var phrase in new[] { "first content line", "last-wins", "Display ID", "symbolic", "non-empty heads", "cherry-pick", "pending", "Cancel", "stale-owner" }) Assert.Contains(phrase, topic.Source, StringComparison.OrdinalIgnoreCase);
    }
    [Fact] public void Chart_topic_explains_alignment_pending_input_and_local_versus_global_endpoint_edits()
    {
        var topic = Assert.Single(NativeHelpGuide.Search("XY Chart, Radar and Sankey visual authoring"));
        foreach (var phrase in new[] { "aligned", "ragged", "positive", "Rename node", "collision", "pending", "Undo", "Cancel", "source-only" })
            Assert.Contains(phrase, topic.Source, StringComparison.OrdinalIgnoreCase);
    }
    [Fact] public void Expanded_editor_topic_explains_source_ownership_and_pinned_family_limits()
    {
        var topic = Assert.Single(NativeHelpGuide.Search("Gantt, Mind map and Pie visual authoring"));
        Assert.Equal("gantt-mind-map-and-pie-visual-authoring", topic.Anchor);
        foreach (var phrase in new[] { "native calendar", "Duplicate", "Zero rows", "Embedded double quotes", "one owner-document", "Cancel", "stale", "protected" })
            Assert.Contains(phrase, topic.Source, StringComparison.OrdinalIgnoreCase);
    }
    [Fact] public void Timeline_Journey_and_Quadrant_topic_explains_occurrences_clamping_and_local_actor_scope()
    {
        var topic = Assert.Single(NativeHelpGuide.Search("Timeline, User Journey and Quadrant visual authoring"));
        foreach (var phrase in new[] { "continuation", "raw", "1–5", "0–1", "actor", "task only", "quadrant 1", "pending", "Undo", "Cancel" }) Assert.Contains(phrase, topic.Source, StringComparison.OrdinalIgnoreCase);
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
