using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Native.Validation;

internal sealed record NativeWordImportFixture(string Path, byte[] Bytes, byte[] Image);

internal static class NativeWordImportFixtureChecks
{
    public static string WriteExcessiveTextFixture(string output)
    {
        var path = Path.Combine(output, "excessive-word-text.docx");
        using var package = WordprocessingDocument.Create(path, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        var main = package.AddMainDocumentPart();
        var relationship = main.AddHyperlinkRelationship(new Uri("https://example.org/" + new string('a', 8192)), true);
        main.Document = new W.Document(new W.Body(Enumerable.Range(0, 4200).Select(_ =>
            new W.Paragraph(new W.Hyperlink(new W.Run(new W.Text("Go"))) { Id = relationship.Id }))));
        return path;
    }

    internal static readonly string[] ExpectedWarnings =
    [
        "An unsupported or missing Word hyperlink destination was retained as text.",
        "A Word field was retained as its stored result; it was not evaluated."
    ];

    public static NativeWordImportFixture Run(string output)
    {
        Directory.CreateDirectory(output);
        const int width = 16, height = 12;
        var pixels = new byte[width * height * 4];
        for (var offset = 0; offset < pixels.Length; offset += 4)
        { pixels[offset] = 62; pixels[offset + 1] = 136; pixels[offset + 2] = 255; pixels[offset + 3] = 255; }
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var imageStream = new MemoryStream(); encoder.Save(imageStream); var image = imageStream.ToArray();
        const string source = "# Imported Café\n\nPortable **bold** text and [guide](https://example.org/import).\n\n~~Removed~~ and ++underlined++.\n\n![Portable pixel](fixture.png)\n\n> Retained quote Ω.\n";
        var content = new ExportContent(new MarkdownService().Parse(source), new Dictionary<int, ExportImage>(),
            new Dictionary<string, ExportImage> { ["fixture.png"] = new(image, width, height, "Portable pixel") });
        using var buffer = new MemoryStream();
        buffer.Write(new DocxExporter().Export(content)); buffer.Position = 0;
        using (var package = WordprocessingDocument.Open(buffer, true))
        {
            var main = package.MainDocumentPart ?? throw new IOException("The Word import fixture has no main part.");
            var document = main.Document ?? throw new IOException("The Word import fixture has no document.");
            var body = document.Body ?? throw new IOException("The Word import fixture has no body.");
            var styles = main.StyleDefinitionsPart?.Styles ?? throw new IOException("The Word import fixture has no styles.");
            styles.Append(new W.Style(new W.StyleName { Val = "Imported strike base" }, new W.StyleRunProperties(new W.Strike()))
                { StyleId = "ImportedStrikeBase", Type = W.StyleValues.Character },
                new W.Style(new W.StyleName { Val = "Imported removed" }, new W.BasedOn { Val = "ImportedStrikeBase" })
                { StyleId = "ImportedRemoved", Type = W.StyleValues.Character },
                new W.Style(new W.StyleName { Val = "Imported underline base" },
                    new W.StyleRunProperties(new W.Underline { Val = W.UnderlineValues.Single }))
                { StyleId = "ImportedUnderlineBase", Type = W.StyleValues.Character },
                new W.Style(new W.StyleName { Val = "Imported underline" }, new W.BasedOn { Val = "ImportedUnderlineBase" })
                { StyleId = "ImportedUnderline", Type = W.StyleValues.Character });
            foreach (var run in document.Descendants<W.Run>())
            {
                var text = string.Concat(run.Elements<W.Text>().Select(item => item.Text));
                if (text is not ("Removed" or "underlined")) continue;
                var properties = run.RunProperties ?? throw new IOException("The decoration fixture has no run properties.");
                properties.RemoveAllChildren<W.Strike>(); properties.RemoveAllChildren<W.Underline>();
                properties.PrependChild(new W.RunStyle { Val = text == "Removed" ? "ImportedRemoved" : "ImportedUnderline" });
            }
            styles.Save();
            var section = body.Elements<W.SectionProperties>().Single();
            var relationship = main.AddHyperlinkRelationship(new Uri("file:///C:/unread-fixture.docx"), true);
            body.InsertBefore(new W.Paragraph(new W.Hyperlink(new W.Run(new W.Text("Local destination"))) { Id = relationship.Id }), section);
            body.InsertBefore(new W.Paragraph(new W.SimpleField(new W.Run(new W.Text("Stored author"))) { Instruction = "AUTHOR" }), section);
            document.Save();
            if (new OpenXmlValidator().Validate(package).Any()) throw new IOException("The Word import fixture is not schema-valid.");
        }
        var bytes = buffer.ToArray();
        var path = Path.Combine(output, "native-word-import-fixture.docx"); File.WriteAllBytes(path, bytes);
        var imported = new DocumentImportService().ImportDocx(bytes);
        File.WriteAllText(Path.Combine(output, "native-word-import-fixture.md"), imported.Markdown);
        File.WriteAllText(Path.Combine(output, "native-word-import-observed.json"), JsonSerializer.Serialize(new
        {
            Warnings = imported.Warnings, Assets = imported.Assets.Select(asset => new { asset.Reference, asset.SuggestedName, Bytes = asset.Bytes.Length })
        }, new JsonSerializerOptions { WriteIndented = true }));
        CheckMarkdown(imported.Markdown);
        if (!imported.Warnings.SequenceEqual(ExpectedWarnings) || imported.Assets.Count != 1
            || !imported.Assets[0].Bytes.SequenceEqual(image) || !File.ReadAllBytes(path).SequenceEqual(bytes))
            throw new IOException("The import fixture lost its independent warnings, original bytes or embedded image.");
        using var retainedImage = new MemoryStream(imported.Assets[0].Bytes);
        var decoded = BitmapFrame.Create(retainedImage, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        if (decoded.PixelWidth != width || decoded.PixelHeight != height)
            throw new IOException("The Word import fixture does not contain the complete, decodable PNG.");
        File.WriteAllText(Path.Combine(output, "native-word-import-fixture.json"), JsonSerializer.Serialize(new
        {
            Passed = true, Warnings = imported.Warnings, OriginalSha256 = Convert.ToHexString(SHA256.HashData(bytes)),
            ImageSha256 = Convert.ToHexString(SHA256.HashData(image)), ImportedImages = imported.Assets.Count, ImageDecoded = true,
            ImageWidth = decoded.PixelWidth, ImageHeight = decoded.PixelHeight,
            Markdown = imported.Markdown, UnsupportedFileLinkRemoved = true, StoredFieldResultRetained = true, InheritedRunStyles = true
        }, new JsonSerializerOptions { WriteIndented = true }));
        return new(path, bytes, image);
    }

    internal static void CheckMarkdown(string markdown)
    {
        var parsed = new MarkdownService().Parse(markdown);
        var quote = parsed.Syntax.Descendants<QuoteBlock>().SingleOrDefault()?.OfType<ParagraphBlock>().SingleOrDefault();
        if (!markdown.StartsWith("# Imported Café\n", StringComparison.Ordinal)
            || !markdown.Contains("Portable **bold** text", StringComparison.Ordinal)
            || !markdown.Contains("~~Removed~~ and ++underlined++", StringComparison.Ordinal)
            || !markdown.Contains("[guide](https://example.org/import)", StringComparison.Ordinal)
            || quote is null || MarkdownService.InlineText(quote.Inline) != "Retained quote Ω."
            || !markdown.Contains("Local destination", StringComparison.Ordinal)
            || !markdown.Contains("Stored author", StringComparison.Ordinal)
            || markdown.Contains("file://", StringComparison.OrdinalIgnoreCase)
            || MarkdownLinks.Read(markdown).Count(link => link.IsImage) != 1
            || !markdown.Contains("![Portable pixel](", StringComparison.Ordinal))
            throw new IOException("The imported Word fixture lost its heading, formatted text, safe link, quote, inert results or single image.");
    }
}
