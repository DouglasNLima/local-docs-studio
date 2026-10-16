using AngleSharp.Html.Parser;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LensDocsStudio.Core.Workspace;

public sealed record WorkspaceDocumentSource(string Path, string Text, bool Unsaved = false);
public sealed record WorkspaceAssetUse(string DocumentPath, string Reference, bool Markdown, bool Unsaved);
public sealed record WorkspaceAsset(string Path, string RelativePath, long Bytes, bool Exists, IReadOnlyList<WorkspaceAssetUse> Uses)
{
    public string Summary => RelativePath + (Exists ? " — " + Uses.Count + " reference" + (Uses.Count == 1 ? "" : "s") : " — missing image");
}
public sealed record WorkspaceAssetAudit(IReadOnlyList<WorkspaceAsset> Assets, IReadOnlyList<string> Warnings, bool UsageComplete);

/// <summary>Audits local raster files against documentation and captured open drafts, without fetching remote references.</summary>
public sealed class WorkspaceAssetService(DocumentStore documents, string? excludedDirectory = null)
{
    public async Task<WorkspaceAssetAudit> AuditAsync(WorkspacePath workspace, IReadOnlyList<WorkspaceDocumentSource>? openDocuments = null,
        CancellationToken cancellationToken = default)
    {
        var listing = await new WorkspaceService().EnumerateAsync(workspace, cancellationToken, includeImages: true, excludedDirectory: excludedDirectory);
        var warnings = listing.Warnings.ToList();
        var complete = warnings.Count == 0;
        var files = Flatten(listing.Entries).ToArray();
        var assetPaths = files.Where(e => WorkspaceService.IsRasterImage(e.Path)).Select(e => e.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var uses = new Dictionary<string, List<WorkspaceAssetUse>>(StringComparer.OrdinalIgnoreCase);
        var overrides = new Dictionary<string, WorkspaceDocumentSource>(StringComparer.OrdinalIgnoreCase);
        foreach (var document in openDocuments ?? [])
        {
            var path = workspace.Resolve(Path.GetRelativePath(workspace.Root, document.Path));
            if (excludedDirectory is not null && WorkspacePath.Contains(excludedDirectory, path)) continue;
            if (!overrides.TryAdd(path, document)) throw new IOException("More than one open draft has the same audit path.");
        }
        long characters = 0;
        foreach (var path in files.Where(e => WorkspaceService.IsDocument(e.Path)).Select(e => e.Path).Concat(overrides.Keys).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var source = overrides.TryGetValue(path, out var captured) ? captured : new WorkspaceDocumentSource(path, (await documents.ReadAsync(path, cancellationToken)).Text);
                characters += source.Text.Length;
                if (characters > 64 * 1024 * 1024) throw new IOException("Asset usage checking reached 64 million source characters.");
                var references = await Task.Run(() => ReadReferences(source.Text), cancellationToken);
                if (references.Opaque)
                {
                    complete = false;
                    warnings.Add("Image usage needs manual review in " + Path.GetRelativePath(workspace.Root, path) + " because it contains front matter, CSS or responsive image references.");
                }
                foreach (var (reference, image, markdown) in references.Links)
                {
                    if (reference.StartsWith('#') || Uri.TryCreate(reference, UriKind.Absolute, out _)) continue;
                    string target;
                    try { target = workspace.ResolveLink(path, reference); }
                    catch (IOException) { complete = false; warnings.Add("An image or link outside the workspace could not be checked in " + Path.GetRelativePath(workspace.Root, path)); continue; }
                    if (excludedDirectory is not null && WorkspacePath.Contains(excludedDirectory, target)) continue;
                    if (!image && !WorkspaceService.IsRasterImage(target)) continue;
                    assetPaths.Add(target);
                    if (!uses.TryGetValue(target, out var referencesToTarget)) uses[target] = referencesToTarget = [];
                    referencesToTarget.Add(new(path, reference, markdown, source.Unsaved));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.DecoderFallbackException)
            { complete = false; warnings.Add("Asset references could not be checked in " + Path.GetRelativePath(workspace.Root, path) + ": " + ex.Message); }
            if (characters > 64 * 1024 * 1024) break;
        }
        var assets = new List<WorkspaceAsset>();
        foreach (var path in assetPaths.Order(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                workspace.Resolve(Path.GetRelativePath(workspace.Root, path));
                var info = new FileInfo(path);
                assets.Add(new(path, Path.GetRelativePath(workspace.Root, path), info.Exists ? info.Length : 0, info.Exists,
                    uses.TryGetValue(path, out var references) ? references : []));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { complete = false; warnings.Add("An asset could not be inspected: " + Path.GetRelativePath(workspace.Root, path)); }
        }
        return new(assets, warnings.Distinct().ToArray(), complete);
    }

    private static (IReadOnlyList<(string Reference, bool Image, bool Markdown)> Links, bool Opaque) ReadReferences(string source)
    {
        var links = MarkdownLinks.Read(source).Select(l => (l.Reference, Image: l.IsImage, Markdown: true)).ToList();
        var syntax = new MarkdownService().Parse(source).Syntax;
        var fragments = syntax.Descendants<HtmlBlock>().Select(h => h.Lines.ToString()).Concat(syntax.Descendants<HtmlInline>().Select(h => h.Tag));
        var opaque = source.StartsWith("---\n", StringComparison.Ordinal) || source.StartsWith("---\r\n", StringComparison.Ordinal);
        foreach (var fragment in fragments)
        {
            var html = new HtmlParser().ParseDocument(fragment);
            if (html.All.Count() > 100_000) throw new IOException("Embedded HTML exceeds the asset-audit complexity limit.");
            foreach (var element in html.All)
            {
                if ((element.GetAttribute("style") ?? "").Contains("url", StringComparison.OrdinalIgnoreCase) || element.LocalName == "style") opaque = true;
                foreach (var attribute in element.Attributes.Where(a => a.Name is "src" or "href" or "poster" or "srcset"))
                {
                    if (attribute.Name == "srcset") { opaque = true; continue; }
                    links.Add((attribute.Value, element.LocalName is "img" or "source" || attribute.Name == "poster", false));
                }
            }
        }
        return (links, opaque);
    }
    private static IEnumerable<WorkspaceEntry> Flatten(IEnumerable<WorkspaceEntry> entries) => entries.SelectMany(e => e.IsDirectory ? Flatten(e.Children) : [e]);
}
