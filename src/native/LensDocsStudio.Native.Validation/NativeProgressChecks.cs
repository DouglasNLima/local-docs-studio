using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LensDocsStudio.Core.Editing;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Exports;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

internal static class NativeProgressChecks
{
    public static void Run(string output)
    {
        var parser = new MarkdownService(); var source = "# Delivery review\n\n";
        foreach (var (label, value, colour) in new[] { ("Evidence & review", 37, "blue"), ("Not started", 0, "neutral"), ("Complete", 100, "green") })
        {
            var edit = ProgressEditService.Plan(parser.Parse(source), source.Length, 0, new(label, value, colour));
            source += edit.Replacement + "\n\n";
        }
        var parsed = parser.Parse(source);
        foreach (var dark in new[] { false, true })
        {
            var rendered = new NativeMarkdownRenderer(new NativeSvgService()).Render(parsed, dark,
                new Dictionary<Markdig.Syntax.FencedCodeBlock, DiagramResult>(), _ => { }, (_, _) => { });
            if (rendered.Warnings.Count != 0) throw new IOException("Recognised progress generated an unsupported-HTML warning.");
            var sections = rendered.Document.Blocks.OfType<Section>().ToArray();
            var expected = new[] { ("Evidence & review: 37%", 37d), ("Not started: 0%", 0d), ("Complete: 100%", 100d) };
            if (sections.Length != expected.Length) throw new IOException("Native progress sections are missing.");
            for (var index = 0; index < sections.Length; index++)
            {
                var section = sections[index]; var bar = (ProgressBar)section.Blocks.OfType<BlockUIContainer>().Single().Child;
                var peer = new ProgressBarAutomationPeer(bar); var range = (IRangeValueProvider?)peer.GetPattern(PatternInterface.RangeValue);
                if (range is null || range.Value != expected[index].Item2 || range.Minimum != 0 || range.Maximum != 100 || !range.IsReadOnly
                    || peer.GetName() != expected[index].Item1 || !new TextRange(section.ContentStart, section.ContentEnd).Text.Contains(expected[index].Item1, StringComparison.Ordinal)
                    || section.Tag is not SourceReference reference || !source[reference.Start..].StartsWith("<figure data-progress-bar", StringComparison.Ordinal)
                    || rendered.SourceElements[reference.Start] != section)
                    throw new IOException("Native progress lost its accessible range, selectable text or source mapping.");
            }
            rendered.Document.Background = new SolidColorBrush(dark ? Color.FromRgb(28, 30, 33) : Color.FromRgb(250, 250, 250));
            var paginator = ((IDocumentPaginatorSource)rendered.Document).DocumentPaginator; paginator.PageSize = new Size(900, 520);
            var page = paginator.GetPage(0);
            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            var layout = sections.Select(section => (ProgressBar)section.Blocks.OfType<BlockUIContainer>().Single().Child).Select(bar =>
            {
                bar.UpdateLayout();
                var track = bar.Template?.FindName("PART_Track", bar) as FrameworkElement;
                var indicator = bar.Template?.FindName("PART_Indicator", bar) as FrameworkElement;
                return new { bar.Value, bar.ActualWidth, Track = track?.ActualWidth, Indicator = indicator?.ActualWidth };
            }).ToArray();
            if (layout.Any(item => item.Track is not > 0 || item.Indicator is null || Math.Abs(item.Indicator.Value - item.Track.Value * item.Value / 100) > 1))
                throw new IOException("Native progress fills do not match their values after layout.");
            File.WriteAllText(Path.Combine(output, "native-progress-layout-" + (dark ? "dark" : "light") + ".json"), JsonSerializer.Serialize(layout));
            page = paginator.GetPage(0);
            var bitmap = new RenderTargetBitmap(900, 520, 96, 96, PixelFormats.Pbgra32); bitmap.Render(page.Visual);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var image = File.Create(Path.Combine(output, "native-progress-" + (dark ? "dark" : "light") + ".png")); encoder.Save(image);
        }
        var content = new ExportContent(parsed, new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>());
        var pdf = new PdfExporter().Export(content); File.WriteAllBytes(Path.Combine(output, "native-progress.pdf"), pdf);
        File.WriteAllBytes(Path.Combine(output, "native-progress.docx"), new DocxExporter().Export(content));
        File.WriteAllText(Path.Combine(output, "native-progress.html"), new HtmlExporter().Export(content));
        using var inspected = UglyToad.PdfPig.PdfDocument.Open(pdf);
        var text = string.Join(" ", inspected.GetPages().SelectMany(p => p.GetWords()).Select(w => w.Text));
        foreach (var value in new[] { "Evidence & review: 37%", "Not started: 0%", "Complete: 100%" })
            if (!text.Contains(value, StringComparison.Ordinal)) throw new IOException("PDF lost progress text: " + value);
        File.WriteAllText(Path.Combine(output, "native-progress.json"), JsonSerializer.Serialize(new
        {
            Passed = true, Checks = new[] { "Native WPF progress exposes read-only accessible values at 0, 37 and 100 percent",
                "Progress labels remain selectable and source-mapped without executing imported attributes", "Actual light/dark progress pages render with measured 0, 37 and 100 percent fills after layout",
                "Actual PDF retains progress labels and percentages; DOCX and HTML outputs are written" }, PdfText = text
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
