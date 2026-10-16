using System.Text;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Exports;

public sealed class DocsSiteExportService
{
    public async Task<DocsSiteResult> ExportAsync(string root, IReadOnlyDictionary<string, string> editedDocuments, string stateDirectory,
        DocsSiteOptions options, IProgress<string>? progress = null, CancellationToken cancellationToken = default, ArtifactBundle? artifactBundle = null, string diagramTheme = "Auto")
    {
        var workspace = new WorkspacePath(root);
        stateDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(stateDirectory));
        if (WorkspacePath.ContainsOrEquals(stateDirectory, workspace.Root))
            throw new IOException("Choose a document workspace outside the native application's private state folder.");
        var listing = await new WorkspaceService().EnumerateAsync(workspace, cancellationToken, excludedDirectory: Path.GetFullPath(stateDirectory));
        if (listing.Warnings.Count != 0) throw new IOException("The workspace could not be completely read. " + string.Join(" ", listing.Warnings));
        var files = Files(listing.Entries).ToArray();
        if (files.Length is 0 or > DocsSiteExporter.MaximumPages) throw new IOException("A Docs Site supports between 1 and 1,000 documents.");
        var store = new DocumentStore(Path.Combine(stateDirectory, "export-recovery"));
        var exporter = new DocsSiteExporter(); var pages = new List<DocsSitePage>();
        var preparation = new ExportPreparationService(new MarkdownService(), new DiagramService(), new NativeSvgService());
        long sourceBytes = 0; long renderedBytes = 0;
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(workspace.Root, file).Replace('\\', '/'); workspace.Resolve(relative);
            var source = editedDocuments.TryGetValue(file, out var edited) ? edited : (await store.ReadAsync(file, cancellationToken)).Text;
            var bytes = Encoding.UTF8.GetByteCount(source); sourceBytes += bytes;
            if (bytes > DocumentStore.MaximumDocumentBytes || sourceBytes > 64 * 1024 * 1024) throw new IOException("Docs Site source exceeds the 32 MB per-document or 64 MB workspace limit.");
            progress?.Report($"Preparing Docs Site page {pages.Count + 1} of {files.Length}: {relative}");
            var page = await StaWorker.RunAsync(() => exporter.PreparePage(relative,
                preparation.Prepare(source, file, workspace.Root, options.Theme == "dark", cancellationToken, 64 * 1024 * 1024, diagramTheme: diagramTheme), artifactBundle, cancellationToken), cancellationToken);
            renderedBytes += Encoding.UTF8.GetByteCount(page.Html) + Encoding.UTF8.GetByteCount(page.SearchText);
            if (renderedBytes > DocsSiteExporter.MaximumOutputBytes / 3) throw new IOException("Rendered Docs Site content exceeds its size budget.");
            pages.Add(page);
        }
        return await Task.Run(() => exporter.Export(pages, options, cancellationToken, artifactBundle), cancellationToken);
    }
    private static IEnumerable<string> Files(IEnumerable<WorkspaceEntry> entries)
    {
        foreach (var entry in entries)
            if (entry.IsDirectory) { foreach (var path in Files(entry.Children)) yield return path; }
            else yield return entry.Path;
    }
}
