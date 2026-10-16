using System.Diagnostics;
using System.Text.Json;
using System.Windows.Threading;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.Validation;

internal static class NativeWorkspaceChromeChecks
{
    public static void Run(string output)
    {
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        try
        {
            var root = Path.Combine(output, "chrome-" + Guid.NewGuid().ToString("N"));
            var workspace = Path.Combine(root, "Documentation"); var state = Path.Combine(root, "private-state");
            Directory.CreateDirectory(Path.Combine(workspace, "guides"));
            var paths = new[] { Path.Combine(workspace, "guides", "Café.md"), Path.Combine(workspace, "First.md"), Path.Combine(workspace, "Second.md") };
            foreach (var path in paths) File.WriteAllText(path, "# " + Path.GetFileNameWithoutExtension(path) + "\r\n");
            var snapshots = paths.Select(File.ReadAllBytes).ToArray();
            using var model = new WorkspaceViewModel(new DocumentStore(Path.Combine(state, "writes")), new SessionStore(state), new WorkspaceService(), new DesktopDialogs());
            Complete(model.SetWorkspaceAsync(workspace));
            foreach (var path in paths) Complete(model.OpenPathAsync(path, workspace));
            var active = model.ActiveDocument!;
            var folder = model.Entries.Single(entry => entry.IsDirectory);
            folder.IsExpanded = false; var retainedChild = folder.Children.Single();
            model.WorkspaceFilter = "Café";
            if (!folder.IsVisible || !retainedChild.IsVisible || folder.IsExpanded || model.ActiveDocument != active || model.Entries.Any(entry => !entry.IsDirectory && entry.IsVisible))
                throw new IOException("Filename filtering lost expansion, active selection or its exact matching files.");
            model.WorkspaceFilter = ""; Complete(model.RefreshWorkspaceCommand.ExecuteAsync(null));
            if (folder.IsExpanded || folder.Children.Single() != retainedChild || model.Entries.Any(entry => !entry.IsVisible))
                throw new IOException("Clearing and refreshing a filter lost surviving workspace nodes or expansion state.");
            model.ExpandWorkspaceCommand.Execute(null);
            if (!folder.IsExpanded) throw new IOException("Expand all did not expand a retained folder.");
            model.CollapseWorkspaceCommand.Execute(null);
            if (folder.IsExpanded) throw new IOException("Collapse all did not collapse a retained folder.");
            var pinned = model.Documents[0];
            Complete(model.TogglePinCommand.ExecuteAsync(pinned)); Complete(model.CloseDocumentCommand.ExecuteAsync(pinned));
            if (!pinned.IsPinned || !model.Documents.Contains(pinned)) throw new IOException("A pinned document closed without first being unpinned.");
            Complete(model.CloseOthersCommand.ExecuteAsync(active));
            if (model.Documents.Count != 2 || !model.Documents.Contains(pinned) || model.ActiveDocument != active)
                throw new IOException("Close others lost the protected pin or the surviving active document.");
            Complete(model.PersistSessionAsync());
            active.PreviewSourceOffset = 5;
            Complete(model.PersistSessionAsync());
            using var restored = new WorkspaceViewModel(new DocumentStore(Path.Combine(state, "writes")), new SessionStore(state), new WorkspaceService(), new DesktopDialogs());
            Complete(restored.InitialiseAsync([]));
            if (restored.Documents.Count != 2 || restored.Documents[0].Id != pinned.Id || !restored.Documents[0].IsPinned || restored.ActiveDocument?.Id != active.Id || restored.ActiveDocument.PreviewSourceOffset != 5)
                throw new IOException("Restarted session state lost tab order, pin or active document.");
            restored.Dispose();
            var iconPath = Path.Combine(workspace, "icon.md"); File.WriteAllText(iconPath, "# Icon fixture");
            WaitUntil(() => model.Entries.Any(entry => entry.Path == iconPath));
            var mutableIcon = model.Entries.Single(entry => entry.Path == iconPath);
            var notifications = new List<string?>(); mutableIcon.PropertyChanged += (_, change) => notifications.Add(change.PropertyName);
            mutableIcon.Path = Path.ChangeExtension(iconPath, ".TXT");
            if (mutableIcon.IconKind != "PlainText" || !notifications.Contains("IconKind") || !notifications.Contains("FileTypeDescription"))
                throw new IOException("Mutable file paths did not notify their file-type identity.");
            mutableIcon.Path = iconPath;
            var diagramPath = Path.ChangeExtension(iconPath, ".MMD"); File.Move(iconPath, diagramPath);
            WaitUntil(() => model.Entries.Any(entry => entry.Path == diagramPath && entry.IconKind == "Mermaid") && model.Entries.All(entry => entry.Path != iconPath));
            Complete(model.RefreshWorkspaceCommand.ExecuteAsync(null));
            if (model.Entries.Single(entry => entry.Path == diagramPath).IconKind != "Mermaid") throw new IOException("Refreshing lost the renamed diagram identity.");
            var retainedDocuments = model.Documents.ToArray(); var retainedRoot = active.WorkspaceRoot; var retainedSnapshot = active.SavedSnapshot;
            File.WriteAllBytes(Path.Combine(workspace, "image.png"), [1, 2, 3, 4]);
            active.EditorDocument.Text += "\r\nUnsaved text with ![relative image](image.png)";
            var retainedText = active.EditorDocument.Text; model.WorkspaceFilter = "Café";
            File.WriteAllText(Path.Combine(workspace, "pending.md"), "# Pending watcher event");
            Complete(model.CloseWorkspaceCommand.ExecuteAsync(null));
            Complete(Task.Delay(500)); // Let already queued filesystem notifications and debounce continuations run.
            if (model.WorkspaceRoot is not null || model.Entries.Count != 0 || model.WorkspaceFilter.Length != 0 || model.CloseWorkspaceCommand.CanExecute(null)
                || !model.Documents.SequenceEqual(retainedDocuments) || model.ActiveDocument != active || !active.IsDirty || active.EditorDocument.Text != retainedText
                || active.WorkspaceRoot != retainedRoot || active.SavedSnapshot != retainedSnapshot
                || typeof(WorkspaceViewModel).GetField("watcher", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(model) is not null)
                throw new IOException("Closing a workspace lost document context or left monitoring/tree state active.");
            using var closedRestored = new WorkspaceViewModel(new DocumentStore(Path.Combine(state, "writes")), new SessionStore(state), new WorkspaceService(), new DesktopDialogs());
            Complete(closedRestored.InitialiseAsync([]));
            if (closedRestored.WorkspaceRoot is not null || closedRestored.Entries.Count != 0 || closedRestored.ActiveDocument?.Id != active.Id
                || closedRestored.ActiveDocument.EditorDocument.Text != retainedText || !closedRestored.ActiveDocument.IsDirty || closedRestored.ActiveDocument.WorkspaceRoot != retainedRoot
                || !closedRestored.RecentWorkspaces.Any(recent => recent.Path == workspace))
                throw new IOException("Restarting after workspace closure lost drafts, document roots or recent-workspace history.");
            Complete(model.SetWorkspaceAsync(workspace));
            if (!model.Entries.Any() || model.ActiveDocument != active || active.EditorDocument.Text != retainedText) throw new IOException("Reopening a workspace lost the retained draft.");
            Complete(model.CloseWorkspaceCommand.ExecuteAsync(null));
            var other = Path.Combine(root, "Other"); Directory.CreateDirectory(other); File.WriteAllText(Path.Combine(other, "other.md"), "# Other workspace");
            Complete(model.SetWorkspaceAsync(other));
            if (model.Entries.Single().Name != "other.md" || active.WorkspaceRoot != retainedRoot || active.EditorDocument.Text != retainedText)
                throw new IOException("Opening another workspace changed the previous document's context.");
            var many = Path.Combine(root, "Many documents"); Directory.CreateDirectory(many);
            var manyPaths = Enumerable.Range(1, 256).Select(index => Path.Combine(many, $"Document {index:000}.md")).ToArray();
            foreach (var path in manyPaths) File.WriteAllText(path, "# " + Path.GetFileNameWithoutExtension(path) + "\n");
            Complete(model.SetWorkspaceAsync(many));
            if (model.Entries.Count != manyPaths.Length) throw new IOException("The many-file workspace did not enumerate completely.");
            Complete(model.CloseWorkspaceCommand.ExecuteAsync(null)); Complete(Task.Delay(500));
            if (model.WorkspaceRoot is not null || model.Entries.Count != 0 || !model.Documents.SequenceEqual(retainedDocuments) || active.EditorDocument.Text != retainedText)
                throw new IOException("Closing the many-file workspace repopulated its tree or lost retained document state.");
            foreach (var path in manyPaths)
                if (File.ReadAllText(path) != "# " + Path.GetFileNameWithoutExtension(path) + "\n") throw new IOException("Workspace closure mutated a many-file source.");
            var empty = Path.Combine(root, "Empty workspace"); Directory.CreateDirectory(empty); Complete(model.SetWorkspaceAsync(empty));
            if (model.Entries.Count != 0 || !model.CloseWorkspaceCommand.CanExecute(null) || model.WorkspaceEmptyMessage.Length == 0)
                throw new IOException("An empty workspace lost its closure action or useful empty state.");
            Complete(model.CloseWorkspaceCommand.ExecuteAsync(null));
            for (var index = 0; index < paths.Length; index++)
                if (!File.ReadAllBytes(paths[index]).SequenceEqual(snapshots[index])) throw new IOException("Workspace chrome changed document bytes.");
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "native-workspace-chrome.json"), JsonSerializer.Serialize(new {
                Passed = true, FilteringPreservesNodes = true, ExpansionRetained = true, ActiveDocumentRetained = true,
                PinsSurviveCloseOthers = true, PinsAndTabOrderRestored = true, SourceBytesPreserved = true,
                MutableAndExternallyRenamedIcons = true, CloseStopsMonitoringAndPendingRefresh = true,
                ManyFileCount = manyPaths.Length, ManyAndEmptyWorkspaceClosure = true,
                CloseRetainsTabsDraftsAndAssets = true, ClosedSessionRestored = true, WorkspaceReopenAndSwitch = true }));
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }
    private static void Complete(Task task)
    {
        var watch = Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(25)) throw new TimeoutException("The workspace chrome fixture exceeded its deadline.");
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false); Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }
    private static void WaitUntil(Func<bool> predicate)
    {
        var watch = Stopwatch.StartNew();
        while (!predicate())
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(5)) throw new TimeoutException("The workspace filesystem identity did not reconcile.");
            Complete(Task.Delay(20));
        }
    }
}
