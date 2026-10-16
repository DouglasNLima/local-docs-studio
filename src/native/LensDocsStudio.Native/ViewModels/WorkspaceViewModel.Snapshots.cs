using CommunityToolkit.Mvvm.Input;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native.ViewModels;

public partial class WorkspaceViewModel
{
    private DocumentSnapshotStore Snapshots => new(StateDirectory);
    public event Action<Guid>? DocumentRemoved;

    private Task<DocumentSnapshotEntry> CaptureSnapshotAsync(DocumentViewModel tab, string reason)
    {
        var recovered = tab.Recovery();
        var source = tab.EffectivePath;
        var workspace = tab.EffectiveWorkspace is { } root ? new WorkspacePath(root) : null;
        return Task.Run(() => Snapshots.CaptureAsync(recovered, reason, source, workspace, OperationToken), OperationToken);
    }

    [RelayCommand] private Task TakeSnapshotAsync()
    {
        var tab = ActiveDocument;
        return RunAsync(async () =>
        {
            if (tab is null || !Documents.Contains(tab)) return;
            var entry = await CaptureSnapshotAsync(tab, "Manual snapshot");
            var snapshot = await Snapshots.ReadAsync(entry, OperationToken);
            Status = snapshot.Warnings.Count == 0 ? "Snapshot retained, including local images." : "Snapshot retained. " + string.Join(" ", snapshot.Warnings);
        });
    }

    [RelayCommand] private Task BrowseSnapshotsAsync() => RunAsync(async () =>
    {
        var store = Snapshots;
        var listing = await Task.Run(() => store.ListAsync(OperationToken), OperationToken);
        if (dialogs.SelectSnapshot(store, listing) is not { } entry) return;
        var snapshot = await store.ReadAsync(entry, OperationToken);
        var tab = new DocumentViewModel(snapshot.Document.Text) { WorkspaceRoot = WorkspaceRoot, DisplayName = "Recovered — " + entry.Name };
        var warning = "";
        if (snapshot.PortableAssets)
        {
            try
            {
                EnsureDraftWorkspace(tab);
                var folder = Path.GetDirectoryName(entry.Path)!;
                await using var transfer = await new AssetTransferService().PrepareAsync(snapshot.PortableText, Path.Combine(folder, "snapshot.md"), new(folder),
                    tab.EffectivePath!, new(tab.AssetWorkspace!), OperationToken, forceCopyImages: true);
                tab.EditorDocument.Text = transfer.Text; transfer.Commit();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { warning = " The original source was recovered; some retained images could not be restored: " + ex.Message; }
        }
        tab.CaretOffset = Math.Clamp(snapshot.Document.CaretOffset, 0, tab.EditorDocument.TextLength);
        tab.VerticalOffset = Math.Max(0, snapshot.Document.VerticalOffset);
        Attach(tab); ActiveDocument = tab;
        await PersistSessionAsync();
        Status = "Opened a recovered copy. Use Save as to choose its destination." + warning;
    });

    [RelayCommand] private Task DeleteDocumentAsync()
    {
        var tab = ActiveDocument;
        return RunAsync(async () =>
        {
            if (tab is not { FilePath: not null, WorkspaceRoot: not null, SavedSnapshot: not null } || !Documents.Contains(tab))
                throw new IOException("Choose a saved workspace document to delete.");
            if (!dialogs.Confirm("Delete " + Path.GetFileName(tab.FilePath) + "?\n\nThe saved source and any unsaved editor text will be retained in Document snapshots. Windows will be asked to recycle the file. Linked documents and image files will stay in the workspace.", "Delete document")) return;
            tab.IsSaving = true;
            try
            {
                if (tab.IsDirty) await CaptureSnapshotAsync(tab, "Before deletion — unsaved editor text");
                await new DocumentRemovalService(documents, Snapshots).RemoveAsync(new(tab.WorkspaceRoot), tab.SavedSnapshot, RecycleService.RemoveAsync, OperationToken);
                tab.Edited -= OnEdited;
                DocumentRemoved?.Invoke(tab.Id);
                Documents.Remove(tab);
                if (ActiveDocument == tab) ActiveDocument = Documents.LastOrDefault();
                await RememberAsync(p => p with { RecentDocuments = p.RecentDocuments?.Where(p => !p.Equals(tab.FilePath, StringComparison.OrdinalIgnoreCase)).ToArray() });
                await PersistSessionAsync();
                if (WorkspaceRoot is not null) await SetWorkspaceAsync(WorkspaceRoot);
                Status = "Deleted " + Path.GetFileName(tab.FilePath) + ". Recover its text through File → Document snapshots.";
            }
            finally { tab.IsSaving = false; }
        });
    }
}
