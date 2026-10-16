using LensDocsStudio.Core.Editing;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LensDocsStudio.Core.Tests;

public sealed class LibrarySnippetSourceTests
{
    [Theory] [InlineData("\n")] [InlineData("\r\n")]
    public void Selection_retains_needed_reference_definitions_without_unrelated_source(string newline)
    {
        const string selected = "![Logo][brand] and [guide][docs]";
        var source = "# Unselected" + newline + newline + selected + newline + newline
            + "[brand]: assets/logo.png \"Logo title\"" + newline + "[docs]: docs/guide.md" + newline + "[unused]: private.md" + newline;
        var result = LibrarySnippetSource.Select(source, source.IndexOf(selected, StringComparison.Ordinal), selected.Length);
        Assert.StartsWith(selected + newline + newline, result);
        Assert.DoesNotContain("Unselected", result); Assert.DoesNotContain("private.md", result);
        var links = new MarkdownService().Parse(result).Syntax.Descendants<LinkInline>().ToArray();
        Assert.Equal(2, links.Length); Assert.Equal("assets/logo.png", links[0].Url); Assert.Equal("Logo title", links[0].Title); Assert.Equal("docs/guide.md", links[1].Url);
    }
    [Fact] public void Repeated_references_are_included_once_and_definitions_already_selected_are_unchanged()
    {
        const string selected = "![First][logo] ![Second][logo]";
        const string source = selected + "\n\n[logo]: logo.png\n";
        var snippet = LibrarySnippetSource.Select(source, 0, selected.Length);
        Assert.Equal(1, snippet.Split("[logo]:", StringSplitOptions.None).Length - 1);
        Assert.Equal(source, LibrarySnippetSource.Select(source, 0, source.Length));
    }
    [Fact] public void Ordinary_snippets_keep_exact_whitespace_Unicode_and_editable_diagrams()
    {
        const string value = "  Café\r\n\r\n```mermaid\r\nflowchart LR\r\nA --> B\r\n```\r\n";
        Assert.Equal(value, LibrarySnippetSource.Select("Before\r\n" + value + "After", 8, value.Length));
    }
    [Fact] public void Invalid_or_oversized_source_selections_are_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LibrarySnippetSource.Select("text", -1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => LibrarySnippetSource.Select("text", 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => LibrarySnippetSource.Select("text", 1, 4));
        Assert.Throws<IOException>(() => LibrarySnippetSource.Select(new string('x', 4 * 1024 * 1024 + 1), 0, 1));
    }
}
