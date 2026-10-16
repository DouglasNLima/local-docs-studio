using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Exports;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static class NativeFigureChecks
{
    public static void Run(string output)
    {
        var root = Path.Combine(output, "figure-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        const int width = 320, height = 120;
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
        {
            var offset = (y * width + x) * 4;
            var ink = y is >= 20 and < 100 && (x is >= 25 and < 130 or >= 190 and < 295);
            pixels[offset] = ink ? (byte)95 : (byte)240; pixels[offset + 1] = ink ? (byte)77 : (byte)240;
            pixels[offset + 2] = ink ? (byte)57 : (byte)240; pixels[offset + 3] = 255;
        }
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4); bitmap.Freeze();
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var imagePath = Path.Combine(root, "figure.png"); using (var file = File.Create(imagePath)) encoder.Save(file);
        const string source = "# Image figure\r\n\r\n<figure data-image-figure>\r\n<img src=\"figure.png\" alt=\"Two review stages\">\r\n<figcaption>Figure 1 — Café &amp; evidence</figcaption>\r\n</figure>\r\n\r\nSource remains portable.";
        var path = Path.Combine(root, "source.md"); File.WriteAllText(path, source);
        var parsed = new MarkdownService().Parse(source); var figure = parsed.Syntax.Descendants<DocumentImageFigureBlock>().Single();
        var prepared = new ExportPreparationService(new MarkdownService(), new DiagramService(), new NativeSvgService()).Prepare(source, path, root, false);
        if (prepared.Images.Count != 1 || prepared.Images["figure.png"].PixelWidth != width) throw new IOException("Figure export preparation did not resolve its workspace image.");
        foreach (var dark in new[] { false, true })
        {
            BitmapSource? copied = null;
            var previewImages = new PreviewImageService().Prepare(parsed, path, root);
            var preparedPixels = previewImages["figure.png"].Pixels ?? throw new IOException("The native preview did not prepare the figure image.");
            var actualPixels = new byte[pixels.Length];
            new FormatConvertedBitmap(preparedPixels, PixelFormats.Bgra32, null, 0).CopyPixels(actualPixels, width * 4, 0);
            if (!actualPixels.SequenceEqual(pixels)) throw new IOException("Figure preview preparation changed its pixels.");
            var rendered = new NativeMarkdownRenderer(new NativeSvgService()).Render(parsed, dark, new Dictionary<FencedCodeBlock, DiagramResult>(), _ => { }, (_, _) => { },
                copyImage: image => copied = image, preparedImages: previewImages);
            if (rendered.Warnings.Count != 0) throw new IOException("A recognised figure produced an unsupported-HTML warning.");
            var section = (Section)rendered.SourceElements[figure.Span.Start];
            var image = (Image)((InlineUIContainer)((Paragraph)section.Blocks.FirstBlock).Inlines.FirstInline).Child;
            if (AutomationProperties.GetName(image) != "Two review stages" || image.Source != preparedPixels || section.Tag is not SourceReference reference
                || source.Substring(reference.Start, reference.End - reference.Start + 1) != source.Substring(figure.Span.Start, figure.Span.Length)
                || !new TextRange(section.ContentStart, section.ContentEnd).Text.Contains("Figure 1 — Café & evidence", StringComparison.Ordinal))
                throw new IOException("Native figure lost its pixels, accessible description, selectable caption or source mapping.");
            ((MenuItem)image.ContextMenu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            if (copied != preparedPixels) throw new IOException("Native figure copying lost the prepared image.");
            rendered.Document.Background = new SolidColorBrush(dark ? Color.FromRgb(28, 30, 33) : Color.FromRgb(250, 250, 250));
            var paginator = ((IDocumentPaginatorSource)rendered.Document).DocumentPaginator; paginator.PageSize = new Size(800, 430);
            var page = paginator.GetPage(0);
            var capture = new RenderTargetBitmap(800, 430, 96, 96, PixelFormats.Pbgra32); capture.Render(page.Visual);
            var imageEncoder = new PngBitmapEncoder(); imageEncoder.Frames.Add(BitmapFrame.Create(capture));
            using var file = File.Create(Path.Combine(output, "native-figure-" + (dark ? "dark" : "light") + ".png")); imageEncoder.Save(file);
        }
        var pdf = new PdfExporter().Export(prepared); File.WriteAllBytes(Path.Combine(output, "native-figure.pdf"), pdf);
        File.WriteAllBytes(Path.Combine(output, "native-figure.docx"), new DocxExporter().Export(prepared));
        File.WriteAllText(Path.Combine(output, "native-figure.html"), new HtmlExporter().Export(prepared));
        using var inspected = UglyToad.PdfPig.PdfDocument.Open(pdf);
        var text = string.Join(" ", inspected.GetPages().SelectMany(page => page.GetWords()).Select(word => word.Text));
        if (!text.Contains("Figure 1 — Café & evidence", StringComparison.Ordinal) || inspected.GetPages().Sum(page => page.NumberOfImages) != 1)
            throw new IOException("The actual PDF lost its figure caption or image.");
        File.Delete(imagePath);
        var missing = new NativeMarkdownRenderer(new NativeSvgService()).Render(parsed, false, new Dictionary<FencedCodeBlock, DiagramResult>(), _ => { }, (_, _) => { });
        if (missing.Warnings.Count != 1 || !new TextRange(missing.Document.ContentStart, missing.Document.ContentEnd).Text.Contains("Figure 1 — Café & evidence", StringComparison.Ordinal)
            || File.ReadAllText(path) != source) throw new IOException("A missing figure image hid its caption or changed source.");
        File.WriteAllText(Path.Combine(output, "native-figure.json"), JsonSerializer.Serialize(new
        {
            Passed = true, Checks = new[] { "Figure images retain accessible descriptions, copy actions and exact source mapping",
                "Selectable captions and actual images render in light and dark WPF pages", "Native preparation and actual PDF/DOCX/HTML retain image and caption content",
                "Missing figure images retain visible captions and unchanged source" }, PdfText = text
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
