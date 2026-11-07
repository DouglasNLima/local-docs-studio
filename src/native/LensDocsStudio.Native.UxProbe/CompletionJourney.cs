using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Document;
using LensDocsStudio.Native.Rendering;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.UxProbe;

internal sealed partial class Probe
{
    private sealed record PixelRegion(string Name, string Expected, double X, double Y, double Width, double Height);
    private readonly List<object> completionCaptures = [];
    private int completionFrame;

    private object CompletionState()
    {
        object Reader(DocumentPreview reader, bool secondary)
        {
            var started = Stopwatch.GetTimestamp();
            var result = Field<PreviewResult>(secondary ? "comparisonPreviewResult" : "rendered");
            var page = reader.Template?.FindName("PageNumber", reader) as Label;
            var native = Tree(reader).OfType<FlowDocumentPageViewer>().FirstOrDefault();
            var peer = UIElementAutomationPeer.CreatePeerForElement(reader);
            var owner = CompletionTrace.ExpectedOwner(window, model, secondary);
            var version = Field<ITextSourceVersion>(secondary ? "comparisonRenderedVersion" : "renderedVersion");
            var currentOwner = owner is not null && version is not null && version.BelongsToSameDocumentAs(owner.EditorDocument.Version)
                && version.CompareAge(owner.EditorDocument.Version) == 0 && Field<Guid?>(secondary ? "comparisonRenderedId" : "renderedDocumentId") == owner.Id
                && (secondary || owner == model.ActiveDocument);
            return new { started, itemStatus = AutomationProperties.GetItemStatus(reader), reader.IsVisible, reader.IsMeasureValid, reader.IsArrangeValid,
                attachedMatches = reader.Document == result?.Document, currentOwner, owner = Field<Guid?>(secondary ? "comparisonRenderedId" : "renderedDocumentId"), expectedOwner = owner?.Id,
                mode = reader.ViewingMode.ToString(), reader.PageNumber, reader.PageCount, nativePage = native?.MasterPageNumber, nativeCount = native?.PageCount,
                pageText = page?.Content?.ToString(), pageUia = page is null ? null : UIElementAutomationPeer.CreatePeerForElement(page)?.GetName(),
                source = reader.IsVisible ? result?.TextMap.VisibleSource(reader.ActualHeight) : null,
                textPattern = reader.IsVisible && peer?.GetPattern(PatternInterface.Text) is ITextProvider provider
                    ? provider.GetVisibleRanges().Select(r => new { meaningful = !string.IsNullOrWhiteSpace(r.GetText(128)), rectangles = r.GetBoundingRectangles() }).ToArray() : null,
                finished = Stopwatch.GetTimestamp() };
        }
        var label = (TextBlock)window.FindName("PreviewStatusLabel");
        return new { tick = Stopwatch.GetTimestamp(), active = model.ActiveDocument?.Id, model.PreviewStatus, statusText = label.Text,
            statusUia = UIElementAutomationPeer.CreatePeerForElement(label)?.GetName(),
            primary = Reader(preview, false), secondary = Reader(Field<DocumentPreview>("comparisonPreview")!, true),
            primaryPending = Field<object>("previewLayoutCancellation") is not null,
            secondaryPending = Field<object>("comparisonLayoutCancellation") is not null, finished = Stopwatch.GetTimestamp() };
    }

    private PixelRegion[] CompletionRegions()
    {
        var bounds = Desktop.ClientBounds(windowHandle); var regions = new List<PixelRegion>();
        void Add(string name, FrameworkElement? element, string expected)
        {
            if (element is null || !element.IsVisible) return;
            var area = Bounds(element); area.Inflate(3, 3); area.Intersect(bounds);
            if (area.IsEmpty) throw new IOException("Completion label is outside the visible viewport.");
            regions.Add(new(name, expected, area.X - bounds.X, area.Y - bounds.Y, area.Width, area.Height));
        }
        var status = (TextBlock)window.FindName("PreviewStatusLabel"); Add("status", status, status.Text);
        foreach (var (reader, name) in new[] { (preview, "primary-page"), (Field<DocumentPreview>("comparisonPreview")!, "secondary-page") })
        {
            var page = reader.Template?.FindName("PageNumber", reader) as Label;
            Add(name, page, page?.Content?.ToString() ?? "");
        }
        return regions.ToArray();
    }

    private static TimeSpan CompletionRemaining(long started)
    {
        var remaining = TimeSpan.FromSeconds(ActionSeconds) - Stopwatch.GetElapsedTime(started);
        if (remaining <= TimeSpan.Zero) throw new TimeoutException("Completion observation exceeded the unchanged 30-second deadline.");
        return remaining;
    }

    private async Task CompletionFrames(string action, CompletionTrace? trace, string[]? completedReaders = null, long? origin = null)
    {
        completedReaders ??= ["primary", "secondary"];
        var started = origin ?? Stopwatch.GetTimestamp();
        // Exactly three captures, declared before collection. The first is never
        // replaced. Later frames explain a boundary; they cannot erase a mismatch.
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background).Task.WaitAsync(CompletionRemaining(started));
        for (var frameIndex = 0; frameIndex < 3; frameIndex++)
        {
            long? committed = null;
            if (frameIndex == 1 && trace is not null)
            {
                var pendingCommit = await Ui(trace.CommitAfterCurrentChanges);
                committed = await pendingCommit.WaitAsync(CompletionRemaining(started));
            }
            var before = await Ui(CompletionState);
            var regions = await Ui(CompletionRegions);
            var frame = Desktop.Capture(Desktop.ClientBounds(windowHandle));
            var after = await Ui(CompletionState);
            var stem = $"completion-{completionFrame++:D3}-{action}-frame-{frameIndex}";
            Desktop.Save(frame, Path.Combine(output, stem + ".png"));
            foreach (var region in regions)
            {
                var x = Math.Max(0, (int)Math.Floor(region.X)); var y = Math.Max(0, (int)Math.Floor(region.Y));
                var width = Math.Min(frame.Width - x, (int)Math.Ceiling(region.Width)); var height = Math.Min(frame.Height - y, (int)Math.Ceiling(region.Height));
                var crop = new CroppedBitmap(frame.Bitmap, new Int32Rect(x, y, width, height)); crop.Freeze();
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(crop));
                using var file = File.Create(Path.Combine(output, stem + "-" + region.Name + ".png")); encoder.Save(file);
            }
            completionCaptures.Add(new { action, frameIndex, file = stem + ".png", frame.Sha256, frame.Started, frame.DwmReturned,
                frame.PixelsStarted, frame.PixelsFinished, frame.CompositionBefore, frame.CompositionAfter, committed, before, after, regions, completedReaders,
                observationStarted = started, observationDeadlineSeconds = ActionSeconds,
                scope = "Displayed desktop pixels; DWM surface flush is not physical monitor scan-out" });
            Program.Write(output, "completion-captures.json", completionCaptures);
            _ = CompletionRemaining(started);
        }
    }

    private async Task CompletionFind(DocumentViewModel owner, string label, CompletionTrace? trace)
    {
        currentAction = label;
        await PointAt(preview); Desktop.Click(); await Until(() => preview.IsKeyboardFocusWithin);
        Desktop.Key(0x46, 0x11); await Until(() => ((FrameworkElement)window.FindName("FindText")).IsVisible);
        Desktop.Key(0x41, 0x11);
        var started = Stopwatch.GetTimestamp(); Desktop.Text("ReadableEnd"); Desktop.Key(0x0D);
        await Until(() => FindQueryReady("ReadableEnd", 1) && ComparisonReady(owner, false, "ReadableEnd")).WaitAsync(CompletionRemaining(started));
        // Preserve the original active-reader boundary, even while the other
        // reader truthfully reports Busy. Its completion is qualified separately.
        await CompletionFrames(label, trace, ["primary"], started);
        var other = await Ui(() => model.Documents.Single(document => document != owner));
        await Until(() => ComparisonReady(owner, false, "ReadableEnd") && ComparisonReady(other, true)
            && Field<object>("previewLayoutCancellation") is null && Field<object>("comparisonLayoutCancellation") is null).WaitAsync(CompletionRemaining(started));
        await CompletionFrames(label + "-all-readers", trace, origin: started);
        Desktop.Key(0x1B); await Until(() => !((FrameworkElement)window.FindName("FindBar")).IsVisible
            && ComparisonReady(owner, false, "ReadableEnd"));
    }

    internal async Task RunCompletion(long startup)
    {
        await Until(() => window.IsLoaded && !model.IsBusy && model.ActiveDocument?.EditorDocument.Text == source && editor.IsVisible);
        await Ui(() => { window.Activate(); Desktop.SetForegroundWindow(new WindowInteropHelper(window).Handle); });
        Desktop.OwnsForeground(); windowHandle = await Ui(() => new WindowInteropHelper(window).Handle);
        var first = await Ui(() => model.ActiveDocument!); var second = await Ui(() => model.Documents.Single(d => d != first));
        var other = await Ui(() => second.EditorDocument.Text);
        var dirty = await Ui(() => new[] { first.IsDirty, second.IsDirty });
        var mode = Environment.GetEnvironmentVariable("LENS_NATIVE_COMPARISON_MODE") ?? "Continuous";
        var scenario = Environment.GetEnvironmentVariable("LENS_NATIVE_COMPLETION_SCENARIO") ?? "pixels";
        var target = Environment.GetEnvironmentVariable("LENS_NATIVE_COMPLETION_READER") ?? "primary";
        var instrumented = Environment.GetEnvironmentVariable("LENS_NATIVE_COMPLETION_TRACE") == "1";
        CompletionTrace? trace = null;
        try
        {
            await ClickControl("LayoutPreview"); await Until(() => ComparisonReady(first, false));
            Desktop.Key(0x46, 0x12);
            await Until(() => ((Menu)window.FindName("ApplicationMenu")).Items.OfType<MenuItem>().First().IsSubmenuOpen);
            Desktop.Key(0x42);
            await Until(() => System.Windows.Input.Keyboard.FocusedElement is MenuItem item && AutomationProperties.GetAutomationId(item) == "OpenBeside");
            Desktop.Key(0x0D); await Until(() => Field<object>("comparisonSlots") is not null && ComparisonReady(first, false) && ComparisonReady(second, true));
            if (instrumented && scenario == "pixels") trace = await Ui(() => new CompletionTrace(window, model));
            if (scenario == "pixels") await CompletionFind(first, "primary-find", trace); else await ComparisonFindEnd(first);
            await ClickControl("ActivateComparison1"); await Until(() => model.ActiveDocument == second && ComparisonReady(second, false));
            if (scenario == "pixels") await CompletionFind(second, "secondary-find", trace); else await ComparisonFindEnd(second);
            await ClickControl("ActivateComparison0");
            await Until(() => model.ActiveDocument == first && ComparisonReady(first, false, "ReadableEnd") && ComparisonReady(second, true, "ReadableEnd")
                && Field<object>("previewLayoutCancellation") is null && Field<object>("comparisonLayoutCancellation") is null);
            var close = await Ui(() => { var b = Bounds(Control("CloseComparison")); return new Point(b.X + b.Width / 2, b.Y + b.Height / 2); });
            void Interrupt()
            {
                if (scenario == "close") { Desktop.Move(close); Desktop.Click(); }
                else Desktop.Key(scenario == "switch" ? (ushort)0x09 : (ushort)0x1B, scenario == "switch" ? [(ushort)0x11] : []);
            }
            if (scenario != "pixels")
            {
                currentAction = "pending-" + scenario;
                await PointAt(preview); Desktop.Click(); await Until(() => preview.IsKeyboardFocusWithin);
                Desktop.Key(0x46, 0x11);
                await Until(() => ((FrameworkElement)window.FindName("FindBar")).IsVisible && ComparisonReady(first, false, "ReadableEnd")
                    && ComparisonReady(second, true, "ReadableEnd") && Field<object>("previewLayoutCancellation") is null && Field<object>("comparisonLayoutCancellation") is null);
                close = await Ui(() => { var b = Bounds(Control("CloseComparison")); return new Point(b.X + b.Width / 2, b.Y + b.Height / 2); });
                var splitter = await Ui(() => Tree(window).OfType<GridSplitter>().Single(e => e.IsVisible && AutomationProperties.GetName(e) == "Resize comparison panes"));
                await PointAt(splitter); Desktop.Click();
                await Until(() => splitter.IsKeyboardFocusWithin && ComparisonReady(first, false, "ReadableEnd") && ComparisonReady(second, true, "ReadableEnd")
                    && Field<object>("previewLayoutCancellation") is null && Field<object>("comparisonLayoutCancellation") is null);
                var direction = await Ui(() => splitter.ResizeDirection == GridResizeDirection.Rows ? (ushort)0x28 : (ushort)0x27);
                if (instrumented) trace = await Ui(() => new CompletionTrace(window, model));
                if (trace is not null)
                {
                    var marker = await Ui(() => trace.Arm(target, scenario));
                    var sender = Task.Run(async () =>
                    {
                        var operation = await marker.WaitAsync(TimeSpan.FromSeconds(ActionSeconds));
                        var sent = Stopwatch.GetTimestamp(); trace.Offered(operation, sent); Interrupt();
                    });
                    Desktop.Key(direction); await sender;
                }
                else { Desktop.Key(direction); Interrupt(); }
                await Until(() => scenario == "close" ? Field<object>("comparisonSlots") is null && model.ActiveDocument == first && ComparisonReady(first, false, "ReadableEnd")
                    : scenario == "switch" ? model.ActiveDocument == second && ComparisonReady(second, false, "ReadableEnd") && ComparisonReady(first, true, "ReadableEnd")
                    : !((FrameworkElement)window.FindName("FindBar")).IsVisible && ComparisonReady(first, false, "ReadableEnd") && ComparisonReady(second, true, "ReadableEnd"));
                await CompletionFrames("interrupted-" + scenario, trace);
            }
            else
            {
                await CompletionFrames("returned-primary", trace);
                for (var i = 0; i < 4; i++) Desktop.Key(0x09, 0x11);
                await Until(() => model.ActiveDocument == first && ComparisonReady(first, false, "ReadableEnd") && ComparisonReady(second, true, "ReadableEnd"));
                await CompletionFrames("rapid-switch", trace);
                Desktop.Move(close); Desktop.Click();
                await Until(() => Field<object>("comparisonSlots") is null && ComparisonReady(first, false, "ReadableEnd"));
                await CompletionFrames("closed-comparison", trace);
            }
            if (!await Ui(() => first.EditorDocument.Text == source && second.EditorDocument.Text == other && model.Documents.Contains(first) && model.Documents.Contains(second)
                    && new[] { first.IsDirty, second.IsDirty }.SequenceEqual(dirty))
                || Program.Hash(File.ReadAllBytes(path)) != fixture.Sha256.ToUpperInvariant() || File.ReadAllText(second.FilePath!) != other)
                throw new IOException("Completion experiment changed a source buffer, dirty state, document or fixture file.");
            var verdict = trace is null ? new { pendingInterruption = "NOT QUALIFIED; uninstrumented functional check" } as object : await Ui(trace.Verdict);
            Program.Write(output, "presentation-completion.json", new { functional = "PASS", scenario, target, mode, instrumented, verdict,
                sourceIntegrity = "PASS", fixture.Sha256, elapsedMs = Stopwatch.GetElapsedTime(startup).TotalMilliseconds,
                pixelAcceptance = "PENDING independent pixel correlation; no frame is discarded" });
        }
        finally
        {
            if (trace is not null) await Ui(() => { try { trace.Save(output); } finally { trace.Dispose(); } });
        }
    }
}
