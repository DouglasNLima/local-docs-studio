using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Threading;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.Validation;

internal static class NativeWorkspaceChecks
{
    public static void Run(string output)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        try
        {
            var pending = CheckAsync(output);
            var frame = new DispatcherFrame();
            var expired = false;
            var timer = new DispatcherTimer(DispatcherPriority.Send, dispatcher) { Interval = TimeSpan.FromSeconds(25) };
            timer.Tick += (_, _) => { expired = true; frame.Continue = false; };
            _ = pending.ContinueWith(_ => dispatcher.BeginInvoke(DispatcherPriority.Send, () => frame.Continue = false), TaskScheduler.Default);
            timer.Start(); Dispatcher.PushFrame(frame); timer.Stop();
            if (expired) throw new TimeoutException("Native workspace monitoring exceeded its validation deadline.");
            pending.GetAwaiter().GetResult();
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }

    private static async Task CheckAsync(string output)
    {
        var root = Path.Combine(output, "workspace-monitor-" + Guid.NewGuid().ToString("N"));
        var state = Path.Combine(root, "state"); var folder = Path.Combine(root, "docs");
        Directory.CreateDirectory(state); Directory.CreateDirectory(folder);
        var original = Path.Combine(folder, "Original.md"); File.WriteAllText(original, "# Saved original");
        File.WriteAllText(Path.Combine(state, "private.md"), "Private recovery text");
        using var model = new WorkspaceViewModel(new DocumentStore(Path.Combine(state, "recovery")), new SessionStore(state), new WorkspaceService(), new DesktopDialogs());
        await model.SetWorkspaceAsync(root); await model.OpenPathAsync(original);
        var tab = model.ActiveDocument!; tab.EditorDocument.Text = "# Unsaved original";
        var folderNode = model.Entries.Single(); var originalNode = folderNode.Children.Single();
        if (folderNode.Path != folder || originalNode.Path != original) throw new IOException("Private native state appeared in the workspace tree.");
        var template = new HierarchicalDataTemplate(typeof(WorkspaceEntryViewModel)) { ItemsSource = new Binding("Children") };
        var tree = new TreeView { ItemsSource = model.Entries, ItemTemplate = template };
        using var host = new HwndSource(new HwndSourceParameters("Workspace monitoring validation") { Width = 640, Height = 480, WindowStyle = unchecked((int)0x80000000) });
        host.RootVisual = tree;
        tree.Measure(new(640, 480)); tree.Arrange(new Rect(0, 0, 640, 480)); tree.UpdateLayout();
        var folderContainer = (TreeViewItem)tree.ItemContainerGenerator.ContainerFromItem(folderNode);
        folderContainer.IsExpanded = true; tree.UpdateLayout();
        var originalContainer = (TreeViewItem)folderContainer.ItemContainerGenerator.ContainerFromItem(originalNode);
        originalContainer.IsSelected = true;
        var created = Path.Combine(folder, "Added.md"); File.WriteAllText(created, "# Added");
        await WaitAsync(() => folderNode.Children.Any(item => item.Path == created), "A new file did not appear in the monitored tree.");
        tree.UpdateLayout();
        if (!ReferenceEquals(tree.ItemContainerGenerator.ContainerFromItem(folderNode), folderContainer) || !folderContainer.IsExpanded
            || !ReferenceEquals(tree.SelectedItem, originalNode) || !ReferenceEquals(folderNode.Children.Single(item => item.Path == original), originalNode))
            throw new IOException("Refreshing a nested folder replaced surviving controls, expansion or selection.");
        folderContainer.IsExpanded = false;
        var collapsedSelection = tree.SelectedItem;
        var renamed = Path.Combine(folder, "Renamed.md"); File.Move(created, renamed);
        await WaitAsync(() => folderNode.Children.Any(item => item.Path == renamed) && folderNode.Children.All(item => item.Path != created), "An externally renamed file was not reflected in the tree.");
        tree.UpdateLayout();
        if (folderContainer.IsExpanded || !ReferenceEquals(tree.SelectedItem, collapsedSelection))
            throw new IOException("Refreshing a collapsed folder changed its expansion or native selection.");
        File.Delete(renamed);
        await WaitAsync(() => folderNode.Children.Count == 1, "An externally deleted file remained in the tree.");
        var addedFolder = Path.Combine(root, "Added folder"); Directory.CreateDirectory(addedFolder);
        var nested = Path.Combine(addedFolder, "Nested.md"); File.WriteAllText(nested, "Nested");
        await WaitAsync(() => model.Entries.Any(item => item.Path == addedFolder && item.Children.Any(child => child.Path == nested)), "A new folder and its document were not enumerated.");
        File.WriteAllText(original, "# Changed externally");
        await WaitAsync(() => tab.ExternalChange, "An external edit did not flag the open document conflict.");
        if (tab.EditorDocument.Text != "# Unsaved original" || !tab.IsDirty || model.ActiveDocument != tab || File.ReadAllText(original) != "# Changed externally")
            throw new IOException("Monitoring replaced unsaved text, changed tabs or wrote a document.");
        File.WriteAllText(Path.Combine(state, "another-private.md"), "Retained recovery");
        await Task.Delay(400);
        if (model.Entries.Any(item => item.Path == state)) throw new IOException("A monitoring refresh exposed private state.");
        var second = Path.Combine(output, "workspace-monitor-second-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(second);
        File.WriteAllText(Path.Combine(second, "Second.md"), "# Second");
        File.WriteAllText(Path.Combine(root, "Pending.md"), "Old workspace change");
        await model.SetWorkspaceAsync(second);
        await Task.Delay(500);
        if (model.Entries.Count != 1 || model.Entries[0].Name != "Second.md" || model.ActiveDocument != tab || tab.EditorDocument.Text != "# Unsaved original")
            throw new IOException("A superseded refresh changed the new workspace or an open draft.");
        model.Dispose(); File.WriteAllText(Path.Combine(second, "After disposal.md"), "Ignored");
        await Task.Delay(400);
        if (model.Entries.Count != 1) throw new IOException("A disposed workspace still applied monitoring changes.");
        host.RootVisual = null;
        File.WriteAllText(Path.Combine(output, "native-workspace-monitor.json"), JsonSerializer.Serialize(new
        {
            Passed = true,
            Checks = new[] { "Created, renamed and deleted files refresh from real filesystem events", "Nested folder creation is enumerated",
                "Surviving WPF folder controls retain expansion and selection", "External edits flag conflicts without replacing unsaved text or saving",
                "Private native state stays outside initial and refreshed trees", "Superseded workspace refreshes cannot overwrite current navigation", "Disposal stops queued monitoring changes" }
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task WaitAsync(Func<bool> condition, string failure)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(5)) throw new IOException(failure);
            await Task.Delay(25);
        }
    }
}
