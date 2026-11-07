using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;
using LensDocsStudio.Native.Validation;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.UxProbe;

// Natural off-screen WPF geometry/ownership control. No foreground input or visual certification.
internal static class ComparisonControl
{
    internal static int Run(string[] args)
    {
        var fixtures = Path.GetFullPath(args[1]); var output = Path.GetFullPath(args[2]);
        var mode = args.FirstOrDefault(a => a.StartsWith("--mode=", StringComparison.Ordinal))?[7..] ?? "Continuous";
        var zoom = double.Parse(args.FirstOrDefault(a => a.StartsWith("--zoom=", StringComparison.Ordinal))?[7..] ?? "100", System.Globalization.CultureInfo.InvariantCulture);
        var scenario = args.FirstOrDefault(a => a.StartsWith("--scenario=", StringComparison.Ordinal))?[11..] ?? "close";
        using var contrast = args.Contains("--high-contrast") ? new NativeTestHighContrast() : null;
        if (Directory.Exists(output)) throw new IOException("Use a fresh comparison evidence directory.");
        Directory.CreateDirectory(output);
        var state = Path.Combine(output, "state"); Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new UserPreferences(Theme: "System", Layout: "Editor",
            AutomaticUpdateChecks: false, ShowOutline: false, Preview: new(ReadingMode: mode))));
        var source = File.ReadAllText(Path.Combine(fixtures, "mixed.md"));
        var firstPath = Path.Combine(output, "mixed.md"); File.WriteAllText(firstPath, source);
        var secondPath = Path.Combine(output, "second.md"); var secondSource = "# Secondary owner\n\n" + source; File.WriteAllText(secondPath, secondSource);
        File.Copy(Path.Combine(fixtures, "pixel.png"), Path.Combine(output, "pixel.png"));
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
        ThemeService.Apply(app.Resources, "System");
        var dialogs = new DesktopDialogs();
        using var model = new WorkspaceViewModel(new DocumentStore(Path.Combine(state, "writes")), new SessionStore(state), new WorkspaceService(), dialogs);
        var compact = args.Contains("--compact");
        var window = new MainWindow(model, dialogs, []) { Width = compact ? 1024 : 1440, Height = compact ? 720 : 900, Left = -20000, Top = -20000,
            ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual };
        var loaded = typeof(MainWindow).GetMethod("WindowLoaded", BindingFlags.Instance | BindingFlags.NonPublic)!;
        window.Loaded -= (RoutedEventHandler)loaded.CreateDelegate(typeof(RoutedEventHandler), window);
        var closing = typeof(MainWindow).GetMethod("WindowClosing", BindingFlags.Instance | BindingFlags.NonPublic)!;
        window.Closing -= (CancelEventHandler)closing.CreateDelegate(typeof(CancelEventHandler), window);
        var preview = (DocumentPreview)window.FindName("Preview"); var editor = (TextEditor)window.FindName("Editor");
        var code = 1; var facts = new List<object>();
        using var trace = new TransitionTrace(window, model, output);
        object? Field(string name) => typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(window);
        object? Call(string name, params object[] values) => typeof(MainWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, values);
        PreviewResult Result(bool secondary) => (PreviewResult)Field(secondary ? "comparisonPreviewResult" : "rendered")!;
        DocumentPreview Reader(bool secondary) => secondary ? (DocumentPreview)Field("comparisonPreview")! : preview;
        TextPointer End(bool secondary, string text)
        {
            if (!Result(secondary).TextMap.TryPreviewRange(text.IndexOf("ReadableEnd", StringComparison.Ordinal), 11, out var start, out _))
                throw new IOException("Current comparison source map is missing.");
            return start!;
        }
        bool Visible(bool secondary, DocumentViewModel owner)
        {
            var reader = Reader(secondary); var result = Field(secondary ? "comparisonPreviewResult" : "rendered") as PreviewResult;
            if (result is null || reader.Document != result.Document || !reader.IsVisible || !reader.IsMeasureValid || !reader.IsArrangeValid
                || Field(secondary ? "comparisonRenderedId" : "renderedDocumentId") is not Guid id || id != owner.Id
                || Field(secondary ? "comparisonRenderedVersion" : "renderedVersion") is not ITextSourceVersion version
                || !version.BelongsToSameDocumentAs(owner.EditorDocument.Version) || version.CompareAge(owner.EditorDocument.Version) != 0
                || AutomationProperties.GetItemStatus(reader).StartsWith("Busy", StringComparison.Ordinal)) return false;
            return UIElementAutomationPeer.CreatePeerForElement(reader)?.GetPattern(PatternInterface.Text) is ITextProvider provider
                && provider.GetVisibleRanges().Any(range => !string.IsNullOrWhiteSpace(range.GetText(128)) && range.GetBoundingRectangles().Length >= 4);
        }
        bool EndVisible(bool secondary, DocumentViewModel owner)
        {
            if (!Visible(secondary, owner) || !End(secondary, owner.EditorDocument.Text).HasValidLayout) return false;
            var rectangle = End(secondary, owner.EditorDocument.Text).GetCharacterRect(LogicalDirection.Forward);
            return !rectangle.IsEmpty && rectangle.Top >= 0 && rectangle.Bottom <= Reader(secondary).ActualHeight;
        }
        async Task Until(Func<bool> predicate)
        {
            var clock = Stopwatch.StartNew();
            do { if (predicate()) return; await Task.Delay(8); } while (clock.Elapsed.TotalSeconds < 30);
            throw new TimeoutException("Comparison semantic response was not observed within 30 seconds.");
        }
        object Snapshot(string stage, bool secondary, DocumentViewModel owner)
        {
            trace.Boundary(stage);
            var reader = Reader(secondary); var result = Result(secondary);
            return new { stage, secondary, owner = owner.Id, model.Layout, model.PreviewStatus,
                itemStatus = AutomationProperties.GetItemStatus(reader), sameAttached = reader.Document == result.Document,
                reader.IsVisible, reader.IsMeasureValid, reader.IsArrangeValid, reader.ActualWidth, reader.ActualHeight,
                mode = reader.ViewingMode.ToString(), reader.Zoom, visible = Visible(secondary, owner), endVisible = EndVisible(secondary, owner),
                end = End(secondary, owner.EditorDocument.Text).HasValidLayout ? End(secondary, owner.EditorDocument.Text).GetCharacterRect(LogicalDirection.Forward).ToString() : null,
                sourceSha256 = Program.Hash(System.Text.Encoding.UTF8.GetBytes(owner.EditorDocument.Text)),
                media = result.Media.Select(m => new { m.Image.ActualWidth, m.Image.ActualHeight }).ToArray() };
        }
        window.Show();
        _ = window.Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                await model.InitialiseAsync([secondPath, firstPath]); Call("ApplyTheme"); Call("ApplyReadingPreferences");
                Call("BindActiveDocument"); Call("SetLayout", "Preview"); await (Task)Call("UpdatePreviewAsync", false)!;
                var first = model.ActiveDocument!; var second = model.Documents.Single(d => d.FilePath == secondPath);
                preview.Zoom = zoom;
                await Until(() => Visible(false, first));
                Call("StartComparison", first, second);
                await Until(() => Visible(false, first) && Visible(true, second));
                await PreviewNavigationService.BringIntoViewAsync(preview, End(false, source));
                await PreviewNavigationService.BringIntoViewAsync(Reader(true), End(true, secondSource));
                await Until(() => EndVisible(false, first) && EndVisible(true, second));
                facts.Add(Snapshot("comparison-deep-primary", false, first));
                facts.Add(Snapshot("comparison-deep-secondary", true, second));
                if (scenario is "findchrome" or "pendingfind" or "pendingfindclose")
                {
                    Call("ShowFind", false);
                    await Until(() => EndVisible(false, first));
                    facts.Add(Snapshot("find-open", false, first));
                    Call("HideFind");
                    if (scenario == "findchrome")
                    {
                        await Until(() => EndVisible(false, first));
                        facts.Add(Snapshot("find-closed", false, first));
                    }
                    if (scenario == "pendingfindclose") Call("CloseComparison");
                }
                if (scenario == "zoom")
                {
                    Call("PreviewZoomChanged", new Button { Tag = "in" }, new RoutedEventArgs());
                    await Until(() => EndVisible(false, first));
                    facts.Add(Snapshot("zoomed-primary", false, first));
                    Call("ResetPreviewZoom", new Button(), new RoutedEventArgs());
                    await Until(() => EndVisible(false, first));
                    facts.Add(Snapshot("zoom-restored-primary", false, first));
                }
                if (scenario == "selection")
                {
                    var firstEnd = End(false, source); preview.Selection.Select(firstEnd, firstEnd.GetPositionAtOffset(11));
                    var secondEnd = End(true, secondSource); Reader(true).Selection.Select(secondEnd, secondEnd.GetPositionAtOffset(11));
                    editor.Select(4, 5);
                }
                if (scenario is "switch" or "selection" or "findchrome" or "pendingfind")
                {
                    Call("ActivateComparison", 1, false);
                    await Until(() => EndVisible(false, second) && EndVisible(true, first));
                    facts.Add(Snapshot("switched-primary", false, second));
                    facts.Add(Snapshot("switched-secondary", true, first));
                    Call("ActivateComparison", 0, false);
                    await Until(() => EndVisible(false, first) && EndVisible(true, second));
                    if (scenario == "selection" && (preview.Selection.Text != "ReadableEnd" || Reader(true).Selection.Text != "ReadableEnd"
                        || editor.SelectionStart != 4 || editor.SelectionLength != 5))
                        throw new IOException("Pane activation lost independent preview/editor selection.");
                    facts.Add(Snapshot("returned-primary", false, first));
                }
                if (scenario == "resize")
                {
                    var splitter = (GridSplitter)Field("comparisonSplitter")!;
                    var before = preview.RenderSize;
                    for (var i = 0; i < 8; i++)
                    {
                        var key = splitter.ResizeDirection == GridResizeDirection.Rows ? Key.Down : Key.Right;
                        splitter.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                        splitter.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, key) { RoutedEvent = Keyboard.KeyDownEvent });
                        // Real key events are separated by native layout. Without
                        // that boundary all eight synthetic moves reuse one size.
                        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                    }
                    await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                    await Until(() => preview.RenderSize != before && EndVisible(false, first) && EndVisible(true, second));
                    facts.Add(Snapshot("resized-primary", false, first));
                    facts.Add(Snapshot("resized-secondary", true, second));
                }
                var attached = preview.Document;
                Call("CloseComparison");
                if (scenario == "pending")
                {
                    Call("StartComparison", first, second);
                    Call("CloseComparison");
                }
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                await Until(() => EndVisible(false, first));
                facts.Add(Snapshot("closed-comparison", false, first));
                if (preview.Document != attached || model.ActiveDocument != first || editor.Text != source || second.EditorDocument.Text != secondSource
                    || File.ReadAllText(firstPath) != source || File.ReadAllText(secondPath) != secondSource)
                    throw new IOException("Comparison closure changed source or current document ownership.");
                Program.Write(output, "control.json", new { verdict = "PASS", scenario, mode, zoom, compact, facts,
                    fixtureSha256 = Program.Hash(File.ReadAllBytes(Path.Combine(fixtures, "mixed.md"))),
                    product = typeof(MainWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                    highContrast = SystemParameters.HighContrast, osHighContrast = NativeTestHighContrast.IsEnabledNow,
                    scope = "Controlled off-screen natural WPF geometry; no foreground input or physical display acceptance" });
                code = 0;
            }
            catch (Exception error)
            {
                if (model.ActiveDocument is { } owner && Field("rendered") is PreviewResult)
                    try { facts.Add(Snapshot("failure-primary", false, owner)); } catch (Exception) { }
                Program.Write(output, "failure.json", new { error = error.ToString(), facts }); Console.Error.WriteLine(error);
            }
            finally { app.Shutdown(code); }
        });
        app.Run(); return code;
    }
}
