using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Core.Tests;

public sealed class DocxListTests
{
    [Fact]
    public void Continuation_paragraphs_do_not_advance_numbering_and_nested_items_keep_their_own_levels()
    {
        const string source = "7. First **bold** Café\n\n   Continuation\n\n   - Child\n\n     ChildContinuation\n\n     3. Grandchild\n\n        GrandchildContinuation\n\n   - ChildTwo\n\n   AfterChildren\n\n8. Second\n\nAfterList";
        var parsed = new MarkdownService().Parse(source);
        using var stream = new MemoryStream(new DocxExporter().Export(new(parsed, new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>())));
        using var package = WordprocessingDocument.Open(stream, false);
        Assert.Empty(new OpenXmlValidator().Validate(package));
        var paragraphs = package.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>().ToArray();
        W.Paragraph Find(string text) => paragraphs.Single(p => p.InnerText == text);
        var numbered = paragraphs.Where(p => Number(p) > 0).ToArray();
        Assert.Equal(new[] { "First bold Café", "Child", "Grandchild", "ChildTwo", "Second" }, numbered.Select(p => p.InnerText));
        Assert.Equal(Number(Find("First bold Café")), Number(Find("Second")));
        Assert.Equal(Number(Find("Child")), Number(Find("ChildTwo")));
        Assert.Equal(3, numbered.Select(Number).Distinct().Count());
        Assert.Equal(new[] { 0, 1, 2, 1, 0 }, numbered.Select(p => p.ParagraphProperties!.NumberingProperties!.NumberingLevelReference!.Val!.Value));
        foreach (var (first, continuation) in new[] { ("First bold Café", "Continuation"), ("First bold Café", "AfterChildren"), ("Child", "ChildContinuation"), ("Grandchild", "GrandchildContinuation") })
        {
            Assert.Equal(0, Number(Find(continuation)));
            Assert.Equal(Find(first).ParagraphProperties!.Indentation!.Left!.Value, Find(continuation).ParagraphProperties!.Indentation!.Left!.Value);
            Assert.Equal("0", Find(continuation).ParagraphProperties!.Indentation!.Hanging!.Value);
        }
        Assert.Null(Find("AfterList").ParagraphProperties!.Indentation);
        Assert.Null(Find("AfterList").ParagraphProperties!.NumberingProperties);
        Assert.Equal(source, parsed.Source);
    }

    [Theory]
    [InlineData("### HeadingFirst", "HeadingFirst")]
    [InlineData("```text\n  CodeFirst\n  ```", "CodeFirst")]
    [InlineData("> QuoteFirst", "QuoteFirst")]
    [InlineData("| TableFirst |\n  | --- |\n  | Value |", "")]
    [InlineData("- NestedFirst", "")]
    public void Non_paragraph_first_blocks_receive_one_parent_marker(string firstBlock, string expectedFirstParagraph)
    {
        var source = "- " + firstBlock + "\n\n  Continuation\n\n- NextItem\n\nAfterList";
        using var stream = new MemoryStream(new DocxExporter().Export(Content(source)));
        using var package = WordprocessingDocument.Open(stream, false);
        Assert.Empty(new OpenXmlValidator().Validate(package));
        var paragraphs = package.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>().ToArray();
        var first = paragraphs.First();
        Assert.Equal(expectedFirstParagraph, first.InnerText);
        Assert.True(Number(first) > 0);
        Assert.Equal(Number(first), Number(paragraphs.Single(p => p.InnerText == "NextItem")));
        Assert.Equal(2, paragraphs.Count(p => Number(p) == Number(first)));
        Assert.Equal(0, Number(paragraphs.Single(p => p.InnerText == "Continuation")));
        if (firstBlock.StartsWith('|')) Assert.All(package.MainDocumentPart.Document.Descendants<W.TableCell>().SelectMany(c => c.Elements<W.Paragraph>()), p => Assert.Equal(0, Number(p)));
        if (firstBlock.StartsWith('-')) Assert.Equal(1, paragraphs.Single(p => p.InnerText == "NestedFirst").ParagraphProperties!.NumberingProperties!.NumberingLevelReference!.Val!.Value);
    }

    [Fact]
    public void Empty_items_keep_a_marker_and_continuations_override_inherited_template_numbering()
    {
        using var template = new MemoryStream();
        using (var package = WordprocessingDocument.Create(template, WordprocessingDocumentType.Document, true))
        {
            var main = package.AddMainDocumentPart(); main.Document = new W.Document(new W.Body());
            main.AddNewPart<StyleDefinitionsPart>().Styles = new W.Styles(new W.Style(new W.StyleName { Val = "Body Text" },
                new W.StyleParagraphProperties(new W.NumberingProperties(new W.NumberingId { Val = 9 }))) { Type = W.StyleValues.Paragraph, StyleId = "NumberedBody" });
            main.AddNewPart<NumberingDefinitionsPart>().Numbering = new W.Numbering(new W.AbstractNum(new W.Level(new W.StartNumberingValue { Val = 1 },
                new W.NumberingFormat { Val = W.NumberFormatValues.Decimal }, new W.LevelText { Val = "%1." }) { LevelIndex = 0 }) { AbstractNumberId = 9 },
                new W.NumberingInstance(new W.AbstractNumId { Val = 9 }) { NumberID = 9 });
        }
        var original = template.ToArray();
        using var result = new MemoryStream(new DocxExporter().Export(Content("-\n- First\n\n  Continuation"), original));
        using var output = WordprocessingDocument.Open(result, false);
        Assert.Empty(new OpenXmlValidator().Validate(output));
        var paragraphs = output.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>().ToArray();
        Assert.Equal(3, paragraphs.Length);
        Assert.Equal("", paragraphs[0].InnerText); Assert.True(Number(paragraphs[0]) > 0);
        Assert.Equal(Number(paragraphs[0]), Number(paragraphs[1]));
        Assert.Equal(0, paragraphs[2].ParagraphProperties!.NumberingProperties!.NumberingId!.Val!.Value);
        Assert.All(paragraphs, p => Assert.Equal("NumberedBody", p.ParagraphProperties!.ParagraphStyleId!.Val!.Value));
        Assert.Equal(original, template.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Indented_images_and_table_cells_keep_their_media_inside_the_available_width(bool customMargins)
    {
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aX1cAAAAASUVORK5CYII=");
        const string source = "- ![Wide](wide.png)\n\n  Continuation\n\n- | Label | Image |\n  | --- | --- |\n  | Value | ![Cell](wide.png) |\n\n  TableContinuation";
        var content = Content(source) with { Images = new Dictionary<string, ExportImage> { ["wide.png"] = new(png, 1, 1, "Wide image", 1200, 80) } };
        byte[]? templateBytes = null;
        if (customMargins)
        {
            using var template = new MemoryStream();
            using (var document = WordprocessingDocument.Create(template, WordprocessingDocumentType.Document, true))
            {
                var main = document.AddMainDocumentPart();
                main.Document = new W.Document(new W.Body(new W.Table(new W.TableProperties(new W.TableCellMarginDefault(
                    new W.TableCellLeftMargin { Width = 720, Type = W.TableWidthValues.Dxa },
                    new W.TableCellRightMargin { Width = 360, Type = W.TableWidthValues.Dxa })),
                    new W.TableGrid(new W.GridColumn()), new W.TableRow(new W.TableCell(new W.Paragraph())))));
            }
            templateBytes = template.ToArray();
        }
        using var stream = new MemoryStream(new DocxExporter().Export(content, templateBytes));
        using var package = WordprocessingDocument.Open(stream, false);
        Assert.Empty(new OpenXmlValidator().Validate(package));
        var body = package.MainDocumentPart!.Document!.Body!;
        var table = Assert.Single(body.Elements<W.Table>());
        var width = long.Parse(table.TableProperties!.TableWidth!.Width!.Value!);
        var tableIndent = table.TableProperties.TableIndentation!.Width!.Value;
        Assert.Equal(360, tableIndent); Assert.Equal(W.TableWidthUnitValues.Dxa, table.TableProperties.TableWidth.Type!.Value);
        var firstImage = Assert.Single(body.Elements<W.Paragraph>().SelectMany(p => p.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.Inline>()));
        Assert.Equal(width * 635, firstImage.Extent!.Cx!.Value);
        var cellImage = Assert.Single(table.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.Inline>());
        Assert.InRange(cellImage.Extent!.Cx!.Value, 1, (width / 2 - (customMargins ? 1080 : 216)) * 635);
        foreach (var inline in new[] { firstImage, cellImage })
        {
            var shape = Assert.Single(inline.Descendants<DocumentFormat.OpenXml.Drawing.Extents>());
            Assert.Equal(inline.Extent!.Cx!.Value, shape.Cx!.Value); Assert.Equal(inline.Extent.Cy!.Value, shape.Cy!.Value);
            Assert.InRange((double)inline.Extent.Cx.Value / inline.Extent.Cy.Value, 14.99, 15.01);
        }
        Assert.Single(package.MainDocumentPart.ImageParts);
        Assert.Equal(png, ReadImage(package.MainDocumentPart.ImageParts.Single()));
    }

    [Fact]
    public void Narrow_template_table_width_is_preserved_and_limits_its_actual_image_extent()
    {
        using var template = new MemoryStream();
        using (var package = WordprocessingDocument.Create(template, WordprocessingDocumentType.Document, true))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(new W.Table(new W.TableProperties(new W.TableWidth { Width = "2500", Type = W.TableWidthUnitValues.Pct }),
                new W.TableGrid(new W.GridColumn()), new W.TableRow(new W.TableCell(new W.Paragraph())))));
        }
        var original = template.ToArray();
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aX1cAAAAASUVORK5CYII=");
        var content = Content("| Image |\n| --- |\n| ![Wide](wide.png) |") with { Images = new Dictionary<string, ExportImage> { ["wide.png"] = new(png, 1, 1, "Wide image", 1200, 80) } };
        using var result = new MemoryStream(new DocxExporter().Export(content, original));
        using var output = WordprocessingDocument.Open(result, false);
        Assert.Empty(new OpenXmlValidator().Validate(output));
        var table = Assert.Single(output.MainDocumentPart!.Document!.Descendants<W.Table>());
        Assert.Equal("2500", table.TableProperties!.TableWidth!.Width!.Value);
        Assert.Equal(W.TableWidthUnitValues.Pct, table.TableProperties.TableWidth.Type!.Value);
        var extent = Assert.Single(table.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent>());
        Assert.InRange(extent.Cx!.Value, 1, ((11906 - 2268) / 2 - 216) * 635L);
        Assert.Equal(original, template.ToArray());
    }

    [Fact]
    public void Code_allocation_preflight_includes_numbered_paragraph_formatting()
    {
        var content = Content("- ```text\n  Code\n  ```");
        using var ordinary = WordprocessingDocument.Open(new MemoryStream(new DocxExporter().Export(content)), false);
        var paragraph = ordinary.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>().Single();
        var count = paragraph.Descendants().Count() + 1;
        Assert.NotEmpty(new DocxExporter().Export(content with { AllocationLimits = new(MaximumWordCodeElements: count) }));
        var error = Assert.Throws<IOException>(() => new DocxExporter().Export(content with { AllocationLimits = new(MaximumWordCodeElements: count - 1) }, [0x01]));
        Assert.Contains("Code formatting", error.Message);
    }

    private static byte[] ReadImage(ImagePart image) { using var source = image.GetStream(); using var output = new MemoryStream(); source.CopyTo(output); return output.ToArray(); }
    private static int Number(W.Paragraph paragraph) => paragraph.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value ?? 0;
    private static ExportContent Content(string source) => new(new MarkdownService().Parse(source), new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>());
}
