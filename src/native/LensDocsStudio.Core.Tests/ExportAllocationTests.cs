using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using Markdig.Extensions.Tables;
using Markdig.Syntax;

namespace LensDocsStudio.Core.Tests;

public sealed class ExportAllocationTests
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aX1cAAAAASUVORK5CYII=");
    private static ExportContent Content(string source, long imageCharacters = 1024 * 1024, int htmlCharacters = 1024 * 1024) =>
        new(new MarkdownService().Parse(source), new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage> { ["image.png"] = new(Png, 1, 1, "Image") })
        { AllocationLimits = new(imageCharacters, htmlCharacters) };

    [Theory]
    [InlineData("html")]
    [InlineData("word")]
    [InlineData("clipboard")]
    [InlineData("site")]
    public void Repeated_image_occurrences_are_rejected_before_serialisation(string format)
    {
        var content = Content("![A](image.png)\n\n![B](image.png)", imageCharacters: 100);
        var error = Assert.Throws<IOException>(() => Export(format, content));
        Assert.Contains("Repeated images", error.Message);
    }

    [Theory]
    [InlineData("html")]
    [InlineData("word")]
    [InlineData("clipboard")]
    [InlineData("site")]
    public void Ordinary_repeated_images_remain_exportable(string format)
    {
        Export(format, Content("# Café\n\n![A](image.png)\n\n![B](image.png)"));
    }

    [Fact]
    public void Word_reuses_image_parts_and_preserves_distinct_accessible_drawings()
    {
        var content = Content("![A](image.png)\n\n![B](image.png)");
        using var document = WordprocessingDocument.Open(new MemoryStream(new DocxExporter().Export(content)), false);
        Assert.Single(document.MainDocumentPart!.ImageParts);
        Assert.Equal(2, document.MainDocumentPart.Document!.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().Count());
        Assert.Empty(new OpenXmlValidator().Validate(document));
    }

    [Fact]
    public void Selected_table_budget_does_not_charge_unrelated_images()
    {
        var content = Content("![A](image.png)\n\n![B](image.png)\n\n| Title |\n|---|\n| ![Cell](image.png) |", imageCharacters: 100);
        var table = Assert.Single(content.Document.Syntax.Descendants<Table>());
        var result = new ClipboardExporter().Export(content, new(table.Span.Start, table.Span.Length));
        Assert.Contains("Cell", result.PlainText);
        Assert.DoesNotContain("[A]", result.PlainText);
    }

    [Fact]
    public void Diagram_and_formula_images_share_the_same_occurrence_budget()
    {
        var content = Content("```mermaid\nflowchart LR\nA-->B\n```\n\n$x$", imageCharacters: 100);
        var diagram = Assert.Single(content.Document.Syntax.Descendants<FencedCodeBlock>());
        var formula = Assert.Single(DocumentMathematics.Read(content.Document.Syntax));
        var image = new ExportImage(Png, 1, 1, "Rendered image");
        content = content with { Diagrams = new Dictionary<int, ExportImage> { [diagram.Span.Start] = image },
            Formulae = new Dictionary<int, ExportImage> { [formula.SourceStart] = image } };
        Assert.Throws<IOException>(() => new HtmlExporter().Export(content));
    }

    [Fact]
    public void Image_occurrence_count_is_limited_even_for_tiny_payloads()
    {
        var content = Content("![A](image.png)\n\n![B](image.png)") with { AllocationLimits = new(MaximumImageOccurrences: 1) };
        Assert.Throws<IOException>(() => new DocxExporter().Export(content));
    }

    [Fact]
    public void Html_markup_is_bounded_during_generation_even_without_images()
    {
        var error = Assert.Throws<IOException>(() => new HtmlExporter().Export(Content("**" + new string('&', 200) + "**", htmlCharacters: 128)));
        Assert.Contains("generated HTML", error.Message);
    }

    [Fact]
    public void Word_package_growth_is_bounded_before_returning_bytes()
    {
        var content = Content("# Document") with { AllocationLimits = new(MaximumOutputBytes: 128) };
        Assert.Throws<IOException>(() => new DocxExporter().Export(content));
    }

    [Theory]
    [InlineData("html")]
    [InlineData("word")]
    [InlineData("clipboard")]
    [InlineData("site")]
    public void Cancelled_exports_stop_before_serialisation(string format) =>
        Assert.Throws<OperationCanceledException>(() => Export(format, Content("# Document"), new CancellationToken(true)));

    private static void Export(string format, ExportContent content, CancellationToken token = default)
    {
        switch (format)
        {
            case "html": Assert.Contains("<html", new HtmlExporter().Export(content, token)); break;
            case "word": Assert.NotEmpty(new DocxExporter().Export(content, cancellationToken: token)); break;
            case "clipboard": Assert.NotEmpty(new ClipboardExporter().Export(content, cancellationToken: token).Html); break;
            case "site": Assert.NotEmpty(new DocsSiteExporter().PreparePage("page.md", content, cancellationToken: token).Html); break;
            default: throw new ArgumentException(format);
        }
    }
}
