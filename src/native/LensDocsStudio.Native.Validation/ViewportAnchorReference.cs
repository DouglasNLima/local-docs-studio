using System.Windows.Documents;
using LensDocsStudio.Native.Rendering;
namespace LensDocsStudio.Native.Validation;

// The 0237b08 exhaustive geometry scan, retained outside timing as an independent oracle.
internal static class ViewportAnchorReference
{
    public static int? VisibleSource(PreviewSourceMap map, double height)
    {
        var wanted = height / 3; var best = double.PositiveInfinity; int? source = null;
        foreach (var literal in map.Literals)
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
            if (!box.IsEmpty && box.Bottom >= 0 && box.Top <= height && distance < best) { best = distance; source = literal.Source.Start + offset; }
        }
        return source;
    }
}
