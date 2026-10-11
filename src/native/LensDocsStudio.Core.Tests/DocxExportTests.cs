using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Core.Tests;

public sealed class DocxExportTests
{
    private static ExportContent Content(string source) => new(new MarkdownService().Parse(source), new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>());

    [Fact]
    public void Word_output_has_valid_headings_tables_numbering_and_unicode()
    {
        var bytes = new DocxExporter().Export(Content("# Café 🌍\n\n## Steps\n\n1. **Write** the document.\n2. Review it.\n\n| Area | State |\n|---|---|\n| Preview | Ready |\n"));
        using var stream = new MemoryStream(bytes);
        using var document = WordprocessingDocument.Open(stream, false);
        var errors = new OpenXmlValidator().Validate(document).Select(e => e.Description).ToArray();
        Assert.True(errors.Length == 0, string.Join("\n", errors));
        var main = document.MainDocumentPart!;
        Assert.Contains("Café", main.Document!.InnerText);
        Assert.Single(main.Document.Descendants<W.Table>());
        Assert.Single(main.Document.Descendants<W.TableHeader>());
        Assert.Equal(2, main.Document.Descendants<W.NumberingProperties>().Count());
        Assert.NotEmpty(main.NumberingDefinitionsPart!.Numbering!.Elements<W.NumberingInstance>());
        Assert.Contains(main.Document.Descendants<W.ParagraphStyleId>(), s => s.Val == "Title");
        Assert.Contains(main.Document.Descendants<W.ParagraphStyleId>(), s => s.Val == "Heading1");
    }

    [Fact]
    public void Template_body_is_replaced_but_header_footer_style_and_section_survive()
    {
        using var templateStream = new MemoryStream();
        using (var template = WordprocessingDocument.Create(templateStream, WordprocessingDocumentType.Document, true))
        {
            var main = template.AddMainDocumentPart();
            var header = main.AddNewPart<HeaderPart>();
            header.Header = new W.Header(new W.Paragraph(new W.Run(new W.Text("Generic header"), new W.FieldChar { FieldCharType = W.FieldCharValues.Begin },
                new W.FieldCode(" PAGE "), new W.FieldChar { FieldCharType = W.FieldCharValues.End })));
            var footer = main.AddNewPart<FooterPart>();
            footer.Footer = new W.Footer(new W.Paragraph(new W.Run(new W.Text("Generic footer"))));
            var styles = main.AddNewPart<StyleDefinitionsPart>();
            styles.Styles = new W.Styles(new W.Style(new W.StyleName { Val = "Heading 1" }) { Type = W.StyleValues.Paragraph, StyleId = "CustomHeading" });
            main.Document = new W.Document(new W.Body(new W.Paragraph(new W.Run(new W.Text("Remove template placeholder"))),
                new W.SectionProperties(new W.HeaderReference { Type = W.HeaderFooterValues.Default, Id = main.GetIdOfPart(header) },
                    new W.FooterReference { Type = W.HeaderFooterValues.Default, Id = main.GetIdOfPart(footer) },
                    new W.PageSize { Width = 15840, Height = 12240, Orient = W.PageOrientationValues.Landscape })));
        }
        var bytes = new DocxExporter().Export(Content("# New title\n\n## Content heading\n\nBody"), templateStream.ToArray());
        using var stream = new MemoryStream(bytes);
        using var output = WordprocessingDocument.Open(stream, false);
        var mainPart = output.MainDocumentPart!;
        Assert.DoesNotContain("Remove template placeholder", mainPart.Document!.InnerText);
        Assert.Equal("Generic header PAGE ", mainPart.HeaderParts.Single().Header!.InnerText);
        Assert.Equal("Generic footer", mainPart.FooterParts.Single().Footer!.InnerText);
        Assert.Contains(mainPart.Document.Descendants<W.ParagraphStyleId>(), s => s.Val == "CustomHeading");
        Assert.Equal((uint)15840, mainPart.Document.Descendants<W.PageSize>().Single().Width!.Value);
        Assert.Equal(W.PageOrientationValues.Landscape, mainPart.Document.Descendants<W.PageSize>().Single().Orient!.Value);
        Assert.Empty(new OpenXmlValidator().Validate(output));
    }

    [Fact]
    public void External_template_resources_are_rejected()
    {
        using var stream = new MemoryStream();
        using (var template = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = template.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(new W.Paragraph()));
            main.AddExternalRelationship("http://schemas.openxmlformats.org/officeDocument/2006/relationships/image", new Uri("https://example.com/tracking.png"));
        }
        Assert.Throws<InvalidDataException>(() => new DocxExporter().Export(Content("# Safe"), stream.ToArray()));
    }

    [Fact]
    public void Missing_images_fail_explicitly_instead_of_silently_disappearing()
    {
        Assert.Throws<InvalidDataException>(() => new DocxExporter().Export(Content("![Missing](assets/missing.png)")));
    }
}
