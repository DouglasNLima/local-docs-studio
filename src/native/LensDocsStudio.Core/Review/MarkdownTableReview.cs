using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;

namespace LensDocsStudio.Core.Review;

internal static class MarkdownTableReview
{
    private readonly record struct Row(int Cells, bool HasPipe, bool SeparatorCandidate, bool ValidSeparator);

    public static void Scan(string source, ParsedDocument document, List<int> lineStarts,
        Action<int, int, string, string> add, CancellationToken cancellationToken)
    {
        var excluded = new bool[lineStarts.Count];
        var bodyStart = FrontMatterService.Read(source).BodyStart;
        if (bodyStart > 0) Exclude(0, bodyStart - 1);
        foreach (var block in document.Syntax.Descendants().OfType<Block>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (block is CodeBlock or HtmlBlock) Exclude(block.Span.Start, block.Span.End);
        }
        for (var index = 0; index + 1 < lineStarts.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (excluded[index] || excluded[index + 1]) continue;
            var header = Parse(Line(index)); var separator = Parse(Line(index + 1));
            if (!header.HasPipe || !separator.SeparatorCandidate) continue;
            if (!separator.ValidSeparator)
                Note(index + 1, "Markdown table separator row is invalid.", "Use at least three dashes per column, such as | --- | --- |.");
            else if (separator.Cells != header.Cells)
                Note(index + 1, $"Table separator has {Cells(separator.Cells)}; its header has {header.Cells}.", "Use the same number of cells in the header, separator and body rows.");
            index++;
            while (index + 1 < lineStarts.Count && !excluded[index + 1])
            {
                var row = Parse(Line(index + 1)); if (!row.HasPipe) break;
                cancellationToken.ThrowIfCancellationRequested(); index++;
                if (row.Cells != header.Cells)
                    Note(index, $"Table row has {Cells(row.Cells)}; its header has {header.Cells}.", "Add or remove cells so this row matches its header.");
            }
        }

        ReadOnlySpan<char> Line(int index)
        {
            var end = index + 1 < lineStarts.Count ? lineStarts[index + 1] : source.Length;
            while (end > lineStarts[index] && source[end - 1] is '\r' or '\n') end--;
            return source.AsSpan(lineStarts[index], end - lineStarts[index]);
        }
        void Note(int index, string message, string suggestion) => add(lineStarts[index], Line(index).Length, message, suggestion);
        void Exclude(int start, int end)
        {
            if (start < 0 || end < start) return;
            var first = LineIndex(Math.Min(start, source.Length)); var last = LineIndex(Math.Min(end, source.Length));
            Array.Fill(excluded, true, first, last - first + 1);
        }
        int LineIndex(int offset) { var index = lineStarts.BinarySearch(offset); return index < 0 ? ~index - 1 : index; }
    }

    private static Row Parse(ReadOnlySpan<char> raw)
    {
        var line = raw.Trim();
        for (var depth = 0; depth < 64; depth++)
        {
            if (line.StartsWith(">", StringComparison.Ordinal)) { line = line[1..].TrimStart(); continue; }
            if (line.Length > 1 && line[0] is '-' or '+' or '*' && char.IsWhiteSpace(line[1])) { line = line[2..].TrimStart(); continue; }
            var number = 0; while (number < Math.Min(9, line.Length) && char.IsAsciiDigit(line[number])) number++;
            if (number > 0 && number + 1 < line.Length && line[number] is '.' or ')' && char.IsWhiteSpace(line[number + 1])) { line = line[(number + 2)..].TrimStart(); continue; }
            break;
        }
        var hasPipe = false;
        if (line.StartsWith("|", StringComparison.Ordinal)) { line = line[1..]; hasPipe = true; }
        if (line.EndsWith("|", StringComparison.Ordinal))
        {
            var slashes = 0; for (var index = line.Length - 2; index >= 0 && line[index] == '\\'; index--) slashes++;
            if (slashes % 2 == 0) { line = line[..^1]; hasPipe = true; }
        }
        var cells = 1; var start = 0; var escaped = false; var candidate = true; var dash = false; var valid = true;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (!(char.IsWhiteSpace(character) || character is '|' or ':' or '-')) candidate = false;
            if (character == '-') dash = true;
            if (escaped) { escaped = false; continue; }
            if (character == '\\') { escaped = true; continue; }
            if (character != '|') continue;
            hasPipe = true; cells++; valid &= SeparatorCell(line[start..index]); start = index + 1;
        }
        valid &= SeparatorCell(line[start..]);
        return new(cells, hasPipe, hasPipe && candidate && dash, valid);
    }
    private static string Cells(int count) => count + (count == 1 ? " cell" : " cells");
    private static bool SeparatorCell(ReadOnlySpan<char> value)
    {
        value = value.Trim();
        if (value.StartsWith(":", StringComparison.Ordinal)) value = value[1..];
        if (value.EndsWith(":", StringComparison.Ordinal)) value = value[..^1];
        return value.Length >= 3 && value.IndexOfAnyExcept('-') < 0;
    }
}
