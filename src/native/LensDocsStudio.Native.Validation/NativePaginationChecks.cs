using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativePaginationChecks
{
    public static void Run(string output)
    {
        var application = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var resources = application.Resources;
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        try
        {
            application.Resources = new ResourceDictionary(); ThemeService.Apply(application.Resources, "Light");
            var styles = new ResourceDictionary { Source = new Uri("pack://application:,,,/LensDocsStudio.Native;component/Rendering/DocumentPreview.xaml") };
            var source = "# Large reading fixture\n\n" + string.Join('\n', Enumerable.Range(1, 4000)
                .Select(index => $"- Item {index:D4}: Native documentation keeps **portable text** and source-aware navigation."));
            var parsed = new MarkdownService().Parse(source);
            var renderer = new NativeMarkdownRenderer(new NativeSvgService());
            var prepared = renderer.RenderAsync(parsed, false, new Dictionary<FencedCodeBlock, DiagramResult>(), _ => { }, (_, _) => { });
            Complete(prepared);
            var result = prepared.Result;
            var before = result.ReadText();
            var viewer = new DocumentPreview { Style = (Style)styles["DocumentPreviewStyle"] };
            viewer.SetPreview(result, source.Length, "Automatic");
            using var host = new HwndSource(new HwndSourceParameters("Hidden native pagination validation")
                { Width = 800, Height = 480, WindowStyle = unchecked((int)0x80000000) });
            var watch = Stopwatch.StartNew(); double firstInputMilliseconds = -1;
            viewer.Dispatcher.BeginInvoke(DispatcherPriority.Input, () => firstInputMilliseconds = watch.Elapsed.TotalMilliseconds);
            host.RootVisual = viewer; Layout(viewer);
            while (firstInputMilliseconds < 0) Pump();
            if (viewer.ViewingMode != FlowDocumentReaderViewingMode.Page) throw new IOException("Automatic reading did not paginate the long fixture.");
            CheckAccessiblePageCounter(viewer);
            CheckAccessibleText(viewer, "Item 4000");
            var target = Find(result.Document, "Item 4000");
            var beats = 0; double worstInputMilliseconds = 0;
            var gap = Stopwatch.StartNew();
            var heartbeat = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(10) };
            heartbeat.Tick += (_, _) => { beats++; worstInputMilliseconds = Math.Max(worstInputMilliseconds, gap.Elapsed.TotalMilliseconds); gap.Restart(); };
            heartbeat.Start(); watch.Restart();
            var navigation = PreviewNavigationService.BringIntoViewAsync(viewer, target, target.GetPositionAtOffset(9));
            Complete(navigation); heartbeat.Stop();
            var navigationMilliseconds = watch.Elapsed.TotalMilliseconds;
            if (!navigation.Result || viewer.PageNumber <= 1 || viewer.Selection is not { } selected
                || selected.Start.CompareTo(target) != 0 || selected.End.CompareTo(target.GetPositionAtOffset(9)!) != 0
                || !selected.Text.EndsWith("Item 4000", StringComparison.Ordinal))
                throw new IOException("Paged navigation did not reveal and select the final item: " + JsonSerializer.Serialize(new
                { navigation.Result, viewer.PageNumber, viewer.PageCount, selection = viewer.Selection?.Text, viewer.IsLoaded, viewer.IsVisible }));
            Layout(viewer);
            CheckAccessiblePageCounter(viewer);
            var rectangle = target.GetCharacterRect(LogicalDirection.Forward);
            if (rectangle.IsEmpty || rectangle.Top < 0 || rectangle.Bottom > viewer.ActualHeight)
                throw new IOException("The selected paginated target is outside the visible page: " + rectangle);
            Pump(); Layout(viewer);
            Save(viewer, Path.Combine(output, "native-paginated-last-page.png"));
            if (!viewer.IsInactiveSelectionHighlightEnabled) throw new IOException("Find selection highlighting is disabled when the reader loses focus.");
            // WPF requires a previously focused text selection to render this highlight.
            // Its actual pixels are checked by the isolated desktop workflow, not this unfocused HWND.
            var checks = new List<string> { "Long documents use native pages while retaining all 4,000 items",
                "Asynchronous final-page navigation exposes the exact selectable target" };

            var first = Find(result.Document, "Item 0001");
            var superseded = PreviewNavigationService.BringIntoViewAsync(viewer, first);
            var latest = PreviewNavigationService.BringIntoViewAsync(viewer, target);
            Complete(Task.WhenAll(superseded, latest));
            if (superseded.Result || !latest.Result) throw new IOException("An earlier page request displaced the latest navigation.");
            var changedSelection = PreviewNavigationService.BringIntoViewAsync(viewer, first);
            viewer.Selection!.Select(target, target.GetPositionAtOffset(4)); Complete(changedSelection);
            if (changedSelection.Result || viewer.Selection.Start.CompareTo(target) != 0
                || viewer.Selection.End.CompareTo(target.GetPositionAtOffset(4)!) != 0)
                throw new IOException("Deferred page navigation changed a newer selection.");
            checks.Add("Superseded requests and intervening selection changes are ignored");
            var stale = PreviewNavigationService.BringIntoViewAsync(viewer, first);
            var small = renderer.Render(new MarkdownService().Parse("# Replacement\n\nRetained replacement."), false,
                new Dictionary<FencedCodeBlock, DiagramResult>(), _ => { }, (_, _) => { });
            viewer.SetPreview(small, 40, "Automatic"); Complete(stale); Layout(viewer);
            if (stale.Result || viewer.ViewingMode != FlowDocumentReaderViewingMode.Scroll || !small.ReadText().Contains("Retained replacement.", StringComparison.Ordinal))
                throw new IOException("A pending request changed the replacement preview.");
            checks.Add("Automatic returns to scrolling for a small replacement and rejects stale document navigation");
            viewer.ApplyReadingMode("Pages"); Layout(viewer); Pump();
            if (viewer.ViewingMode != FlowDocumentReaderViewingMode.Page) throw new IOException("Explicit page reading was not applied.");
            viewer.ApplyReadingMode("Continuous"); Layout(viewer); Pump();
            if (viewer.ViewingMode != FlowDocumentReaderViewingMode.Scroll) throw new IOException("Explicit continuous reading was not applied.");
            CheckAccessibleText(viewer, "Retained replacement.");
            if (result.ReadText() != before || parsed.Source != source) throw new IOException("Changing reading modes changed document content or source.");
            checks.Add("Explicit reading choices preserve source and complete preview content");

            // Exercise both established cost characteristics without adding another large-layout timing test.
            foreach (var (characters, elements) in new[] { (256 * 1024 - 1, 999), (256 * 1024, 1), (40, 1000) })
            {
                var probe = small with { SourceElements = Enumerable.Range(0, elements).ToDictionary(index => index, _ => (TextElement)small.Document.Blocks.FirstBlock) };
                viewer.SetPreview(probe, characters, "Continuous"); Layout(viewer);
                var expected = characters >= 256 * 1024 || elements >= 1000;
                var guidance = (FrameworkElement)viewer.Template.FindName("ContinuousGuidance", viewer);
                viewer.Selection!.Select(small.Document.ContentStart, small.Document.ContentEnd);
                var selection = viewer.Selection.Text; var page = viewer.PageNumber;
                viewer.ApplyReadingMode("Continuous"); Layout(viewer);
                if (viewer.ShowContinuousGuidance != expected || guidance.IsVisible != expected
                    || viewer.ViewingMode != FlowDocumentReaderViewingMode.Scroll || viewer.Selection.Text != selection || viewer.PageNumber != page)
                    throw new IOException("Continuous guidance changed selection, position or explicit reading mode, or used the wrong document characteristics.");
                var readingPeer = FrameworkElementAutomationPeer.CreatePeerForElement(viewer);
                var accessibleGuidance = PeerDescendants(readingPeer).Any(peer => peer.GetAutomationId() == "ContinuousGuidance" && peer.IsControlElement() && !peer.IsOffscreen());
                if (accessibleGuidance != expected) throw new IOException("Visible Continuous guidance is absent from the reader's native automation tree.");
                viewer.ApplyReadingMode("Pages"); Layout(viewer);
                if (viewer.ShowContinuousGuidance || guidance.IsVisible) throw new IOException("Continuous guidance remained visible in Pages.");
            }
            viewer.SetPreview(small, 40, "Automatic"); Layout(viewer);
            if (viewer.ShowContinuousGuidance) throw new IOException("Continuous guidance survived a small replacement.");
            checks.Add("Continuous guidance uses source characters or mapped-element counts, preserves selection and explicit mode, and clears in Pages or a small replacement");

            var drawing = new DrawingVisual();
            using (var dc = drawing.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 200, 1200));
                dc.DrawRectangle(Brushes.Red, null, new Rect(0, 0, 200, 100));
                dc.DrawRectangle(Brushes.Green, null, new Rect(0, 1100, 200, 100));
            }
            var tall = new RenderTargetBitmap(200, 1200, 96, 96, PixelFormats.Pbgra32); tall.Render(drawing); tall.Freeze();
            const string mediaSource = "# Tall image\n\n![Retained tall image](tall.png)\n\nAfter the image.";
            var mediaDocument = new MarkdownService().Parse(mediaSource);
            var mediaResult = renderer.Render(mediaDocument, false, new Dictionary<FencedCodeBlock, DiagramResult>(), _ => { }, (_, _) => { },
                preparedImages: new Dictionary<string, PreviewImage> { ["tall.png"] = new(tall, null) });
            var image = mediaResult.Media.Single().Image;
            foreach (var zoom in new[] { 50, 100, 200 })
            {
                viewer.Zoom = zoom; viewer.SetPreview(mediaResult, mediaSource.Length, "Pages"); Layout(viewer); Pump(); Layout(viewer);
                var imagePosition = ((InlineUIContainer)image.Parent).ElementStart;
                var showImage = PreviewNavigationService.BringIntoViewAsync(viewer, imagePosition);
                Complete(showImage); Layout(viewer); Pump(); Layout(viewer);
                if (!showImage.Result) throw new IOException("Paged image navigation did not complete.");
                var bounds = image.TransformToAncestor(viewer).TransformBounds(new Rect(image.RenderSize));
                var footer = (FrameworkElement)viewer.Template.FindName("PageControls", viewer);
                var pageBottom = footer.TranslatePoint(new Point(), viewer).Y;
                if (bounds.Top < -1 || bounds.Left < -1 || bounds.Right > viewer.ActualWidth + 1 || bounds.Bottom > pageBottom + 1)
                    throw new IOException($"A paginated image was clipped at {zoom}%: {bounds}; page bottom {pageBottom}.");
                Save(viewer, Path.Combine(output, $"native-paginated-image-{zoom}.png"));
            }
            viewer.ApplyReadingMode("Continuous"); Layout(viewer); Pump();
            if (image.MaxHeight != 720 || image.MaxWidth != 720 || !ReferenceEquals(image.Source, tall) || mediaDocument.Source != mediaSource)
                throw new IOException("Returning to scrolling changed the original image or source.");
            checks.Add("Tall images fit the visible page at 50%, 100% and 200%, retaining original pixels and scrolling limits");
            CheckDiagram(viewer, renderer, output);
            checks.Add("Tall native diagrams retain all four working actions in narrow, zoomed light and dark pages and restore continuous layout");
            host.RootVisual = null;
            File.WriteAllText(Path.Combine(output, "native-pagination.json"), JsonSerializer.Serialize(new
            {
                passed = checks.Count, checks, sourceCharacters = source.Length, firstInputMilliseconds, navigationMilliseconds,
                inputPulsesDuringNavigation = beats, worstInputMilliseconds,
                limitation = "Hidden native HWND measurements on this machine; no universal latency threshold or physical DPI certification."
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { application.Resources = resources; SynchronizationContext.SetSynchronizationContext(previousContext); }
    }

    private static void CheckAccessibleText(DocumentPreview viewer, string expected)
    {
        var peer = FrameworkElementAutomationPeer.CreatePeerForElement(viewer);
        if (peer.GetPattern(PatternInterface.Text) is not ITextProvider provider
            || !provider.DocumentRange.GetText(-1).Contains(expected, StringComparison.Ordinal))
            throw new IOException("The reading control does not expose its complete document through native text automation.");
    }

    private static IEnumerable<AutomationPeer> PeerDescendants(AutomationPeer parent)
    {
        foreach (var child in parent.GetChildren() ?? [])
        {
            yield return child;
            foreach (var descendant in PeerDescendants(child)) yield return descendant;
        }
    }

    private static void CheckAccessiblePageCounter(DocumentPreview viewer)
    {
        var counter = (FrameworkElement)viewer.Template.FindName("PageNumber", viewer);
        var peer = FrameworkElementAutomationPeer.CreatePeerForElement(counter);
        if (!peer.IsControlElement() || peer.GetAutomationId() != "PreviewPageNumber"
            || peer.GetName() != $"Page {viewer.PageNumber} of {viewer.PageCount}")
            throw new IOException("The current page counter is missing from native control-view automation or has an obsolete accessible name: "
                + JsonSerializer.Serialize(new { control = peer.IsControlElement(), id = peer.GetAutomationId(), name = peer.GetName(), viewer.PageNumber, viewer.PageCount }));
    }

    private static void CheckDiagram(DocumentPreview viewer, NativeMarkdownRenderer renderer, string output)
    {
        var source = "```mermaid\nflowchart TD\n" + string.Join('\n', Enumerable.Range(1, 16)
            .Select(index => $"N{index}[Stage {index}] --> N{index + 1}[Stage {index + 1}]")) + "\n```\n\nAfter the diagram.";
        foreach (var dark in new[] { false, true })
        {
            ThemeService.Apply(Application.Current.Resources, dark ? "Dark" : "Light");
            // This hidden HWND has no Window in Application.Windows to receive theme invalidation.
            // Local resources exercise the actual dynamic style in each theme.
            ThemeService.Apply(viewer.Resources, dark ? "Dark" : "Light");
            var parsed = new MarkdownService().Parse(source);
            var block = parsed.Syntax.OfType<FencedCodeBlock>().Single();
            var diagram = new DiagramService().Render(block.Lines.ToString(), dark);
            if (!diagram.Success) throw new IOException("The pagination diagram fixture failed: " + diagram.Error);
            var actions = new List<(string Format, string Content)>();
            var result = renderer.Render(parsed, dark, new Dictionary<FencedCodeBlock, DiagramResult> { [block] = diagram },
                _ => { }, (format, content) => actions.Add((format, content)));
            var media = result.Media.Single(); var original = media.Image.Source;
            viewer.Zoom = 200; viewer.SetPreview(result, source.Length, "Pages");
            for (var pass = 0; pass < 3; pass++) { Layout(viewer, 300, 360); Pump(); }
            var navigate = PreviewNavigationService.BringIntoViewAsync(viewer, media.Host!.ContentStart);
            Complete(navigate); Layout(viewer, 300, 360); Pump(); Layout(viewer, 300, 360);
            if (!navigate.Result || media.PageScroller is not { } scroller || scroller.ScrollableWidth < 1)
                throw new IOException("Narrow paginated diagram controls did not expose native scrolling.");
            if (media.Image.ActualHeight < 20 || viewer.Background is not SolidColorBrush background
                || background.Color != ((SolidColorBrush)viewer.Resources["WindowBrush"]).Color)
                throw new IOException("The narrow diagram lost its visible image or reading theme.");
            var footer = (FrameworkElement)viewer.Template.FindName("PageControls", viewer);
            var pageBottom = footer.TranslatePoint(new Point(), viewer).Y;
            var bounds = scroller.TransformToAncestor(viewer).TransformBounds(new Rect(scroller.RenderSize));
            if (bounds.Top < -1 || bounds.Left < -1 || bounds.Right > viewer.ActualWidth + 1 || bounds.Bottom > pageBottom + 1)
                throw new IOException("The paginated diagram container extends outside its page: " + bounds);
            foreach (var button in ((WrapPanel)media.Actions!).Children.OfType<Button>())
            {
                button.BringIntoView(); Layout(viewer, 300, 360); Pump();
                var buttonBounds = button.TransformToAncestor(scroller).TransformBounds(new Rect(button.RenderSize));
                if (buttonBounds.Right < 1 || buttonBounds.Left >= scroller.ActualWidth || buttonBounds.Bottom < 1 || buttonBounds.Top >= scroller.ActualHeight)
                    throw new IOException("A diagram action cannot be brought into its native scroll view: " + button.Content);
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            if (!actions.Select(action => action.Format).SequenceEqual(new[] { "Zoom", "SVG", "PNG", "Copy source" })
                || actions.Take(3).Any(action => action.Content != diagram.Svg) || actions[^1].Content != diagram.Source)
                throw new IOException("Paginated diagram actions changed the original vector or Mermaid source.");
            scroller.ScrollToLeftEnd(); Layout(viewer, 300, 360); Pump();
            Save(viewer, Path.Combine(output, $"native-paginated-diagram-{(dark ? "dark" : "light")}.png"));
            viewer.ApplyReadingMode("Continuous"); Layout(viewer, 300, 360); Pump();
            if (media.PageScroller is not null || media.Host.Child is not StackPanel || media.Image.MaxHeight != 900
                || !double.IsPositiveInfinity(media.Image.MaxWidth) || !ReferenceEquals(original, media.Image.Source) || parsed.Source != source)
                throw new IOException("Continuous mode did not restore the diagram and its original source.");
        }
    }

    private static TextPointer Find(FlowDocument document, string value)
    {
        for (var pointer = document.ContentStart; pointer is not null && pointer.CompareTo(document.ContentEnd) < 0; pointer = pointer.GetNextContextPosition(LogicalDirection.Forward))
        {
            if (pointer.GetPointerContext(LogicalDirection.Forward) != TextPointerContext.Text) continue;
            var index = pointer.GetTextInRun(LogicalDirection.Forward).IndexOf(value, StringComparison.Ordinal);
            if (index >= 0) return pointer.GetPositionAtOffset(index)!;
        }
        throw new IOException("The paginated fixture lost text: " + value);
    }
    private static void Layout(FrameworkElement viewer, double width = 800, double height = 480)
    {
        viewer.Measure(new Size(width, height)); viewer.Arrange(new Rect(0, 0, width, height)); viewer.UpdateLayout();
    }
    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }
    private static void Complete(Task task)
    {
        var watch = Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(25)) throw new TimeoutException("Native pagination exceeded its validation deadline.");
            Pump();
        }
        task.GetAwaiter().GetResult();
    }
    private static void Save(FrameworkElement viewer, string path)
    {
        var bitmap = new RenderTargetBitmap((int)viewer.ActualWidth, (int)viewer.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(viewer);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
}
