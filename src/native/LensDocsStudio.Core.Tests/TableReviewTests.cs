using LensDocsStudio.Core.Review;

namespace LensDocsStudio.Core.Tests;

public sealed class TableReviewTests
{
    [Theory]
    [InlineData("| A | B |\n|---|---|\n| Only |", 3, "Table row has 1 cell; its header has 2.")]
    [InlineData("| A | B |\n|---|---|\n| One | Two | Extra |", 3, "Table row has 3 cells; its header has 2.")]
    [InlineData("| A | B |\n|---|---|---|\n| One | Two |", 2, "Table separator has 3 cells; its header has 2.")]
    [InlineData("| A | B |\n|-|-|\n| One | Two |", 2, "Markdown table separator row is invalid.")]
    [InlineData("A | B\n:--- | ---:\nOne | Two | Three", 3, "Table row has 3 cells; its header has 2.")]
    public void Table_review_uses_original_rows_including_invalid_or_normalised_tables(string table, int relativeLine, string expected)
    {
        var source = "# Table\r\n\r\n" + table.Replace("\n", "\r\n", StringComparison.Ordinal);
        var issue = Assert.Single(new DocumentReviewService().Analyse(source).Issues);
        Assert.Equal("Tables", issue.Rule); Assert.Equal(expected, issue.Message); Assert.Equal(relativeLine + 2, issue.Line); Assert.Equal(1, issue.Column);
        Assert.Equal(table.Split('\n')[relativeLine - 1], source.Substring(issue.Start, issue.Length));
    }

    [Theory]
    [InlineData("| A | B |\n|:---|---:|\n| Escaped \\| pipe | Value |")]
    [InlineData("A | B\n--- | ---\nEscaped \\| pipe | Value")]
    [InlineData("| A | B |\n|---|---|\n| `a\\|b` | Value |")]
    [InlineData("| A | B |\n|---|---|\n| | |")]
    [InlineData("| A |\n|---|\n| Value |")]
    [InlineData("| A | B |\n|---|---|\nValue | Trailing\\|")]
    public void Table_review_retains_escaped_pipes_alignment_empty_cells_and_optional_outer_delimiters(string table) =>
        Assert.Empty(new DocumentReviewService().Analyse("# Table\n\n" + table).Issues);

    [Theory]
    [InlineData("> | A | B |\n> |---|---|\n> | Extra | Cell | Here |")]
    [InlineData("- | A | B |\n  |---|---|\n  | Extra | Cell | Here |")]
    [InlineData("1. | A | B |\n   |---|---|\n   | Extra | Cell | Here |")]
    [InlineData("- > | A | B |\n  > |---|---|\n  > | Extra | Cell | Here |")]
    public void Table_review_preserves_original_quote_and_list_source_positions(string table)
    {
        var source = "# Nested\n\n" + table;
        var issue = Assert.Single(new DocumentReviewService().Analyse(source).Issues);
        Assert.Equal("Tables", issue.Rule); Assert.Equal(table.Split('\n')[2], source.Substring(issue.Start, issue.Length));
    }

    [Fact]
    public void Table_review_skips_code_HTML_and_front_matter()
    {
        const string source = "---\ntitle: ignored\n| A | B |\n|-|-|\n---\n# Safe\n\n```text\n| A | B |\n|-|-|\n```\n\n" +
            "    | A | B |\n    |-|-|\n\n<div>\n| A | B |\n|-|-|\n</div>\n\n:::mermaid\n| A | B |\n|-|-|\n:::";
        Assert.Empty(new DocumentReviewService().Analyse(source).Issues);
    }

    [Fact]
    public void Table_review_stops_at_paragraph_boundaries_and_does_not_treat_pipe_prose_as_a_table()
    {
        const string source = "# Notes\n\nUse A | B to compare.\nOrdinary paragraph.\n\n| A | B |\n|---|---|\n| One | Two |\n\nSeparate | prose | with | pipes.";
        Assert.Empty(new DocumentReviewService().Analyse(source).Issues);
    }
}
