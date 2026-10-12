using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Core.Tests;

public sealed class WordBlockImportTests
{
    [Theory]
    [InlineData("````text\nCafé <tag> &copy;\n\t**literal**\n```\n````")]
    [InlineData("- ```text\n  First\n  \tSecond\n  ```\n\n  Continuation\n\n- Next")]
    [InlineData("```text\r\nFirst\r\n\tSecond\r\n```")]
    public void Exported_code_blocks_reimport_with_literal_text_and_list_placement(string source)
    {
        var before = new MarkdownService().Parse(source);
        var result = RoundTrip(source);
        var after = new MarkdownService().Parse(result.Markdown);
        var expected = Assert.Single(before.Syntax.Descendants<CodeBlock>());
        var actual = Assert.Single(after.Syntax.Descendants<CodeBlock>());
        Assert.Equal(expected.Lines.ToString().Replace("\r\n", "\n", StringComparison.Ordinal), actual.Lines.ToString());
        Assert.Equal(expected.Parent?.GetType(), actual.Parent?.GetType());
        Assert.Equal(before.Syntax.Descendants<ListItemBlock>().Count(), after.Syntax.Descendants<ListItemBlock>().Count());
        Assert.Empty(result.Warnings);
    }

    [Theory]
    [InlineData("> First **bold** Café\n>\n> Second [Reference](https://example.org/review)\n\nAfterQuote")]
    [InlineData("7. > First **bold** Café\n   >\n   > Second [Reference](https://example.org/review)\n\n   Continuation\n\n8. Next")]
    public void Consecutive_quote_paragraphs_keep_one_quote_and_formatted_content(string source)
    {
        var before = new MarkdownService().Parse(source);
        var result = RoundTrip(source);
        var after = new MarkdownService().Parse(result.Markdown);
        var quote = Assert.Single(after.Syntax.Descendants<QuoteBlock>());
        Assert.Equal(2, quote.Count);
        Assert.Equal(Assert.Single(before.Syntax.Descendants<QuoteBlock>()).Parent?.GetType(), quote.Parent?.GetType());
        Assert.Equal("First bold Café", MarkdownService.InlineText(Assert.IsType<ParagraphBlock>(quote[0]).Inline));
        Assert.Equal("https://example.org/review", Assert.Single(quote.Descendants<LinkInline>()).Url);
        Assert.Equal(before.Syntax.Descendants<ListItemBlock>().Count(), after.Syntax.Descendants<ListItemBlock>().Count());
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Explicit_style_inheritance_preserves_code_quote_and_heading_semantics()
    {
        var styles = new W.Styles(Style("Code"), Style("Quote"), Style("Heading3"), Style("CustomCode", "Code"), Style("CustomQuote", "Quote"), Style("CustomHeading", "Heading3"));
        var result = new DocumentImportService().ImportDocx(Package(styles,
            Paragraph("CustomCode", "a < b && *literal*"), Paragraph("CustomQuote", "Quoted"), Paragraph("CustomHeading", "Heading")));
        var parsed = new MarkdownService().Parse(result.Markdown);
        Assert.Equal("a < b && *literal*", Assert.Single(parsed.Syntax.Descendants<CodeBlock>()).Lines.ToString());
        Assert.Single(parsed.Syntax.Descendants<QuoteBlock>());
        Assert.Equal(3, Assert.Single(parsed.Headings).Level);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Generic_spacing_styles_are_not_guessed_to_be_code()
    {
        var result = new DocumentImportService().ImportDocx(Package(new W.Styles(Style("NoSpacing")), Paragraph("NoSpacing", "Ordinary text")));
        Assert.IsType<ParagraphBlock>(Assert.Single(new MarkdownService().Parse(result.Markdown).Syntax));
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Style_cycles_terminate_with_readable_text_and_one_warning()
    {
        var result = new DocumentImportService().ImportDocx(Package(new W.Styles(Style("A", "B"), Style("B", "A")),
            Paragraph("A", "First"), Paragraph("B", "Second")));
        Assert.Contains("First", result.Markdown); Assert.Contains("Second", result.Markdown);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void Bookmarks_point_before_literal_code_without_becoming_code_characters()
    {
        var code = Paragraph("Code", "value < 2");
        code.InsertAfter(new W.BookmarkStart { Name = "CodeTarget", Id = "1" }, code.ParagraphProperties);
        code.Append(new W.BookmarkEnd { Id = "1" });
        var result = new DocumentImportService().ImportDocx(Package(new W.Styles(Style("Code")),
            new W.Paragraph(new W.Hyperlink(new W.Run(new W.Text("Go"))) { Anchor = "CodeTarget" }), code));
        var parsed = new MarkdownService().Parse(result.Markdown);
        var block = Assert.Single(parsed.Syntax.Descendants<CodeBlock>());
        Assert.Equal("value < 2", block.Lines.ToString());
        var target = DocumentAnchors.Find(parsed, Assert.Single(parsed.Syntax.Descendants<LinkInline>()).Url![1..]);
        Assert.NotNull(target); Assert.True(target.SourceStart < block.Span.Start);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Code_and_quotes_in_table_cells_keep_literal_text_and_column_boundaries()
    {
        var table = new W.Table(new W.TableRow(new W.TableCell(Paragraph("Code", "a | b <tag>")), new W.TableCell(Paragraph("Quote", "Quoted"))),
            new W.TableRow(new W.TableCell(Paragraph("Code", "value\\path")), new W.TableCell(new W.Paragraph(new W.Run(new W.Text("Body"))))));
        var result = new DocumentImportService().ImportDocx(Package(new W.Styles(Style("Code"), Style("Quote")), table));
        var imported = Assert.Single(new MarkdownService().Parse(result.Markdown).Syntax.OfType<Markdig.Extensions.Tables.Table>());
        Assert.All(imported.OfType<Markdig.Extensions.Tables.TableRow>(), row => Assert.Equal(2, row.Count));
        var firstCell = Assert.IsType<Markdig.Extensions.Tables.TableCell>(Assert.IsType<Markdig.Extensions.Tables.TableRow>(imported[0])[0]);
        Assert.Equal("a | b <tag>", MarkdownService.InlineText(Assert.IsType<ParagraphBlock>(Assert.Single(firstCell)).Inline));
        Assert.Contains(result.Warnings, w => w.Contains("code blocks inside table cells", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, w => w.Contains("quotes inside table cells", StringComparison.Ordinal));
    }

    [Fact]
    public void Code_styled_images_are_kept_as_images_with_a_conversion_note()
    {
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aX1cAAAAASUVORK5CYII=");
        var bytes = new DocxExporter().Export(new(new MarkdownService().Parse("![Image](image.png)"), new Dictionary<int, ExportImage>(),
            new Dictionary<string, ExportImage> { ["image.png"] = new(png, 1, 1, "Image") }));
        using var stream = new MemoryStream(); stream.Write(bytes);
        using (var package = WordprocessingDocument.Open(stream, true))
            package.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>().First().ParagraphProperties!.ParagraphStyleId = new W.ParagraphStyleId { Val = "Code" };
        var result = new DocumentImportService().ImportDocx(stream.ToArray());
        Assert.Equal(png, Assert.Single(result.Assets).Bytes);
        Assert.True(Assert.Single(new MarkdownService().Parse(result.Markdown).Syntax.Descendants<LinkInline>()).IsImage);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void Literal_code_links_keep_labels_and_report_the_lost_link_semantics()
    {
        using var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = package.AddMainDocumentPart();
            var link = main.AddHyperlinkRelationship(new Uri("https://example.org/not-fetched"), true);
            main.Document = new W.Document(new W.Body(new W.Paragraph(new W.ParagraphProperties(new W.ParagraphStyleId { Val = "Code" }),
                new W.Hyperlink(new W.Run(new W.RunProperties(new W.Bold()), new W.Text("**literal**"))) { Id = link.Id })));
        }
        var result = new DocumentImportService().ImportDocx(stream.ToArray());
        var parsed = new MarkdownService().Parse(result.Markdown);
        Assert.Equal("**literal**", Assert.Single(parsed.Syntax.Descendants<CodeBlock>()).Lines.ToString());
        Assert.Empty(parsed.Syntax.Descendants<LinkInline>());
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void Default_styles_and_outline_overrides_do_not_corrupt_inherited_code_semantics()
    {
        var defaultQuote = Style("Quote"); defaultQuote.Default = true;
        var codeChild = Style("CustomCode", "Code"); codeChild.StyleParagraphProperties = new W.StyleParagraphProperties(new W.OutlineLevel { Val = 9 });
        var headingAsBody = Paragraph("Heading3", "Body"); headingAsBody.ParagraphProperties!.OutlineLevel = new W.OutlineLevel { Val = 9 };
        var result = new DocumentImportService().ImportDocx(Package(new W.Styles(defaultQuote, Style("Code"), Style("Heading3"), codeChild),
            new W.Paragraph(new W.Run(new W.Text("Default quote"))), headingAsBody, Paragraph("CustomCode", "Code"), Paragraph("Heading3", "Heading")));
        var parsed = new MarkdownService().Parse(result.Markdown);
        Assert.Single(parsed.Syntax.Descendants<QuoteBlock>());
        Assert.Single(parsed.Syntax.Descendants<CodeBlock>());
        Assert.Equal(3, Assert.Single(parsed.Headings).Level);
        Assert.IsType<ParagraphBlock>(parsed.Syntax[1]);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Excessive_style_inheritance_is_rejected_without_changing_input()
    {
        var styles = new W.Styles(Enumerable.Range(0, 66).Select(i => Style("Custom" + i, i == 65 ? null : "Custom" + (i + 1))));
        var bytes = Package(styles, Paragraph("Custom0", "Body")); var original = bytes.ToArray();
        Assert.Throws<InvalidDataException>(() => new DocumentImportService().ImportDocx(bytes));
        Assert.Equal(original, bytes);
    }

    [Fact]
    public void Ordinary_blocks_between_quotes_prevent_accidental_quote_merging()
    {
        var result = RoundTrip("> First\n\n```text\nMiddle\n```\n\n> Last");
        var syntax = new MarkdownService().Parse(result.Markdown).Syntax;
        Assert.Equal(3, syntax.Count);
        Assert.IsType<QuoteBlock>(syntax[0]); Assert.IsType<FencedCodeBlock>(syntax[1]); Assert.IsType<QuoteBlock>(syntax[2]);
        Assert.Empty(result.Warnings);
    }

    private static ImportedDocument RoundTrip(string source)
    {
        var parsed = new MarkdownService().Parse(source);
        var bytes = new DocxExporter().Export(new(parsed, new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>()));
        var original = bytes.ToArray();
        var result = new DocumentImportService().ImportDocx(bytes);
        Assert.Equal(original, bytes); Assert.Equal(source, parsed.Source);
        return result;
    }
    private static W.Style Style(string id, string? parent = null)
    {
        var style = new W.Style(new W.StyleName { Val = id }) { Type = W.StyleValues.Paragraph, StyleId = id };
        if (parent is not null) style.BasedOn = new W.BasedOn { Val = parent };
        return style;
    }
    private static W.Paragraph Paragraph(string style, string text) => new(new W.ParagraphProperties(new W.ParagraphStyleId { Val = style }), new W.Run(new W.Text(text)));
    private static byte[] Package(W.Styles styles, params OpenXmlElement[] blocks)
    {
        using var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(blocks));
            main.AddNewPart<StyleDefinitionsPart>().Styles = styles;
        }
        return stream.ToArray();
    }
}
