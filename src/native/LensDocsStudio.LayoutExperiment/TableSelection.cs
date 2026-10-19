using System.Collections.Immutable;

namespace LensDocsStudio.LayoutExperiment;

internal sealed record TableCellIndex(SemanticNode Node, int Row);
internal sealed record TableIndex(SemanticNode Node, ImmutableArray<TableCellIndex> Cells);

internal static class TableSelection
{
    private static (TableIndex Table, TableCellIndex Cell)? At(LayoutSnapshot snapshot, int offset)
    {
        (TableIndex Table, TableCellIndex Cell)? result = null;
        foreach (var table in snapshot.Indices.Tables)
        foreach (var cell in table.Cells)
            if (cell.Node.Start <= offset && offset < cell.Node.End && (result is null || cell.Node.End - cell.Node.Start < result.Value.Cell.Node.End - result.Value.Cell.Node.Start)) result = (table, cell);
        return result;
    }
    public static (int Anchor, int Active) Normalise(LayoutSnapshot snapshot, int anchor, int active, bool preferPreviousRow = false)
    {
        if (anchor == active) return (anchor, active);
        var activeCell = preferPreviousRow ? At(snapshot, active - 1) ?? At(snapshot, active) : At(snapshot, active) ?? At(snapshot, active - 1);
        if (activeCell is { } entering && (anchor < entering.Table.Node.Start || anchor > entering.Table.Node.End))
        {
            var row = entering.Table.Cells.Where(cell => cell.Row == entering.Cell.Row).Select(cell => cell.Node).ToArray();
            return (anchor, anchor < entering.Table.Node.Start ? row.Max(cell => cell.End) : row.Min(cell => cell.Start));
        }
        var first = At(snapshot, Math.Min(anchor, active)); var last = At(snapshot, Math.Max(anchor, active) - 1);
        if (first is null || last is null || first.Value.Table.Node.Id != last.Value.Table.Node.Id || first.Value.Cell.Node.Id == last.Value.Cell.Node.Id) return (anchor, active);
        var cells = Rectangle(first.Value.Table, first.Value.Cell, last.Value.Cell);
        var start = cells.Min(cell => cell.Start); var end = cells.Max(cell => cell.End);
        return anchor <= active ? (start, end) : (end, start);
    }
    public static TableIndex? CellRangeTable(LayoutSnapshot snapshot, int start, int end)
    {
        if (start == end) return null;
        var first = At(snapshot, start); var last = At(snapshot, end - 1);
        return first is not null && last is not null && first.Value.Table.Node.Id == last.Value.Table.Node.Id && first.Value.Cell.Node.Id != last.Value.Cell.Node.Id ? first.Value.Table : null;
    }
    public static int Arrow(LayoutSnapshot snapshot, int anchor, int active, bool right, ref int boundaryOffset, ref bool boundaryRight)
    {
        foreach (var table in snapshot.Indices.Tables.Where(table => anchor < table.Node.Start && active >= table.Node.Start && active <= table.Node.End))
        {
            var rows = table.Cells.GroupBy(cell => cell.Row).Select(row => row.Max(cell => cell.Node.End)).ToArray();
            if (active == table.Node.End)
            {
                if (boundaryOffset != active || boundaryRight != right) { boundaryOffset = active; boundaryRight = right; return active; }
                boundaryOffset = -1;
                return right ? SnapshotIndices.Adjacent(snapshot.Indices.Characters, active, true) : rows.Length > 1 ? rows[^2] : SnapshotIndices.Adjacent(snapshot.Indices.Characters, table.Node.Start, false);
            }
            if (!right && Array.IndexOf(rows, active) is var row && row >= 0)
            { boundaryOffset = -1; return row > 0 ? rows[row - 1] : SnapshotIndices.Adjacent(snapshot.Indices.Characters, table.Node.Start, false); }
        }
        boundaryOffset = -1; return -1;
    }
    private static SemanticNode[] Rectangle(TableIndex table, TableCellIndex first, TableCellIndex last)
    {
        var rowStart = Math.Min(first.Row, last.Row); var rowEnd = Math.Max(first.Row + first.Node.RowSpan, last.Row + last.Node.RowSpan);
        var colStart = Math.Min(first.Node.Column, last.Node.Column); var colEnd = Math.Max(first.Node.Column + first.Node.ColumnSpan, last.Node.Column + last.Node.ColumnSpan);
        return table.Cells.Where(cell => cell.Row < rowEnd && cell.Row + cell.Node.RowSpan > rowStart && cell.Node.Column < colEnd && cell.Node.Column + cell.Node.ColumnSpan > colStart).Select(cell => cell.Node).ToArray();
    }
    public static SemanticNode[] PaintedCells(LayoutSnapshot snapshot, int start, int end)
    {
        if (start == end) return [];
        var first = At(snapshot, start); var last = At(snapshot, end - 1);
        if (first is not null && last is not null && first.Value.Table.Node.Id == last.Value.Table.Node.Id && first.Value.Cell.Node.Id != last.Value.Cell.Node.Id)
            return Rectangle(first.Value.Table, first.Value.Cell, last.Value.Cell);
        return snapshot.Indices.Tables.SelectMany(table => table.Cells).Select(cell => cell.Node).Where(cell => start <= cell.Start && end >= cell.End).ToArray();
    }
}
