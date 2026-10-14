using System.Windows.Documents;
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
        var first = PreviewBoundary(from); var last = PreviewBoundary(to);
        start = first ?? 0; length = last is not null && first is not null ? last.Value - first.Value : 0;
        return first is not null && last is not null && length >= 0;
    }
    private int? PreviewBoundary(TextPointer position)
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
                return offset is >= 0 && offset <= literal.Source.Length ? literal.Source.Start + offset : null;
            }
        }
        return null;
    }
}
