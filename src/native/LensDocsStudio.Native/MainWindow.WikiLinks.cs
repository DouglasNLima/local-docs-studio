using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private WikiLinkIndex? WikiCompletionCatalogue()
    {
        if (model.WorkspaceRoot is not { } root || model.ActiveDocument?.WorkspaceRoot != root) return null;
        IEnumerable<string> Paths(IEnumerable<ViewModels.WorkspaceEntryViewModel> entries)
        {
            foreach (var entry in entries)
                if (entry.IsDirectory) { foreach (var path in Paths(entry.Children)) yield return path; }
                else if (WorkspaceService.IsDocument(entry.Path)) yield return Path.GetRelativePath(root, entry.Path).Replace('\\', '/');
        }
        return new(Paths(model.Entries));
    }
    private async void NavigateWikiLink(Guid documentId, string source, string target)
    {
        string? reference = null;
        await model.RunAsync(async () =>
        {
            if (model.ActiveDocument is not { FilePath: not null, WorkspaceRoot: not null } tab || tab.Id != documentId || tab.EditorDocument.Text != source)
                throw new IOException("Refresh this document preview before following its wikilink.");
            var workspace = new WorkspacePath(tab.WorkspaceRoot);
            var listing = await new WorkspaceService().EnumerateAsync(workspace, model.OperationToken, excludedDirectory: model.StateDirectory);
            var paths = WikiDocumentPaths(listing.Entries).Select(path => Path.GetRelativePath(workspace.Root, path).Replace('\\', '/')).ToArray();
            var result = new WikiLinkIndex(paths).Resolve(target, Path.GetRelativePath(workspace.Root, tab.FilePath).Replace('\\', '/'));
            if (!result.Found)
            {
                model.Status = result.Error + (result.Candidates.Count > 0 ? " Matches: " + string.Join(", ", result.Candidates.Take(5)) : "")
                    + (listing.Warnings.Count > 0 ? " Some workspace folders could not be checked." : "");
                return;
            }
            var resolved = workspace.Resolve(result.Path!);
            if (!File.Exists(resolved)) throw new IOException("The linked workspace document is no longer available.");
            if (model.ActiveDocument != tab || tab.EditorDocument.Text != source) return;
            reference = MarkdownLinks.RelativeReference(tab.FilePath, resolved) + (result.Fragment.Length > 0 ? "#" + Uri.EscapeDataString(result.Fragment) : "");
        });
        if (reference is not null && model.ActiveDocument?.Id == documentId && model.ActiveDocument.EditorDocument.Text == source) Navigate(reference);
    }
    private static IEnumerable<string> WikiDocumentPaths(IEnumerable<WorkspaceEntry> entries)
    {
        foreach (var entry in entries)
        {
            if (entry.IsDirectory) { foreach (var path in WikiDocumentPaths(entry.Children)) yield return path; }
            else if (WorkspaceService.IsDocument(entry.Path)) yield return entry.Path;
        }
    }
}
