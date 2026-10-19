using System.Windows.Documents;
using System.Windows;
using LensDocsStudio.Core.Parsing;

namespace LensDocsStudio.Native.Rendering;

public sealed record PreviewLiteral(Run Run, LiteralSourceSpan Source, int RunOffset = 0)
{
    public TextPointer Start => Run.ContentStart.GetPositionAtOffset(RunOffset)!;
    public TextPointer End => Run.ContentStart.GetPositionAtOffset(RunOffset + Source.Length)!;
}

/// <summary>Character boundaries in literal runs, indexed once for each immutable preview.</summary>
public sealed class PreviewSourceMap
{
    private readonly PreviewLiteral[] literals;
    private readonly PreviewLiteral[] displayLiterals;
    private readonly PreviewViewportGroup[] verticalGroups;
    public IReadOnlyList<PreviewLiteral> Literals => literals;
    public PreviewSourceMap(IEnumerable<PreviewLiteral> literals) : this(literals, []) { }
    internal PreviewSourceMap(IEnumerable<PreviewLiteral> literals, IReadOnlyList<PreviewViewportGroup> groups)
    {
        // The renderer supplies document order, including relocated footnotes,
        // and records vertical ranges while it already owns each paragraph/row.
        // Avoid walking WPF parents or allocating a grouping per literal here.
        displayLiterals = literals.ToArray();
        this.literals = displayLiterals;
        for (var index = 1; index < displayLiterals.Length; index++)
            if (displayLiterals[index].Source.Start < displayLiterals[index - 1].Source.Start)
            { this.literals = displayLiterals.OrderBy(item => item.Source.Start).ToArray(); break; }
        verticalGroups = groups.ToArray();
    }
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
            else return literal.Start.GetPositionAtOffset(offset - literal.Source.Start, LogicalDirection.Forward);
        }
        return null;
    }
    public bool TrySourceRange(TextPointer from, TextPointer to, out int start, out int length)
    {
        var first = PreviewBoundary(from, true); var last = PreviewBoundary(to, false);
        start = first ?? 0; length = last is not null && first is not null ? last.Value - first.Value : 0;
        return first is not null && last is not null && length >= 0;
    }
    public bool TrySourceOffset(TextPointer position, out int offset)
    {
        // A reading position has forward affinity. At a formatting boundary the
        // range's backwards-affinity end may precede its start in Markdown.
        var source = PreviewBoundary(position, true);
        offset = source ?? 0;
        return source is not null;
    }
    private int? PreviewBoundary(TextPointer position, bool startBoundary)
    {
        var low = 0; var high = literals.Length - 1;
        while (low <= high)
        {
            var middle = (low + high) / 2; var literal = literals[middle];
            if (!position.IsInSameDocument(literal.Start)) return null;
            if (position.CompareTo(literal.Start) < 0) high = middle - 1;
            else if (position.CompareTo(literal.End) > 0) low = middle + 1;
            else
            {
                var offset = literal.Start.GetOffsetToPosition(position);
                if (startBoundary && offset == literal.Source.Length && middle + 1 < literals.Length
                    && EmptyFormattingGap(position, literals[middle + 1].Start))
                    return literals[middle + 1].Source.Start;
                if (!startBoundary && offset == 0 && middle > 0
                    && EmptyFormattingGap(literals[middle - 1].End, position))
                    return literals[middle - 1].Source.End;
                return offset is >= 0 && offset <= literal.Source.Length ? literal.Source.Start + offset : null;
            }
        }
        // Native/UIA selection can place endpoints outside transparent formatting elements.
        // Skip only a bounded, empty structural gap; never cross visible/generated text.
        if (startBoundary && low < literals.Length && EmptyFormattingGap(position, literals[low].Start))
            return literals[low].Source.Start;
        if (!startBoundary && high >= 0 && EmptyFormattingGap(literals[high].End, position))
            return literals[high].Source.End;
        return null;
    }
    private static bool EmptyFormattingGap(TextPointer from, TextPointer to)
        => from.IsInSameDocument(to) && from.GetOffsetToPosition(to) is >= 0 and <= 128 && new TextRange(from, to).Text.Length == 0;
    private IEnumerable<PreviewLiteral> ViewportLiterals(double height)
    {
        // Validity is evaluated afresh for details toggles. Unknown geometry falls
        // back to the exhaustive scan instead of guessing a source anchor.
        if (verticalGroups.Length == 0) return literals;
        var visibleGroups = verticalGroups.Where(group => displayLiterals[group.Start].Start.HasValidLayout).ToArray();
        var low = 0; var high = visibleGroups.Length;
        while (low < high)
        {
            var middle = (low + high) / 2;
            var rectangle = displayLiterals[visibleGroups[middle].Start].Start.GetCharacterRect(LogicalDirection.Forward);
            if (rectangle.IsEmpty) return literals;
            if (rectangle.Top < 0) low = middle + 1; else high = middle;
        }
        var candidates = new List<PreviewViewportGroup>();
        // The preceding paragraph/row may span the viewport's top edge.
        for (var index = Math.Max(0, low - 1); index < visibleGroups.Length; index++)
        {
            var group = visibleGroups[index];
            var top = displayLiterals[group.Start].Start.GetCharacterRect(LogicalDirection.Forward);
            if (top.IsEmpty) return literals;
            if (top.Top > height) break;
            candidates.Add(group);
        }
        return Enumerate();
        IEnumerable<PreviewLiteral> Enumerate()
        {
            foreach (var group in candidates)
                for (var index = group.Start; index < group.Start + group.Length; index++) yield return displayLiterals[index];
        }
    }
    public int? VisibleSource(double height)
    {
        var wanted = height / 3; var best = double.PositiveInfinity; int? source = null;
        foreach (var literal in ViewportLiterals(height))
        {
            var first = literal.Start; var last = literal.End;
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
            if (!box.IsEmpty && box.Bottom >= 0 && box.Top <= height && (distance < best || distance == best && (source is null || literal.Source.Start + offset < source))) { best = distance; source = literal.Source.Start + offset; }
        }
        return source;
    }
}

internal readonly record struct PreviewViewportGroup(int Start, int Length);
