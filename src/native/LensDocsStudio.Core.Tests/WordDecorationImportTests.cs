using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Core.Tests;

public sealed class WordDecorationImportTests
{
    [Theory]
    [InlineData("Before ~~removed~~ after", MarkdownEmphasis.Strike)]
    [InlineData("Before ++underlined++ after", MarkdownEmphasis.Underline)]
    [InlineData("Before **~~removed~~** after", MarkdownEmphasis.Bold | MarkdownEmphasis.Strike)]
    [InlineData("Before *++underlined++* after", MarkdownEmphasis.Italic | MarkdownEmphasis.Underline)]
    [InlineData("[~~removed~~](https://example.org/)", MarkdownEmphasis.Strike)]
    [InlineData("| Label |\n|---|\n| ++underlined++ |", MarkdownEmphasis.Underline)]
    public void Word_round_trip_preserves_supported_text_decorations(string source, MarkdownEmphasis expected)
    {
        var bytes = new DocxExporter().Export(new(new MarkdownService().Parse(source),
            new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>()));
        var original = bytes.ToArray();
        var imported = new DocumentImportService().ImportDocx(bytes);
        var syntax = new MarkdownService().Parse(imported.Markdown).Syntax;
        var formatting = syntax.Descendants<EmphasisInline>().Aggregate(MarkdownEmphasis.None,
            (value, emphasis) => value | MarkdownFormatting.Emphasis(emphasis));
        Assert.Equal(expected, formatting);
        Assert.Empty(imported.Warnings);
        Assert.Equal(original, bytes);
    }

    [Fact]
    public void Explicitly_disabled_decorations_do_not_add_markdown_formatting()
    {
        var bytes = Package(new W.Strike { Val = false }, new W.DoubleStrike { Val = false }, new W.Underline { Val = W.UnderlineValues.None });
        var imported = new DocumentImportService().ImportDocx(bytes);
        Assert.Equal("  Literal \\~\\~text\\~\\~  \n\n", imported.Markdown);
        Assert.Empty(new MarkdownService().Parse(imported.Markdown).Syntax.Descendants<EmphasisInline>());
        Assert.Empty(imported.Warnings);
    }

    [Fact]
    public void Double_strike_retains_deletion_meaning_with_an_explicit_conversion_note()
    {
        var imported = new DocumentImportService().ImportDocx(Package(new W.DoubleStrike()));
        var emphasis = Assert.Single(new MarkdownService().Parse(imported.Markdown).Syntax.Descendants<EmphasisInline>());
        Assert.Equal(MarkdownEmphasis.Strike, MarkdownFormatting.Emphasis(emphasis));
        Assert.Equal("Literal ~~text~~", MarkdownService.InlineText(emphasis));
        Assert.Equal("A Word double strikethrough was simplified to a single strikethrough.", Assert.Single(imported.Warnings));
    }

    [Fact]
    public void Decoration_markup_is_included_in_the_import_text_limit()
    {
        var bytes = Package(new W.Strike(), new W.Underline { Val = W.UnderlineValues.Single });
        var importer = new DocumentImportService();
        var imported = importer.ImportDocx(bytes);
        Assert.Equal(imported.Markdown, importer.ImportDocx(bytes, limits: new(MaximumTextCharacters: imported.Markdown.Length)).Markdown);
        Assert.Throws<InvalidDataException>(() => importer.ImportDocx(bytes, limits: new(MaximumTextCharacters: imported.Markdown.Length - 1)));
    }

    private static byte[] Package(params OpenXmlElement[] properties)
    {
        using var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(new W.Paragraph(new W.Run(new W.RunProperties(properties),
                new W.Text("  Literal ~~text~~  ") { Space = SpaceProcessingModeValues.Preserve }))));
        }
        return stream.ToArray();
    }
}
