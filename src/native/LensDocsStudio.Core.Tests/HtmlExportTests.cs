using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;

namespace LensDocsStudio.Core.Tests;

public sealed class HtmlExportTests
{
    [Fact]
    public void Active_html_and_script_links_are_never_exported_as_executable_content()
    {
        var parsed = new MarkdownService().Parse("# Safe\n\n<script>alert(1)</script>\n\n[Unsafe](javascript:alert)\n\n**Readable**");
        var html = new HtmlExporter().Export(new(parsed, new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>()));
        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("<strong>Readable</strong>", html);
        Assert.Contains("Content-Security-Policy", html);
    }
}
