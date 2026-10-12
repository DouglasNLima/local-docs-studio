using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Core.Parsing;

namespace LensDocsStudio.Core.Tests;

public sealed class DocumentImportTests
{
    [Fact]
    public void Html_import_preserves_headings_emphasis_and_tables_without_active_content()
    {
        var result = new DocumentImportService().ImportHtml("<h1>Café</h1><p><strong>Write</strong> locally.</p><script>steal()</script><iframe src='https://example.com'></iframe><table><tr><th>Area</th><th>Status</th></tr><tr><td>Export</td><td>Ready</td></tr></table>");
        Assert.Contains("# Café", result.Markdown);
        Assert.Contains("**Write**", result.Markdown);
        Assert.Contains("| Export | Ready |", result.Markdown);
        Assert.DoesNotContain("steal", result.Markdown);
        Assert.DoesNotContain("iframe", result.Markdown);
        Assert.Empty(result.Assets);
    }
    [Fact]
    public void Html_import_retains_embedded_image_bytes_for_persistence()
    {
        var result = new DocumentImportService().ImportHtml("<p>Image</p><img alt='Logo' src='data:image/png;base64,iVBORw0KGgo='>");
        var asset = Assert.Single(result.Assets);
        Assert.Contains(asset.Reference, result.Markdown);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, asset.Bytes);
    }
    [Fact]
    public void Html_table_images_are_imported_once_and_active_subtrees_do_not_collect_assets()
    {
        const string image = "<img src='data:image/png;base64,iVBORw0KGgo='>";
        var result = new DocumentImportService().ImportHtml("<table><tr><td>" + image + "</td></tr></table><svg><foreignObject>" + image + "</foreignObject></svg>");
        Assert.Single(result.Assets);
    }
    [Fact]
    public void Excessively_nested_html_is_rejected_before_recursive_conversion()
    {
        Assert.Throws<InvalidDataException>(() => new DocumentImportService().ImportHtml(string.Concat(Enumerable.Repeat("<div>", 150)) + "Deep"));
    }
    [Fact]
    public void Word_export_import_preserves_document_structure_and_unicode()
    {
        var source = "# Café\n\n## Review\n\n**Important** text.\n\n| Area | Status |\n|---|---|\n| Export | Ready |";
        var bytes = new DocxExporter().Export(new(new MarkdownService().Parse(source), new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>()));
        var result = new DocumentImportService().ImportDocx(bytes);
        Assert.Contains("# Café", result.Markdown);
        Assert.Contains("## Review", result.Markdown);
        Assert.Contains("**Important**", result.Markdown);
        Assert.Contains("| Export | Ready |", result.Markdown);
    }

    [Fact]
    public void Word_reimport_retains_each_heading_level_including_list_headings()
    {
        var source = string.Join("\n\n", Enumerable.Range(1, 6).Select(level => new string('#', level) + " Heading" + level))
            + "\n\n- ### NestedHeading\n\n  Continuation";
        var bytes = new DocxExporter().Export(new(new MarkdownService().Parse(source), new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>()));
        var result = new DocumentImportService().ImportDocx(bytes);
        var parsed = new MarkdownService().Parse(result.Markdown);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 3 }, parsed.Headings.Select(h => h.Level));
        Assert.Equal(new[] { "Heading1", "Heading2", "Heading3", "Heading4", "Heading5", "Heading6", "NestedHeading" }, parsed.Headings.Select(h => h.Title));
    }
}
