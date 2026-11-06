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
using ICSharpCode.AvalonEdit;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;
using LensDocsStudio.Native.ViewModels;
using LensDocsStudio.Native.Validation;

namespace LensDocsStudio.Native.UxProbe;

// Controlled geometry experiment: owns an off-screen window and sends no desktop input.
// This cannot substitute for the unchanged foreground journey or physical screenshots.
internal static class LayoutControl
{
    internal static int Run(string[] args)
    {
        var fixtures = Path.GetFullPath(args[1]); var output = Path.GetFullPath(args[2]);
        var mode = args.FirstOrDefault(a => a.StartsWith("--mode=", StringComparison.Ordinal))?[7..] ?? "Continuous";
        using var contrast = args.Contains("--high-contrast") ? new NativeTestHighContrast() : null;
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
        var window = new MainWindow(model, dialogs, []) { Width = 1024, Height = 720, Left = -20000, Top = -20000,
            ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual };
        var loaded = typeof(MainWindow).GetMethod("WindowLoaded", BindingFlags.Instance | BindingFlags.NonPublic)!;
        window.Loaded -= (RoutedEventHandler)loaded.CreateDelegate(typeof(RoutedEventHandler), window);
        var closing = typeof(MainWindow).GetMethod("WindowClosing", BindingFlags.Instance | BindingFlags.NonPublic)!;
        window.Closing -= (CancelEventHandler)closing.CreateDelegate(typeof(CancelEventHandler), window);
        var preview = (DocumentPreview)window.FindName("Preview"); var editor = (TextEditor)window.FindName("Editor");
        var code = 1;
        using var trace = new TransitionTrace(window, model, output);
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
            trace.Boundary(stage);
            var scroll = Scroll();
            return new { stage, model.Layout, model.PreviewStatus, sourceMatches = editor.Text == source,
                sameAttachedDocument = preview.Document == Result().Document, preview.IsVisible, preview.IsMeasureValid, preview.IsArrangeValid,
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
                await Until(() => Field("rendered") is PreviewResult && preview.IsMeasureValid && preview.IsArrangeValid && (mode == "Pages" || End().HasValidLayout) && VisibleText());
                // Select a real source-mapped end position, then let normal scroll layout settle.
                await PreviewNavigationService.BringIntoViewAsync(preview, End());
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                await Until(() => End().GetCharacterRect(LogicalDirection.Forward).Bottom <= preview.ActualHeight && VisibleText());
                facts.Add(Snapshot("split-at-end"));
                var document = preview.Document;
                Call("SetLayout", "Preview");
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
                    var owner = model.ActiveDocument!;
                    preview.Selection.Select(End(), End().GetPositionAtOffset(11));
                    editor.Select(4, 5); var caret = editor.CaretOffset;
                    foreach (var layout in new[] { "Split", "Editor", "Preview", "Split", "Preview" })
                    {
                        Call("SetLayout", layout);
                        if (layout == "Editor") continue;
                        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                        await Until(() => !AutomationProperties.GetItemStatus(preview).StartsWith("Busy", StringComparison.Ordinal)
                            && VisibleText() && End().GetCharacterRect(LogicalDirection.Forward).Bottom <= preview.ActualHeight);
                        if (preview.Document != document || editor.CaretOffset != caret || editor.SelectionStart != 4 || editor.SelectionLength != 5
                            || preview.Selection.Text != "ReadableEnd" || editor.Text != source)
                            throw new IOException("Layout reflow changed independent selection, caret, source or document ownership.");
                        facts.Add(Snapshot("retained-" + layout));
                    }
                    // Supersede queued reflows before their callbacks can run.
                    foreach (var layout in new[] { "Split", "Preview", "Editor", "Preview" }) Call("SetLayout", layout);
                    await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                    await Until(() => !AutomationProperties.GetItemStatus(preview).StartsWith("Busy", StringComparison.Ordinal) && VisibleText());
                    facts.Add(Snapshot("rapid-transitions"));
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
                    product = typeof(MainWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion, mode,
                    highContrast = SystemParameters.HighContrast, osHighContrast = NativeTestHighContrast.IsEnabledNow,
                    scope = "Controlled off-screen natural WPF layout; no real input or physical display acceptance" });
                code = displaced == args.Contains("--expect-displacement") ? 0 : 1;
            }
            catch (Exception error) { Program.Write(output, "failure.json", new { error = error.ToString(), facts }); Console.Error.WriteLine(error); }
            finally { app.Shutdown(code); }
        });
        app.Run(); return code;
    }
}
