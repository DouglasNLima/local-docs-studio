using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Editor;

namespace LensDocsStudio.Native.Validation;

internal static class NativePerformanceChecks
{
    public static void Run(string output)
    {
        var root = Path.Combine(output, "performance-" + Guid.NewGuid().ToString("N"));
        var workspaceRoot = Path.Combine(root, "workspace"); Directory.CreateDirectory(workspaceRoot);
        // Fixture creation is deliberately outside the measured filesystem operations.
        for (var folder = 0; folder < 20; folder++)
        {
            var directory = Path.Combine(workspaceRoot, "Section-" + folder); Directory.CreateDirectory(directory);
            for (var file = 0; file < 50; file++) File.WriteAllText(Path.Combine(directory, "Document-" + file + ".md"), "# Portable document\n\nReview this content.\n");
        }
        var source = "# Large editing fixture\n\n" + string.Concat(Enumerable.Range(0, 20000).Select(index =>
            $"Paragraph {index:D5}: **portable Markdown** retains Unicode Café and [relative links](Section-0/Document-0.md). "
            + "The current source must survive native insertion, layout and Undo without an implicit file write.\n\n"));
        var path = Path.Combine(workspaceRoot, "Large.md"); File.WriteAllText(path, source, new UTF8Encoding(false));
        var bytes = new FileInfo(path).Length;
        var samples = new List<object>();
        using var process = Process.GetCurrentProcess();
        var baseline = Memory();
        var service = new WorkspaceService();
        var store = new DocumentStore(Path.Combine(root, "recovery"));
        var parser = new MarkdownService();
        for (var iteration = 0; iteration < 3; iteration++)
        {
            var watch = Stopwatch.StartNew();
            var listing = service.EnumerateAsync(new WorkspacePath(workspaceRoot)).GetAwaiter().GetResult();
            var enumerateMilliseconds = watch.Elapsed.TotalMilliseconds;
            if (listing.Warnings.Count != 0 || CountDocuments(listing.Entries) != 1001) throw new IOException("The performance workspace was not enumerated completely.");
            watch.Restart(); var snapshot = store.ReadAsync(path).GetAwaiter().GetResult(); var readMilliseconds = watch.Elapsed.TotalMilliseconds;
            if (snapshot.Text != source || snapshot.Utf8Bom) throw new IOException("Opening the large UTF-8 fixture changed source bytes.");
            watch.Restart(); var parsed = parser.Parse(snapshot.Text); var parseMilliseconds = watch.Elapsed.TotalMilliseconds;
            if (parsed.Syntax.Count != 20001 || parsed.Headings.Count != 1) throw new IOException("The large fixture lost parsed paragraphs or its heading.");
            samples.Add(new { Iteration = iteration + 1, EnumerationMilliseconds = enumerateMilliseconds, ReadMilliseconds = readMilliseconds,
                ParseMilliseconds = parseMilliseconds, Memory = Memory() });
        }
        var editor = new TextEditor { FontFamily = new FontFamily("Consolas"), FontSize = 14, WordWrap = true, ShowLineNumbers = true,
            Background = Brushes.White, Foreground = Brushes.Black };
        editor.TextArea.TextView.LineTransformers.Add(new MarkdownHighlighting());
        using var host = new HwndSource(new HwndSourceParameters("Large editor performance validation")
            { Width = 800, Height = 480, WindowStyle = unchecked((int)0x80000000) });
        var editorWatch = Stopwatch.StartNew(); editor.Text = source; editor.Document.UndoStack.ClearAll(); host.RootVisual = editor; Layout();
        var initialLayoutMilliseconds = editorWatch.Elapsed.TotalMilliseconds;
        var edits = new List<object>();
        foreach (var position in new[] { 0, source.Length / 2, source.Length })
        {
            editor.CaretOffset = position;
            var navigation = EditorNavigationService.BringIntoViewAsync(editor, position);
            Complete(navigation);
            if (!navigation.GetAwaiter().GetResult()) throw new IOException("Native editor navigation did not reach the measured target.");
            Layout();
            var view = editor.TextArea.TextView;
            var target = view.GetVisualPosition(new TextViewPosition(editor.Document.GetLocation(position)), VisualYPosition.LineMiddle) - view.ScrollOffset;
            if (target.Y < 0 || target.Y > view.ActualHeight) throw new IOException("The measured large-document edit target is outside the native viewport: "
                + JsonSerializer.Serialize(new { position, target.Y, view.ActualHeight, view.ScrollOffset }));
            for (var repeat = 0; repeat < 5; repeat++)
            {
                editorWatch.Restart(); editor.Document.Insert(position, "é"); Layout(); var insertion = editorWatch.Elapsed.TotalMilliseconds;
                editorWatch.Restart(); editor.Document.UndoStack.Undo(); Layout(); var undo = editorWatch.Elapsed.TotalMilliseconds;
                if (editor.Document.TextLength != source.Length) throw new IOException("Large-document Undo lost its original text length.");
                edits.Add(new { Offset = position, Iteration = repeat + 1, TargetViewportY = target.Y, view.ActualHeight,
                    InsertAndLayoutMilliseconds = insertion, UndoAndLayoutMilliseconds = undo });
            }
        }
        if (editor.Text != source || File.ReadAllText(path) != source) throw new IOException("Large-document editing changed original source or its saved file.");
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(800, 480, 96, 96, PixelFormats.Pbgra32); bitmap.Render(editor);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using (var image = File.Create(Path.Combine(output, "native-large-editor.png"))) encoder.Save(image);
        var stale = EditorNavigationService.BringIntoViewAsync(editor, 0);
        editor.Document.Insert(0, "changed"); Complete(stale);
        if (stale.GetAwaiter().GetResult()) throw new IOException("Editor navigation accepted changed source.");
        editor.Document.UndoStack.Undo();
        var superseded = EditorNavigationService.BringIntoViewAsync(editor, 0);
        var latest = EditorNavigationService.BringIntoViewAsync(editor, source.Length);
        Complete(Task.WhenAll(superseded, latest));
        if (superseded.GetAwaiter().GetResult() || !latest.GetAwaiter().GetResult()) throw new IOException("Superseded editor navigation displaced the latest request.");
        var movedSelection = EditorNavigationService.BringIntoViewAsync(editor, source.Length); editor.Select(0, 8); Complete(movedSelection);
        if (movedSelection.GetAwaiter().GetResult() || editor.SelectionStart != 0 || editor.SelectionLength != 8 || editor.Text != source)
            throw new IOException("Editor navigation affected a subsequently changed selection.");
        var replaced = EditorNavigationService.BringIntoViewAsync(editor, 0); editor.Document = new("Replacement document"); Complete(replaced);
        if (replaced.GetAwaiter().GetResult() || editor.Text != "Replacement document") throw new IOException("Editor navigation affected a replacement document.");
        var finalMemory = Memory(); host.RootVisual = null; editor.Document = new();
        File.WriteAllText(Path.Combine(output, "native-performance.json"), JsonSerializer.Serialize(new {
            Passed = true, Runtime = Environment.Version.ToString(), Environment.ProcessorCount, Is64BitProcess = Environment.Is64BitProcess,
            WorkspaceDocuments = 1001, SourceBytes = bytes, SourceCharacters = source.Length, Paragraphs = 20000,
            BaselineMemory = baseline, FilesystemAndParsing = samples, EditorInitialLayoutMilliseconds = initialLayoutMilliseconds,
            ProgrammaticEdits = edits, FinalMemory = finalMemory,
            StaleSourceAndDocumentRejected = true, SupersededNavigationRejected = true, ChangedSelectionPreserved = true,
            Limitations = "Single-process fixture observations with warm operating-system caches after setup; no forced garbage collection. Memory includes the validation host and earlier checks. Programmatic native edits include visible layout, not OS keyboard delivery or application preview scheduling. No machine-independent latency threshold is claimed."
        }, new JsonSerializerOptions { WriteIndented = true }));
        void Layout()
        {
            editor.Measure(new Size(800, 480)); editor.Arrange(new Rect(0, 0, 800, 480)); editor.UpdateLayout();
            var frame = new System.Windows.Threading.DispatcherFrame();
            editor.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () => frame.Continue = false);
            System.Windows.Threading.Dispatcher.PushFrame(frame); editor.UpdateLayout();
            editor.TextArea.TextView.EnsureVisualLines();
        }
        void Complete(Task task)
        {
            var watch = Stopwatch.StartNew();
            while (!task.IsCompleted)
            {
                if (watch.Elapsed > TimeSpan.FromSeconds(10)) throw new TimeoutException("Native editor navigation exceeded its validation deadline.");
                Layout();
            }
            task.GetAwaiter().GetResult();
        }
        object Memory()
        {
            process.Refresh();
            return new { WorkingSetBytes = process.WorkingSet64, PrivateBytes = process.PrivateMemorySize64,
                ManagedHeapBytes = GC.GetTotalMemory(false), TotalManagedAllocatedBytes = GC.GetTotalAllocatedBytes() };
        }
        static int CountDocuments(IEnumerable<WorkspaceEntry> entries) => entries.Sum(entry => entry.IsDirectory ? CountDocuments(entry.Children) : 1);
    }
}
