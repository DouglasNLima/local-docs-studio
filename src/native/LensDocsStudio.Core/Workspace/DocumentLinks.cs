namespace LensDocsStudio.Core.Workspace;

public static class DocumentLinks
{
    public static string Create(WorkspacePath workspace, string sourcePath, string targetPath, string? label = null, string? anchor = null)
    {
        var source = workspace.Resolve(Path.GetRelativePath(workspace.Root, Path.GetFullPath(sourcePath)));
        var target = workspace.Resolve(Path.GetRelativePath(workspace.Root, Path.GetFullPath(targetPath)));
        if (!WorkspaceService.IsDocument(target) || !File.Exists(target)) throw new IOException("Choose an existing workspace document.");
        var relative = Path.GetRelativePath(Path.GetDirectoryName(source)!, target);
        var reference = string.Join('/', relative.Split(Path.DirectorySeparatorChar).Select(Uri.EscapeDataString));
        if (!string.IsNullOrEmpty(anchor)) reference += "#" + Uri.EscapeDataString(anchor);
        label = string.IsNullOrWhiteSpace(label) ? Path.GetFileNameWithoutExtension(target) : label.Trim();
        label = label.Replace("\\", "\\\\").Replace("[", "\\[").Replace("]", "\\]").Replace("\r", " ").Replace("\n", " ");
        return $"[{label}]({reference})";
    }
}
