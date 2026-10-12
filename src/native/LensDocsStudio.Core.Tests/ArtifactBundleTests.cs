using System.IO.Compression;
using System.Text;
using System.Text.Json;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;

namespace LensDocsStudio.Core.Tests;

public sealed class ArtifactBundleTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "lens-artefacts-" + Guid.NewGuid().ToString("N"));
    public ArtifactBundleTests() => Directory.CreateDirectory(root);
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "artifact-bundles", name);
    private async Task<BundleImportResult> ImportFixture(string name)
    {
        var archive = Path.Combine(root, name + ".zip"); ZipFile.CreateFromDirectory(Fixture(name), archive);
        return await new MarkdownBundleService().ImportAsync(archive, Path.Combine(root, name));
    }

    [Fact] public async Task Rich_fixture_retains_supplied_evidence_and_only_imported_document_targets()
    {
        var imported = await ImportFixture("valid-rich"); var bundle = Assert.IsType<ArtifactBundle>(imported.ArtifactBundle);
        Assert.Equal("Rich Artefact Bundle", bundle.Title); Assert.Equal("README.md", bundle.EntryDocument);
        Assert.Equal(7, bundle.EvidenceLevels.Count); Assert.True(bundle.HasCandidateFindings);
        Assert.Equal("candidate finding", bundle.Find("findings/candidate-risk.md")!.EvidenceLevel);
        Assert.Equal("confirmed finding", bundle.Find("findings/confirmed-control.md")!.EvidenceLevel);
        Assert.Equal(2, bundle.Warnings.Count);
        Assert.Equal("blocked/unavailable evidence", Assert.Single(bundle.Warnings, warning => warning.Code == "CONNECTED_METADATA_UNAVAILABLE").EvidenceLevel);
        Assert.Equal("Diagrams", Assert.Single(bundle.Diagrams).Group);
        Assert.False(File.Exists(Path.Combine(imported.Root, ArtifactBundleManifest.Filename)));
        Assert.Equal(File.ReadAllText(Path.Combine(Fixture("valid-rich"), "README.md")), File.ReadAllText(Path.Combine(imported.Root, "README.md")));
        Assert.All(bundle.Entries, entry => Assert.True(File.Exists(new WorkspacePath(imported.Root).Resolve(entry.Path))));
    }

    [Theory] [InlineData("generic-zip")] [InlineData("invalid-manifest")]
    public async Task Generic_or_invalid_metadata_keeps_ordinary_documents_usable(string fixture)
    {
        var imported = await ImportFixture(fixture);
        Assert.Null(imported.ArtifactBundle); Assert.True(imported.Documents >= 2);
        Assert.False(File.Exists(Path.Combine(imported.Root, ArtifactBundleManifest.Filename)));
        if (fixture == "invalid-manifest") Assert.Contains(imported.Warnings, warning => warning.StartsWith("Artefact bundle manifest", StringComparison.Ordinal));
    }

    [Fact] public async Task Unsafe_metadata_fixture_cannot_authorise_external_or_missing_paths()
    {
        var imported = await ImportFixture("unsafe-paths"); var bundle = Assert.IsType<ArtifactBundle>(imported.ArtifactBundle);
        Assert.Equal("", bundle.EntryDocument); Assert.Equal(2, bundle.Documents.Count); Assert.NotEmpty(imported.Warnings);
        Assert.Equal(new[] { "README.md", "safe/visible.md" }, bundle.Documents.Select(entry => entry.Path));
        Assert.Equal("candidate finding", bundle.Find("safe/visible.md")!.EvidenceLevel);
        Assert.False(File.Exists(Path.Combine(root, "secrets.md")));
    }

    [Theory]
    [InlineData("../outside.md")] [InlineData("/outside.md")] [InlineData("C:\\outside.md")]
    [InlineData("https://example.invalid/file.md")] [InlineData("file:///outside.md")] [InlineData("a/./b.md")]
    [InlineData("a\u0001b.md")] [InlineData("a.md:stream")] [InlineData("\\\\server\\share\\a.md")]
    public void Untrusted_metadata_paths_are_rejected(string path) => Assert.Equal("", ArtifactBundleManifest.NormalisePath(path));

    [Fact] public void Display_values_are_bounded_literal_text_and_unknown_fields_do_not_survive_export()
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { formatVersion = ArtifactBundleManifest.FormatVersion,
            title = "  <img onerror=run()>\n" + new string('x', 300), summary = new string('s', 1000), endpoint = "https://example.invalid/private",
            evidenceLevels = new[] { " candidate finding ", "invented runtime result" },
            documents = new[] { new { path = "README.md", title = "javascript:run()", kind = "summary", evidenceLevel = "invented label", order = "2.5" } },
            warnings = Enumerable.Range(0, 30).Select(index => new { code = "W" + index, message = "Unverified", evidenceLevel = "candidate finding" }).ToArray() });
        var result = ArtifactBundleManifest.Parse(bytes, ["README.md"]); var bundle = Assert.IsType<ArtifactBundle>(result.Bundle);
        Assert.Equal(180, bundle.Title.Length); Assert.Equal(420, bundle.Summary.Length); Assert.DoesNotContain('\n', bundle.Title);
        Assert.Equal("candidate finding", Assert.Single(bundle.EvidenceLevels)); Assert.Equal(20, bundle.Warnings.Count);
        Assert.Equal("", Assert.Single(bundle.Documents).EvidenceLevel); Assert.Equal(2.5, bundle.Documents[0].Order);
        using var exported = JsonDocument.Parse(ArtifactBundleManifest.BuildReviewManifest(bundle, ["README.md", "new.md"]));
        Assert.False(exported.RootElement.TryGetProperty("endpoint", out _));
        var entry = exported.RootElement.GetProperty("documents")[0]; Assert.False(entry.TryGetProperty("title", out _)); Assert.False(entry.TryGetProperty("evidenceLevel", out _));
        Assert.False(exported.RootElement.GetProperty("documents")[1].TryGetProperty("evidenceLevel", out _));
    }

    [Theory] [InlineData("null")] [InlineData("[]")] [InlineData("{broken")]
    [InlineData("{\"formatVersion\":\"future\"}")]
    public void Unsupported_manifest_shapes_return_a_warning_without_throwing(string json)
    {
        var result = ArtifactBundleManifest.Parse(Encoding.UTF8.GetBytes(json), ["README.md"]);
        Assert.Null(result.Bundle); Assert.Single(result.Warnings);
    }

    [Fact] public async Task Oversized_optional_metadata_is_skipped_without_discarding_imported_source()
    {
        var archive = Path.Combine(root, "oversized.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("README.md").Open())) writer.Write("# Kept");
            using var metadata = new StreamWriter(zip.CreateEntry(ArtifactBundleManifest.Filename).Open()); metadata.Write(new string('x', ArtifactBundleManifest.MaximumBytes + 1));
        }
        var result = await new MarkdownBundleService().ImportAsync(archive, Path.Combine(root, "imported"));
        Assert.Null(result.ArtifactBundle); Assert.Contains(result.Warnings, warning => warning.Contains("too large", StringComparison.Ordinal));
        Assert.Equal("# Kept", File.ReadAllText(Path.Combine(result.Root, "README.md")));
    }

    [Fact] public async Task Explicit_review_pack_rebuilds_safe_metadata_and_current_source_while_generic_export_stays_clean()
    {
        var imported = await ImportFixture("valid-rich"); var bundle = imported.ArtifactBundle!; var workspace = new WorkspacePath(imported.Root);
        var path = workspace.Resolve("findings/candidate-risk.md"); var original = await File.ReadAllTextAsync(path);
        var edited = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [path] = "# Still a candidate\n\nUnsaved review notes." };
        File.Delete(workspace.Resolve("findings/confirmed-control.md"));
        File.WriteAllText(workspace.Resolve("new.md"), "# New unlabelled document");
        var service = new MarkdownBundleService();
        var genericBytes = await service.ExportAsync(workspace, edited);
        using (var generic = new ZipArchive(new MemoryStream(genericBytes))) Assert.Null(generic.GetEntry(ArtifactBundleManifest.Filename));
        var reviewed = await service.ExportReviewPackAsync(workspace, bundle, edited);
        using var archive = new ZipArchive(new MemoryStream(reviewed));
        using var manifest = new MemoryStream(); await archive.GetEntry(ArtifactBundleManifest.Filename)!.Open().CopyToAsync(manifest);
        var parsed = ArtifactBundleManifest.Parse(manifest.ToArray(), archive.Entries.Select(entry => entry.FullName)).Bundle!;
        Assert.Equal("candidate finding", parsed.Find("findings/candidate-risk.md")!.EvidenceLevel);
        Assert.Null(parsed.Find("findings/confirmed-control.md")); Assert.Equal("", parsed.Find("new.md")!.EvidenceLevel);
        using var reader = new StreamReader(archive.GetEntry("findings/candidate-risk.md")!.Open()); Assert.Equal(edited[path], await reader.ReadToEndAsync());
        Assert.Equal(original, await File.ReadAllTextAsync(path));
        var packPath = Path.Combine(root, "reviewed.zip"); await File.WriteAllBytesAsync(packPath, reviewed);
        var roundTrip = await service.ImportAsync(packPath, Path.Combine(root, "round-trip"));
        Assert.Equal("candidate finding", roundTrip.ArtifactBundle!.Find("findings/candidate-risk.md")!.EvidenceLevel);
        Assert.False(File.Exists(Path.Combine(roundTrip.Root, ArtifactBundleManifest.Filename)));
    }

    [Fact] public void Only_imported_documents_gain_metadata_and_duplicate_paths_keep_the_first_entry()
    {
        var bytes = Encoding.UTF8.GetBytes("""{"formatVersion":"lens-artifact-bundle-1.0","entryDocument":"image.png","documents":[{"path":"README.md","title":"First"},{"path":"README.md","title":"Second"},{"path":"missing.md"},{"path":"image.png"}],"diagrams":false,"warnings":"invalid","evidenceLevels":false}""");
        var result = ArtifactBundleManifest.Parse(bytes, ["README.md", "image.png"]); var bundle = result.Bundle!;
        Assert.Equal("First", Assert.Single(bundle.Documents).Title); Assert.Empty(bundle.Diagrams); Assert.Empty(bundle.Warnings); Assert.Empty(bundle.EvidenceLevels);
        Assert.Equal("", bundle.EntryDocument); Assert.Equal(5, result.Warnings.Count);
    }

    [Fact] public async Task Docs_site_uses_artefact_fallbacks_after_front_matter_and_keeps_candidates_display_only()
    {
        var imported = await ImportFixture("front-matter-precedence"); var bundle = imported.ArtifactBundle!;
        var exporter = new DocsSiteExporter();
        DocsSitePage Page(string relative) => exporter.PreparePage(relative, new(new MarkdownService().Parse(File.ReadAllText(Path.Combine(imported.Root, relative))),
            new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>()), bundle);
        var home = Page("README.md"); var finding = Page("findings/security-summary.md");
        Assert.Equal("Front Matter Home", home.Title); Assert.Equal(99, home.Metadata.Order); Assert.Equal("Front Matter", home.Metadata.NavigationGroup);
        Assert.Equal("Manifest Finding", finding.Title); Assert.Equal(2, finding.Metadata.Order); Assert.Equal("Findings", finding.Metadata.NavigationGroup);
        Assert.Equal("candidate finding", finding.EvidenceLevel); Assert.Equal("finding", finding.ArtifactKind);
        var result = exporter.Export([home, finding], new("Review site"), artifactBundle: bundle);
        using var archive = new ZipArchive(new MemoryStream(result.Bytes));
        Assert.Null(archive.GetEntry(ArtifactBundleManifest.Filename));
        using var reader = new StreamReader(archive.GetEntry("assets/search-index.json")!.Open()); using var index = JsonDocument.Parse(await reader.ReadToEndAsync());
        var metadata = index.RootElement.GetProperty("artifactBundle"); Assert.True(metadata.GetProperty("candidateFindings").GetBoolean());
        using var siteManifest = JsonDocument.Parse(archive.GetEntry("site-manifest.json")!.Open());
        Assert.True(siteManifest.RootElement.GetProperty("artifactBundle").GetProperty("candidateFindings").GetBoolean());
        Assert.Contains("Candidate findings remain candidate findings.", metadata.GetProperty("notes").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal("README.md", metadata.GetProperty("entryDocument").GetString());
        Assert.Equal("Manifest Finding", index.RootElement.GetProperty("pages")[0].GetProperty("title").GetString());
        Assert.All(index.RootElement.GetProperty("entries").EnumerateArray().Where(entry => entry.GetProperty("pageId").GetString() == finding.Id),
            entry => Assert.Contains("candidate finding", entry.GetProperty("text").GetString(), StringComparison.Ordinal));
        Assert.Equal("candidate finding", bundle.Find("findings/security-summary.md")!.EvidenceLevel);
    }

    [Fact] public void Docs_site_does_not_infer_evidence_or_open_uri_like_metadata()
    {
        var source = """{"formatVersion":"lens-artifact-bundle-1.0","title":"javascript:run()","sourceTool":"https://example.invalid/","entryDocument":"missing.md","documents":[{"path":"README.md","title":"file:///secret.md","kind":"javascript:run()","evidenceLevel":"unknown proof"}]}""";
        var bundle = ArtifactBundleManifest.Parse(Encoding.UTF8.GetBytes(source), ["README.md"]).Bundle!;
        var page = new DocsSiteExporter().PreparePage("README.md", new(new MarkdownService().Parse("# Actual title"), new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>()), bundle);
        Assert.Equal("Actual title", page.Title); Assert.Equal("", page.ArtifactKind); Assert.Equal("", page.EvidenceLevel);
        var metadata = ArtifactBundleManifest.BuildSiteMetadata(bundle, ["README.md"]);
        Assert.False(metadata.ContainsKey("title")); Assert.False(metadata.ContainsKey("sourceTool")); Assert.False(metadata.ContainsKey("entryDocument"));
        Assert.False(metadata.ContainsKey("evidenceLevels")); Assert.False(metadata.ContainsKey("candidateFindings"));
    }

    [Fact] public void Utf8_bom_is_supported_while_invalid_utf8_and_excessive_nesting_are_ignored()
    {
        var source = Encoding.UTF8.GetBytes("""{"formatVersion":"lens-artifact-bundle-1.0","title":"Café"}""");
        Assert.Equal("Café", ArtifactBundleManifest.Parse(new byte[] { 0xef, 0xbb, 0xbf }.Concat(source).ToArray(), []).Bundle!.Title);
        var invalid = Encoding.UTF8.GetBytes("{\"title\":\"").Concat(new byte[] { 0xff }).Concat(Encoding.UTF8.GetBytes("\",\"formatVersion\":\"lens-artifact-bundle-1.0\"}")).ToArray();
        Assert.Null(ArtifactBundleManifest.Parse(invalid, []).Bundle);
        Assert.Null(ArtifactBundleManifest.Parse(Encoding.UTF8.GetBytes("{\"formatVersion\":\"lens-artifact-bundle-1.0\",\"title\":\"\\ud800\"}"), []).Bundle);
        Assert.Null(ArtifactBundleManifest.Parse(Encoding.UTF8.GetBytes(new string('[', 40) + new string(']', 40)), []).Bundle);
    }

    public void Dispose() => Directory.Delete(root, true);
}
