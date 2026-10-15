namespace LensDocsStudio.Core.Workspace;

public sealed record WorkspaceFileIdentity(string IconKind, string Description)
{
    public static WorkspaceFileIdentity FromPath(string path, bool directory = false) => directory ? new("Folder", "Folder") : Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".md" or ".markdown" => new("Markdown", "Markdown document"),
        ".mmd" or ".mermaid" => new("Mermaid", "Mermaid diagram"),
        ".txt" => new("PlainText", "Plain-text document"),
        ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" => new("Image", "Image file"),
        _ => new("Document", "File")
    };
}
