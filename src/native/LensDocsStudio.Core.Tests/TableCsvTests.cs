using System.Text;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using Markdig.Extensions.Tables;
using Markdig.Syntax;

namespace LensDocsStudio.Core.Tests;

public sealed class TableCsvTests
{
    [Fact]
    public void Csv_keeps_selected_table_order_Unicode_empty_cells_and_literal_values_without_a_BOM()
    {
        const string source = "# Private\n\n| Other | Table |\n|---|---|\n| Excluded | row |\n\n" +
            "| Label | Value |\n|---|---|\n| **Café 😀** | =1+1 |\n| Identifier | 000123 |\n| Empty | |\n| | |\n\nPrivate conclusion.";
        var result = Export(source, 1);
        Assert.Equal(2, result.TableNumber);
        Assert.Equal(Encoding.UTF8.GetBytes("Label,Value\r\nCafé 😀,=1+1\r\nIdentifier,000123\r\nEmpty,\r\n,"), result.Bytes);
    }

    [Fact]
    public void Csv_quotes_commas_quotes_and_embedded_newlines_without_changing_cell_text()
    {
        const string source = "| Item | Value |\n|---|---|\n| a,b | say &quot;hello&quot; |\n| Lines | first&#10;second |\n| White space | A&nbsp;B&#13;&#10;  C |";
        Assert.Equal("Item,Value\r\n\"a,b\",\"say \"\"hello\"\"\"\r\nLines,\"first\nsecond\"\r\nWhite space,\"A B\nC\"", Text(source));
    }

    [Fact]
    public void Csv_retains_passive_labels_code_links_and_image_alt_text_without_opening_resources()
    {
        const string source = "| Label | Value |\n|---|---|\n| <span data-status-badge data-status-kind=\"ready\">Ready &amp; tested</span> | `a,b` |\n" +
            "| <span data-doc-anchor id=\"target\">Named</span> | [Guide][guide] |\n| <kbd>Ctrl</kbd> | ![Café image][image] |\n" +
            "| Link | <https://example.invalid/guide> |\n\n[guide]: https://example.invalid/never-opened\n[image]: ../missing-image.png";
        Assert.Equal("Label,Value\r\nReady & tested,\"a,b\"\r\nNamed,Guide\r\nCtrl,Café image\r\nLink,https://example.invalid/guide", Text(source));
    }

    [Fact]
    public void Csv_keeps_unknown_HTML_as_inert_visible_source()
    {
        const string source = "| Label | Value |\n|---|---|\n| HTML | <b onclick=\"run()\">literal</b> |";
        Assert.Equal("Label,Value\r\nHTML,\"<b onclick=\"\"run()\"\">literal</b>\"", Text(source));
    }

    [Fact]
    public void Csv_supports_nested_tables_without_including_quote_markers_or_surrounding_source()
    {
        const string source = "Excluded.\n\n> | Label | Value |\n> |---|---|\n> | **Nested** | `000123` |\n\nExcluded end.";
        Assert.Equal("Label,Value\r\nNested,000123", Text(source));
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 0)]
    [InlineData(int.MaxValue, 1)]
    [InlineData(0, 5)]
    public void Csv_refuses_invalid_or_stale_table_positions(int start, int length) =>
        Assert.Throws<IOException>(() => new TableCsvExporter().Export("# Heading\n\n| A | B |\n|---|---|\n| 1 | 2 |", new(start, length)));

    [Fact]
    public void Csv_checks_source_size_before_parsing() =>
        Assert.Throws<IOException>(() => new TableCsvExporter().Export(new string('x', TableCsvExporter.MaximumSourceCharacters + 1), new(0, 1)));

    [Fact]
    public void Csv_refuses_excessive_cells_instead_of_truncating_output()
    {
        var source = "| A | B |\n|---|---|\n" + string.Concat(Enumerable.Repeat("| 1 | 2 |\n", TableCsvExporter.MaximumCells / 2));
        Assert.Throws<IOException>(() => Export(source));
    }

    [Fact]
    public void Csv_observes_cancellation_before_parsing()
    {
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => new TableCsvExporter().Export("| A | B |\n|---|---|", new(0, 1), cancelled.Token));
    }

    private static string Text(string source) => Encoding.UTF8.GetString(Export(source).Bytes);
    private static ExportedTableCsv Export(string source, int tableIndex = 0)
    {
        var table = new MarkdownService().Parse(source).Syntax.Descendants<Table>().ElementAt(tableIndex);
        return new TableCsvExporter().Export(source, new(table.Span.Start, table.Span.Length));
    }
}
