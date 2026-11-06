using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.Validation;

// Real compiled MainWindow, AvalonEdit buffers and automation peers. This is not
// physical input, OS High Contrast, clean-machine or screen-reader certification.
internal static class NativeReadingProductivityChecks
{
    public static void Run(string output)
    {
        if (Directory.Exists(output)) throw new IOException("Fresh reading-productivity evidence required.");
        Directory.CreateDirectory(output);
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        ThemeService.Apply(app.Resources, "Light");
        var state = Path.Combine(output, "state");
        var dialogs = new DesktopDialogs();
        using var model = new WorkspaceViewModel(new DocumentStore(Path.Combine(state, "writes")), new SessionStore(state), new WorkspaceService(), dialogs);
        var window = new MainWindow(model, dialogs, []);
        var checks = new List<string>();
        var watch = Stopwatch.StartNew();
        try
        {
            var editor = (TextEditor)window.FindName("Editor");
            var statistics = (Button)window.FindName("DocumentStatisticsButton");
            model.NewDocumentCommand.Execute(null);
            var first = model.ActiveDocument!;
            editor.Document.Text = "# Café 😀\n\nHello **world**!";
            editor.Select(5, 2);
            AwaitSummary("3 words · 19 chars");
            Check(editor.SelectionStart == 5 && editor.SelectionLength == 2, "Statistics retain exact AvalonEdit selection without preview rendering");
            Check(AutomationProperties.GetName(statistics).Contains("3 words", StringComparison.Ordinal)
                && UIElementAutomationPeer.CreatePeerForElement(statistics).GetPattern(PatternInterface.Invoke) is not null,
                "The real statistics button exposes its metrics and Invoke pattern");
            Check(first.PersistenceState == "Unsaved changes", "An edited draft is never labelled Saved");
            editor.Document.Insert(editor.Document.TextLength, " Again");
            AwaitSummary("4 words · 25 chars");
            editor.Document.UndoStack.Undo(); AwaitSummary("3 words · 19 chars");
            editor.Document.UndoStack.Redo(); AwaitSummary("4 words · 25 chars");
            Check(editor.Document.Text.EndsWith("Again", StringComparison.Ordinal), "Metrics follow independent AvalonEdit undo and redo without modifying history");
            editor.Document.Text = string.Concat(Enumerable.Repeat("large ", 100_000));
            model.NewDocumentCommand.Execute(null);
            editor.Document.Text = "# Current";
            AwaitSummary("1 words · 7 chars");
            Check(model.ActiveDocument != first && first.EditorDocument.TextLength == 600_000,
                "A document switch discards obsolete metrics and preserves the outgoing buffer");
            var second = model.ActiveDocument!;
            second.IsSaving = true; Check(second.PersistenceState == "Saving…", "Persistence status follows an actual model save operation");
            second.IsSaving = false; second.SaveFailed = true; Check(second.PersistenceState == "Save failed", "Save failure remains explicit");
            second.ExternalChange = true; Check(second.PersistenceState == "External change", "External conflict takes precedence over ordinary pending changes");
            model.ActiveDocument = first; editor.Document.Text = ""; AwaitSummary("0 words · 0 chars");
            Check(Convert.ToString(statistics.Content)!.StartsWith("0 min read", StringComparison.Ordinal), "Empty documents preserve zero-minute reading time");
            File.WriteAllText(Path.Combine(output, "completion.json"), JsonSerializer.Serialize(new
            {
                status = "PASS", scope = "COMPILED_WPF_AVALONEDIT_AND_UIA_PEERS; NOT PHYSICAL_GUI_CERTIFICATION",
                source = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
                elapsedMs = watch.Elapsed.TotalMilliseconds, checks
            }, new JsonSerializerOptions { WriteIndented = true }));

            void AwaitSummary(string expected)
            {
                // Stop only the preview scheduler: the actual independent metrics timer must run.
                ((DispatcherTimer)typeof(MainWindow).GetField("previewTimer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!).Stop();
                var deadline = Stopwatch.StartNew();
                while (!Convert.ToString(statistics.Content)!.Contains(expected, StringComparison.Ordinal) && deadline.Elapsed < TimeSpan.FromSeconds(10))
                {
                    var frame = new DispatcherFrame();
                    window.Dispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
                    Dispatcher.PushFrame(frame); Thread.Sleep(1);
                }
                Check(Convert.ToString(statistics.Content)!.Contains(expected, StringComparison.Ordinal), "Current-buffer metrics: " + expected);
            }
            void Check(bool value, string message) { if (!value) throw new IOException(message + ": " + statistics.Content); checks.Add(message); }
        }
        finally { typeof(MainWindow).GetMethod("CompleteClose", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null); }
    }
}
