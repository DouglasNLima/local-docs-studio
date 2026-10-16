using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LensDocsStudio.Core.Documents;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Core.Tests;

public sealed class LocalLibraryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "lens-content-library-" + Guid.NewGuid().ToString("N"));
    private LocalLibraryStore Store => new(Path.Combine(root, "private-library"));
    private string StoredPath => Path.Combine(Store.DirectoryPath, "library.json");
    private static byte[] Fixture => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "native-local-library.json"));
    private static LocalLibraryData Sample => LocalLibraryFormat.Read(Fixture);
    private static byte[] Change(Action<JsonObject> change)
    {
        var source = JsonNode.Parse(Fixture)!.AsObject(); change(source); return Encoding.UTF8.GetBytes(source.ToJsonString());
    }

    [Fact] public void Browser_library_round_trip_retains_exact_Unicode_CRLF_relative_links_and_profile_values()
    {
        var source = Sample; var template = Assert.Single(source.Templates);
        Assert.Contains("# Café handbook\r\n", template.Content); Assert.Contains("(assets/logo.png)", template.Content);
        Assert.Equal("cafe-handbook.md", template.Name); Assert.True(Assert.Single(source.Profiles).DevopsMarkdownExport);
        var bytes = LocalLibraryFormat.Write(source, DateTimeOffset.Parse("2026-10-12T00:00:00Z")); var result = LocalLibraryFormat.Read(bytes);
        Assert.Equal(source.Templates, result.Templates); Assert.Equal(source.Snippets, result.Snippets); Assert.Equal(source.Profiles, result.Profiles);
        using var document = JsonDocument.Parse(bytes);
        Assert.Equal(LocalLibraryFormat.FormatVersion, document.RootElement.GetProperty("formatVersion").GetString());
        Assert.Equal(template.Content, document.RootElement.GetProperty("templates")[0].GetProperty("content").GetString());
        Assert.Equal("dark", document.RootElement.GetProperty("profiles")[0].GetProperty("docsSite").GetProperty("theme").GetString());
    }

    [Fact] public void Byte_order_mark_missing_unused_collections_and_unknown_metadata_do_not_change_source_text()
    {
        var bytes = Change(json => { json.Remove("profiles"); json.Remove("snippets"); json["untrustedPath"] = "C:/not-authorised"; json["templates"]![0]!["network"] = "https://example.com/not-requested"; });
        var imported = LocalLibraryFormat.Read(new byte[] { 0xef, 0xbb, 0xbf }.Concat(bytes).ToArray());
        Assert.Equal(Sample.Templates, imported.Templates); Assert.Empty(imported.Profiles); Assert.Empty(imported.Snippets);
    }

    [Theory] [InlineData("{}")] [InlineData("[]")] [InlineData("null")] [InlineData("{broken")]
    [InlineData("{\"formatVersion\":\"lens-docs-studio-library-2.0\"}")]
    [InlineData("{\"formatVersion\":\"lens-docs-studio-library-1.0\",\"templates\":null}")]
    [InlineData("{\"formatVersion\":\"lens-docs-studio-library-1.0\",\"profiles\":{}}")]
    [InlineData("{\"formatVersion\":\"lens-docs-studio-library-1.0\",\"formatVersion\":\"lens-docs-studio-library-1.0\"}")]
    public void Unsupported_malformed_or_ambiguous_exports_are_rejected(string source)
        => Assert.Throws<InvalidDataException>(() => LocalLibraryFormat.Read(Encoding.UTF8.GetBytes(source)));

    [Theory] [InlineData("../escape.md")] [InlineData("C:\\outside.md")] [InlineData("folder/inside.md")]
    [InlineData("document.exe")] [InlineData("NUL.md")] [InlineData("COM1.md")] [InlineData("file.md ")]
    public void Imported_template_names_cannot_supply_paths_or_unsafe_save_suggestions(string name)
        => Assert.Throws<InvalidDataException>(() => LocalLibraryFormat.Read(Change(json => json["templates"]![0]!["name"] = name)));

    [Fact] public void Invalid_names_dates_types_and_repeated_identifiers_are_rejected_without_coercion()
    {
        Assert.Throws<InvalidDataException>(() => LocalLibraryFormat.Read(Change(json => json["snippets"]![0]!["label"] = "Bad\nname")));
        Assert.Throws<InvalidDataException>(() => LocalLibraryFormat.Read(Change(json => json["profiles"]![0]!["createdAt"] = -1)));
        Assert.Throws<InvalidDataException>(() => LocalLibraryFormat.Read(Change(json => json["profiles"]![0]!["devopsMarkdownExport"] = "false")));
        Assert.Throws<InvalidDataException>(() => LocalLibraryFormat.Read(Change(json => json["profiles"]![0]!["docsSite"]!["theme"] = "script")));
        Assert.Throws<InvalidDataException>(() => LocalLibraryFormat.Read(Change(json => json["templates"]!.AsArray().Add(json["templates"]![0]!.DeepClone()))));
        Assert.Throws<InvalidDataException>(() => LocalLibraryFormat.Read(Change(json => json["snippets"]![0]!["content"] = "before\0after")));
    }

    [Fact] public void Limits_count_UTF8_bytes_collection_sizes_and_aggregate_content_before_import()
    {
        var sample = Sample; var item = sample.Templates[0];
        Assert.Throws<InvalidDataException>(() => LocalLibraryFormat.Read(new byte[LocalLibraryFormat.MaximumFileBytes + 1]));
        Assert.Throws<InvalidDataException>(() => LocalLibraryFormat.Write(sample with { Templates = [item with { Content = new string('é', 524289) }] }));
        Assert.Throws<InvalidDataException>(() => LocalLibraryFormat.Write(sample with { Templates = Enumerable.Range(0, 201).Select(i => item with { Id = "template-" + i }).ToArray() }));
        Assert.Throws<InvalidDataException>(() => LocalLibraryFormat.Write(sample with { Templates = Enumerable.Range(0, 9).Select(i => item with { Id = "template-" + i, Content = new string('a', 1024 * 1024) }).ToArray() }));
        var tooMany = Change(json => { json["templates"] = new JsonArray(Enumerable.Repeat<JsonNode?>(null, 201).ToArray()); });
        Assert.Contains("too many templates", Assert.Throws<InvalidDataException>(() => LocalLibraryFormat.Read(tooMany)).Message);
    }

    [Fact] public void Merge_retains_existing_entries_and_reassigns_conflicting_ids_without_repeated_import_duplicates()
    {
        var original = Sample;
        var changed = original with
        {
            Templates = [original.Templates[0] with { Content = "# New template\r\n" }],
            Snippets = [original.Snippets[0] with { Label = "Second snippet" }],
            Profiles = [original.Profiles[0] with { DocsSite = original.Profiles[0].DocsSite! with { Theme = "light" } }]
        };
        var merged = LocalLibraryFormat.Merge(original, changed);
        Assert.Equal(3, merged.Added); Assert.Equal(3, merged.ReassignedIds); Assert.Equal(0, merged.AlreadyPresent);
        Assert.Equal(original.Templates[0], merged.Library.Templates[0]); Assert.Equal(changed.Templates[0].Content, merged.Library.Templates[1].Content);
        Assert.NotEqual(original.Templates[0].Id, merged.Library.Templates[1].Id);
        var repeated = LocalLibraryFormat.Merge(merged.Library, changed);
        Assert.Equal(0, repeated.Added); Assert.Equal(3, repeated.AlreadyPresent); Assert.Equal(merged.Library.Templates, repeated.Library.Templates);
    }

    [Fact] public void Archived_identifiers_remain_reserved_and_identical_archived_entries_are_not_silently_restored()
    {
        var archived = Sample; var repeated = LocalLibraryFormat.Merge(LocalLibraryData.Empty, archived, archived);
        Assert.Equal(3, repeated.AlreadyPresent); Assert.Empty(repeated.Library.Templates);
        var changed = archived with { Templates = [archived.Templates[0] with { Content = "# Different" }], Snippets = [], Profiles = [] };
        var merge = LocalLibraryFormat.Merge(LocalLibraryData.Empty, changed, archived);
        Assert.Equal(1, merge.Added); Assert.Equal(1, merge.ReassignedIds); Assert.NotEqual(changed.Templates[0].Id, merge.Library.Templates[0].Id);
    }

    [Fact] public async Task Import_merge_and_new_store_instance_preserve_every_existing_entry_and_source_export()
    {
        Directory.CreateDirectory(root); var sourcePath = Path.Combine(root, "legacy.json"); await File.WriteAllBytesAsync(sourcePath, Fixture);
        var initial = await Store.ReadAsync(); var first = await Store.ImportAsync(initial, await File.ReadAllBytesAsync(sourcePath));
        var changed = Sample with { Templates = [Sample.Templates[0] with { Content = "# Another template" }], Snippets = [], Profiles = [] };
        var second = await Store.ImportAsync(first.Snapshot, LocalLibraryFormat.Write(changed));
        var restored = await Store.ReadAsync(); Assert.Equal(2, restored.State.Active.Templates.Length);
        Assert.Equal(1, second.ReassignedIds); Assert.Equal(Sample.Templates[0], restored.State.Active.Templates[0]);
        Assert.Single(restored.State.Active.Snippets); Assert.Single(restored.State.Active.Profiles); Assert.Equal(Fixture, await File.ReadAllBytesAsync(sourcePath));
    }

    [Theory] [InlineData(LocalLibraryKind.Template)] [InlineData(LocalLibraryKind.Snippet)] [InlineData(LocalLibraryKind.ExportProfile)]
    public async Task Rename_archive_and_restore_are_persistent_and_retain_content(LocalLibraryKind kind)
    {
        var initial = (await Store.ImportAsync(await Store.ReadAsync(), Fixture)).Snapshot;
        var id = kind == LocalLibraryKind.Template ? Sample.Templates[0].Id : kind == LocalLibraryKind.Snippet ? Sample.Snippets[0].Id : Sample.Profiles[0].Id;
        var renamed = await Store.RenameAsync(initial, kind, id, "  Renamed Café  ");
        var archived = await Store.MoveAsync(renamed, kind, id, true);
        var loaded = await Store.ReadAsync(); Assert.Equal(archived.File!.Fingerprint, loaded.File!.Fingerprint);
        var restored = await Store.MoveAsync(loaded, kind, id, false);
        Assert.Empty(restored.State.Archived.Templates); Assert.Empty(restored.State.Archived.Snippets); Assert.Empty(restored.State.Archived.Profiles);
        if (kind == LocalLibraryKind.Template) Assert.Equal(Sample.Templates[0] with { Label = "Renamed Café" }, Assert.Single(restored.State.Active.Templates));
        else if (kind == LocalLibraryKind.Snippet) Assert.Equal(Sample.Snippets[0] with { Label = "Renamed Café" }, Assert.Single(restored.State.Active.Snippets));
        else Assert.Equal(Sample.Profiles[0] with { Label = "Renamed Café" }, Assert.Single(restored.State.Active.Profiles));
    }

    [Fact] public async Task Stale_external_changes_and_foreign_snapshots_cannot_overwrite_content()
    {
        var initial = (await Store.ImportAsync(await Store.ReadAsync(), Fixture)).Snapshot;
        var renamed = await Store.RenameAsync(initial, LocalLibraryKind.Template, Sample.Templates[0].Id, "Changed elsewhere");
        var bytes = await File.ReadAllBytesAsync(StoredPath);
        await Assert.ThrowsAsync<DocumentConflictException>(() => Store.ImportAsync(initial, Fixture));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(StoredPath));
        var other = new LocalLibraryStore(Path.Combine(root, "other-library"));
        await Assert.ThrowsAsync<ArgumentException>(() => other.SaveAsync(renamed, renamed.State));
        Assert.False(File.Exists(Path.Combine(other.DirectoryPath, "library.json")));
    }

    [Fact] public async Task Corrupt_saved_state_and_invalid_import_are_retained_without_resetting_existing_content()
    {
        var initial = (await Store.ImportAsync(await Store.ReadAsync(), Fixture)).Snapshot; var original = await File.ReadAllBytesAsync(StoredPath);
        await Assert.ThrowsAsync<InvalidDataException>(() => Store.ImportAsync(initial, Encoding.UTF8.GetBytes("{}")));
        Assert.Equal(original, await File.ReadAllBytesAsync(StoredPath));
        await File.WriteAllTextAsync(StoredPath, "{ broken");
        await Assert.ThrowsAsync<InvalidDataException>(() => Store.ReadAsync()); Assert.Equal("{ broken", await File.ReadAllTextAsync(StoredPath));
    }

    [Fact] public async Task Cancellation_preserves_saved_bytes_and_does_not_create_a_new_library()
    {
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var empty = await Store.ReadAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Store.ImportAsync(empty, Fixture, cancelled.Token)); Assert.False(File.Exists(StoredPath));
        var initial = (await Store.ImportAsync(empty, Fixture)).Snapshot; var original = await File.ReadAllBytesAsync(StoredPath);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Store.RenameAsync(initial, LocalLibraryKind.Template, Sample.Templates[0].Id, "Cancelled", cancellationToken: cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Store.MoveAsync(initial, LocalLibraryKind.Snippet, Sample.Snippets[0].Id, true, cancelled.Token));
        Assert.Equal(original, await File.ReadAllBytesAsync(StoredPath));
    }

    [Fact] public async Task Unsupported_state_and_cross_archive_identifier_collisions_fail_before_writing()
    {
        var initial = (await Store.ImportAsync(await Store.ReadAsync(), Fixture)).Snapshot; var original = await File.ReadAllBytesAsync(StoredPath);
        await Assert.ThrowsAsync<InvalidDataException>(() => Store.SaveAsync(initial, initial.State with { SchemaVersion = 2 }));
        await Assert.ThrowsAsync<InvalidDataException>(() => Store.SaveAsync(initial, initial.State with { Archived = Sample }));
        Assert.Equal(original, await File.ReadAllBytesAsync(StoredPath));
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
