using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Threading;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

internal static class NativeSelectionNavigationChecks
{
    // Reflection deliberately supports both the four-argument baseline API and
    // candidate API, so the identical compiled probe can load either product DLL.
    public static void RunZoomProbe(string output, bool expectObsoletePublication)
    {
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        LensDocsStudio.Native.Platform.ThemeService.Apply(app.Resources, "Light");
        var styles = new ResourceDictionary { Source = new Uri("pack://application:,,,/LensDocsStudio.Native;component/Rendering/DocumentPreview.xaml") };
        var navigate = typeof(PreviewNavigationService).GetMethods().Single(method => method.Name == "BringIntoViewAsync"
            && method.GetParameters()[0].ParameterType == typeof(FlowDocumentReader));
        var observations = new List<object>(); var publishedAny = false;
        foreach (var mode in new[] { "Continuous", "Pages" })
        {
            var run = new Run("planning planning"); var document = new FlowDocument(new Paragraph(run));
            var viewer = new DocumentPreview { Style = (Style)styles["DocumentPreviewStyle"], Document = document };
            viewer.ApplyReadingMode(mode);
            using var host = new HwndSource(new HwndSourceParameters("Hidden exact-source zoom probe") {
                Width = 850, Height = 550, WindowStyle = unchecked((int)0x80000000) });
            host.RootVisual = viewer; Layout();
            if (!Complete(Navigate(0))) throw new IOException("Zoom probe could not establish its original selection.");
            var pending = Navigate(9); viewer.Zoom = 110; Layout();
            var published = Complete(pending); publishedAny |= published;
            observations.Add(new { Mode = mode, ZoomBefore = 100, ZoomAfter = viewer.Zoom, ObsoletePublished = published,
                RenderedSelection = viewer.Selection?.Text, TextOffset = viewer.Selection is { } selection ? run.ContentStart.GetOffsetToPosition(selection.Start) : (int?)null });
            Task<bool> Navigate(int offset)
            {
                object?[] parameters = [viewer, run.ContentStart.GetPositionAtOffset(offset), run.ContentStart.GetPositionAtOffset(offset + 8), null, null];
                return (Task<bool>)navigate.Invoke(null, parameters.Take(navigate.GetParameters().Length).ToArray())!;
            }
            void Layout() { viewer.Measure(new Size(850, 550)); viewer.Arrange(new Rect(0, 0, 850, 550)); viewer.UpdateLayout(); }
        }
        var product = typeof(DocumentPreview).Assembly.Location;
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "zoom-probe.json"), JsonSerializer.Serialize(new {
            ProductSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(product))),
            ProbeSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(NativeSelectionNavigationChecks).Assembly.Location))),
            ExpectObsoletePublication = expectObsoletePublication, ObsoletePublished = publishedAny, Observations = observations
        }, new JsonSerializerOptions { WriteIndented = true }));
        if (publishedAny != expectObsoletePublication) throw new IOException("Zoom probe did not produce its expected source-specific result.");
    }

    // Hidden real WPF layout/dispatcher checks. Real mouse and OS appearance remain hosted.
    public static void Run(string output)
    {
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var application = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        LensDocsStudio.Native.Platform.ThemeService.Apply(application.Resources, "Light");
        var styles = new ResourceDictionary { Source = new Uri("pack://application:,,,/LensDocsStudio.Native;component/Rendering/DocumentPreview.xaml") };
        Directory.CreateDirectory(output); var observations = new List<object>();
        var source = SelectionTableFixture.Create();
        foreach (var mode in new[] { "Continuous", "Pages" })
        {
            var result = Render();
            var viewer = new DocumentPreview { Style = (Style)styles["DocumentPreviewStyle"] }; viewer.SetPreview(result, source.Length, mode);
            using var host = new HwndSource(new HwndSourceParameters("Hidden selection navigation checks") {
                Width = 850, Height = 550, WindowStyle = unchecked((int)0x80000000) });
            host.RootVisual = viewer; Layout();
            var first = source.IndexOf("planning", StringComparison.Ordinal);
            var second = source.IndexOf("planning", first + 8, StringComparison.Ordinal);
            result.TextMap.TryPreviewRange(first, 8, out var a, out var b);
            result.TextMap.TryPreviewRange(second, 8, out var c, out var d);
            void Diagnostic(string message) => File.AppendAllText(Path.Combine(output, "navigation-events.txt"), mode + " " + message + Environment.NewLine);
            var old = PreviewNavigationService.BringIntoViewAsync(viewer, a!, b!, Diagnostic);
            var newer = PreviewNavigationService.BringIntoViewAsync(viewer, c!, d!, Diagnostic);
            var oldResult = Complete(old); var newResult = Complete(newer);
            Diagnostic($"old {oldResult}, new {newResult}, selected '{viewer.Selection?.Text}', exact {Exact(second)}, selection available {viewer.Selection is not null}");
            Check(!oldResult && newResult && Exact(second), "Newest repeated occurrence wins");
            old = PreviewNavigationService.BringIntoViewAsync(viewer, a!, b!);
            PreviewNavigationService.CancelPending(viewer);
            Check(!Complete(old) && Exact(second), "Explicit cancellation preserves the newer selection");
            old = PreviewNavigationService.BringIntoViewAsync(viewer, a!, b!);
            viewer.ApplyReadingMode(mode == "Pages" ? "Continuous" : "Pages");
            viewer.ApplyReadingMode(mode); Layout();
            Check(!Complete(old), "View replacement and return invalidate queued selection");
            old = PreviewNavigationService.BringIntoViewAsync(viewer, a!, b!);
            viewer.Zoom = 110; Layout();
            Check(!Complete(old), "Zoom invalidates queued selection");
            var sourceCurrent = true;
            old = PreviewNavigationService.BringIntoViewAsync(viewer, a!, b!, isCurrent: () => sourceCurrent);
            sourceCurrent = false;
            Check(!Complete(old), "New source interaction invalidates queued selection before publication");
            var validityChecks = 0;
            old = PreviewNavigationService.BringIntoViewAsync(viewer, a!, b!, isCurrent: () => ++validityChecks < 2);
            Check(!Complete(old) && validityChecks >= 2, "Source validity is checked again after deferred layout or pagination");
            old = PreviewNavigationService.BringIntoViewAsync(viewer, a!, b!);
            var replacement = Render(); viewer.SetPreview(replacement, source.Length, mode); Layout();
            Check(!Complete(old) && viewer.Document == replacement.Document, "Render completion invalidates old pointers");
            Check(new TextRange(result.Document.ContentStart, result.Document.ContentEnd).Text.Contains("Q-15", StringComparison.Ordinal), "Navigation preserves all rendered rows");
            void Layout() { viewer.Measure(new Size(850, 550)); viewer.Arrange(new Rect(0, 0, 850, 550)); viewer.UpdateLayout(); }
            bool Exact(int offset) => viewer.Selection is { } selected && selected.Text == "planning"
                && result.TextMap.TrySourceRange(selected.Start, selected.End, out var start, out var length) && start == offset && length == 8;
            void Check(bool passed, string name)
            {
                observations.Add(new { Mode = mode, Name = name, Passed = passed });
                File.WriteAllText(Path.Combine(output, "navigation.json"), JsonSerializer.Serialize(observations, new JsonSerializerOptions { WriteIndented = true }));
                if (!passed) throw new IOException(mode + ": " + name);
            }
        }
        PreviewResult Render() => new NativeMarkdownRenderer(new NativeSvgService()).Render(new MarkdownService().Parse(source), false,
            new Dictionary<Markdig.Syntax.FencedCodeBlock, DiagramResult>(), _ => { }, (_, _) => { });
    }

    private static bool Complete(Task<bool> operation)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!operation.IsCompleted && watch.Elapsed < TimeSpan.FromSeconds(5))
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
        }
        if (!operation.IsCompleted) throw new TimeoutException("Selection navigation exceeded its bounded dispatcher observation.");
        return operation.GetAwaiter().GetResult();
    }
}
