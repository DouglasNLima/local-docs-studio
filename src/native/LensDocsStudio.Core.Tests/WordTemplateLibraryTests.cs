using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using LensDocsStudio.Core.Documents;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Core.Tests;

public sealed class WordTemplateLibraryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "lens-template-library-" + Guid.NewGuid().ToString("N"));
    private WordTemplateLibrary Library => new(Path.Combine(root, "library"));
    private string PathFor(WordTemplateEntry entry, string name) => Path.Combine(Library.DirectoryPath, entry.Archived ? "archived" : "", entry.Record.Id.ToString("N"), name);
    private async Task<string> Source(bool active = false)
    {
        Directory.CreateDirectory(root); var path = Path.Combine(root, Guid.NewGuid().ToString("N") + ".docx");
        using var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = package.AddMainDocumentPart(); var header = main.AddNewPart<HeaderPart>();
            header.Header = new W.Header(new W.Paragraph(new W.Run(new W.Text("Retained generic header"))));
            if (active) header.Header.Append(new W.Paragraph(new W.SimpleField { Instruction = "INCLUDETEXT https://example.com/untrusted" }));
            main.Document = new W.Document(new W.Body(new W.Paragraph(new W.Run(new W.Text("Original confidential body"))),
                new W.SectionProperties(new W.HeaderReference { Id = main.GetIdOfPart(header), Type = W.HeaderFooterValues.Default })));
            package.PackageProperties.Creator = "Original private author";
        }
        await File.WriteAllBytesAsync(path, stream.ToArray()); return path;
    }

    [Fact] public async Task Imported_templates_survive_a_new_library_instance_without_original_body_or_file_changes()
    {
        var source = await Source(); var original = await File.ReadAllBytesAsync(source); var imported = await Library.ImportAsync(source, "Café handbook");
        var listed = Assert.Single((await Library.ListAsync()).Entries); Assert.Equal(imported.Record, listed.Record with { Warnings = imported.Record.Warnings });
        Assert.Equal(Path.GetFileName(source), listed.Record.SourceName); Assert.Equal(original, await File.ReadAllBytesAsync(source));
        var prepared = await Library.ReadAsync(listed); using var stream = new MemoryStream(prepared.Bytes); using var package = WordprocessingDocument.Open(stream, false);
        Assert.DoesNotContain("Original confidential body", package.MainDocumentPart!.Document!.InnerText);
        Assert.Equal("Retained generic header", package.MainDocumentPart.HeaderParts.Single().Header!.InnerText);
        Assert.NotEqual("Original private author", package.PackageProperties.Creator);
        File.Delete(source); Assert.NotEmpty((await Library.ReadAsync(listed)).Bytes);
    }

    [Fact] public async Task Repeated_import_names_create_independent_copies_without_overwriting_existing_templates()
    {
        var source = await Source(); var first = await Library.ImportAsync(source, "Handbook"); var firstBytes = await File.ReadAllBytesAsync(PathFor(first, "template.docx"));
        var second = await Library.ImportAsync(source, "Handbook"); Assert.NotEqual(first.Record.Id, second.Record.Id);
        Assert.Equal(2, (await Library.ListAsync()).Entries.Count); Assert.Equal(firstBytes, await File.ReadAllBytesAsync(PathFor(first, "template.docx")));
    }

    [Fact] public async Task Renaming_changes_only_metadata_and_rejects_stale_selection_operations()
    {
        var imported = await Library.ImportAsync(await Source(), "Original"); var bytes = await File.ReadAllBytesAsync(PathFor(imported, "template.docx"));
        var renamed = await Library.RenameAsync(imported, "  Renamed Café  "); Assert.Equal("Renamed Café", renamed.Name);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(PathFor(renamed, "template.docx"))); Assert.Equal("Renamed Café", Assert.Single((await Library.ListAsync()).Entries).Name);
        await Assert.ThrowsAsync<DocumentConflictException>(() => Library.RenameAsync(imported, "Stale rename"));
        await Assert.ThrowsAsync<DocumentConflictException>(() => Library.ReadAsync(imported));
        await Assert.ThrowsAsync<DocumentConflictException>(() => Library.ArchiveAsync(imported));
        Assert.NotEmpty((await Library.ReadAsync(renamed)).Bytes);
    }

    [Fact] public async Task Archiving_and_restoring_preserve_identical_library_bytes_and_original_documents()
    {
        var source = await Source(); var original = await File.ReadAllBytesAsync(source); var imported = await Library.ImportAsync(source, "Reusable");
        var bytes = await File.ReadAllBytesAsync(PathFor(imported, "template.docx")); var archived = await Library.ArchiveAsync(imported);
        Assert.Empty((await Library.ListAsync()).Entries); Assert.True(Assert.Single((await Library.ListAsync(true)).Entries).Archived);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(PathFor(archived, "template.docx"))); Assert.NotEmpty((await Library.ReadAsync(archived)).Bytes);
        var restored = await Library.RestoreAsync(archived); Assert.False(restored.Archived); Assert.Empty((await Library.ListAsync(true)).Entries);
        Assert.Single((await Library.ListAsync()).Entries); Assert.Equal(bytes, await File.ReadAllBytesAsync(PathFor(restored, "template.docx")));
        Assert.Equal(original, await File.ReadAllBytesAsync(source));
    }

    [Theory] [InlineData("")] [InlineData("   ")] [InlineData("Bad\nname")] [InlineData("Bad\0name")]
    public async Task Invalid_template_names_do_not_publish_library_entries(string name)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Library.ImportAsync(Path.Combine(root, "unused.docx"), name));
        Assert.Empty((await Library.ListAsync()).Entries);
    }

    [Fact] public async Task Overlong_names_and_active_templates_do_not_replace_the_existing_library()
    {
        var source = await Source(); var existing = await Library.ImportAsync(source, "Retained");
        await Assert.ThrowsAsync<ArgumentException>(() => Library.ImportAsync(source, new string('a', 121)));
        var active = await Source(true); var activeBytes = await File.ReadAllBytesAsync(active);
        await Assert.ThrowsAsync<InvalidDataException>(() => Library.ImportAsync(active, "Untrusted"));
        Assert.Equal(existing.Record.Id, Assert.Single((await Library.ListAsync()).Entries).Record.Id); Assert.Equal(activeBytes, await File.ReadAllBytesAsync(active));
        Assert.Empty(Directory.GetDirectories(Library.DirectoryPath, ".import-*"));
    }

    [Fact] public async Task Corrupt_metadata_is_reported_and_preserved_while_valid_entries_remain_usable()
    {
        var source = await Source(); var broken = await Library.ImportAsync(source, "Broken"); var good = await Library.ImportAsync(source, "Good");
        var path = PathFor(broken, "record.json"); await File.WriteAllTextAsync(path, "{ broken");
        var listing = await Library.ListAsync(); Assert.Single(listing.Warnings); Assert.Equal(good.Record.Id, Assert.Single(listing.Entries).Record.Id);
        Assert.Equal("{ broken", await File.ReadAllTextAsync(path)); Assert.True(File.Exists(PathFor(broken, "template.docx")));
        Assert.NotEmpty((await Library.ReadAsync(good)).Bytes);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Changed_package_size_or_checksum_is_rejected_without_changing_stored_bytes(bool changeSize)
    {
        var imported = await Library.ImportAsync(await Source(), "Template"); var path = PathFor(imported, "template.docx"); var bytes = await File.ReadAllBytesAsync(path);
        if (changeSize) bytes = [.. bytes, 0]; else bytes[0] ^= 0xff;
        await File.WriteAllBytesAsync(path, bytes);
        await Assert.ThrowsAsync<IOException>(() => Library.ReadAsync(imported)); Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
    }

    [Fact] public async Task Recomputed_untrusted_metadata_cannot_authorise_an_active_template()
    {
        var imported = await Library.ImportAsync(await Source(), "Template"); var unsafeBytes = await File.ReadAllBytesAsync(await Source(true));
        await File.WriteAllBytesAsync(PathFor(imported, "template.docx"), unsafeBytes);
        await File.WriteAllTextAsync(PathFor(imported, "record.json"), JsonSerializer.Serialize(imported.Record with { Bytes = unsafeBytes.Length, Sha256 = DocumentSnapshot.Hash(unsafeBytes) }));
        var entry = Assert.Single((await Library.ListAsync()).Entries);
        await Assert.ThrowsAsync<InvalidDataException>(() => Library.ReadAsync(entry));
    }

    [Fact] public async Task Foreign_record_ids_and_oversized_metadata_do_not_redirect_or_authorise_reads()
    {
        var imported = await Library.ImportAsync(await Source(), "Template"); var path = PathFor(imported, "record.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(imported.Record with { Id = Guid.NewGuid(), SourceName = "https://example.com/not-a-file-authorisation" }));
        Assert.Single((await Library.ListAsync()).Warnings); Assert.Empty((await Library.ListAsync()).Entries);
        await File.WriteAllTextAsync(path, new string('x', 16385)); Assert.Single((await Library.ListAsync()).Warnings); Assert.Equal(16385, new FileInfo(path).Length);
    }

    [Fact] public async Task Cancellation_preserves_library_metadata_packages_and_source_files()
    {
        var source = await Source(); var imported = await Library.ImportAsync(source, "Retained");
        var metadata = await File.ReadAllBytesAsync(PathFor(imported, "record.json")); var bytes = await File.ReadAllBytesAsync(PathFor(imported, "template.docx"));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Library.ImportAsync(source, "Cancelled", cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Library.RenameAsync(imported, "Cancelled", cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Library.ArchiveAsync(imported, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Library.ReadAsync(imported, cancellation.Token));
        Assert.Equal(metadata, await File.ReadAllBytesAsync(PathFor(imported, "record.json"))); Assert.Equal(bytes, await File.ReadAllBytesAsync(PathFor(imported, "template.docx")));
        Assert.Single((await Library.ListAsync()).Entries); Assert.Empty(Directory.GetDirectories(Library.DirectoryPath, ".import-*"));
    }

    [Fact] public async Task An_existing_library_lock_prevents_concurrent_mutation_without_publishing_partial_entries()
    {
        var source = await Source(); Directory.CreateDirectory(Library.DirectoryPath);
        using (var held = new FileStream(Path.Combine(Library.DirectoryPath, "library.lock"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            await Assert.ThrowsAsync<IOException>(() => Library.ImportAsync(source, "Busy"));
        Assert.Empty((await Library.ListAsync()).Entries); Assert.Empty(Directory.GetDirectories(Library.DirectoryPath, ".import-*"));
        Assert.NotNull(await Library.ImportAsync(source, "Available"));
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
