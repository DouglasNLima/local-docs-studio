using System.Text.Json;
using System.Text.Json.Nodes;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Core.Tests;

public sealed class DocumentWorkspaceTests
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "lens-document-context-" + Guid.NewGuid().ToString("N"));
    private string First => Path.Combine(root, "first");
    private string Document => Path.Combine(First, "docs", "Café.md");

    [Fact]
    public void Another_active_workspace_keeps_the_documents_retained_root()
        => Assert.Equal(First, DocumentWorkspace.Resolve(Document, Path.Combine(root, "second"), First));

    [Fact]
    public void An_explicitly_selected_containing_folder_takes_precedence_over_a_narrower_root()
        => Assert.Equal(First, DocumentWorkspace.Resolve(Document, First, Path.GetDirectoryName(Document)));

    [Fact]
    public void Missing_legacy_context_falls_back_to_the_document_directory()
        => Assert.Equal(Path.GetDirectoryName(Document), DocumentWorkspace.Resolve(Document, Path.Combine(root, "second")));

    [Fact]
    public void A_destination_outside_both_workspaces_does_not_reuse_either_root()
    {
        var destination = Path.Combine(root, "third", "note.md");
        Assert.Equal(Path.GetDirectoryName(destination), DocumentWorkspace.Resolve(destination, First, Path.Combine(root, "second")));
    }

    [Fact]
    public void A_common_directory_name_prefix_does_not_grant_workspace_access()
    {
        var destination = Path.Combine(root, "first-other", "note.md");
        Assert.Equal(Path.GetDirectoryName(destination), DocumentWorkspace.Resolve(destination, First, First));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relative-root")]
    [InlineData("C:\\invalid\0root")]
    public void Invalid_optional_roots_do_not_prevent_recovery_of_document_text(string? candidate)
        => Assert.Equal(Path.GetDirectoryName(Document), DocumentWorkspace.Resolve(Document, candidate, candidate));

    [Fact]
    public void Relative_document_paths_cannot_acquire_an_implicit_working_directory()
        => Assert.Throws<ArgumentException>(() => DocumentWorkspace.Resolve("docs/note.md", First));

    [Fact]
    public void Workspace_context_round_trips_with_the_existing_draft_and_saved_baseline()
    {
        var document = new RecoveredDocument(Guid.NewGuid(), Document, "Draft Ω", "saved-fingerprint", true, 4, 20,
            SavedText: "Saved Café", WorkspaceRoot: First);
        var state = new SessionState(1, Path.Combine(root, "second"), document.Id, [document]);
        var restored = JsonSerializer.Deserialize<SessionState>(JsonSerializer.Serialize(state))!;
        Assert.Equal(document, Assert.Single(restored.Documents));
        Assert.Equal(First, DocumentWorkspace.Resolve(document.Path!, restored.Workspace, restored.Documents[0].WorkspaceRoot));
    }

    [Fact]
    public void Earlier_schema_one_documents_without_a_context_keep_their_text_and_identity()
    {
        var document = new RecoveredDocument(Guid.NewGuid(), Document, "Draft Ω", "saved-fingerprint", false, 2, 10, SavedText: "Saved Café");
        var node = JsonSerializer.SerializeToNode(new SessionState(1, null, document.Id, [document]))!;
        node["Documents"]![0]!.AsObject().Remove("WorkspaceRoot");
        var restored = node.Deserialize<SessionState>()!;
        Assert.Equal(document, Assert.Single(restored.Documents));
        Assert.Null(restored.Documents[0].WorkspaceRoot);
        Assert.Equal(Path.GetDirectoryName(Document), DocumentWorkspace.Resolve(Document, restored.Workspace, restored.Documents[0].WorkspaceRoot));
    }
}
