using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using LensDocsStudio.Core.Review;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Validation;
using LensDocsStudio.Native.ViewModels;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var output = Path.GetFullPath(args[0]);
        if (Directory.Exists(output)) throw new IOException("Use a fresh statistics evidence directory.");
        Directory.CreateDirectory(output);
        var highContrast = args.Contains("--expect-high-contrast");
        if (SystemParameters.HighContrast != highContrast || NativeTestHighContrast.IsEnabledNow != highContrast)
            throw new IOException("Actual Windows and WPF High Contrast must match the requested appearance.");
        var theme = args.Contains("--dark") ? "Dark" : "Light";
        var state = Path.Combine(output, "state"); Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new UserPreferences(
            Theme: theme, Layout: "Editor", AutomaticUpdateChecks: false, ShowOutline: false)));
        var fixture = Path.Combine(output, "source.md");
        var source = "# Café 😀\r\n\r\nHello **world**!\r\n\r\n![Alt](unrelated.png)\r\n";
        var original = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(source)).ToArray();
        File.WriteAllBytes(fixture, original);
        // A locked linked file ensures statistics do not try to read image bytes.
        using var lockedAsset = new FileStream(Path.Combine(output, "unrelated.png"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        foreach (var resource in new[] { "CommandIcons", "DesktopStyles", "FormStyles" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"pack://application:,,,/LensDocsStudio.Native;component/Resources/{resource}.xaml") });
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
        ThemeService.Apply(app.Resources, theme);
        var dialogs = new DesktopDialogs();
        using var model = new WorkspaceViewModel(new DocumentStore(Path.Combine(state, "writes")), new SessionStore(state), new WorkspaceService(), dialogs);
        var window = new MainWindow(model, dialogs, []) { Width = 1100, Height = 720, Left = 30, Top = 30 };
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        window.Loaded -= (RoutedEventHandler)typeof(MainWindow).GetMethod("WindowLoaded", flags)!.CreateDelegate(typeof(RoutedEventHandler), window);
        window.Closing -= (CancelEventHandler)typeof(MainWindow).GetMethod("WindowClosing", flags)!.CreateDelegate(typeof(CancelEventHandler), window);
        object? Field(string name) => typeof(MainWindow).GetField(name, flags)!.GetValue(window);
        object? Call(string name, params object[] values) => typeof(MainWindow).GetMethod(name, flags)!.Invoke(window, values);
        var button = (Button)window.FindName("DocumentStatisticsButton");
        var editor = (TextEditor)window.FindName("Editor");
        var gate = (SemaphoreSlim)Field("statisticsGate")!;
        var timer = (DispatcherTimer)Field("statisticsTimer")!;
        var checks = new List<string>(); var code = 1; var closed = false;
        void Check(bool value, string label) { if (!value) throw new IOException(label); checks.Add(label); File.AppendAllText(Path.Combine(output, "checks.txt"), label + "\n"); }
        async Task WaitFor(Func<bool> ready)
        {
            var clock = Stopwatch.StartNew();
            while (!ready())
            {
                if (clock.Elapsed > TimeSpan.FromSeconds(30)) throw new TimeoutException("Statistics did not settle within the control bound.");
                await Task.Delay(10);
            }
        }
        Task Measure() => (Task)Call("UpdateStatisticsAsync")!;
        string Label() => button.Content?.ToString() ?? "";
        void Metrics(int words, int characters) => Check(Label() == new DocumentStatistics(words, characters).Summary,
            $"Current statistics: {words} words, {characters} characters");
        void Capture(string name)
        {
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, name + ".png")); png.Save(stream);
        }
        window.Show();
        _ = window.Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                Check(Label() == "No document" && !button.IsEnabled && AutomationProperties.GetName(button) == "No document", "No-document state is explicit and disabled");
                await model.InitialiseAsync([fixture]); Call("ApplyTheme"); Call("SetLayout", "Editor");
                await WaitFor(() => Label() != "Counting…"); Metrics(4, 23);
                var first = model.ActiveDocument!;
                Check(editor.Text == source && !first.IsDirty, "Opening and statistics preserve loaded source and clean state");
                var peer = UIElementAutomationPeer.CreatePeerForElement(button)!;
                Check(peer.GetAutomationControlType() == AutomationControlType.Button && peer.IsKeyboardFocusable()
                    && peer.GetName().Contains("4 words") && peer.GetHelpText().Contains("220 words"), "UIA exposes metrics, help and a keyboard-focusable button");
                Check(button.Focus() && button.IsKeyboardFocused, "Statistics accepts keyboard focus");
                Check(button.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous)), "Keyboard can leave statistics");
                Check(((UIElement)Keyboard.FocusedElement).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)) && button.IsKeyboardFocused,
                    "Tab traversal returns to statistics");
                var reviewOpened = false;
                async Task ObserveReview()
                {
                    await WaitFor(() => window.OwnedWindows.OfType<DocumentReviewWindow>().Any());
                    var review = window.OwnedWindows.OfType<DocumentReviewWindow>().Single();
                    reviewOpened = true;
                    Check(((TextBlock)review.FindName("Summary")).Text.Contains("4 words · 23 characters"), "Document Review agrees with status metrics");
                    review.Close();
                }
                var closeReview = ObserveReview();
                // Exercise the native Button keyboard activation route, not the review handler directly.
                button.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent });
                await WaitFor(() => reviewOpened);
                await closeReview;
                for (var i = 0; i < 20; i++) editor.Document.Text = "rapid " + i;
                Check(Label() == "Counting…", "Rapid edits immediately clear superseded metrics");
                await WaitFor(() => Label() != "Counting…"); Metrics(2, 8);
                editor.Document.Text = "one"; await Measure(); Metrics(1, 3);
                editor.Document.Text = "two more words"; await Measure(); Metrics(3, 14);
                editor.Document.UndoStack.Undo(); await WaitFor(() => Label() != "Counting…"); Metrics(1, 3);
                Check(editor.Text == "one", "Undo restores the preceding source and different metrics");
                editor.Document.UndoStack.Redo(); await WaitFor(() => Label() != "Counting…"); Metrics(3, 14);
                Check(editor.Text == "two more words", "Redo restores the latest source and different metrics");
                model.NewDocumentCommand.Execute(null);
                Check(Label() == "Counting…", "Switching to an empty document clears outgoing figures");
                await WaitFor(() => Label() != "Counting…"); Metrics(0, 0);
                var empty = model.ActiveDocument!;
                model.ActiveDocument = first; await Measure(); Metrics(3, 14);
                await gate.WaitAsync();
                var old = Measure();
                model.ActiveDocument = empty;
                var current = Measure();
                Check(!current.IsCompleted, "Only one calculation can hold the worker gate");
                gate.Release(); await Task.WhenAll(old, current); Metrics(0, 0);
                Check(old.IsCompletedSuccessfully, "Cancelled queued calculations finish without publishing or surfacing errors");
                // This detached tab deliberately has no model edit subscription: version ownership alone must reject its result.
                var detached = new DocumentViewModel("obsolete words"); model.ActiveDocument = detached;
                await gate.WaitAsync(); var replaced = Measure(); detached.EditorDocument.Text = "replacement";
                gate.Release(); await replaced;
                Check(Label() == "Counting…", "Changed source version rejects results even without a model notification");
                Call("QueueStatistics"); await Measure(); Metrics(1, 11);
                model.ActiveDocument = first;
                editor.Document.Text = "# Large\n\n" + string.Concat(Enumerable.Repeat(string.Concat(Enumerable.Repeat("word ", 99)) + "word\n", 1000)) + "final";
                await gate.WaitAsync(); var large = Measure();
                var pulse = false; await window.Dispatcher.InvokeAsync(() => pulse = true, DispatcherPriority.Input);
                Check(pulse && !large.IsCompleted, "Dispatcher input is served while large analysis waits for its bounded worker");
                gate.Release(); await large; Metrics(100_002, 500_011);
                Check(editor.Text.EndsWith("final", StringComparison.Ordinal), "Large analysis preserves the final source");
                editor.Document.Text = string.Concat(Enumerable.Repeat(new string('x', 1023) + "\n", 4096)) + "x";
                await Measure(); Check(Label() == "Statistics unavailable", "Oversized source has an explicit unavailable state");
                editor.Document.Text = source; await Measure(); Metrics(4, 23);
                Check(File.ReadAllBytes(fixture).SequenceEqual(original), "Source bytes and UTF-8 BOM remain unchanged");
                first.EditorDocument.UndoStack.ClearAll(); first.MarkSaved(first.SavedSnapshot!);
                await model.ReloadCommand.ExecuteAsync(null); await Measure(); Metrics(4, 23);
                await model.CloseDocumentCommand.ExecuteAsync(first); await Measure(); Metrics(0, 0);
                Check(model.ActiveDocument == empty, "Closing selects and measures the remaining document");
                await model.OpenPathAsync(fixture); await Measure(); Metrics(4, 23);
                foreach (var width in new[] { 860d, 1440d })
                {
                    window.Width = width; await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    var point = button.TransformToAncestor(window).Transform(new Point());
                    Check(button.ActualWidth > 80 && point.X >= 0 && point.X + button.ActualWidth <= window.ActualWidth,
                        "Statistics remains inside the status bar at width " + width);
                    Check(peer.GetName().Contains("23 chars"), "Full accessible metrics survive visual truncation");
                    Capture("status-" + width);
                }
                window.Width = 860;
                var exported = Path.Combine(output, "example.html");
                var exportBytes = Encoding.UTF8.GetBytes("<!doctype html><p>Local test export</p>");
                File.WriteAllBytes(exported, exportBytes);
                await model.CompleteExportAsync(exported, exportBytes);
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                foreach (var control in Descendants(window).OfType<FrameworkElement>().Where(element =>
                    AutomationProperties.GetAutomationId(element) is "OpenExportedFile" or "RevealExportedFile" or "DocumentStatistics" or "CaretStatus"))
                {
                    var point = control.TransformToAncestor(window).Transform(new Point());
                    Check(control.IsVisible && control.ActualWidth > 25 && point.X >= 0 && point.X + control.ActualWidth <= window.ActualWidth,
                        "Export completion retains visible status control " + AutomationProperties.GetAutomationId(control));
                }
                Capture("status-export-860");
                model.IsBusy = true;
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                var cancel = Descendants(window).OfType<Button>().Single(element => AutomationProperties.GetAutomationId(element) == "CancelOperation");
                Check(cancel.IsVisible && cancel.IsEnabled && cancel.TransformToAncestor(window).Transform(new Point()).X + cancel.ActualWidth <= window.ActualWidth,
                    "Statistics retains the visible enabled cancellation control at minimum width");
                model.IsBusy = false;
                Check(((SolidColorBrush)button.Foreground).Color == ((SolidColorBrush)window.FindResource("TextBrush")).Color,
                    "Statistics uses the current native theme text brush");
                Check(!highContrast || SystemParameters.HighContrast && NativeTestHighContrast.IsEnabledNow,
                    highContrast ? "Actual Windows High Contrast remains active throughout presentation checks" : "Normal Windows appearance remains active throughout presentation checks");
                model.ActiveDocument = null; await Measure();
                Check(Label() == "No document" && !button.IsEnabled, "Removing the active document clears all metrics");
                model.ActiveDocument = empty;
                await gate.WaitAsync(); var pendingClose = Measure(); var beforeClose = Label();
                window.Close(); closed = true; gate.Release(); await pendingClose;
                Check(Label() == beforeClose && !timer.IsEnabled, "Window closure cancels work and prevents late UI publication");
                Check(File.ReadAllBytes(fixture).SequenceEqual(original), "All edit, switch, review and close controls preserve saved bytes");
                code = 0;
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(output, "failure.txt"), error.ToString()); Console.Error.WriteLine(error); }
            finally
            {
                File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { result = code == 0 ? "PASS" : "FAIL", checks,
                    theme, actualHighContrast = highContrast, scope = "Shown owned WPF window, UIA peers and routed keyboard events; no physical screen-reader or injected desktop-input certification" }, new JsonSerializerOptions { WriteIndented = true }));
                if (!closed) window.Close(); app.Shutdown(code);
            }
        });
        app.Run(); return code;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
