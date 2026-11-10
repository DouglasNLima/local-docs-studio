using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using LensDocsStudio.Core.Rendering;
using ICSharpCode.AvalonEdit;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;
using LensDocsStudio.Native.ViewModels;
using LensDocsStudio.Native.Validation;

namespace LensDocsStudio.Native.UxProbe;

// Focused PR #80 control extracted from 22c223c6, using its unchanged mixed fixture.
// Owns an isolated WPF window; no injected desktop input or OS appearance changes.
internal static class LayoutControl
{
    internal static int Run(string[] args)
    {
        var fixtures = Path.GetFullPath(args[1]); var output = Path.GetFullPath(args[2]);
        var mode = args.FirstOrDefault(a => a.StartsWith("--mode=", StringComparison.Ordinal))?[7..] ?? "Continuous";
        var zoom = double.Parse(args.FirstOrDefault(a => a.StartsWith("--zoom=", StringComparison.Ordinal))?[7..] ?? "100", System.Globalization.CultureInfo.InvariantCulture);
        var shown = args.Contains("--shown");
        if (SystemParameters.HighContrast != args.Contains("--expect-high-contrast") || NativeTestHighContrast.IsEnabledNow != args.Contains("--expect-high-contrast"))
            throw new IOException("Actual Windows and WPF appearance must match the requested test.");
        if (Directory.Exists(output)) throw new IOException("Use a fresh controlled evidence directory.");
        Directory.CreateDirectory(output);
        var state = Path.Combine(output, "state"); Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new UserPreferences(Theme: "System", Layout: "Editor",
            AutomaticUpdateChecks: false, ShowOutline: false, Preview: new(ReadingMode: mode))));
        var sourcePath = Path.Combine(fixtures, "mixed.md");
        var source = File.ReadAllText(sourcePath);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
        ThemeService.Apply(app.Resources, "System");
        var dialogs = new DesktopDialogs();
        using var model = new WorkspaceViewModel(new DocumentStore(Path.Combine(state, "writes")), new SessionStore(state), new WorkspaceService(), dialogs);
        var window = new MainWindow(model, dialogs, []) { Width = 1024, Height = 720, Left = shown ? 40 : -20000, Top = shown ? 40 : -20000,
            ShowActivated = shown, ShowInTaskbar = shown, WindowStartupLocation = WindowStartupLocation.Manual };
        var loaded = typeof(MainWindow).GetMethod("WindowLoaded", BindingFlags.Instance | BindingFlags.NonPublic)!;
        window.Loaded -= (RoutedEventHandler)loaded.CreateDelegate(typeof(RoutedEventHandler), window);
        var closing = typeof(MainWindow).GetMethod("WindowClosing", BindingFlags.Instance | BindingFlags.NonPublic)!;
        window.Closing -= (CancelEventHandler)closing.CreateDelegate(typeof(CancelEventHandler), window);
        var preview = (DocumentPreview)window.FindName("Preview"); var editor = (TextEditor)window.FindName("Editor");
        var code = 1;
        var facts = new List<object>();
        object? Field(string name) => typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(window);
        object? Call(string name, params object[] arguments) => typeof(MainWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, arguments);
        PreviewResult Result() => (PreviewResult)Field("rendered")!;
        ScrollViewer? Scroll() => PreviewSemanticAnchors.Descendant<FlowDocumentScrollViewer>(preview) is { } reader
            ? reader.Template.FindName("PART_ContentHost", reader) as ScrollViewer : null;
        TextPointer End()
        {
            if (!Result().TextMap.TryPreviewRange(source.IndexOf("ReadableEnd", StringComparison.Ordinal), 11, out var first, out _)) throw new IOException("Distant source map missing.");
            return first!;
        }
        double ContextSpan()
        {
            if (!Result().TextMap.TryPreviewRange(source.IndexOf("Distant target", StringComparison.Ordinal), 14, out var first, out _))
                throw new IOException("Distant heading source map missing.");
            return End().GetCharacterRect(LogicalDirection.Forward).Top - first!.GetCharacterRect(LogicalDirection.Forward).Top;
        }
        async Task Until(Func<bool> predicate)
        {
            var clock = Stopwatch.StartNew();
            do { if (predicate()) return; await Task.Delay(8); } while (clock.Elapsed.TotalSeconds < 30);
            throw new TimeoutException("Controlled semantic response was not observed within 30 seconds.");
        }
        bool VisibleText() => UIElementAutomationPeer.CreatePeerForElement(preview)?.GetPattern(PatternInterface.Text) is ITextProvider provider &&
            provider.GetVisibleRanges().Any(r => !string.IsNullOrWhiteSpace(r.GetText(128)) && r.GetBoundingRectangles().Length >= 4);
        object Snapshot(string stage)
        {
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(Path.Combine(output, $"{facts.Count:00}-{stage}.png"))) png.Save(stream);
            var scroll = Scroll();
            return new { stage, model.Layout, model.PreviewStatus, sourceMatches = editor.Text == source,
                sameAttachedDocument = preview.Document == Result().Document,
                activeDocument = model.ActiveDocument?.Id, renderedDocument = Field("renderedDocumentId"),
                sourceGeneration = Field("diagnosticSourceGeneration"), acceptedGeneration = Field("diagnosticAcceptedGeneration"),
                loadingGeneration = ((PreviewLoadingState)Field("previewLoading")!).Generation,
                loadingPhase = ((PreviewLoadingState)Field("previewLoading")!).Phase.ToString(),
                itemStatus = AutomationProperties.GetItemStatus(preview), preview.IsVisible, preview.IsMeasureValid, preview.IsArrangeValid,
                verticalOffset = scroll?.VerticalOffset, extentHeight = scroll?.ExtentHeight, viewportHeight = scroll?.ViewportHeight, preview.ActualWidth, preview.ActualHeight,
                end = End().GetCharacterRect(LogicalDirection.Forward).ToString(), textPatternVisible = VisibleText(),
                media = Result().Media.Select(m => new { m.Image.ActualWidth, m.Image.ActualHeight, m.Image.IsVisible,
                    bounds = preview.IsAncestorOf(m.Image) ? m.Image.TransformToAncestor(preview).TransformBounds(new Rect(m.Image.RenderSize)).ToString() : null }).ToArray() };
        }
        window.Show();
        _ = window.Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                await model.InitialiseAsync([sourcePath]); Call("ApplyTheme"); Call("ApplyReadingPreferences");
                Call("BindActiveDocument"); Call("SetLayout", "Split");
                await (Task)Call("UpdatePreviewAsync", false)!;
                preview.Zoom = zoom;
                await Until(() => Field("rendered") is PreviewResult && preview.IsMeasureValid && preview.IsArrangeValid && (mode == "Pages" || End().HasValidLayout) && VisibleText());
                // Select a real source-mapped end position, then let normal scroll layout settle.
                await PreviewNavigationService.BringIntoViewAsync(preview, End());
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                await Until(() => End().GetCharacterRect(LogicalDirection.Forward).Bottom <= preview.ActualHeight && VisibleText());
                facts.Add(Snapshot("split-at-end"));
                var readingPosition = End().GetCharacterRect(LogicalDirection.Forward).Top;
                var contextSpan = ContextSpan();
                var document = preview.Document;
                Call("SetLayout", "Preview");
                if (!args.Contains("--expect-displacement")) Call("SetLayout", "Preview"); // Re-select while presentation is pending.
                if (!args.Contains("--expect-displacement") && mode != "Pages" && model.PreviewStatus.StartsWith("Preview updated", StringComparison.Ordinal))
                    throw new IOException("An unfinished reflow falsely announced an updated preview.");
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                await Until(() => preview.IsMeasureValid && preview.IsArrangeValid && End().HasValidLayout);
                facts.Add(Snapshot("preview-after-reflow"));
                var displaced = !VisibleText();
                if (preview.Document != document || editor.Text != source) throw new IOException("The controlled resize changed document ownership or source.");
                if (!args.Contains("--expect-displacement"))
                {
                    await Until(() => !AutomationProperties.GetItemStatus(preview).StartsWith("Busy", StringComparison.Ordinal) && VisibleText());
                    if (mode != "Pages" && Math.Abs(End().GetCharacterRect(LogicalDirection.Forward).Top - readingPosition) > Math.Abs(ContextSpan() - contextSpan) + 2)
                        throw new IOException("Reflow changed the visible reading position at the selected zoom.");
                    var loading = (PreviewLoadingState)Field("previewLoading")!;
                    if (mode != "Pages" && (loading.Busy || loading.Phase != PreviewLoadingPhase.Completed || loading.Context?.Layout != "Preview"))
                        throw new IOException("Current layout restoration did not complete its owned loading presentation.");
                    var owner = model.ActiveDocument!;
                    preview.Selection.Select(End(), End().GetPositionAtOffset(11));
                    editor.Select(4, 5); var caret = editor.CaretOffset;
                    foreach (var layout in new[] { "Split", "Editor", "Preview", "Split", "Preview" })
                    {
                        if (preview.IsVisible) { readingPosition = End().GetCharacterRect(LogicalDirection.Forward).Top; contextSpan = ContextSpan(); }
                        Call("SetLayout", layout);
                        if (layout == "Editor") continue;
                        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                        await Until(() => !AutomationProperties.GetItemStatus(preview).StartsWith("Busy", StringComparison.Ordinal)
                            && VisibleText() && End().GetCharacterRect(LogicalDirection.Forward).Top < preview.ActualHeight);
                        if (preview.Document != document || editor.CaretOffset != caret || editor.SelectionStart != 4 || editor.SelectionLength != 5
                            || preview.Selection.Text != "ReadableEnd" || editor.Text != source)
                            throw new IOException("Layout reflow changed independent selection, caret, source or document ownership.");
                        // At high zoom the preceding heading can wrap onto another
                        // line; account for that measured change in source geometry.
                        if (mode != "Pages" && Math.Abs(End().GetCharacterRect(LogicalDirection.Forward).Top - readingPosition) > Math.Abs(ContextSpan() - contextSpan) + 2)
                            throw new IOException("Repeated reflow changed the visible reading position at the selected zoom.");
                        facts.Add(Snapshot("retained-" + layout));
                    }
                    // Source input supersedes the owned layout and cannot publish the old completion.
                    Call("SetLayout", "Split");
                    var oldLoading = loading.Generation;
                    editor.Document.Insert(0, "Transient source edit\n");
                    if (loading.Generation == oldLoading && loading.Busy)
                        throw new IOException("A source edit retained obsolete layout ownership.");
                    editor.Document.Remove(0, "Transient source edit\n".Length);
                    await (Task)Call("UpdatePreviewAsync", false)!;
                    await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                    await Until(() => VisibleText() && !loading.Busy);
                    document = preview.Document;
                    await PreviewNavigationService.BringIntoViewAsync(preview, End());
                    await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                    if (editor.Text != source || !Result().ReadText().Contains("ReadableEnd", StringComparison.Ordinal))
                        throw new IOException("Rapid source edits left an obsolete preview.");
                    facts.Add(Snapshot("current-after-source-edits"));
                    // Supersede queued reflows before their callbacks can run.
                    foreach (var layout in new[] { "Split", "Preview", "Editor", "Preview" }) Call("SetLayout", layout);
                    await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                    await Until(() => !AutomationProperties.GetItemStatus(preview).StartsWith("Busy", StringComparison.Ordinal) && VisibleText());
                    facts.Add(Snapshot("rapid-transitions"));
                    // A zoom change invalidates a pending restoration without reporting failure.
                    Call("SetLayout", "Split");
                    preview.Zoom = zoom == 100 ? 110 : 100;
                    await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                    await Until(() => !loading.Busy);
                    if (loading.Phase == PreviewLoadingPhase.Failed || model.PreviewStatus.Contains("could not", StringComparison.Ordinal))
                        throw new IOException("Superseded zoom was reported as a restoration failure.");
                    preview.Zoom = zoom;
                    await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                    await PreviewNavigationService.BringIntoViewAsync(preview, End());
                    await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                    // Opening Find supersedes reflow; query/match and independent reader state survive layout changes.
                    Call("SetLayout", "Preview");
                    Call("ShowFind", false);
                    var find = (TextBox)window.FindName("FindText"); find.Text = "ReadableEnd";
                    await (Task)Call("RefreshFindAsync")!;
                    var query = find.Text; var matches = Field("findMatches");
                    var findGeneration = Field("findGeneration");
                    if (loading.Busy) throw new IOException("Find left obsolete reflow pending.");
                    await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                    await PreviewNavigationService.BringIntoViewAsync(preview, End());
                    await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                    // A stable focus target can remain present in both layouts.
                    var focusTarget = (TextBox)window.FindName("WorkspaceFilterField");
                    if (shown && !focusTarget.Focus()) throw new IOException("The shown focus control was not focusable.");
                    var focusBefore = Keyboard.FocusedElement;
                    Call("SetLayout", "Split");
                    await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                    await Until(() => !loading.Busy && VisibleText());
                    if (find.Text != query || !Equals(Field("findGeneration"), findGeneration) || Field("findMatches") != matches)
                        throw new IOException("Layout restoration changed the existing Find state.");
                    if (shown && !ReferenceEquals(focusBefore, Keyboard.FocusedElement))
                        throw new IOException("Layout restoration stole keyboard focus.");
                    facts.Add(new { stage = "find-and-zoom-cancellation", queryPreserved = true, matchStatePreserved = true,
                        zoom = preview.Zoom, status = model.PreviewStatus, loading = loading.Phase.ToString(),
                        scope = "Existing layout behaviour closes the Find bar; its query and match state remain unchanged" });
                    Call("SetLayout", "Split");
                    model.NewDocumentCommand.Execute(null);
                    const string replacement = "# Replacement\n\nCurrent replacement\n";
                    model.ActiveDocument!.EditorDocument.Text = replacement;
                    await (Task)Call("UpdatePreviewAsync", false)!;
                    await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                    await Until(() => preview.IsMeasureValid && preview.IsArrangeValid && VisibleText());
                    if (preview.Document == document || editor.Text != replacement || owner.EditorDocument.Text != source
                        || !Result().ReadText().Contains("Current replacement", StringComparison.Ordinal))
                        throw new IOException("A stale reflow replaced the incoming document or lost the outgoing source.");
                    owner.EditorDocument.Insert(0, "Unsaved local change\n");
                    await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                    if (owner.EditorDocument.Text != "Unsaved local change\n" + source || editor.Text != replacement || File.ReadAllText(sourcePath) != source)
                        throw new IOException("The reflow/cancellation path lost an unsaved change or changed the fixture file.");
                    facts.Add(new { stage = "superseded-by-document", currentSource = true, outgoingUnsavedPreserved = true, fixtureUnchanged = true });
                }
                Program.Write(output, "control.json", new { verdict = displaced ? "OBSERVED EMPTY TEXT VIEWPORT" : "VISIBLE TEXT", facts,
                    fixtureSha256 = Program.Hash(File.ReadAllBytes(sourcePath)),
                    product = typeof(MainWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion, mode, zoom,
                    highContrast = SystemParameters.HighContrast, osHighContrast = NativeTestHighContrast.IsEnabledNow,
                    shown, scope = "Natural WPF layout and UIA text geometry; window render captures, no injected desktop input or physical scan-out certification" });
                code = displaced == args.Contains("--expect-displacement") ? 0 : 1;
            }
            catch (Exception error) { Program.Write(output, "failure.json", new { error = error.ToString(), facts }); Console.Error.WriteLine(error); }
            finally { app.Shutdown(code); }
        });
        app.Run(); return code;
    }
}
