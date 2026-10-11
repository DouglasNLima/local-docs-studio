using System.IO.Compression;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Native.Exports;

namespace LensDocsStudio.Native.Validation;

internal static class NativeDocsSiteChecks
{
    public static void Run(string output)
    {
        var root = Path.Combine(output, "site-source-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var docs = Path.Combine(root, "docs"); var state = Path.Combine(root, "private-state"); Directory.CreateDirectory(docs); Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(root, "README.md"), "---\ntitle: Native handbook\norder: 0\n---\n# Native documentation\n\n[Start here](docs/start.md#evidence)\n\n![Native pixels](image.png)\n\n```mermaid\nflowchart LR\nA[Write] --> B[Review]\n```\n");
        File.AppendAllText(Path.Combine(root, "README.md"), "\n<figure data-progress-bar data-progress-colour=\"blue\"><figcaption><span data-progress-label>Review progress</span><span data-progress-value>37%</span></figcaption><progress max=\"100\" value=\"37\">37%</progress></figure>\n");
        var path = Path.Combine(docs, "start.md"); File.WriteAllText(path, "# Saved content");
        File.WriteAllText(Path.Combine(state, "private.md"), "Private recovery must stay outside the site.");
        var pixels = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 255, 255, 0, 255, 0, 255, 255, 0, 0, 255, 0, 0, 0, 255 }, 8);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(pixels));
        using (var image = File.Create(Path.Combine(root, "image.png"))) encoder.Save(image);
        var edited = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [path] = "---\ntitle: Getting started\norder: 1\nnavGroup: Guides\ntags: [review, evidence]\ndraft: true\n---\n# Getting started\n\n## Evidence\n\nUnsaved reviewed content. [Home](../README.md)\n" };
        edited[path] += "\n## Publishing\n\n" + string.Join(' ', Enumerable.Repeat("Background detail.", 35))
            + " release-ready-token remains searchable.\n\n```html\n<img src=x onerror=run()>\n```\n\n> ### Nested notes\n>\n> Local nested-section-token evidence.\n";
        edited[path] += "\n[Named target](#named-position) [Empty target](#empty-position)\n\nBefore <span id=named-position data-doc-anchor>Named destination</span> after.\n\nBefore <span id=empty-position data-doc-anchor></span>following text.\n";
        var result = new DocsSiteExportService().ExportAsync(root, edited, state, new("Native handbook", "Local documentation export")).GetAwaiter().GetResult();
        if (result.SourcePages != 2 || result.Diagrams != 1 || result.Warnings.Count != 0) throw new IOException("Native Docs Site lost documents or included private state.");
        var zipPath = Path.Combine(output, "native-docs-site.zip"); File.WriteAllBytes(zipPath, result.Bytes);
        var extracted = Path.Combine(output, "docs-site"); Directory.CreateDirectory(extracted); ZipFile.ExtractToDirectory(zipPath, extracted, true);
        var indexText = File.ReadAllText(Path.Combine(extracted, "assets", "search-index.json"));
        using var index = JsonDocument.Parse(indexText);
        if (indexText.Contains("Private recovery", StringComparison.Ordinal) || !indexText.Contains("Unsaved reviewed content", StringComparison.Ordinal)
            || !index.RootElement.GetProperty("pages")[0].GetProperty("html").GetString()!.Contains("data:image/png;base64,", StringComparison.Ordinal))
            throw new IOException("Native Docs Site lost current editor text, embedded pixels or private-state isolation.");
        if (File.ReadAllText(path) != "# Saved content") throw new IOException("Docs Site export changed a source document.");
        File.WriteAllText(Path.Combine(output, "native-docs-site.json"), JsonSerializer.Serialize(new
        {
            Passed = true, result.SourcePages, result.Pages, result.Diagrams, PackageBytes = result.Bytes.Length,
            Checks = new[] { "Native workspace export includes current saved-file drafts without writing source", "Private native state stays outside the package",
                "Actual raster pixels and native Mermaid drawings are embedded", "A portable site ZIP contains metadata, navigation and search data" },
            IndexPath = Path.Combine(extracted, "index.html")
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
