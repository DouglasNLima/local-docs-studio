using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using LensDocsStudio.Core.Editing;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;

namespace LensDocsStudio.Core.Tests;

public sealed class TableOfContentsTests
{
    private const string Source = "# Root\r\n\r\n## Again\r\n\r\n### Café **bold**\r\n\r\n#### Four\r\n\r\n##### Five\r\n\r\n###### Six\r\n\r\n## Again\r\n\r\n```md\r\n# Excluded\r\n```\r\n\r\n<details>\r\n<summary>Hidden</summary>\r\n## Inside\r\n</details>";

    [Fact]
    public void Copied_targets_resolve_to_actual_exported_identifiers_for_all_levels_and_duplicate_Unicode_headings()
    {
        var parsed = new MarkdownService().Parse(Source);
        var toc = TableOfContentsService.Create(parsed.Headings);
        Assert.Equal(8, parsed.Headings.Count);
        Assert.Equal(Enumerable.Range(1, 6), parsed.Headings.Take(6).Select(h => h.Level));
        Assert.Equal(8, parsed.Headings.Select(h => h.Anchor).Distinct().Count());
        var html = new HtmlParser().ParseDocument(new HtmlExporter().RenderFragment(new ExportContent(parsed,
            new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>())));
        var fragments = Regex.Matches(toc, @"\]\(#([^)]+)\)").Select(m => Uri.UnescapeDataString(m.Groups[1].Value)).ToArray();
        Assert.Equal(parsed.Headings.Select(h => h.Anchor), fragments);
        Assert.All(fragments, id => Assert.NotNull(html.GetElementById(id)));
        Assert.DoesNotContain("Excluded", toc);
        Assert.Contains("Café bold", toc);
        Assert.Equal(Source, parsed.Source);
    }

    [Theory]
    [InlineData(TableOfContentsStyle.Bullets, "- [A](#a)\n  - [B](#b)\n- [C](#c)")]
    [InlineData(TableOfContentsStyle.Numbered, "1. [A](#a)\n   1. [B](#b)\n1. [C](#c)")]
    [InlineData(TableOfContentsStyle.Plain, "A\n  B\nC")]
    public void Normalisation_retains_hierarchy_when_levels_are_skipped_or_out_of_order(TableOfContentsStyle style, string expected)
    {
        DocumentHeading[] headings = [new("A", "a", 3, 0, 0), new("B", "b", 6, 10, 1), new("C", "c", 2, 20, 2)];
        Assert.Equal(expected, TableOfContentsService.Create(headings, new(Style: style)));
        var unnormalised = TableOfContentsService.Create(headings, new(Style: style, Normalise: false));
        var width = style == TableOfContentsStyle.Numbered ? 3 : 2;
        Assert.StartsWith(new string(' ', 2 * width), unnormalised);
        Assert.Contains("\n" + new string(' ', 5 * width), unnormalised);
        if (style != TableOfContentsStyle.Plain)
        {
            var parsed = new MarkdownService().Parse(expected);
            var html = new HtmlParser().ParseDocument(new HtmlExporter().RenderFragment(new ExportContent(parsed,
                new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>())));
            Assert.Equal("B", html.QuerySelector("li li a")!.TextContent);
        }
    }

    [Fact]
    public void Level_filter_and_details_wrapper_do_not_include_unselected_headings()
    {
        var headings = new MarkdownService().Parse(Source).Headings;
        var toc = TableOfContentsService.Create(headings, new(5, 6, Details: true));
        Assert.StartsWith("<details>\n<summary>Table of Contents</summary>\n\n- [Five]", toc);
        Assert.EndsWith("\n\n</details>", toc);
        Assert.DoesNotContain("Root", toc);
        Assert.Equal("", TableOfContentsService.Create([]));
    }

    [Fact]
    public void Copied_labels_escape_Markdown_and_fragments_encode_Unicode()
    {
        DocumentHeading[] headings = [new("A [label] *value*", "révision:一", 1, 0, 0)];
        Assert.Equal("- [A \\[label\\] \\*value\\*](#r%C3%A9vision%3A%E4%B8%80)", TableOfContentsService.Create(headings));
    }

    [Theory]
    [InlineData(0, 6)] [InlineData(1, 7)] [InlineData(5, 2)]
    public void Invalid_ranges_are_rejected_without_partial_output(int start, int end) =>
        Assert.Throws<ArgumentException>(() => TableOfContentsService.Create([], new(start, end)));

    [Fact]
    public void Every_punctuation_label_round_trips_as_text_and_fragments_remain_parser_owned()
    {
        const string label = "A \\ [x] ![image] &copy; <b> *x* _y_ `z` {#id} | ~ ^ $ = + : (one)";
        DocumentHeading[] headings = [new(label, "café:一%#?&/[]()", 1, 0, 0)];
        var toc = TableOfContentsService.Create(headings);
        var parsed = new MarkdownService().Parse(toc);
        var html = new HtmlParser().ParseDocument(new HtmlExporter().RenderFragment(new ExportContent(parsed,
            new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>())));
        Assert.Equal(label, html.QuerySelector("a")!.TextContent);
        Assert.Empty(html.QuerySelectorAll("img, em, strong, code, b"));
        Assert.Contains("#caf%C3%A9%3A%E4%B8%80%25%23%3F%26%2F%5B%5D%28%29", toc);
    }

    [Fact]
    public void Containers_and_code_follow_the_current_parser_and_all_export_targets_match()
    {
        const string source = "# Visible\n\n:::mermaid\n# Not a heading\n:::\n\n~~~md\n## Also excluded\n~~~\n\n::: note\n### Container\n:::\n\nSetext\n------\n\n# Café {#custom:一}\n\n# Café {#custom:一}";
        var parsed = new MarkdownService().Parse(source);
        var toc = TableOfContentsService.Create(parsed.Headings);
        Assert.DoesNotContain("excluded", toc); Assert.DoesNotContain("Not a heading", toc);
        Assert.Contains("Container", toc); Assert.Contains("Setext", toc);
        var diagramOffset = source.IndexOf(":::mermaid", StringComparison.Ordinal);
        var html = new HtmlParser().ParseDocument(new HtmlExporter().RenderFragment(new ExportContent(parsed,
            new Dictionary<int, ExportImage> { [diagramOffset] = new([1], 1, 1, "Excluded code") }, new Dictionary<string, ExportImage>())));
        Assert.All(parsed.Headings, heading =>
        {
            Assert.Contains("](#" + Uri.EscapeDataString(heading.Anchor) + ")", toc);
            Assert.Equal(heading.Title, html.GetElementById(heading.Anchor)!.TextContent);
        });
        Assert.Equal(source, parsed.Source);
    }

    [Fact]
    public void Large_source_keeps_last_heading_and_preserves_every_character()
    {
        var source = "# First\r\n\r\n```text\r\n" + new string('x', 4 * 1024 * 1024 - 100) + "\r\n```\r\n\r\n###### Last café";
        var parsed = new MarkdownService().Parse(source);
        var toc = TableOfContentsService.Create(parsed.Headings);
        Assert.Equal(2, parsed.Headings.Count); Assert.Contains("Last café", toc);
        Assert.Equal(source, parsed.Source);
    }

    [Fact]
    public void Many_headings_are_complete_and_cancellation_and_output_limits_are_enforced()
    {
        var source = string.Concat(Enumerable.Range(0, 10_000).Select(i => $"## Heading {i}\n"));
        var parsed = new MarkdownService().Parse(source);
        Assert.Equal(10_000, TableOfContentsService.Create(parsed.Headings).Split('\n').Length);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => TableOfContentsService.Create(parsed.Headings, cancellationToken: cancelled.Token));
        var oversized = new DocumentHeading(new string('x', 1000), "anchor", 1, 0, 0);
        Assert.Throws<IOException>(() => TableOfContentsService.Create(Enumerable.Repeat(oversized, 17_000).ToArray()));
    }

    [Fact]
    public void Empty_or_standalone_diagram_documents_do_not_create_an_empty_wrapper()
    {
        Assert.Equal("", TableOfContentsService.Create(new MarkdownService().Parse("").Headings, new(Details: true)));
        Assert.Equal("", TableOfContentsService.Create(new MarkdownService().Parse("# Diagram comment\nflowchart LR\nA-->B", true).Headings));
        Assert.Throws<ArgumentException>(() => TableOfContentsService.Create([], new(Style: (TableOfContentsStyle)99)));
    }
}
