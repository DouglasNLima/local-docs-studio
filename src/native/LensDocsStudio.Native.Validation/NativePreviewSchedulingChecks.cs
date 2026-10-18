using System.Reflection;
using System.Diagnostics.Tracing;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.ComponentModel;
using ICSharpCode.AvalonEdit;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.Validation;

internal static class NativePreviewSchedulingChecks
{
    public static void Run(string output, bool shown = false, bool highContrast = false)
    {
        Directory.CreateDirectory(output);
        using var contrast = highContrast ? new NativeTestHighContrast() : null;
        var application = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        ThemeService.Apply(application.Resources, "Light");
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var root = Path.Combine(output, "preview-scheduling-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var dialogs = new DesktopDialogs();
        var model = new WorkspaceViewModel(new DocumentStore(Path.Combine(root, "writes")), new SessionStore(root), new WorkspaceService(), dialogs);
        MainWindow? window = null;
        var heartbeatWatch = Stopwatch.StartNew(); var lastPulse = 0d; var gaps = new List<double>();
        var heartbeat = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
        heartbeat.Tick += (_, _) => { var now = heartbeatWatch.Elapsed.TotalMilliseconds; if (gaps.Count < 4096) gaps.Add(now - lastPulse); lastPulse = now; };
        heartbeat.Start();
        var fixtureEvidence = new List<object>();
        var cancellationEvidence = new List<object>();
        try
        {
            model.NewDocumentCommand.Execute(null);
            window = new MainWindow(model, dialogs, []);
            if (shown)
            {
                var loaded = typeof(MainWindow).GetMethod("WindowLoaded", BindingFlags.Instance | BindingFlags.NonPublic)!;
                window.Loaded -= (RoutedEventHandler)loaded.CreateDelegate(typeof(RoutedEventHandler), window);
                window.Width = 1280; window.Height = 800;
                Invoke("ApplyTheme"); window.Show();
                if (highContrast && !SystemParameters.HighContrast) throw new IOException("Actual OS High Contrast was not enabled.");
                Pump(model.UpdatePreferencesAsync(current => current with { Preview = (current.Preview ?? new()) with { ReadingMode = "Continuous" } }));
                Invoke("ApplyReadingPreferences");
            }
            Invoke("BindActiveDocument"); Invoke("SetLayout", "Editor");
            Pump(Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background).Task);
            var tab = model.ActiveDocument!;
            var editor = (TextEditor)window.FindName("Editor");
            var outline = (ListBox)window.FindName("Outline");
            var preview = (LensDocsStudio.Native.Rendering.DocumentPreview)window.FindName("Preview");
            if (shown)
            {
                foreach (var fixture in ContinuousPreviewFixtures.Create(root, "all"))
                {
                    var path = Path.Combine(root, fixture.Name + ".md"); File.WriteAllText(path, fixture.Source);
                    var bytes = File.ReadAllBytes(path);
                    Pump(model.OpenPathAsync(path, root)); Invoke("BindActiveDocument"); Invoke("SetLayout", "Preview");
                    Pump((Task)Invoke("UpdatePreviewAsync", false)!); Drain();
                    var result = (LensDocsStudio.Native.Rendering.PreviewResult)Field("rendered")!;
                    if (!result.ReadText().Contains(fixture.Last, StringComparison.Ordinal) || result.Warnings.Count != 0
                        || preview.ViewingMode != FlowDocumentReaderViewingMode.Scroll
                        || !File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) throw new IOException("Visible fixture opening lost content, media, Continuous mode or source.");
                    var last = result.SourceElements.MaxBy(item => item.Key).Value;
                    Pump(LensDocsStudio.Native.Rendering.PreviewNavigationService.BringIntoViewAsync(preview, last.ContentStart, last.ContentEnd)); Drain();
                    if (!preview.Selection.Text.Contains(fixture.Last, StringComparison.Ordinal)) throw new IOException("Visible final-target selection failed.");
                    var selected = preview.Selection.Text;
                    preview.Focus();
                    var sourceSelection = editor.SelectionLength;
                    model.ActiveDocument!.EditorDocument.Insert(0, "<!-- Fresh leading edit -->\n\n");
                    var caretAfterEdit = editor.CaretOffset;
                    Pump((Task)Invoke("UpdatePreviewAsync", false)!); Drain();
                    if (preview.Selection.Text != selected || !preview.IsKeyboardFocusWithin || editor.CaretOffset != caretAfterEdit
                        || editor.SelectionLength != sourceSelection) throw new IOException("Preview replacement lost mapped selection, focus or editor state.");
                    model.ActiveDocument.EditorDocument.UndoStack.Undo();
                    if (editor.Text != fixture.Source || !File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) throw new IOException("Fresh edit or rendering changed Undo or saved bytes.");
                    Pump((Task)Invoke("UpdatePreviewAsync", false)!); Drain();
                    var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using (var file = File.Create(Path.Combine(output, fixture.Name + "-visible-window.png"))) encoder.Save(file);
                    fixtureEvidence.Add(new { fixture.Name, Complete = true, DistantSelection = true, FreshEditUndo = true, FocusRetained = true,
                        SourceBytesPreserved = true, Dpi = VisualTreeHelper.GetDpi(window).PixelsPerInchX });
                    var reopened = model.ActiveDocument;
                    Pump(model.CloseDocumentCommand.ExecuteAsync(reopened)); Pump(model.OpenPathAsync(path, root)); Invoke("BindActiveDocument");
                    Pump((Task)Invoke("UpdatePreviewAsync", false)!); Drain();
                    if (editor.Text != fixture.Source) throw new IOException("Reopening changed exact fixture source.");
                }
                model.ActiveDocument = tab; Invoke("BindActiveDocument"); Invoke("SetLayout", "Editor");
            }
            var source = "# Current heading\n\nBefore.\n\n## Target heading\n\n<details>\n<summary>Evidence</summary>\n\nHidden current text.\n</details>";
            tab.EditorDocument.Text = source;
            Pump((Task)Invoke("UpdatePreviewAsync", false)!);
            if (outline.Items.Count != 2 || !model.PreviewStatus.Contains("paused", StringComparison.Ordinal)
                || preview.Document.Blocks.Count != 0 || editor.Text != source)
                throw new IOException("Editor layout did not retain its current outline while pausing native preview work: "
                    + JsonSerializer.Serialize(new { Headings = outline.Items.Count, model.PreviewStatus, Blocks = preview.Document?.Blocks.Count, SourceMatches = editor.Text == source }));
            // Visible fixture opening may already have selected this index. Exercise
            // a real selection change rather than assigning the retained value.
            outline.SelectedIndex = -1; outline.SelectedIndex = 1;
            if (editor.CaretOffset != source.IndexOf("## Target heading", StringComparison.Ordinal))
                throw new IOException("Editor-only outline navigation depended on a rendered preview.");
            Invoke("ShowFind", false);
            if (((TextBlock)window.FindName("FindContext")).Text != "Find in editor") throw new IOException("Editor layout routed Find to a hidden preview.");

            var copy = (Task<string?>)Invoke("PreparePreviewTextAsync")!; Pump(copy);
            if (copy.Result is not { } text || !text.Contains("Hidden current text.", StringComparison.Ordinal) || editor.Text != source || model.Layout != "Editor")
                throw new IOException("Explicit text preparation in Editor layout copied stale content or changed source/layout.");
            tab.EditorDocument.Insert(tab.EditorDocument.TextLength, "\n\nLatest edit.");
            Pump((Task)Invoke("UpdatePreviewAsync", false)!);
            if (preview.Document.Blocks.Count != 0) throw new IOException("Editing resumed unnecessary hidden preview construction.");
            copy = (Task<string?>)Invoke("PreparePreviewTextAsync")!; Pump(copy);
            if (!copy.Result!.Contains("Latest edit.", StringComparison.Ordinal)) throw new IOException("A second text preparation reused stale hidden preview text.");
            tab.EditorDocument.UndoStack.Undo();
            if (editor.Text != source) throw new IOException("Preview preparation damaged source Undo.");

            Invoke("SetLayout", "Preview"); Pump((Task)Invoke("UpdatePreviewAsync", false)!);
            if (preview.Document.Blocks.Count == 0 || !new TextRange(preview.Document.ContentStart, preview.Document.ContentEnd).Text.Contains("Current heading", StringComparison.Ordinal))
                throw new IOException("Showing preview did not render the current source.");
            Invoke("ShowFind", false);
            if (((TextBlock)window.FindName("FindContext")).Text != "Find in preview") throw new IOException("Preview layout routed Find to its hidden editor.");
            Invoke("SetLayout", "Editor");
            if (((System.Windows.FrameworkElement)window.FindName("FindBar")).Visibility != System.Windows.Visibility.Collapsed)
                throw new IOException("A layout switch retained the previous hidden search context.");

            tab.EditorDocument.Text = string.Join("\n\n", Enumerable.Range(1, 4000).Select(index => "Paragraph " + index));
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Input, () => tab.EditorDocument.Insert(0, "# Changed during preparation\n\n"));
            copy = (Task<string?>)Invoke("PreparePreviewTextAsync")!; Pump(copy);
            if (copy.Result is not null || !editor.Text.StartsWith("# Changed during preparation", StringComparison.Ordinal))
                throw new IOException("Changed source produced an obsolete copy result or lost the intervening edit.");
            Pump((Task)Invoke("UpdatePreviewAsync", false)!);
            if (outline.Items.Count != 1 || preview.Document.Blocks.Count != 0)
                throw new IOException("Cancelled preview work did not recover the latest editor-only outline.");

            Invoke("SetLayout", "Preview");
            var large = string.Join("\n", Enumerable.Range(1, 4000).Select(i => $"- **Café 😀 item{i}**"));
            tab.EditorDocument.Text = large;
            using (var editDuringConstruction = new DuringConstruction(() => tab.EditorDocument.Insert(0, "# Intervening edit\n\n")))
            {
                Pump((Task)Invoke("UpdatePreviewAsync", false)!);
                if (!editDuringConstruction.Observed || !editor.Text.StartsWith("# Intervening edit", StringComparison.Ordinal))
                    throw new IOException("The editing regression did not reach native construction.");
            }
            Pump((Task)Invoke("UpdatePreviewAsync", false)!);
            if (!new TextRange(preview.Document.ContentStart, preview.Document.ContentEnd).Text.Contains("Intervening edit", StringComparison.Ordinal))
                throw new IOException("A stale native construction result replaced the intervening edit.");
            var previousDocument = preview.Document;
            using (var themeDuringConstruction = new DuringConstruction(() => model.Theme = model.Theme == "Dark" ? "Light" : "Dark"))
            {
                Pump((Task)Invoke("UpdatePreviewAsync", false)!);
                if (!themeDuringConstruction.Observed || preview.Document != previousDocument)
                    throw new IOException("An obsolete appearance installed its completed document.");
            }
            foreach (var theme in new[] { "Light", "Dark", "Light", "Dark", "Light" })
            { model.Theme = theme; Invoke("ApplyTheme"); Invoke("QueuePreview"); }
            Pump((Task)Invoke("UpdatePreviewAsync", false)!);
            if (shown && !highContrast)
            {
                NativeTestHighContrast? transition = null;
                var installed = new List<object>(); var obsoleteAppearance = false; var transitionStarted = false;
                var documentProperty = DependencyPropertyDescriptor.FromProperty(FlowDocumentReader.DocumentProperty, typeof(LensDocsStudio.Native.Rendering.DocumentPreview));
                EventHandler installation = (_, _) =>
                {
                    if (!transitionStarted) return;
                    var actual = NativeTestHighContrast.IsEnabledNow;
                    var currentColour = preview.Document?.Foreground is SolidColorBrush brush && brush.Color == SystemColors.WindowTextColor;
                    if (actual && !currentColour) obsoleteAppearance = true;
                    if (installed.Count < 64) installed.Add(new { ActualHighContrast = actual, WpfHighContrast = SystemParameters.HighContrast,
                        CurrentSystemColour = currentColour, Colour = preview.Document?.Foreground?.ToString(), ExpectedColour = SystemColors.WindowTextColor.ToString() });
                };
                documentProperty.AddValueChanged(preview, installation);
                try
                {
                    previousDocument = preview.Document;
                    using (var contrastDuringConstruction = new DuringConstruction(() => { transitionStarted = true; transition = new NativeTestHighContrast(); }))
                    {
                        Pump((Task)Invoke("UpdatePreviewAsync", false)!);
                        File.WriteAllText(Path.Combine(output, "high-contrast-transition-initial.json"), JsonSerializer.Serialize(new
                        {
                            Observed = contrastDuringConstruction.Observed, TransitionCreated = transition is not null,
                            ActualHighContrast = NativeTestHighContrast.IsEnabledNow, WpfHighContrast = SystemParameters.HighContrast,
                            PreviousDocumentRetained = preview.Document == previousDocument, ObsoleteAppearance = obsoleteAppearance, Installations = installed
                        }, new JsonSerializerOptions { WriteIndented = true }));
                        // A Windows preference notification can also finish a newer valid
                        // render. Observe every installation; identity alone rejects it.
                        if (!contrastDuringConstruction.Observed || transition is null || !NativeTestHighContrast.IsEnabledNow || obsoleteAppearance)
                            throw new IOException("An actual High Contrast transition installed the obsolete appearance; see high-contrast-transition.json.");
                    }
                    var appearanceWait = Stopwatch.StartNew();
                    Pump(WaitForAppearanceAsync(true));
                    Invoke("QueuePreview"); Pump(WaitForCurrentAppearanceAsync()); Drain();
                    File.WriteAllText(Path.Combine(output, "high-contrast-transition.json"), JsonSerializer.Serialize(new
                    {
                        ActualHighContrast = NativeTestHighContrast.IsEnabledNow, WpfHighContrast = SystemParameters.HighContrast,
                        ObsoleteAppearance = obsoleteAppearance, Installations = installed
                    }, new JsonSerializerOptions { WriteIndented = true }));
                    if (obsoleteAppearance || preview.Document.Foreground is not SolidColorBrush textBrush || textBrush.Color != SystemColors.WindowTextColor)
                        throw new IOException("Current actual High Contrast preview lost system text colours.");
                    if (!editor.TextArea.TextView.LineTransformers.OfType<LensDocsStudio.Native.Editor.MarkdownHighlighting>().Single().HighContrast
                        || application.Resources["WindowBrush"] is not SolidColorBrush windowBrush || windowBrush.Color != SystemColors.WindowColor)
                        throw new IOException("The delayed Windows notification did not update the editor and application appearance automatically.");
                    cancellationEvidence.Add(new { Stage = "Actual OS High Contrast enabled during WPF construction", ObsoleteCompletionRejected = true,
                        AppearanceNotificationAndCurrentPreviewMilliseconds = appearanceWait.Elapsed.TotalMilliseconds });

                    async Task WaitForCurrentAppearanceAsync()
                    {
                        var wait = Stopwatch.StartNew();
                        while (true)
                        {
                            if (obsoleteAppearance) throw new IOException("An obsolete appearance installed while waiting for the current render.");
                            if (preview.Document != previousDocument && preview.Document.Foreground is SolidColorBrush currentBrush
                                && currentBrush.Color == SystemColors.WindowTextColor
                                && Field("renderedVersion") is ICSharpCode.AvalonEdit.Document.ITextSourceVersion version
                                && version.CompareAge(model.ActiveDocument!.EditorDocument.Version) == 0
                                && model.PreviewStatus.StartsWith("Preview updated", StringComparison.Ordinal)) return;
                            if (wait.Elapsed >= TimeSpan.FromSeconds(15)) throw new TimeoutException("Current appearance preview exceeded the existing validation deadline.");
                            await Task.Delay(25);
                        }
                    }
                }
                finally
                {
                    documentProperty.RemoveValueChanged(preview, installation);
                    transition?.Dispose(); Pump(WaitForAppearanceAsync(false)); Invoke("ApplyTheme"); Invoke("QueuePreview");
                    Pump((Task)Invoke("UpdatePreviewAsync", false)!); Drain();
                }
                if (SystemParameters.HighContrast) throw new IOException("Actual High Contrast was not restored after the transition case.");
            }
            tab.EditorDocument.UndoStack.Undo();
            if (editor.Text != large) throw new IOException("Rendering changed large-document Undo or Unicode source.");
            var gate = (SemaphoreSlim)Field("previewGate")!;
            gate.Wait(); Task latestQueued;
            try
            {
                previousDocument = preview.Document;
                var queued = (Task)Invoke("UpdatePreviewAsync", false)!;
                if (queued.IsCompleted) throw new IOException("The queued cancellation case did not wait for rendering capacity.");
                var cancelWatch = Stopwatch.StartNew();
                tab.EditorDocument.Insert(tab.EditorDocument.TextLength, "\n\nLatest queued source.");
                latestQueued = (Task)Invoke("UpdatePreviewAsync", false)!;
                Pump(queued);
                cancellationEvidence.Add(new { Stage = "Queued behind preview gate", Milliseconds = cancelWatch.Elapsed.TotalMilliseconds });
                if (preview.Document != previousDocument) throw new IOException("Cancelled queued work installed a document.");
            }
            finally { gate.Release(); }
            Pump(latestQueued);
            if (!new TextRange(preview.Document.ContentStart, preview.Document.ContentEnd).Text.Contains("Latest queued source.", StringComparison.Ordinal))
                throw new IOException("The latest queued source did not render after capacity became available.");
            tab.EditorDocument.UndoStack.Undo();
            if (editor.Text != large) throw new IOException("Queued rendering changed source Undo.");
            using (var rapidEdits = new DuringConstruction(() =>
            {
                for (var revision = 0; revision < 12; revision++) tab.EditorDocument.Insert(tab.EditorDocument.TextLength, "\n\nFresh revision " + revision);
                var distant = tab.EditorDocument.Text.IndexOf("item4000", StringComparison.Ordinal);
                tab.EditorDocument.Replace(distant, "item4000".Length, "distant changed item");
            }))
            {
                Pump((Task)Invoke("UpdatePreviewAsync", false)!);
                if (!rapidEdits.Observed) throw new IOException("Rapid edits did not reach native construction.");
            }
            Pump((Task)Invoke("UpdatePreviewAsync", false)!);
            var latestText = new TextRange(preview.Document.ContentStart, preview.Document.ContentEnd).Text;
            if (!latestText.Contains("Fresh revision 11", StringComparison.Ordinal) || !latestText.Contains("distant changed item", StringComparison.Ordinal)
                || latestText.Contains("item4000", StringComparison.Ordinal)) throw new IOException("Rapid/distant source edits installed an obsolete preview.");
            for (var undo = 0; undo < 13; undo++) tab.EditorDocument.UndoStack.Undo();
            if (editor.Text != large) throw new IOException("Rapid edit preview regeneration damaged exact Undo history.");
            using (var switchDuringConstruction = new DuringConstruction(() =>
            {
                model.NewDocumentCommand.Execute(null);
                model.ActiveDocument!.EditorDocument.Text = "# Intermediate tab\n\nIntermediate content.";
                model.NewDocumentCommand.Execute(null);
                model.ActiveDocument!.EditorDocument.Text = "# Latest tab\n\nLatest content.";
                Invoke("BindActiveDocument");
            }))
            {
                Pump((Task)Invoke("UpdatePreviewAsync", false)!);
                if (!switchDuringConstruction.Observed) throw new IOException("Rapid switching did not interrupt native construction.");
            }
            Pump((Task)Invoke("UpdatePreviewAsync", false)!);
            if (editor.Text != "# Latest tab\n\nLatest content." || tab.EditorDocument.Text != large
                || !new TextRange(preview.Document.ContentStart, preview.Document.ContentEnd).Text.Contains("Latest content.", StringComparison.Ordinal)
                || new TextRange(preview.Document.ContentStart, preview.Document.ContentEnd).Text.Contains("item4000", StringComparison.Ordinal))
                throw new IOException("Obsolete construction mixed documents after rapid tab switching.");
            model.ActiveDocument = tab; Invoke("BindActiveDocument");
            var closingPath = Path.Combine(root, "closing.md");
            var closingBytes = System.Text.Encoding.UTF8.GetBytes(large); File.WriteAllBytes(closingPath, closingBytes);
            tab.MarkSaved(new LensDocsStudio.Core.Documents.DocumentSnapshot(closingPath, large,
                LensDocsStudio.Core.Documents.DocumentSnapshot.Hash(closingBytes), false));
            Task? closed = null;
            using (var closeDuringConstruction = new DuringConstruction(() =>
            {
                closed = model.CloseDocumentCommand.ExecuteAsync(tab);
            }))
            {
                Pump((Task)Invoke("UpdatePreviewAsync", false)!);
                if (!closeDuringConstruction.Observed) throw new IOException("Closing the tab did not interrupt native construction.");
            }
            if (closed is null) throw new IOException("The close command was not invoked.");
            Pump(closed);
            Pump((Task)Invoke("UpdatePreviewAsync", false)!);
            if (tab.EditorDocument.Text != large || model.Documents.Contains(tab)
                || !File.ReadAllBytes(closingPath).AsSpan().SequenceEqual(closingBytes)
                || !new TextRange(preview.Document.ContentStart, preview.Document.ContentEnd).Text.Contains("Latest content.", StringComparison.Ordinal))
                throw new IOException("Closing a tab committed obsolete native rendering or changed its source: " + JsonSerializer.Serialize(new
                { SourceMatches = tab.EditorDocument.Text == large, RemainsOpen = model.Documents.Contains(tab),
                    FileMatches = File.ReadAllBytes(closingPath).AsSpan().SequenceEqual(closingBytes),
                    ActiveSource = model.ActiveDocument?.EditorDocument.Text, PreviewText = new TextRange(preview.Document.ContentStart, preview.Document.ContentEnd).Text,
                    model.PreviewStatus }));

            previousDocument = preview.Document;
            model.ActiveDocument!.EditorDocument.Text = large;
            using (var closeWindowDuringConstruction = new DuringConstruction(() => Invoke("CompleteClose")))
            {
                Pump((Task)Invoke("UpdatePreviewAsync", false)!);
                if (!closeWindowDuringConstruction.Observed || preview.Document != previousDocument)
                    throw new IOException("A closing window installed an obsolete preview.");
            }
            heartbeat.Stop();
            File.WriteAllText(Path.Combine(output, "native-preview-scheduling.json"), JsonSerializer.Serialize(new { Passed = true,
                VisibleWindow = shown, ActualHighContrast = SystemParameters.HighContrast, Fixtures = fixtureEvidence,
                Cancellations = cancellationEvidence,
                InputHeartbeatMilliseconds = gaps, MaximumObservedInputGapMilliseconds = gaps.DefaultIfEmpty().Max(),
                Checks = new[] { "Editor-only layout retains current headings and source navigation without a preview",
                    "Explicit text preparation includes current closed details and subsequent edits without changing layout or Undo",
                    "Showing preview renders current source and Find follows the visible layout",
                    "Intervening edits cancel text preparation without yielding an obsolete copy result",
                    "Editing during native construction preserves current source, preview and Undo",
                    "Rapid tab switching cannot commit obsolete or mixed preview content",
                    "Closing a tab during native construction leaves its source intact and renders the surviving tab",
                    "Obsolete appearance and closing-window completions cannot install; rapid themes retain the latest source" },
                Limitation = "In-process WPF input heartbeats include synchronous layout stalls. They are separate from foreground keyboard/UIA transport checks. Visible-window rasters are labelled; native UIA captures are retained separately." }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            heartbeat.Stop();
            if (window is not null) { if (!(bool)Field("closing")!) Invoke("CompleteClose"); } else model.Dispose();
            SynchronizationContext.SetSynchronizationContext(previous);
        }
        object? Invoke(string name, params object[] arguments) => typeof(MainWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, arguments);
        object? Field(string name) => typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window);
        void Drain() => Pump(Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Task);
        async Task WaitForAppearanceAsync(bool expected)
        {
            var wait = Stopwatch.StartNew();
            while (SystemParameters.HighContrast != expected || NativeTestHighContrast.IsEnabledNow != expected)
            {
                if (wait.Elapsed >= TimeSpan.FromSeconds(15)) throw new TimeoutException("Windows appearance notification exceeded the existing validation deadline.");
                await Task.Delay(25);
            }
        }
    }

    private sealed class DuringConstruction(Action action) : EventListener
    {
        public bool Observed { get; private set; }
        protected override void OnEventSourceCreated(EventSource source)
        { if (source.Name == "LensDocsStudio-Native-PreviewConstruction") EnableEvents(source, EventLevel.Verbose); }
        protected override void OnEventWritten(EventWrittenEventArgs args)
        {
            if (Observed || args.EventId != 1 || args.Payload?[0]?.ToString() != "document-ready") return;
            Observed = true;
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Input, action);
        }
    }

    private static void Pump(Task task)
    {
        if (task.IsCompleted) { task.GetAwaiter().GetResult(); return; }
        var frame = new DispatcherFrame(); var expired = false;
        var timer = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromSeconds(15) };
        timer.Tick += (_, _) => { expired = true; frame.Continue = false; };
        var dispatcher = Dispatcher.CurrentDispatcher;
        _ = task.ContinueWith(_ => dispatcher.BeginInvoke(DispatcherPriority.Send, () => frame.Continue = false), TaskScheduler.Default);
        timer.Start(); Dispatcher.PushFrame(frame); timer.Stop();
        if (expired) throw new TimeoutException("Preview scheduling did not finish within the validation deadline.");
        task.GetAwaiter().GetResult();
    }
}
