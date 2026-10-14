using System.Windows.Documents;
using System.Windows;
using LensDocsStudio.Core.Parsing;

namespace LensDocsStudio.Native.Rendering;

public sealed record PreviewLiteral(Run Run, LiteralSourceSpan Source);

/// <summary>Character boundaries in literal runs, indexed once for each immutable preview.</summary>
public sealed class PreviewSourceMap
{
    private readonly PreviewLiteral[] literals;
    public IReadOnlyList<PreviewLiteral> Literals => literals;
    public PreviewSourceMap(IEnumerable<PreviewLiteral> literals) => this.literals = literals.OrderBy(item => item.Source.Start).ToArray();

    public bool TryPreviewRange(int start, int length, out TextPointer? from, out TextPointer? to)
    {
        from = SourceBoundary(start); to = SourceBoundary(start + length);
        if (length > 0)
        {
            var low = 0; var high = literals.Length;
            while (low < high)
            {
                var middle = (low + high) / 2;
                if (literals[middle].Source.End <= start) low = middle + 1; else high = middle;
            }
            if (low == literals.Length || literals[low].Source.Start >= start + length) return false;
        }
        return from is not null && to is not null && from.CompareTo(to) <= 0;
    }
    private TextPointer? SourceBoundary(int offset)
    {
        var low = 0; var high = literals.Length - 1;
        while (low <= high)
        {
            var middle = (low + high) / 2; var literal = literals[middle];
            if (offset < literal.Source.Start) high = middle - 1;
            else if (offset > literal.Source.End) low = middle + 1;
            else return literal.Run.ContentStart.GetPositionAtOffset(offset - literal.Source.Start, LogicalDirection.Forward);
        }
        return null;
    }
    public bool TrySourceRange(TextPointer from, TextPointer to, out int start, out int length)
    {
        var first = PreviewBoundary(from, true); var last = PreviewBoundary(to, false);
        start = first ?? 0; length = last is not null && first is not null ? last.Value - first.Value : 0;
        return first is not null && last is not null && length >= 0;
    }
    private int? PreviewBoundary(TextPointer position, bool startBoundary)
    {
        var low = 0; var high = literals.Length - 1;
        while (low <= high)
        {
            var middle = (low + high) / 2; var literal = literals[middle];
            if (!position.IsInSameDocument(literal.Run.ContentStart)) return null;
            if (position.CompareTo(literal.Run.ContentStart) < 0) high = middle - 1;
            else if (position.CompareTo(literal.Run.ContentEnd) > 0) low = middle + 1;
            else
            {
                var offset = literal.Run.ContentStart.GetOffsetToPosition(position);
                if (startBoundary && offset == literal.Source.Length && middle + 1 < literals.Length
                    && EmptyFormattingGap(position, literals[middle + 1].Run.ContentStart))
                    return literals[middle + 1].Source.Start;
                if (!startBoundary && offset == 0 && middle > 0
                    && EmptyFormattingGap(literals[middle - 1].Run.ContentEnd, position))
                    return literals[middle - 1].Source.End;
                return offset is >= 0 && offset <= literal.Source.Length ? literal.Source.Start + offset : null;
            }
        }
        // Native/UIA selection can place endpoints outside transparent formatting elements.
        // Skip only a bounded, empty structural gap; never cross visible/generated text.
        if (startBoundary && low < literals.Length && EmptyFormattingGap(position, literals[low].Run.ContentStart))
            return literals[low].Source.Start;
        if (!startBoundary && high >= 0 && EmptyFormattingGap(literals[high].Run.ContentEnd, position))
            return literals[high].Source.End;
        return null;
    }
    private static bool EmptyFormattingGap(TextPointer from, TextPointer to)
        => from.IsInSameDocument(to) && from.GetOffsetToPosition(to) is >= 0 and <= 128 && new TextRange(from, to).Text.Length == 0;
    public int? VisibleSource(double height)
    {
        var wanted = height / 3; var best = double.PositiveInfinity; int? source = null;
        foreach (var literal in literals)
        {
            var first = literal.Run.ContentStart; var last = literal.Run.ContentEnd;
            if (!first.HasValidLayout || !last.HasValidLayout) continue;
            var start = first.GetCharacterRect(LogicalDirection.Forward); var end = last.GetCharacterRect(LogicalDirection.Backward);
            if (start.IsEmpty || end.IsEmpty || end.Bottom < 0 || start.Top > height) continue;
            var offset = 0;
            if (start.Top < wanted && end.Bottom > wanted && end.Top > start.Top)
            {
                var low = 0; var high = literal.Source.Length;
                while (low < high)
                {
                    var middle = (low + high) / 2;
                    var pointer = first.GetPositionAtOffset(middle)?.GetInsertionPosition(LogicalDirection.Forward);
                    if (pointer is null || !pointer.HasValidLayout) break;
                    var rectangle = pointer.GetCharacterRect(LogicalDirection.Forward);
                    if (rectangle.Top < wanted) low = middle + 1; else high = middle;
                }
                offset = low;
            }
            var located = first.GetPositionAtOffset(offset);
            if (located is null || !located.HasValidLayout) continue;
            var box = located.GetCharacterRect(LogicalDirection.Forward);
            var distance = Math.Abs(box.Top - wanted);
            if (!box.IsEmpty && box.Bottom >= 0 && box.Top <= height && distance < best) { best = distance; source = literal.Source.Start + offset; }
        }
        return source;
    }
}
