using System.Text.Json;
using LensDocsStudio.Core.Documents;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Core.Tests;

public sealed class DocumentSnapshotTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "lens-snapshots-" + Guid.NewGuid().ToString("N"));
    private DocumentSnapshotStore Snapshots => new(Path.Combine(root, "state"));
    private DocumentStore Documents => new(Path.Combine(root, "state", "writes"));
    public DocumentSnapshotTests() => Directory.CreateDirectory(root);

    [Fact] public async Task Snapshot_retains_exact_source_and_an_independent_copy_of_local_images()
    {
        var path = Path.Combine(root, "guide.md"); var image = Path.Combine(root, "diagram.png");
        var bytes = new byte[] { 1, 2, 3, 4 };
        await File.WriteAllBytesAsync(image, bytes);
        const string text = "# Café\r\n\r\n![Diagram](diagram.png)\r\n[Next](next.md)\r\n";
        var entry = await Snapshots.CaptureAsync(new(Guid.NewGuid(), path, text, null, true, 4, 40), "Manual snapshot", path, new(root));
        File.Delete(image);
        var record = await Snapshots.ReadAsync(entry);
        Assert.Equal(text, record.Document.Text); Assert.True(record.Document.Utf8Bom); Assert.True(record.PortableAssets);
        var folder = Path.GetDirectoryName(entry.Path)!;
        var copied = new WorkspacePath(folder).ResolveLink(Path.Combine(folder, "snapshot.md"), MarkdownLinks.Read(record.PortableText).Single(l => l.IsImage).Reference);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(copied)); Assert.Single(record.Warnings);
        Assert.Single((await Snapshots.ListAsync()).Entries);
    }

    [Fact] public async Task Missing_images_do_not_prevent_recovery_of_exact_source_text()
    {
        const string text = "![Missing](missing.png)";
        var path = Path.Combine(root, "guide.md");
        var entry = await Snapshots.CaptureAsync(new(Guid.NewGuid(), path, text, null, false, 0, 0), "Before reload", path, new(root));
        var record = await Snapshots.ReadAsync(entry);
        Assert.Equal(text, record.Document.Text); Assert.Equal(text, record.PortableText); Assert.False(record.PortableAssets);
        Assert.Contains(record.Warnings, w => w.Contains("could not all be copied", StringComparison.Ordinal));
    }

    [Fact] public async Task Malformed_snapshots_remain_on_disk_and_older_reload_snapshots_are_readable()
    {
        var session = new SessionStore(Path.Combine(root, "state"));
        await session.WriteAsync("snapshot-old.json", new RecoveredDocument(Guid.NewGuid(), null, "Old draft", null, false, 0, 0));
        var invalid = Path.Combine(session.DirectoryPath, "snapshot-invalid.json");
        await File.WriteAllTextAsync(invalid, "broken");
        var listing = await Snapshots.ListAsync();
        Assert.Single(listing.Entries); Assert.True(listing.Entries[0].Legacy); Assert.Single(listing.Warnings);
        Assert.Equal("Old draft", (await Snapshots.ReadAsync(listing.Entries[0])).Document.Text);
        Assert.Equal("broken", await File.ReadAllTextAsync(invalid));
        await Assert.ThrowsAsync<IOException>(() => Snapshots.ReadAsync(new(Path.Combine(root, "outside.json"), DateTimeOffset.UtcNow, "Outside", "Invalid")));
    }

    [Fact] public async Task Removal_retains_saved_source_before_handing_the_isolated_file_to_the_shell()
    {
        var source = await Documents.SaveAsync(Path.Combine(root, "guide.md"), "Saved content", null);
        var called = false;
        var entry = await new DocumentRemovalService(Documents, Snapshots).RemoveAsync(new(root), source, async held =>
        {
            called = true;
            Assert.False(File.Exists(source.Path)); Assert.Equal("Saved content", await File.ReadAllTextAsync(held));
            Assert.Single((await Snapshots.ListAsync()).Entries);
            File.Move(held, Path.Combine(root, "recycle-fixture.md"));
        });
        Assert.True(called); Assert.False(File.Exists(source.Path));
        Assert.Equal(source.Text, (await Snapshots.ReadAsync(entry)).Document.Text);
        Assert.Empty(Directory.EnumerateDirectories(root, ".lens-delete-*"));
    }

    [Fact] public async Task Cancelled_shell_operation_restores_the_original_file_and_keeps_its_snapshot()
    {
        var source = await Documents.SaveAsync(Path.Combine(root, "guide.md"), "Preserve", null);
        await Assert.ThrowsAsync<OperationCanceledException>(() => new DocumentRemovalService(Documents, Snapshots).RemoveAsync(new(root), source,
            _ => throw new OperationCanceledException()));
        Assert.Equal("Preserve", await File.ReadAllTextAsync(source.Path));
        Assert.Single((await Snapshots.ListAsync()).Entries);
        Assert.Empty(Directory.EnumerateDirectories(root, ".lens-delete-*"));
    }

    [Fact] public async Task Failed_removal_does_not_replace_a_new_file_at_the_original_path()
    {
        var source = await Documents.SaveAsync(Path.Combine(root, "guide.md"), "Original", null);
        string? retained = null;
        var error = await Assert.ThrowsAsync<IOException>(() => new DocumentRemovalService(Documents, Snapshots).RemoveAsync(new(root), source, async held =>
        {
            retained = held;
            await File.WriteAllTextAsync(source.Path, "External replacement");
            throw new IOException("Shell could not recycle");
        }));
        Assert.Contains(retained!, error.Message); Assert.Equal("Original", await File.ReadAllTextAsync(retained!));
        Assert.Equal("External replacement", await File.ReadAllTextAsync(source.Path));
    }

    [Fact] public async Task External_edits_prevent_deletion_before_the_shell_is_called()
    {
        var source = await Documents.SaveAsync(Path.Combine(root, "guide.md"), "Original", null);
        await File.WriteAllTextAsync(source.Path, "External edits");
        var called = false;
        await Assert.ThrowsAsync<DocumentConflictException>(() => new DocumentRemovalService(Documents, Snapshots).RemoveAsync(new(root), source,
            _ => { called = true; return Task.CompletedTask; }));
        Assert.False(called); Assert.Equal("External edits", await File.ReadAllTextAsync(source.Path));
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
