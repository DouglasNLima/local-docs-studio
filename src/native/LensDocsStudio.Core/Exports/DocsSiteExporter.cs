using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Imports;

namespace LensDocsStudio.Core.Exports;

public sealed record DocsSiteOptions(string Title, string Description = "", string Theme = "system");
public sealed record DocsSiteHeading(string Id, string Text, int Level);
public sealed record DocsSitePage(string Id, string Path, string Title, DocumentMetadata Metadata, string Html,
    IReadOnlyList<DocsSiteHeading> Headings, string SearchText, int DiagramTotal, bool GeneratedHome = false)
{
    public string ArtifactKind { get; init; } = "";
    public string EvidenceLevel { get; init; } = "";
}
public sealed record DocsSiteResult(byte[] Bytes, int SourcePages, int Pages, int Diagrams, IReadOnlyList<string> Warnings);

/// <summary>Creates a portable static export. Its scripts are output assets, never a desktop runtime.</summary>
public sealed class DocsSiteExporter
{
    public const int MaximumPages = 1000;
    public const long MaximumOutputBytes = 256 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public DocsSitePage PreparePage(string path, ExportContent content, ArtifactBundle? artifactBundle = null)
    {
        path = ValidatePath(path);
        var html = new HtmlParser().ParseDocument(new HtmlExporter().RenderFragment(content, preserveWikiLinks: true));
        var headingElements = html.Body!.QuerySelectorAll("h1,h2,h3,h4,h5,h6");
        var usedIds = new HashSet<string>(html.Body.QuerySelectorAll("[id]").Except(headingElements).Select(e => e.Id!), StringComparer.Ordinal);
        var headings = headingElements.Select(h =>
        {
            var baseId = string.IsNullOrEmpty(h.Id) ? "section" : h.Id; var id = baseId; var suffix = 2;
            while (!usedIds.Add(id)) id = baseId + "-" + suffix++;
            h.Id = id;
            var headingCopy = (IElement)h.Clone(true);
            foreach (var image in headingCopy.QuerySelectorAll("img")) image.ReplaceWith(html.CreateTextNode(image.GetAttribute("alt") ?? ""));
            return new DocsSiteHeading(id, headingCopy.TextContent.Trim(), h.LocalName[1] - '0');
        }).ToArray();
        var metadata = content.Document.Metadata; var artifact = artifactBundle?.Find(path);
        var fallbackTitle = ArtifactBundleManifest.SafeExportText(artifact?.Title ?? "");
        var title = metadata.Title.Length > 0 ? metadata.Title : fallbackTitle.Length > 0 ? fallbackTitle : headings.FirstOrDefault()?.Text ?? System.IO.Path.GetFileNameWithoutExtension(path);
        metadata = metadata with { Order = metadata.Order ?? artifact?.Order,
            NavigationGroup = metadata.NavigationGroup.Length > 0 ? metadata.NavigationGroup : artifact?.Group ?? "" };
        var id = "page-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(path.ToUpperInvariant())))[..16];
        return new(id, path, title, metadata, html.Body.InnerHtml, headings, html.Body.TextContent,
            content.Diagrams.Count) { ArtifactKind = ArtifactBundleManifest.SafeExportText(artifact?.Kind ?? ""), EvidenceLevel = artifact?.EvidenceLevel ?? "" };
    }

    public DocsSiteResult Export(IReadOnlyList<DocsSitePage> sourcePages, DocsSiteOptions options, CancellationToken cancellationToken = default, ArtifactBundle? artifactBundle = null)
    {
        if (sourcePages.Count is 0 or > MaximumPages) throw new IOException("Choose between 1 and 1,000 documents for a Docs Site.");
        if (options.Title.Length is 0 or > 512 || options.Description.Length > 8192 || options.Theme is not ("system" or "light" or "dark"))
            throw new ArgumentException("The Docs Site title, description or theme is invalid.", nameof(options));
        var byPath = new Dictionary<string, DocsSitePage>(StringComparer.OrdinalIgnoreCase);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        long bodyBytes = 0;
        foreach (var page in sourcePages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!byPath.TryAdd(ValidatePath(page.Path), page) || !ids.Add(page.Id)) throw new IOException("Docs Site documents contain duplicate paths or identifiers.");
            bodyBytes += Encoding.UTF8.GetByteCount(page.Html) + Encoding.UTF8.GetByteCount(page.SearchText);
            if (bodyBytes > MaximumOutputBytes / 3) throw new IOException("Rendered Docs Site content exceeds its size budget.");
        }
        var warnings = new List<string>();
        var wikiIndex = new WikiLinkIndex(sourcePages.Select(page => page.Path));
        var pages = sourcePages.OrderBy(p => p.Metadata.Order ?? double.PositiveInfinity)
            .ThenBy(p => p.Metadata.NavigationGroup, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Path, StringComparer.OrdinalIgnoreCase).Select(RewriteLinks).ToList();
        var home = pages.OrderBy(p => p.Path.Count(c => c == '/')).FirstOrDefault(p => System.IO.Path.GetFileName(p.Path).ToLowerInvariant() is "readme.md" or "readme.markdown" or "index.md" or "index.markdown");
        if (home is null)
        {
            var cards = string.Join("", pages.Select(p => "<li><a href=\"#" + p.Id + "\">" + H(p.Title) + "</a> — " + H(p.Metadata.Description.Length > 0 ? p.Metadata.Description : p.Path) + "</li>"));
            home = new("home", "Home", options.Title, new(), "<h1 id=\"overview\">" + H(options.Title) + "</h1><p>" + H(options.Description) + "</p><ul>" + cards + "</ul>",
                [new("overview", options.Title, 1)], options.Title + " " + options.Description, 0, true);
            pages.Insert(0, home);
        }
        var records = pages.Select(p => new
        {
            p.Id, p.Path, p.Title, p.Html, p.Metadata.Description, p.Metadata.Order, p.Metadata.Tags, p.Metadata.Draft,
            NavGroup = p.Metadata.NavigationGroup, p.ArtifactKind, p.EvidenceLevel, p.Headings,
            p.DiagramTotal, DiagramErrors = 0, p.GeneratedHome
        }).ToArray();
        var entries = DocsSiteSearch.Build(pages, bodyBytes, cancellationToken);
        var artifactBundleMetadata = artifactBundle is null ? null : ArtifactBundleManifest.BuildSiteMetadata(artifactBundle, sourcePages.Select(page => page.Path));
        var index = new { FormatVersion = "docs-site-2.0", options.Title, options.Description, DefaultTheme = options.Theme, HomePageId = home.Id, Pages = records, Entries = entries, ArtifactBundle = artifactBundleMetadata };
        var manifest = new { FormatVersion = "docs-site-2.0", Generator = "Lens Docs Studio Native", options.Title, options.Description, DefaultTheme = options.Theme,
            GeneratedAt = DateTimeOffset.UtcNow, SourcePageCount = sourcePages.Count, PageCount = pages.Count, DiagramTotal = pages.Sum(p => p.DiagramTotal), DiagramErrors = 0,
            HomePage = new { home.Id, home.Title, home.Path, Generated = home.GeneratedHome },
            Assets = new[] { "assets/docs-site.css", "assets/docs-site.js", "assets/docs-site-data.js", "assets/search-index.json" },
            Pages = records.Select(p => new { p.Id, p.Path, p.Title, p.Description, p.Order, p.Tags, p.Draft, p.NavGroup, p.ArtifactKind, p.EvidenceLevel, p.Headings, Diagrams = p.DiagramTotal, p.DiagramErrors, p.GeneratedHome }), ArtifactBundle = artifactBundleMetadata };
        var indexJson = JsonSerializer.Serialize(index, Json);
        using var output = new MemoryStream(); long total = 0;
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            Add("index.html", Shell(options)); Add("assets/docs-site.css", Resource("site.css")); Add("assets/docs-site.js", Resource("site.js"));
            Add("assets/docs-site-data.js", "window.__LENS_DOCS_SITE_DATA__ = " + indexJson + ";\n");
            Add("assets/search-index.json", indexJson); Add("site-manifest.json", JsonSerializer.Serialize(manifest, Json));
            Add("README.md", "# " + options.Title + "\n\nExported by Lens Docs Studio Native.\n\nOpen index.html directly, or copy the complete folder to GitHub Pages or another static host. Keep the assets folder beside index.html. No server, runtime build or external service is required.\n\nThis package contains rendered documentation, embedded local images and diagrams, navigation, local full-text search and light/dark/system themes. Original Markdown and private workspace state are not included.\n");
            Add(".nojekyll", "");
            void Add(string path, string value)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var bytes = Encoding.UTF8.GetBytes(value); total += bytes.Length;
                if (total > MaximumOutputBytes) throw new IOException("The Docs Site exceeds the 256 MB export limit.");
                var entry = archive.CreateEntry(path, CompressionLevel.Optimal); entry.LastWriteTime = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
                using var stream = entry.Open(); stream.Write(bytes);
            }
        }
        return new(output.ToArray(), sourcePages.Count, pages.Count, pages.Sum(p => p.DiagramTotal), warnings);

        DocsSitePage RewriteLinks(DocsSitePage page)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = new HtmlParser().ParseDocument(page.Html);
            foreach (var anchor in document.Body!.QuerySelectorAll("a[href]"))
            {
                var href = anchor.GetAttribute("href")!;
                if (Uri.TryCreate(href, UriKind.Absolute, out var external) && external.Scheme is "https" or "http" or "mailto") continue;
                var parts = href.Split('#', 2); var target = page;
                var path = ResolveReference(page.Path, parts[0]);
                if (path is null || parts[0].Length > 0 && !byPath.TryGetValue(path, out target))
                {
                    anchor.RemoveAttribute("href"); anchor.SetAttribute("title", "Target was not included in this Docs Site.");
                    warnings.Add("Unresolved link in " + page.Path + ": " + href); continue;
                }
                anchor.SetAttribute("href", "#" + target.Id + (parts.Length == 2 ? "/" + Uri.EscapeDataString(Uri.UnescapeDataString(parts[1])) : ""));
            }
            foreach (var anchor in document.Body.QuerySelectorAll("a[data-wikilink-target]"))
            {
                var resolved = wikiIndex.Resolve(anchor.GetAttribute("data-wikilink-target")!, page.Path);
                anchor.RemoveAttribute("data-wikilink-target");
                if (resolved.Path is { } path && byPath.TryGetValue(path, out var linked))
                    anchor.SetAttribute("href", "#" + linked.Id + (resolved.Fragment.Length > 0 ? "/" + Uri.EscapeDataString(resolved.Fragment) : ""));
                else
                {
                    anchor.RemoveAttribute("href"); anchor.SetAttribute("title", resolved.Error ?? "The linked document is not included in this site.");
                    warnings.Add("Unresolved wikilink in " + page.Path + ": " + anchor.TextContent);
                }
            }
            return page with { Html = document.Body.InnerHtml };
        }
    }
    private static string ValidatePath(string path)
    {
        path = path.Replace('\\', '/');
        if (path.Length is 0 or > 4096 || path.StartsWith('/') || path.Contains(':') || path.Any(char.IsControl)
            || path.Split('/').Any(p => p is "" or "." or "..")) throw new IOException("A Docs Site source path is not a safe relative path.");
        return path;
    }
    private static string? ResolveReference(string source, string reference)
    {
        if (reference.Length == 0) return source;
        if (reference.Contains('?') || reference.Contains('\\') || reference.StartsWith('/') || reference.Contains(':')) return null;
        reference = Uri.UnescapeDataString(reference);
        if (reference.Contains(':') || reference.Contains('\\') || reference.StartsWith('/') || reference.Any(char.IsControl)) return null;
        var parts = source.Split('/').SkipLast(1).ToList();
        foreach (var part in reference.Split('/'))
        {
            if (part is "" or ".") continue;
            if (part == "..") { if (parts.Count == 0) return null; parts.RemoveAt(parts.Count - 1); }
            else parts.Add(part);
        }
        return string.Join('/', parts);
    }
    private static string H(string text) => WebUtility.HtmlEncode(text);
    private static string Resource(string name)
    {
        using var stream = typeof(DocsSiteExporter).Assembly.GetManifestResourceStream("DocsSite." + name) ?? throw new IOException("A Docs Site export template is unavailable.");
        using var reader = new StreamReader(stream); return reader.ReadToEnd();
    }
    private static string Shell(DocsSiteOptions options) => "<!doctype html>\n<html lang=\"en-GB\" data-theme-preference=\"" + options.Theme + "\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">"
        + "<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src data:; connect-src 'none'; object-src 'none'; base-uri 'none'; form-action 'none'\">"
        + "<title>" + H(options.Title) + "</title><meta name=\"description\" content=\"" + H(options.Description) + "\"><link rel=\"stylesheet\" href=\"./assets/docs-site.css\"></head><body>"
        + "<a class=\"skip-link\" href=\"#content\">Skip to document</a><div class=\"layout\"><aside><header><strong id=\"siteTitle\"></strong><p id=\"siteDescription\"></p><button id=\"themeToggle\" type=\"button\">System theme</button></header>"
        + "<label for=\"searchInput\">Search docs</label><input id=\"searchInput\" type=\"search\" placeholder=\"Pages, headings and text\"><nav id=\"nav\" aria-label=\"Docs navigation\"></nav><section id=\"searchResults\" aria-label=\"Search results\" aria-live=\"polite\" hidden></section></aside>"
        + "<main><div id=\"docMeta\"></div><article id=\"content\" tabindex=\"-1\"></article><noscript>Enable JavaScript to browse this exported documentation.</noscript></main></div>"
        + "<script src=\"./assets/docs-site-data.js\"></script><script src=\"./assets/docs-site.js\"></script></body></html>";
}
