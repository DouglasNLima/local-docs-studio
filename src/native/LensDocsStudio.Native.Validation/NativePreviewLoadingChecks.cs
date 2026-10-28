using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FlaUI.UIA3;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.Validation;

internal static class NativePreviewLoadingChecks
{
    public static void Run(string output, string root, bool shown = false, bool highContrast = false)
    {
        // This harness owns its window and isolated model; it never sends desktop input.
        // Actual OS High Contrast remains restricted by NativeTestHighContrast.
        Directory.CreateDirectory(output);
        var mediaRoot = Path.Combine(output, "media"); Directory.CreateDirectory(mediaRoot);
        // Artefact bundle tiny.png is a textual placeholder; exercise real native image decoding.
        var pixels = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null,
            new byte[] { 0x50, 0x88, 0xff, 0xff, 0x66, 0x66, 0x66, 0xff, 0x88, 0xaa, 0xcc, 0xff, 0x50, 0x88, 0xff, 0xff }, 8);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(pixels));
        using (var image = File.Create(Path.Combine(mediaRoot, "example.png"))) encoder.Save(image);
        using var phases = new MermaidPhaseRecorder();
        using var contrast = highContrast ? new NativeTestHighContrast() : null;
        if (Application.Current is null)
        {
            // Load the real resources without App.OnStartup, which owns the ordinary user's session.
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (var resource in new[] { "CommandIcons", "DesktopStyles", "FormStyles" })
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"pack://application:,,,/LensDocsStudio.Native;component/Resources/{resource}.xaml") });
        }
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var checks = new List<string>(); var timings = new List<object>(); var validated = false;
        var dialogs = new DesktopDialogs();
        using var model = new WorkspaceViewModel(new DocumentStore(Path.Combine(output, "writes")), new SessionStore(Path.Combine(output, "state")), new WorkspaceService(), dialogs);
        var window = new MainWindow(model, dialogs, []);
        var loaded = typeof(MainWindow).GetMethod("WindowLoaded", BindingFlags.Instance | BindingFlags.NonPublic)!;
        window.Loaded -= (RoutedEventHandler)loaded.CreateDelegate(typeof(RoutedEventHandler), window);
        var gate = (SemaphoreSlim)Field("previewGate")!;
        var queuedPreview = (DispatcherTimer)Field("previewTimer")!;
        var loading = (PreviewLoadingState)Field("previewLoading")!;
        var preview = (DocumentPreview)window.FindName("Preview");
        var host = (FrameworkElement)window.FindName("PreviewContentHost");
        var view = (PreviewLoadingView)window.FindName("PreviewLoading");
        var ring = (ProgressBar)view.FindName("Ring"); var status = (TextBlock)view.FindName("Status");
        var gateHeld = false;
        try
        {
            window.Width = 1280; window.Height = 800; Call("ApplyTheme");
            if (shown) window.Show(); else { window.Measure(new Size(1280, 800)); window.Arrange(new Rect(0, 0, 1280, 800)); }
            if (highContrast) Check(SystemParameters.HighContrast, "Actual OS High Contrast is active");
            foreach (var theme in highContrast ? new[] { "System" } : new[] { "Light", "Dark" })
            foreach (var mode in new[] { "Continuous", "Pages" })
            {
                model.Theme = theme; Call("ApplyTheme");
                Await(model.UpdatePreferencesAsync(p => p with { Preview = (p.Preview ?? new()) with { ReadingMode = mode } }));
                Call("ApplyReadingPreferences"); Call("SetLayout", "Split");
                model.NewDocumentCommand.Execute(null); var a = model.ActiveDocument!;
                a.AssetWorkspace = mediaRoot;
                a.EditorDocument.Text = "# Incoming document\n\nOrdinary **Markdown** preview.\n\n![Local image](example.png)\n\n| Name | Value |\n| --- | --- |\n| Example | Complete |\n\n" + string.Join("\n\n", Enumerable.Repeat("A paragraph for scrolling and selection.", 35));
                Hold(); var first = Render();
                Until(() => view.Visibility == Visibility.Visible);
                Check(loading.Busy && !loading.Refresh && host.Visibility == Visibility.Hidden, "Incoming document masks stale content while pending");
                Check(ring.Visibility == Visibility.Visible && ring.IsIndeterminate && status.Text.Length > 0, "Visible ring and meaningful status reflect real pending work");
                Check(AutomationProperties.GetAutomationId(ring) == "PreviewLoadingIndicator" && AutomationProperties.GetItemStatus(ring) == "Busy", "Stable accessible busy indicator");
                if (shown)
                {
                    var handle = new WindowInteropHelper(window).Handle;
                    Await(Task.Run(() => {
                        using var automation = new UIA3Automation(); var app = automation.FromHandle(handle);
                        var found = app.FindFirstDescendant(c => c.ByAutomationId("PreviewLoadingIndicator"));
                        if (found is null || found.IsOffscreen || found.BoundingRectangle.Width <= 0 || found.Properties.ItemStatus.Value != "Busy") throw new IOException("UIA did not expose the visible busy ring.");
                    }));
                    Check(ring.IsVisible, "Shown WPF progress ring is actually visible");
                    ring.ApplyTemplate();
                    var rotation = (RotateTransform)ring.Template.FindName("RingRotation", ring);
                    Check(rotation.HasAnimatedProperties == SystemParameters.ClientAreaAnimation, "Ring honours the current Windows animation preference");
                    ring.Tag = false;
                    Until(() => !rotation.HasAnimatedProperties);
                    Check(!rotation.HasAnimatedProperties && ring.IsVisible, "Reduced motion removes animation while keeping visible busy feedback");
                    ring.SetResourceReference(FrameworkElement.TagProperty, SystemParameters.ClientAreaAnimationKey);
                    if (theme == "Light" && mode == "Continuous")
                    {
                        MeasureBusyPresentation("Windows animation preference");
                        ring.Tag = false; Until(() => !rotation.HasAnimatedProperties);
                        MeasureBusyPresentation("Static reduced-motion ring");
                        ring.SetResourceReference(FrameworkElement.TagProperty, SystemParameters.ClientAreaAnimationKey);
                    }
                    Capture(theme + "-" + mode + "-initial");
                }
                var capturedBeforeRelease = RequestObservation(first);
                timings.Add(new { theme, mode, Scenario = "Captured render before gate release", Observation = capturedBeforeRelease });
                Release(); var watch = Stopwatch.StartNew(); Await(first, watch);
                var capturedTaskMilliseconds = watch.Elapsed.TotalMilliseconds;
                var capturedAfterAwait = RequestObservation(first);
                timings.Add(new { theme, mode, Scenario = "Captured render task finished", CapturedTaskCompletionMilliseconds = capturedTaskMilliseconds,
                    Observation = capturedAfterAwait });
                // Appearance broadcasts can supersede a held request. Its task completes
                // without accepting content; the current request still owns loading.
                Until(() => !loading.Busy && !queuedPreview.IsEnabled, watch);
                timings.Add(new { theme, mode, OrdinaryPreviewMilliseconds = watch.Elapsed.TotalMilliseconds,
                    CapturedTaskCompletionMilliseconds = capturedTaskMilliseconds,
                    BeforeRelease = capturedBeforeRelease, AfterCapturedTask = capturedAfterAwait, CurrentTerminal = RequestObservation(first) });
                Check(!loading.Busy && view.Visibility == Visibility.Collapsed && host.Visibility == Visibility.Visible, "Successful first layout dismisses loading");
                Check(CurrentAcceptedSource(), "Current document and exact source revision completed after any superseded request");
                Check(new TextRange(preview.Document.ContentStart, preview.Document.ContentEnd).Text.Contains("Incoming document"), "Accepted content belongs to the active document");
                var firstResult = (PreviewResult)Field("rendered")!;
                Check(firstResult.Media.Count == 1 && firstResult.Warnings.Count == 0, "Local image media is prepared with the ordinary table and long text: " + string.Join("; ", firstResult.Warnings));
                Capture(theme + "-" + mode + "-completed");
                // Deterministically reproduce a queued appearance refresh superseding
                // an awaited task, without depending on Windows broadcast timing.
                Hold(); var superseded = Render(); var heldGeneration = loading.Generation;
                Check(!superseded.IsCompleted, "Deterministic supersession starts with a pending held render task");
                Call("QueuePreview"); Await(superseded);
                Check(loading.Busy && loading.Generation > heldGeneration && loading.Context?.Document == a.Id,
                    "A completed superseded task cannot certify the current queued preview");
                Release(); var replacementWatch = Stopwatch.StartNew();
                Until(() => !loading.Busy && !queuedPreview.IsEnabled, replacementWatch);
                Check(CurrentAcceptedSource() && view.Visibility == Visibility.Collapsed && host.Visibility == Visibility.Visible,
                    "Queued replacement accepts the current source and dismisses loading within the original deadline");
                timings.Add(new { theme, mode, Scenario = "Deterministic queued refresh supersession",
                    ReplacementMilliseconds = replacementWatch.Elapsed.TotalMilliseconds, CurrentTerminal = RequestObservation(superseded) });
                firstResult = (PreviewResult)Field("rendered")!;
                Check(firstResult.Media.Count == 1 && firstResult.Warnings.Count == 0,
                    "Queued replacement retains prepared local media without warnings");
                preview.Zoom = 120; var accepted = preview.Document;
                Check(firstResult.TextMap.TryPreviewRange(a.EditorDocument.Text.IndexOf("Ordinary", StringComparison.Ordinal), 8, out var selectionStart, out var selectionEnd), "Selected source has a verified preview range");
                preview.Selection.Select(selectionStart!, selectionEnd!); var focus = Keyboard.FocusedElement;
                Hold(); a.EditorDocument.Insert(a.EditorDocument.TextLength, "\n\nUpdated revision."); var refresh = Render();
                Until(() => view.Visibility == Visibility.Visible);
                Check(loading.Refresh && ReferenceEquals(preview.Document, accepted) && host.Visibility == Visibility.Visible && status.Text == "Updating preview…", "Same-document refresh retains accepted content with compact status");
                Capture(theme + "-" + mode + "-refresh");
                a.EditorDocument.Insert(a.EditorDocument.TextLength, "\n\nLatest rapid edit."); var revision = Render(); Await(refresh);
                Check(loading.Refresh && ReferenceEquals(preview.Document, accepted), "Rapid source edits retain the accepted document and transfer loading");
                Release(); Await(revision);
                Check(preview.Zoom == 120 && preview.Selection.Text == "Ordinary" && ReferenceEquals(focus, Keyboard.FocusedElement), "Successful refresh preserves zoom selection and keyboard focus");
                Hold(); a.EditorDocument.Insert(a.EditorDocument.TextLength, "\n\nSuperseded revision."); refresh = Render();
                // Supersede a held request; its cancellation must not dismiss the incoming document's ring.
                model.NewDocumentCommand.Execute(null); var b = model.ActiveDocument!; b.EditorDocument.Text = "# Current document B\n\nCorrect incoming content.";
                var next = Render(); Await(refresh); Until(() => view.Visibility == Visibility.Visible);
                Check(loading.Busy && loading.Context?.Document == b.Id && host.Visibility == Visibility.Hidden, "Stale cancellation cannot hide the new document spinner or expose outgoing content");
                model.NewDocumentCommand.Execute(null); var c = model.ActiveDocument!; c.EditorDocument.Text = "# Document C";
                var third = Render(); Await(next);
                model.ActiveDocument = a; Call("BindActiveDocument"); var back = Render(); Await(third);
                model.ActiveDocument = b; Call("BindActiveDocument"); next = Render(); Await(back);
                Check(loading.Busy && loading.Context?.Document == b.Id && host.Visibility == Visibility.Hidden, "Rapid switching across three tabs retains only the latest request");
                Release(); Await(next);
                Check(preview.Zoom == 120 && !loading.Busy && new TextRange(preview.Document.ContentStart, preview.Document.ContentEnd).Text.Contains("Current document B"), "Latest document installs with preserved zoom");

                Hold(); b.EditorDocument.Text = "# Markdown survives\n\n```mermaid\nflowchart TD\nA -->\n```\n\nUnrelated paragraph remains visible.\n\n```mermaid\nerDiagram\nA ||--o{ B : owns\n```";
                var error = Render(); Until(() => view.Visibility == Visibility.Visible); Release(); Await(error);
                Check(!loading.Busy && ((PreviewResult)Field("rendered")!).Warnings.Count == 1, "Actual Mermaid syntax error ends loading while successful Markdown and the other diagram survive");
                Check(new TextRange(preview.Document.ContentStart, preview.Document.ContentEnd).Text.Contains("Unrelated paragraph"), "Diagram failure preserves unrelated Markdown");
                Capture(theme + "-" + mode + "-error");

                Hold(); b.EditorDocument.Insert(0, "<!-- pending -->\n"); var paused = Render(); Call("SetLayout", "Editor"); Await(paused);
                Check(!loading.Busy && view.Visibility == Visibility.Collapsed, "Editor-only layout cancels visible loading");
                Release(); Call("SetLayout", "Split"); Await(Render());
                Hold(); var closing = Render(); model.ActiveDocument = null; Call("BindActiveDocument"); Await(closing);
                Check(!loading.Busy && host.Visibility == Visibility.Visible && preview.Document.Blocks.Count == 0, "Closing the active document removes loading and outgoing content"); Release();
            }
            // A ready first page must not wait for unrelated background pagination.
            model.NewDocumentCommand.Execute(null); var paged = model.ActiveDocument!;
            paged.EditorDocument.Text = "# Paged document\n\n" + string.Join("\n\n", Enumerable.Repeat("A paragraph for first-page readiness. " + new string('x', 120), 4000));
            Call("SetLayout", "Split"); Await(Render());
            paged.EditorDocument.Insert(paged.EditorDocument.TextLength, "\n\nLatest paged revision.");
            Await(ObservePagedReadiness(Render()));

            // The full production path handles dense Mermaid plus Markdown, without a timing stub.
            model.NewDocumentCommand.Execute(null); model.ActiveDocument!.EditorDocument.Text = "# Complex ER\n\n```mermaid\n" + File.ReadAllText(Path.Combine(root, "tests/fixtures/native-complex-er/complete-detailed.mmd")) + "```\n\nComplete document.\n\n```mermaid\n" + File.ReadAllText(Path.Combine(root, "tests/fixtures/native-complex-er/monitoring.mmd")) + "```\n";
            Call("SetLayout", "Split"); var complex = Render();
            Until(() => view.Visibility == Visibility.Visible || complex.IsCompleted);
            Check(view.Visibility == Visibility.Visible, "A real dense Mermaid render keeps the busy indicator visible");
            Capture("complex-loading"); Await(complex);
            Check(!loading.Busy && view.Visibility == Visibility.Collapsed, "Real dense rendering reaches a terminal state with no stale spinner");
            if (((PreviewResult)Field("rendered")!).Warnings.Count != 0) Capture("complex-failed");
            Check(((PreviewResult)Field("rendered")!).Warnings.Count == 0, "Complex 39/115/284 document completes through native preview: " + string.Join("; ", ((PreviewResult)Field("rendered")!).Warnings));
            Capture("complex-completed");
            Hold(); model.ActiveDocument!.EditorDocument.Insert(0, "<!-- closing during work -->\n"); var closePending = Render();
            Call("CompleteClose"); Await(closePending);
            Check(!loading.Busy && view.Visibility == Visibility.Collapsed, "Application closure removes pending loading and stops its visibility timer");
            Release();
            validated = true;
        }
        finally
        {
            if (gateHeld) gate.Release(); Call("CompleteClose");
            phases.Write(Path.Combine(output, "phases.json"));
            File.WriteAllText(Path.Combine(output, "preview-loading.json"), JsonSerializer.Serialize(new { Status = validated ? "PASS" : "FAIL", Scope = shown ? "Actual shown WPF and UIA" : "Compiled WPF lifecycle with controlled pipeline gates", highContrast, checks, timings }, new JsonSerializerOptions { WriteIndented = true }));
        }
        void Hold() { gate.Wait(); gateHeld = true; }
        void Release() { gate.Release(); gateHeld = false; }
        Task Render() => (Task)Call("UpdatePreviewAsync", false)!;
        object RequestObservation(Task captured) => new { captured.IsCompleted, TaskStatus = captured.Status.ToString(),
            loading.Generation, Phase = loading.Phase.ToString(), loading.Busy, PreviewQueued = queuedPreview.IsEnabled,
            Request = Field("diagnosticRequest"), CurrentAcceptedSource = CurrentAcceptedSource() };
        bool CurrentAcceptedSource() => loading.Phase == PreviewLoadingPhase.Completed
            && model.ActiveDocument is { } active && loading.Context?.Document == active.Id
            && Field("renderedDocumentId") is Guid renderedId && renderedId == active.Id
            && Field("parsed") is ParsedDocument acceptedParsed && acceptedParsed.Source == active.EditorDocument.Text
            && Field("parsedVersion") is ICSharpCode.AvalonEdit.Document.ITextSourceVersion acceptedVersion
            && active.EditorDocument.Version.CompareAge(acceptedVersion) == 0;
        object? Field(string name) => typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window);
        object? Call(string name, params object?[] args) => typeof(MainWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, args);
        void Check(bool value, string message) { if (!value) throw new IOException(message); checks.Add(message); }
        async Task ObservePagedReadiness(Task render)
        {
            await render; // UI continuation observes readiness before background pagination resumes.
            var paginator = (DynamicDocumentPaginator)((IDocumentPaginatorSource)preview.Document).DocumentPaginator;
            var complete = paginator.IsPageCountValid; var pages = paginator.PageCount;
            Check(!loading.Busy && host.Visibility == Visibility.Visible && paginator.GetPage(0) != DocumentPage.Missing,
                "Pages refresh installs its first visible page and dismisses loading");
            timings.Add(new { Scenario = "Pages refresh readiness", PagesAtReady = pages, PaginationCompleteAtReady = complete,
                BackgroundPaginationEnabled = paginator.IsBackgroundPaginationEnabled });
            Check(paginator.IsBackgroundPaginationEnabled && !complete, "Pages refresh does not wait for all background pages");
            Capture("pages-refresh-ready");
        }
        void MeasureBusyPresentation(string scenario)
        {
            using var process = Process.GetCurrentProcess(); var cpu = process.TotalProcessorTime;
            var allocated = GC.GetTotalAllocatedBytes(); var clock = Stopwatch.StartNew(); var last = 0d; var gaps = new List<double>();
            var pulse = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
            pulse.Tick += (_, _) => { var now = clock.Elapsed.TotalMilliseconds; gaps.Add(now - last); last = now; };
            pulse.Start();
            try { Until(() => clock.Elapsed >= TimeSpan.FromMilliseconds(400)); }
            finally { pulse.Stop(); }
            timings.Add(new { scenario, Milliseconds = clock.Elapsed.TotalMilliseconds, CpuMilliseconds = (process.TotalProcessorTime - cpu).TotalMilliseconds,
                AllocatedBytes = GC.GetTotalAllocatedBytes() - allocated, Pulses = gaps.Count, MeanDispatcherGap = gaps.DefaultIfEmpty().Average(), MaximumDispatcherGap = gaps.DefaultIfEmpty().Max() });
        }
        void Capture(string name)
        {
            if (!shown) return;
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(Path.Combine(output, name + ".png")); encoder.Save(stream);
        }
    }
    private static void Await(Task task, Stopwatch? sharedDeadline = null) { Until(() => task.IsCompleted, sharedDeadline); task.GetAwaiter().GetResult(); }
    private static void Until(Func<bool> ready, Stopwatch? sharedDeadline = null)
    {
        if (ready()) return;
        var watch = sharedDeadline ?? Stopwatch.StartNew();
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(10) };
        timer.Tick += (_, _) => { if (ready() || watch.Elapsed > TimeSpan.FromSeconds(25)) frame.Continue = false; };
        timer.Start();
        try { Dispatcher.PushFrame(frame); }
        finally { timer.Stop(); }
        if (!ready()) throw new TimeoutException("Preview lifecycle check exceeded its bounded wait.");
    }
}
