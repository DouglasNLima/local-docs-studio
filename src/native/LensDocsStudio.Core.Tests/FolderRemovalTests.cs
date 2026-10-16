using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Core.Documents;

namespace LensDocsStudio.Core.Tests;

public sealed class FolderRemovalTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "lens-folder-removal-" + Guid.NewGuid().ToString("N"));
    private readonly string workspace, source, state;
    private readonly WorkspaceFolderRemovalService service;
    public FolderRemovalTests()
    {
        workspace = Path.Combine(root, "workspace"); source = Path.Combine(workspace, "docs"); state = Path.Combine(root, "private");
        Directory.CreateDirectory(Path.Combine(source, "assets")); Directory.CreateDirectory(Path.Combine(source, "empty"));
        File.WriteAllText(Path.Combine(source, "Café.md"), "# Café\r\n"); File.WriteAllBytes(Path.Combine(source, "assets", "payload.bin"), [0, 1, 2, 255]);
        service = new(new DocumentStore(Path.Combine(state, "writes")), new DocumentSnapshotStore(Path.Combine(state, "snapshots")), state);
    }
    [Fact] public async Task Successful_folder_removal_retains_documents_and_passes_only_an_isolated_workspace_folder()
    {
        var scope = new WorkspacePath(workspace); var plan = await service.PlanAsync(scope, source);
        Assert.Equal(4, plan.Entries.Count);
        await service.RemoveAsync(scope, plan, held =>
        {
            Assert.True(WorkspacePath.Contains(workspace, held)); Assert.NotEqual(source, held);
            Assert.Equal(new byte[] { 0, 1, 2, 255 }, File.ReadAllBytes(Path.Combine(held, "assets", "payload.bin")));
            Directory.Delete(held, true); return Task.CompletedTask;
        });
        Assert.False(Directory.Exists(source));
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(state, "snapshots"), "*", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetDirectories(workspace, ".lens-delete-folder-*"));
    }
    [Fact] public async Task Cancelled_recycling_restores_the_complete_folder_and_assets()
    {
        var scope = new WorkspacePath(workspace); var plan = await service.PlanAsync(scope, source);
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.RemoveAsync(scope, plan, _ => throw new OperationCanceledException()));
        Assert.Equal("# Café\r\n", File.ReadAllText(Path.Combine(source, "Café.md")));
        Assert.Equal(new byte[] { 0, 1, 2, 255 }, File.ReadAllBytes(Path.Combine(source, "assets", "payload.bin")));
        Assert.True(Directory.Exists(Path.Combine(source, "empty")));
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task Changed_or_new_files_require_a_new_review_before_any_removal(bool newFile)
    {
        var scope = new WorkspacePath(workspace); var plan = await service.PlanAsync(scope, source); var called = false;
        File.WriteAllText(Path.Combine(source, newFile ? "new.md" : "Café.md"), "Newer source");
        await Assert.ThrowsAsync<DocumentConflictException>(() => service.RemoveAsync(scope, plan, _ => { called = true; return Task.CompletedTask; }));
        Assert.False(called); Assert.True(Directory.Exists(source));
    }
    [Fact] public async Task Root_escapes_and_private_state_are_not_valid_folder_removal_targets()
    {
        var scope = new WorkspacePath(workspace);
        await Assert.ThrowsAsync<IOException>(() => service.PlanAsync(scope, workspace));
        await Assert.ThrowsAsync<IOException>(() => service.PlanAsync(scope, state));
        var privateInside = Path.Combine(source, "private"); Directory.CreateDirectory(privateInside);
        var guarded = new WorkspaceFolderRemovalService(new(Path.Combine(state, "writes")), new(Path.Combine(state, "snapshots")), privateInside);
        await Assert.ThrowsAsync<IOException>(() => guarded.PlanAsync(scope, source));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
