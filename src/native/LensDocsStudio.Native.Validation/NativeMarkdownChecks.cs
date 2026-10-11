using System.Text.Json;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Exports;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

internal static class NativeMarkdownChecks
{
    public static void Run(string output, string repository)
    {
        var parsed = new MarkdownService().Parse(File.ReadAllText(Path.Combine(repository, "tests", "fixtures", "native", "markdown-semantics.md")));
        string Text(TextElement element) => new TextRange(element.ContentStart, element.ContentEnd).Text;
        foreach (var dark in new[] { false, true })
        {
            var result = new NativeMarkdownRenderer(new NativeSvgService()).Render(parsed, dark,
                new Dictionary<Markdig.Syntax.FencedCodeBlock, DiagramResult>(), _ => { }, (_, _) => { });
            if (result.Warnings.Count != 0) throw new IOException(string.Join(" ", result.Warnings));
            var callout = result.Document.Blocks.OfType<Section>().Single();
            if (!Text(callout).Contains("Warning", StringComparison.Ordinal) || !Text(callout).Contains("Before publishing", StringComparison.Ordinal)
                || !Text(callout).Contains("Review evidence", StringComparison.Ordinal) || callout.Tag is not SourceReference source || parsed.Source[source.Start] != '>')
                throw new IOException("Native callout lost its label, formatted body or source mapping.");
            var lists = result.Document.Blocks.OfType<System.Windows.Documents.List>().ToArray();
            if (lists.Length != 2 || lists[0].StartIndex != 7 || lists[1].MarkerStyle != TextMarkerStyle.None
                || !Text(lists[1].ListItems.FirstListItem).TrimStart().StartsWith("0. Item zero", StringComparison.Ordinal)
                || !Text(lists[1].ListItems.LastListItem).TrimStart().StartsWith("1. Item one", StringComparison.Ordinal))
                throw new IOException("Native lists lost their ordered starting values: " + JsonSerializer.Serialize(lists.Select(l => new { l.StartIndex, l.MarkerStyle, First = Text(l.ListItems.FirstListItem), Last = Text(l.ListItems.LastListItem) })));
            var cells = result.Document.Blocks.OfType<Table>().Single().RowGroups[0].Rows[1].Cells;
            if (!cells.Select(c => c.TextAlignment).SequenceEqual(new[] { TextAlignment.Left, TextAlignment.Center, TextAlignment.Right }))
                throw new IOException("Native table cells lost their column alignment.");
            var paragraph = result.Document.Blocks.OfType<Paragraph>().Single(p => Text(p).Contains("Withdrawn", StringComparison.Ordinal));
            var spans = paragraph.Inlines.OfType<Span>().ToArray();
            var strike = spans.Single(s => Text(s) == "Withdrawn");
            if (strike.FontWeight != FontWeights.Normal || strike.FontStyle != FontStyles.Normal || !strike.TextDecorations.Any(d => d.Location == TextDecorationLocation.Strikethrough))
                throw new IOException("Strikethrough incorrectly inherited bold/italic or lost its decoration.");
            if (!spans.Any(s => Text(s) == "2" && s.BaselineAlignment == BaselineAlignment.Subscript)
                || !spans.Any(s => Text(s) == "3" && s.BaselineAlignment == BaselineAlignment.Superscript)
                || !spans.Any(s => Text(s) == "Inserted" && s.TextDecorations.Any(d => d.Location == TextDecorationLocation.Underline))
                || !spans.Any(s => Text(s) == "Marked" && s.Background is not null)) throw new IOException("Native extended emphasis lost its intended style.");
            var soft = result.Document.Blocks.OfType<Paragraph>().Single(p => Text(p).Contains("Soft first", StringComparison.Ordinal));
            var hard = result.Document.Blocks.OfType<Paragraph>().Single(p => Text(p).Contains("Hard first", StringComparison.Ordinal));
            if (!Text(soft).Contains("Soft first soft second.", StringComparison.Ordinal) || soft.Inlines.OfType<LineBreak>().Any()
                || hard.Inlines.OfType<LineBreak>().Count() != 1) throw new IOException("Native soft and hard line breaks are not distinct.");
            result.Document.Background = new SolidColorBrush(dark ? Color.FromRgb(28, 30, 33) : Color.FromRgb(250, 250, 250));
            var paginator = ((IDocumentPaginatorSource)result.Document).DocumentPaginator;
            paginator.PageSize = new Size(900, 1100);
            var page = paginator.GetPage(0);
            var bitmap = new RenderTargetBitmap(900, 1100, 96, 96, PixelFormats.Pbgra32); bitmap.Render(page.Visual);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var image = File.Create(Path.Combine(output, "native-semantics-" + (dark ? "dark" : "light") + ".png")); encoder.Save(image);
        }
        var content = new ExportContent(parsed, new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>());
        var pdf = new PdfExporter().Export(content);
        File.WriteAllBytes(Path.Combine(output, "native-semantics.pdf"), pdf);
        File.WriteAllBytes(Path.Combine(output, "native-semantics.docx"), new DocxExporter().Export(content));
        using var inspected = UglyToad.PdfPig.PdfDocument.Open(pdf);
        var words = string.Join(" ", inspected.GetPages().SelectMany(p => p.GetWords()).Select(w => w.Text));
        foreach (var expected in new[] { "Warning", "Before publishing", "7. Item seven", "0. Item zero", "~~Withdrawn~~", "Soft first soft second." })
            if (!words.Contains(expected, StringComparison.Ordinal)) throw new IOException("The PDF lost semantic fixture content: " + expected);
        File.WriteAllText(Path.Combine(output, "native-semantics.json"), JsonSerializer.Serialize(new
        {
            Passed = true,
            Checks = new[] { "Callouts retain kind, formatted body and source mapping", "Ordered lists retain seven and zero starting values",
                "Table columns retain left, centre and right alignment", "Independent emphasis retains strike, scripts, insertion and highlight",
                "Soft and hard line breaks remain distinct", "Light and dark native pages render to actual images",
                "PDF retains callout labels, ordered starts, soft lines and explicit deletion markers" },
            PdfLimitation = "MigraDoc has no strikeout font property; PDF retains visible Markdown deletion markers. PDF highlight shading is not yet implemented.",
            PdfText = words
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
