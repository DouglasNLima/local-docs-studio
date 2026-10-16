using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace LensDocsStudio.Native.Rendering;

/// <summary>Semantic anchors reuse parser spans and only inspect already-valid native layout.</summary>
public sealed class PreviewSemanticAnchors
{
    private readonly KeyValuePair<int, TextElement>[] anchors;
    private readonly PreviewSourceMap textMap;
    public PreviewSemanticAnchors(PreviewResult result)
    { anchors = result.SourceElements.OrderBy(item => item.Key).ToArray(); textMap = result.TextMap; }
    public TextElement? AtSource(int offset)
    {
        var low = 0; var high = anchors.Length;
        while (low < high) { var middle = (low + high) / 2; if (anchors[middle].Key <= offset) low = middle + 1; else high = middle; }
        return anchors.Length == 0 ? null : anchors[Math.Max(0, low - 1)].Value;
    }
    public SourceReference? AtPointer(TextPointer pointer)
    {
        var low = 0; var high = anchors.Length;
        while (low < high)
        {
            var middle = (low + high) / 2; var element = anchors[middle].Value;
            if (!pointer.IsInSameDocument(element.ContentStart)) return null;
            if (element.ContentStart.CompareTo(pointer) <= 0) low = middle + 1; else high = middle;
        }
        if (low > 0 && anchors[low - 1].Value.Tag is SourceReference reference) return reference;
        if (low == 0 && anchors.Length > 0 && anchors[0].Value.Tag is SourceReference first) return first;
        for (var current = pointer.Parent as DependencyObject; current is not null; current = LogicalTreeHelper.GetParent(current))
            if (current is TextElement { Tag: SourceReference source }) return source;
        return null;
    }
    public int? VisibleSource(DocumentPreview viewer)
    {
        if (viewer.ViewingMode != FlowDocumentReaderViewingMode.Scroll)
        {
            var page = Descendant<FlowDocumentPageViewer>(viewer)?.PageViews.FirstOrDefault()?.DocumentPage;
            if (page is null || viewer.Document is null) return null;
            var paginator = (DynamicDocumentPaginator)((IDocumentPaginatorSource)viewer.Document).DocumentPaginator;
            if (paginator.GetPagePosition(page) is TextPointer position)
            {
                // A page can begin halfway through a paragraph. Its block start may
                // be on the previous page; retain the exact literal boundary instead.
                var insertion = position.GetInsertionPosition(LogicalDirection.Forward) ?? position;
                if (textMap.TrySourceOffset(insertion, out var exactSource)) return exactSource;
                return AtPointer(position)?.Start;
            }
            return null;
        }
        // Dirty/non-visible positions are skipped, so this cannot paginate the whole document.
        var wanted = Math.Max(0, viewer.ActualHeight / 3);
        var closest = double.PositiveInfinity; int? source = null;
        foreach (var (offset, element) in anchors)
        {
            var pointer = element.ContentStart;
            if (!pointer.HasValidLayout) continue;
            var rectangle = pointer.GetCharacterRect(LogicalDirection.Forward);
            if (rectangle.IsEmpty || rectangle.Bottom < 0 || rectangle.Top > viewer.ActualHeight) continue;
            var distance = Math.Abs(rectangle.Top - wanted);
            if (distance < closest) { closest = distance; source = offset; }
        }
        return source;
    }
    public static T? Descendant<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent is T match) return match;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            if (Descendant<T>(VisualTreeHelper.GetChild(parent, index)) is { } child) return child;
        return null;
    }
}
