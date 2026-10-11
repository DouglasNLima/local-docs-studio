using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Core.Documents;

namespace LensDocsStudio.Core.Tests;

public sealed class WorkspaceMoveTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "lens-move-" + Guid.NewGuid().ToString("N"));
    private DocumentStore Store => new(Path.Combine(root, "state", "writes"));
    private WorkspaceMoveService Service => new(Store, Path.Combine(root, "state", "moves"));
    public WorkspaceMoveTests() { Directory.CreateDirectory(Path.Combine(root, "docs")); Directory.CreateDirectory(Path.Combine(root, "reference")); }

    [Fact] public async Task Move_updates_incoming_links_and_rebases_source_images_without_reformatting_markdown()
    {
        var source = Path.Combine(root, "docs", "guide.md"); var home = Path.Combine(root, "README.md");
        await File.WriteAllBytesAsync(source, DocumentSnapshot.Encode("# Guide\r\n\r\n![Image](../assets/a.png)\r\n[Next](next.md#details)\r\n", true));
        await File.WriteAllTextAsync(home, "[Guide][ref]\n\n[ref]: docs/guide.md#intro \"Guide title\"\n\n`[Example](docs/guide.md)`\n");
        var plan = await Service.PlanAsync(new(root), source, "reference/moved guide.md");
        Assert.Single(plan.OtherDocuments);
        var result = await Service.ApplyAsync(new(root), plan, true);
        Assert.False(File.Exists(source)); Assert.True(result.Moved.Utf8Bom);
        Assert.Equal("# Guide\r\n\r\n![Image](../assets/a.png)\r\n[Next](../docs/next.md#details)\r\n", await File.ReadAllTextAsync(result.Moved.Path));
        var incoming = await File.ReadAllTextAsync(home);
        Assert.Contains("[ref]: reference/moved%20guide.md#intro \"Guide title\"", incoming);
        Assert.Contains("`[Example](docs/guide.md)`", incoming);
        Assert.Empty(Directory.GetFiles(Path.Combine(root, "state", "moves"), "*.move.json"));
    }

    [Fact] public async Task Move_without_link_updates_preserves_all_document_bytes()
    {
        var source = Path.Combine(root, "docs", "guide.md"); var home = Path.Combine(root, "README.md");
        await File.WriteAllTextAsync(source, "[Link](next.md)"); await File.WriteAllTextAsync(home, "[Guide](docs/guide.md)");
        var plan = await Service.PlanAsync(new(root), source, "reference/guide.md");
        await Service.ApplyAsync(new(root), plan, false);
        Assert.Equal("[Link](next.md)", await File.ReadAllTextAsync(plan.Destination));
        Assert.Equal("[Guide](docs/guide.md)", await File.ReadAllTextAsync(home));
    }

    [Fact] public async Task External_link_edits_after_planning_stop_the_move_before_mutation()
    {
        var source = Path.Combine(root, "docs", "guide.md"); var home = Path.Combine(root, "README.md");
        await File.WriteAllTextAsync(source, "original"); await File.WriteAllTextAsync(home, "[Guide](docs/guide.md)");
        var plan = await Service.PlanAsync(new(root), source, "reference/guide.md");
        await File.WriteAllTextAsync(home, "External edits");
        await Assert.ThrowsAsync<DocumentConflictException>(() => Service.ApplyAsync(new(root), plan, true));
        Assert.Equal("original", await File.ReadAllTextAsync(source)); Assert.False(File.Exists(plan.Destination));
        Assert.Equal("External edits", await File.ReadAllTextAsync(home));
    }

    [Fact] public async Task A_locked_incoming_document_rolls_the_move_back_without_losing_source()
    {
        var source = Path.Combine(root, "docs", "guide.md"); var home = Path.Combine(root, "README.md");
        await File.WriteAllTextAsync(source, "original"); await File.WriteAllTextAsync(home, "[Guide](docs/guide.md)");
        var plan = await Service.PlanAsync(new(root), source, "reference/guide.md");
        using (var locked = new FileStream(home, FileMode.Open, FileAccess.Read, FileShare.Read))
            await Assert.ThrowsAsync<IOException>(() => Service.ApplyAsync(new(root), plan, true));
        Assert.Equal("original", await File.ReadAllTextAsync(source)); Assert.False(File.Exists(plan.Destination));
        Assert.Equal("[Guide](docs/guide.md)", await File.ReadAllTextAsync(home));
    }

    [Fact] public async Task Move_rejects_destination_collision_and_workspace_escape()
    {
        var source = Path.Combine(root, "docs", "guide.md"); await File.WriteAllTextAsync(source, "keep");
        await File.WriteAllTextAsync(Path.Combine(root, "reference", "exists.md"), "existing");
        await Assert.ThrowsAsync<IOException>(() => Service.PlanAsync(new(root), source, "reference/exists.md"));
        await Assert.ThrowsAsync<IOException>(() => Service.PlanAsync(new(root), source, "../outside.md"));
    }
    [Fact] public async Task A_late_failure_restores_link_documents_that_were_already_updated()
    {
        var source = Path.Combine(root, "docs", "guide.md"); var first = Path.Combine(root, "a.md"); var lockedPath = Path.Combine(root, "z.md");
        await File.WriteAllTextAsync(source, "[Image](local.png)");
        const string incoming = "[Guide](docs/guide.md#section)";
        await File.WriteAllTextAsync(first, incoming); await File.WriteAllTextAsync(lockedPath, incoming);
        var plan = await Service.PlanAsync(new(root), source, "reference/moved.md");
        using (var locked = new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            await Assert.ThrowsAsync<IOException>(() => Service.ApplyAsync(new(root), plan, true));
        Assert.Equal("[Image](local.png)", await File.ReadAllTextAsync(source));
        Assert.Equal(incoming, await File.ReadAllTextAsync(first)); Assert.Equal(incoming, await File.ReadAllTextAsync(lockedPath));
        Assert.False(File.Exists(plan.Destination));
    }
    [Fact] public async Task Interrupted_move_recovery_retains_both_versions_without_replaying_paths()
    {
        var source = Path.Combine(root, "docs", "guide.md"); await File.WriteAllTextAsync(source, "[Next](next.md)");
        var plan = await Service.PlanAsync(new(root), source, "reference/guide.md");
        var directory = Path.Combine(root, "state", "moves"); Directory.CreateDirectory(directory);
        var journalPath = Path.Combine(directory, "interrupted.move.json");
        await File.WriteAllTextAsync(journalPath, System.Text.Json.JsonSerializer.Serialize(new WorkspaceMoveJournal(1, root, plan, DateTimeOffset.UtcNow)));
        var recovery = new WorkspaceMoveRecovery(directory);
        var item = Assert.Single((await recovery.ReadAsync()).Items);
        Assert.Equal(2, item.Drafts.Count);
        Assert.Equal(plan.Source.Text, item.Drafts[0].Text); Assert.Equal(plan.UpdatedSource, item.Drafts[1].Text);
        Assert.True(File.Exists(source)); Assert.False(File.Exists(plan.Destination));
        recovery.MarkReviewed(item.JournalPath);
        Assert.True(File.Exists(journalPath + ".reviewed.json")); Assert.Empty((await recovery.ReadAsync()).Items);
    }
    public void Dispose() => Directory.Delete(root, true);
}
