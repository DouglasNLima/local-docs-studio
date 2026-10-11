using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Core.Tests;

public sealed class SafeInlineSemanticsTests
{
    private const string Snippets = "Status: <span data-status-badge data-status-kind=ready>Ready &amp; reviewed</span>. "
        + "Use <span data-shortcut><kbd>Ctrl</kbd> + <kbd>K</kbd></span> or <kbd>F1</kbd>.";
    private static ExportContent Content(string source) => new(new MarkdownService().Parse(source), new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>());

    [Fact] public void Passive_snippets_retain_decoded_text_kind_and_exact_source_ranges()
    {
        var parsed = new MarkdownService().Parse(Snippets);
        var inlines = parsed.Syntax.Descendants<DocumentSemanticInline>().ToArray();
        Assert.Equal(new[] { DocumentInlineKind.StatusBadge, DocumentInlineKind.Shortcut, DocumentInlineKind.KeyboardKey }, inlines.Select(i => i.Kind));
        Assert.Equal(new[] { "Ready & reviewed", "Ctrl + K", "F1" }, inlines.Select(i => i.Text));
        Assert.Equal("ready", inlines[0].Variant);
        Assert.Equal("<span data-status-badge data-status-kind=ready>Ready &amp; reviewed</span>", Snippets[inlines[0].Span.Start..(inlines[0].Span.End + 1)]);
        Assert.Equal(Snippets, parsed.Source);
        Assert.Equal("Status: Ready & reviewed. Use Ctrl + K or F1.", MarkdownService.InlineText(Assert.Single(parsed.Syntax.OfType<ParagraphBlock>()).Inline));
    }

    [Fact] public void Badge_labels_use_Markdown_visible_text_without_promoting_attributes()
    {
        var content = Content("Label: <span data-status-badge data-status-kind=unexpected onclick='run()' style='background:url(https://example.com)' aria-label='False label'>**Ready** &amp; \\*reviewed\\*</span>.");
        var badge = Assert.Single(content.Document.Syntax.Descendants<DocumentSemanticInline>());
        Assert.Equal("Ready & *reviewed*", badge.Text); Assert.Equal("custom", badge.Variant);
        var html = new HtmlExporter().RenderFragment(content);
        Assert.Contains("<strong>Ready &amp; *reviewed*</strong>", html);
        Assert.DoesNotContain("onclick", html); Assert.DoesNotContain("example.com", html); Assert.DoesNotContain("False label", html);
    }

    [Theory]
    [InlineData("<span data-status-badge>[Document](document.md)</span>")]
    [InlineData("<span data-status-badge>![Image](image.png)</span>")]
    [InlineData("<span data-status-badge><img src='image.png'>Ready</span>")]
    [InlineData("<span data-status-badge><script>run()</script>Ready</span>")]
    [InlineData("<span data-status-badge>Unclosed")]
    [InlineData("<span title='data-status-badge'>Ordinary text</span>")]
    [InlineData("<span data-status-badge><!-- hidden -->Ready</span>")]
    public void Unsupported_content_retains_its_original_inline_tree(string source)
    {
        var parsed = new MarkdownService().Parse("Before " + source);
        Assert.Empty(parsed.Syntax.Descendants<DocumentSemanticInline>());
        Assert.Equal("Before " + source, parsed.Source);
        if (source.Contains("](image.png)", StringComparison.Ordinal)) Assert.True(Assert.Single(parsed.Syntax.Descendants<LinkInline>()).IsImage);
        if (source.Contains("](document.md)", StringComparison.Ordinal)) Assert.Equal("document.md", Assert.Single(parsed.Syntax.Descendants<LinkInline>()).Url);
    }

    [Fact] public void Oversized_labels_and_code_examples_are_not_interpreted_as_passive_snippets()
    {
        var source = "`<kbd>F1</kbd>`\n\n```html\n<span data-status-badge>Ready</span>\n```\n\nStatus: <span data-status-badge>" + new string('a', 513) + "</span>";
        var parsed = new MarkdownService().Parse(source);
        Assert.Empty(parsed.Syntax.Descendants<DocumentSemanticInline>());
        Assert.Single(parsed.Syntax.Descendants<CodeInline>()); Assert.Single(parsed.Syntax.Descendants<FencedCodeBlock>());
    }

    [Fact] public void Html_and_Word_exports_retain_passive_labels_shortcuts_and_decoded_entities()
    {
        var content = Content("# Research &amp; review\n\n" + Snippets + "\n\nA &lt; B; &#169; documentation.");
        Assert.Equal("Research & review", Assert.Single(content.Document.Headings).Title);
        var html = new HtmlExporter().RenderFragment(content);
        Assert.Contains("<kbd>Ctrl + K</kbd>", html); Assert.Contains("<strong>Ready &amp; reviewed</strong>", html);
        using var package = WordprocessingDocument.Open(new MemoryStream(new DocxExporter().Export(content)), false);
        Assert.Empty(new OpenXmlValidator().Validate(package));
        var document = package.MainDocumentPart!.Document!;
        Assert.Contains("Research & review", document.InnerText); Assert.Contains("A < B; © documentation.", document.InnerText);
        Assert.NotNull(Assert.Single(document.Descendants<W.Run>(), r => r.InnerText == "Ready & reviewed").RunProperties!.Bold);
        Assert.Equal("Consolas", Assert.Single(document.Descendants<W.Run>(), r => r.InnerText == "Ctrl + K").RunProperties!.RunFonts!.Ascii!.Value);
        Assert.DoesNotContain("<span", document.InnerText);
    }

    [Fact] public void Markdown_clipboard_retains_the_literal_HTML_source_of_recognised_snippets()
    {
        var exported = new MarkdownClipboardExporter().Export(Content(Snippets));
        Assert.Equal(Snippets, exported.PlainText);
        var carried = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(HtmlClipboardFormat.Decode(exported.Html));
        Assert.Equal(Snippets, carried.QuerySelector("[data-markdown-clipboard]")!.TextContent);
        Assert.Empty(carried.QuerySelectorAll("kbd,strong,[data-status-badge],[data-shortcut]"));
    }
}
