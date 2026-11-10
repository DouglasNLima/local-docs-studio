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
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;
using LensDocsStudio.Native.Validation;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.UxProbe;

// Exercises the real Settings apply callback and WPF/UIA without desktop input injection.
internal static class PagesTransitionControl
{
    internal static int Run(string[] args)
    {
        var fixture = Path.GetFullPath(Path.Combine(args[1], "mixed.md"));
        var output = Path.GetFullPath(args[2]);
        var highContrast = args.Contains("--expect-high-contrast");
        if (SystemParameters.HighContrast != highContrast || NativeTestHighContrast.IsEnabledNow != highContrast)
            throw new IOException("Actual Windows and WPF appearance must match the requested test.");
        if (Directory.Exists(output)) throw new IOException("Use a fresh transition evidence directory.");
        Directory.CreateDirectory(output);
        var state = Path.Combine(output, "state"); Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new UserPreferences(
            Theme: "System", Layout: "Editor", AutomaticUpdateChecks: false, ShowOutline: false,
            Preview: new(ReadingMode: "Continuous"))));
        var source = File.ReadAllText(fixture);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
        ThemeService.Apply(app.Resources, "System");
        var dialogs = new DesktopDialogs();
        using var model = new WorkspaceViewModel(new DocumentStore(Path.Combine(state, "writes")), new SessionStore(state), new WorkspaceService(), dialogs);
        var window = new MainWindow(model, dialogs, []) { Width = 1024, Height = 720, Left = 40, Top = 40,
            WindowStartupLocation = WindowStartupLocation.Manual };
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        window.Loaded -= (RoutedEventHandler)typeof(MainWindow).GetMethod("WindowLoaded", flags)!.CreateDelegate(typeof(RoutedEventHandler), window);
        window.Closing -= (CancelEventHandler)typeof(MainWindow).GetMethod("WindowClosing", flags)!.CreateDelegate(typeof(CancelEventHandler), window);
        var preview = (DocumentPreview)window.FindName("Preview");
        var editor = (TextEditor)window.FindName("Editor");
        object? Field(string name) => typeof(MainWindow).GetField(name, flags)!.GetValue(window);
        object? Call(string name, params object[] values) => typeof(MainWindow).GetMethod(name, flags)!.Invoke(window, values);
        var loading = (PreviewLoadingState)Field("previewLoading")!;
        var checks = new List<string>(); var facts = new List<object>(); var code = 1;
        void Check(bool value, string message) { if (!value) throw new IOException(message); checks.Add(message); }
        bool VisibleText() => UIElementAutomationPeer.CreatePeerForElement(preview)?.GetPattern(PatternInterface.Text) is ITextProvider provider
            && provider.GetVisibleRanges().Any(range => !string.IsNullOrWhiteSpace(range.GetText(128)) && range.GetBoundingRectangles().Length >= 4);
        async Task Settle()
        {
            var clock = Stopwatch.StartNew();
            while (clock.Elapsed < TimeSpan.FromSeconds(30))
            {
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                if (preview.IsMeasureValid && preview.IsArrangeValid && VisibleText() && !loading.Busy) return;
                await Task.Delay(8); // Existing control polling bound; never a product reflow delay.
            }
            throw new TimeoutException("Current visible content did not settle within the existing 30-second control bound.");
        }
        void Snapshot(string stage) => facts.Add(new { stage, model.Layout, mode = preview.ViewingMode.ToString(),
            model.PreviewStatus, itemStatus = AutomationProperties.GetItemStatus(preview), phase = loading.Phase.ToString(),
            loading.Generation, loading.Busy, active = model.ActiveDocument?.Id, rendered = Field("renderedDocumentId"),
            sourceGeneration = Field("diagnosticSourceGeneration"), acceptedGeneration = Field("diagnosticAcceptedGeneration"),
            sameDocument = preview.Document == ((PreviewResult)Field("rendered")!).Document,
            sourceMatches = editor.Text == model.ActiveDocument!.EditorDocument.Text,
            anchorRetained = Field("previewLayoutAnchor") is not null, restorationPending = Field("previewLayoutCancellation") is not null,
            visibleText = preview.IsVisible && VisibleText(), preview.Zoom });
        async Task SettingsMode(string mode)
        {
            // Open the actual modal Settings window; its Apply command invokes the production callback.
            var apply = window.Dispatcher.InvokeAsync(async () =>
            {
                var settings = window.OwnedWindows.OfType<SettingsWindow>().Single();
                try
                {
                    var viewModel = (SettingsViewModel)settings.DataContext;
                    viewModel.PreviewReadingMode = mode;
                    await viewModel.ApplyCommand.ExecuteAsync(null);
                    Check(viewModel.Status == "Settings saved.", "Settings applied " + mode + " through its production callback");
                }
                finally { if (settings.IsVisible) settings.Close(); }
            }, DispatcherPriority.Loaded);
            Call("OpenSettings", window, new RoutedEventArgs());
            await apply.Task.Unwrap();
        }
        window.Show();
        _ = window.Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                await model.InitialiseAsync([fixture]); Call("ApplyTheme"); Call("ApplyReadingPreferences");
                Call("BindActiveDocument"); Call("SetLayout", "Split");
                await (Task)Call("UpdatePreviewAsync", false)!; await Settle();
                var owner = model.ActiveDocument!; var document = preview.Document;
                editor.Select(4, 5); var caret = editor.CaretOffset;
                var result = (PreviewResult)Field("rendered")!;
                Check(result.TextMap.TryPreviewRange(source.IndexOf("ReadableEnd", StringComparison.Ordinal), 11, out var start, out var end), "Source selection is mapped");
                preview.Selection.Select(start!, end!);
                var selection = preview.Selection.Text; var zoom = preview.Zoom;
                // Exercise both direct return paths, including Preview followed by Split.
                foreach (var returnLayout in new[] { "Preview", "Split" })
                {
                    await SettingsMode("Continuous"); Call("SetLayout", "Split"); await Settle();
                    Call("SetLayout", "Editor");
                    Check(Field("previewLayoutAnchor") is not null, "Continuous retains a source anchor while hidden before " + returnLayout);
                    await SettingsMode("Pages");
                    Check(preview.ViewingMode == FlowDocumentReaderViewingMode.Page, "Settings selects effective Pages while hidden");
                    var generation = loading.Generation; var phase = loading.Phase;
                    var focusTarget = (TextBox)window.FindName("WorkspaceFilterField");
                    Check(focusTarget.Focus(), "Persistent workspace focus is available");
                    var focus = Keyboard.FocusedElement;
                    Call("SetLayout", returnLayout);
                    Snapshot("immediate-" + returnLayout);
                    await Settle(); Snapshot("visible-" + returnLayout);
                    Check(loading.Phase != PreviewLoadingPhase.Failed && AutomationProperties.GetItemStatus(preview) != "Preview unavailable"
                        && !model.PreviewStatus.Contains("could not", StringComparison.Ordinal), "Valid Pages content is not reported as failed in " + returnLayout);
                    Check(Field("previewLayoutAnchor") is null && Field("previewLayoutCancellation") is null,
                        "Incompatible scroll restoration is discarded before owning loading in " + returnLayout);
                    Check(loading.Generation == generation && loading.Phase == phase,
                        "Pages reuses the legitimate loading state without a new Ready or failure publication in " + returnLayout);
                    Check(preview.ViewingMode == FlowDocumentReaderViewingMode.Page && VisibleText(), "Pages content has visible UIA text in " + returnLayout);
                    Check(preview.Document == document && model.ActiveDocument == owner && (Guid)Field("renderedDocumentId")! == owner.Id
                        && (bool)typeof(MainWindow).GetProperty("SyncCurrent", flags)!.GetValue(window)!, "Current document and source version remain owned in " + returnLayout);
                    Check(editor.Text == source && editor.CaretOffset == caret && editor.SelectionStart == 4 && editor.SelectionLength == 5
                        && preview.Selection.Text == selection && preview.Zoom == zoom && ReferenceEquals(focus, Keyboard.FocusedElement),
                        "Source caret selection zoom and focus survive " + returnLayout);
                    if (returnLayout == "Preview") { Call("SetLayout", "Split"); await Settle(); Snapshot("pages-preview-to-split"); }
                    await SettingsMode("Continuous"); await Settle();
                    Call("SetLayout", "Preview"); await Settle();
                    Check(preview.ViewingMode == FlowDocumentReaderViewingMode.Scroll && VisibleText() && loading.Phase != PreviewLoadingPhase.Failed,
                        "Returning to Continuous retains usable content after " + returnLayout);
                }
                // Supersede a retained hidden anchor with a new document and rapid layout changes.
                Call("SetLayout", "Split"); await Settle(); Call("SetLayout", "Editor"); await SettingsMode("Pages");
                model.NewDocumentCommand.Execute(null);
                const string replacement = "# Current replacement\n\nCurrent source after rapid layout and document changes.\n";
                model.ActiveDocument!.EditorDocument.Text = replacement;
                foreach (var layout in new[] { "Preview", "Split", "Editor", "Preview", "Split" }) Call("SetLayout", layout);
                await (Task)Call("UpdatePreviewAsync", false)!; await Settle(); Snapshot("rapid-document-layout-replacement");
                Check(preview.ViewingMode == FlowDocumentReaderViewingMode.Page && editor.Text == replacement && owner.EditorDocument.Text == source
                    && preview.Document != document && (Guid)Field("renderedDocumentId")! == model.ActiveDocument.Id
                    && ((PreviewResult)Field("rendered")!).ReadText().Contains("Current source", StringComparison.Ordinal), "Rapid changes retain only current source and preserve outgoing content");
                Check(!loading.Busy && loading.Phase == PreviewLoadingPhase.Completed && Field("previewLayoutAnchor") is null
                    && Field("previewLayoutCancellation") is null && AutomationProperties.GetItemStatus(preview) != "Preview unavailable", "Rapid changes leave no obsolete restoration or failure");
                Check(File.ReadAllText(fixture) == source, "Original source fixture is unchanged");
                code = 0;
            }
            catch (Exception error) { facts.Add(new { error = error.ToString() }); Console.Error.WriteLine(error); }
            finally
            {
                Program.Write(output, "pages-transition.json", new { verdict = code == 0 ? "PASS" : "FAIL", checks, facts,
                    product = typeof(MainWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                    highContrast = SystemParameters.HighContrast, osHighContrast = NativeTestHighContrast.IsEnabledNow,
                    scope = "Actual shown WPF Settings apply and UIA visible text; no injected input or physical-display certification" });
                Call("CompleteClose"); app.Shutdown(code);
            }
        });
        app.Run(); return code;
    }
}
