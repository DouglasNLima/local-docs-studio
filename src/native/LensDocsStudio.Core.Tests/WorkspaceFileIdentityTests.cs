using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Core.Tests;

public sealed class WorkspaceFileIdentityTests
{
    [Theory]
    [InlineData("guide.md", "Markdown", "Markdown document")]
    [InlineData("guide.MD", "Markdown", "Markdown document")]
    [InlineData("guide.markdown", "Markdown", "Markdown document")]
    [InlineData("guide.MARKDOWN", "Markdown", "Markdown document")]
    [InlineData("flow.mmd", "Mermaid", "Mermaid diagram")]
    [InlineData("flow.MMD", "Mermaid", "Mermaid diagram")]
    [InlineData("flow.mermaid", "Mermaid", "Mermaid diagram")]
    [InlineData("flow.MERMAID", "Mermaid", "Mermaid diagram")]
    [InlineData("notes.TXT", "PlainText", "Plain-text document")]
    [InlineData("asset.PNG", "Image", "Image file")]
    [InlineData("unknown.bin", "Document", "File")]
    [InlineData("no-extension", "Document", "File")]
    public void Extensions_select_distinct_accessible_file_identities(string path, string icon, string description)
    {
        Assert.Equal(new(icon, description), WorkspaceFileIdentity.FromPath(path));
        Assert.Equal(new("Folder", "Folder"), WorkspaceFileIdentity.FromPath(path, true));
    }
}
