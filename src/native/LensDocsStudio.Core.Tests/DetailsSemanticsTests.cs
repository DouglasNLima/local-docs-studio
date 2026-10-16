using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using LensDocsStudio.Core.Exports;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using AngleSharp.Html.Parser;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Core.Tests;

public sealed class DetailsSemanticsTests
{
    private const string ExportSource = "# Details export\n\n<details data-details-block>\n<summary>Café &amp; evidence</summary>\n\n## Inside\n\nRetained **bold** and [guide](guide.md).\n\n| Item | Result |\n|---|---|\n| Hidden row | Ready |\n</details>\n\n<details open>\n<summary>Open section</summary>\n\nVisible body.\n</details>";
    private static ExportContent Content(string source) => new(new MarkdownService().Parse(source), new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>());

    [Fact] public void HTML_details_preserve_labels_open_state_headings_tables_and_safe_links()
    {
        var html = new HtmlParser().ParseDocument(new HtmlExporter().Export(Content(ExportSource)));
        var sections = html.QuerySelectorAll("details"); Assert.Equal(2, sections.Length);
        Assert.False(sections[0].HasAttribute("open")); Assert.True(sections[1].HasAttribute("open"));
        Assert.Equal("Café & evidence", sections[0].QuerySelector("summary")!.TextContent);
        Assert.Equal("inside", sections[0].QuerySelector("h2")!.Id); Assert.Contains("Hidden row", sections[0].TextContent);
        Assert.Equal("guide.md", sections[0].QuerySelector("a")!.GetAttribute("href")); Assert.Single(sections[0].QuerySelectorAll("table"));
        var encoded = new HtmlExporter().RenderFragment(Content("<details>\n<summary>&lt;script&gt;literal&lt;/script&gt;</summary>\n\nBody\n</details>"));
        Assert.Empty(new HtmlParser().ParseDocument(encoded).QuerySelectorAll("script")); Assert.Contains("&lt;script&gt;literal", encoded);
    }

    [Fact] public void Clipboard_expands_details_into_passive_content_with_all_text_and_literal_table_cells()
    {
        var result = new ClipboardExporter().Export(Content(ExportSource)); var html = new HtmlParser().ParseDocument(HtmlClipboardFormat.Decode(result.Html));
        Assert.Empty(html.QuerySelectorAll("details,summary")); Assert.Equal(2, html.QuerySelectorAll("section.document-details").Length);
        Assert.Contains("Café & evidence", result.PlainText); Assert.Contains("Hidden row\tReady", result.PlainText); Assert.Contains("Visible body.", result.PlainText);
        Assert.Contains(html.QuerySelectorAll("strong"), element => element.TextContent == "bold");
    }

    [Fact] public void Word_exports_complete_closed_sections_with_summary_bold_headings_links_and_tables()
    {
        using var package = WordprocessingDocument.Open(new MemoryStream(new DocxExporter().Export(Content("[Jump to inside](#inside)\n\n" + ExportSource))), false);
        Assert.Empty(new OpenXmlValidator().Validate(package)); var body = package.MainDocumentPart!.Document!.Body!;
        Assert.Contains("Café & evidence", body.InnerText); Assert.Contains("Hidden row", body.InnerText); Assert.Contains("Visible body.", body.InnerText);
        Assert.DoesNotContain("<details", body.InnerText); Assert.Single(body.Descendants<W.Table>());
        Assert.Contains(body.Descendants<W.Run>(), run => run.InnerText == "Café & evidence" && run.RunProperties?.Bold is not null);
        var target = Assert.Single(body.Descendants<W.Paragraph>().Single(paragraph => paragraph.InnerText == "Inside").Descendants<W.BookmarkStart>());
        Assert.Contains(body.Descendants<W.Hyperlink>(), link => link.Anchor?.Value == target.Name!.Value);
    }

    [Theory] [InlineData("\n")] [InlineData("\r\n")]
    public void Complete_details_retain_label_nested_Markdown_links_and_exact_original_source_positions(string newline)
    {
        var source = "# Before\n\n<details data-details-block>\n  <summary>Café &amp; evidence</summary>\n\n## Inside\n\n**Retained** [guide](docs/guide.md).\n\n![Logo](assets/logo.png)\n</details>\n\n# After".Replace("\n", newline);
        var document = new MarkdownService().Parse(source); var details = Assert.Single(document.Syntax.Descendants<DocumentDetailsBlock>());
        Assert.Equal("Café & evidence", details.Summary); Assert.False(details.InitiallyExpanded);
        Assert.Equal(source.IndexOf("<details", StringComparison.Ordinal), details.Span.Start);
        Assert.Equal(source.IndexOf("</details>", StringComparison.Ordinal) + 10, details.Span.End + 1);
        Assert.Equal(new[] { "Before", "Inside", "After" }, document.Headings.Select(heading => heading.Title));
        var heading = document.Headings.Single(heading => heading.Title == "Inside"); Assert.Equal(source.IndexOf("## Inside", StringComparison.Ordinal), heading.SourceStart);
        Assert.Contains(details.Descendants<EmphasisInline>(), inline => inline.DelimiterCount == 2);
        var links = MarkdownLinks.Read(source); Assert.Equal(2, links.Count);
        Assert.All(links, link => Assert.Equal(link.Reference, source.Substring(link.Start, link.Length)));
        var guide = links.Single(link => !link.IsImage);
        Assert.Equal(source.Replace("docs/guide.md", "docs/renamed.md"), MarkdownLinks.Rewrite(source, [(guide, "docs/renamed.md")]));
    }

    [Fact] public void Nested_open_sections_are_distinct_and_do_not_swallow_following_content()
    {
        const string source = "<details open data-details-block>\n<summary>Outer</summary>\n\nBefore\n\n<details>\n<summary>Inner</summary>\n\nInside\n</details>\n\nAfter inner\n</details>\n\nOutside";
        var document = new MarkdownService().Parse(source); var sections = document.Syntax.Descendants<DocumentDetailsBlock>().ToArray();
        Assert.Equal(2, sections.Length); Assert.True(sections[0].InitiallyExpanded); Assert.Equal("Inner", sections[1].Summary);
        Assert.Equal(sections[0], sections[1].Parent); Assert.IsType<ParagraphBlock>(document.Syntax.Last());
        Assert.Equal("Outside", MarkdownService.InlineText(((ParagraphBlock)document.Syntax.Last()).Inline));
    }

    [Theory] [InlineData("<details onclick=\"alert(1)\">")] [InlineData("<details style=\"display:none\">")]
    [InlineData("<details open open>")] [InlineData("<details data-details-block data-details-block>")]
    public void Extra_or_duplicate_attributes_remain_inert_source(string opening)
    {
        var document = new MarkdownService().Parse(opening + "\n<summary>Label</summary>\n\nBody\n</details>");
        Assert.Empty(document.Syntax.Descendants<DocumentDetailsBlock>()); Assert.IsType<HtmlBlock>(document.Syntax[0]);
    }

    [Theory] [InlineData("<summary><strong>Markup</strong></summary>")] [InlineData("<summary></summary>")]
    [InlineData("<summary>Bad&#0;label</summary>")] [InlineData("<summary onclick='x'>Label</summary>")]
    public void Unsupported_summaries_remain_visible_HTML_source(string summary)
    {
        var document = new MarkdownService().Parse("<details>\n" + summary + "\n\nBody\n</details>");
        Assert.Empty(document.Syntax.Descendants<DocumentDetailsBlock>());
    }

    [Fact] public void Unclosed_details_and_literal_examples_never_become_collapsible_sections()
    {
        const string unclosed = "<details>\n<summary>Unfinished</summary>\n\nKeep source";
        var parser = new MarkdownService(); Assert.Empty(parser.Parse(unclosed).Syntax.Descendants<DocumentDetailsBlock>());
        Assert.Empty(parser.Parse("```html\n" + unclosed + "\n</details>\n```").Syntax.Descendants<DocumentDetailsBlock>());
        Assert.Empty(parser.Parse("<!--\n" + unclosed + "\n</details>\n-->").Syntax.Descendants<DocumentDetailsBlock>());
    }

    [Fact] public void Closing_tags_inside_fences_and_comments_do_not_end_the_outer_section()
    {
        const string source = "<details>\n<summary>Examples</summary>\n\n```html\n</details>\n```\n\n<!--\n</details>\n-->\n\n## Retained heading\n</details>";
        var document = new MarkdownService().Parse(source); var details = Assert.Single(document.Syntax.Descendants<DocumentDetailsBlock>());
        Assert.Equal(source.Length - 1, details.Span.End); Assert.Single(details.Descendants<HeadingBlock>());
        Assert.Equal("</details>", Assert.Single(details.Descendants<FencedCodeBlock>()).Lines.ToString());
    }

    [Fact] public void Reference_definitions_outside_details_still_resolve_inside_them()
    {
        const string source = "<details>\n<summary>Reference links</summary>\n\n[Guide][guide]\n</details>\n\n[guide]: docs/guide.md";
        var link = Assert.Single(MarkdownLinks.Read(source)); Assert.Equal("docs/guide.md", source.Substring(link.Start, link.Length));
        Assert.Single(new MarkdownService().Parse(source).Syntax.Descendants<DocumentDetailsBlock>());
    }

    [Fact] public void Oversized_sections_and_labels_stay_as_source()
    {
        var parser = new MarkdownService();
        Assert.Empty(parser.Parse("<details>\n<summary>" + new string('x', 513) + "</summary>\nBody\n</details>").Syntax.Descendants<DocumentDetailsBlock>());
        Assert.Empty(parser.Parse("<details>\n<summary>Large</summary>\n\n" + new string('x', 1024 * 1024) + "\n</details>").Syntax.Descendants<DocumentDetailsBlock>());
    }
}
