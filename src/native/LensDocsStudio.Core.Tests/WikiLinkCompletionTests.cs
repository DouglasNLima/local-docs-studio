using LensDocsStudio.Core.Editing;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;

namespace LensDocsStudio.Core.Tests;

public sealed class WikiLinkCompletionTests
{
    private static WikiLinkIndex Catalogue => new(["README.md", "docs/Guide.md", "docs/Café review.md", "one/Summary.md", "two/Summary.md"]);
    [Theory]
    [InlineData("Before [[Gui", "Before [[docs/Guide.md]]")]
    [InlineData("Before\r\n[[Gui", "Before\r\n[[docs/Guide.md]]")]
    public void Completion_inserts_a_full_workspace_path_with_closing_brackets(string source, string expected)
    {
        var edit = WikiLinkCompletionService.Plan(source, source.Length, Catalogue, "docs/Guide.md")!;
        Assert.Equal(expected, Apply(source, edit)); Assert.Equal(expected.Length, edit.SelectionStart);
    }
    [Fact] public void Mid_target_completion_preserves_alias_and_existing_delimiters()
    {
        const string source = "Before [[Guide | Friendly label]] after";
        var edit = WikiLinkCompletionService.Plan(source, source.IndexOf("Guide", StringComparison.Ordinal) + 3, Catalogue, "docs/Guide.md")!;
        Assert.Equal("Before [[docs/Guide.md | Friendly label]] after", Apply(source, edit));
        Assert.Equal(Apply(source, edit).IndexOf(" after", StringComparison.Ordinal), edit.SelectionStart);
    }
    [Theory]
    [InlineData("")]
    [InlineData("Normal text")]
    [InlineData("[[")]
    [InlineData("\\[[Gui")]
    [InlineData("[[Guide|Gui")]
    [InlineData("[[Guide#Gui")]
    [InlineData("```md\n[[Gui")]
    [InlineData("::: mermaid\n[[Gui")]
    [InlineData("<!-- [[Gui")]
    public void Automatic_completion_does_not_capture_unrelated_or_literal_input(string source)
        => Assert.True(WikiLinkCompletionService.Query(source, source.Length, Catalogue) is null,
            string.Join("; ", new MarkdownService().Parse(source).Syntax.Descendants().Select(node => node.GetType().Name + ": " + node.Span)));
    [Fact] public void Completion_stays_outside_inline_code_and_standard_links()
    {
        foreach (var source in new[] { "`[[Gui]]`", "[[[Gui]]](https://example.test)" })
            Assert.Null(WikiLinkCompletionService.Query(source, source.IndexOf("Gui", StringComparison.Ordinal) + 3, Catalogue, true));
    }
    [Fact] public void Explicit_empty_completion_and_duplicate_names_show_distinct_paths()
    {
        Assert.Equal(5, WikiLinkCompletionService.Query("[[", 2, Catalogue, true)!.Paths.Count);
        Assert.Equal(new[] { "one/Summary.md", "two/Summary.md" }, WikiLinkCompletionService.Query("[[Summary", 9, Catalogue)!.Paths);
        Assert.Null(WikiLinkCompletionService.Plan("[[Gui", 5, Catalogue, "../private.md"));
    }
    [Fact] public void Unicode_spaces_and_reserved_filename_characters_round_trip()
    {
        var path = "docs/Café [100%] #review.md"; var index = new WikiLinkIndex([path]);
        const string source = "[[Café"; var edit = WikiLinkCompletionService.Plan(source, source.Length, index, path)!;
        var result = Apply(source, edit); var link = Assert.Single(WikiLinks.Read(result));
        Assert.Equal(path, index.Resolve(link.Target, "README.md").Path);
        Assert.Contains("Café ", result);
    }
    [Fact] public void Completion_bounds_source_and_result_count()
    {
        var source = new string('a', MermaidCompletionService.AutomaticLimit) + "\n[[Gui";
        Assert.Null(WikiLinkCompletionService.Query(source, source.Length, Catalogue));
        var index = new WikiLinkIndex(Enumerable.Range(0, 150).Select(i => "Guide" + i + ".md"));
        Assert.Equal(80, WikiLinkCompletionService.Query("[[Gui", 5, index)!.Paths.Count);
    }
    private static string Apply(string source, MarkdownEdit edit) => source.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.Replacement);
}
