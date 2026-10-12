using System.Reflection;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.Validation;

internal static class NativePreviewSchedulingChecks
{
    public static void Run(string output)
    {
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var root = Path.Combine(output, "preview-scheduling-" + Guid.NewGuid().ToString("N"));
        var dialogs = new DesktopDialogs();
        var model = new WorkspaceViewModel(new DocumentStore(Path.Combine(root, "writes")), new SessionStore(root), new WorkspaceService(), dialogs);
        MainWindow? window = null;
        try
        {
            model.NewDocumentCommand.Execute(null);
            window = new MainWindow(model, dialogs, []);
            Invoke("BindActiveDocument"); Invoke("SetLayout", "Editor");
            Pump(Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background).Task);
            var tab = model.ActiveDocument!;
            var editor = (TextEditor)window.FindName("Editor");
            var outline = (ListBox)window.FindName("Outline");
            var preview = (LensDocsStudio.Native.Rendering.DocumentPreview)window.FindName("Preview");
            var source = "# Current heading\n\nBefore.\n\n## Target heading\n\n<details>\n<summary>Evidence</summary>\n\nHidden current text.\n</details>";
            tab.EditorDocument.Text = source;
            Pump((Task)Invoke("UpdatePreviewAsync", false)!);
            if (outline.Items.Count != 2 || !model.PreviewStatus.Contains("paused", StringComparison.Ordinal)
                || preview.Document.Blocks.Count != 0 || editor.Text != source)
                throw new IOException("Editor layout did not retain its current outline while pausing native preview work: "
                    + JsonSerializer.Serialize(new { Headings = outline.Items.Count, model.PreviewStatus, Blocks = preview.Document?.Blocks.Count, SourceMatches = editor.Text == source }));
            outline.SelectedIndex = 1;
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

            File.WriteAllText(Path.Combine(output, "native-preview-scheduling.json"), JsonSerializer.Serialize(new { Passed = true,
                Checks = new[] { "Editor-only layout retains current headings and source navigation without a preview",
                    "Explicit text preparation includes current closed details and subsequent edits without changing layout or Undo",
                    "Showing preview renders current source and Find follows the visible layout",
                    "Intervening edits cancel text preparation without yielding an obsolete copy result" },
                Limitation = "Unshown native controls; foreground keyboard and clipboard delivery are covered separately in Windows CI." }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            if (window is not null) Invoke("CompleteClose"); else model.Dispose();
            SynchronizationContext.SetSynchronizationContext(previous);
        }
        object? Invoke(string name, params object[] arguments) => typeof(MainWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, arguments);
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
