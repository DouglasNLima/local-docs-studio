using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;

namespace LensDocsStudio.Core.Tests;

public sealed class FrontMatterTests
{
    [Fact] public void Existing_flat_metadata_contract_preserves_values_and_original_source_offsets()
    {
        const string source = "\uFEFF---\r\ntitle: 'Reviewed guide'\r\ndescription: A local guide\r\norder: -2.5\r\ntags: [review, 'Café', false]\r\ndraft: true\r\nnav-group: Handbook\r\nunknown: ignored\r\n---\r\n# Visible title\r\n\r\nBody.";
        var parsed = new MarkdownService().Parse(source);
        Assert.Equal("Reviewed guide", parsed.Metadata.Title); Assert.Equal("A local guide", parsed.Metadata.Description);
        Assert.Equal(-2.5, parsed.Metadata.Order); Assert.Equal(new[] { "review", "Café" }, parsed.Metadata.Tags);
        Assert.True(parsed.Metadata.Draft); Assert.Equal("Handbook", parsed.Metadata.NavigationGroup);
        Assert.Equal(source, parsed.Source);
        var heading = Assert.Single(parsed.Headings);
        Assert.Equal("Visible title", heading.Title); Assert.Equal(source.IndexOf('#'), heading.SourceStart); Assert.Equal(9, heading.SourceLine);
        Assert.Equal(2, parsed.Syntax.Count);
    }

    [Theory]
    [InlineData("navGroup")]
    [InlineData("nav_group")]
    [InlineData("nav-group")]
    public void Navigation_aliases_and_later_values_match_the_flat_contract(string key)
    {
        var result = FrontMatterService.Read("---\ntitle: First\nTITLE: Second\n" + key + ": Guides\ndraft: false\norder: not-a-number\ntags: review\n---\nBody");
        Assert.Equal("Second", result.Metadata.Title); Assert.Equal("Guides", result.Metadata.NavigationGroup);
        Assert.False(result.Metadata.Draft); Assert.Null(result.Metadata.Order); Assert.Equal(new[] { "review" }, result.Metadata.Tags);
    }

    [Theory]
    [InlineData("---\nUnclosed header\n# Body")]
    [InlineData("Before\n---\ntitle: Later\n---\n# Body")]
    [InlineData("```yaml\n---\ntitle: Example\n---\n```")]
    public void Unclosed_headers_and_examples_remain_source_content(string source)
    {
        var result = FrontMatterService.Read(source); Assert.Equal(0, result.BodyStart); Assert.Equal("", result.Metadata.Title);
        Assert.Equal(source, FrontMatterService.HideHeader(source, result.BodyStart));
    }

    [Fact] public void Oversized_headers_remain_visible_instead_of_being_silently_removed()
    {
        var source = "---\ntitle: " + new string('x', FrontMatterService.MaximumHeaderCharacters) + "\n---\n# Body";
        Assert.Equal(0, FrontMatterService.Read(source).BodyStart);
    }

    [Fact] public void Header_stripping_retains_body_image_spans_and_source_clipboard_metadata()
    {
        const string source = "---\ntitle: Source metadata\n---\n# Body\n\n![Image](asset.png)";
        var parsed = new MarkdownService().Parse(source);
        var content = new ExportContent(parsed, new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage> { ["asset.png"] = new([1, 2, 3], 1, 1, "Image") });
        var copy = new MarkdownClipboardExporter().Export(content);
        Assert.Equal(source.Replace("asset.png", "data:image/png;base64,AQID"), copy.PlainText);
        var html = new HtmlExporter().RenderFragment(content);
        Assert.Contains(">Body</h1>", html); Assert.DoesNotContain("Source metadata", html);
    }

    [Fact] public void Standalone_Mermaid_retains_its_literal_front_matter_like_lines()
    {
        const string source = "---\ntitle: Diagram\n---\nflowchart TD\nA --> B";
        var parsed = new MarkdownService().Parse(source, true);
        Assert.Equal(source, Assert.IsType<FencedCodeBlock>(Assert.Single(parsed.Syntax)).Lines.ToString());
        Assert.Equal("", parsed.Metadata.Title);
    }
}
