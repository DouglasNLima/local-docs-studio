using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Core.Tests;

public sealed class WordHyperlinkImportTests
{
    [Fact]
    public void Exported_web_and_mail_links_reimport_with_formatted_labels_and_exact_targets()
    {
        const string source = "Before [**Café** and *review*](https://example.org/docs?q=a&b=2#part) after.\n\n- [Mail](mailto:review@example.org)\n\n  [Continuation](http://example.org/next)\n\n| Link |\n|---|\n| [Table](https://example.org/table) |";
        var bytes = new DocxExporter().Export(new(new MarkdownService().Parse(source), new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>()));
        var original = bytes.ToArray();
        var result = new DocumentImportService().ImportDocx(bytes);
        var parsed = new MarkdownService().Parse(result.Markdown);
        var links = parsed.Syntax.Descendants<LinkInline>().Where(l => !l.IsImage).ToArray();
        Assert.Equal(new[] { "https://example.org/docs?q=a&b=2#part", "mailto:review@example.org", "http://example.org/next", "https://example.org/table" }, links.Select(l => l.Url));
        Assert.Equal("Café and review", MarkdownService.InlineText(links[0]));
        Assert.Equal(new[] { 2, 1 }, links[0].Descendants<EmphasisInline>().Select(e => e.DelimiterCount));
        Assert.Equal(original, bytes);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Internal_word_links_resolve_to_retained_bookmark_targets()
    {
        const string source = "[Go](#target)\n\n## Target\n\n[Again](#target)";
        var bytes = new DocxExporter().Export(new(new MarkdownService().Parse(source), new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>()));
        var result = new DocumentImportService().ImportDocx(bytes);
        var parsed = new MarkdownService().Parse(result.Markdown);
        var links = parsed.Syntax.Descendants<LinkInline>().ToArray();
        Assert.Equal(2, links.Length);
        Assert.Equal(links[0].Url, links[1].Url);
        var target = DocumentAnchors.Find(parsed, links[0].Url![1..]);
        Assert.NotNull(target);
        Assert.Equal(parsed.Headings[0].SourceLine, target.SourceLine);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Link_label_punctuation_and_destination_parentheses_do_not_change_markdown_structure()
    {
        const string label = "Café [label] *literal* &copy; <tag>";
        var result = new DocumentImportService().ImportDocx(Package(main =>
        {
            var relationship = main.AddHyperlinkRelationship(new Uri("https://example.org/a(b)?x=&copy;"), true);
            return [new W.Paragraph(new W.Hyperlink(new W.Run(new W.Text(label))) { Id = relationship.Id })];
        }));
        var link = Assert.Single(new MarkdownService().Parse(result.Markdown).Syntax.Descendants<LinkInline>());
        Assert.Equal(label, MarkdownService.InlineText(link));
        Assert.Equal("https://example.org/a(b)?x=&copy;", Uri.UnescapeDataString(link.Url!));
        Assert.Empty(link.Descendants<EmphasisInline>());
        Assert.Empty(result.Warnings);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/private/launch.exe")]
    [InlineData("data:text/html,active")]
    [InlineData("ms-excel:ofe|u|https://example.org/workbook")]
    public void Unsupported_external_destinations_remain_literal_labels_with_a_warning(string destination)
    {
        var result = new DocumentImportService().ImportDocx(Package(main =>
        {
            var relationship = main.AddHyperlinkRelationship(new Uri(destination), true);
            return [new W.Paragraph(new W.Hyperlink(new W.Run(new W.Text("[Label](javascript:bad)"))) { Id = relationship.Id })];
        }));
        var parsed = new MarkdownService().Parse(result.Markdown);
        Assert.Empty(parsed.Syntax.Descendants<LinkInline>());
        Assert.Equal("[Label](javascript:bad)", MarkdownService.InlineText(Assert.IsType<ParagraphBlock>(Assert.Single(parsed.Syntax)).Inline));
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void Missing_relationships_and_bookmarks_are_reported_without_inventing_targets()
    {
        var result = new DocumentImportService().ImportDocx(Package(_ =>
        [new W.Paragraph(new W.Hyperlink(new W.Run(new W.Text("Missing relationship"))) { Id = "missing" }),
         new W.Paragraph(new W.Hyperlink(new W.Run(new W.Text("Missing bookmark"))) { Anchor = "missing" })]));
        Assert.Empty(new MarkdownService().Parse(result.Markdown).Syntax.Descendants<LinkInline>());
        Assert.Contains("Missing relationship", result.Markdown);
        Assert.Contains("Missing bookmark", result.Markdown);
        Assert.NotEmpty(result.Warnings);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unpaired_or_ambiguous_bookmarks_do_not_create_navigation_targets(bool duplicate)
    {
        var result = new DocumentImportService().ImportDocx(Package(_ =>
        {
            var paragraph = new W.Paragraph(new W.BookmarkStart { Name = "Target", Id = "1" }, new W.Run(new W.Text("Body")));
            if (duplicate) paragraph.Append(new W.BookmarkEnd { Id = "1" }, new W.BookmarkStart { Name = "Target", Id = "2" }, new W.BookmarkEnd { Id = "2" });
            return [new W.Paragraph(new W.Hyperlink(new W.Run(new W.Text("Go"))) { Anchor = "Target" }), paragraph];
        }));
        var parsed = new MarkdownService().Parse(result.Markdown);
        Assert.Empty(parsed.Syntax.Descendants<LinkInline>());
        Assert.Empty(parsed.Targets);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void Table_link_labels_escape_pipes_once_and_linked_images_keep_exact_bytes()
    {
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aX1cAAAAASUVORK5CYII=");
        const string source = "[![Picture](picture.png)](https://example.org/picture)\n\n| Link |\n|---|\n| [Pipe \\| label](https://example.org/table) |";
        var bytes = new DocxExporter().Export(new(new MarkdownService().Parse(source), new Dictionary<int, ExportImage>(),
            new Dictionary<string, ExportImage> { ["picture.png"] = new(png, 1, 1, "Picture") }));
        var result = new DocumentImportService().ImportDocx(bytes);
        var parsed = new MarkdownService().Parse(result.Markdown);
        var links = parsed.Syntax.Descendants<LinkInline>().ToArray();
        Assert.Equal(3, links.Length);
        Assert.Equal("https://example.org/picture", links[0].Url);
        Assert.True(links[1].IsImage);
        Assert.Same(links[0], links[1].Parent);
        Assert.Equal("Pipe | label", MarkdownService.InlineText(links[2]));
        var table = Assert.Single(parsed.Syntax.OfType<Markdig.Extensions.Tables.Table>());
        Assert.All(table.OfType<Markdig.Extensions.Tables.TableRow>(), row => Assert.Single(row));
        Assert.Equal(png, Assert.Single(result.Assets).Bytes);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Field_instructions_remain_inert_and_only_the_stored_result_is_imported()
    {
        var result = new DocumentImportService().ImportDocx(Package(_ => [new W.Paragraph(
            new W.SimpleField(new W.Run(new W.Text("Stored result"))) { Instruction = "HYPERLINK \"file:///C:/private/launch.exe\"" })]));
        Assert.Equal("Stored result", result.Markdown.Trim());
        Assert.Empty(new MarkdownService().Parse(result.Markdown).Syntax.Descendants<LinkInline>());
        Assert.Single(result.Warnings);
        Assert.Contains("not evaluated", result.Warnings[0]);
    }

    [Fact]
    public void Excessive_inline_nesting_is_rejected_without_visiting_link_targets()
    {
        var bytes = Package(main =>
        {
            var relationship = main.AddHyperlinkRelationship(new Uri("https://example.org/not-fetched"), true);
            OpenXmlElement content = new W.Run(new W.Text("Nested"));
            for (var depth = 0; depth < 70; depth++) content = new W.Hyperlink(content) { Id = relationship.Id };
            return [new W.Paragraph(content)];
        });
        Assert.Throws<InvalidDataException>(() => new DocumentImportService().ImportDocx(bytes));
    }

    [Fact]
    public void Relationship_identity_takes_precedence_over_an_internal_anchor()
    {
        var result = new DocumentImportService().ImportDocx(Package(main =>
        {
            var relationship = main.AddHyperlinkRelationship(new Uri("https://example.org/external"), true);
            return [new W.Paragraph(new W.Hyperlink(new W.Run(new W.Text("External"))) { Id = relationship.Id, Anchor = "Target" },
                new W.Hyperlink(new W.Run(new W.Text("Broken"))) { Id = "missing", Anchor = "Target" }),
                new W.Paragraph(new W.BookmarkStart { Name = "Target", Id = "1" }, new W.Run(new W.Text("Body")), new W.BookmarkEnd { Id = "1" })];
        }));
        var parsed = new MarkdownService().Parse(result.Markdown);
        Assert.Equal("https://example.org/external", Assert.Single(parsed.Syntax.Descendants<LinkInline>()).Url);
        Assert.Empty(parsed.Targets);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void Bookmark_names_cannot_become_markup_and_emphasis_keeps_surrounding_whitespace()
    {
        const string name = "\"/><script>active</script>";
        var result = new DocumentImportService().ImportDocx(Package(_ =>
        [new W.Paragraph(new W.Hyperlink(new W.Run(new W.RunProperties(new W.Bold()), new W.Text("  Go  "))) { Anchor = name }),
         new W.Paragraph(new W.BookmarkStart { Name = name, Id = "1" }, new W.Run(new W.Text("Body")), new W.BookmarkEnd { Id = "1" })]));
        var parsed = new MarkdownService().Parse(result.Markdown);
        var link = Assert.Single(parsed.Syntax.Descendants<LinkInline>());
        Assert.NotNull(DocumentAnchors.Find(parsed, link.Url![1..]));
        Assert.DoesNotContain("<script>", result.Markdown);
        Assert.Contains("[  **Go**  ]", result.Markdown);
        Assert.Empty(result.Warnings);
    }

    private static byte[] Package(Func<MainDocumentPart, OpenXmlElement[]> blocks)
    {
        using var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(blocks(main)));
        }
        return stream.ToArray();
    }
}
