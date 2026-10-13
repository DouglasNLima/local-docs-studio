using System.Text;
using LensDocsStudio.Core.Parsing;
using Markdig.Extensions.Tables;
using Markdig.Syntax;

namespace LensDocsStudio.Core.Editing;

public enum TableColumnAlignment { Default, Left, Centre, Right }

/// <summary>A private, bounded draft. Row zero is the header and never moves into the body.</summary>
public sealed class TableEditGrid
{
    public const int MaximumRows = 4096, MaximumColumns = 128, MaximumCells = 65536;
    public const int MaximumCellCharacters = 16384, MaximumCharacters = 1_000_000;
    private readonly List<List<string>> rows;
    private readonly List<TableColumnAlignment> alignments;
    private int characters;
    public int RowCount => rows.Count;
    public int ColumnCount => alignments.Count;
    public string Cell(int row, int column) => rows[row][column];
    public TableColumnAlignment Alignment(int column) => alignments[column];

    public TableEditGrid(IEnumerable<IEnumerable<string>> source, IEnumerable<TableColumnAlignment> alignment)
    {
        alignments = alignment.Take(MaximumColumns + 1).ToList();
        rows = source.Take(MaximumRows + 1).Select(row => row.Take(MaximumColumns + 1).ToList()).ToList();
        CheckDimensions(rows.Count, alignments.Count);
        if (alignments.Any(value => !Enum.IsDefined(value)) || rows.Any(row => row.Count != alignments.Count))
            throw new ArgumentException("Use the same number of cells in every table row and valid column alignments.");
        foreach (var cell in rows.SelectMany(row => row)) { CheckCell(cell); characters = checked(characters + cell.Length); }
        CheckCharacters(characters);
    }
    public TableEditGrid Copy() => new(rows, alignments);
    public static TableEditGrid Create() => new(new[] { new[] { "Column A", "Column B", "Column C" }, new[] { "", "", "" }, new[] { "", "", "" } },
        Enumerable.Repeat(TableColumnAlignment.Default, 3));
    public bool SameAs(TableEditGrid other) => alignments.SequenceEqual(other.alignments) && RowCount == other.RowCount
        && rows.Select((row, index) => row.SequenceEqual(other.rows[index])).All(equal => equal);
    public void SetCell(int row, int column, string value)
    {
        CheckCell(value); var next = characters - rows[row][column].Length + value.Length; CheckCharacters(next);
        rows[row][column] = value; characters = next;
    }
    public void SetAlignment(int column, TableColumnAlignment value)
    { if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value)); alignments[column] = value; }
    public void InsertRow(int after)
    {
        if (after < 0 || after >= RowCount) throw new ArgumentOutOfRangeException(nameof(after));
        CheckDimensions(RowCount + 1, ColumnCount); rows.Insert(after + 1, Enumerable.Repeat("", ColumnCount).ToList());
    }
    public void RemoveRow(int row)
    {
        if (row <= 0 || row >= RowCount || RowCount <= 2) throw new ArgumentException("Keep the header and at least one body row.");
        characters -= rows[row].Sum(cell => cell.Length); rows.RemoveAt(row);
    }
    public void MoveRow(int row, int delta)
    {
        if (delta is not (-1 or 1) || row <= 0 || row + delta <= 0 || row >= RowCount || row + delta >= RowCount)
            throw new ArgumentException("Move a body row within the table body.");
        (rows[row], rows[row + delta]) = (rows[row + delta], rows[row]);
    }
    public void InsertColumn(int after)
    {
        if (after < 0 || after >= ColumnCount) throw new ArgumentOutOfRangeException(nameof(after));
        CheckDimensions(RowCount, ColumnCount + 1); var header = "Column " + (after + 2); CheckCharacters(characters + header.Length);
        for (var row = 0; row < RowCount; row++) rows[row].Insert(after + 1, row == 0 ? header : "");
        alignments.Insert(after + 1, TableColumnAlignment.Default); characters += header.Length;
    }
    public void RemoveColumn(int column)
    {
        if (column < 0 || column >= ColumnCount || ColumnCount <= 1) throw new ArgumentException("Keep at least one table column.");
        foreach (var row in rows) { characters -= row[column].Length; row.RemoveAt(column); } alignments.RemoveAt(column);
    }
    public void MoveColumn(int column, int delta)
    {
        if (delta is not (-1 or 1) || column < 0 || column >= ColumnCount || column + delta < 0 || column + delta >= ColumnCount)
            throw new ArgumentException("Move a column within the table.");
        foreach (var row in rows) (row[column], row[column + delta]) = (row[column + delta], row[column]);
        (alignments[column], alignments[column + delta]) = (alignments[column + delta], alignments[column]);
    }
    private static void CheckDimensions(int height, int width)
    {
        if (height is < 1 or > MaximumRows || width is < 1 or > MaximumColumns || (long)height * width > MaximumCells)
            throw new ArgumentException("The table editor supports up to 4,096 rows, 128 columns and 65,536 cells. Edit larger tables in Markdown.");
    }
    private static void CheckCharacters(int count)
    { if (count > MaximumCharacters) throw new ArgumentException("The table editor supports up to one million cell characters. Edit larger tables in Markdown."); }
    private static void CheckCell(string value)
    {
        if (value is null || value.Length > MaximumCellCharacters || value.Any(character => char.IsControl(character) && character != '\t'))
            throw new ArgumentException("Use up to 16,384 characters on one line per cell. Use <br> for a displayed line break.");
    }
}

public sealed class TableEditSession
{
    private readonly ParsedDocument document;
    private readonly TableEditGrid original;
    private readonly int start, length;
    private readonly string prefix, newline;
    public bool IsExisting { get; }
    public TableEditGrid Grid { get; }

    internal TableEditSession(ParsedDocument document, int start, int length, TableEditGrid grid, bool existing, string prefix, string newline)
    {
        this.document = document; this.start = start; this.length = length; this.prefix = prefix; this.newline = newline;
        IsExisting = existing; original = grid.Copy(); Grid = grid;
    }
    public MarkdownEdit Plan(TableEditGrid grid)
    {
        if (IsExisting && grid.SameAs(original)) return new(start, length, document.Source.Substring(start, length), start, length);
        var lines = new List<string>(grid.RowCount + 1);
        for (var row = 0; row < grid.RowCount; row++)
        {
            lines.Add("| " + string.Join(" | ", Enumerable.Range(0, grid.ColumnCount).Select(column => EscapePipes(grid.Cell(row, column)))) + " |");
            if (row == 0) lines.Add("| " + string.Join(" | ", Enumerable.Range(0, grid.ColumnCount).Select(column => grid.Alignment(column) switch
                { TableColumnAlignment.Left => ":---", TableColumnAlignment.Centre => ":---:", TableColumnAlignment.Right => "---:", _ => "---" })) + " |");
        }
        var plain = string.Join(newline, lines);
        if (plain.Length > 2 * TableEditGrid.MaximumCharacters) throw new ArgumentException("The generated table is too large for the table editor.");
        var parsed = new MarkdownService().Parse(plain);
        if (parsed.Syntax.Count != 1 || parsed.Syntax[0] is not Table table || table.Count != grid.RowCount || table.ColumnDefinitions.Count != grid.ColumnCount)
            throw new ArgumentException("A cell changes the table structure. Complete its Markdown or edit the table source directly.");
        if ((long)plain.Length + (long)(lines.Count - 1) * prefix.Length > 2 * TableEditGrid.MaximumCharacters)
            throw new ArgumentException("The generated table and its surrounding indentation are too large for the table editor.");
        var replacement = string.Join(newline, lines.Select((line, index) => (index == 0 ? "" : prefix) + line));
        return IsExisting ? new(start, length, replacement, start, replacement.Length)
            : MarkdownEditService.PlanBlock(document.Source, start, length, replacement, replacement);
    }
    private static string EscapePipes(string value)
    {
        var output = new StringBuilder(value.Length); var slashes = 0;
        foreach (var character in value.Trim())
        {
            if (character == '|' && slashes % 2 == 0) output.Append('\\');
            output.Append(character); slashes = character == '\\' ? slashes + 1 : 0;
        }
        return output.ToString();
    }
}

public static class TableEditService
{
    public const int MaximumSourceCharacters = 4 * 1024 * 1024;
    public static TableEditSession Open(ParsedDocument document, int start, int length)
    {
        var source = document.Source;
        if (start < 0 || length < 0 || (long)start + length > source.Length) throw new ArgumentOutOfRangeException(nameof(start));
        if (source.Length > MaximumSourceCharacters) throw new ArgumentException("The table editor supports documents up to 4 million characters.");
        var newline = source.IndexOf('\n') is var first && first > 0 && source[first - 1] == '\r' ? "\r\n" : "\n";
        var tables = document.Syntax.Descendants<Table>().ToArray();
        var table = tables.Where(value => start >= value.Span.Start && (long)start + length <= value.Span.End + 1)
            .OrderBy(value => value.Span.Length).FirstOrDefault();
        if (table is null)
        {
            if (tables.Any(value => start < value.Span.End + 1 && (long)start + length > value.Span.Start))
                throw new ArgumentException("Place the caret inside one table, or select that complete table, before editing it.");
            return new(document, start, length, TableEditGrid.Create(), false, "", newline);
        }
        var end = table.Span.End + 1;
        while (end > table.Span.Start && source[end - 1] is '\r' or '\n') end--;
        if (end - table.Span.Start > 2 * TableEditGrid.MaximumCharacters) throw new ArgumentException("This table is too large for the grid; edit its Markdown source.");
        var raw = source[table.Span.Start..end];
        var lines = raw.Replace("\r\n", "\n").Split('\n');
        if (lines.Length < 2 || lines.Length > TableEditGrid.MaximumRows + 1) throw Unsupported();
        var prefixLength = 0; while (prefixLength < lines[1].Length && lines[1][prefixLength] is ' ' or '\t' or '>') prefixLength++;
        var prefix = lines[1][..prefixLength];
        var separator = SplitRow(lines[1][prefixLength..]);
        var alignments = separator.Select(Alignment).ToArray();
        var rows = new List<string[]>();
        for (var index = 0; index < lines.Length; index++)
        {
            if (index == 1) continue;
            if (index > 1 && !lines[index].StartsWith(prefix, StringComparison.Ordinal)) throw Unsupported();
            var cells = SplitRow(index == 0 ? lines[index] : lines[index][prefix.Length..]);
            if (cells.Count > alignments.Length) throw new ArgumentException("A row has extra cells. Correct its Markdown before using the grid so no content is discarded.");
            rows.Add(Enumerable.Range(0, alignments.Length).Select(column => column < cells.Count ? cells[column] : "").ToArray());
        }
        if (rows.Count != table.Count || alignments.Length != table.ColumnDefinitions.Count) throw Unsupported();
        return new(document, table.Span.Start, end - table.Span.Start, new(rows, alignments), true, prefix, raw.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n");
    }
    private static List<string> SplitRow(string raw)
    {
        var line = raw.Trim();
        if (line.StartsWith('|')) line = line[1..];
        if (line.EndsWith('|'))
        {
            var slashes = 0; for (var index = line.Length - 2; index >= 0 && line[index] == '\\'; index--) slashes++;
            if (slashes % 2 == 0) line = line[..^1];
        }
        var cells = new List<string>(); var start = 0; var escaped = false;
        for (var index = 0; index < line.Length; index++)
        {
            if (escaped) { escaped = false; continue; }
            if (line[index] == '\\') { escaped = true; continue; }
            if (line[index] != '|') continue;
            cells.Add(line[start..index].Trim()); start = index + 1;
            if (cells.Count > TableEditGrid.MaximumColumns) throw Unsupported();
        }
        cells.Add(line[start..].Trim()); return cells;
    }
    private static TableColumnAlignment Alignment(string value)
    {
        var dashes = value.AsSpan().Trim(':');
        if (dashes.Length < 3 || dashes.IndexOfAnyExcept('-') >= 0) throw Unsupported();
        return (value.StartsWith(':'), value.EndsWith(':')) switch
        { (true, true) => TableColumnAlignment.Centre, (true, false) => TableColumnAlignment.Left, (false, true) => TableColumnAlignment.Right, _ => TableColumnAlignment.Default };
    }
    private static ArgumentException Unsupported() => new("This table uses a form the grid cannot preserve. Edit its Markdown source directly.");
}
