using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using W = DocumentFormat.OpenXml.Wordprocessing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace LensDocsStudio.Core.Tests;

public sealed class WordImageDescriptionImportTests
{
    // These core fixtures isolate package metadata; the native fixture also decodes real PNG pixels.
    private static readonly byte[] Payload = [137, 80, 78, 71, 13, 10, 26, 10];

    [Theory]
    [InlineData("Architecture — Café Ω")]
    [InlineData("[Label](https://unrequested.invalid) *literal* &copy; <script>")]
    [InlineData("")]
    public void Exported_image_description_returns_as_literal_alternative_text(string description)
    {
        var bytes = Package(description);
        var original = bytes.ToArray();
        var imported = new DocumentImportService().ImportDocx(bytes);
        var image = Assert.Single(Images(imported.Markdown));
        Assert.Equal(description, MarkdownService.InlineText(image));
        Assert.Empty(image.Descendants<EmphasisInline>());
        Assert.Equal(Payload, Assert.Single(imported.Assets).Bytes);
        Assert.Empty(imported.Warnings);
        Assert.Equal(original, bytes);
    }

    [Theory]
    [InlineData("drawing-description", "Drawing description")]
    [InlineData("picture-description", "Picture description")]
    [InlineData("drawing-title", "Drawing title")]
    [InlineData("picture-title", "Picture title")]
    [InlineData("none", "Imported image")]
    public void Missing_description_uses_the_nearest_available_metadata_then_the_existing_fallback(string kind, string expected)
    {
        var bytes = Modify(Package("Original"), main =>
        {
            var drawing = main.Document!.Descendants<DW.DocProperties>().Single();
            var picture = main.Document.Descendants<PIC.NonVisualDrawingProperties>().Single();
            drawing.RemoveAttribute("descr", ""); drawing.RemoveAttribute("title", "");
            picture.RemoveAttribute("descr", ""); picture.RemoveAttribute("title", "");
            if (kind == "drawing-description") drawing.Description = "Drawing description";
            if (kind is "drawing-description" or "picture-description") picture.Description = "Picture description";
            if (kind is "drawing-description" or "picture-description" or "drawing-title") drawing.Title = "Drawing title";
            if (kind != "none") picture.Title = "Picture title";
        });
        Assert.Equal(expected, MarkdownService.InlineText(Assert.Single(Images(new DocumentImportService().ImportDocx(bytes).Markdown))));
    }

    [Fact]
    public void Explicit_empty_description_is_not_replaced_by_a_picture_description_or_title()
    {
        var bytes = Modify(Package(""), main =>
        {
            main.Document!.Descendants<DW.DocProperties>().Single().Title = "Title";
            main.Document.Descendants<PIC.NonVisualDrawingProperties>().Single().Description = "Picture description";
        });
        Assert.Equal("", MarkdownService.InlineText(Assert.Single(Images(new DocumentImportService().ImportDocx(bytes).Markdown))));
    }

    [Fact]
    public void Shared_image_bytes_keep_separate_descriptions_for_two_drawings_in_one_run()
    {
        var content = new ExportContent(new MarkdownService().Parse("![one](one.png)\n\n![two](two.png)"),
            new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>
            { ["one.png"] = new(Payload, 1, 1, "First image"), ["two.png"] = new(Payload, 1, 1, "Second image") });
        var bytes = Modify(new DocxExporter().Export(content), main =>
        {
            Assert.Single(main.ImageParts);
            var paragraphs = main.Document!.Body!.Elements<W.Paragraph>().ToArray();
            var drawings = main.Document.Descendants<W.Drawing>().ToArray();
            drawings[1].Remove(); paragraphs[0].Elements<W.Run>().First().Append(drawings[1]); paragraphs[1].Remove();
        });
        var imported = new DocumentImportService().ImportDocx(bytes);
        var images = Images(imported.Markdown);
        Assert.Equal(new[] { "First image", "Second image" }, images.Select(MarkdownService.InlineText));
        Assert.Single(images.Select(image => image.Url).Distinct());
        Assert.Equal(Payload, Assert.Single(imported.Assets).Bytes);
    }

    [Fact]
    public void Floating_picture_description_is_read_from_its_own_anchor()
    {
        var bytes = Modify(Package("Floating Café"), main =>
        {
            var inline = main.Document!.Descendants<DW.Inline>().Single();
            var anchor = new DW.Anchor(new DW.SimplePosition { X = 0, Y = 0 },
                new DW.HorizontalPosition(new DW.PositionOffset("0")) { RelativeFrom = DW.HorizontalRelativePositionValues.Page },
                new DW.VerticalPosition(new DW.PositionOffset("0")) { RelativeFrom = DW.VerticalRelativePositionValues.Page },
                inline.Extent!.CloneNode(true), new DW.WrapNone(), inline.DocProperties!.CloneNode(true),
                inline.NonVisualGraphicFrameDrawingProperties!.CloneNode(true), inline.Graphic!.CloneNode(true))
            { SimplePos = false, RelativeHeight = 0, BehindDoc = false, Locked = false, LayoutInCell = true, AllowOverlap = true };
            inline.Parent!.ReplaceChild(anchor, inline);
        });
        Assert.Equal("Floating Café", MarkdownService.InlineText(Assert.Single(Images(new DocumentImportService().ImportDocx(bytes).Markdown))));
    }

    [Fact]
    public void Line_breaks_in_image_descriptions_cannot_split_markdown_blocks()
    {
        var bytes = Package("First\r\n\r\n[Second](https://unrequested.invalid)\tΩ");
        var imported = new DocumentImportService().ImportDocx(bytes);
        var parsed = new MarkdownService().Parse(imported.Markdown);
        Assert.IsType<ParagraphBlock>(Assert.Single(parsed.Syntax));
        var image = Assert.Single(parsed.Syntax.Descendants<LinkInline>());
        Assert.True(image.IsImage);
        Assert.Equal("First  [Second](https://unrequested.invalid) Ω", MarkdownService.InlineText(image));
    }

    private static LinkInline[] Images(string markdown) => new MarkdownService().Parse(markdown).Syntax.Descendants<LinkInline>().Where(link => link.IsImage).ToArray();

    private static byte[] Package(string description) => new DocxExporter().Export(new(new MarkdownService().Parse("![image](image.png)"),
        new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage> { ["image.png"] = new(Payload, 1, 1, description) }));

    private static byte[] Modify(byte[] source, Action<MainDocumentPart> edit)
    {
        using var stream = new MemoryStream(); stream.Write(source); stream.Position = 0;
        using (var package = WordprocessingDocument.Open(stream, true))
        {
            edit(package.MainDocumentPart!); package.MainDocumentPart!.Document!.Save();
            Assert.Empty(new OpenXmlValidator(FileFormatVersions.Office2010).Validate(package));
        }
        return stream.ToArray();
    }
}
