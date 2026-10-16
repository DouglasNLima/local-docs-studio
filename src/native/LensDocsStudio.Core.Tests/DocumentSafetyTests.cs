using LensDocsStudio.Core.Documents;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Core.Tests;

public sealed class DocumentSafetyTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "lens-native-tests-" + Guid.NewGuid().ToString("N"));
    public DocumentSafetyTests() => Directory.CreateDirectory(root);
    private DocumentStore Store => new(Path.Combine(root, "recovery"));

    [Fact]
    public async Task Saves_unicode_and_preserves_bom_and_line_endings()
    {
        var path = Path.Combine(root, "Café.md");
        await File.WriteAllBytesAsync(path, DocumentSnapshot.Encode("# Café\r\n", true));
        var snapshot = await Store.ReadAsync(path);
        var saved = await Store.SaveAsync(path, snapshot.Text + "こんにちは 🌍\r\n", snapshot);
        Assert.True(saved.Utf8Bom);
        Assert.Equal(saved, await Store.ReadAsync(path));
        Assert.Empty(await Store.ReadPendingWritesAsync());
    }

    [Fact]
    public async Task Rejects_external_changes_without_overwriting_either_version()
    {
        var path = Path.Combine(root, "source.md");
        var original = await Store.SaveAsync(path, "original", null);
        await File.WriteAllTextAsync(path, "external");
        await Assert.ThrowsAsync<DocumentConflictException>(() => Store.SaveAsync(path, "local edits", original));
        Assert.Equal("external", await File.ReadAllTextAsync(path));
        Assert.Equal("original", original.Text);
    }

    [Fact]
    public async Task Creating_or_saving_as_never_overwrites_an_existing_file()
    {
        var path = Path.Combine(root, "exists.md");
        await File.WriteAllTextAsync(path, "keep");
        await Assert.ThrowsAsync<IOException>(() => Store.SaveAsync(path, "overwrite", null));
        Assert.Equal("keep", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Cancelled_save_leaves_original_untouched()
    {
        var path = Path.Combine(root, "cancel.md");
        var original = await Store.SaveAsync(path, "keep", null);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Store.SaveAsync(path, "replace", original, cancellation.Token));
        Assert.Equal("keep", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Locked_file_is_not_truncated()
    {
        var path = Path.Combine(root, "locked.md");
        var original = await Store.SaveAsync(path, "keep", null);
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            await Assert.ThrowsAsync<IOException>(() => Store.SaveAsync(path, "replace", original));
        Assert.Equal("keep", await File.ReadAllTextAsync(path));
    }

    [Theory]
    [InlineData("../escape.md")]
    [InlineData("C:\\outside.md")]
    [InlineData("//server/share/file.md")]
    [InlineData("document.md:stream")]
    [InlineData("CON.md")]
    [InlineData("folder./file.md")]
    public void Workspace_rejects_unsafe_paths(string reference) =>
        Assert.Throws<IOException>(() => new WorkspacePath(root).Resolve(reference));

    [Fact]
    public void Links_resolve_from_the_source_directory()
    {
        var paths = new WorkspacePath(root);
        var source = paths.Resolve("docs/guide.md");
        Assert.Equal(paths.Resolve("reference/Café.md"), paths.ResolveLink(source, "../reference/Caf%C3%A9.md#section"));
        Assert.Equal(paths.Resolve("reference/Café #1 %.md"), paths.ResolveLink(source, "../reference/Caf%C3%A9%20%231%20%25.md"));
        Assert.Throws<IOException>(() => paths.ResolveLink(source, "../%252e%252e/escape.md"));
        Assert.Throws<IOException>(() => paths.ResolveLink(source, "../../escape.md"));
        Assert.Throws<IOException>(() => paths.ResolveLink(source, "https://example.com/a.md"));
    }

    [Fact]
    public async Task Recovery_retains_unsaved_text_and_falls_back_to_previous_state()
    {
        var store = new SessionStore(Path.Combine(root, "state"));
        var state = new SessionState(1, root, null, [new(Guid.NewGuid(), null, "unsaved", null, false, 4, 20)]);
        await store.WriteAsync("session.json", state);
        await store.WriteAsync("session.json", state with { Workspace = null });
        await File.WriteAllTextAsync(Path.Combine(store.DirectoryPath, "session.json"), "{broken");
        var restored = await store.ReadAsync<SessionState>("session.json");
        Assert.Equal("unsaved", Assert.Single(restored!.Documents).Text);
        Assert.Equal(root, restored.Workspace);
    }

    [Fact]
    public async Task Invalid_utf8_is_rejected_instead_of_replacing_source_characters()
    {
        var path = Path.Combine(root, "bad.md");
        await File.WriteAllBytesAsync(path, [0xff, 0xfe, 0x41]);
        await Assert.ThrowsAsync<System.Text.DecoderFallbackException>(() => Store.ReadAsync(path));
    }

    [Fact] public void Drive_root_workspaces_accept_children_without_allowing_sibling_prefixes()
    {
        var drive = Path.GetPathRoot(root)!;
        Assert.Equal(root, new WorkspacePath(drive).Resolve(Path.GetRelativePath(drive, root)));
        Assert.False(WorkspacePath.Contains(root, root + "-other/file.md"));
    }

    [Fact] public async Task Malformed_recovery_record_does_not_hide_other_recoverable_drafts()
    {
        var store = Store;
        Directory.CreateDirectory(store.RecoveryDirectory);
        await File.WriteAllTextAsync(Path.Combine(store.RecoveryDirectory, "bad.pending.json"), "{broken");
        var pending = new PendingWrite(1, Path.Combine(root, "draft.md"), [], DocumentSnapshot.Encode("recover me", false), DateTimeOffset.UtcNow);
        await File.WriteAllTextAsync(Path.Combine(store.RecoveryDirectory, "valid.pending.json"), System.Text.Json.JsonSerializer.Serialize(pending));
        var recovered = Assert.Single(await store.ReadPendingWritesAsync());
        Assert.Equal("recover me", DocumentSnapshot.FromBytes(recovered.Path, recovered.Intended).Text);
        Assert.Single(store.RecoveryWarnings);
        Assert.Equal("{broken", await File.ReadAllTextAsync(Path.Combine(store.RecoveryDirectory, "bad.pending.json")));
    }

    [Fact] public async Task Both_malformed_session_versions_are_retained_before_new_state_is_saved()
    {
        var store = new SessionStore(Path.Combine(root, "state"));
        Directory.CreateDirectory(store.DirectoryPath);
        await File.WriteAllTextAsync(Path.Combine(store.DirectoryPath, "session.json"), "{broken-primary");
        await File.WriteAllTextAsync(Path.Combine(store.DirectoryPath, "session.json.bak"), "{broken-backup");
        Assert.Null(await store.ReadAsync<SessionState>("session.json"));
        await store.WriteAsync("session.json", new SessionState(1, null, null, []));
        var retained = Directory.GetFiles(store.DirectoryPath, "*.unreadable-*.json").Select(File.ReadAllText).ToArray();
        Assert.Contains("{broken-primary", retained); Assert.Contains("{broken-backup", retained);
    }

    public void Dispose() => Directory.Delete(root, true);
}
