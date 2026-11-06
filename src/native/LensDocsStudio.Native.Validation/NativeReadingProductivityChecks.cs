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
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Core.Editing;
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
            second.ExternalChange = true; Check(second.PersistenceState == "External conflict", "External conflict takes precedence over ordinary pending changes");
            model.ActiveDocument = first; editor.Document.Text = ""; AwaitSummary("0 words · 0 chars");
            Check(Convert.ToString(statistics.Content)!.StartsWith("0 min read", StringComparison.Ordinal), "Empty documents preserve zero-minute reading time");
            const string headingsSource = "# Root\n\n## Branch\n\n##### Five\n\n###### Six\n\n## Other";
            editor.Document.Text = headingsSource; model.Layout = "Editor";
            var prepare = (Task)typeof(MainWindow).GetMethod("UpdatePreviewAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [false])!;
            PumpUntil(() => prepare.IsCompleted);
            prepare.GetAwaiter().GetResult();
            var outline = (ListBox)window.FindName("Outline");
            Check(outline.Items.Count == 5, "Actual Editor-layout parsing publishes all heading levels in the existing outline");
            var toolbar = (ToolBar)window.FindName("OutlineToolbar");
            toolbar.Items.OfType<Button>().Single(button => AutomationProperties.GetName(button) == "Collapse all heading groups")
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(outline.Items.Count == 1, "The compiled Collapse All control hides descendants without modifying source");
            var filter = (TextBox)window.FindName("OutlineFilter");
            filter.Text = "Six"; Check(outline.Items.Count == 1, "Filtering includes a matching H6 inside a collapsed branch");
            filter.Text = ""; Check(outline.Items.Count == 1, "Clearing a filter preserves expansion state");
            toolbar.Items.OfType<Button>().Single(button => AutomationProperties.GetName(button) == "Expand all heading groups")
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(outline.Items.Count == 5 && editor.Text == headingsSource, "The compiled Expand All control restores the tree without editing");
            var toc = new TableOfContentsWindow(new MarkdownService().Parse(headingsSource).Headings);
            try
            {
                var generated = (TextBox)toc.FindName("PreviewText");
                Check(generated.IsReadOnly && generated.Text.Contains("- [Six](#six)", StringComparison.Ordinal), "The compiled TOC window presents real renderer targets in a read-only preview");
                ((ComboBox)toc.FindName("StartLevel")).SelectedItem = 5;
                ((CheckBox)toc.FindName("DetailsWrapper")).IsChecked = true;
                ((ComboBox)toc.FindName("ListStyle")).SelectedIndex = 1;
                Check(generated.Text.StartsWith("<details>\n<summary>Table of Contents</summary>\n\n1. [Five]", StringComparison.Ordinal)
                    && !generated.Text.Contains("Root", StringComparison.Ordinal), "Actual TOC controls apply level range, ordered lists and the details wrapper");
                ((ComboBox)toc.FindName("EndLevel")).SelectedItem = 2;
                Check(!((Button)toc.FindName("CopyButton")).IsEnabled && generated.Text.Length == 0,
                    "An invalid range disables Copy without replacing the document");
            }
            finally { toc.Close(); }
            var slash = (EditorSlashCommandService)typeof(MainWindow).GetField("slashCommands", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
            editor.Text = "/heading"; editor.CaretOffset = editor.Text.Length;
            slash.Refresh();
            Check(slash.CommandList.Items.Count == 6, "The real AvalonEdit slash controller filters all six heading commands");
            Check(UIElementAutomationPeer.CreatePeerForElement(slash.CommandList).GetPattern(PatternInterface.Selection) is not null,
                "The native slash list exposes the UI Automation Selection pattern");
            slash.CommandList.SelectedIndex = 5; slash.Accept();
            Check(editor.Text == "###### " && !slash.IsOpen, "Accepting the actual native slash selection inserts H6 and closes the popup");
            editor.Document.UndoStack.Undo(); Check(editor.Text == "/heading", "A native slash insertion is one AvalonEdit undo operation");
            editor.Document.UndoStack.Redo(); Check(editor.Text == "###### ", "Native slash redo restores the single insertion");
            editor.Text = "```md\n/"; editor.CaretOffset = editor.Text.Length; slash.Refresh();
            Check(!slash.IsOpen, "Native slash commands cannot open inside a fenced code block");
            var longSource = string.Concat(Enumerable.Repeat("Ordinary prose.\n", 65_536)) + "\n/";
            var timing = Stopwatch.StartNew(); var slashContext = SlashCommandService.Query(longSource, longSource.Length)!;
            var initialQueryMs = timing.Elapsed.TotalMilliseconds; timing.Restart();
            for (var query = 0; query < 100; query++) slashContext = SlashCommandService.Query(longSource, longSource.Length, slashContext)!;
            var cachedQueriesMs = timing.Elapsed.TotalMilliseconds;
            Check(slashContext.Start == longSource.Length - 1, "A large-buffer slash query retains the exact source range with cached prefix validation");
            editor.Text = "# First\n\nLeft buffer"; first = model.ActiveDocument!;
            model.NewDocumentCommand.Execute(null); second = model.ActiveDocument!; editor.Text = "# Second\n\nRight buffer";
            model.ActiveDocument = first;
            Invoke("StartComparison", first, second);
            var passiveEditor = (TextEditor)Field("comparisonEditor");
            Check(ReferenceEquals(passiveEditor.Document, second.EditorDocument) && ReferenceEquals(editor.Document, first.EditorDocument),
                "Comparison shares the two authoritative AvalonEdit documents without duplicating writable buffers");
            editor.Document.Insert(editor.Document.TextLength, " edited"); editor.Select(2, 3);
            var findText = (TextBox)window.FindName("FindText"); findText.Text = "Left";
            ((FrameworkElement)window.FindName("FindBar")).Visibility = Visibility.Visible;
            Invoke("ActivateComparison", 1, false);
            Check(model.ActiveDocument == second && editor.Document == second.EditorDocument && findText.Text == "",
                "Activating the right pane routes the real editor and clears the other document's Find query");
            editor.Document.Insert(editor.Document.TextLength, " changed");
            Invoke("ActivateComparison", 0, false);
            Check(editor.SelectionStart == 2 && editor.SelectionLength == 3 && findText.Text == "Left",
                "Returning to the left pane restores its exact selection and Find session");
            editor.Document.UndoStack.Undo();
            Check(editor.Text == "# First\n\nLeft buffer" && second.EditorDocument.Text.EndsWith("changed", StringComparison.Ordinal),
                "Undo in the left pane cannot alter the right document's independent history");
            Invoke("ActivateComparison", 1, false); editor.Document.UndoStack.Undo();
            Check(editor.Text == "# Second\n\nRight buffer", "The right document retains its own Undo stack after focus changes");
            editor.Document.UndoStack.Redo();
            var modes = (ComboBox[])Field("comparisonModes"); modes[0].SelectedItem = "Preview"; modes[1].SelectedItem = "Preview";
            var comparisonRender = (Task)Invoke("RenderComparisonAsync")!; PumpUntil(() => comparisonRender.IsCompleted); comparisonRender.GetAwaiter().GetResult();
            var secondaryPreview = (LensDocsStudio.Native.Rendering.DocumentPreview)Field("comparisonPreview");
            Check(new System.Windows.Documents.TextRange(secondaryPreview.Document.ContentStart, secondaryPreview.Document.ContentEnd).Text.Contains("Left buffer", StringComparison.Ordinal),
                "The actual secondary WPF preview renders its own Markdown through the canonical native services");
            Check(UIElementAutomationPeer.CreatePeerForElement(((Button[])Field("comparisonActivate"))[0]).GetPattern(PatternInterface.Invoke) is not null,
                "Comparison activation exposes a native UI Automation Invoke action");
            Invoke("CloseComparison");
            Check(model.Documents.Contains(first) && model.Documents.Contains(second) && second.EditorDocument.Text.EndsWith("changed", StringComparison.Ordinal),
                "Closing comparison retains both open documents and all unsaved changes");
            Invoke("StartComparison", second, first); Invoke("CloseComparison");
            Check(model.ActiveDocument == second, "Comparison can be reopened and closed without changing its active document");

            var workspace = Path.GetFullPath(Path.Combine(output, "workspace")); Directory.CreateDirectory(workspace);
            var leftPath = Path.Combine(workspace, "left.md"); var rightPath = Path.Combine(workspace, "right.md");
            File.WriteAllText(leftPath, "# Disk left"); File.WriteAllText(rightPath, "# Disk right");
            Complete(model.OpenPathAsync(leftPath, workspace)); var leftFile = model.ActiveDocument!;
            Complete(model.OpenPathAsync(rightPath, workspace)); var rightFile = model.ActiveDocument!;
            Invoke("StartComparison", rightFile, leftFile);
            editor.Document.Insert(editor.Document.TextLength, " edited");
            Complete(model.SaveTabAsync(rightFile, false));
            Check(File.ReadAllText(rightPath) == "# Disk right edited" && File.ReadAllText(leftPath) == "# Disk left",
                "Comparison saves the active real file without writing or replacing the secondary file");
            Invoke("ActivateComparison", 1, false); editor.Document.Insert(editor.Document.TextLength, " pending");
            File.WriteAllText(leftPath, "# External replacement");
            var conflicted = model.SaveTabAsync(leftFile, false); PumpUntil(() => conflicted.IsCompleted);
            try { conflicted.GetAwaiter().GetResult(); throw new IOException("Expected a document conflict."); }
            catch (LensDocsStudio.Core.Documents.DocumentConflictException) { }
            Check(leftFile.ExternalChange && leftFile.IsDirty && File.ReadAllText(leftPath) == "# External replacement"
                && editor.Text == "# Disk left pending", "External conflicts retain both the changed disk file and the comparison buffer");
            Complete(model.PersistSessionAsync()); Invoke("CloseComparison");
            Check(leftFile.EditorDocument.Text == "# Disk left pending" && rightFile.EditorDocument.Text == "# Disk right edited",
                "Recovery persistence and comparison closure retain each document independently");
            void Complete(Task task) { PumpUntil(() => task.IsCompleted); task.GetAwaiter().GetResult(); }

            object Field(string name) => typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
            object? Invoke(string name, params object[] arguments) => typeof(MainWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, arguments);
            File.WriteAllText(Path.Combine(output, "completion.json"), JsonSerializer.Serialize(new
            {
                status = "PASS", scope = "COMPILED_WPF_AVALONEDIT_AND_UIA_PEERS; NOT PHYSICAL_GUI_CERTIFICATION",
                source = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
                elapsedMs = watch.Elapsed.TotalMilliseconds, slashPerformance = new { sourceCharacters = longSource.Length, initialQueryMs, cached100QueriesMs = cachedQueriesMs }, checks
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
            void PumpUntil(Func<bool> ready)
            {
                var deadline = Stopwatch.StartNew();
                while (!ready() && deadline.Elapsed < TimeSpan.FromSeconds(10))
                {
                    var frame = new DispatcherFrame();
                    window.Dispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
                    Dispatcher.PushFrame(frame); Thread.Sleep(1);
                }
                if (!ready()) throw new IOException("The reading-productivity operation did not complete.");
            }
            void Check(bool value, string message) { if (!value) throw new IOException(message + ": " + statistics.Content); checks.Add(message); }
        }
        finally { typeof(MainWindow).GetMethod("CompleteClose", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null); }
    }
}
