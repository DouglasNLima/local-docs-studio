using System.Security.Cryptography;
using LensDocsStudio.Core.Documents;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Core.Workspace;

public sealed record FolderRemovalEntry(string RelativePath, bool IsDirectory, string? Fingerprint, DocumentSnapshot? Document);
public sealed record FolderRemovalPlan(string Source, IReadOnlyList<FolderRemovalEntry> Entries);

/// <summary>Review a complete folder inventory, retain documents and isolate it before Windows recycling.</summary>
public sealed class WorkspaceFolderRemovalService(DocumentStore documents, DocumentSnapshotStore snapshots, string privateDirectory)
{
    public async Task<FolderRemovalPlan> PlanAsync(WorkspacePath workspace, string source, CancellationToken cancellationToken = default)
    {
        source = ValidateSource(workspace, source);
        return new(source, await InventoryAsync(source, null, cancellationToken));
    }
    private string ValidateSource(WorkspacePath workspace, string source)
    {
        var safe = workspace.Resolve(Path.GetRelativePath(workspace.Root, source));
        if (!Directory.Exists(safe)) throw new IOException("The selected workspace folder is unavailable.");
        if (WorkspacePath.ContainsOrEquals(safe, privateDirectory)) throw new IOException("A folder containing private application state cannot be deleted.");
        return safe;
    }
    public async Task RemoveAsync(WorkspacePath workspace, FolderRemovalPlan plan, Func<string, Task> recycle, CancellationToken cancellationToken = default)
    {
        var source = ValidateSource(workspace, plan.Source);
        var locks = new List<FileStream>();
        string? holding = null; string? held = null; var moved = false;
        try
        {
            var current = await InventoryAsync(source, locks, cancellationToken);
            CheckInventory(plan.Entries, current);
            foreach (var entry in current.Where(entry => entry.Document is not null))
            {
                var document = entry.Document!;
                await snapshots.CaptureAsync(new(Guid.NewGuid(), document.Path, document.Text, document.Fingerprint, document.Utf8Bom, 0, 0),
                    "Before folder deletion — saved file", document.Path, workspace, cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            var relativeParent = Path.GetRelativePath(workspace.Root, Path.GetDirectoryName(source)!);
            holding = workspace.Resolve(Path.Combine(relativeParent, ".lens-delete-folder-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(holding);
            held = Path.Combine(holding, Path.GetFileName(source));
            // Windows cannot rename a directory while child handles remain open.
            ReleaseLocks();
            WorkspacePath.RejectReparsePoints(source); WorkspacePath.RejectReparsePoints(held);
            Directory.Move(source, held); moved = true;
            CheckInventory(plan.Entries, await InventoryAsync(held, locks, cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
            // Retain handles during verification; the shell must then own the complete directory move.
            ReleaseLocks();
            await recycle(held);
            if (Directory.Exists(held)) throw new IOException("Windows did not recycle the folder. Its contents have been preserved.");
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            ReleaseLocks();
            if (moved && held is not null && Directory.Exists(held))
            {
                try { WorkspacePath.RejectReparsePoints(source); WorkspacePath.RejectReparsePoints(held); Directory.Move(held, source); }
                catch (Exception restoreError) when (restoreError is IOException or UnauthorizedAccessException)
                { throw new IOException("Folder deletion did not finish and restoration was blocked. The retained folder remains at " + held + ". Document snapshots are also available.", error); }
            }
            throw;
        }
        finally
        {
            ReleaseLocks();
            if (holding is not null)
            {
                try { WorkspacePath.RejectReparsePoints(holding); Directory.Delete(holding, false); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
            }
        }
        void ReleaseLocks() { foreach (var file in locks) file.Dispose(); locks.Clear(); }
    }
    private static void CheckInventory(IReadOnlyList<FolderRemovalEntry> expected, IReadOnlyList<FolderRemovalEntry> actual)
    {
        if (expected.Count != actual.Count || !expected.Select(entry => (entry.RelativePath, entry.IsDirectory, entry.Fingerprint))
            .SequenceEqual(actual.Select(entry => (entry.RelativePath, entry.IsDirectory, entry.Fingerprint))))
            throw new DocumentConflictException("Folder contents changed after review. Refresh the workspace and review deletion again.");
    }
    private async Task<IReadOnlyList<FolderRemovalEntry>> InventoryAsync(string root, List<FileStream>? heldFiles, CancellationToken cancellationToken)
    {
        WorkspacePath.RejectReparsePoints(root);
        var result = new List<FolderRemovalEntry>();
        async Task Walk(string directory, int depth)
        {
            if (depth > 32) throw new IOException("Folder deletion is limited to 32 nested levels.");
            foreach (var path in Directory.EnumerateFileSystemEntries(directory).Order(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (result.Count >= 20000) throw new IOException("Folder deletion is limited to 20,000 reviewed entries.");
                WorkspacePath.RejectReparsePoints(path);
                var relative = Path.GetRelativePath(root, path);
                if (Directory.Exists(path)) { result.Add(new(relative, true, null, null)); await Walk(path, depth + 1); continue; }
                FileStream? file = new(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 65536, true);
                try
                {
                    var fingerprint = Convert.ToHexString(await SHA256.HashDataAsync(file, cancellationToken)).ToLowerInvariant();
                    var document = WorkspaceService.IsDocument(path) ? await documents.ReadAsync(path, cancellationToken) : null;
                    if (document is not null && !document.Fingerprint.Equals(fingerprint, StringComparison.OrdinalIgnoreCase))
                        throw new DocumentConflictException("A document changed while folder deletion was being planned.");
                    result.Add(new(relative, false, fingerprint, document));
                    if (heldFiles is not null) { heldFiles.Add(file); file = null; }
                }
                finally { file?.Dispose(); }
            }
        }
        await Walk(root, 0);
        return result.OrderBy(entry => entry.RelativePath, StringComparer.Ordinal).ToArray();
    }
}
