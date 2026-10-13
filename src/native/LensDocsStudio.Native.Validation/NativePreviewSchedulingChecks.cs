using System.Reflection;
using System.Diagnostics.Tracing;
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
        Directory.CreateDirectory(root);
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
            tab.EditorDocument.UndoStack.Undo();
            if (editor.Text != large) throw new IOException("Rendering changed large-document Undo or Unicode source.");
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

            File.WriteAllText(Path.Combine(output, "native-preview-scheduling.json"), JsonSerializer.Serialize(new { Passed = true,
                Checks = new[] { "Editor-only layout retains current headings and source navigation without a preview",
                    "Explicit text preparation includes current closed details and subsequent edits without changing layout or Undo",
                    "Showing preview renders current source and Find follows the visible layout",
                    "Intervening edits cancel text preparation without yielding an obsolete copy result",
                    "Editing during native construction preserves current source, preview and Undo",
                    "Rapid tab switching cannot commit obsolete or mixed preview content",
                    "Closing a tab during native construction leaves its source intact and renders the surviving tab" },
                Limitation = "Unshown native controls; foreground keyboard and clipboard delivery are covered separately in Windows CI." }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            if (window is not null) Invoke("CompleteClose"); else model.Dispose();
            SynchronizationContext.SetSynchronizationContext(previous);
        }
        object? Invoke(string name, params object[] arguments) => typeof(MainWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, arguments);
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
