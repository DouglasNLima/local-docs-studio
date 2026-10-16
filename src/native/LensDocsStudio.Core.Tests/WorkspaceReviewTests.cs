using LensDocsStudio.Core.Review;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Core.Tests;

public sealed class WorkspaceReviewTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "lens-document-review-" + Guid.NewGuid().ToString("N"));
    private string Workspace => Path.Combine(root, "workspace");
    private string SourcePath => Path.Combine(Workspace, "Source.md");
    private WorkspaceDocumentReviewService Service => new(new DocumentStore(Path.Combine(root, "recovery")), Path.Combine(Workspace, "state"));
    public WorkspaceReviewTests() { Directory.CreateDirectory(Workspace); Directory.CreateDirectory(Path.Combine(root, "outside")); }

    [Fact] public async Task Wiki_review_checks_drafts_fragments_missing_and_ambiguous_targets_without_writes()
    {
        Directory.CreateDirectory(Path.Combine(Workspace, "one")); Directory.CreateDirectory(Path.Combine(Workspace, "two"));
        var target = Path.Combine(Workspace, "Target.md"); await File.WriteAllTextAsync(target, "# Saved target");
        await File.WriteAllTextAsync(Path.Combine(Workspace, "one", "Summary.md"), "# One");
        await File.WriteAllTextAsync(Path.Combine(Workspace, "two", "Summary.md"), "# Two");
        const string source = "# Source\r\n\r\n[[Target#new-heading|Current]] [[Target#saved-target|Old]] [[Missing]] [[Summary]] [[#source]]";
        var result = await Service.AnalyseAsync(source, SourcePath, new(Workspace), new Dictionary<string, string> { [target] = "# New heading" });
        Assert.Equal(5, result.Metrics.Links); Assert.Equal(3, result.Issues.Count); Assert.Equal(WorkspaceLinkReviewState.Complete, result.WorkspaceLinks);
        Assert.Contains(result.Issues, issue => source.Substring(issue.Start, issue.Length) == "[[Target#saved-target|Old]]" && issue.Message.Contains("section is missing", StringComparison.Ordinal));
        Assert.Contains(result.Issues, issue => issue.Message.Contains("ambiguous", StringComparison.Ordinal));
        Assert.Contains(result.Issues, issue => issue.Message.Contains("No workspace document", StringComparison.Ordinal));
        Assert.Equal("# Saved target", await File.ReadAllTextAsync(target)); Assert.False(File.Exists(SourcePath));
    }

    [Fact] public async Task Wiki_review_excludes_private_state_and_never_reads_external_targets()
    {
        Directory.CreateDirectory(Path.Combine(Workspace, "state")); await File.WriteAllTextAsync(Path.Combine(Workspace, "state", "secret.md"), "# Secret");
        var result = await Service.AnalyseAsync("# Source\n\n[[state/secret]] [[file:///private.md]]", SourcePath, new(Workspace));
        Assert.Equal(2, result.Issues.Count); Assert.DoesNotContain(result.Issues, issue => issue.Message.Contains("# Secret", StringComparison.Ordinal));
        Assert.False(Directory.Exists(Path.Combine(root, "recovery")));
    }

    [Fact]
    public async Task Workspace_review_resolves_encoded_paths_reference_links_and_named_targets_without_writes()
    {
        const string target = "# Target\n\n<span id=\"Café\" data-doc-anchor></span>";
        var path = Path.Combine(Workspace, "Other guide.md"); await File.WriteAllTextAsync(path, target);
        const string source = "# Source\r\n\r\n[Section][ref] [Named](Other%20guide.md#Caf%C3%A9)\r\n\r\n[ref]: Other%20guide.md#target";
        await File.WriteAllTextAsync(SourcePath, "Original saved bytes");
        var result = await Service.AnalyseAsync(source, SourcePath, new(Workspace));
        Assert.Empty(result.Issues); Assert.Equal(WorkspaceLinkReviewState.Complete, result.WorkspaceLinks);
        Assert.Equal("Original saved bytes", await File.ReadAllTextAsync(SourcePath)); Assert.Equal(target, await File.ReadAllTextAsync(path));
        Assert.False(Directory.Exists(Path.Combine(root, "recovery")));
    }

    [Fact]
    public async Task Workspace_review_prefers_unsaved_target_text_with_case_insensitive_paths()
    {
        var target = Path.Combine(Workspace, "Target.md"); await File.WriteAllTextAsync(target, "# Old heading");
        var source = "# Source\n\n[New](target.md#new-heading) [Old](Target.md#old-heading)";
        var result = await Service.AnalyseAsync(source, SourcePath, new(Workspace), new Dictionary<string, string> { [target] = "# New heading" });
        var issue = Assert.Single(result.Issues); Assert.Contains("linked section is missing", issue.Message);
        Assert.Equal("[Old](Target.md#old-heading)", source.Substring(issue.Start, issue.Length));
        Assert.Equal("# Old heading", await File.ReadAllTextAsync(target));
    }

    [Fact]
    public async Task Workspace_review_uses_current_source_for_explicit_self_links()
    {
        await File.WriteAllTextAsync(SourcePath, "# Old heading");
        var result = await Service.AnalyseAsync("# New heading\n\n[Self](Source.md#new-heading)", SourcePath, new(Workspace));
        Assert.Empty(result.Issues); Assert.Equal("# Old heading", await File.ReadAllTextAsync(SourcePath));
    }

    [Fact]
    public async Task Workspace_review_distinguishes_missing_files_and_sections_at_exact_source_locations()
    {
        await File.WriteAllTextAsync(Path.Combine(Workspace, "Target.md"), "# Target");
        const string source = "# Source\r\n\r\nCafé [Missing](missing.md)\r\n\r\n[Section](Target.md#absent)";
        var result = await Service.AnalyseAsync(source, SourcePath, new(Workspace));
        Assert.Equal(2, result.Issues.Count); Assert.Equal(WorkspaceLinkReviewState.Complete, result.WorkspaceLinks);
        var missing = result.Issues[0]; Assert.Contains("document is missing", missing.Message); Assert.Equal(3, missing.Line); Assert.Equal(6, missing.Column);
        Assert.Equal("[Missing](missing.md)", source.Substring(missing.Start, missing.Length));
        Assert.Contains("section is missing", result.Issues[1].Message); Assert.Equal(5, result.Issues[1].Line);
    }

    [Fact]
    public async Task Workspace_review_marks_locked_or_invalid_text_as_unchecked_instead_of_missing()
    {
        var locked = Path.Combine(Workspace, "Locked.md"); await File.WriteAllTextAsync(locked, "# Locked");
        await File.WriteAllBytesAsync(Path.Combine(Workspace, "Invalid.md"), [0xff, 0xff, 0x00]);
        using var handle = new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var result = await Service.AnalyseAsync("# Source\n\n[Locked](Locked.md) [Invalid](Invalid.md)", SourcePath, new(Workspace));
        Assert.Equal(WorkspaceLinkReviewState.Incomplete, result.WorkspaceLinks); Assert.Equal(2, result.Issues.Count);
        Assert.All(result.Issues, issue => Assert.Contains("could not be read safely", issue.Message));
    }

    [Fact]
    public async Task Workspace_review_refuses_escapes_and_private_state_including_open_overrides()
    {
        Directory.CreateDirectory(Path.Combine(Workspace, "state"));
        var privatePath = Path.Combine(Workspace, "state", "private.md"); await File.WriteAllTextAsync(privatePath, "# Private");
        var externalPath = Path.Combine(root, "outside", "external.md"); await File.WriteAllTextAsync(externalPath, "# External");
        const string source = "# Source\n\n[Private](state/private.md) [Outside](../outside/external.md)";
        var result = await Service.AnalyseAsync(source, SourcePath, new(Workspace), new Dictionary<string, string> { [privatePath] = "# Draft", [externalPath] = "# Draft" });
        Assert.Equal(WorkspaceLinkReviewState.Incomplete, result.WorkspaceLinks); Assert.Equal(2, result.Issues.Count);
        Assert.Contains("private application state", result.Issues[0].Message); Assert.Contains("within the workspace", result.Issues[1].Message);
        Assert.Equal("# Private", await File.ReadAllTextAsync(privatePath)); Assert.Equal("# External", await File.ReadAllTextAsync(externalPath));
    }

    [Theory]
    [InlineData("%2e%2e/outside/file.md")]
    [InlineData("%252e%252e/outside/file.md")]
    [InlineData("/absolute.md")]
    [InlineData("Target.md?query=1")]
    public async Task Workspace_review_reports_unsupported_local_paths_without_expanding_its_scope(string reference)
    {
        var result = await Service.AnalyseAsync("# Source\n\n[Local](" + reference + ")", SourcePath, new(Workspace));
        Assert.Equal(WorkspaceLinkReviewState.Incomplete, result.WorkspaceLinks);
        Assert.Contains("could not be checked within the workspace", Assert.Single(result.Issues).Message);
    }

    [Fact]
    public async Task Workspace_review_does_not_fetch_external_links_or_image_and_asset_references()
    {
        const string source = "# Source\n\n[Web](https://example.invalid/a.md#b) [Mail](mailto:example@example.invalid) [PDF](missing.pdf) ![Image](missing.png)";
        var result = await Service.AnalyseAsync(source, SourcePath, new(Workspace));
        Assert.Empty(result.Issues); Assert.Equal(WorkspaceLinkReviewState.Complete, result.WorkspaceLinks);
    }

    [Fact]
    public async Task Workspace_review_limits_distinct_targets_and_discloses_unchecked_links()
    {
        var source = "# Source\n\n" + string.Join(' ', Enumerable.Range(0, WorkspaceDocumentReviewService.MaximumTargets + 1).Select(i => $"[Guide](guide-{i}.md)"));
        var result = await Service.AnalyseAsync(source, SourcePath, new(Workspace));
        Assert.True(result.Limited); Assert.Equal(WorkspaceLinkReviewState.Incomplete, result.WorkspaceLinks);
        Assert.Equal(WorkspaceDocumentReviewService.MaximumTargets + 1, result.Issues.Count);
        Assert.Contains("100-document limit", result.Issues[^1].Message);
    }

    [Fact]
    public async Task Workspace_review_counts_oversized_drafts_towards_aggregate_budget_and_stops()
    {
        var large = new string('x', 5 * 1024 * 1024); var overrides = new Dictionary<string, string>();
        for (var index = 0; index < 5; index++) overrides[Path.Combine(Workspace, $"large-{index}.md")] = large;
        var source = "# Source\n\n" + string.Join(' ', Enumerable.Range(0, 5).Select(i => $"[Large](large-{i}.md)"));
        var result = await Service.AnalyseAsync(source, SourcePath, new(Workspace), overrides);
        Assert.True(result.Limited); Assert.Equal(WorkspaceLinkReviewState.Incomplete, result.WorkspaceLinks); Assert.Equal(4, result.Issues.Count);
        Assert.Contains("16-million-character limit", result.Issues[^1].Message);
    }

    [Fact]
    public async Task Workspace_review_honours_cancellation_before_reading()
    {
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => Service.AnalyseAsync("# Source", SourcePath, new(Workspace), cancellationToken: cancelled.Token));
    }

    [Fact]
    public async Task Unavailable_workspace_still_allows_current_buffer_checks_without_claiming_missing_links()
    {
        Directory.Delete(Workspace);
        var result = await Service.AnalysePathAsync("# Source\n\nTODO [Target](Target.md)", SourcePath, Workspace);
        Assert.Equal(WorkspaceLinkReviewState.Incomplete, result.WorkspaceLinks); Assert.Equal(2, result.Issues.Count);
        Assert.Contains(result.Issues, issue => issue.Rule == "TODO/FIXME");
        Assert.Contains(result.Issues, issue => issue.Message == "The workspace is unavailable or unsafe to inspect.");
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
