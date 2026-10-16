using CommunityToolkit.Mvvm.Input;
using LensDocsStudio.Core.Documents;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native.ViewModels;

public partial class WorkspaceViewModel
{
    [RelayCommand] private Task BrowseAssetsAsync() => RunAsync(async () =>
    {
        if (WorkspaceRoot is not { } root) throw new IOException("Open a workspace folder to manage its images.");
        var workspace = new WorkspacePath(root);
        if (root.Equals(StateDirectory, StringComparison.OrdinalIgnoreCase) || WorkspacePath.Contains(StateDirectory, root))
            throw new IOException("Choose a documentation workspace outside native application state.");
        var service = new WorkspaceAssetService(documents, StateDirectory);
        while (!OperationToken.IsCancellationRequested)
        {
            var captured = Documents.Where(d => d.FilePath is not null && WorkspacePath.Contains(root, d.FilePath))
                .Select(d => new WorkspaceDocumentSource(d.FilePath!, d.EditorDocument.Text, d.IsDirty)).ToArray();
            var audit = await service.AuditAsync(workspace, captured, OperationToken);
            if (dialogs.SelectAssetAction(workspace, audit) is not { } selection) return;
            if (selection.Action == "refresh" || selection.Asset is not { } asset) continue;
            // Save all open workspace drafts before planning: a newly typed reference may be absent from disk.
            var tabs = Documents.Where(d => d.FilePath is not null && WorkspacePath.Contains(root, d.FilePath)).ToArray();
            var dirty = tabs.Where(d => d.IsDirty).ToArray();
            if (dirty.Length != 0)
            {
                if (!dialogs.Confirm("Save open workspace changes before reviewing this image operation?\n\n" + string.Join("\n", dirty.Select(d => d.Title)), "Save before image changes")) continue;
                var saved = true;
                foreach (var tab in dirty) if (!await SaveTabAsync(tab, false)) { saved = false; break; }
                if (!saved) continue;
            }
            foreach (var tab in tabs) tab.IsSaving = true;
            try
            {
                var mutation = new WorkspaceAssetMutationService(documents, Snapshots, Path.Combine(StateDirectory, "move-recovery"));
                if (selection.Action == "rename")
                {
                    var destination = dialogs.WorkspacePathPrompt("Rename or move image", "Destination inside " + root, asset.RelativePath, "Review changes");
                    if (destination is null) continue;
                    var plan = await mutation.PlanMoveAsync(workspace, asset.Path, destination, OperationToken);
                    if (!dialogs.ReviewAssetMove(workspace, plan)) continue;
                    foreach (var tab in tabs.Where(t => plan.Documents.Any(c => c.Original.Path.Equals(t.FilePath, StringComparison.OrdinalIgnoreCase))))
                    {
                        var expected = plan.Documents.First(c => c.Original.Path.Equals(tab.FilePath, StringComparison.OrdinalIgnoreCase)).Original;
                        if (tab.IsDirty || tab.SavedSnapshot?.Fingerprint != expected.Fingerprint || tab.EditorDocument.Text != expected.Text)
                            throw new DocumentConflictException("An open document changed. Save or reload it before renaming its image: " + tab.Title);
                    }
                    var updated = await mutation.MoveAsync(workspace, plan, OperationToken);
                    foreach (var snapshot in updated)
                    {
                        var tab = tabs.FirstOrDefault(t => snapshot.Path.Equals(t.FilePath, StringComparison.OrdinalIgnoreCase));
                        if (tab is null) continue;
                        tab.EditorDocument.Replace(0, tab.EditorDocument.TextLength, snapshot.Text); tab.MarkSaved(snapshot);
                    }
                    await PersistSessionAsync(); DocumentChanged?.Invoke(this, EventArgs.Empty);
                    Status = "Renamed image; updated " + updated.Count + " documents. A recovery image is retained in Document snapshots.";
                }
                else if (selection.Action == "remove")
                {
                    var expected = await WorkspaceAssetMutationService.ReadAsync(asset.Path, OperationToken);
                    if (!dialogs.Confirm("Remove " + asset.RelativePath + "?\n\nDocumentation references will be checked again. Other file types and excluded folders are not checked. An independent image copy will be kept in Document snapshots before Windows is asked to recycle the file.", "Remove unreferenced image")) continue;
                    await mutation.RemoveAsync(workspace, expected, RecycleService.RemoveAsync, OperationToken);
                    Status = "Image removed. Recover its independent copy through File → Document snapshots.";
                }
            }
            finally { foreach (var tab in tabs) tab.IsSaving = false; }
        }
    });
}
