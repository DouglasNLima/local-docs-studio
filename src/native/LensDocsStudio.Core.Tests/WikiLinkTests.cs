using System.IO.Compression;
using System.Text.Json;
using AngleSharp.Html.Parser;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;

namespace LensDocsStudio.Core.Tests;

public sealed class WikiLinkTests
{
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Parser_preserves_exact_source_spans_aliases_and_literal_contexts(string newline)
    {
        var source = string.Join(newline, "# [[Guide|Readable heading]]", "", "Before **[[ docs/Café.md#detail | Friendly | label ]]** after.",
            "", "`[[Code]]` and \\[[Escaped]]", "", "```md", "[[Fenced]]", "```", "", "[[[Nested]]](https://example.test)", "",
            "<!-- [[Comment]] -->", "", "[[Unfinished", "");
        var parsed = new MarkdownService().Parse(source);
        var links = parsed.Syntax.Descendants<WikiLinkInline>().ToArray();
        Assert.Equal(2, links.Length); Assert.Equal("Readable heading", Assert.Single(parsed.Headings).Title);
        Assert.Equal("Guide", source.Substring(links[0].TargetSpan.Start, links[0].TargetSpan.Length));
        Assert.Equal("docs/Café.md#detail", links[1].Target);
        Assert.Equal("Friendly | label", links[1].Label);
        Assert.Equal("[[ docs/Café.md#detail | Friendly | label ]]", source.Substring(links[1].Span.Start, links[1].Span.Length));
        Assert.Equal(2, links[1].Line); Assert.Equal(source, parsed.Source);
        Assert.Empty(MarkdownLinks.Read("[[Guide]] `[[Other]]`"));
    }

    [Theory]
    [InlineData("[[]]")]
    [InlineData("[[   | alias ]]")]
    [InlineData("[[line\nbreak]]")]
    [InlineData("[[target]")]
    public void Incomplete_or_empty_wikilinks_remain_source(string source) => Assert.Empty(WikiLinks.Read(source));

    [Fact] public void Oversized_wikilink_is_left_literal() => Assert.Empty(WikiLinks.Read("[[" + new string('a', 4097) + "]]"));

    [Theory]
    [InlineData("Guide", "docs/Guide.md", "Guide.md", "")]
    [InlineData("next", "docs/Guide.md", "docs/next.markdown", "")]
    [InlineData("../Guide#intro", "docs/Guide.md", "Guide.md", "intro")]
    [InlineData("DIAGRAM", "docs/Guide.md", "diagrams/Diagram.mmd", "")]
    [InlineData("docs/Caf%C3%A9%20guide.md#d%C3%A9tail", "Guide.md", "docs/Café guide.md", "détail")]
    [InlineData("#local", "Guide.md", "Guide.md", "local")]
    [InlineData("./docs//next", "Guide.md", "docs/next.markdown", "")]
    public void Resolver_prefers_explicit_paths_then_unique_names(string target, string current, string expected, string fragment)
    {
        var index = new WikiLinkIndex(["Guide.md", "docs/Guide.md", "docs/next.markdown", "diagrams/Diagram.mmd", "docs/Café guide.md"]);
        var result = index.Resolve(target, current);
        Assert.True(result.Found); Assert.Equal(expected, result.Path); Assert.Equal(fragment, result.Fragment);
    }

    [Theory]
    [InlineData("https://example.test")]
    [InlineData("javascript:alert(1)")]
    [InlineData("C:/private.md")]
    [InlineData("//server/private.md")]
    [InlineData("%2f%2fserver/private.md")]
    [InlineData("file%3a/private.md")]
    [InlineData("../../../private")]
    [InlineData("Guide?query")]
    [InlineData("Guide#bad%0Afragment")]
    public void Resolver_never_authorises_external_or_escaping_targets(string target)
    {
        var result = new WikiLinkIndex(["Guide.md", "private.md", "server/private.md"]).Resolve(target, "Guide.md");
        Assert.False(result.Found); Assert.NotNull(result.Error);
    }

    [Fact] public void Ambiguous_names_are_reported_without_choosing_a_file()
    {
        var index = new WikiLinkIndex(["home.md", "one/Summary.md", "two/Summary.md", "Guide.md", "Guide.markdown"]);
        var result = index.Resolve("Summary", "home.md");
        Assert.False(result.Found); Assert.Equal(new[] { "one/Summary.md", "two/Summary.md" }, result.Candidates);
        Assert.Contains("ambiguous", result.Error); Assert.False(index.Resolve("Guide", "home.md").Found);
        Assert.Equal("one/Summary.md", index.Resolve("one/Summary", "home.md").Path);
    }

    [Fact] public void Catalogue_rejects_unsafe_and_unsupported_paths_and_has_a_bound()
    {
        var index = new WikiLinkIndex(["Guide.md", "GUIDE.md", "../private.md", "https://example.test/a.md", "image.png", "bad./file.md"]);
        Assert.Single(index.Paths); Assert.Throws<IOException>(() => new WikiLinkIndex(Enumerable.Repeat("Guide.md", 20001)));
    }

    [Fact] public void Html_and_site_exports_keep_alias_text_and_resolve_only_included_documents()
    {
        const string source = "# Home\n\n[[docs/Guide#evidence|Read <guide>]] [[Missing|Unresolved]] [[javascript:alert(1)|Unsafe]]";
        var parser = new MarkdownService(); var content = new ExportContent(parser.Parse(source), new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>());
        var fragment = new HtmlExporter().RenderFragment(content);
        Assert.Contains("Read &lt;guide&gt;", fragment); Assert.DoesNotContain("data-wikilink", fragment); Assert.DoesNotContain("href=", fragment);
        var exporter = new DocsSiteExporter(); var home = exporter.PreparePage("README.md", content);
        var guide = exporter.PreparePage("docs/Guide.md", new(parser.Parse("# Guide\n\n## Evidence"), new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>()));
        var result = exporter.Export([home, guide], new("Wiki site"));
        Assert.Equal(2, result.Warnings.Count);
        using var archive = new ZipArchive(new MemoryStream(result.Bytes)); using var index = JsonDocument.Parse(archive.GetEntry("assets/search-index.json")!.Open());
        var html = index.RootElement.GetProperty("pages").EnumerateArray().Single(page => page.GetProperty("path").GetString() == "README.md").GetProperty("html").GetString()!;
        var document = new HtmlParser().ParseDocument(html);
        Assert.Equal("#" + guide.Id + "/evidence", document.QuerySelector("a[href]")!.GetAttribute("href"));
        Assert.Equal("Read <guide>", document.QuerySelector("a[href]")!.TextContent);
        Assert.Equal(2, document.QuerySelectorAll("a:not([href])").Length); Assert.DoesNotContain("data-wikilink", html);
        Assert.Equal(source, content.Document.Source);
    }
}
