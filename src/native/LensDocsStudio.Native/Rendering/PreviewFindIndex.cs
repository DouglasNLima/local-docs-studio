using System.Diagnostics;
using System.Text;
using System.Windows.Documents;
using System.Windows.Threading;
using LensDocsStudio.Core.Parsing;

namespace LensDocsStudio.Native.Rendering;

/// <summary>Text offsets across inline formatting, mapped back to the exact FlowDocument revision.</summary>
public sealed class PreviewFindIndex
{
    private sealed record Segment(int Offset, int Length, TextPointer Start, TextPointer End, bool Formula);
    private readonly List<Segment> segments = [];
    public string Text { get; private set; } = "";
    public bool Complete { get; private set; } = true;
    public bool HasFormulae { get; private set; }

    public static async Task<PreviewFindIndex> CreateAsync(FlowDocument document, IReadOnlyList<PreviewMaths> formulae, CancellationToken token)
    {
        var result = new PreviewFindIndex();
        var formulas = formulae.ToDictionary(item => item.Element);
        var text = new StringBuilder(); var watch = Stopwatch.StartNew(); var buffer = new char[8192];
        var pointer = document.ContentStart;
        while (pointer is not null && pointer.CompareTo(document.ContentEnd) < 0)
        {
            token.ThrowIfCancellationRequested();
            if (text.Length > FindMatches.MaximumCharacters || result.segments.Count > 200_000)
            { result.Complete = false; break; }
            if (watch.ElapsedMilliseconds >= 4)
            { await Dispatcher.Yield(DispatcherPriority.Background); token.ThrowIfCancellationRequested(); watch.Restart(); }
            var context = pointer.GetPointerContext(LogicalDirection.Forward);
            if (context == TextPointerContext.Text)
            {
                var length = pointer.GetTextInRun(LogicalDirection.Forward, buffer, 0, buffer.Length);
                var end = pointer.GetPositionAtOffset(length)!;
                result.segments.Add(new(text.Length, length, pointer, end, false));
                text.Append(buffer, 0, length); pointer = end; continue;
            }
            if (context == TextPointerContext.ElementStart && pointer.GetAdjacentElement(LogicalDirection.Forward) is InlineUIContainer inline)
            {
                text.Append('\0');
                if (formulas.TryGetValue(inline, out var formula))
                {
                    result.HasFormulae = true;
                    result.segments.Add(new(text.Length, formula.Source.Length, inline.ElementStart, inline.ElementEnd, true));
                    text.Append(formula.Source);
                }
                text.Append('\0'); pointer = inline.ElementEnd; continue;
            }
            // Structural boundaries and embedded controls cannot manufacture a text match.
            // Span/Run/Hyperlink boundaries deliberately remain adjacent.
            if (context == TextPointerContext.EmbeddedElement || pointer.GetAdjacentElement(LogicalDirection.Forward) is Block or LineBreak)
                text.Append('\0');
            pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);
        }
        result.Text = result.Complete ? text.ToString() : "";
        return result;
    }

    public (TextPointer Start, TextPointer End, bool Formula) Range(int offset, int length)
    {
        var first = Locate(offset); var last = Locate(offset + length - 1);
        return (first.Formula ? first.Start : first.Start.GetPositionAtOffset(offset - first.Offset)!,
            last.Formula ? last.End : last.Start.GetPositionAtOffset(offset + length - last.Offset)!, first.Formula);
    }
    private Segment Locate(int offset)
    {
        var low = 0; var high = segments.Count - 1;
        while (low <= high)
        {
            var middle = (low + high) / 2; var segment = segments[middle];
            if (offset < segment.Offset) high = middle - 1;
            else if (offset >= segment.Offset + segment.Length) low = middle + 1;
            else return segment;
        }
        throw new ArgumentOutOfRangeException(nameof(offset));
    }
}
