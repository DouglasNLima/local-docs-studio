using System.Windows.Threading;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Native.ViewModels;

public partial class WorkspaceViewModel
{
    private readonly Dispatcher workspaceDispatcher = Dispatcher.CurrentDispatcher;
    private CancellationTokenSource? treeRefresh;
    private bool disposed;

    private void StopMonitoring()
    {
        var previous = watcher; watcher = null;
        treeRefresh?.Cancel(); treeRefresh = null;
        if (previous is null) return;
        previous.EnableRaisingEvents = false;
        previous.Changed -= OnFileChanged; previous.Created -= OnFileChanged;
        previous.Deleted -= OnFileChanged; previous.Renamed -= OnFileChanged;
        previous.Error -= OnMonitoringError;
        previous.Dispose();
    }

    private void StartMonitoring(string root)
    {
        watcher = new FileSystemWatcher(root)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size
        };
        watcher.Changed += OnFileChanged; watcher.Created += OnFileChanged;
        watcher.Deleted += OnFileChanged; watcher.Renamed += OnFileChanged;
        watcher.Error += OnMonitoringError;
        watcher.EnableRaisingEvents = true;
        // Close the gap between the initial enumeration and enabling filesystem notifications.
        QueueTreeRefresh();
    }

    private void OnMonitoringError(object sender, ErrorEventArgs args) => workspaceDispatcher.BeginInvoke(() =>
    {
        if (disposed || !ReferenceEquals(sender, watcher)) return;
        Status = "Folder monitoring was interrupted. Reopen the workspace to resume.";
        QueueTreeRefresh();
    });

    private bool IsPrivatePath(string path) => WorkspacePath.ContainsOrEquals(StateDirectory, path);

    private void OnFileChanged(object sender, FileSystemEventArgs args)
    {
        if (workspaceDispatcher.HasShutdownStarted) return;
        workspaceDispatcher.BeginInvoke(async () =>
        {
            if (disposed || !ReferenceEquals(sender, watcher)) return;
            var oldPath = (args as RenamedEventArgs)?.OldFullPath;
            if (IsPrivatePath(args.FullPath) && (oldPath is null || IsPrivatePath(oldPath))) return;
            if (args.ChangeType != WatcherChangeTypes.Changed) QueueTreeRefresh();
            foreach (var document in Documents.Where(d => string.Equals(d.FilePath, args.FullPath, StringComparison.OrdinalIgnoreCase)
                || oldPath is not null && string.Equals(d.FilePath, oldPath, StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                var path = document.FilePath!;
                string? fingerprint = null;
                try { fingerprint = (await documents.ReadAsync(path)).Fingerprint; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                if (!disposed && ReferenceEquals(sender, watcher) && Documents.Contains(document) && document.FilePath == path)
                    document.ExternalChange = fingerprint is null || fingerprint != document.SavedSnapshot?.Fingerprint;
                RefreshTreeIndicators();
            }
        });
    }

    private void QueueTreeRefresh()
    {
        treeRefresh?.Cancel();
        if (disposed || WorkspaceRoot is not { } root || watcher is null) return;
        var cancellation = new CancellationTokenSource(); treeRefresh = cancellation;
        _ = RefreshTreeAsync(root, watcher, cancellation);
    }

    private async Task RefreshTreeAsync(string root, FileSystemWatcher owner, CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(250, cancellation.Token);
            var listing = await workspaces.EnumerateAsync(new(root), cancellation.Token, excludedDirectory: StateDirectory);
            cancellation.Token.ThrowIfCancellationRequested();
            if (disposed || !ReferenceEquals(owner, watcher) || WorkspaceRoot != root) return;
            WorkspaceEntryViewModel.Reconcile(Entries, listing.Entries);
            ApplyWorkspaceFilter();
            if (listing.Warnings.Count != 0) Status = string.Join(" ", listing.Warnings.Distinct());
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            if (!disposed && ReferenceEquals(owner, watcher) && !cancellation.IsCancellationRequested)
                Status = "The workspace tree could not be refreshed. Reopen the workspace when its folder is available.";
        }
        finally
        {
            if (ReferenceEquals(treeRefresh, cancellation)) treeRefresh = null;
            cancellation.Dispose();
        }
    }
}
