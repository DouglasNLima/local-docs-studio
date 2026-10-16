using LensDocsStudio.Core.Documents;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Core.Workspace;

/// <summary>Retains a durable snapshot and isolates the exact file before passing it to the Windows recycle operation.</summary>
public sealed class DocumentRemovalService(DocumentStore documents, DocumentSnapshotStore snapshots)
{
    public async Task<DocumentSnapshotEntry> RemoveAsync(WorkspacePath workspace, DocumentSnapshot expected,
        Func<string, Task> recycle, CancellationToken cancellationToken = default)
    {
        var path = workspace.Resolve(Path.GetRelativePath(workspace.Root, expected.Path));
        if (!WorkspaceService.IsDocument(path)) throw new IOException("Only a documentation file can be removed with this command.");
        if ((await documents.ReadAsync(path, cancellationToken)).Fingerprint != expected.Fingerprint)
            throw new DocumentConflictException("The file changed externally. Reload it before deleting it.");
        var retained = await snapshots.CaptureAsync(new(Guid.NewGuid(), path, expected.Text, expected.Fingerprint, expected.Utf8Bom, 0, 0),
            "Before deletion — saved file", path, workspace, cancellationToken);
        var holdingFolder = workspace.Resolve(Path.Combine(Path.GetDirectoryName(Path.GetRelativePath(workspace.Root, path))!, ".lens-delete-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(holdingFolder);
        var held = Path.Combine(holdingFolder, Path.GetFileName(path));
        var moved = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            WorkspacePath.RejectReparsePoints(path); WorkspacePath.RejectReparsePoints(held);
            File.Move(path, held, false); moved = true;
            if ((await documents.ReadAsync(held, cancellationToken)).Fingerprint != expected.Fingerprint)
                throw new DocumentConflictException("The file changed before deletion. Its current contents have been preserved.");
            cancellationToken.ThrowIfCancellationRequested();
            // Once the shell takes ownership, cancellation must be reported by that operation, not after it committed.
            await recycle(held);
            if (File.Exists(held)) throw new IOException("Windows did not remove the file. Its contents have been preserved.");
            return retained;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (moved && File.Exists(held))
            {
                try
                {
                    WorkspacePath.RejectReparsePoints(path); WorkspacePath.RejectReparsePoints(held);
                    File.Move(held, path, false);
                }
                catch (Exception restoreError) when (restoreError is IOException or UnauthorizedAccessException)
                { throw new IOException("Deletion did not finish and another file or lock prevented restoration. The original file remains at " + held + ". A recovery snapshot is also available.", error); }
            }
            throw;
        }
        finally
        {
            // This newly created folder is only removed when empty. Never recursively delete recovery files.
            try { WorkspacePath.RejectReparsePoints(holdingFolder); Directory.Delete(holdingFolder, false); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}
