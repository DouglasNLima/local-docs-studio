using System.IO.Compression;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Core.Tests;

public sealed class WordImportTextLimitTests
{
    [Fact]
    public void Repeated_shared_hyperlinks_cannot_expand_a_small_package_into_unbounded_markdown()
    {
        var bytes = SharedLinks(4200, 8192);
        var original = bytes.ToArray();
        using (var zip = new ZipArchive(new MemoryStream(bytes)))
            Assert.True(zip.Entries.Sum(entry => entry.Length) < 2 * 1024 * 1024);
        ImportedDocument? imported = null;
        var error = Record.Exception(() => imported = new DocumentImportService().ImportDocx(bytes));
        Assert.True(error is InvalidDataException,
            $"A {bytes.Length}-byte package produced {imported?.Markdown.Length} Markdown characters instead of rejecting excessive text growth: {error}");
        Assert.Equal("The Word document exceeds the imported text limit.", error.Message);
        Assert.Equal(original, bytes);
    }

    [Theory]
    [InlineData("Ordinary Café Ω text")]
    [InlineData("**Strong** and *emphasis* with literal \\* symbols &copy;")]
    [InlineData("# Heading\n\n> First\n>\n> Second\n\nEnd")]
    [InlineData("7. First\n\n   Continuation\n\n   - Nested\n\n8. Second")]
    [InlineData("````\nLiteral ``` and <tag> &copy;\n\tMore\n````")]
    [InlineData("[One](https://example.org/a?x=1&y=2) [Two](https://example.org/a?x=1&y=2)")]
    [InlineData("| Name | Value |\n|---|---|\n| Café | Literal \\| pipe |\n| Next | Cell |")]
    public void Exact_markdown_limit_succeeds_and_one_character_less_rejects_without_changing_source(string source)
    {
        var bytes = new DocxExporter().Export(new(new MarkdownService().Parse(source),
            new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>()));
        var original = bytes.ToArray();
        var importer = new DocumentImportService();
        var expected = importer.ImportDocx(bytes);
        var actual = importer.ImportDocx(bytes, limits: new(MaximumTextCharacters: expected.Markdown.Length));
        Assert.Equal(expected.Markdown, actual.Markdown);
        Assert.Equal(expected.Warnings, actual.Warnings);
        Assert.Throws<InvalidDataException>(() => importer.ImportDocx(bytes, limits: new(MaximumTextCharacters: expected.Markdown.Length - 1)));
        Assert.Equal(original, bytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Shared_targets_are_bounded_inside_paragraphs_and_tables(bool table)
    {
        var bytes = Package(main =>
        {
            var relationship = main.AddHyperlinkRelationship(new Uri("https://example.org/" + new string('a', 256)), true);
            var paragraph = new W.Paragraph(Enumerable.Range(0, 16).Select(_ =>
                new W.Hyperlink(new W.Run(new W.Text("Go"))) { Id = relationship.Id }));
            return table ? new W.Table(new W.TableRow(new W.TableCell(paragraph))) : paragraph;
        });
        Assert.Throws<InvalidDataException>(() => new DocumentImportService().ImportDocx(bytes, limits: new(MaximumTextCharacters: 1024)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Escaping_and_code_fences_count_towards_the_text_limit(bool code)
    {
        var bytes = Package(main =>
        {
            main.AddNewPart<StyleDefinitionsPart>().Styles = new W.Styles(new W.Style { StyleId = "Code", Type = W.StyleValues.Paragraph });
            return new W.Paragraph(new W.ParagraphProperties(new W.ParagraphStyleId { Val = code ? "Code" : "Normal" }),
                new W.Run(new W.Text(new string(code ? '`' : '&', 64))));
        });
        Assert.Throws<InvalidDataException>(() => new DocumentImportService().ImportDocx(bytes, limits: new(MaximumTextCharacters: 128)));
    }

    [Fact]
    public void Table_line_break_expansion_and_document_totals_are_bounded()
    {
        var bytes = Package(_ => new W.Table(Enumerable.Range(0, 16).Select(_ => new W.TableRow(
            new W.TableCell(new W.Paragraph(Enumerable.Range(0, 16).Select(_ => new W.Run(new W.Text("x"), new W.Break()))))))));
        Assert.Throws<InvalidDataException>(() => new DocumentImportService().ImportDocx(bytes, limits: new(MaximumTextCharacters: 64)));
        Assert.Throws<InvalidDataException>(() => new DocumentImportService().ImportDocx(bytes, limits: new(MaximumTextCharacters: 512)));
    }

    [Fact]
    public void Bounded_import_retains_each_shared_link_label_and_exact_destination()
    {
        var bytes = SharedLinks(16, 256);
        var imported = new DocumentImportService().ImportDocx(bytes, limits: new(MaximumTextCharacters: 8192));
        var links = new MarkdownService().Parse(imported.Markdown).Syntax.Descendants<LinkInline>().ToArray();
        Assert.Equal(16, links.Length);
        Assert.All(links, link =>
        {
            Assert.Equal("Go", MarkdownService.InlineText(link));
            Assert.Equal("https://example.org/" + new string('a', 256), link.Url);
        });
        Assert.Empty(imported.Warnings);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Invalid_text_limits_are_rejected_before_package_parsing(int limit) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new DocumentImportService().ImportDocx([], limits: new(MaximumTextCharacters: limit)));

    private static byte[] Package(Func<MainDocumentPart, OpenXmlElement> content)
    {
        using var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(content(main)));
        }
        return stream.ToArray();
    }

    private static byte[] SharedLinks(int count, int destinationLength)
    {
        using var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = package.AddMainDocumentPart();
            var relationship = main.AddHyperlinkRelationship(new Uri("https://example.org/" + new string('a', destinationLength)), true);
            main.Document = new W.Document(new W.Body(Enumerable.Range(0, count).Select(_ =>
                new W.Paragraph(new W.Hyperlink(new W.Run(new W.Text("Go"))) { Id = relationship.Id }))));
        }
        return stream.ToArray();
    }
}
