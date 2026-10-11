using AngleSharp.Html.Parser;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Core.Parsing;
using Markdig;
using Markdig.Syntax;

namespace LensDocsStudio.Core.Tests;

public sealed class ClipboardPasteTests
{
    private static AngleSharp.Dom.IDocument Render(string markdown) => new HtmlParser().ParseDocument(Markdown.ToHtml(markdown, new MarkdownService().Pipeline));

    [Fact] public void HTML_literal_Markdown_punctuation_stays_literal_after_conversion()
    {
        const string literal = "# A [link](https://example.com) *text* & <safe> | ~x~";
        var imported = new HtmlMarkdownConverter().Convert("<p># A [link](https://example.com) *text* &amp; &lt;safe&gt; | ~x~</p>");
        var html = Render(imported.Markdown);
        Assert.Equal(literal, html.QuerySelector("p")!.TextContent);
        Assert.Empty(html.QuerySelectorAll("h1,a,em,safe"));
    }

    [Fact] public void Nested_lists_preserve_indentation_and_ordered_start()
    {
        var result = new HtmlMarkdownConverter().Convert("<ol start='3'><li>First<ul><li>Child A</li><li>Child B</li></ul></li><li>Second</li></ol>");
        var html = Render(result.Markdown);
        Assert.Equal("3", html.QuerySelector("ol")!.GetAttribute("start"));
        Assert.Equal(2, html.QuerySelectorAll("ol > li").Length);
        Assert.Equal(2, html.QuerySelectorAll("ol > li > ul > li").Length);
        Assert.Contains("Second", html.QuerySelectorAll("ol > li")[1].TextContent);
    }

    [Fact] public void Inline_and_block_code_preserve_backticks_whitespace_and_blank_lines()
    {
        const string code = "first\n```\n\n\nlast\n";
        var result = new HtmlMarkdownConverter().Convert("<p><code>` a `` b `</code></p><pre>" + code + "</pre>");
        var html = Render(result.Markdown);
        Assert.Equal("` a `` b `", html.QuerySelector("p code")!.TextContent);
        Assert.Equal(code, html.QuerySelector("pre code")!.TextContent);
    }

    [Fact] public void Merged_table_cells_expand_without_shifting_following_cells_or_duplicating_content()
    {
        var result = new HtmlMarkdownConverter().Convert("<table><tr><th rowspan='2'>A</th><th colspan='2'>B</th></tr><tr><td>C</td><td>D</td></tr><tr><td>E</td><td><code>a|b</code></td><td>F</td></tr></table>");
        var html = Render(result.Markdown);
        var rows = html.QuerySelectorAll("tr");
        Assert.Equal(3, rows.Length); Assert.All(rows, row => Assert.Equal(3, row.Children.Length));
        Assert.Equal(new[] { "", "C", "D" }, rows[1].Children.Select(c => c.TextContent));
        Assert.Equal("a|b", rows[2].Children[1].TextContent);
        Assert.Contains(result.Warnings, warning => warning.Contains("Merged"));
    }

    [Fact] public void Active_HTML_and_unsafe_links_cannot_become_Markdown_resources()
    {
        var result = new HtmlMarkdownConverter().Convert("<script>secret</script><a href='javascript:alert(1)'>Keep text</a><img src='file:///C:/private.png' alt='Private'><p onclick='alert(2)'>Visible</p><template>Hidden</template>");
        var html = Render(result.Markdown);
        Assert.Empty(html.QuerySelectorAll("script,img,a,template,[onclick]"));
        Assert.Contains("Keep text", html.Body!.TextContent); Assert.Contains("Visible", html.Body.TextContent);
        Assert.DoesNotContain("secret", result.Markdown); Assert.DoesNotContain("private.png", result.Markdown); Assert.DoesNotContain("Hidden", result.Markdown);
        Assert.Equal(2, result.Warnings.Count);
    }

    [Fact] public void Encoded_links_and_duplicate_embedded_images_keep_their_identity()
    {
        var result = new HtmlMarkdownConverter().Convert("<a href='notes%20one.md#part'>Notes</a><img src='data:image/png;base64,AQID' alt='A'><img src='data:image/png;base64,AQID' alt='B'>");
        var asset = Assert.Single(result.Assets);
        var html = Render(result.Markdown);
        Assert.Equal("notes%20one.md#part", html.QuerySelector("a")!.GetAttribute("href"));
        Assert.All(html.QuerySelectorAll("img"), image => Assert.Equal(asset.Reference, image.GetAttribute("src")));
        Assert.Equal(new[] { "A", "B" }, html.QuerySelectorAll("img").Select(image => image.GetAttribute("alt")));
    }

    [Fact] public void HTML_complexity_table_expansion_and_cancelled_work_are_bounded()
    {
        var converter = new HtmlMarkdownConverter();
        Assert.Throws<InvalidDataException>(() => converter.Convert("<table><tr><td rowspan='10000' colspan='256'>Large</td></tr></table>"));
        Assert.Throws<OperationCanceledException>(() => converter.Convert("<p>Cancelled</p>", new(true)));
        var malformed = converter.Convert("<img src='data:image/png;base64,A=' alt='Unavailable'>");
        Assert.Empty(malformed.Assets); Assert.Single(malformed.Warnings); Assert.Contains("Unavailable", malformed.Markdown);
    }

    [Theory]
    [InlineData(PasteMode.Text, "first\nsecond")]
    [InlineData(PasteMode.Code, "```\nfirst\nsecond\n```")]
    [InlineData(PasteMode.Quote, "> first\n> second")]
    [InlineData(PasteMode.List, "- first\n- second")]
    [InlineData(PasteMode.Checklist, "- [ ] first\n- [ ] second")]
    [InlineData(PasteMode.NumberedList, "1. first\n2. second")]
    [InlineData(PasteMode.Mermaid, "```mermaid\nfirst\nsecond\n```")]
    public void Explicit_paste_modes_produce_the_selected_source_format(PasteMode mode, string expected)
    {
        var result = new ClipboardPasteService().Convert(new("first\r\nsecond", ""), mode);
        Assert.Equal(expected, result.Document.Markdown); Assert.Equal(mode != PasteMode.Text, result.Block); Assert.Empty(result.Document.Assets);
    }

    [Fact] public void Auto_paste_preserves_Markdown_carried_with_rich_HTML()
    {
        const string source = "# Source\n\n[Local](notes.md)";
        var result = new ClipboardPasteService().Convert(new(source, HtmlClipboardFormat.Encode("<p>Different rendered content</p>")), PasteMode.Auto);
        Assert.Equal(source, result.Document.Markdown); Assert.False(result.Block);
    }

    [Fact] public void Auto_paste_decodes_CF_HTML_and_preserves_surrounding_table_content()
    {
        var result = new ClipboardPasteService().Convert(new("Plain fallback", HtmlClipboardFormat.Encode("<h2>Café</h2><p>Before</p><table><tr><th>A</th><th>B</th></tr><tr><td>1</td><td>2</td></tr></table><p>After</p>")), PasteMode.Auto);
        var html = Render(result.Document.Markdown);
        Assert.Equal("Café", html.QuerySelector("h2")!.TextContent); Assert.Single(html.QuerySelectorAll("table"));
        Assert.Contains("Before", html.Body!.TextContent); Assert.Contains("After", html.Body.TextContent);
    }

    [Fact] public void Spreadsheet_TSV_handles_quoted_newlines_quotes_empty_cells_and_literal_Markdown()
    {
        var result = new ClipboardPasteService().Convert(new("Name\tDetails\tOther\nCafé\t\"Line 1\n\"\"quoted\"\" | *value*\"\t", ""), PasteMode.Auto);
        var html = Render(result.Document.Markdown);
        Assert.Equal(3, html.QuerySelectorAll("tbody td").Length);
        Assert.Equal("Line 1\"quoted\" | *value*", html.QuerySelectorAll("tbody td")[1].TextContent);
        Assert.Empty(html.QuerySelectorAll("em")); Assert.Single(html.QuerySelectorAll("br"));
    }

    [Fact] public void CSV_is_explicit_and_unfinished_quoted_cells_are_reported()
    {
        var service = new ClipboardPasteService(); const string csv = "A,B\n1,2";
        Assert.Equal(csv, service.Convert(new(csv, ""), PasteMode.Auto).Document.Markdown);
        Assert.Single(Render(service.Convert(new(csv, ""), PasteMode.Table).Document.Markdown).QuerySelectorAll("table"));
        Assert.Throws<IOException>(() => service.Convert(new("A\tB\n1\t\"unfinished", ""), PasteMode.Table));
        Assert.Equal("A\tB\n1\t\"unfinished", service.Convert(new("A\tB\n1\t\"unfinished", ""), PasteMode.Text).Document.Markdown);
    }

    [Fact] public void HTML_only_text_paste_retains_paragraphs_without_active_subtrees()
    {
        var result = new ClipboardPasteService().Convert(new("", "<p>First</p><script>Hidden</script><p>Second<br>Third</p>"), PasteMode.Text);
        Assert.Equal("First\nSecond\nThird", result.Document.Markdown);
    }

    [Fact] public void Forced_text_preserves_whitespace_and_does_not_parse_malformed_HTML()
    {
        var result = new ClipboardPasteService().Convert(new(" \t\n  ", "Version:1.0\nStartFragment:99999"), PasteMode.Text);
        Assert.Equal(" \t\n  ", result.Document.Markdown); Assert.False(result.Block);
    }

    [Fact] public void Rich_paste_reports_invalid_CF_HTML_instead_of_pasting_unselected_context()
    {
        Assert.Throws<IOException>(() => new ClipboardPasteService().Convert(new("Plain fallback", "Version:1.0\nStartFragment:0\nEndFragment:1\n<p>Unselected</p>"), PasteMode.Auto));
    }

    [Theory]
    [InlineData("::: mermaid\nflowchart LR\n A --> B\n:::")]
    [InlineData("````mermaid\nflowchart LR\n A --> B\n````")]
    public void Mermaid_paste_unwraps_existing_fences(string source)
    {
        var result = new ClipboardPasteService().Convert(new(source, ""), PasteMode.Mermaid);
        var code = Assert.Single(new MarkdownService().Parse(result.Document.Markdown).Syntax.Descendants<FencedCodeBlock>());
        Assert.Equal("mermaid", code.Info); Assert.Equal("flowchart LR\n A --> B", code.Lines.ToString());
    }
}
