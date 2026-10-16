using LensDocsStudio.Core.Editing;
using LensDocsStudio.Core.Parsing;
using Markdig.Extensions.Tables;
using Markdig.Syntax;

namespace LensDocsStudio.Core.Tests;

public sealed class TableEditTests
{
    private static string Apply(string source, MarkdownEdit edit) => source.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.Replacement);
    [Theory] [InlineData("\n")] [InlineData("\r\n")]
    public void Opening_and_applying_an_unchanged_table_preserves_exact_spacing_escapes_and_line_endings(string newline)
    {
        var source = "Before\n\n| Name  | Value |\n|:-----|-----:|\n| **Café** | `a\\|b` and C:\\work |\n\nAfter".Replace("\n", newline);
        var session = TableEditService.Open(new MarkdownService().Parse(source), source.IndexOf("Café", StringComparison.Ordinal), 0);
        Assert.True(session.IsExisting); Assert.Equal(2, session.Grid.RowCount); Assert.Equal(2, session.Grid.ColumnCount);
        Assert.Equal("`a\\|b` and C:\\work", session.Grid.Cell(1, 1));
        Assert.Equal(TableColumnAlignment.Left, session.Grid.Alignment(0)); Assert.Equal(TableColumnAlignment.Right, session.Grid.Alignment(1));
        Assert.Equal(source, Apply(source, session.Plan(session.Grid)));
    }
    [Theory] [InlineData("\n")] [InlineData("\r\n")]
    public void A_cell_edit_preserves_surrounding_documents_links_markers_and_alignment(string newline)
    {
        var source = "# Before\n\n| Name | Value |\n|:---:|---:|\n| [guide][g] | **Old** |\n\n[g]: docs/guide.md\n\nAfter".Replace("\n", newline);
        var parsed = new MarkdownService().Parse(source); var original = Assert.Single(parsed.Syntax.Descendants<Table>());
        var session = TableEditService.Open(parsed, source.IndexOf("Old", StringComparison.Ordinal), 0); session.Grid.SetCell(1, 1, "**New Ω**");
        var edit = session.Plan(session.Grid); var changed = Apply(source, edit);
        Assert.Equal(source[..original.Span.Start], changed[..edit.Start]); Assert.EndsWith(source[(original.Span.End + 1)..], changed);
        Assert.Equal(edit.Replacement, changed.Substring(edit.SelectionStart, edit.SelectionLength));
        var table = Assert.Single(new MarkdownService().Parse(changed).Syntax.Descendants<Table>());
        Assert.Equal(TableColumnAlign.Center, table.ColumnDefinitions[0].Alignment); Assert.Equal(TableColumnAlign.Right, table.ColumnDefinitions[1].Alignment);
        Assert.Equal("docs/guide.md", Assert.Single(MarkdownLinks.Read(changed)).Reference);
        if (newline == "\r\n") Assert.DoesNotContain("\n", changed.Replace("\r\n", ""));
    }
    [Theory] [InlineData("> ", "> ")] [InlineData("- ", "  ")] [InlineData("1. ", "   ")]
    public void Nested_table_edits_preserve_quote_or_list_containers(string firstPrefix, string continuation)
    {
        var source = firstPrefix + "| A | B |\n" + continuation + "|---|---:|\n" + continuation + "| one | two |\n\nAfter";
        var session = TableEditService.Open(new MarkdownService().Parse(source), source.IndexOf("two", StringComparison.Ordinal), 0);
        Assert.True(session.IsExisting); session.Grid.SetCell(1, 1, "updated"); session.Grid.InsertRow(1); session.Grid.SetCell(2, 0, "added");
        var changed = Apply(source, session.Plan(session.Grid));
        Assert.StartsWith(firstPrefix, changed); Assert.Contains(continuation + "| added |  |", changed); Assert.EndsWith("\n\nAfter", changed);
        var table = Assert.Single(new MarkdownService().Parse(changed).Syntax.Descendants<Table>());
        Assert.Equal(3, table.Count); Assert.False(table.Parent is MarkdownDocument);
    }
    [Fact]
    public void Added_raw_pipes_are_escaped_without_double_escaping_existing_Markdown()
    {
        const string source = "| A | B |\n|---|---|\n| first | last |";
        var session = TableEditService.Open(new MarkdownService().Parse(source), 2, 0);
        session.Grid.SetCell(1, 0, "a|b and `c\\|d` and C:\\work");
        var changed = Apply(source, session.Plan(session.Grid));
        Assert.Contains("a\\|b and `c\\|d` and C:\\work", changed);
        var reopened = TableEditService.Open(new MarkdownService().Parse(changed), changed.IndexOf("a\\|b", StringComparison.Ordinal), 0);
        Assert.Equal(2, reopened.Grid.ColumnCount); Assert.Equal(changed, Apply(changed, reopened.Plan(reopened.Grid)));
    }
    [Fact]
    public void Row_and_column_operations_move_complete_values_and_alignment_without_moving_the_header()
    {
        var grid = new TableEditGrid(new[] { new[] { "A", "B" }, new[] { "one", "two" }, new[] { "three", "four" } },
            new[] { TableColumnAlignment.Left, TableColumnAlignment.Right });
        grid.MoveRow(2, -1); Assert.Equal("A", grid.Cell(0, 0)); Assert.Equal("three", grid.Cell(1, 0));
        grid.MoveColumn(0, 1); Assert.Equal("B", grid.Cell(0, 0)); Assert.Equal("four", grid.Cell(1, 0)); Assert.Equal(TableColumnAlignment.Right, grid.Alignment(0));
        grid.InsertRow(1); grid.SetCell(2, 1, "New row"); grid.InsertColumn(0); grid.SetCell(1, 1, "New column");
        Assert.Equal(4, grid.RowCount); Assert.Equal(3, grid.ColumnCount); Assert.Equal("New row", grid.Cell(2, 2));
        grid.RemoveRow(2); grid.RemoveColumn(1); Assert.Equal("four", grid.Cell(1, 0)); Assert.Equal(3, grid.RowCount);
        var before = grid.Copy(); Assert.Throws<ArgumentException>(() => grid.RemoveRow(0)); Assert.Throws<ArgumentException>(() => grid.MoveRow(1, -1));
        Assert.Throws<ArgumentException>(() => grid.MoveColumn(0, -1)); Assert.True(grid.SameAs(before));
    }
    [Theory] [InlineData("\n")] [InlineData("\r\n")]
    public void New_tables_replace_only_the_reviewed_selection_and_use_document_line_endings(string newline)
    {
        var source = "Before\n\nselected\n\nAfter".Replace("\n", newline); var start = source.IndexOf("selected", StringComparison.Ordinal);
        var session = TableEditService.Open(new MarkdownService().Parse(source), start, 8); Assert.False(session.IsExisting);
        session.Grid.SetCell(1, 0, "Café Ω"); var edit = session.Plan(session.Grid); var changed = Apply(source, edit);
        Assert.StartsWith("Before" + newline + newline, changed); Assert.EndsWith(newline + newline + "After", changed);
        Assert.Equal(3, Assert.Single(new MarkdownService().Parse(changed).Syntax.Descendants<Table>()).Count);
        Assert.Equal(edit.Replacement.Trim('\r', '\n'), changed.Substring(edit.SelectionStart, edit.SelectionLength));
        if (newline == "\r\n") Assert.DoesNotContain("\n", changed.Replace("\r\n", ""));
    }
    [Fact]
    public void Fenced_examples_are_not_mistaken_for_an_existing_table()
    {
        const string source = "```text\n| A | B |\n|---|---|\n| one | two |\n```";
        Assert.False(TableEditService.Open(new MarkdownService().Parse(source), source.IndexOf("one", StringComparison.Ordinal), 0).IsExisting);
    }
    [Fact]
    public void An_overlapping_selection_cannot_silently_replace_part_of_a_table()
    {
        const string source = "Before\n\n| A | B |\n|---|---|\n| one | two |\n\nAfter";
        Assert.Throws<ArgumentException>(() => TableEditService.Open(new MarkdownService().Parse(source), 0, source.IndexOf("two", StringComparison.Ordinal)));
    }
    [Fact]
    public void Existing_short_rows_are_padded_but_extra_source_cells_are_never_discarded()
    {
        const string shortRow = "| A | B |\n|---|---|\n| one |";
        var session = TableEditService.Open(new MarkdownService().Parse(shortRow), shortRow.IndexOf("one", StringComparison.Ordinal), 0);
        Assert.Equal("", session.Grid.Cell(1, 1)); Assert.Equal(shortRow, Apply(shortRow, session.Plan(session.Grid)));
        const string extra = "| A | B |\n|---|---|\n| one | two | third |";
        Assert.Throws<ArgumentException>(() => TableEditService.Open(new MarkdownService().Parse(extra), extra.IndexOf("one", StringComparison.Ordinal), 0));
    }
    [Fact]
    public void Draft_copies_and_rejected_cell_changes_leave_original_values_intact()
    {
        var original = TableEditGrid.Create(); var copy = original.Copy(); copy.SetCell(1, 0, "private edit"); Assert.Equal("", original.Cell(1, 0));
        Assert.Throws<ArgumentException>(() => copy.SetCell(1, 0, "split\nrow"));
        Assert.Throws<ArgumentException>(() => copy.SetCell(1, 0, new string('x', TableEditGrid.MaximumCellCharacters + 1)));
        Assert.Equal("private edit", copy.Cell(1, 0));
        Assert.Throws<ArgumentException>(() => new TableEditGrid(new[] { new[] { "A" }, new[] { "B", "C" } }, new[] { TableColumnAlignment.Default }));
    }
    [Fact]
    public void A_header_only_table_and_single_column_remain_valid_without_forced_content()
    {
        const string source = "| A |\n|---|";
        var session = TableEditService.Open(new MarkdownService().Parse(source), 2, 0); Assert.Equal(1, session.Grid.RowCount);
        Assert.Equal(source, Apply(source, session.Plan(session.Grid))); session.Grid.InsertRow(0);
        Assert.Equal(2, Assert.Single(new MarkdownService().Parse(Apply(source, session.Plan(session.Grid))).Syntax.Descendants<Table>()).Count);
        Assert.Throws<ArgumentException>(() => session.Grid.RemoveColumn(0)); Assert.Throws<ArgumentException>(() => session.Grid.RemoveRow(1));
    }
    [Fact]
    public void Row_and_column_growth_at_the_cell_budget_preserves_the_complete_existing_draft()
    {
        var grid = new TableEditGrid(Enumerable.Range(0, 512).Select(_ => Enumerable.Repeat("", 128)), Enumerable.Repeat(TableColumnAlignment.Default, 128));
        grid.SetCell(511, 127, "Last visible source");
        Assert.Throws<ArgumentException>(() => grid.InsertRow(511)); Assert.Throws<ArgumentException>(() => grid.InsertColumn(127));
        Assert.Equal(512, grid.RowCount); Assert.Equal(128, grid.ColumnCount); Assert.Equal("Last visible source", grid.Cell(511, 127));
    }
    [Fact]
    public void Character_budget_rejection_preserves_old_values_and_does_not_add_a_partial_column()
    {
        var text = new string('x', 15625);
        var grid = new TableEditGrid(new[] { Enumerable.Repeat(text, 32), Enumerable.Repeat(text, 32) }, Enumerable.Repeat(TableColumnAlignment.Left, 32));
        Assert.Throws<ArgumentException>(() => grid.SetCell(1, 0, text + "x")); Assert.Throws<ArgumentException>(() => grid.InsertColumn(0));
        Assert.Equal(text, grid.Cell(1, 0)); Assert.Equal(32, grid.ColumnCount); Assert.Equal(TableColumnAlignment.Left, grid.Alignment(31));
    }
}
