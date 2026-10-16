using System.Security.Cryptography;
using System.Text.Json;
using LensDocsStudio.Core.Documents;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Core.Workspace;

public sealed record AssetFileSnapshot(string Path, string Fingerprint, long Bytes);
public sealed record WorkspaceAssetMovePlan(AssetFileSnapshot Source, string Destination, IReadOnlyList<DocumentLinkChange> Documents);
public sealed record AssetMoveJournal(int SchemaVersion, WorkspaceAssetMovePlan Plan, string RetainedImage, DateTimeOffset CreatedAt);

/// <summary>Reviewed asset operations retain independent recovery bytes and never replace destination files.</summary>
public sealed class WorkspaceAssetMutationService(DocumentStore documents, DocumentSnapshotStore snapshots, string recoveryDirectory)
{
    private string NativeState => Path.GetDirectoryName(snapshots.DirectoryPath)!;
    public static async Task<AssetFileSnapshot> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        WorkspacePath.RejectReparsePoints(path);
        if (!WorkspaceService.IsRasterImage(path)) throw new IOException("Choose a supported local raster image.");
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
        if (file.Length > 32 * 1024 * 1024) throw new IOException("The image exceeds the 32 MB asset operation limit.");
        return new(path, Convert.ToHexString(await SHA256.HashDataAsync(file, cancellationToken)), file.Length);
    }

    public async Task<WorkspaceAssetMovePlan> PlanMoveAsync(WorkspacePath workspace, string path, string destination, CancellationToken cancellationToken = default)
    {
        path = workspace.Resolve(Path.GetRelativePath(workspace.Root, path));
        destination = workspace.Resolve(destination);
        ValidatePath(workspace, path); ValidatePath(workspace, destination);
        if (!Path.GetExtension(path).Equals(Path.GetExtension(destination), StringComparison.OrdinalIgnoreCase)) throw new IOException("Keep the existing image extension when renaming an asset.");
        if (path.Equals(destination, StringComparison.OrdinalIgnoreCase)) throw new IOException("Choose a different image filename or folder.");
        if (File.Exists(destination) || Directory.Exists(destination)) throw new IOException("The destination already exists; no image will be replaced.");
        if (!Directory.Exists(Path.GetDirectoryName(destination))) throw new IOException("Create the destination folder before moving the image.");
        var source = await ReadAsync(path, cancellationToken);
        var audit = await new WorkspaceAssetService(documents, NativeState).AuditAsync(workspace, cancellationToken: cancellationToken);
        if (!audit.UsageComplete) throw new IOException("Image usage checking is incomplete. Resolve the audit warnings before renaming an image.");
        var asset = audit.Assets.FirstOrDefault(a => a.Path.Equals(path, StringComparison.OrdinalIgnoreCase)) ?? throw new IOException("The selected image is outside the audited folders.");
        if (asset.Uses.Any(u => !u.Markdown)) throw new IOException("This image is referenced in embedded HTML. Update those references manually before renaming the image.");
        var changes = new List<DocumentLinkChange>();
        foreach (var documentPath in asset.Uses.Select(u => u.DocumentPath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var original = await documents.ReadAsync(documentPath, cancellationToken);
            var replacements = new List<(MarkdownLink Link, string Reference)>();
            foreach (var link in await Task.Run(() => MarkdownLinks.Read(original.Text), cancellationToken))
            {
                if (Uri.TryCreate(link.Reference, UriKind.Absolute, out _) || link.Reference.StartsWith('#')) continue;
                if (!workspace.ResolveLink(documentPath, link.Reference).Equals(path, StringComparison.OrdinalIgnoreCase)) continue;
                var fragment = link.Reference.Contains('#') ? "#" + link.Reference.Split('#', 2)[1] : "";
                replacements.Add((link, MarkdownLinks.RelativeReference(documentPath, destination) + fragment));
            }
            var updated = MarkdownLinks.Rewrite(original.Text, replacements);
            if (updated != original.Text) changes.Add(new(original, updated, replacements.Count));
        }
        return new(source, destination, changes);
    }

    public async Task<IReadOnlyList<DocumentSnapshot>> MoveAsync(WorkspacePath workspace, WorkspaceAssetMovePlan plan, CancellationToken cancellationToken = default)
    {
        ValidatePath(workspace, plan.Source.Path); ValidatePath(workspace, plan.Destination);
        if (File.Exists(plan.Destination) || Directory.Exists(plan.Destination)) throw new IOException("The destination appeared after review; no file was replaced.");
        // Rebuild the plan to catch newly added references as well as edits to already affected documents.
        var latest = await PlanMoveAsync(workspace, plan.Source.Path, Path.GetRelativePath(workspace.Root, plan.Destination), cancellationToken);
        if (latest.Source != plan.Source || !latest.Documents.SequenceEqual(plan.Documents)) throw new DocumentConflictException("Image references or contents changed after review. Refresh and review the rename again.");
        if (plan.Documents.Sum(d => (long)d.Original.Text.Length + d.UpdatedText.Length) > 32 * 1024 * 1024) throw new IOException("The image rename exceeds the recovery text limit.");
        var retained = await CaptureImageAsync(workspace, plan.Source, "Before image rename", cancellationToken);
        var folder = Path.GetFullPath(recoveryDirectory);
        WorkspacePath.RejectReparsePoints(folder); Directory.CreateDirectory(folder);
        var journalPath = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".asset.json");
        var journalBytes = await Task.Run(() => JsonSerializer.SerializeToUtf8Bytes(new AssetMoveJournal(1, plan, retained.Path, DateTimeOffset.UtcNow)), cancellationToken);
        if (journalBytes.Length > 128 * 1024 * 1024) throw new IOException("The image rename exceeds the recovery record limit.");
        await DocumentStore.WriteDurableAsync(journalPath, journalBytes, cancellationToken);
        var completed = new List<(DocumentSnapshot Original, DocumentSnapshot Saved)>();
        var moved = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidatePath(workspace, plan.Source.Path); ValidatePath(workspace, plan.Destination);
            latest = await PlanMoveAsync(workspace, plan.Source.Path, Path.GetRelativePath(workspace.Root, plan.Destination), cancellationToken);
            if (latest.Source != plan.Source || !latest.Documents.SequenceEqual(plan.Documents)) throw new DocumentConflictException("Image references or contents changed while recovery was being retained. Review the operation again.");
            File.Move(plan.Source.Path, plan.Destination, false); moved = true;
            if ((await ReadAsync(plan.Destination, cancellationToken)).Fingerprint != plan.Source.Fingerprint)
                throw new DocumentConflictException("The image changed before its move. Its current bytes have been preserved.");
            foreach (var change in plan.Documents)
            {
                ValidatePath(workspace, change.Original.Path);
                var saved = await documents.SaveAsync(change.Original.Path, change.UpdatedText, change.Original, cancellationToken);
                completed.Add((change.Original, saved));
            }
            File.Delete(journalPath);
            return completed.Select(c => c.Saved).ToArray();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            var restored = true;
            foreach (var (original, saved) in completed.AsEnumerable().Reverse())
            {
                try { await documents.SaveAsync(original.Path, original.Text, saved, CancellationToken.None); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { restored = false; }
            }
            if (moved)
            {
                try
                {
                    ValidatePath(workspace, plan.Source.Path); ValidatePath(workspace, plan.Destination);
                    if (File.Exists(plan.Source.Path) || (await ReadAsync(plan.Destination)).Fingerprint != plan.Source.Fingerprint) restored = false;
                    else File.Move(plan.Destination, plan.Source.Path, false);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { restored = false; }
            }
            if (restored) { File.Delete(journalPath); throw; }
            throw new IOException("The image rename could not be fully reversed. Current files were preserved. Original and planned text remain in " + journalPath + "; the image is retained in Document snapshots.", error);
        }
    }

    public async Task<DocumentSnapshotEntry> RemoveAsync(WorkspacePath workspace, AssetFileSnapshot expected, Func<string, Task> recycle,
        CancellationToken cancellationToken = default)
    {
        ValidatePath(workspace, expected.Path);
        await CheckUnusedAsync();
        var retained = await CaptureImageAsync(workspace, expected, "Before image removal", cancellationToken);
        await CheckUnusedAsync();
        var holdingFolder = workspace.Resolve(Path.Combine(Path.GetDirectoryName(Path.GetRelativePath(workspace.Root, expected.Path))!, ".lens-delete-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(holdingFolder);
        var held = Path.Combine(holdingFolder, Path.GetFileName(expected.Path));
        var moved = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidatePath(workspace, expected.Path); ValidatePath(workspace, held);
            File.Move(expected.Path, held, false); moved = true;
            if ((await ReadAsync(held, cancellationToken)).Fingerprint != expected.Fingerprint) throw new DocumentConflictException("The image changed before removal. Its current bytes have been preserved.");
            cancellationToken.ThrowIfCancellationRequested();
            await recycle(held);
            if (File.Exists(held)) throw new IOException("Windows did not remove the image.");
            return retained;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (moved && File.Exists(held))
            {
                try { ValidatePath(workspace, expected.Path); ValidatePath(workspace, held); File.Move(held, expected.Path, false); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { throw new IOException("Removal did not finish. The image remains at " + held + ". An independent recovery copy is also in Document snapshots.", error); }
            }
            throw;
        }
        finally
        {
            try { WorkspacePath.RejectReparsePoints(holdingFolder); Directory.Delete(holdingFolder, false); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }

        async Task CheckUnusedAsync()
        {
            var audit = await new WorkspaceAssetService(documents, NativeState).AuditAsync(workspace, cancellationToken: cancellationToken);
            var asset = audit.Assets.FirstOrDefault(a => a.Path.Equals(expected.Path, StringComparison.OrdinalIgnoreCase));
            if (!audit.UsageComplete || asset is null || asset.Uses.Count != 0) throw new IOException("The image is referenced or its usage could not be checked completely. Nothing was removed.");
            if (await ReadAsync(expected.Path, cancellationToken) != expected) throw new DocumentConflictException("The image changed after selection. Refresh the image list before removing it.");
        }
    }

    private async Task<DocumentSnapshotEntry> CaptureImageAsync(WorkspacePath workspace, AssetFileSnapshot expected, string reason, CancellationToken cancellationToken)
    {
        var source = workspace.Resolve("lens-image-recovery.md");
        var markdown = "![Recovered image](" + MarkdownLinks.RelativeReference(source, expected.Path) + ")\n";
        var entry = await snapshots.CaptureAsync(new(Guid.NewGuid(), null, markdown, null, false, 0, 0, DisplayName: "Image — " + Path.GetFileName(expected.Path)), reason, source, workspace, cancellationToken);
        var record = await snapshots.ReadAsync(entry, cancellationToken);
        if (!record.PortableAssets) throw new IOException("The recovery image could not be copied. The original image was preserved.");
        var retainedWorkspace = new WorkspacePath(Path.GetDirectoryName(entry.Path)!);
        var reference = MarkdownLinks.Read(record.PortableText).Single(l => l.IsImage).Reference;
        var retainedImage = retainedWorkspace.ResolveLink(Path.Combine(retainedWorkspace.Root, "snapshot.md"), reference);
        if ((await ReadAsync(retainedImage, cancellationToken)).Fingerprint != expected.Fingerprint || (await ReadAsync(expected.Path, cancellationToken)).Fingerprint != expected.Fingerprint)
            throw new DocumentConflictException("The image changed while its recovery copy was being retained. No image was moved or removed.");
        return entry;
    }

    private void ValidatePath(WorkspacePath workspace, string path)
    {
        workspace.Resolve(Path.GetRelativePath(workspace.Root, path));
        if (WorkspacePath.Contains(NativeState, path)) throw new IOException("Native recovery and application state cannot be changed through workspace image management.");
    }
}
