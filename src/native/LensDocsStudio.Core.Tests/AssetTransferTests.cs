using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Core.Tests;

public sealed class AssetTransferTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "lens-assets-" + Guid.NewGuid().ToString("N"));
    private readonly byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a9l8AAAAASUVORK5CYII=");
    public AssetTransferTests() { Directory.CreateDirectory(Path.Combine(root, "source")); Directory.CreateDirectory(Path.Combine(root, "destination")); }

    [Fact] public async Task Save_as_copies_each_image_once_and_preserves_reference_style_and_unrelated_source()
    {
        var source = Path.Combine(root, "source", "draft.md"); var target = Path.Combine(root, "destination", "saved.md");
        await File.WriteAllBytesAsync(Path.Combine(root, "source", "a.png"), png);
        const string text = "# Source\r\n\r\n![First](a.png \"Caption\")\r\n![Second][pic]\r\n\r\n[pic]: <a.png> \"Title\"\r\n\r\n`![code](a.png)`\r\n";
        await using var prepared = await new AssetTransferService().PrepareAsync(text, source, new(Path.GetDirectoryName(source)!), target, new(Path.GetDirectoryName(target)!));
        var copied = Assert.Single(prepared.CreatedPaths);
        Assert.Equal(png, await File.ReadAllBytesAsync(copied));
        Assert.Equal(2, MarkdownLinks.Read(prepared.Text).Count);
        foreach (var link in MarkdownLinks.Read(prepared.Text)) Assert.Equal(copied, new WorkspacePath(Path.GetDirectoryName(target)!).ResolveLink(target, link.Reference));
        Assert.Contains("![Second][pic]", prepared.Text);
        Assert.Contains("\"Caption\"", prepared.Text); Assert.Contains("`![code](a.png)`", prepared.Text);
        Assert.Contains("\r\n", prepared.Text);
        prepared.Commit();
    }

    [Fact] public async Task Cancelled_or_failed_save_does_not_leave_new_references_or_remove_original_images()
    {
        var source = Path.Combine(root, "source", "draft.md"); var target = Path.Combine(root, "destination", "saved.md");
        var original = Path.Combine(root, "source", "a.png"); await File.WriteAllBytesAsync(original, png);
        var prepared = await new AssetTransferService().PrepareAsync("![a](a.png)", source, new(Path.GetDirectoryName(source)!), target, new(Path.GetDirectoryName(target)!));
        var copied = Assert.Single(prepared.CreatedPaths);
        await prepared.DisposeAsync();
        Assert.False(File.Exists(copied)); Assert.Equal(png, await File.ReadAllBytesAsync(original)); Assert.False(File.Exists(target));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new AssetTransferService().PrepareAsync("![a](a.png)", source, new(Path.GetDirectoryName(source)!), target, new(Path.GetDirectoryName(target)!), cancelled.Token));
    }

    [Fact] public async Task Save_as_inside_one_workspace_rebases_links_without_copying_images()
    {
        var source = Path.Combine(root, "source", "draft.md"); var target = Path.Combine(root, "destination", "saved.md");
        await using var prepared = await new AssetTransferService().PrepareAsync("![a](a.png)\n[Guide](guide.md)", source, new(root), target, new(root));
        Assert.Empty(prepared.CreatedPaths);
        Assert.Equal("![a](../source/a.png)\n[Guide](../source/guide.md)", prepared.Text);
    }

    [Fact] public async Task Save_as_rejects_workspace_escape_in_image_reference()
    {
        var source = Path.Combine(root, "source", "draft.md"); var target = Path.Combine(root, "destination", "saved.md");
        await Assert.ThrowsAsync<IOException>(() => new AssetTransferService().PrepareAsync("![a](../../outside.png)", source, new(Path.GetDirectoryName(source)!), target, new(Path.GetDirectoryName(target)!)));
    }
    [Fact] public async Task Finalising_a_draft_copies_images_out_of_private_state_even_when_state_is_inside_the_workspace()
    {
        var draftRoot = Path.Combine(root, "source");
        await File.WriteAllBytesAsync(Path.Combine(draftRoot, "image.png"), png);
        await using var prepared = await new AssetTransferService().PrepareAsync("![a](image.png)", Path.Combine(draftRoot, "Untitled.md"), new(draftRoot),
            Path.Combine(root, "saved.md"), new(root), forceCopyImages: true);
        Assert.Single(prepared.CreatedPaths);
        Assert.StartsWith("![a](assets/images/", prepared.Text);
        Assert.DoesNotContain("source/", prepared.Text);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task First_save_retains_ordinary_links_instead_of_rebasing_them_from_private_image_staging(bool stateInsideWorkspace)
    {
        var draftRoot = Path.Combine(root, "source");
        await File.WriteAllBytesAsync(Path.Combine(draftRoot, "image.png"), png);
        var targetRoot = stateInsideWorkspace ? root : Path.Combine(root, "destination");
        const string ordinary = "[Guide](Renamed%20document.md#details)\n[Parent](../guide.md)\n";
        await using var prepared = await new AssetTransferService().PrepareAsync(ordinary + "![Image](image.png)",
            Path.Combine(draftRoot, "Untitled.md"), new(draftRoot), Path.Combine(targetRoot, "saved.md"), new(targetRoot),
            forceCopyImages: true, preserveDocumentLinks: true);
        Assert.StartsWith(ordinary, prepared.Text); Assert.Empty(prepared.UncopiedDocumentLinks);
        Assert.Equal(png, await File.ReadAllBytesAsync(Assert.Single(prepared.CreatedPaths)));
        Assert.DoesNotContain("source/", prepared.Text);
    }
    public void Dispose() => Directory.Delete(root, true);
}
