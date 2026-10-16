using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Core.Tests;

public sealed class DocxTemplateStyleTests
{
    private static ExportContent Content(string source) => new(new MarkdownService().Parse(source), new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>());
    private static byte[] Template(IEnumerable<W.Style> styles, W.Table? table = null)
    {
        using var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = package.AddMainDocumentPart(); main.Document = new W.Document(new W.Body());
            if (table is not null) main.Document.Body!.Append(table);
            main.AddNewPart<StyleDefinitionsPart>().Styles = new W.Styles(styles);
        }
        return stream.ToArray();
    }
    private static W.Style Style(string id, string name, W.StyleValues type) => new(new W.StyleName { Val = name }) { StyleId = id, Type = type };

    [Fact] public void Template_semantic_styles_keep_heading_levels_body_code_quote_and_table_mappings()
    {
        var template = Template([Style("BrandH1", "Heading 1", W.StyleValues.Paragraph), Style("BrandH2", "Heading2", W.StyleValues.Paragraph),
            Style("BrandH3", "Heading_3", W.StyleValues.Paragraph), Style("BrandBody", "Body Text", W.StyleValues.Paragraph),
            Style("BrandCode", "No Spacing", W.StyleValues.Paragraph), Style("BrandQuote", "Intense Quote", W.StyleValues.Paragraph),
            Style("BrandTable", "Table Grid", W.StyleValues.Table)]);
        var original = template.ToArray();
        const string source = "# First heading\n\n## Second heading\n\n### Third heading\n\nBody text.\n\n> Quoted text.\n\n```txt\nCode sample\n```\n\n| Header |\n|---|\n| Cell |";
        using var stream = new MemoryStream(new DocxExporter().Export(Content(source), template));
        using var package = WordprocessingDocument.Open(stream, false); Assert.Empty(new OpenXmlValidator().Validate(package));
        var paragraphs = package.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>().ToArray();
        string? Selected(string text) => paragraphs.Single(paragraph => paragraph.InnerText == text).ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        Assert.Equal("BrandH1", Selected("First heading")); Assert.Equal("BrandH2", Selected("Second heading")); Assert.Equal("BrandH3", Selected("Third heading"));
        Assert.Equal("BrandBody", Selected("Body text.")); Assert.Equal("BrandQuote", Selected("Quoted text.")); Assert.Equal("BrandCode", Selected("Code sample"));
        Assert.Equal("BrandTable", package.MainDocumentPart.Document.Descendants<W.Table>().Single().TableProperties!.TableStyle!.Val!.Value);
        Assert.Equal(original, template);
    }

    [Fact] public void Default_headings_have_distinct_semantic_outline_levels_and_readable_style_definitions()
    {
        var source = string.Join("\n\n", Enumerable.Range(1, 6).Select(level => new string('#', level) + " Level " + level));
        using var stream = new MemoryStream(new DocxExporter().Export(Content(source)));
        using var package = WordprocessingDocument.Open(stream, false);
        var styles = package.MainDocumentPart!.StyleDefinitionsPart!.Styles!;
        var paragraphs = package.MainDocumentPart.Document!.Body!.Elements<W.Paragraph>().ToArray();
        for (var index = 0; index < 6; index++)
        {
            var id = paragraphs[index].ParagraphProperties!.ParagraphStyleId!.Val!.Value;
            var style = styles.Elements<W.Style>().Single(style => style.StyleId == id);
            Assert.Equal(index, style.StyleParagraphProperties?.OutlineLevel?.Val?.Value);
            Assert.NotNull(style.StyleRunProperties?.Bold); Assert.NotNull(style.StyleRunProperties?.FontSize);
        }
        Assert.Empty(new OpenXmlValidator().Validate(package));
    }

    [Fact] public void Same_named_character_or_table_styles_are_not_used_as_paragraph_styles()
    {
        var template = Template([Style("Heading1", "Heading 1", W.StyleValues.Table), Style("Normal", "Normal", W.StyleValues.Character),
            Style("ActualBody", "Body Text", W.StyleValues.Paragraph)]);
        using var stream = new MemoryStream(new DocxExporter().Export(Content("# Heading\n\nBody."), template));
        using var package = WordprocessingDocument.Open(stream, false);
        var styles = package.MainDocumentPart!.StyleDefinitionsPart!.Styles!;
        var paragraphs = package.MainDocumentPart.Document!.Body!.Elements<W.Paragraph>().ToArray();
        Assert.Equal("ActualBody", paragraphs[1].ParagraphProperties!.ParagraphStyleId!.Val!.Value);
        var headingStyle = paragraphs[0].ParagraphProperties!.ParagraphStyleId!.Val!.Value;
        Assert.NotEqual("Heading1", headingStyle);
        Assert.Equal(W.StyleValues.Paragraph, styles.Elements<W.Style>().Single(style => style.StyleId == headingStyle).Type!.Value);
        Assert.Equal(styles.ChildElements.Count, styles.Elements<W.Style>().Select(style => style.StyleId!.Value).Distinct().Count());
    }

    [Fact] public void Existing_table_prototype_style_and_width_survive_generated_content()
    {
        var template = Template([Style("CorporateTable", "Corporate table", W.StyleValues.Table)], new W.Table(
            new W.TableProperties(new W.TableStyle { Val = "CorporateTable" }, new W.TableWidth { Type = W.TableWidthUnitValues.Pct, Width = "4200" }),
            new W.TableGrid(new W.GridColumn()), new W.TableRow(new W.TableCell(new W.Paragraph(new W.Run(new W.Text("Template placeholder")))))));
        using var stream = new MemoryStream(new DocxExporter().Export(Content("| A |\n|---|\n| New content |"), template));
        using var package = WordprocessingDocument.Open(stream, false);
        var table = package.MainDocumentPart!.Document!.Descendants<W.Table>().Single();
        Assert.Equal("CorporateTable", table.TableProperties!.TableStyle!.Val!.Value); Assert.Equal("4200", table.TableProperties.TableWidth!.Width!.Value);
        Assert.Contains("New content", table.InnerText); Assert.DoesNotContain("Template placeholder", table.InnerText);
        Assert.Empty(new OpenXmlValidator().Validate(package));
    }

    [Fact] public void Existing_style_formatting_is_preserved_and_nested_quote_lists_retain_numbering()
    {
        var heading = Style("CorporateHeading", "Heading 1", W.StyleValues.Paragraph);
        heading.StyleRunProperties = new W.StyleRunProperties { RunFonts = new W.RunFonts { Ascii = "Arial", HighAnsi = "Arial" }, Color = new W.Color { Val = "245577" }, FontSize = new W.FontSize { Val = "52" } };
        var quote = Style("CorporateQuote", "Quote", W.StyleValues.Paragraph);
        quote.StyleParagraphProperties = new W.StyleParagraphProperties(new W.Indentation { Left = "720" });
        var template = Template([heading, quote]);
        using var stream = new MemoryStream(new DocxExporter().Export(Content("# Heading\n\n> 3. Quoted list\n> 4. Another item\n\nOrdinary paragraph."), template));
        using var package = WordprocessingDocument.Open(stream, false);
        var styles = package.MainDocumentPart!.StyleDefinitionsPart!.Styles!;
        Assert.Equal(heading.OuterXml, styles.Elements<W.Style>().Single(style => style.StyleId == "CorporateHeading").OuterXml);
        Assert.Equal(quote.OuterXml, styles.Elements<W.Style>().Single(style => style.StyleId == "CorporateQuote").OuterXml);
        var paragraphs = package.MainDocumentPart.Document!.Body!.Elements<W.Paragraph>().ToArray();
        Assert.All(paragraphs.Where(paragraph => paragraph.InnerText is "Quoted list" or "Another item"), paragraph =>
        { Assert.Equal("CorporateQuote", paragraph.ParagraphProperties!.ParagraphStyleId!.Val!.Value); Assert.NotNull(paragraph.ParagraphProperties.NumberingProperties); });
        Assert.Equal("Normal", paragraphs.Last().ParagraphProperties!.ParagraphStyleId!.Val!.Value);
        Assert.Empty(new OpenXmlValidator().Validate(package));
    }
}
