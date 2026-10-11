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
    [Fact] public async Task Export_excludes_private_state_and_its_draft_overrides_without_excluding_similarly_named_documents()
    {
        var workspaceRoot = Path.Combine(root, "workspace"); Directory.CreateDirectory(workspaceRoot);
        var state = Path.Combine(workspaceRoot, "state"); Directory.CreateDirectory(Path.Combine(state, "snapshots"));
        var publicFolder = Path.Combine(workspaceRoot, "state-guide"); Directory.CreateDirectory(publicFolder);
        var document = Path.Combine(workspaceRoot, "README.md"); await File.WriteAllTextAsync(document, "# Saved document");
        var privateDocument = Path.Combine(state, "snapshots", "private.md"); await File.WriteAllTextAsync(privateDocument, "Private recovery text");
        await File.WriteAllBytesAsync(Path.Combine(state, "private.png"), [1, 2, 3]);
        await File.WriteAllTextAsync(Path.Combine(publicFolder, "guide.md"), "# Public state guide");
        var image = Path.Combine(workspaceRoot, "image.png"); await File.WriteAllBytesAsync(image, [4, 5, 6]);
        var drafts = new Dictionary<string, string> { [document] = "# Current draft", [privateDocument] = "Private draft", [image] = "Never encode image bytes as text" };
        var bytes = await new MarkdownBundleService().ExportAsync(new WorkspacePath(workspaceRoot), drafts, excludedDirectory: state.ToUpperInvariant() + Path.DirectorySeparatorChar);
        using var archive = new ZipArchive(new MemoryStream(bytes));
        Assert.Equal(new[] { "README.md", "image.png", "state-guide/guide.md" }, archive.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal));
        using var reader = new StreamReader(archive.GetEntry("README.md")!.Open()); Assert.Equal("# Current draft", await reader.ReadToEndAsync());
        using var imageBytes = new MemoryStream(); using var originalImage = archive.GetEntry("image.png")!.Open();
        await originalImage.CopyToAsync(imageBytes); Assert.Equal(new byte[] { 4, 5, 6 }, imageBytes.ToArray());
        Assert.Equal("# Saved document", await File.ReadAllTextAsync(document)); Assert.Equal("Private recovery text", await File.ReadAllTextAsync(privateDocument));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Export_refuses_a_workspace_equal_to_or_inside_private_state(bool child)
    {
        var state = Path.Combine(root, "state"); var workspace = child ? Path.Combine(state, "snapshots") : state; Directory.CreateDirectory(workspace);
        await File.WriteAllTextAsync(Path.Combine(workspace, "private.md"), "Preserve private data");
        await Assert.ThrowsAsync<IOException>(() => new MarkdownBundleService().ExportAsync(new WorkspacePath(workspace), excludedDirectory: state + Path.DirectorySeparatorChar));
        Assert.Equal("Preserve private data", await File.ReadAllTextAsync(Path.Combine(workspace, "private.md")));
    }

    [Fact] public async Task An_unrelated_private_folder_does_not_hide_an_ordinary_workspace_state_folder()
    {
        var workspace = Path.Combine(root, "workspace"); Directory.CreateDirectory(Path.Combine(workspace, "state"));
        await File.WriteAllTextAsync(Path.Combine(workspace, "state", "guide.md"), "# Public documentation");
        var bytes = await new MarkdownBundleService().ExportAsync(new WorkspacePath(workspace), excludedDirectory: Path.Combine(root, "private-state"));
        using var archive = new ZipArchive(new MemoryStream(bytes)); Assert.Equal("state/guide.md", Assert.Single(archive.Entries).FullName);
    }

    [Fact] public async Task Incomplete_folder_enumeration_does_not_return_a_partial_bundle()
    {
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# Retain this");
        var nested = root;
        for (var depth = 0; depth < 34; depth++) { nested = Path.Combine(nested, "d"); Directory.CreateDirectory(nested); }
        var failure = await Assert.ThrowsAsync<IOException>(() => new MarkdownBundleService().ExportAsync(new WorkspacePath(root)));
        Assert.Contains("completely read", failure.Message); Assert.Equal("# Retain this", await File.ReadAllTextAsync(Path.Combine(root, "README.md")));
    }

    [Fact] public async Task Edited_documents_are_bounded_by_encoded_bytes_before_zip_creation()
    {
        var document = Path.Combine(root, "README.md"); await File.WriteAllTextAsync(document, "# Saved");
        var edits = new Dictionary<string, string> { [document] = new string('é', 16 * 1024 * 1024 + 1) };
        var failure = await Assert.ThrowsAsync<IOException>(() => new MarkdownBundleService().ExportAsync(new WorkspacePath(root), edits));
        Assert.Contains("32 MB", failure.Message); Assert.Equal("# Saved", await File.ReadAllTextAsync(document));
    }

    [Fact] public async Task Empty_workspaces_and_cancellation_do_not_produce_bundles()
    {
        await Assert.ThrowsAsync<IOException>(() => new MarkdownBundleService().ExportAsync(new WorkspacePath(root)));
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# Saved");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new MarkdownBundleService().ExportAsync(new WorkspacePath(root), cancellationToken: new(true)));
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public async Task Forged_ZIP_entry_sizes_do_not_leave_an_imported_workspace(int declaredLength)
    {
        var archive = Archive(("README.md", "# Retained source")); var bytes = await File.ReadAllBytesAsync(archive); var central = CentralDirectory(bytes);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(central + 24, 4), (uint)declaredLength);
        await File.WriteAllBytesAsync(archive, bytes);
        var destination = Path.Combine(root, "imported");
        await Assert.ThrowsAsync<InvalidDataException>(() => new MarkdownBundleService().ImportAsync(archive, destination));
        Assert.False(Directory.Exists(destination)); Assert.Empty(Directory.EnumerateDirectories(root, ".lens-import-*"));
    }

    [Fact] public async Task A_failed_content_checksum_does_not_publish_a_partially_imported_workspace()
    {
        var archive = Archive(("README.md", "# Retained source")); var bytes = await File.ReadAllBytesAsync(archive);
        bytes[CentralDirectory(bytes) + 16] ^= 0xff;
        await File.WriteAllBytesAsync(archive, bytes);
        var destination = Path.Combine(root, "imported");
        var failure = await Assert.ThrowsAsync<InvalidDataException>(() => new MarkdownBundleService().ImportAsync(archive, destination));
        Assert.Contains("checksum", failure.Message);
        Assert.False(Directory.Exists(destination)); Assert.Empty(Directory.EnumerateDirectories(root, ".lens-import-*"));
    }

    [Fact] public async Task Valid_empty_documents_and_UTF8_content_round_trip_without_false_checksum_failures()
    {
        var archive = Archive(("README.md", "# Café 日本語\r\nKeep these bytes."), ("empty.md", ""));
        var destination = Path.Combine(root, "imported");
        var result = await new MarkdownBundleService().ImportAsync(archive, destination);
        Assert.Equal(2, result.Documents);
        Assert.Equal("# Café 日本語\r\nKeep these bytes.", await File.ReadAllTextAsync(Path.Combine(destination, "README.md")));
        Assert.Empty(await File.ReadAllBytesAsync(Path.Combine(destination, "empty.md")));
    }

    private static int CentralDirectory(byte[] bytes)
    {
        for (var offset = 0; offset <= bytes.Length - 28; offset++)
            if (bytes[offset] == 0x50 && bytes[offset + 1] == 0x4b && bytes[offset + 2] == 0x01 && bytes[offset + 3] == 0x02) return offset;
        throw new InvalidDataException("The test ZIP has no central directory.");
    }
    public void Dispose() => Directory.Delete(root, true);
}
