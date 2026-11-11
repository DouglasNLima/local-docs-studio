using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using LensDocsStudio.Core.Editing;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Validation;
using LensDocsStudio.Native.ViewModels;

internal static class Program
{
    [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")] private static extern IntPtr GetClipboardOwner();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr owner);
    [DllImport("user32.dll")] private static extern bool CloseClipboard();
    [STAThread]
    private static int Main(string[] args)
    {
        var output = Path.GetFullPath(args[0]);
        if (Directory.Exists(output)) throw new IOException("Use a fresh outline evidence directory.");
        Directory.CreateDirectory(output);
        var highContrast = args.Contains("--expect-high-contrast");
        if (SystemParameters.HighContrast != highContrast || NativeTestHighContrast.IsEnabledNow != highContrast)
            throw new IOException("Actual Windows and WPF High Contrast must match the requested appearance.");
        var theme = args.Contains("--dark") ? "Dark" : "Light";
        var state = Path.Combine(output, "state"); Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new UserPreferences(
            Theme: theme, Layout: "Editor", AutomaticUpdateChecks: false, ShowOutline: true)));
        const string source = "# Root\r\n\r\n## Branch\r\n\r\n### Leaf\r\n\r\n#### Four\r\n\r\n##### Five\r\n\r\n###### Six café\r\n\r\n## Branch\r\n\r\n### Duplicate child\r\n\r\n# Last\r\n";
        var fixture = Path.Combine(output, "source.md");
        var original = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(source)).ToArray(); File.WriteAllBytes(fixture, original);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        foreach (var resource in new[] { "CommandIcons", "DesktopStyles", "FormStyles" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"pack://application:,,,/LensDocsStudio.Native;component/Resources/{resource}.xaml") });
        var bootstrapResources = app.Resources.MergedDictionaries.Select(resource => resource.Source.ToString()).ToArray();
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
        ThemeService.Apply(app.Resources, theme);
        var dialogs = new DesktopDialogs();
        using var model = new WorkspaceViewModel(new DocumentStore(Path.Combine(state, "writes")), new SessionStore(state), new WorkspaceService(), dialogs);
        var window = new MainWindow(model, dialogs, []) { Width = 1100, Height = 720, Left = 30, Top = 30 };
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        window.Loaded -= (RoutedEventHandler)typeof(MainWindow).GetMethod("WindowLoaded", flags)!.CreateDelegate(typeof(RoutedEventHandler), window);
        window.Closing -= (CancelEventHandler)typeof(MainWindow).GetMethod("WindowClosing", flags)!.CreateDelegate(typeof(CancelEventHandler), window);
        object? Call(string name, params object[] values) => typeof(MainWindow).GetMethod(name, flags)!.Invoke(window, values);
        var outline = (TreeView)window.FindName("Outline"); var filter = (TextBox)window.FindName("OutlineFilter");
        var editor = (TextEditor)window.FindName("Editor"); var tocButton = (Button)window.FindName("CopyTocButton");
        var checks = new List<string>(); var code = 1;
        DataObject? savedClipboard = null; uint ownedClipboard = 0;
        var observedWindows = new Dictionary<Window, object>(); long ownershipObservationTicks = 0;
        void Check(bool value, string label)
        {
            var started = Stopwatch.GetTimestamp();
            var windows = app.Windows.Cast<Window>().ToArray();
            if (!ReferenceEquals(Application.Current, app) || app.GetType() != typeof(Application) || windows.Count(item => ReferenceEquals(item, window)) != 1
                || windows.Any(item => !ReferenceEquals(item, window) && (item is not TableOfContentsWindow || !ReferenceEquals(item.Owner, window))))
                throw new IOException("The Outline control has an unexpected application or window owner.");
            foreach (var item in windows) observedWindows.TryAdd(item, new
            { Type = item.GetType().FullName, MainWindow = ReferenceEquals(item, window), OwnedByMainWindow = ReferenceEquals(item.Owner, window) });
            ownershipObservationTicks += Stopwatch.GetTimestamp() - started;
            if (!value) throw new IOException(label);
            checks.Add(label); File.AppendAllText(Path.Combine(output, "checks.txt"), label + "\n");
        }
        async Task WaitFor(Func<bool> ready)
        {
            var clock = Stopwatch.StartNew();
            while (!ready()) { if (clock.Elapsed > TimeSpan.FromSeconds(25)) throw new TimeoutException("Outline control did not settle."); await Task.Delay(10); }
        }
        Task Drain() => window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Task;
        async Task Parse() { await (Task)Call("UpdatePreviewAsync", false)!; await Drain(); }
        IEnumerable<OutlineHeadingViewModel> All(IEnumerable<OutlineHeadingViewModel> nodes)
        { foreach (var node in nodes) { yield return node; foreach (var child in All(node.Children)) yield return child; } }
        OutlineHeadingViewModel[] Nodes() => All(outline.Items.Cast<OutlineHeadingViewModel>()).ToArray();
        TreeViewItem Row(OutlineHeadingViewModel node)
        {
            ItemsControl owner = node.Parent is null ? outline : Row(node.Parent);
            owner.UpdateLayout();
            return (TreeViewItem)(owner.ItemContainerGenerator.ContainerFromItem(node) ?? throw new IOException("Missing heading container: " + node.Title));
        }
        void SendKey(UIElement target, Key key) => target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, key) { RoutedEvent = Keyboard.KeyDownEvent });
        void Group(bool expand) => Call(expand ? "ExpandOutlineGroups" : "CollapseOutlineGroups", window, new RoutedEventArgs());
        void Capture(string name, Window target)
        {
            var bitmap = new RenderTargetBitmap((int)target.ActualWidth, (int)target.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(target);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(Path.Combine(output, name + ".png")); png.Save(stream);
        }
        window.Show();
        _ = window.Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                if (args.Contains("--dismissal"))
                {
                    var modal = new TableOfContentsWindow(new ICSharpCode.AvalonEdit.Document.TextDocument(source).CreateSnapshot(), false, () => true) { Owner = window };
                    async Task ObserveDismissal()
                    {
                        await WaitFor(() => modal.IsVisible && ((Button)modal.FindName("CopyButton")).IsEnabled);
                        try
                        {
                            var start = (ComboBox)modal.FindName("StartLevel");
                            Check(start.IsKeyboardFocused && start.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)), "Dialog supports Tab traversal from its initial option");
                            Check(((ComboBox)modal.FindName("EndLevel")).IsKeyboardFocused, "Tab reaches the next labelled heading option");
                            var copy = (Button)modal.FindName("CopyButton"); Check(copy.Focus(), "Dialog Copy action is keyboard reachable");
                            // IsCancel registers the real WPF Escape access key in this modal scope.
                            AccessKeyManager.ProcessKey(PresentationSource.FromVisual(modal), "\u001b", false);
                            await WaitFor(() => !modal.IsVisible);
                            Check(!modal.IsVisible, "WPF Escape dismisses the native modal dialog");
                        }
                        finally { if (modal.IsVisible) modal.Close(); }
                    }
                    var dismissal = ObserveDismissal(); modal.ShowDialog(); await dismissal;
                    code = 0; return;
                }
                Check(!tocButton.IsEnabled && ((TextBlock)window.FindName("OutlineEmpty")).Text.Contains("Open a document"), "No-document actions and message are explicit");
                await model.InitialiseAsync([fixture]); Call("ApplyTheme"); Call("SetLayout", "Editor"); await Parse();
                var first = model.ActiveDocument!;
                Check(outline.Items.Count == 2 && Nodes().Length == 9 && Nodes().Take(6).Select(node => node.Heading.Level).SequenceEqual(Enumerable.Range(1, 6)), "H1–H6 form actual parent and child collections in source order");
                var root = Nodes()[0]; var branch = Nodes()[1]; var leaf = Nodes()[2]; var secondBranch = Nodes()[6];
                Check(leaf.Parent == branch && branch.Parent == root && secondBranch.Parent == root, "Skipped and repeated heading ancestry is explicit");
                var rootPeer = UIElementAutomationPeer.CreatePeerForElement(Row(root))!;
                Check(rootPeer.GetAutomationControlType() == AutomationControlType.TreeItem && rootPeer.GetName() == "Heading level 1: Root", "UIA exposes a named native heading tree item");
                var expansion = (IExpandCollapseProvider)rootPeer.GetPattern(PatternInterface.ExpandCollapse)!;
                expansion.Collapse(); await Drain(); Check(!root.IsExpanded && expansion.ExpandCollapseState == ExpandCollapseState.Collapsed, "UIA Collapse communicates collapsed state");
                expansion.Expand(); await Drain(); Check(root.IsExpanded, "UIA Expand restores descendants");
                var previewStatus = model.PreviewStatus;
                Row(branch).Focus(); Row(branch).IsSelected = true; SendKey(Row(branch), Key.Left); await Drain();
                Check(!branch.IsExpanded && Row(branch).IsKeyboardFocused, "Left collapses a section without losing row focus");
                SendKey(Row(branch), Key.Right); await Drain(); Check(branch.IsExpanded, "Right expands a section");
                SendKey(Row(branch), Key.Right); await Drain(); Check(outline.SelectedItem == leaf, "Right moves into the first child");
                SendKey(Row(leaf), Key.Left); await Drain(); Check(!leaf.IsExpanded, "Left first collapses an expanded child");
                SendKey(Row(leaf), Key.Left); await Drain(); Check(outline.SelectedItem == branch, "Left on a collapsed child moves to its parent");
                SendKey(Row(branch), Key.Home); await Drain(); Check(outline.SelectedItem == root, "Home selects the first visible heading");
                SendKey(Row(root), Key.End); await Drain(); Check(((OutlineHeadingViewModel)outline.SelectedItem).Title == "Last", "End selects the final visible heading");
                SendKey(Row((OutlineHeadingViewModel)outline.SelectedItem), Key.Up); await Drain();
                Check(((OutlineHeadingViewModel)outline.SelectedItem).Title == "Duplicate child", "Up traverses visible descendants");
                SendKey(Row((OutlineHeadingViewModel)outline.SelectedItem), Key.Down); await Drain(); Check(((OutlineHeadingViewModel)outline.SelectedItem).Title == "Last", "Down traverses visible headings");
                Row(branch).Focus(); Row(branch).IsSelected = true; SendKey(Row(branch), Key.Enter); await Drain();
                Check(editor.CaretOffset == branch.Heading.SourceStart && editor.SelectionLength == 0 && outline.IsKeyboardFocusWithin, "Enter navigates to exact source and keeps keyboard focus usable");
                Group(false); filter.Text = "Six café"; await Drain();
                Check(Nodes().Count(node => node.IsVisible) == 6 && Nodes().Take(6).All(node => node.IsExpanded), "Filter reveals a deep match with its complete ancestor path");
                filter.Text = ""; await Drain(); Check(!root.IsExpanded && !branch.IsExpanded, "Clearing the filter restores collapsed sections");
                filter.Text = "missing heading"; await Drain(); Check(((TextBlock)window.FindName("OutlineEmpty")).Text.Contains("No headings match"), "No-results message is clear");
                filter.Text = ""; Group(true); await Drain();
                Check(Nodes().All(node => node.IsExpanded) && model.PreviewStatus == previewStatus && editor.Text == source, "Expand and filter controls preserve source and do not rerender preview");
                Row(secondBranch).IsExpanded = false; model.NewDocumentCommand.Execute(null); var empty = model.ActiveDocument!; await Parse();
                Check(Nodes().Length == 0 && ((TextBlock)window.FindName("OutlineEmpty")).Text.Contains("Add Markdown headings"), "Empty document state is useful");
                editor.Document.Text = source; await Parse(); Check(Nodes()[6].IsExpanded, "New documents have independent expansion state");
                model.ActiveDocument = first; await Parse(); Check(!Nodes()[6].IsExpanded, "Switching tabs restores this document's collapsed state");
                editor.Document.Insert(0, "Introduction\r\n\r\n"); await Parse(); Check(!Nodes()[6].IsExpanded, "Text anchors retain collapsed identity when source positions move");
                var firstBranchStart = editor.Text.IndexOf("## Branch", StringComparison.Ordinal);
                var firstBranchEnd = editor.Text.IndexOf("## Branch", firstBranchStart + 1, StringComparison.Ordinal);
                editor.Document.Remove(firstBranchStart, firstBranchEnd - firstBranchStart); await Parse();
                Check(Nodes().Length == 4 && !Nodes()[1].IsExpanded, "Duplicate anchor renumbering retains the surviving section's own state");
                editor.Document.UndoStack.Undo(); await Parse(); Check(Nodes().Length == 9 && !Nodes()[6].IsExpanded, "Undo restores headings without losing surviving collapse state");
                editor.Document.UndoStack.Redo(); await Parse(); Check(Nodes().Length == 4, "Redo updates the hierarchy");
                var rename = editor.Text.IndexOf("Branch", StringComparison.Ordinal); editor.Document.Replace(rename, "Branch".Length, "Renamed"); await Parse();
                Check(Nodes()[1].Title == "Renamed" && Nodes()[1].IsExpanded, "Renamed headings discard obsolete collapse records");
                editor.Document.Remove(Nodes()[1].Heading.SourceStart, editor.Text.IndexOf("# Last", StringComparison.Ordinal) - Nodes()[1].Heading.SourceStart); await Parse();
                Check(Nodes().Length == 2 && Nodes().All(node => node.IsExpanded), "Removed headings cannot transfer collapsed state to another heading");
                editor.Document.Text = source; await Parse(); editor.Focus(); editor.CaretOffset = source.IndexOf("# Last", StringComparison.Ordinal); await Drain();
                Check(Nodes().Single(node => node.IsCurrent).Title == "Last", "Current-heading highlight follows the caret");
                editor.Select(3, 4); Call("ShowFind", false); var dirty = first.IsDirty;
                var findText = ((TextBox)window.FindName("FindText")); findText.Text = "Branch"; await Drain();
                var selection = (editor.SelectionStart, editor.SelectionLength);
                // Materialise every existing clipboard format before the test owns it.
                if (Clipboard.GetDataObject() is { } previous)
                {
                    savedClipboard = new DataObject();
                    foreach (var format in previous.GetFormats(false)) if (previous.GetData(format, false) is { } data) savedClipboard.SetData(format, data, false);
                }
                var observed = false;
                async Task ObserveDialog(bool invalidate)
                {
                    await WaitFor(() => window.OwnedWindows.OfType<TableOfContentsWindow>().Any());
                    var dialog = window.OwnedWindows.OfType<TableOfContentsWindow>().Single();
                    try
                    {
                        var copy = (Button)dialog.FindName("CopyButton"); var preview = (TextBox)dialog.FindName("PreviewText"); var status = (TextBlock)dialog.FindName("ResultStatus");
                        await WaitFor(() => copy.IsEnabled);
                        Check(preview.Text == TableOfContentsService.Create(new MarkdownService().Parse(editor.Text).Headings), "Dialog preview uses the current unsaved parser headings");
                        if (invalidate)
                        {
                            editor.Document.Insert(0, "# Changed\n"); await Drain();
                            Check(preview.Text.Length == 0 && !copy.IsEnabled && status.Text.Contains("document changed"), "Open dialog invalidates immediately when source version changes");
                        }
                        else
                        {
                            Check(((ComboBox)dialog.FindName("StartLevel")).IsKeyboardFocused, "Dialog initially focuses a usable option");
                            ((ComboBox)dialog.FindName("StartLevel")).SelectedIndex = 5; ((ComboBox)dialog.FindName("EndLevel")).SelectedIndex = 0;
                            await WaitFor(() => status.Text.Contains("start level")); Check(!copy.IsEnabled && preview.Text == "", "Invalid level ranges clear preview and disable Copy");
                            ((ComboBox)dialog.FindName("StartLevel")).SelectedIndex = 0; ((ComboBox)dialog.FindName("EndLevel")).SelectedIndex = 5;
                            ((ComboBox)dialog.FindName("ListStyle")).SelectedIndex = 1; ((CheckBox)dialog.FindName("DetailsWrapper")).IsChecked = true;
                            await WaitFor(() => copy.IsEnabled && preview.Text.StartsWith("<details>"));
                            var expected = TableOfContentsService.Create(new MarkdownService().Parse(editor.Text).Headings, new(Style: TableOfContentsStyle.Numbered, Details: true));
                            Check(preview.Text == expected, "Numbered and details options update the exact preview");
                            var sequenceBeforeCopy = GetClipboardSequenceNumber();
                            copy.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            var copyStatus = status.Text;
                            var sequenceAfterCopy = GetClipboardSequenceNumber();
                            var ownerAfterCopy = GetClipboardOwner();
                            GetWindowThreadProcessId(ownerAfterCopy, out var ownerProcessAfterCopy);
                            var sequenceBeforeRead = GetClipboardSequenceNumber();
                            string? actualClipboard = null; Exception? clipboardReadFailure = null;
                            try { actualClipboard = Clipboard.GetText(); }
                            catch (Exception error) when (error is ExternalException or ThreadStateException) { clipboardReadFailure = error; }
                            var sequenceAfterRead = GetClipboardSequenceNumber();
                            var ownerAfterRead = GetClipboardOwner();
                            GetWindowThreadProcessId(ownerAfterRead, out var ownerProcessAfterRead);
                            var copied = copyStatus == "Table of contents copied.";
                            var matches = clipboardReadFailure is null && actualClipboard == expected;
                            var stable = sequenceAfterCopy != 0 && sequenceAfterCopy == sequenceBeforeRead && sequenceBeforeRead == sequenceAfterRead;
                            // Restore only after an exact, stable observation of a successful
                            // test write. A failed Copy must not claim another owner's data.
                            ownedClipboard = copied && matches && stable && sequenceAfterCopy != sequenceBeforeCopy
                                ? sequenceAfterRead : 0;
                            string TextDigest(string value) => Convert.ToHexString(
                                System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
                            File.WriteAllText(Path.Combine(output, "clipboard-copy.json"), JsonSerializer.Serialize(new
                            {
                                status = copyStatus, expectedLength = expected.Length, expectedSha256 = TextDigest(expected),
                                actualLength = actualClipboard?.Length, actualSha256 = actualClipboard is null ? null : TextDigest(actualClipboard),
                                matches, sourceCurrent = editor.Text == source, sequenceBeforeCopy, sequenceAfterCopy, sequenceBeforeRead, sequenceAfterRead,
                                ownerWindowAfterCopy = ownerAfterCopy.ToInt64(), ownerProcessAfterCopy,
                                ownerWindowAfterRead = ownerAfterRead.ToInt64(), ownerProcessAfterRead,
                                testProcess = Environment.ProcessId, restorationOwnershipEstablished = ownedClipboard != 0,
                                readErrorType = clipboardReadFailure?.GetType().FullName, readErrorHResult = clipboardReadFailure?.HResult
                            }));
                            if (clipboardReadFailure is not null) throw new IOException("Clipboard observation failed.", clipboardReadFailure);
                            Check(copied, "Copy reports a successful clipboard write");
                            Check(stable, "Clipboard observation retains the copied sequence");
                            Check(matches, "Clipboard contains the exact generated Markdown");
                            using var locked = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
                            var lockThread = new Thread(() => { if (!OpenClipboard(IntPtr.Zero)) return; locked.Set(); release.Wait(TimeSpan.FromSeconds(5)); CloseClipboard(); });
                            lockThread.Start();
                            try { Check(locked.Wait(TimeSpan.FromSeconds(2)), "Clipboard lock fixture acquired ownership"); copy.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Check(status.Text.Contains("unavailable"), "Locked clipboard produces a recoverable message"); }
                            finally { release.Set(); lockThread.Join(); }
                            dialog.Width = 440; await Drain(); Capture("toc-minimum", dialog);
                            Check(copy.TransformToAncestor(dialog).Transform(new Point()).X + copy.ActualWidth <= dialog.ActualWidth, "Dialog actions fit its minimum width");
                        }
                        observed = true;
                    }
                    finally { dialog.Close(); }
                }
                Check(tocButton.Focus(), "Copy TOC toolbar action accepts keyboard focus"); var observer = ObserveDialog(false); SendKey(tocButton, Key.Enter); await observer;
                File.WriteAllText(Path.Combine(output, "preservation.json"), JsonSerializer.Serialize(new { observed, focus = tocButton.IsKeyboardFocused, source = editor.Text == source, dirty = first.IsDirty, expectedDirty = dirty, selection = new[] { editor.SelectionStart, editor.SelectionLength }, expectedSelection = new[] { selection.SelectionStart, selection.SelectionLength }, find = findText.Text }));
                Check(observed && tocButton.IsKeyboardFocused && editor.Text == source && first.IsDirty == dirty
                    && (editor.SelectionStart, editor.SelectionLength) == selection && findText.Text == "Branch", "Copy and dismissal preserve source, dirty state, selection, Find and caller focus");
                observer = ObserveDialog(true); SendKey(tocButton, Key.Enter); await observer;
                // Exercise cancellation while parsing is queued, without reading linked assets.
                var direct = new TableOfContentsWindow(first.EditorDocument.CreateSnapshot(), false, () => true) { Owner = window };
                var worker = (SemaphoreSlim)typeof(TableOfContentsWindow).GetField("worker", flags)!.GetValue(direct)!;
                await worker.WaitAsync(); direct.Show(); await Drain(); direct.InvalidateSource(); worker.Release(); await Drain();
                Check(!((Button)direct.FindName("CopyButton")).IsEnabled && ((TextBox)direct.FindName("PreviewText")).Text == "", "Cancelled queued generation never publishes stale Markdown"); direct.Close();
                var noHeadings = new TableOfContentsWindow(new ICSharpCode.AvalonEdit.Document.TextDocument("").CreateSnapshot(), false, () => true) { Owner = window };
                noHeadings.Show(); await WaitFor(() => ((TextBlock)noHeadings.FindName("ResultStatus")).Text.Contains("no headings"));
                Check(!((Button)noHeadings.FindName("CopyButton")).IsEnabled, "Empty TOC disables Copy with an explanation"); noHeadings.Close();
                editor.Document.Text = source; await Parse(); window.Width = 860; await Drain(); Capture("outline-minimum", window);
                Check(outline.ActualWidth >= 150 && outline.TransformToAncestor(window).Transform(new Point()).X + outline.ActualWidth <= window.ActualWidth, "Outline fits the minimum supported 860-pixel window");
                Check(((SolidColorBrush)Row(Nodes()[0]).Foreground).Color == ((SolidColorBrush)window.FindResource("TextBrush")).Color, "Outline text uses the native theme resource");
                Check(File.ReadAllBytes(fixture).SequenceEqual(original), "All navigation, copy and edit controls preserve original saved bytes and BOM");
                first.MarkSaved(first.SavedSnapshot!); await model.CloseDocumentCommand.ExecuteAsync(first); await Parse();
                Check(model.ActiveDocument == empty, "Closing a document selects the remaining document");
                await model.OpenPathAsync(fixture); await Parse(); Check(Nodes().All(node => node.IsExpanded), "Reopened documents do not inherit disposed collapsed state");
                var active = model.ActiveDocument!;
                var savedPath = active.FilePath; active.FilePath = Path.ChangeExtension(savedPath, ".mmd");
                Call("RefreshOutline");
                Check(!outline.IsEnabled && outline.Items.Count == 0, "An unchanged text version cannot reuse headings from a different parser mode");
                async Task ObserveEmptyToc(Action? change = null)
                {
                    await WaitFor(() => window.OwnedWindows.OfType<TableOfContentsWindow>().Any());
                    var dialog = window.OwnedWindows.OfType<TableOfContentsWindow>().Single();
                    try
                    {
                        var status = (TextBlock)dialog.FindName("ResultStatus");
                        if (change is null)
                        {
                            await WaitFor(() => status.Text.Contains("no headings"));
                            Check(!((Button)dialog.FindName("CopyButton")).IsEnabled, "TOC respects a newly selected standalone Mermaid identity");
                        }
                        else { change(); await WaitFor(() => status.Text.Contains("document changed")); Check(((TextBox)dialog.FindName("PreviewText")).Text == "", "Switching active documents invalidates the open TOC"); }
                    }
                    finally { dialog.Close(); }
                }
                var emptyObserver = ObserveEmptyToc(); tocButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await emptyObserver;
                active.FilePath = savedPath; await Parse();
                emptyObserver = ObserveEmptyToc(() => model.ActiveDocument = empty); tocButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await emptyObserver;
                model.ActiveDocument = active; await Parse();
                editor.Document.Insert(0, "Updated\n"); Call("OutlineUpdateFailed");
                Check(!outline.IsEnabled && ((TextBlock)window.FindName("OutlineEmpty")).Text.Contains("could not be updated"), "Failed current parsing has an explicit outline error state");
                await Parse(); Check(outline.IsEnabled, "Successful parsing recovers the outline after an error");
                editor.Document.Text = string.Concat(Enumerable.Range(0, 1000).Select(i => $"# Section {i}\n## Child {i}\n")); await Parse();
                Group(false); Check(Nodes().Where(node => node.Children.Count > 0).All(node => !node.IsExpanded), "Collapse All handles a thousand sections completely");
                Group(true); Check(Nodes().All(node => node.IsExpanded), "Expand All restores a thousand sections completely");
                editor.Document.Text = source; await Parse();
                var largeSource = new ICSharpCode.AvalonEdit.Document.TextDocument("# Large\n\n```text\n" + new string('x', 4 * 1024 * 1024 - 100) + "\n```\n\n## Final");
                var largeDialog = new TableOfContentsWindow(largeSource.CreateSnapshot(), false, () => true) { Owner = window };
                largeDialog.Show(); var pulse = false; await window.Dispatcher.InvokeAsync(() => pulse = true, DispatcherPriority.Input);
                await WaitFor(() => ((Button)largeDialog.FindName("CopyButton")).IsEnabled);
                Check(pulse && ((TextBox)largeDialog.FindName("PreviewText")).Text.Contains("Final"), "Large generation serves dispatcher input and includes the final heading");
                largeDialog.Close();
                var staleSource = new ICSharpCode.AvalonEdit.Document.TextDocument("# Before"); var version = staleSource.Version;
                var staleDialog = new TableOfContentsWindow(staleSource.CreateSnapshot(), false, () => staleSource.Version.CompareAge(version) == 0) { Owner = window };
                var staleGate = (SemaphoreSlim)typeof(TableOfContentsWindow).GetField("worker", flags)!.GetValue(staleDialog)!;
                await staleGate.WaitAsync(); staleDialog.Show(); await Drain(); staleSource.Text = "# After"; staleGate.Release();
                await WaitFor(() => ((TextBlock)staleDialog.FindName("ResultStatus")).Text.Contains("document changed"));
                Check(((TextBox)staleDialog.FindName("PreviewText")).Text == "", "Source-version guards reject queued results without a model notification"); staleDialog.Close();
                var closeDialog = new TableOfContentsWindow(staleSource.CreateSnapshot(), false, () => true) { Owner = window };
                await staleGate.WaitAsync(); closeDialog.Show(); await Drain(); closeDialog.Close(); staleGate.Release(); await Drain();
                Check(!((Button)closeDialog.FindName("CopyButton")).IsEnabled, "Closing while queued prevents late TOC publication");
                Check(!highContrast || NativeTestHighContrast.IsEnabledNow && SystemParameters.HighContrast, "Requested OS appearance remains unchanged");
                code = 0;
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(output, "failure.txt"), error.ToString()); Console.Error.WriteLine(error); Capture("failure", window); }
            finally
            {
                try
                {
                var sequenceBeforeRestore = GetClipboardSequenceNumber();
                var restoration = ownedClipboard == 0 ? "No successful test ownership established" : "Clipboard changed after the test write";
                try
                {
                    if (ownedClipboard != 0 && sequenceBeforeRestore == ownedClipboard)
                    {
                        if (savedClipboard is null) Clipboard.Clear(); else Clipboard.SetDataObject(savedClipboard, true);
                        restoration = "Restored the captured clipboard";
                    }
                }
                catch (Exception error) { code = 1; restoration = "Clipboard restoration failed"; File.WriteAllText(Path.Combine(output, "clipboard-restoration-failure.txt"), error.ToString()); }
                File.WriteAllText(Path.Combine(output, "clipboard-restoration.json"), JsonSerializer.Serialize(new
                { restoration, ownedClipboard, sequenceBeforeRestore, sequenceAfterRestore = GetClipboardSequenceNumber() }));
                File.WriteAllText(Path.Combine(output, "window-ownership.json"), JsonSerializer.Serialize(new
                { applicationType = app.GetType().FullName, bootstrapResources, observedWindows = observedWindows.Values,
                    observations = checks.Count, observationMilliseconds = ownershipObservationTicks * 1000d / Stopwatch.Frequency,
                    productionAppConstructed = app is App }));
                File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { result = code == 0 ? "PASS" : "FAIL", checks, theme,
                    actualHighContrast = highContrast, scope = "Owned shown WPF windows, UIA providers and routed keyboard events; physical input and screen-reader certification are not claimed" }, new JsonSerializerOptions { WriteIndented = true }));
                }
                catch (Exception error) { code = 1; Console.Error.WriteLine("Final control evidence failed: " + error); }
                finally
                {
                    try { window.Close(); }
                    catch (Exception error) { code = 1; Console.Error.WriteLine("Control window cleanup failed: " + error); }
                    finally { app.Shutdown(code); }
                }
            }
        });
        app.Run();
        try
        {
            var remainingWindows = app.Windows.Count;
            if (remainingWindows != 0) code = 1;
            File.WriteAllText(Path.Combine(output, "cleanup.json"), JsonSerializer.Serialize(new { remainingWindows, exitCode = code }));
        }
        catch (Exception error) { code = 1; Console.Error.WriteLine("Final window cleanup observation failed: " + error); }
        return code;
    }
}
