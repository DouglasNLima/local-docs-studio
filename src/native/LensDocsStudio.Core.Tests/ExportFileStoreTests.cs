using LensDocsStudio.Core.Exports;

namespace LensDocsStudio.Core.Tests;

public sealed class ExportFileStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "lens-export-test-" + Guid.NewGuid().ToString("N"));
    public ExportFileStoreTests() => Directory.CreateDirectory(root);

    [Fact]
    public async Task Changed_export_is_not_overwritten()
    {
        var path = Path.Combine(root, "file.pdf");
        await File.WriteAllBytesAsync(path, [1, 2, 3]);
        var expected = await ExportFileStore.ReadExistingAsync(path);
        await File.WriteAllBytesAsync(path, [3, 2, 1]);
        await Assert.ThrowsAsync<IOException>(() => new ExportFileStore(Path.Combine(root, "recovery")).WriteAsync(path, [9], expected));
        Assert.Equal(new byte[] { 3, 2, 1 }, await File.ReadAllBytesAsync(path));
    }
    [Fact]
    public async Task Successful_export_replaces_bytes_and_completes_its_journal()
    {
        var path = Path.Combine(root, "file.pdf"); var recovery = Path.Combine(root, "recovery");
        await File.WriteAllBytesAsync(path, [1, 2, 3]);
        await new ExportFileStore(recovery).WriteAsync(path, [9], await ExportFileStore.ReadExistingAsync(path));
        Assert.Equal(new byte[] { 9 }, await File.ReadAllBytesAsync(path));
        Assert.Empty(Directory.EnumerateFiles(recovery));
    }
    [Fact]
    public async Task New_export_does_not_overwrite_a_file_created_since_the_dialogue()
    {
        var path = Path.Combine(root, "file.pdf");
        await File.WriteAllBytesAsync(path, [1]);
        await Assert.ThrowsAsync<IOException>(() => new ExportFileStore(root).WriteAsync(path, [9], null));
        Assert.Equal(new byte[] { 1 }, await File.ReadAllBytesAsync(path));
        Assert.Single(Directory.EnumerateFiles(root));
    }
    [Fact]
    public async Task Cancelled_export_preserves_the_destination()
    {
        var path = Path.Combine(root, "file.pdf");
        await File.WriteAllBytesAsync(path, [1]);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ExportFileStore(root).WriteAsync(path, [9], [1], cancellation.Token));
        Assert.Equal(new byte[] { 1 }, await File.ReadAllBytesAsync(path));
    }
    public void Dispose() => Directory.Delete(root, true);
}
