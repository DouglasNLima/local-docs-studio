using CommunityToolkit.Mvvm.Input;
using LensDocsStudio.Core.Documents;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native.ViewModels;

public partial class WorkspaceViewModel
{
    [RelayCommand] private Task DeleteFolderAsync(WorkspaceEntryViewModel? entry)
    {
        var root = WorkspaceRoot; var path = entry is { IsDirectory: true } ? entry.Path : null;
        return RunAsync(async () =>
        {
            if (root is null || path is null || WorkspaceRoot != root) throw new IOException("Select a folder in the current workspace before deleting it.");
            var workspace = new WorkspacePath(root); path = workspace.Resolve(Path.GetRelativePath(root, path));
            var open = Documents.Where(document => document.FilePath is not null && WorkspacePath.Contains(path, document.FilePath)).ToArray();
            foreach (var document in open) document.IsSaving = true;
            try
            {
                var removal = new WorkspaceFolderRemovalService(documents, Snapshots, StateDirectory);
                var plan = await Task.Run(() => removal.PlanAsync(workspace, path, OperationToken), OperationToken);
                foreach (var document in open)
                {
                    var expected = plan.Entries.SingleOrDefault(entry => entry.Document?.Path.Equals(document.FilePath, StringComparison.OrdinalIgnoreCase) == true)?.Document;
                    if (expected is null || document.SavedSnapshot?.Fingerprint != expected.Fingerprint)
                        throw new DocumentConflictException("An open document changed externally. Reload it before reviewing folder deletion: " + document.Title);
                }
                if (!dialogs.Confirm("Delete folder " + Path.GetFileName(path) + "?\n\n"
                    + plan.Entries.Count(entry => !entry.IsDirectory) + " files and " + plan.Entries.Count(entry => entry.IsDirectory)
                    + " subfolders will be sent to the Windows Recycle Bin. Saved documentation and unsaved open text will be retained in Document snapshots. Linked documents outside this folder will not be rewritten.",
                    "Delete workspace folder")) return;
                foreach (var document in open.Where(document => document.IsDirty))
                    await CaptureSnapshotAsync(document, "Before folder deletion — unsaved editor text");
                await removal.RemoveAsync(workspace, plan, RecycleService.RemoveFolderAsync, OperationToken);
                var selected = ActiveDocument;
                foreach (var document in open)
                { document.Edited -= OnEdited; DocumentRemoved?.Invoke(document.Id); Documents.Remove(document); }
                ActiveDocument = selected is not null && Documents.Contains(selected) ? selected : Documents.LastOrDefault();
                await RememberAsync(current => current with { RecentDocuments = current.RecentDocuments?.Where(recent => !WorkspacePath.Contains(path, recent)).ToArray() });
                await PersistSessionAsync(); await SetWorkspaceAsync(root);
                Status = "Folder recycled. Recover document text through File → Document snapshots; restore the complete folder through the Windows Recycle Bin.";
            }
            finally { foreach (var document in open) document.IsSaving = false; }
        });
    }
}
