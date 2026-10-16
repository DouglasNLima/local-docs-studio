using AngleSharp.Html.Parser;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using LensDocsStudio.Core.Editing;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Core.Tests;

public sealed class ProgressSemanticsTests
{
    private static string Snippet(string value = "37", string maximum = "100", string label = "Review &amp; evidence", string colour = "blue")
        => $"<figure data-progress-bar data-progress-colour=\"{colour}\" onclick=\"run()\"><figcaption><span data-progress-label>{label}</span><span data-progress-value>{value}%</span></figcaption><progress max=\"{maximum}\" value=\"{value}\">{value}%</progress></figure>";
    private static ExportContent Content(string source) => new(new MarkdownService().Parse(source), new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>());

    [Fact] public void Progress_retains_decoded_label_value_colour_and_source_mapping_without_imported_attributes()
    {
        var snippet = Snippet(); var source = "# Review\r\n\r\n" + snippet + "\r\n\r\nAfter.";
        var content = Content(source); var progress = Assert.Single(content.Document.Syntax.Descendants<DocumentProgressBlock>());
        Assert.Equal("Review & evidence", progress.Label); Assert.Equal(37, progress.Value); Assert.Equal(100, progress.Maximum);
        Assert.Equal("blue", progress.Colour); Assert.Equal("Review & evidence: 37%", progress.Description);
        Assert.Equal(snippet, source[progress.Span.Start..(progress.Span.End + 1)]); Assert.Equal(2, progress.Line); Assert.Equal(source, content.Document.Source);
        var html = new HtmlParser().ParseDocument(new HtmlExporter().RenderFragment(content));
        Assert.Equal("Review & evidence: 37%", Assert.Single(html.QuerySelectorAll("figcaption")).TextContent);
        Assert.Equal("37", Assert.Single(html.QuerySelectorAll("progress")).GetAttribute("value")); Assert.Empty(html.QuerySelectorAll("[onclick]"));
        Assert.Contains("Review & evidence: 37%", new ClipboardExporter().Export(content).PlainText);
        Assert.Equal(source, new MarkdownClipboardExporter().Export(content).PlainText);
    }

    [Theory]
    [InlineData("0", "100", "0%")]
    [InlineData("100", "100", "100%")]
    [InlineData("-1", "100", "0%")]
    [InlineData("150", "100", "100%")]
    [InlineData("1", "8", "12.5%")]
    public void Finite_progress_values_follow_native_HTML_bounds(string value, string maximum, string expected)
    {
        var progress = Assert.Single(Content(Snippet(value, maximum, colour: "unknown")).Document.Syntax.Descendants<DocumentProgressBlock>());
        Assert.Equal(expected, progress.PercentageText); Assert.Equal("accent", progress.Colour);
    }

    [Theory]
    [InlineData("NaN", "100")]
    [InlineData("Infinity", "100")]
    [InlineData("20", "0")]
    [InlineData("20", "-1")]
    [InlineData("20", "Infinity")]
    public void Invalid_progress_numbers_remain_visible_as_source(string value, string maximum)
    {
        var content = Content(Snippet(value, maximum));
        Assert.Empty(content.Document.Syntax.Descendants<DocumentProgressBlock>()); Assert.Single(content.Document.Syntax.Descendants<HtmlBlock>());
        Assert.Contains("&lt;figure", new HtmlExporter().RenderFragment(content));
    }

    [Fact] public void Active_unknown_incomplete_oversized_and_code_examples_are_not_interpreted_as_progress()
    {
        foreach (var snippet in new[] { Snippet(label: "<script>run()</script>"), Snippet(label: "A<!-- hidden -->B"), Snippet(label: new string('x', 513)),
            Snippet().Replace("data-progress-bar", "title=\"data-progress-bar\""), Snippet().Replace("</figure>", ""), Snippet() + "<p>Keep me</p>",
            Snippet().Replace("onclick=\"run()\"", "title=\"" + new string('x', 17000) + "\""), "```html\n" + Snippet() + "\n```" })
        {
            var content = Content(snippet); Assert.Empty(content.Document.Syntax.Descendants<DocumentProgressBlock>()); Assert.Equal(snippet, content.Document.Source);
        }
    }

    [Fact] public void Word_progress_keeps_percentage_label_bar_dimensions_and_schema_valid_structure()
    {
        using var package = WordprocessingDocument.Open(new MemoryStream(new DocxExporter().Export(Content(Snippet() + "\n\n" + Snippet("0") + "\n\n" + Snippet("100")))), false);
        Assert.Empty(new OpenXmlValidator().Validate(package));
        var body = package.MainDocumentPart!.Document!.Body!; Assert.Contains("Review & evidence: 37%", body.InnerText); Assert.DoesNotContain("<figure", body.InnerText);
        var tables = body.Elements<W.Table>().ToArray(); Assert.Equal(3, tables.Length);
        Assert.Equal(new[] { "1850", "3150" }, tables[0].Descendants<W.TableCellWidth>().Select(w => w.Width!.Value));
        Assert.Equal(new[] { "2563EB", "EDEDED" }, tables[0].Descendants<W.Shading>().Select(s => s.Fill!.Value));
        Assert.Single(tables[1].Descendants<W.TableCell>()); Assert.Single(tables[2].Descendants<W.TableCell>());
    }

    [Fact] public void Native_edit_plan_inserts_escaped_portable_markup_and_edits_exactly_one_existing_indicator()
    {
        const string source = "Before\r\n\r\nselected\r\n\r\nAfter";
        var edit = ProgressEditService.Plan(new MarkdownService().Parse(source), source.IndexOf("selected", StringComparison.Ordinal), 8, new("<Review> & evidence", 65, "green"));
        var inserted = source.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.Replacement);
        Assert.StartsWith("Before\r\n\r\n<figure", inserted); Assert.EndsWith("</figure>\r\n\r\nAfter", inserted);
        Assert.DoesNotContain("\n", inserted.Replace("\r\n", "")); Assert.Contains("&lt;Review&gt; &amp; evidence", inserted);
        var document = new MarkdownService().Parse(inserted); var block = Assert.Single(document.Syntax.Descendants<DocumentProgressBlock>());
        Assert.Equal("<Review> & evidence", block.Label); Assert.Equal(65, block.Value);
        var update = ProgressEditService.Plan(document, block.Span.Start + 5, 0, new("Ready", 100, "neutral"));
        var updated = inserted.Remove(update.Start, update.Length).Insert(update.Start, update.Replacement);
        Assert.Equal(inserted[..block.Span.Start], updated[..update.Start]); Assert.EndsWith("\r\n\r\nAfter", updated);
        Assert.Equal("Ready: 100%", Assert.Single(new MarkdownService().Parse(updated).Syntax.Descendants<DocumentProgressBlock>()).Description);
        Assert.Equal(update.Replacement, updated.Substring(update.SelectionStart, update.SelectionLength));
        Assert.Null(ProgressEditService.Find(document, 0, inserted.Length));
    }

    [Fact] public void Progress_edit_rejects_invalid_input_and_never_changes_code_examples()
    {
        var document = new MarkdownService().Parse("```html\n" + Snippet() + "\n```");
        Assert.Null(ProgressEditService.Find(document, 20, 0));
        foreach (var options in new[] { new ProgressOptions(""), new("Label", -1), new("Label", 101), new("Label", 50, "url(remote)"), new(new string('x', 513)) })
            Assert.Throws<ArgumentException>(() => ProgressEditService.Plan(document, 0, 0, options));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProgressEditService.Plan(document, -1, 0, new()));
    }
}
