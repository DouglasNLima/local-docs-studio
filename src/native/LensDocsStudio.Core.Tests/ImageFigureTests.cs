using AngleSharp.Html.Parser;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Review;
using LensDocsStudio.Core.Workspace;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Core.Tests;

public sealed class ImageFigureTests
{
    private const string Figure = "<figure data-image-figure>\r\n  <img alt='Café &amp; map' src=\"assets/a&amp;b.png\" title='A &quot;title&quot;'>\r\n  <figcaption>Plan &amp; evidence 🛡️</figcaption>\r\n</figure>";
    private const string Source = "# Figure\r\n\r\n" + Figure + "\r\n\r\nAfter.";
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aX1sAAAAASUVORK5CYII=");
    private static ExportContent Content(string source = Source) => new(new MarkdownService().Parse(source), new Dictionary<int, ExportImage>(),
        new Dictionary<string, ExportImage> { ["assets/a&b.png"] = new(Png, 1, 1, "Café & map") });

    [Fact] public void Figure_retains_decoded_content_and_exact_original_image_and_URL_spans()
    {
        var parsed = new MarkdownService().Parse(Source);
        var figure = Assert.Single(parsed.Syntax.Descendants<DocumentImageFigureBlock>());
        Assert.Equal(Figure, Source.Substring(figure.Span.Start, figure.Span.Length));
        var image = Assert.IsType<FigureImageInline>(Assert.Single(parsed.Syntax.Descendants<LinkInline>()));
        Assert.Equal("Café & map", MarkdownService.InlineText(image)); Assert.Equal("Plan & evidence 🛡️", figure.Caption);
        Assert.Equal("A \"title\"", image.Title); Assert.Equal("assets/a&b.png", image.Url);
        Assert.Equal("assets/a&amp;b.png", Source.Substring(image.UrlSpan.Start, image.UrlSpan.Length));
        Assert.StartsWith("<img alt=", Source.Substring(image.Span.Start, image.Span.Length));
        Assert.Equal(3, image.Line); Assert.Same(figure.Inline, image.Parent);
        Assert.Equal(Source, parsed.Source);
    }

    [Fact] public void Rewriting_a_figure_changes_only_its_URL_and_escapes_HTML_attribute_characters()
    {
        var link = Assert.Single(MarkdownLinks.Read(Source)); Assert.True(link.HtmlEncoded); Assert.True(link.IsImage);
        var rewritten = MarkdownLinks.Rewrite(Source, [(link, "new/a&b'\".png")]);
        Assert.Equal(Source.Replace("assets/a&amp;b.png", "new/a&amp;b&#39;&quot;.png"), rewritten);
        Assert.Equal("new/a&b'\".png", Assert.Single(MarkdownLinks.Read(rewritten)).Reference);
    }

    [Theory]
    [InlineData("<figure data-image-figure><img src='a.png'></figure>")]
    [InlineData("<FIGURE DATA-IMAGE-FIGURE=''><IMG SRC='a.png' ALT='' /></FIGURE>")]
    [InlineData("<figure data-image-figure><img src='a.png'><figcaption></figcaption></figure>")]
    public void Caption_and_alt_can_be_empty_without_losing_the_image(string source)
    {
        var figure = Assert.Single(new MarkdownService().Parse(source).Syntax.Descendants<DocumentImageFigureBlock>());
        Assert.Equal("a.png", figure.Image.Url); Assert.Equal("", figure.Caption);
        var note = Assert.Single(new DocumentReviewService().Analyse("# Title\n\n" + source).Issues);
        Assert.Equal("Alt text", note.Rule); Assert.Contains("alt attribute", note.Suggestion);
    }

    [Theory]
    [InlineData("<figure><img src='a.png'></figure>")]
    [InlineData("<figure data-image-figure onclick='run()'><img src='a.png'></figure>")]
    [InlineData("<figure data-image-figure><img src='a.png' srcset='b.png 2x'></figure>")]
    [InlineData("<figure data-image-figure><img src='a.png' src='b.png'></figure>")]
    [InlineData("<figure data-image-figure><img src='a.png' style='background:url(b.png)'></figure>")]
    [InlineData("<figure data-image-figure><img src=a.png></figure>")]
    [InlineData("<figure data-image-figure><img src='a.png'><img src='b.png'></figure>")]
    [InlineData("<figure data-image-figure><img src='a.png'><figcaption><b>Caption</b></figcaption></figure>")]
    [InlineData("<figure data-image-figure><!-- hidden --><img src='a.png'></figure>")]
    [InlineData("<figure data-image-figure><img src='a.png'>")]
    [InlineData("<figure data-image-figure><img src='a.png'><figcaption>Unfinished")]
    [InlineData("<figure data-image-figure><img src='a.png'   ")]
    public void Unknown_or_malformed_figures_remain_inert_original_HTML(string source)
    {
        var parsed = new MarkdownService().Parse(source);
        Assert.Empty(parsed.Syntax.Descendants<DocumentImageFigureBlock>());
        Assert.Equal(source, parsed.Source); Assert.NotEmpty(parsed.Syntax.Descendants<HtmlBlock>());
        var output = new HtmlExporter().Export(new(parsed, new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>()));
        Assert.Empty(new HtmlParser().ParseDocument(output).QuerySelectorAll("img,script,figure"));
    }

    [Fact] public void Caption_limits_and_code_examples_preserve_literal_source()
    {
        var source = "<figure data-image-figure><img src='a.png'><figcaption>" + new string('a', 4097) + "</figcaption></figure>";
        Assert.Empty(new MarkdownService().Parse(source).Syntax.Descendants<DocumentImageFigureBlock>());
        Assert.Empty(new MarkdownService().Parse("```html\n" + Figure + "\n```").Syntax.Descendants<DocumentImageFigureBlock>());
        var review = new DocumentReviewService().Analyse("# Title\n\n<figure data-image-figure><img src='a.png' alt='Diagram'><figcaption>\n|A|B|\n|---|---|\n|extra|cell|here|\n</figcaption></figure>");
        Assert.Empty(review.Issues); Assert.Equal(1, review.Metrics.Images);
    }

    [Fact] public void Every_unfinished_figure_prefix_remains_literal_without_a_parser_failure()
    {
        for (var length = 0; length < Figure.Length; length++)
        {
            var source = Figure[..length]; var parsed = new MarkdownService().Parse(source);
            Assert.Empty(parsed.Syntax.Descendants<DocumentImageFigureBlock>()); Assert.Equal(source, parsed.Source);
        }
    }

    [Fact] public void Mixed_Markdown_and_figure_images_share_one_pasted_asset_without_losing_surrounding_source()
    {
        var source = Source + "\r\n\r\n![Same image](assets/a&b.png)\r\n";
        var copied = new MarkdownClipboardExporter().Export(Content(source));
        var pasted = new MarkdownImageImporter().Import(copied.PlainText); var asset = Assert.Single(pasted.Assets);
        Assert.Equal(source.Replace("assets/a&amp;b.png", asset.Reference).Replace("assets/a&b.png", asset.Reference), pasted.Markdown);
        Assert.Equal(2, MarkdownLinks.Read(pasted.Markdown).Count);
    }

    [Fact] public void HTML_and_Word_export_keep_real_images_captions_and_caption_style()
    {
        var content = Content();
        var html = new HtmlParser().ParseDocument(new HtmlExporter().Export(content));
        var figure = Assert.Single(html.QuerySelectorAll("figure"));
        Assert.Equal("Plan & evidence 🛡️", figure.QuerySelector("figcaption")!.TextContent);
        Assert.Equal("Café & map", figure.QuerySelector("img")!.GetAttribute("alt"));
        Assert.Equal("data:image/png;base64," + Convert.ToBase64String(Png), figure.QuerySelector("img")!.GetAttribute("src"));
        using var stream = new MemoryStream(new DocxExporter().Export(content));
        using var package = WordprocessingDocument.Open(stream, false);
        Assert.Empty(new OpenXmlValidator().Validate(package));
        Assert.Equal(Png, Read(package.MainDocumentPart!.ImageParts.Single()));
        var caption = package.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>().Single(p => p.InnerText == "Plan & evidence 🛡️");
        Assert.Equal("Caption", caption.ParagraphProperties!.ParagraphStyleId!.Val!.Value);
        var text = new ClipboardExporter().Export(content).PlainText;
        Assert.Contains("[Café & map]\nPlan & evidence 🛡️", text);
        static byte[] Read(ImagePart part) { using var input = part.GetStream(); using var buffer = new MemoryStream(); input.CopyTo(buffer); return buffer.ToArray(); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Markdown_clipboard_round_trip_preserves_figure_markup_and_portable_image_bytes(bool htmlOnly)
    {
        var copy = new MarkdownClipboardExporter().Export(Content());
        Assert.Equal(Source.Replace("assets/a&amp;b.png", "data:image/png;base64," + Convert.ToBase64String(Png)), copy.PlainText);
        var paste = new ClipboardPasteService().Convert(new(htmlOnly ? "" : copy.PlainText, copy.Html), PasteMode.Auto).Document;
        var asset = Assert.Single(paste.Assets); Assert.Equal(Png, asset.Bytes);
        var figure = Assert.Single(new MarkdownService().Parse(paste.Markdown).Syntax.Descendants<DocumentImageFigureBlock>());
        Assert.Equal(asset.Reference, figure.Image.Url); Assert.Equal("Café & map", MarkdownService.InlineText(figure.Image)); Assert.Equal("Plan & evidence 🛡️", figure.Caption);
        if (!htmlOnly) Assert.Equal(Source.Replace("assets/a&amp;b.png", asset.Reference), paste.Markdown);
        Assert.DoesNotContain("data:image", paste.Markdown);
    }

    [Fact] public void Large_embedded_figure_images_are_persisted_beyond_the_small_HTML_component_limit()
    {
        var bytes = Enumerable.Range(0, 20000).Select(i => (byte)i).ToArray();
        var source = Source.Replace("assets/a&amp;b.png", "data:image/png;base64," + Convert.ToBase64String(bytes));
        var paste = new MarkdownImageImporter().Import(source); var asset = Assert.Single(paste.Assets);
        Assert.Equal(bytes, asset.Bytes); Assert.Equal(Source.Replace("assets/a&amp;b.png", asset.Reference), paste.Markdown);
    }

    [Fact] public async Task Asset_audit_and_portable_Save_as_use_the_exact_figure_URL()
    {
        var root = Path.Combine(Path.GetTempPath(), "lens-figure-" + Guid.NewGuid().ToString("N"));
        try
        {
            var original = Path.Combine(root, "original"); var target = Path.Combine(root, "copy");
            Directory.CreateDirectory(Path.Combine(original, "assets")); Directory.CreateDirectory(target);
            var path = Path.Combine(original, "guide.md"); await File.WriteAllTextAsync(path, Source);
            await File.WriteAllBytesAsync(Path.Combine(original, "assets", "a&b.png"), Png);
            var audit = await new WorkspaceAssetService(new DocumentStore(Path.Combine(root, "recovery"))).AuditAsync(new(original));
            Assert.True(audit.UsageComplete); var use = Assert.Single(Assert.Single(audit.Assets).Uses); Assert.True(use.Markdown);
            await using var transfer = await new AssetTransferService().PrepareAsync(Source, path, new(original), Path.Combine(target, "guide.md"), new(target));
            var image = Assert.Single(MarkdownLinks.Read(transfer.Text)); var copied = new WorkspacePath(target).ResolveLink(Path.Combine(target, "guide.md"), image.Reference);
            Assert.Equal(Png, await File.ReadAllBytesAsync(copied));
            Assert.Equal(Source.Replace("assets/a&amp;b.png", System.Net.WebUtility.HtmlEncode(image.Reference)), transfer.Text);
            Assert.Equal(Source, await File.ReadAllTextAsync(path));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
