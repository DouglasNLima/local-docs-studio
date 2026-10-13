namespace LensDocsStudio.Core.Workspace;

/// <summary>Retains an open document's selected workspace when the active folder changes.</summary>
public static class DocumentWorkspace
{
    public static string Resolve(string documentPath, string? activeRoot, string? retainedRoot = null)
    {
        if (!Path.IsPathFullyQualified(documentPath)) throw new ArgumentException("Choose an absolute document path.", nameof(documentPath));
        var path = Path.GetFullPath(documentPath);
        return ContainingRoot(activeRoot, path) ?? ContainingRoot(retainedRoot, path) ?? Path.GetDirectoryName(path)!;
    }

    private static string? ContainingRoot(string? root, string path)
    {
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root)) return null;
        try
        {
            var candidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            return WorkspacePath.Contains(candidate, path) ? candidate : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // An invalid optional recovery context must not discard the document's text.
            return null;
        }
    }
}
