using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Core.Tests;

public sealed class AutolinkExportTests
{
    [Theory]
    [InlineData("https://example.org/review?q=one&mode=two#result", "https://example.org/review?q=one&mode=two#result")]
    [InlineData("http://example.org/path", "http://example.org/path")]
    [InlineData("review@example.org", "mailto:review@example.org")]
    [InlineData("mailto:review@example.org?subject=Review", "mailto:review@example.org?subject=Review")]
    [InlineData("file:///C:/private/readme.md", null)]
    [InlineData("javascript:example", null)]
    public void Autolink_labels_survive_headings_plain_text_and_Word_with_only_allowed_relationships(string label, string? destination)
    {
        var source = $"# Review <{label}>\r\n\r\nBefore **<{label}>** after.";
        var parsed = new MarkdownService().Parse(source);
        Assert.Equal(source, parsed.Source);
        Assert.Equal("Review " + label, Assert.Single(parsed.Headings).Title);
        Assert.Equal("Before " + label + " after.", MarkdownService.InlineText(parsed.Syntax.OfType<ParagraphBlock>().Single().Inline));
        Assert.Equal(2, parsed.Syntax.Descendants<AutolinkInline>().Count());
        var content = new ExportContent(parsed, new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>());
        using var stream = new MemoryStream(new DocxExporter().Export(content));
        using var word = WordprocessingDocument.Open(stream, false);
        Assert.Empty(new OpenXmlValidator().Validate(word));
        var main = word.MainDocumentPart!;
        Assert.Contains("Before " + label + " after.", main.Document!.InnerText);
        Assert.Contains(main.Document.Descendants<W.Run>(), r => r.InnerText == label && r.RunProperties?.Bold is not null);
        if (destination is null)
        {
            Assert.Empty(main.HyperlinkRelationships);
            Assert.Empty(main.Document.Descendants<W.Hyperlink>());
        }
        else
        {
            Assert.Equal(2, main.Document.Descendants<W.Hyperlink>().Count());
            Assert.All(main.HyperlinkRelationships, r => Assert.Equal(destination, r.Uri.AbsoluteUri));
            Assert.NotEmpty(main.HyperlinkRelationships);
        }
    }

    [Theory]
    [InlineData("https://example.org/a\nb")]
    [InlineData("https://example.org/a\tb")]
    [InlineData("file:///C:/private/readme.md")]
    [InlineData("javascript:example")]
    [InlineData("../document.md")]
    [InlineData("//example.org/path")]
    public void Exported_external_links_do_not_normalise_controls_or_enable_local_and_active_schemes(string reference)
        => Assert.Null(MarkdownLinks.ExternalUri(reference));
}
