using CommunityToolkit.Mvvm.Input;
using LensDocsStudio.Core.Documents;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Native.ViewModels;

public partial class WorkspaceViewModel
{
    [RelayCommand] private Task MoveFolderAsync(WorkspaceEntryViewModel? entry)
    {
        var root = WorkspaceRoot; var selected = entry is { IsDirectory: true } ? entry.Path : null;
        return RunAsync(async () =>
        {
            if (root is null || selected is null || WorkspaceRoot != root) throw new IOException("Select a folder in the current workspace tree before moving it.");
            var workspace = new WorkspacePath(root);
            var source = workspace.Resolve(Path.GetRelativePath(root, selected));
            var relative = dialogs.WorkspacePathPrompt("Rename or move folder", "Destination inside " + root,
                Path.GetRelativePath(root, source), "Review folder changes");
            if (relative is null) return;
            var open = Documents.Where(document => document.FilePath is not null && WorkspacePath.Contains(root, document.FilePath)).ToArray();
            var dirty = open.Where(document => document.IsDirty).ToArray();
            if (dirty.Length > 0)
            {
                if (!dialogs.Confirm("Save these workspace documents before reviewing the folder move? Current links must be on disk for the reviewed transaction.\n\n"
                    + string.Join("\n", dirty.Select(document => document.Title)), "Save before folder move")) return;
                foreach (var document in dirty) if (!await SaveTabAsync(document, false)) return;
            }
            if (open.Any(document => document.IsDirty || !Documents.Contains(document))) throw new DocumentConflictException("A workspace tab changed. Save it and review the folder move again.");
            var captured = open.Select(document => (Document: document, Path: document.FilePath!, Text: document.EditorDocument.Text, Snapshot: document.SavedSnapshot)).ToArray();
            foreach (var document in open) document.IsSaving = true;
            try
            {
                Status = "Reviewing folder contents and document links…";
                var mover = new WorkspaceFolderMoveService(documents, Path.Combine(StateDirectory, "move-recovery"), StateDirectory);
                var plan = await Task.Run(() => mover.PlanAsync(workspace, source, relative, OperationToken), OperationToken);
                OperationToken.ThrowIfCancellationRequested();
                if (Changed()) throw new DocumentConflictException("The workspace changed while planning the folder move. Review it again.");
                var updateLinks = dialogs.ReviewFolderMove(workspace, plan); if (updateLinks is null) return;
                if (Changed()) throw new DocumentConflictException("The workspace changed during folder review. Review it again.");
                foreach (var item in captured)
                {
                    var change = plan.Documents.FirstOrDefault(change => change.Original.Path.Equals(item.Path, StringComparison.OrdinalIgnoreCase));
                    if (change is not null && (item.Snapshot?.Fingerprint != change.Original.Fingerprint || item.Text != change.Original.Text))
                        throw new DocumentConflictException("An affected document changed externally. Reload it before moving the folder: " + item.Document.Title);
                }
                Status = "Moving folder and applying reviewed links…";
                var result = await Task.Run(() => mover.ApplyAsync(workspace, plan, updateLinks.Value, OperationToken), OperationToken);
                foreach (var item in captured)
                {
                    var nextPath = WorkspaceFolderMoveService.Relocate(item.Path, plan.Source, plan.Destination);
                    var saved = result.Documents.FirstOrDefault(snapshot => snapshot.Path.Equals(nextPath, StringComparison.OrdinalIgnoreCase));
                    if (saved is null) continue;
                    if (item.Document.EditorDocument.Text != saved.Text) item.Document.EditorDocument.Replace(0, item.Document.EditorDocument.TextLength, saved.Text);
                    item.Document.MarkSaved(saved);
                    if (item.Path != saved.Path) DocumentRelocated?.Invoke(item.Document.Id, saved.Path);
                }
                try
                {
                    await RememberAsync(preferences => preferences with
                    {
                        RecentDocuments = preferences.RecentDocuments?.Select(path => WorkspaceFolderMoveService.Relocate(path, plan.Source, plan.Destination)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                        RecentWorkspaces = preferences.RecentWorkspaces?.Select(path => WorkspaceFolderMoveService.Relocate(path, plan.Source, plan.Destination)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                    });
                    await PersistSessionAsync(); await SetWorkspaceAsync(root); DocumentChanged?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or OperationCanceledException)
                { throw new IOException("The folder was moved, but session state or the workspace view could not be refreshed. Open the destination folder before continuing. " + error.Message, error); }
                Status = "Moved folder " + Path.GetFileName(plan.Destination) + "; files: " + plan.Entries.Count(item => !item.IsDirectory)
                    + "; documents with updated links: " + (updateLinks.Value ? plan.Documents.Count(item => item.Original.Text != item.UpdatedText) : 0) + ".";
                bool Changed() => WorkspaceRoot != root || captured.Any(item => !Documents.Contains(item.Document) || item.Document.FilePath != item.Path
                    || item.Document.EditorDocument.Text != item.Text || item.Document.SavedSnapshot != item.Snapshot);
            }
            finally { foreach (var document in open) document.IsSaving = false; }
        });
    }
}
