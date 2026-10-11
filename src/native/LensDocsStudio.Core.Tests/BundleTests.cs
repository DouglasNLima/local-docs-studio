using System.IO.Compression;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Core.Tests;

public sealed class BundleTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "lens-bundle-tests-" + Guid.NewGuid().ToString("N"));
    public BundleTests() => Directory.CreateDirectory(root);
    private string Archive(params (string Path, string Text)[] entries)
    {
        var path = Path.Combine(root, Guid.NewGuid().ToString("N") + ".zip");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, text) in entries) { using var writer = new StreamWriter(zip.CreateEntry(name).Open()); writer.Write(text); }
        return path;
    }
    [Theory]
    [InlineData("../escaped.md")]
    [InlineData("/absolute.md")]
    [InlineData("C:/outside.md")]
    [InlineData("folder/../../escape.md")]
    [InlineData("README.md:stream")]
    public async Task Unsafe_bundle_paths_do_not_escape_or_leave_a_partial_workspace(string name)
    {
        var archive = Archive(("README.md", "# Safe"), (name, "unsafe"));
        var destination = Path.Combine(root, "imported");
        await Assert.ThrowsAsync<InvalidDataException>(() => new MarkdownBundleService().ImportAsync(archive, destination));
        Assert.False(Directory.Exists(destination));
        Assert.False(File.Exists(Path.Combine(root, "escaped.md")));
        Assert.Empty(Directory.EnumerateDirectories(root, ".lens-import-*"));
    }
    [Fact]
    public async Task Case_collisions_are_rejected_without_overwriting()
    {
        var archive = Archive(("README.md", "First"), ("readme.md", "Second"));
        await Assert.ThrowsAsync<InvalidDataException>(() => new MarkdownBundleService().ImportAsync(archive, Path.Combine(root, "imported")));
    }
    [Fact]
    public async Task Generic_bundle_round_trip_preserves_edits_but_excludes_optional_manifest()
    {
        var archive = Archive(("README.md", "# Original"), ("docs/guide.md", "## Guide"), ("lens-artifact-bundle.json", "{}"));
        var imported = await new MarkdownBundleService().ImportAsync(archive, Path.Combine(root, "imported"));
        Assert.Equal(2, imported.Documents);
        var bytes = await new MarkdownBundleService().ExportAsync(new WorkspacePath(imported.Root),
            new Dictionary<string, string> { [Path.Combine(imported.Root, "README.md")] = "# Unsaved changes" });
        using var zip = new ZipArchive(new MemoryStream(bytes));
        Assert.Null(zip.GetEntry("lens-artifact-bundle.json"));
        using var reader = new StreamReader(zip.GetEntry("README.md")!.Open());
        Assert.Equal("# Unsaved changes", await reader.ReadToEndAsync());
        Assert.Equal("# Original", await File.ReadAllTextAsync(Path.Combine(imported.Root, "README.md")));
    }
    public void Dispose() => Directory.Delete(root, true);
}
