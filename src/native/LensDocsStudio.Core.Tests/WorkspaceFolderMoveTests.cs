using LensDocsStudio.Core.Documents;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Core.Tests;

public sealed class WorkspaceFolderMoveTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "lens-folder-move-" + Guid.NewGuid().ToString("N"));
    private string State => Path.Combine(root, "private");
    private string Source => Path.Combine(root, "docs");
    private string Destination => Path.Combine(root, "reference", "Guide folder");
    private WorkspaceFolderMoveService Service => new(new DocumentStore(Path.Combine(State, "writes")), Path.Combine(State, "moves"), State);
    public WorkspaceFolderMoveTests() { Directory.CreateDirectory(Source); Directory.CreateDirectory(Path.Combine(root, "reference")); }
    private async Task<string> Write(string relative, string text)
    {
        var path = Path.Combine(root, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!); await File.WriteAllTextAsync(path, text); return path;
    }
    [Fact] public async Task Folder_move_preserves_assets_empty_folders_source_spans_and_cross_boundary_links()
    {
        Directory.CreateDirectory(Path.Combine(Source, "empty")); Directory.CreateDirectory(Path.Combine(Source, "assets"));
        byte[] asset = [0, 1, 2, 255, 254]; await File.WriteAllBytesAsync(Path.Combine(Source, "assets", "image.png"), asset);
        var source = await Write("docs/guide.md", "# Guide\r\n\r\n![Local](assets/image.png) [Outside](../home.md#home) [[next|Next]] [[#guide|Here]]\r\n");
        await File.WriteAllBytesAsync(source, DocumentSnapshot.Encode(await File.ReadAllTextAsync(source), true));
        await Write("docs/next.md", "# Next\n\n[Guide](guide.md#guide)");
        const string mermaid = "flowchart LR\nA[[guide]] --> B"; await Write("docs/diagram.mmd", mermaid);
        var home = await Write("home.md", "# Home\n\n[Guide][reference] [[docs/guide|Wiki]] ![Asset](docs/assets/image.png) `[[docs/guide]]`\n\n[reference]: docs/guide.md#guide \"Title\"\n");
        var plan = await Service.PlanAsync(new(root), Source, "reference/Guide folder");
        var result = await Service.ApplyAsync(new(root), plan, true);
        Assert.False(Directory.Exists(Source)); Assert.True(Directory.Exists(Path.Combine(Destination, "empty")));
        Assert.Equal(asset, await File.ReadAllBytesAsync(Path.Combine(Destination, "assets", "image.png")));
        var moved = Assert.Single(result.Documents, document => Path.GetFileName(document.Path) == "guide.md"); Assert.True(moved.Utf8Bom);
        Assert.Contains("![Local](assets/image.png) [Outside](../../home.md#home)", moved.Text);
        Assert.Contains("[[reference/Guide folder/next.md|Next]] [[reference/Guide folder/guide.md#guide|Here]]\r\n", moved.Text);
        Assert.Equal(mermaid, await File.ReadAllTextAsync(Path.Combine(Destination, "diagram.mmd")));
        var incoming = await File.ReadAllTextAsync(home);
        Assert.Contains("[reference]: reference/Guide%20folder/guide.md#guide \"Title\"", incoming);
        Assert.Contains("[[reference/Guide folder/guide.md|Wiki]]", incoming); Assert.Contains("![Asset](reference/Guide%20folder/assets/image.png)", incoming);
        Assert.Contains("`[[docs/guide]]`", incoming); Assert.Empty(Directory.GetFiles(Path.Combine(State, "moves"), "*.folder-move.json"));
    }
    [Fact] public async Task Move_without_link_updates_preserves_document_and_binary_bytes()
    {
        var source = await Write("docs/guide.md", "[Outside](../home.md)"); var home = await Write("home.md", "[[docs/guide|Guide]]");
        var original = await File.ReadAllBytesAsync(source); await File.WriteAllBytesAsync(Path.Combine(Source, "binary.dat"), [128, 255, 0]);
        var plan = await Service.PlanAsync(new(root), Source, "reference/Guide folder");
        await Service.ApplyAsync(new(root), plan, false);
        Assert.Equal(original, await File.ReadAllBytesAsync(Path.Combine(Destination, "guide.md")));
        Assert.Equal("[[docs/guide|Guide]]", await File.ReadAllTextAsync(home)); Assert.Equal(new byte[] { 128, 255, 0 }, await File.ReadAllBytesAsync(Path.Combine(Destination, "binary.dat")));
    }
    [Theory] [InlineData("file")] [InlineData("asset")] [InlineData("new-file")] [InlineData("empty-folder")] [InlineData("incoming")]
    public async Task Changed_inventory_or_linked_source_refuses_commit_without_mutation(string change)
    {
        var source = await Write("docs/guide.md", "# Guide"); var home = await Write("home.md", "[Guide](docs/guide.md)");
        await File.WriteAllBytesAsync(Path.Combine(Source, "asset.dat"), [1, 2]);
        var plan = await Service.PlanAsync(new(root), Source, "reference/Guide folder");
        if (change == "file") await File.WriteAllTextAsync(source, "# External guide");
        if (change == "asset") await File.WriteAllBytesAsync(Path.Combine(Source, "asset.dat"), [3, 4]);
        if (change == "new-file") await Write("docs/new.md", "# New");
        if (change == "empty-folder") Directory.CreateDirectory(Path.Combine(Source, "new-empty"));
        if (change == "incoming") await File.WriteAllTextAsync(home, "# External incoming");
        await Assert.ThrowsAsync<DocumentConflictException>(() => Service.ApplyAsync(new(root), plan, true));
        Assert.True(Directory.Exists(Source)); Assert.False(Directory.Exists(Destination)); Assert.False(Directory.Exists(Path.Combine(State, "moves")));
    }
    [Fact] public async Task Late_link_write_failure_restores_folder_assets_and_completed_document_updates()
    {
        await Write("docs/guide.md", "[Outside](../a.md)"); await Write("a.md", "[Guide](docs/guide.md)"); var last = await Write("z.md", "[[docs/guide|Guide]]");
        await File.WriteAllBytesAsync(Path.Combine(Source, "asset.dat"), [0, 255]);
        var plan = await Service.PlanAsync(new(root), Source, "reference/Guide folder");
        using (var locked = new FileStream(last, FileMode.Open, FileAccess.Read, FileShare.Read))
            await Assert.ThrowsAsync<IOException>(() => Service.ApplyAsync(new(root), plan, true));
        Assert.True(Directory.Exists(Source)); Assert.False(Directory.Exists(Destination));
        Assert.Equal("[Outside](../a.md)", await File.ReadAllTextAsync(Path.Combine(Source, "guide.md")));
        Assert.Equal("[Guide](docs/guide.md)", await File.ReadAllTextAsync(Path.Combine(root, "a.md")));
        Assert.Equal("[[docs/guide|Guide]]", await File.ReadAllTextAsync(last)); Assert.Equal(new byte[] { 0, 255 }, await File.ReadAllBytesAsync(Path.Combine(Source, "asset.dat")));
        Assert.Empty(Directory.GetFiles(Path.Combine(State, "moves"), "*.folder-move.json"));
    }
    [Fact] public async Task Unsafe_destinations_private_state_and_cancelled_operations_never_move_source()
    {
        await Write("docs/guide.md", "# Guide"); await Write("private/retained.md", "[Guide](../docs/guide.md)");
        foreach (var destination in new[] { "docs/child", "docs", "reference", "../outside", "private/new", "missing/child" })
            await Assert.ThrowsAnyAsync<IOException>(() => Service.PlanAsync(new(root), Source, destination));
        await Assert.ThrowsAsync<IOException>(() => Service.PlanAsync(new(root), State, "reference/private"));
        var plan = await Service.PlanAsync(new(root), Source, "reference/Guide folder");
        Assert.DoesNotContain(plan.Documents, document => WorkspacePath.Contains(State, document.Original.Path));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service.ApplyAsync(new(root), plan, true, cancelled.Token));
        Assert.True(Directory.Exists(Source)); Assert.False(Directory.Exists(Destination));
    }
    [Fact] public async Task A_folder_containing_private_state_is_refused_and_empty_folders_move()
    {
        var nestedPrivate = Path.Combine(Source, "private"); Directory.CreateDirectory(nestedPrivate);
        var mover = new WorkspaceFolderMoveService(new(Path.Combine(State, "writes")), Path.Combine(State, "moves"), nestedPrivate);
        await Assert.ThrowsAsync<IOException>(() => mover.PlanAsync(new(root), Source, "reference/Guide folder"));
        var plan = await Service.PlanAsync(new(root), Source, "reference/Guide folder");
        await Service.ApplyAsync(new(root), plan, true); Assert.True(Directory.Exists(Path.Combine(Destination, "private")));
    }
    [Fact] public async Task Interrupted_folder_recovery_retains_original_and_planned_text_without_replaying_paths()
    {
        await Write("docs/guide.md", "[Outside](../home.md)"); await Write("home.md", "[[docs/guide|Guide]]");
        var plan = await Service.PlanAsync(new(root), Source, "reference/Guide folder"); var recoveryRoot = Path.Combine(State, "moves"); Directory.CreateDirectory(recoveryRoot);
        var journal = Path.Combine(recoveryRoot, "interrupted.folder-move.json");
        await File.WriteAllTextAsync(journal, System.Text.Json.JsonSerializer.Serialize(new WorkspaceFolderMoveJournal(1, root, plan, DateTimeOffset.UtcNow)));
        var recovery = new WorkspaceMoveRecovery(recoveryRoot); var listing = await recovery.ReadAsync(); var item = Assert.Single(listing.Items);
        Assert.Equal(4, item.Drafts.Count); Assert.Contains(item.Drafts, draft => draft.Text == "[Outside](../../home.md)");
        Assert.Contains(listing.Warnings, warning => warning.Contains("No file paths were replayed", StringComparison.Ordinal));
        Assert.True(Directory.Exists(Source)); Assert.False(Directory.Exists(Destination));
        recovery.MarkReviewed(journal); Assert.True(File.Exists(journal + ".reviewed.json")); Assert.Empty((await recovery.ReadAsync()).Items);
    }
    [Fact] public async Task Oversized_assets_are_refused_before_reading_and_new_destinations_are_not_replaced()
    {
        var asset = Path.Combine(Source, "large.dat");
        using (var file = File.Create(asset)) file.SetLength(WorkspaceFolderMoveService.MaximumBytes + 1);
        await Assert.ThrowsAsync<IOException>(() => Service.PlanAsync(new(root), Source, "reference/Guide folder"));
        using (var file = File.Open(asset, FileMode.Truncate)) file.WriteByte(42);
        var plan = await Service.PlanAsync(new(root), Source, "reference/Guide folder"); Directory.CreateDirectory(Destination);
        var retained = Path.Combine(Destination, "retained.txt"); await File.WriteAllTextAsync(retained, "Existing data");
        await Assert.ThrowsAsync<IOException>(() => Service.ApplyAsync(new(root), plan, true));
        Assert.Equal("Existing data", await File.ReadAllTextAsync(retained)); Assert.True(File.Exists(asset));
    }
    [Fact] public async Task Raw_html_reference_limitations_are_visible_and_code_remains_literal()
    {
        await Write("docs/guide.md", "# Guide");
        var html = await Write("html.md", "<a href=\"docs/guide.md\">Guide</a>");
        await Write("code.md", "```html\n<a href=\"docs/guide.md\">Example</a>\n```");
        var plan = await Service.PlanAsync(new(root), Source, "reference/Guide folder");
        Assert.Contains(plan.Warnings, warning => warning.Contains("Raw HTML", StringComparison.Ordinal) && warning.Contains("html.md", StringComparison.Ordinal));
        Assert.DoesNotContain(plan.Warnings, warning => warning.Contains("code.md", StringComparison.Ordinal));
        await Service.ApplyAsync(new(root), plan, true); Assert.Equal("<a href=\"docs/guide.md\">Guide</a>", await File.ReadAllTextAsync(html));
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task Case_only_folder_rename_preserves_assets_empty_folders_and_optional_portable_link_updates(bool updateLinks)
    {
        Directory.CreateDirectory(Path.Combine(Source, "Empty"));
        var guide = await Write("docs/Guide.md", "# Guide\r\n\r\n[Outside](../home.md) ![Local](asset.bin)\r\n");
        await File.WriteAllBytesAsync(guide, DocumentSnapshot.Encode(await File.ReadAllTextAsync(guide), true));
        var bytes = await File.ReadAllBytesAsync(guide); byte[] asset = [0, 128, 255];
        await File.WriteAllBytesAsync(Path.Combine(Source, "asset.bin"), asset);
        const string links = "[Guide](docs/Guide.md#guide) [[docs/Guide|Alias]] ![Asset](docs/asset.bin)";
        var home = await Write("home.md", links);
        var plan = await Service.PlanAsync(new(root), Source, "DOCS");
        var result = await Service.ApplyAsync(new(root), plan, updateLinks);
        var renamed = Path.Combine(root, "DOCS");
        Assert.Contains(renamed, Directory.GetDirectories(root), StringComparer.Ordinal);
        Assert.DoesNotContain(Source, Directory.GetDirectories(root), StringComparer.Ordinal);
        Assert.True(Directory.Exists(Path.Combine(renamed, "Empty")));
        Assert.Equal(asset, await File.ReadAllBytesAsync(Path.Combine(renamed, "asset.bin")));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(renamed, "Guide.md")));
        Assert.Contains(result.Documents, document => document.Path.Equals(Path.Combine(renamed, "Guide.md"), StringComparison.Ordinal) && document.Utf8Bom);
        Assert.Equal(updateLinks ? "[Guide](DOCS/Guide.md#guide) [[DOCS/Guide.md|Alias]] ![Asset](DOCS/asset.bin)" : links, await File.ReadAllTextAsync(home));
        Assert.Empty(Directory.GetFiles(Path.Combine(State, "moves"), "*.folder-move.json"));
    }
    [Fact] public async Task Failed_case_only_folder_move_restores_original_directory_spelling_and_completed_links()
    {
        await Write("docs/Guide.md", "# Guide\r\n");
        const string firstText = "[Guide](docs/Guide.md)"; var first = await Write("a.md", firstText);
        const string lastText = "[[docs/Guide|Guide]]"; var last = await Write("z.md", lastText);
        var plan = await Service.PlanAsync(new(root), Source, "DOCS");
        using (var locked = new FileStream(last, FileMode.Open, FileAccess.Read, FileShare.Read))
            await Assert.ThrowsAsync<IOException>(() => Service.ApplyAsync(new(root), plan, true));
        Assert.Contains(Source, Directory.GetDirectories(root), StringComparer.Ordinal);
        Assert.DoesNotContain(Path.Combine(root, "DOCS"), Directory.GetDirectories(root), StringComparer.Ordinal);
        Assert.Equal(firstText, await File.ReadAllTextAsync(first)); Assert.Equal(lastText, await File.ReadAllTextAsync(last));
        Assert.Equal("# Guide\r\n", await File.ReadAllTextAsync(Path.Combine(Source, "Guide.md")));
        Assert.Empty(Directory.GetFiles(Path.Combine(State, "moves"), "*.folder-move.json"));
    }
    [Fact] public async Task Case_only_folder_move_rejects_changed_assets_and_retains_parent_directory_spelling()
    {
        var guide = await Write("docs/Nested/Guide.md", "# Nested guide"); var parent = Path.GetDirectoryName(guide)!;
        var plan = await Service.PlanAsync(new(root), parent, "DOCS/NESTED");
        Assert.Equal(Path.Combine(Source, "NESTED"), plan.Destination);
        await File.WriteAllTextAsync(guide, "# External guide");
        await Assert.ThrowsAsync<DocumentConflictException>(() => Service.ApplyAsync(new(root), plan, true));
        Assert.Contains(parent, Directory.GetDirectories(Source), StringComparer.Ordinal);
        Assert.Equal("# External guide", await File.ReadAllTextAsync(guide));
        await Assert.ThrowsAsync<IOException>(() => Service.PlanAsync(new(root), parent, "DOCS/Nested"));
    }
    public void Dispose() => Directory.Delete(root, true);
}
