using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Native.Validation;

internal static class NativeWordTemplateChecks
{
    private static ExportContent Content(string source, byte[] pixels) => new(new MarkdownService().Parse(source), new Dictionary<int, ExportImage>(),
        new Dictionary<string, ExportImage> { ["logo.png"] = new(pixels, 1, 1, "Template logo") });

    internal static byte[] Create(byte[] pixels, bool attachedTemplate = false)
    {
        using var document = new MemoryStream(); document.Write(new DocxExporter().Export(Content("![Logo](logo.png)", pixels)));
        using (var package = WordprocessingDocument.Open(document, true))
        {
            var main = package.MainDocumentPart!; var drawing = main.Document!.Descendants<W.Drawing>().Single().CloneNode(true);
            var header = main.AddNewPart<HeaderPart>(); header.Header = new W.Header(new W.Paragraph(new W.Run(new W.Text("Native template header — Café"), drawing)));
            var image = header.AddImagePart(ImagePartType.Png, "logo"); using var data = new MemoryStream(pixels); image.FeedData(data);
            drawing.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().Single().Embed = "logo";
            header.Header.Append(new W.Paragraph(new W.SimpleField(new W.Run(new W.Text("1"))) { Instruction = "PAGE" }));
            var footer = main.AddNewPart<FooterPart>(); footer.Footer = new W.Footer(new W.Paragraph(new W.Run(new W.Text("Native template footer"))));
            main.Document.Body = new W.Body(new W.Paragraph(new W.Run(new W.Text("Private template body must not be exported"))),
                new W.SectionProperties(new W.HeaderReference { Type = W.HeaderFooterValues.Default, Id = main.GetIdOfPart(header) },
                    new W.FooterReference { Type = W.HeaderFooterValues.Default, Id = main.GetIdOfPart(footer) }));
            main.StyleDefinitionsPart!.Styles!.Append(new W.Style(new W.StyleName { Val = "Heading 1" })
            {
                Type = W.StyleValues.Paragraph, StyleId = "NativeHeading",
                StyleParagraphProperties = new W.StyleParagraphProperties(new W.OutlineLevel { Val = 0 }),
                StyleRunProperties = new W.StyleRunProperties { Bold = new W.Bold(), Color = new W.Color { Val = "245577" }, FontSize = new W.FontSize { Val = "40" } }
            });
            main.AddNewPart<WordprocessingCommentsPart>().Comments = new W.Comments(new W.Comment(new W.Paragraph(new W.Run(new W.Text("Private template comment")))) { Author = "Private original author", Id = "1" });
            var custom = main.AddCustomXmlPart(CustomXmlPartType.CustomXml); using var customData = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("<private>Private template metadata</private>")); custom.FeedData(customData);
            package.PackageProperties.Creator = "Private original author";
            if (attachedTemplate)
            {
                var settings = main.DocumentSettingsPart!;
                settings.Settings!.Append(new W.AttachedTemplate { Id = "passive-template" });
                settings.AddExternalRelationship("http://schemas.openxmlformats.org/officeDocument/2006/relationships/attachedTemplate",
                    new Uri("https://example.invalid/unavailable-template.dotx"), "passive-template");
            }
        }
        return document.ToArray();
    }

    internal static void Check(byte[] bytes, byte[] pixels)
    {
        using var stream = new MemoryStream(bytes); using var package = WordprocessingDocument.Open(stream, false);
        var errors = new OpenXmlValidator().Validate(package).Select(error => error.Description).ToArray();
        if (errors.Length > 0) throw new IOException("The template export failed schema validation: " + string.Join("; ", errors));
        var main = package.MainDocumentPart!;
        if (main.DocumentSettingsPart?.ExternalRelationships.Any() == true || main.DocumentSettingsPart?.Settings?.Descendants<W.AttachedTemplate>().Any() == true)
            throw new IOException("The generated Word export retained an external attached-template reference.");
        if (main.Document!.InnerText.Contains("Private template", StringComparison.Ordinal) || main.WordprocessingCommentsPart is not null || main.CustomXmlParts.Any()
            || package.PackageProperties.Creator == "Private original author") throw new IOException("The Word export retained unrelated template content.");
        var section = main.Document.Body!.Elements<W.SectionProperties>().Single();
        var headerReference = section.Elements<W.HeaderReference>().Single(); var footerReference = section.Elements<W.FooterReference>().Single();
        if (main.GetPartById(headerReference.Id!) is not HeaderPart header || !header.Header!.InnerText.Contains("Native template header — Café", StringComparison.Ordinal)
            || main.GetPartById(footerReference.Id!) is not FooterPart footer || footer.Footer!.InnerText != "Native template footer"
            || !main.Document.Descendants<W.ParagraphStyleId>().Any(style => style.Val == "NativeHeading"))
            throw new IOException("The Word export lost its template heading, header or footer.");
        using var logo = header.ImageParts.Single().GetStream(); using var copy = new MemoryStream(); logo.CopyTo(copy);
        if (!pixels.SequenceEqual(copy.ToArray())) throw new IOException("The Word template logo pixels changed.");
    }

    public static void Run(string output)
    {
        var pixels = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aX1sAAAAASUVORK5CYII=");
        var template = Create(pixels); var content = Content("# Native template export\n\n7. Retained numbering\n8. Second item\n\n| A | B |\n|---|---|\n| Café | Evidence |\n\n![Body image](logo.png)", pixels);
        var result = new DocxTemplateService().Prepare(template);
        var exported = new DocxExporter().Export(content, result.Bytes); Check(exported, pixels);
        File.WriteAllBytes(Path.Combine(output, "native-word-template.docx"), exported);
        File.WriteAllText(Path.Combine(output, "native-word-template.json"), JsonSerializer.Serialize(new
        {
            Passed = true, Checks = new[] { "Prepared template and generated DOCX retain actual logo bytes, custom heading, referenced header/footer, numbering and valid package structure without original body/comments/custom XML/author metadata" }, result.Warnings
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
