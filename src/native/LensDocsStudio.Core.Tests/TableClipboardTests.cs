using AngleSharp.Html.Parser;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using Markdig.Extensions.Tables;
using Markdig.Syntax;

namespace LensDocsStudio.Core.Tests;

public sealed class TableClipboardTests
{
    [Fact]
    public void Selected_table_plain_clipboard_round_trip_preserves_newlines_tabs_quotes_and_empty_cells()
    {
        const string source = "| Nome | Notas | Vazio |\n|---|---|---|\n| Café | primeira&#10;segunda | |\n| Ação | disse &quot;Olá&quot;&#9;hoje | |";
        var parsed = new MarkdownService().Parse(source);
        var table = parsed.Syntax.Descendants<Table>().Single();
        var result = new ClipboardExporter().Export(new(parsed, new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>()),
            new(table.Span.Start, table.Span.Length));
        Assert.Equal("Nome\tNotas\tVazio\nCafé\t\"primeira\nsegunda\"\t\nAção\t\"disse \"\"Olá\"\"\thoje\"\t", result.PlainText);
        var imported = new LensDocsStudio.Core.Imports.ClipboardPasteService().Convert(new(result.PlainText, ""),
            LensDocsStudio.Core.Imports.PasteMode.Table);
        var roundTrip = new MarkdownService().Parse(imported.Document.Markdown).Syntax.Descendants<Table>().Single();
        Assert.Equal(3, roundTrip.Count);
        Assert.All(roundTrip.OfType<TableRow>(), row => Assert.Equal(3, row.Count));
        Assert.Contains("primeira<br>segunda", imported.Document.Markdown);
        Assert.Contains("disse \"Olá\"", imported.Document.Markdown);
        var exportedAgain = new ClipboardExporter().Export(new(new MarkdownService().Parse(imported.Document.Markdown),
            new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>()));
        Assert.Equal(result.PlainText, exportedAgain.PlainText);
        var exportedHtml = new HtmlParser().ParseDocument(HtmlClipboardFormat.Decode(exportedAgain.Html));
        Assert.Single(exportedHtml.QuerySelectorAll("tbody br"));
        Assert.Equal(source, parsed.Source);
    }

    [Fact]
    public void Selected_table_copy_excludes_surrounding_content_and_keeps_literal_Excel_cells()
    {
        const string source = "# Private introduction\n\n| Other | Data |\n|---|---|\n| Excluded | row |\n\n" +
            "| Label | Value |\n|---|---:|\n| **Café** | =1+1 |\n| Identifier | 000123 |\n\nPrivate conclusion.";
        var parsed = new MarkdownService().Parse(source); var table = parsed.Syntax.Descendants<Table>().Last();
        var content = new ExportContent(parsed, new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>());
        var exporter = new ClipboardExporter(); var before = exporter.Export(content);
        var result = exporter.Export(content, new(table.Span.Start, table.Span.Length));
        var html = new HtmlParser().ParseDocument(HtmlClipboardFormat.Decode(result.Html));
        Assert.Single(html.QuerySelectorAll("table")); Assert.Empty(html.QuerySelectorAll("h1"));
        Assert.DoesNotContain("Private", result.PlainText); Assert.DoesNotContain("Excluded", result.Html);
        Assert.Equal("Label\tValue\nCafé\t=1+1\nIdentifier\t000123", result.PlainText);
        Assert.Equal("Café", html.QuerySelector("strong")!.TextContent);
        Assert.True(html.QuerySelector("table")!.HasAttribute("x:str"));
        Assert.All(html.QuerySelectorAll("td,th"), cell => Assert.Contains("mso-number-format:\"\\@\";", cell.GetAttribute("style")));
        Assert.Equal(before, exporter.Export(content)); Assert.Equal(source, parsed.Source); Assert.Same(parsed.Syntax, table.Parent);
    }

    [Fact]
    public void Nested_table_copy_uses_external_reference_definitions_without_copying_their_source()
    {
        const string source = "Surrounding text.\n\n> | Picture | Link |\n> |---|---|\n> | ![Café][image] | [Guide][guide] |\n\n" +
            "[image]: diagram.png\n[guide]: https://example.com/guide\n\n![Unrelated](missing.png)";
        var parsed = new MarkdownService().Parse(source); var table = parsed.Syntax.Descendants<Table>().Single();
        var bytes = new byte[] { 1, 2, 3 };
        var content = new ExportContent(parsed, new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage> { ["diagram.png"] = new(bytes, 1, 1, "Café") });
        var result = new ClipboardExporter().Export(content, new(table.Span.Start, table.Span.Length));
        var html = new HtmlParser().ParseDocument(HtmlClipboardFormat.Decode(result.Html));
        Assert.Single(html.QuerySelectorAll("table")); Assert.Empty(html.QuerySelectorAll("blockquote"));
        Assert.Equal("data:image/png;base64,AQID", html.QuerySelector("img")!.GetAttribute("src"));
        Assert.Equal("https://example.com/guide", html.QuerySelector("a")!.GetAttribute("href"));
        Assert.DoesNotContain("Unrelated", result.Html); Assert.DoesNotContain("[guide]", result.Html);
        Assert.Contains("[Café]\tGuide", result.PlainText); Assert.IsType<QuoteBlock>(table.Parent);
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, 0)]
    [InlineData(int.MaxValue, 1)]
    [InlineData(0, int.MaxValue)]
    [InlineData(0, 5)]
    public void Invalid_or_stale_table_positions_do_not_copy_different_content(int start, int length)
    {
        var parsed = new MarkdownService().Parse("# Heading\n\n| Key | Value |\n|---|---|\n| Actual | selected |");
        Assert.Throws<IOException>(() => new ClipboardExporter().Export(new(parsed, new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>()), new(start, length)));
    }
}
