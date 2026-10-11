using AngleSharp.Html.Parser;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Editing;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Core.Tests;

public sealed class AnchorTests
{
    private const string Source = "# Overview\r\n\r\n[Named](#review) [Empty](#empty) [Heading](#overview)\r\n\r\nBefore <span id=review data-doc-anchor onclick='run()' style='color:red'>Café &amp; **review**</span> after.\r\n\r\n<span id=empty data-doc-anchor></span>Following text.";
    private static ExportContent Content(string source) => new(new MarkdownService().Parse(source), new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>());

    [Fact] public void Named_and_empty_anchors_preserve_source_and_decoded_selectable_labels()
    {
        var parsed = new MarkdownService().Parse(Source);
        var anchors = parsed.Syntax.Descendants<DocumentAnchorInline>().ToArray();
        Assert.Equal(2, anchors.Length); Assert.Equal("Café & review", anchors[0].Text); Assert.Empty(anchors[1].Text);
        Assert.Equal(new[] { "overview", "review", "empty" }, parsed.Targets.Select(t => t.Id));
        foreach (var anchor in anchors)
        {
            var target = DocumentAnchors.Find(parsed, anchor.Id)!;
            Assert.Equal(anchor.Span.Start, target.SourceStart); Assert.Equal(anchor.Line, target.SourceLine);
            Assert.StartsWith("<span id=", Source[anchor.Span.Start..(anchor.Span.End + 1)]);
        }
        Assert.Equal(Source, parsed.Source);
        Assert.Contains("Before Café & review after.", string.Join("\n", parsed.Syntax.OfType<ParagraphBlock>().Select(p => MarkdownService.InlineText(p.Inline))));
    }

    [Fact] public void Duplicate_targets_are_consistent_across_native_HTML_and_Docs_Site_navigation()
    {
        var content = Content("# Review {#review}\n\n<span id=review data-doc-anchor>First</span>\n\n<span id=review data-doc-anchor>Second</span>\n\n## Again {#review}");
        var expected = new[] { "review", "review-2", "review-3", "review-4" };
        Assert.Equal(expected, content.Document.Targets.Select(t => t.Id));
        var html = new HtmlParser().ParseDocument(new HtmlExporter().RenderFragment(content));
        Assert.Equal(expected, html.QuerySelectorAll("[id]").Select(e => e.Id));
        var site = new DocsSiteExporter().PreparePage("README.md", content);
        Assert.Equal(new[] { "review", "review-4" }, site.Headings.Select(h => h.Id));
        Assert.Contains("id=\"review-2\"", site.Html);
    }

    [Theory]
    [InlineData("<span data-doc-anchor>No identifier</span>")]
    [InlineData("<span id='invalid id' data-doc-anchor>Spaces</span>")]
    [InlineData("<span id=review data-doc-anchor><img src=x></span>")]
    [InlineData("<span id=review data-doc-anchor><script>run()</script></span>")]
    [InlineData("<span id=review data-doc-anchor><!-- hidden -->Label</span>")]
    [InlineData("<span id=review data-doc-anchor>[Keep link](doc.md)</span>")]
    [InlineData("<span id=review data-doc-anchor data-status-badge>Ambiguous</span>")]
    [InlineData("<span id=review title=data-doc-anchor>Unrecognised</span>")]
    [InlineData("<span id=review data-doc-anchor>Unclosed")]
    public void Unsupported_anchor_structures_remain_literal_source(string snippet)
    {
        var parsed = new MarkdownService().Parse("Before " + snippet);
        Assert.Empty(parsed.Syntax.Descendants<DocumentAnchorInline>()); Assert.Empty(parsed.Targets);
        Assert.Equal("Before " + snippet, parsed.Source);
    }

    [Fact] public void Generated_suffixes_preserve_later_declared_identifiers_and_bounded_anchor_lengths()
    {
        var longest = new string('a', 128);
        var parsed = new MarkdownService().Parse("# Review {#review}\n\n<span id=review data-doc-anchor>Duplicate</span>\n\n<span id=review-2 data-doc-anchor>Declared later</span>\n\n"
            + $"<span id={longest} data-doc-anchor>First long</span>\n\n<span id={longest} data-doc-anchor>Second long</span>");
        Assert.Equal(new[] { "review", "review-3", "review-2", longest, new string('a', 126) + "-2" }, parsed.Targets.Select(t => t.Id));
        Assert.All(parsed.Syntax.Descendants<DocumentAnchorInline>(), a => Assert.True(DocumentAnchors.IsValidId(a.Id)));
    }

    [Fact] public void Code_examples_and_oversized_identifiers_are_not_navigation_targets()
    {
        var source = "`<span id=example data-doc-anchor></span>`\n\n```html\n<span id=fenced data-doc-anchor>Example</span>\n```\n\n"
            + "Before <span data-doc-anchor id='" + new string('a', 129) + "'>Too long</span>";
        Assert.Empty(new MarkdownService().Parse(source).Targets);
    }

    [Fact] public void HTML_and_clipboard_preserve_targets_and_labels_without_imported_attributes()
    {
        var content = Content(Source); var html = new HtmlParser().ParseDocument(new HtmlExporter().RenderFragment(content));
        Assert.Equal("Café & review", html.GetElementById("review")!.TextContent); Assert.Empty(html.GetElementById("empty")!.TextContent);
        Assert.Empty(html.QuerySelectorAll("[onclick],[style],[data-doc-anchor]"));
        Assert.Equal("#review", html.QuerySelector("a")!.GetAttribute("href"));
        Assert.Equal(Source, new MarkdownClipboardExporter().Export(content).PlainText);
        Assert.Contains("Café & review", new ClipboardExporter().Export(content).PlainText);
    }

    [Fact] public void Word_bookmarks_and_local_links_resolve_to_the_same_named_and_empty_targets()
    {
        using var package = WordprocessingDocument.Open(new MemoryStream(new DocxExporter().Export(Content(Source))), false);
        Assert.Empty(new OpenXmlValidator().Validate(package));
        var document = package.MainDocumentPart!.Document!;
        var bookmarks = document.Descendants<W.BookmarkStart>().ToArray(); Assert.Equal(3, bookmarks.Length);
        Assert.Equal(bookmarks.Select(b => b.Id!.Value).Order(), document.Descendants<W.BookmarkEnd>().Select(b => b.Id!.Value).Order());
        var names = bookmarks.Select(b => b.Name!.Value).ToHashSet();
        var links = document.Descendants<W.Hyperlink>().ToArray(); Assert.Equal(3, links.Length);
        Assert.All(links, l => Assert.Contains(l.Anchor!.Value, names));
        Assert.Contains("Café & review", document.InnerText); Assert.DoesNotContain("<span", document.InnerText);
    }

    [Fact] public void Insert_and_label_updates_preserve_surrounding_source_and_literal_punctuation()
    {
        const string original = "Before selected after\r\n";
        const string label = "[Literal] *label* <ready> & `key` \\ + ++ ~ ^ = $ | !";
        var edit = AnchorEditService.Plan(new MarkdownService().Parse(original), 7, 8, new("révision", label));
        var inserted = original.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.Replacement);
        Assert.StartsWith("Before <span", inserted); Assert.EndsWith("</span> after\r\n", inserted);
        var parsed = new MarkdownService().Parse(inserted);
        var anchor = Assert.Single(parsed.Syntax.Descendants<DocumentAnchorInline>());
        Assert.Equal(label, anchor.Text); Assert.Equal("révision", anchor.Id);
        var update = AnchorEditService.Plan(parsed, edit.SelectionStart, edit.SelectionLength, new("révision", ""));
        var updated = inserted.Remove(update.Start, update.Length).Insert(update.Start, update.Replacement);
        Assert.Equal("Before <span id=\"r&#233;vision\" data-doc-anchor></span> after\r\n", updated);
        Assert.Empty(Assert.Single(new MarkdownService().Parse(updated).Syntax.Descendants<DocumentAnchorInline>()).Text);
    }

    [Fact] public void Word_navigation_does_not_reuse_retained_template_bookmark_names_or_identifiers()
    {
        using var template = new MemoryStream();
        using (var package = WordprocessingDocument.Create(template, DocumentFormat.OpenXml.WordprocessingDocumentType.Document, true))
        {
            var main = package.AddMainDocumentPart(); var header = main.AddNewPart<HeaderPart>();
            header.Header = new W.Header(new W.Paragraph(new W.BookmarkStart { Id = "1", Name = "LensTarget1" }, new W.Run(new W.Text("Retained header")), new W.BookmarkEnd { Id = "1" }));
            main.Document = new W.Document(new W.Body(new W.SectionProperties(new W.HeaderReference { Id = main.GetIdOfPart(header), Type = W.HeaderFooterValues.Default })));
        }
        using var exported = WordprocessingDocument.Open(new MemoryStream(new DocxExporter().Export(Content(Source), template.ToArray())), false);
        Assert.Empty(new OpenXmlValidator().Validate(exported));
        var starts = exported.MainDocumentPart!.Document!.Descendants<W.BookmarkStart>().ToArray();
        Assert.All(starts, b => { Assert.NotEqual("1", b.Id!.Value); Assert.NotEqual("LensTarget1", b.Name!.Value); });
        Assert.Contains("Retained header", Assert.Single(exported.MainDocumentPart.HeaderParts).Header!.InnerText);
    }

    [Fact] public void Inserting_duplicate_heading_or_anchor_identifiers_does_not_produce_an_edit()
    {
        var parsed = new MarkdownService().Parse(Source);
        foreach (var id in new[] { "overview", "review", "empty" })
            Assert.Throws<ArgumentException>(() => AnchorEditService.Plan(parsed, 0, 0, new(id, "Duplicate")));
        var anchor = parsed.Syntax.Descendants<DocumentAnchorInline>().First();
        Assert.Throws<ArgumentException>(() => AnchorEditService.Plan(parsed, anchor.Span.Start, 0, new("renamed", "Would break links")));
    }

    [Theory]
    [InlineData("invalid id", "Label")]
    [InlineData("-invalid", "Label")]
    [InlineData("", "Label")]
    [InlineData("valid", "Label\nAnother line")]
    public void Invalid_anchor_options_are_refused(string id, string label) =>
        Assert.Throws<ArgumentException>(() => AnchorEditService.Plan(new MarkdownService().Parse("Keep original"), 0, 0, new(id, label)));
}
