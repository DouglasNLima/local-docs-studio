using System.IO.Compression;
using System.Text;
using System.Text.Json;
using AngleSharp.Html.Parser;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;

namespace LensDocsStudio.Core.Tests;

public sealed class DocsSiteTests
{
    private static readonly DocsSiteExporter Exporter = new();
    private static DocsSitePage Page(string path, string source) => Exporter.PreparePage(path,
        new(new MarkdownService().Parse(source), new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>()));
    private static Dictionary<string, string> Entries(byte[] bytes)
    {
        using var archive = new ZipArchive(new MemoryStream(bytes));
        return archive.Entries.ToDictionary(e => e.FullName, e => { using var reader = new StreamReader(e.Open(), Encoding.UTF8); return reader.ReadToEnd(); });
    }

    [Fact] public void Site_preserves_metadata_priority_ordering_relative_links_and_offline_assets()
    {
        var readme = Page("README.md", "---\ntitle: Handbook\norder: 0\n---\n# Body title\n\n[Start](docs/Start%20here.md#evidence)");
        var guide = Page("docs/Start here.md", "---\ntitle: Start here\norder: 1\ndescription: Review instructions\ntags: [review, evidence]\nnavGroup: Guides\ndraft: true\n---\n# Getting started\n\n## Evidence\n\nKeep **evidence**. [Home](../README.md)");
        var result = Exporter.Export([guide, readme], new("Local docs", "Local documentation", "dark"));
        Assert.Empty(result.Warnings); Assert.Equal(2, result.Pages); Assert.Equal(2, result.SourcePages);
        var entries = Entries(result.Bytes);
        Assert.Equal(8, entries.Count); Assert.Contains(".nojekyll", entries.Keys); Assert.Contains("assets/docs-site-data.js", entries.Keys);
        using var index = JsonDocument.Parse(entries["assets/search-index.json"]);
        var pages = index.RootElement.GetProperty("pages").EnumerateArray().ToArray();
        Assert.Equal(new[] { "Handbook", "Start here" }, pages.Select(p => p.GetProperty("title").GetString()));
        Assert.Equal(readme.Id, index.RootElement.GetProperty("homePageId").GetString());
        Assert.True(pages[1].GetProperty("draft").GetBoolean()); Assert.Equal("Guides", pages[1].GetProperty("navGroup").GetString());
        Assert.Contains("#" + guide.Id + "/evidence", pages[0].GetProperty("html").GetString());
        Assert.Contains("#" + readme.Id, pages[1].GetProperty("html").GetString());
        Assert.DoesNotContain("order:", pages[0].GetProperty("html").GetString());
        Assert.Contains("Review instructions", index.RootElement.GetProperty("entries")[1].GetProperty("text").GetString());
        Assert.StartsWith("window.__LENS_DOCS_SITE_DATA__ = ", entries["assets/docs-site-data.js"]);
        Assert.Contains("connect-src 'none'", entries["index.html"]); Assert.DoesNotContain("http://", entries["index.html"]);
    }

    [Fact] public void Site_generates_a_home_and_keeps_active_markup_and_metadata_inert()
    {
        var page = Page("guide.md", "---\ntitle: </script><img src=x onerror=run()>\n---\n# Guide\n\n<script>run()</script>\n\n[Unsafe](javascript:run)");
        var entries = Entries(Exporter.Export([page], new("<script>Title</script>")).Bytes);
        using var index = JsonDocument.Parse(entries["assets/search-index.json"]);
        Assert.Equal("home", index.RootElement.GetProperty("homePageId").GetString());
        foreach (var item in index.RootElement.GetProperty("pages").EnumerateArray())
        {
            var html = new HtmlParser().ParseDocument(item.GetProperty("html").GetString()!);
            Assert.Empty(html.QuerySelectorAll("script,[onerror],[onclick],[src=x],a[href^='javascript:']"));
        }
        Assert.DoesNotContain("</script><img", entries["assets/docs-site-data.js"]);
        Assert.DoesNotContain("lens-artifact-bundle.json", entries.Keys);
    }

    [Fact] public void Unresolved_and_escaping_links_are_visible_without_local_navigation()
    {
        var page = Page("guide.md", "[Missing](missing.md) [Outside](../private.md) [Remote](https://example.com)");
        var result = Exporter.Export([page], new("Guide"));
        Assert.Equal(2, result.Warnings.Count);
        using var index = JsonDocument.Parse(Entries(result.Bytes)["assets/search-index.json"]);
        var html = new HtmlParser().ParseDocument(index.RootElement.GetProperty("pages")[1].GetProperty("html").GetString()!);
        Assert.Equal("https://example.com", Assert.Single(html.QuerySelectorAll("a[href]")).GetAttribute("href"));
        Assert.Contains("Missing", html.Body!.TextContent); Assert.Contains("Outside", html.Body.TextContent);
    }

    [Theory]
    [InlineData("../escape.md")]
    [InlineData("/absolute.md")]
    [InlineData("C:/local.md")]
    [InlineData("folder//document.md")]
    public void Source_paths_must_be_safe_relative_paths(string path) => Assert.Throws<IOException>(() => Page(path, "# Source"));

    [Fact] public void Duplicate_paths_empty_sites_invalid_options_and_cancellation_fail_before_output()
    {
        var page = Page("guide.md", "# Guide");
        Assert.Throws<IOException>(() => Exporter.Export([page, Page("GUIDE.md", "# Other")], new("Docs")));
        Assert.Throws<IOException>(() => Exporter.Export([], new("Docs")));
        Assert.Throws<ArgumentException>(() => Exporter.Export([page], new("Docs", Theme: "invalid")));
        Assert.Throws<OperationCanceledException>(() => Exporter.Export([page], new("Docs"), new(true)));
    }
}
