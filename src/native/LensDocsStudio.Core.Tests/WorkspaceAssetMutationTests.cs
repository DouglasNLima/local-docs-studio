using System.Text.Json;
using LensDocsStudio.Core.Documents;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Core.Tests;

public sealed class WorkspaceAssetMutationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "lens-asset-changes-" + Guid.NewGuid().ToString("N"));
    private string WorkspaceRoot => Path.Combine(root, "workspace");
    private string State => Path.Combine(root, "state");
    private string Image => Path.Combine(WorkspaceRoot, "assets", "original image.png");
    private string Guide => Path.Combine(WorkspaceRoot, "guide.md");
    private string JournalDirectory => Path.Combine(State, "move-recovery");
    private DocumentSnapshotStore Snapshots => new(State);
    private WorkspaceAssetMutationService Service => new(new(Path.Combine(State, "writes")), Snapshots, JournalDirectory);
    public WorkspaceAssetMutationTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Image)!);
        File.WriteAllBytes(Image, [0, 255, 31, 10, 0, 99]);
    }

    [Fact] public async Task Image_move_preserves_binary_bytes_and_source_formatting_with_reference_links_and_fragments()
    {
        const string original = "# Images\r\n\r\n![First][picture]\r\n![Again][picture]\r\n\r\n[picture]: assets/original%20image.png#view \"Caption\"\r\n\r\n```md\n![Example](assets/original%20image.png)\n```\n";
        await File.WriteAllTextAsync(Guide, original);
        var expected = await File.ReadAllBytesAsync(Image);
        var plan = await Service.PlanMoveAsync(new(WorkspaceRoot), Image, "assets/renamed image.png");
        Assert.Single(plan.Documents);
        var updated = await Service.MoveAsync(new(WorkspaceRoot), plan);
        Assert.Single(updated); Assert.False(File.Exists(Image)); Assert.Equal(expected, await File.ReadAllBytesAsync(plan.Destination));
        var text = await File.ReadAllTextAsync(Guide);
        Assert.Contains("[picture]: assets/renamed%20image.png#view \"Caption\"\r\n", text);
        Assert.Contains("```md\n![Example](assets/original%20image.png)\n```", text);
        Assert.Empty(Directory.GetFiles(JournalDirectory));
        Assert.Equal(expected, await RetainedImageAsync(Assert.Single((await Snapshots.ListAsync()).Entries)));
    }

    [Fact] public async Task New_references_after_review_require_a_new_plan_without_moving_or_rewriting_files()
    {
        await File.WriteAllTextAsync(Guide, "![Image](assets/original%20image.png)");
        var plan = await Service.PlanMoveAsync(new(WorkspaceRoot), Image, "assets/renamed.png");
        var added = Path.Combine(WorkspaceRoot, "new-reference.md");
        await File.WriteAllTextAsync(added, "[Download](assets/original%20image.png)");
        await Assert.ThrowsAsync<DocumentConflictException>(() => Service.MoveAsync(new(WorkspaceRoot), plan));
        Assert.True(File.Exists(Image)); Assert.False(File.Exists(plan.Destination));
        Assert.Equal("[Download](assets/original%20image.png)", await File.ReadAllTextAsync(added));
    }

    [Fact] public async Task Changed_image_and_destination_collisions_are_preserved()
    {
        var plan = await Service.PlanMoveAsync(new(WorkspaceRoot), Image, "assets/renamed.png");
        await File.WriteAllBytesAsync(Image, [3, 4, 5]);
        await Assert.ThrowsAsync<DocumentConflictException>(() => Service.MoveAsync(new(WorkspaceRoot), plan));
        await File.WriteAllBytesAsync(plan.Destination, [9]);
        await Assert.ThrowsAsync<IOException>(() => Service.MoveAsync(new(WorkspaceRoot), plan));
        Assert.Equal(new byte[] { 3, 4, 5 }, await File.ReadAllBytesAsync(Image)); Assert.Equal(new byte[] { 9 }, await File.ReadAllBytesAsync(plan.Destination));
    }

    [Fact] public async Task Locked_linked_document_reverses_completed_edits_and_the_image_move()
    {
        const string source = "![Image](assets/original%20image.png)";
        await File.WriteAllTextAsync(Guide, source);
        var second = Path.Combine(WorkspaceRoot, "z-locked.md");
        await File.WriteAllTextAsync(second, source);
        var plan = await Service.PlanMoveAsync(new(WorkspaceRoot), Image, "assets/renamed.png");
        // Reading remains possible for revalidation; writing must fail after the first document was updated.
        using var held = new FileStream(second, FileMode.Open, FileAccess.Read, FileShare.Read);
        await Assert.ThrowsAsync<IOException>(() => Service.MoveAsync(new(WorkspaceRoot), plan));
        Assert.True(File.Exists(Image)); Assert.False(File.Exists(plan.Destination)); Assert.Equal(source, await File.ReadAllTextAsync(Guide));
        Assert.Empty(Directory.GetFiles(JournalDirectory));
    }

    [Theory]
    [InlineData("<img src=\"assets/original%20image.png\">")]
    [InlineData("---\ncover: assets/original%20image.png\n---\n")]
    public async Task Opaque_or_HTML_references_prevent_automatic_asset_rename(string source)
    {
        await File.WriteAllTextAsync(Guide, source);
        await Assert.ThrowsAsync<IOException>(() => Service.PlanMoveAsync(new(WorkspaceRoot), Image, "assets/renamed.png"));
        Assert.True(File.Exists(Image));
    }

    [Fact] public async Task Unreferenced_image_removal_retains_independent_exact_bytes_for_the_snapshot_browser()
    {
        var expected = await WorkspaceAssetMutationService.ReadAsync(Image);
        var bytes = await File.ReadAllBytesAsync(Image);
        var entry = await Service.RemoveAsync(new(WorkspaceRoot), expected, path => { File.Delete(path); return Task.CompletedTask; });
        Assert.False(File.Exists(Image)); Assert.Equal(bytes, await RetainedImageAsync(entry));
        Assert.Contains("Before image removal", entry.Reason);
    }

    [Fact] public async Task A_new_reference_prevents_removal_and_never_calls_the_shell()
    {
        var expected = await WorkspaceAssetMutationService.ReadAsync(Image);
        await File.WriteAllTextAsync(Guide, "![Now needed](assets/original%20image.png)");
        var called = false;
        await Assert.ThrowsAsync<IOException>(() => Service.RemoveAsync(new(WorkspaceRoot), expected, _ => { called = true; return Task.CompletedTask; }));
        Assert.False(called); Assert.True(File.Exists(Image));
    }

    [Fact] public async Task Cancelled_shell_removal_restores_the_original_image()
    {
        var expected = await WorkspaceAssetMutationService.ReadAsync(Image);
        await Assert.ThrowsAsync<OperationCanceledException>(() => Service.RemoveAsync(new(WorkspaceRoot), expected, _ => throw new OperationCanceledException()));
        Assert.Equal(expected, await WorkspaceAssetMutationService.ReadAsync(Image));
        Assert.Empty(Directory.GetDirectories(Path.GetDirectoryName(Image)!, ".lens-delete-*"));
    }

    [Fact] public async Task Removal_rollback_never_overwrites_an_external_replacement()
    {
        var expected = await WorkspaceAssetMutationService.ReadAsync(Image);
        string? isolated = null;
        var error = await Assert.ThrowsAsync<IOException>(() => Service.RemoveAsync(new(WorkspaceRoot), expected, path =>
        {
            isolated = path; File.WriteAllBytes(Image, [88]); throw new IOException("Simulated shell failure");
        }));
        Assert.Contains("independent recovery copy", error.Message); Assert.Equal(new byte[] { 88 }, await File.ReadAllBytesAsync(Image));
        Assert.Equal(expected.Fingerprint, (await WorkspaceAssetMutationService.ReadAsync(isolated!)).Fingerprint);
        Assert.Equal(expected.Fingerprint, DocumentSnapshot.Hash(await RetainedImageAsync(Assert.Single((await Snapshots.ListAsync()).Entries))));
    }

    [Fact] public async Task Interrupted_asset_move_recovers_original_and_planned_text_without_replaying_paths()
    {
        await File.WriteAllTextAsync(Guide, "![Image](assets/original%20image.png)");
        var plan = await Service.PlanMoveAsync(new(WorkspaceRoot), Image, "assets/renamed.png");
        Directory.CreateDirectory(JournalDirectory);
        var journal = Path.Combine(JournalDirectory, "fixture.asset.json");
        await File.WriteAllTextAsync(journal, JsonSerializer.Serialize(new AssetMoveJournal(1, plan, "untrusted-path", DateTimeOffset.UtcNow)));
        var recovery = new WorkspaceMoveRecovery(JournalDirectory);
        var listing = await recovery.ReadAsync();
        Assert.Single(listing.Warnings); Assert.Equal(2, Assert.Single(listing.Items).Drafts.Count);
        Assert.True(File.Exists(Image)); Assert.False(File.Exists(plan.Destination));
        Assert.Equal(plan.Documents[0].Original.Text, await File.ReadAllTextAsync(Guide));
        recovery.MarkReviewed(journal); Assert.True(File.Exists(journal + ".reviewed.json"));
    }

    [Fact] public async Task Image_operations_cannot_move_native_recovery_files_even_when_a_parent_is_selected_as_the_workspace()
    {
        Directory.CreateDirectory(State);
        var path = Path.Combine(State, "recovery.png"); await File.WriteAllBytesAsync(path, [1, 2]);
        await Assert.ThrowsAsync<IOException>(() => Service.PlanMoveAsync(new(root), path, "workspace/assets/recovery.png"));
        var expected = await WorkspaceAssetMutationService.ReadAsync(path);
        await Assert.ThrowsAsync<IOException>(() => Service.RemoveAsync(new(root), expected, _ => throw new InvalidOperationException("Must not be called")));
        Assert.True(File.Exists(path));
    }

    private async Task<byte[]> RetainedImageAsync(DocumentSnapshotEntry entry)
    {
        var record = await Snapshots.ReadAsync(entry); Assert.True(record.PortableAssets);
        var folder = Path.GetDirectoryName(entry.Path)!;
        var link = Assert.Single(MarkdownLinks.Read(record.PortableText));
        return await File.ReadAllBytesAsync(new WorkspacePath(folder).ResolveLink(Path.Combine(folder, "snapshot.md"), link.Reference));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
