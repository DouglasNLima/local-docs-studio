namespace LensDocsStudio.Core.Workspace;

/// <summary>Paths are scoped to an explicitly selected folder; links never grant extra access.</summary>
public sealed class WorkspacePath
{
    public string Root { get; }
    public WorkspacePath(string root)
    {
        Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (!Directory.Exists(Root)) throw new DirectoryNotFoundException("The workspace folder is unavailable.");
        RejectReparsePoints(Root);
    }

    public string Resolve(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) ||
            relativePath.Contains(':') || relativePath.Contains('\0'))
            throw new IOException("Choose a relative path inside the workspace.");
        foreach (var component in relativePath.Replace('/', '\\').Split('\\'))
            if (component is not ("." or "..") && (component.EndsWith('.') || component.EndsWith(' ')))
                throw new IOException("The path contains an unsupported Windows filename.");
        var path = Path.GetFullPath(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!Contains(Root, path))
            throw new IOException("The path leaves the workspace.");
        foreach (var part in Path.GetRelativePath(Root, path).Split(Path.DirectorySeparatorChar))
        {
            if (part.EndsWith(' ') || part.EndsWith('.') || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                System.Text.RegularExpressions.Regex.IsMatch(part, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                throw new IOException("The path contains an unsupported Windows filename.");
        }
        RejectReparsePoints(path);
        return path;
    }

    public string ResolveLink(string sourcePath, string reference)
    {
        var pathPart = reference.Split('#', 2)[0];
        if (pathPart.Contains('?') || pathPart.StartsWith('/') || pathPart.StartsWith('\\'))
            throw new IOException("This is not a workspace document link.");
        var decoded = Uri.UnescapeDataString(pathPart);
        if (System.Text.RegularExpressions.Regex.IsMatch(decoded, "%[0-9a-fA-F]{2}") && decoded != pathPart)
            throw new IOException("Ambiguous encoded paths are not supported.");
        if (Uri.TryCreate(decoded, UriKind.Absolute, out _))
            throw new IOException("External links require an explicit external action.");
        var source = Resolve(Path.GetRelativePath(Root, Path.GetFullPath(sourcePath)));
        return Resolve(Path.GetRelativePath(Root, Path.Combine(Path.GetDirectoryName(source)!, decoded)));
    }

    public static void RejectReparsePoints(string path)
    {
        for (var current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Symbolic links and junctions are not followed.");
        }
    }

    public static bool Contains(string root, string path)
    {
        root = Path.GetFullPath(root);
        var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
