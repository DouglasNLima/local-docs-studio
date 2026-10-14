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
            for (var index = 0; index < paths.Length; index++)
                if (!File.ReadAllBytes(paths[index]).SequenceEqual(snapshots[index])) throw new IOException("Workspace chrome changed document bytes.");
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "native-workspace-chrome.json"), JsonSerializer.Serialize(new {
                Passed = true, FilteringPreservesNodes = true, ExpansionRetained = true, ActiveDocumentRetained = true,
                PinsSurviveCloseOthers = true, PinsAndTabOrderRestored = true, SourceBytesPreserved = true }));
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
}
