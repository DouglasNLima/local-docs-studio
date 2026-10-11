using CommunityToolkit.Mvvm.Input;
using LensDocsStudio.Core.Documents;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Native.ViewModels;

public partial class WorkspaceViewModel
{
    [RelayCommand] private Task CreateFileAsync() => RunAsync(async () =>
    {
        if (WorkspaceRoot is not { } root) throw new IOException("Open a workspace folder before creating a file.");
        var relative = dialogs.WorkspacePathPrompt("Create workspace file", "New document path", "new-document.md", "Create file");
        if (relative is null) return;
        var workspace = new WorkspacePath(root);
        var path = workspace.Resolve(relative);
        if (!WorkspaceService.IsDocument(path)) throw new IOException("Use a Markdown, Mermaid or text file extension.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await documents.SaveAsync(path, "", null, OperationToken);
        await SetWorkspaceAsync(root); await OpenPathAsync(path); await PersistSessionAsync();
        Status = "Created " + relative;
    });

    [RelayCommand] private Task CreateFolderAsync() => RunAsync(async () =>
    {
        if (WorkspaceRoot is not { } root) throw new IOException("Open a workspace before creating a folder.");
        var relative = dialogs.WorkspacePathPrompt("Create workspace folder", "New folder path", "docs", "Create folder");
        if (relative is null) return;
        var path = new WorkspacePath(root).Resolve(relative);
        if (Directory.Exists(path) || File.Exists(path)) throw new IOException("That path already exists.");
        Directory.CreateDirectory(path);
        await SetWorkspaceAsync(root); Status = "Created folder " + relative;
    });

    [RelayCommand] private Task MoveDocumentAsync()
    {
        var tab = ActiveDocument;
        return RunAsync(async () =>
        {
            if (tab is not { FilePath: not null, WorkspaceRoot: not null } || !Documents.Contains(tab)) throw new IOException("Save a document before renaming or moving it.");
            if (tab.IsDirty)
            {
                if (!dialogs.Confirm("Save this document's changes before planning its move?", "Save before move") || !await SaveTabAsync(tab, false)) return;
            }
            var workspace = new WorkspacePath(tab.WorkspaceRoot);
            var relative = dialogs.WorkspacePathPrompt("Rename or move document", "Destination inside " + workspace.Root,
                Path.GetRelativePath(workspace.Root, tab.FilePath), "Review changes");
            if (relative is null) return;
            var mover = new WorkspaceMoveService(documents, Path.Combine(StateDirectory, "move-recovery"));
            var plan = await mover.PlanAsync(workspace, tab.FilePath, relative, OperationToken);
            if (tab.SavedSnapshot?.Fingerprint != plan.Source.Fingerprint) throw new DocumentConflictException("This document changed externally. Reload it before moving it.");
            var updateLinks = dialogs.ReviewMove(workspace, plan);
            if (updateLinks is null) return;
            var dirty = updateLinks.Value ? Documents.Where(d => d.IsDirty && plan.OtherDocuments.Any(c => c.Original.Path.Equals(d.FilePath, StringComparison.OrdinalIgnoreCase))).ToArray() : [];
            if (dirty.Length != 0)
            {
                if (!dialogs.Confirm("Save these affected documents before reviewing link changes?\n\n" + string.Join("\n", dirty.Select(d => d.Title)), "Unsaved linked documents")) return;
                foreach (var document in dirty) if (!await SaveTabAsync(document, false)) return;
                plan = await mover.PlanAsync(workspace, tab.FilePath, relative, OperationToken);
                updateLinks = dialogs.ReviewMove(workspace, plan);
                if (updateLinks is null) return;
            }
            if (tab.SavedSnapshot?.Fingerprint != plan.Source.Fingerprint) throw new DocumentConflictException("This document changed externally. Reload it before moving it.");
            var affected = Documents.Where(d => d == tab || updateLinks.Value && plan.OtherDocuments.Any(c => c.Original.Path.Equals(d.FilePath, StringComparison.OrdinalIgnoreCase))).ToArray();
            foreach (var document in affected)
            {
                var expected = document == tab ? plan.Source : plan.OtherDocuments.First(c => c.Original.Path.Equals(document.FilePath, StringComparison.OrdinalIgnoreCase)).Original;
                if (document.IsDirty || document.SavedSnapshot?.Fingerprint != expected.Fingerprint || document.EditorDocument.Text != expected.Text)
                    throw new DocumentConflictException("An affected tab changed while the move was being planned. Save or reload it and review the move again: " + document.Title);
            }
            foreach (var document in affected) document.IsSaving = true;
            try
            {
                var result = await mover.ApplyAsync(workspace, plan, updateLinks.Value, OperationToken);
                foreach (var document in affected)
                {
                    var snapshot = document == tab ? result.Moved : result.UpdatedDocuments.First(s => s.Path.Equals(document.FilePath, StringComparison.OrdinalIgnoreCase));
                    if (document.EditorDocument.Text != snapshot.Text) document.EditorDocument.Replace(0, document.EditorDocument.TextLength, snapshot.Text);
                    document.MarkSaved(snapshot);
                }
                DocumentRelocated?.Invoke(tab.Id, result.Moved.Path);
                await RememberAsync(p => p with { RecentDocuments = LensDocsStudio.Core.State.RecentPaths.Touch(p.RecentDocuments?.Where(p => !p.Equals(plan.Source.Path, StringComparison.OrdinalIgnoreCase)), result.Moved.Path) });
                await PersistSessionAsync();
                if (WorkspaceRoot == workspace.Root) await SetWorkspaceAsync(workspace.Root);
                DocumentChanged?.Invoke(this, EventArgs.Empty);
                Status = "Moved " + Path.GetFileName(result.Moved.Path) + "; updated " + result.UpdatedDocuments.Count + " linked documents.";
            }
            finally { foreach (var document in affected) document.IsSaving = false; }
        });
    }
}
