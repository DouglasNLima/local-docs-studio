namespace LensDocsStudio.Core.Workspace;

public sealed record WorkspaceEntry(string Name, string Path, bool IsDirectory, IReadOnlyList<WorkspaceEntry> Children);
public sealed record WorkspaceListing(IReadOnlyList<WorkspaceEntry> Entries, IReadOnlyList<string> Warnings);

public sealed class WorkspaceService
{
    public static bool IsDocument(string path) => Path.GetExtension(path).ToLowerInvariant() is ".md" or ".markdown" or ".mmd" or ".mermaid" or ".txt";
    public static bool IsRasterImage(string path) => Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp";

    public Task<WorkspaceListing> EnumerateAsync(WorkspacePath workspace, CancellationToken cancellationToken = default, bool includeImages = false, string? excludedDirectory = null) =>
        Task.Run(() =>
        {
            var warnings = new List<string>();
            var count = 0;
            IReadOnlyList<WorkspaceEntry> Walk(string directory, int depth)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (depth > 32 || count > 20000) { warnings.Add("Workspace enumeration reached the safety limit."); return []; }
                var entries = new List<WorkspaceEntry>();
                try
                {
                    foreach (var entry in Directory.EnumerateFileSystemEntries(directory).Order(StringComparer.OrdinalIgnoreCase))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (++count > 20000) { warnings.Add("Workspace enumeration reached 20,000 entries."); break; }
                        var name = Path.GetFileName(entry);
                        if (name is ".git" or "node_modules" or "bin" or "obj" || (File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) continue;
                        var folder = Directory.Exists(entry);
                        if (folder && !entry.Equals(excludedDirectory, StringComparison.OrdinalIgnoreCase)) entries.Add(new(name, entry, true, Walk(entry, depth + 1)));
                        else if (!folder && (IsDocument(entry) || includeImages && IsRasterImage(entry))) entries.Add(new(name, entry, false, []));
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { warnings.Add("An unreadable folder was skipped: " + Path.GetRelativePath(workspace.Root, directory)); }
                return entries.OrderByDescending(e => e.IsDirectory).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToArray();
            }
            return new WorkspaceListing(Walk(workspace.Root, 0), warnings);
        }, cancellationToken);
}
