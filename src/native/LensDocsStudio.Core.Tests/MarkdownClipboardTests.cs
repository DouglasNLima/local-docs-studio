using AngleSharp.Html.Parser;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LensDocsStudio.Core.Tests;

public sealed class MarkdownClipboardTests
{
    private static readonly ExportImage Image = new([1, 2, 3], 1, 1, "Prepared image");
    private static ExportedClipboard Copy(string source, Dictionary<string, ExportImage>? images = null, bool standalone = false)
    {
        var parsed = new MarkdownService().Parse(source, standalone);
        return new MarkdownClipboardExporter().Export(new(parsed,
            parsed.Syntax.Descendants<FencedCodeBlock>().Where(c => c.Info is "mermaid" or "mmd").ToDictionary(c => c.Span.Start, _ => Image), images ?? []));
    }

    [Fact] public void Existing_Azure_DevOps_fidelity_fixture_keeps_every_character_outside_its_two_image_destinations()
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "azure-devops-markdown-fidelity.md"));
        var copy = Copy(source, new() { ["test image"] = Image, ["second test image"] = new([4, 5, 6], 1, 1, "Second") });
        Assert.Equal(source.Replace("<test image>", "<data:image/png;base64,AQID>").Replace("<second test image>", "<data:image/png;base64,BAUG>"), copy.PlainText);
        var html = new HtmlParser().ParseDocument(HtmlClipboardFormat.Decode(copy.Html));
        Assert.Empty(html.QuerySelectorAll("h1,h2,h3,p,table,ul,ol,li,blockquote,pre,code,strong,em,a,hr"));
        Assert.Equal(new[] { "Architecture diagram", "Secondary diagram" }, html.QuerySelectorAll("img").Select(i => i.GetAttribute("alt")));
    }

    [Fact] public void Literal_Markdown_copy_keeps_CRLF_whitespace_punctuation_and_raw_HTML_without_semantic_elements()
    {
        const string source = "# Café 🛡️\r\n\r\n**bold** _italic_ `code`  \r\n\r\n- one\r\n  - two\r\n\r\n| A | B |\r\n|---|---|\r\n| 1 | 2 |\r\n\r\n---\r\n<script>alert(1)</script>\r\n";
        var copy = Copy(source);
        Assert.Equal(source, copy.PlainText);
        var html = new HtmlParser().ParseDocument(HtmlClipboardFormat.Decode(copy.Html));
        Assert.Empty(html.QuerySelectorAll("h1,h2,p,ul,ol,li,table,pre,code,strong,em,a,hr,script"));
        Assert.Equal(source.Replace("\r\n", "\n"), html.QuerySelector("[data-markdown-clipboard]")!.TextContent);
        Assert.Equal(source, new ClipboardPasteService().Convert(new(copy.PlainText, copy.Html), PasteMode.Auto).Document.Markdown);
    }

    [Fact] public void Repeated_images_keep_their_positions_alt_text_titles_and_all_surrounding_source()
    {
        const string source = "Before\r\n![First](<asset one.png> \"Title & one\") then ![Second](<asset one.png>)\r\nAfter";
        var copy = Copy(source, new() { ["asset one.png"] = Image });
        Assert.Equal(source.Replace("asset one.png", "data:image/png;base64,AQID"), copy.PlainText);
        var html = new HtmlParser().ParseDocument(HtmlClipboardFormat.Decode(copy.Html));
        var images = html.QuerySelectorAll("img");
        Assert.Equal(new[] { "First", "Second" }, images.Select(i => i.GetAttribute("alt")));
        Assert.Equal(new[] { "0", "1" }, images.Select(i => i.GetAttribute("data-markdown-clipboard-image")));
        Assert.Equal("Title & one", images[0].GetAttribute("title"));
        Assert.Equal("Before\n then \nAfter", html.QuerySelector("span")!.TextContent);
        var paste = new ClipboardPasteService().Convert(new(copy.PlainText, copy.Html), PasteMode.Auto);
        var asset = Assert.Single(paste.Document.Assets);
        Assert.Equal(new byte[] { 1, 2, 3 }, asset.Bytes);
        Assert.Equal(source.Replace("asset one.png", asset.Reference), paste.Document.Markdown);
    }

    [Fact] public void Reference_images_expand_only_at_image_occurrences_and_keep_shared_links_and_definitions()
    {
        const string source = "![A *label*][shared]\n\n[Download][shared]\n\n[shared]: images/picture.png \"Picture title\"\n";
        var copy = Copy(source, new() { ["images/picture.png"] = Image });
        Assert.Equal("![A *label*](data:image/png;base64,AQID \"Picture title\")\n\n[Download][shared]\n\n[shared]: images/picture.png \"Picture title\"\n", copy.PlainText);
        var image = Assert.Single(new MarkdownService().Parse(copy.PlainText).Syntax.Descendants<LinkInline>(), i => i.IsImage);
        Assert.Equal("A label", MarkdownService.InlineText(image)); Assert.Equal("Picture title", image.Title);
    }

    [Theory]
    [InlineData("```mermaid\ngraph TD\n A-->B\n```", true)]
    [InlineData("::: mermaid\ngraph TD\n A-->B\n:::", true)]
    [InlineData("~~~mermaid\ngraph TD\n A-->B\n~~~~", true)]
    [InlineData("```mermaid\ngraph TD\n A-->B", false)]
    [InlineData("::: mermaid\ngraph TD\n A-->B", false)]
    public void Only_complete_Mermaid_blocks_become_images(string block, bool complete)
    {
        var copy = Copy("Before\n\n" + block + "\n");
        Assert.Equal(complete ? "Before\n\n![Mermaid diagram](data:image/png;base64,AQID)\n" : "Before\n\n" + block + "\n", copy.PlainText);
    }

    [Fact] public void Standalone_Mermaid_copies_as_one_image()
        => Assert.Equal("![Mermaid diagram](data:image/png;base64,AQID)", Copy("graph TD\n A-->B", standalone: true).PlainText);

    [Fact] public void Image_examples_inside_code_and_remote_references_remain_literal()
    {
        const string source = "`![Example](asset.png)`\n\n```md\n![Example](asset.png)\n```\n\n![Remote](https://example.invalid/a.png)\n\n![Real](asset.png)";
        var copy = Copy(source, new() { ["asset.png"] = Image });
        Assert.Equal(source.Replace("![Real](asset.png)", "![Real](data:image/png;base64,AQID)"), copy.PlainText);
        Assert.Single(new HtmlParser().ParseDocument(HtmlClipboardFormat.Decode(copy.Html)).QuerySelectorAll("img"));
        var paste = new MarkdownImageImporter().Import("`![Example](data:image/png;base64,AQID)`\n\n![Real](data:image/png;base64,AQID)");
        Assert.Single(paste.Assets); Assert.StartsWith("`![Example](data:image/png;base64,AQID)`", paste.Markdown);
    }

    [Fact] public void Source_carrier_keeps_non_heading_plain_source_authoritative_and_text_paste_keeps_data_URIs_literal()
    {
        var copy = Copy("**bold**\n\n_italic_\n");
        Assert.Equal(copy.PlainText, new ClipboardPasteService().Convert(new(copy.PlainText, copy.Html), PasteMode.Auto).Document.Markdown);
        const string source = "![Image](data:image/png;base64,AQID)";
        var text = new ClipboardPasteService().Convert(new(source, "<p>Alternative</p>"), PasteMode.Text);
        Assert.Equal(source, text.Document.Markdown); Assert.Empty(text.Document.Assets);
    }

    [Theory]
    [InlineData("data:image/svg+xml;base64,AQID")]
    [InlineData("data:image/png;base64,???")]
    [InlineData("data:image/png;base64,")]
    public void Invalid_embedded_Markdown_images_fail_before_any_editor_change(string reference)
        => Assert.Throws<IOException>(() => new MarkdownImageImporter().Import("![Invalid](" + reference + ")"));

    [Fact] public void Image_import_cancellation_and_copy_limits_are_enforced()
    {
        Assert.Throws<OperationCanceledException>(() => new MarkdownImageImporter().Import("source", new(true)));
        var parsed = new ParsedDocument(new string('x', 4 * 1024 * 1024 + 1), new(), []);
        Assert.Throws<IOException>(() => new MarkdownClipboardExporter().Export(new(parsed, new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>())));
        Assert.Throws<IOException>(() => Copy(string.Join('\n', Enumerable.Repeat("![Same](image.png)", 9)),
            new() { ["image.png"] = new(new byte[3 * 1024 * 1024], 1, 1, "Same") }));
    }

    [Theory]
    [InlineData("![Visible][]\n\n[Visible]: image.png")]
    [InlineData("![Visible]\n\n[Visible]: image.png")]
    public void Collapsed_and_shortcut_reference_images_keep_visible_alt_text(string source)
    {
        var copy = Copy(source, new() { ["image.png"] = Image });
        Assert.StartsWith("![Visible](data:image/png;base64,AQID)\n\n", copy.PlainText);
    }

    [Fact] public void Image_looking_text_that_is_not_a_parsed_image_stays_literal()
    {
        const string source = "![Visible] [shared]\n\n[shared]: image.png";
        Assert.Equal(source, Copy(source, new() { ["image.png"] = Image }).PlainText);
    }

    [Fact] public void HTML_only_source_carrier_retains_literal_text_and_imports_its_ordered_images()
    {
        var copy = Copy("**bold**\n\n![A](image.png \"Title\")\n\n- item", new() { ["image.png"] = Image });
        var paste = new ClipboardPasteService().Convert(new("", copy.Html), PasteMode.Auto);
        var asset = Assert.Single(paste.Document.Assets);
        Assert.Equal("**bold**\n\n![A](" + asset.Reference + " \"Title\")\n\n- item", paste.Document.Markdown);
    }

    [Fact] public void Markdown_source_remains_authoritative_even_when_an_unused_HTML_representation_is_invalid()
    {
        const string source = "# Heading\r\n";
        var paste = new ClipboardPasteService().Convert(new(source, "Version:1.0\nStartFragment:0\ndata-markdown-clipboard"), PasteMode.Auto);
        Assert.Equal(source, paste.Document.Markdown);
    }
}
