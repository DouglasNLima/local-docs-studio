using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Core.Tests;

public sealed class WorkspaceAssetTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "lens-assets-audit-" + Guid.NewGuid().ToString("N"));
    private WorkspaceAssetService Service => new(new DocumentStore(Path.Combine(root, "state")));
    public WorkspaceAssetTests() { Directory.CreateDirectory(Path.Combine(root, "assets")); Directory.CreateDirectory(Path.Combine(root, "docs")); }

    [Fact] public async Task Asset_audit_resolves_relative_markdown_references_and_identifies_unreferenced_files()
    {
        await File.WriteAllBytesAsync(Path.Combine(root, "assets", "used image.png"), [1, 2]);
        await File.WriteAllBytesAsync(Path.Combine(root, "assets", "unused.png"), [3]);
        await File.WriteAllTextAsync(Path.Combine(root, "docs", "guide.md"), "![Used][image]\n\n[image]: ../assets/used%20image.png\n\n![Missing](../assets/missing.png)\n\n![Remote](https://example.invalid/image.png)");
        var audit = await Service.AuditAsync(new(root));
        Assert.True(audit.UsageComplete); Assert.Empty(audit.Warnings); Assert.Equal(3, audit.Assets.Count);
        Assert.Single(audit.Assets.Single(a => a.Path.EndsWith("used image.png", StringComparison.Ordinal)).Uses);
        Assert.Empty(audit.Assets.Single(a => a.Path.EndsWith("unused.png", StringComparison.Ordinal)).Uses);
        Assert.False(audit.Assets.Single(a => a.Path.EndsWith("missing.png", StringComparison.Ordinal)).Exists);
    }

    [Fact] public async Task Asset_audit_uses_open_drafts_instead_of_older_saved_text()
    {
        var path = Path.Combine(root, "guide.md");
        await File.WriteAllTextAsync(path, "Saved source without image references");
        await File.WriteAllBytesAsync(Path.Combine(root, "assets", "draft.png"), [1]);
        var audit = await Service.AuditAsync(new(root), [new(path, "![Draft](assets/draft.png)", true)]);
        var use = Assert.Single(Assert.Single(audit.Assets).Uses);
        Assert.True(use.Unsaved); Assert.Equal(path, use.DocumentPath);
    }

    [Fact] public async Task HTML_image_references_are_counted_but_code_examples_are_not()
    {
        await File.WriteAllTextAsync(Path.Combine(root, "guide.md"), "<img src=\"assets/actual.png\">\n\n```html\n<img src=\"assets/example.png\">\n```\n");
        var audit = await Service.AuditAsync(new(root));
        var asset = Assert.Single(audit.Assets);
        Assert.EndsWith("actual.png", asset.Path); Assert.False(Assert.Single(asset.Uses).Markdown);
    }

    [Fact] public async Task Unreadable_documents_and_opaque_metadata_prevent_an_unqualified_unused_assessment()
    {
        await File.WriteAllBytesAsync(Path.Combine(root, "invalid.md"), [255, 254]);
        await File.WriteAllTextAsync(Path.Combine(root, "metadata.md"), "---\nimage: assets/cover.png\n---\n\n# Title");
        await File.WriteAllTextAsync(Path.Combine(root, "styled.md"), "<div style=\"background-image:url(assets/background.png)\">Example</div>");
        var audit = await Service.AuditAsync(new(root));
        Assert.False(audit.UsageComplete); Assert.Equal(3, audit.Warnings.Count);
    }

    [Fact] public async Task Asset_audit_rejects_outside_drafts_and_reports_traversing_references()
    {
        await Assert.ThrowsAsync<IOException>(() => Service.AuditAsync(new(root), [new(Path.Combine(root, "..", "outside.md"), "outside")]));
        await File.WriteAllTextAsync(Path.Combine(root, "guide.md"), "![Outside](../outside.png)");
        var audit = await Service.AuditAsync(new(root));
        Assert.False(audit.UsageComplete); Assert.Empty(audit.Assets); Assert.Single(audit.Warnings);
    }

    [Fact] public async Task Native_recovery_folders_inside_the_selected_workspace_are_excluded_from_asset_management()
    {
        var state = Path.Combine(root, "state"); Directory.CreateDirectory(state);
        await File.WriteAllBytesAsync(Path.Combine(state, "recovery.png"), [1]);
        await File.WriteAllBytesAsync(Path.Combine(root, "assets", "workspace.png"), [2]);
        var audit = await new WorkspaceAssetService(new(Path.Combine(state, "writes")), state).AuditAsync(new(root));
        Assert.EndsWith("workspace.png", Assert.Single(audit.Assets).Path);
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
