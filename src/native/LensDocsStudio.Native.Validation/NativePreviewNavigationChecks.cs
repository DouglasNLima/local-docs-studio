using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static class NativePreviewNavigationChecks
{
    public static void Run(string output)
    {
        var source = string.Join("\n\n", Enumerable.Range(1, 50).Select(index => "Earlier paragraph " + index))
            + "\n\n<details>\n<summary>Hidden evidence</summary>\n\nNative navigation needle.\n</details>\n\nAfter details.";
        var parsed = new MarkdownService().Parse(source);
        var rendered = new NativeMarkdownRenderer(new NativeSvgService()).Render(parsed, false, new Dictionary<FencedCodeBlock, DiagramResult>(), _ => { }, (_, _) => { });
        rendered.Document.Background = Brushes.White;
        var viewer = new FlowDocumentScrollViewer { Document = rendered.Document, IsToolBarVisible = false, Background = Brushes.White };
        using var host = new HwndSource(new HwndSourceParameters("Native preview navigation validation")
            { Width = 640, Height = 260, WindowStyle = unchecked((int)0x80000000) });
        host.RootVisual = viewer; Layout();
        var offset = source.IndexOf("Native navigation needle", StringComparison.Ordinal);
        rendered.RevealSource(offset);
        var paragraph = (Paragraph)rendered.SourceElements[offset]; var position = paragraph.Inlines.FirstInline.ContentStart;
        viewer.Selection.Select(position, position.GetPositionAtOffset("Native navigation needle".Length)!);
        (position.Parent as FrameworkContentElement)?.BringIntoView(); Layout();
        var beforeRectangle = position.GetCharacterRect(LogicalDirection.Forward);
        var task = PreviewNavigationService.BringIntoViewAsync(viewer, position);
        var frame = new DispatcherFrame(); task.ContinueWith(_ => viewer.Dispatcher.BeginInvoke(() => frame.Continue = false)); Dispatcher.PushFrame(frame);
        task.GetAwaiter().GetResult(); Layout();
        var rectangle = position.GetCharacterRect(LogicalDirection.Forward);
        var scrolling = (ScrollViewer)viewer.Template.FindName("PART_ContentHost", viewer);
        if (scrolling.VerticalOffset <= 0 || rectangle.IsEmpty || rectangle.Top < 0 || rectangle.Bottom > scrolling.ViewportHeight)
            throw new IOException("The revealed native preview target is outside its actual viewport: " + JsonSerializer.Serialize(new { scrolling.VerticalOffset, scrolling.ViewportHeight, Rectangle = rectangle.ToString() }));
        if (viewer.Selection.Text != "Native navigation needle" || parsed.Source != source) throw new IOException("Preview navigation changed its selection or source.");
        var visibleOffset = scrolling.VerticalOffset;
        var capture = new RenderTargetBitmap(640, 260, 96, 96, PixelFormats.Pbgra32); capture.Render(viewer);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(capture));
        using (var image = File.Create(Path.Combine(output, "native-preview-navigation.png"))) encoder.Save(image);
        foreach (var zoom in new[] { 50d, 125d, 200d })
        {
            viewer.MinZoom = 50; viewer.MaxZoom = 200; viewer.Zoom = zoom; scrolling.ScrollToTop(); Layout();
            task = PreviewNavigationService.BringIntoViewAsync(viewer, position);
            frame = new DispatcherFrame(); task.ContinueWith(_ => viewer.Dispatcher.BeginInvoke(() => frame.Continue = false)); Dispatcher.PushFrame(frame);
            task.GetAwaiter().GetResult(); Layout();
            var zoomed = position.GetCharacterRect(LogicalDirection.Forward);
            if (zoomed.IsEmpty || zoomed.Top < 0 || zoomed.Bottom > scrolling.ViewportHeight)
                throw new IOException("Preview navigation lost visible selection at zoom " + zoom + ": " + zoomed);
        }
        var oldPosition = position; var replacement = new FlowDocument(new Paragraph(new Run("New document"))); viewer.Document = replacement;
        PreviewNavigationService.BringIntoViewAsync(viewer, oldPosition).GetAwaiter().GetResult();
        task = PreviewNavigationService.BringIntoViewAsync(viewer, replacement.ContentStart);
        // Replacing the viewer again before deferred layout must invalidate pending navigation.
        viewer.Document = new FlowDocument(new Paragraph(new Run("Latest document")));
        frame = new DispatcherFrame(); task.ContinueWith(_ => viewer.Dispatcher.BeginInvoke(() => frame.Continue = false)); Dispatcher.PushFrame(frame); task.GetAwaiter().GetResult();
        if (!new TextRange(viewer.Document.ContentStart, viewer.Document.ContentEnd).Text.Contains("Latest document", StringComparison.Ordinal)) throw new IOException("Stale preview navigation replaced the current document.");
        File.WriteAllText(Path.Combine(output, "native-preview-navigation.json"), JsonSerializer.Serialize(new { Passed = true,
            Checks = new[] { "Hidden details target becomes visible after actual FlowDocument layout", "Exact selection and source survive scrolling", "Superseded document navigation is ignored", "Navigation stays visible at 50%, 125% and 200% preview zoom" },
            BeforeRectangle = beforeRectangle.ToString(), VerticalOffset = visibleOffset, Rectangle = rectangle.ToString() }));
        void Layout() { viewer.Measure(new Size(640, 260)); viewer.Arrange(new Rect(0, 0, 640, 260)); viewer.UpdateLayout(); }
    }
}
