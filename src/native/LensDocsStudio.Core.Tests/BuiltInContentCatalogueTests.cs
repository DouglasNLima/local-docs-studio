using LensDocsStudio.Core.Content;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Core.Tests;

public sealed class BuiltInContentCatalogueTests
{
    private static readonly DateOnly Date = new(2026, 10, 12);
    private static CatalogueEntry Entry(string id) => BuiltInContentCatalogue.Entries.Single(entry => entry.Id == id);

    [Fact] public void All_seventeen_previous_Mermaid_templates_retain_their_exact_pinned_source()
    {
        using var baseline = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/native-editor-baseline.json")));
        var templates = baseline.RootElement.GetProperty("templates").EnumerateArray().ToArray(); Assert.Equal(17, templates.Length);
        foreach (var template in templates)
        {
            var entry = Entry(template.GetProperty("id").GetString()!);
            Assert.Equal(template.GetProperty("sha256").GetString(), Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(entry.ContentPattern))).ToLowerInvariant());
        }
    }

    [Fact] public void Bundled_inventory_covers_examples_diagrams_and_all_three_document_studios()
    {
        var entries = BuiltInContentCatalogue.Entries;
        Assert.Equal(44, entries.Count(entry => !entry.IsSnippet)); Assert.Equal(34, entries.Count(entry => entry.IsSnippet));
        Assert.Equal(22, entries.Count(entry => !entry.IsSnippet && entry.Format == "mermaid")); // Twenty families; C4 has three explicitly named variants.
        foreach (var family in new[] { "xychart", "radar", "sankey" })
        {
            Assert.Contains(entries, entry => entry.Id == "mermaid/" + family && entry.Format == "mermaid");
            Assert.Contains(entries, entry => entry.Id == "mermaid-snippet/" + family && entry.IsSnippet);
        }
        foreach (var family in new[] { "kanban", "requirement", "gitgraph" })
        {
            Assert.Contains(entries, e => e.Id == "mermaid/" + family && e.Format == "mermaid");
            Assert.Contains(entries, e => e.Id == "mermaid-snippet/" + family + "-native" && e.IsSnippet);
        }
        foreach (var variant in new[] { "architecture", "c4-context", "c4-container", "c4-component", "block" })
            Assert.Contains(entries, entry => entry.Id == "mermaid/" + variant && entry.Format == "mermaid");
        foreach (var family in new[] { "architecture", "c4", "block" })
            Assert.Contains(entries, entry => entry.Id == "mermaid-snippet/" + family + "-native" && entry.IsSnippet);
        Assert.Equal(new[] { "Examples", "Mermaid", "Project Docs", "Release Notes", "Requirements" }, entries.Select(entry => entry.Group).Distinct());
        foreach (var entry in entries)
        {
            var generated = BuiltInContentCatalogue.Generate(entry, date: Date);
            Assert.False(string.IsNullOrWhiteSpace(generated.Content)); Assert.DoesNotContain("{{", generated.Content);
            if (!entry.IsSnippet) { Assert.NotNull(generated.Name); Assert.Equal(generated.Name, Path.GetFileName(generated.Name)); }
            if (entry.Format == "markdown" && !entry.IsSnippet) Assert.NotEmpty(new MarkdownService().Parse(generated.Content).Headings);
        }
    }

    [Fact] public void Project_generation_retains_Unicode_and_literal_user_field_tokens_without_recursive_expansion()
    {
        var result = BuiltInContentCatalogue.Generate(Entry("project/readme"), new Dictionary<string, string>
        { ["title"] = "Café 東京 {{audience}}", ["audience"] = "Engineers & **reviewers**" }, Date);
        Assert.StartsWith("# Café 東京 {{audience}}\n", result.Content);
        Assert.Contains("Engineers & **reviewers**", result.Content); Assert.Equal("caf-audience-readme.md", result.Name);
        Assert.DoesNotContain("# Café 東京 Engineers", result.Content);
    }

    [Fact] public void Release_and_work_item_generators_apply_all_fields_and_a_deterministic_current_date()
    {
        var release = BuiltInContentCatalogue.Generate(Entry("release/customer"), new Dictionary<string, string> { ["version"] = "v2.4.1" }, Date);
        Assert.Equal("release-notes-v2-4-1.md", release.Name); Assert.Contains("Release date: 2026-10-12", release.Content); Assert.Contains("title v2.4.1 rollout", release.Content);
        var task = BuiltInContentCatalogue.Generate(Entry("requirements/devopsConclusion"), new Dictionary<string, string>
        { ["title"] = "Café approvals", ["workItemId"] = "AB#431", ["developer"] = "Zoë", ["date"] = "2026-10-15" }, Date);
        Assert.Equal("ab-431-conclusion.md", task.Name); Assert.Contains("Work item: AB#431", task.Content);
        Assert.Contains("Developer: Zoë", task.Content); Assert.Contains("Date: 2026-10-15", task.Content); Assert.Contains("Conclusion: Café approvals", task.Content);
    }

    [Fact] public void Blank_fields_use_defaults_without_changing_other_generations()
    {
        var entry = Entry("project/adr");
        var first = BuiltInContentCatalogue.Generate(entry, new Dictionary<string, string> { ["title"] = "  ", ["date"] = "" }, Date);
        Assert.Equal("adr-use-a-modular-architecture.md", first.Name); Assert.Contains("Date: 2026-10-12", first.Content);
        _ = BuiltInContentCatalogue.Generate(entry, new Dictionary<string, string> { ["title"] = "Different" }, Date);
        Assert.Equal(first, BuiltInContentCatalogue.Generate(entry, date: Date));
    }

    [Theory] [InlineData("CON", "document-con.md")] [InlineData("lpt1", "document-lpt1.md")]
    [InlineData("../../Escape", "escape.md")] [InlineData("C:\\outside", "c-outside.md")]
    [InlineData("東京", "document.md")] [InlineData("  Sprint 1  ", "sprint-1.md")]
    public void Suggested_names_are_portable_basenames_even_for_paths_devices_or_non_Latin_titles(string title, string name)
        => Assert.Equal(name, BuiltInContentCatalogue.Generate(Entry("release/sprint"), new Dictionary<string, string> { ["title"] = title }, Date).Name);

    [Fact] public void Long_allowed_fields_preserve_content_while_bounding_the_filename()
    {
        var title = new string('a', BuiltInContentCatalogue.MaximumFieldCharacters);
        var result = BuiltInContentCatalogue.Generate(Entry("project/readme"), new Dictionary<string, string> { ["title"] = title }, Date);
        Assert.StartsWith("# " + title + "\n", result.Content); Assert.Equal(new string('a', 96) + "-readme.md", result.Name);
    }

    [Theory] [InlineData("line\nbreak")] [InlineData("line\rbreak")] [InlineData("null\0byte")] [InlineData("tab\tfield")]
    public void Generator_fields_reject_control_characters_before_creating_source(string title)
        => Assert.Throws<ArgumentException>(() => BuiltInContentCatalogue.Generate(Entry("project/readme"), new Dictionary<string, string> { ["title"] = title }, Date));

    [Fact] public void Oversized_unknown_and_unpaired_Unicode_fields_do_not_create_invalid_library_content()
    {
        var entry = Entry("project/readme");
        Assert.Throws<ArgumentException>(() => BuiltInContentCatalogue.Generate(entry, new Dictionary<string, string> { ["title"] = new('a', 513) }, Date));
        Assert.Throws<ArgumentException>(() => BuiltInContentCatalogue.Generate(entry, new Dictionary<string, string> { ["path"] = "outside" }, Date));
        Assert.Throws<InvalidDataException>(() => BuiltInContentCatalogue.Generate(entry, new Dictionary<string, string> { ["title"] = "\ud800" }, Date));
        Assert.Throws<ArgumentException>(() => BuiltInContentCatalogue.Generate(entry with { ContentPattern = "Unreviewed" }, date: Date));
    }

    [Fact] public void Built_in_snippets_preserve_source_and_can_round_trip_in_the_portable_user_library()
    {
        var entry = Entry("requirements-snippet/flow"); var result = BuiltInContentCatalogue.Generate(entry, date: Date);
        Assert.Equal(entry.ContentPattern, result.Content); Assert.Null(result.Name);
        var roundTrip = LocalLibraryFormat.Read(LocalLibraryFormat.Write(new([], [result], [])));
        Assert.Equal(result, Assert.Single(roundTrip.Snippets)); Assert.Contains("```mermaid\nflowchart LR", result.Content);
    }
}
