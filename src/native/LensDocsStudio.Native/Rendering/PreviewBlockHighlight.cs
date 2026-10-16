using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace LensDocsStudio.Native.Rendering;

/// <summary>A semantic highlight is presentation-only; it never changes FlowDocument formatting.</summary>
public sealed class PreviewBlockHighlight(DocumentPreview preview) : Adorner(preview)
{
    private TextElement? block;
    public void Show(TextElement? element) { block = element; IsHitTestVisible = false; InvalidateVisual(); }
    protected override void OnRender(DrawingContext context)
    {
        if (block is null || preview.Document is null || !block.ContentStart.IsInSameDocument(preview.Document.ContentStart) || !block.ContentStart.HasValidLayout) return;
        var first = block.ContentStart.GetCharacterRect(LogicalDirection.Forward);
        if (first.IsEmpty) return;
        var last = block.ContentEnd.HasValidLayout ? block.ContentEnd.GetCharacterRect(LogicalDirection.Backward) : first;
        var top = Math.Clamp(first.Top - 4, 0, Math.Max(0, ActualHeight));
        var bottom = Math.Clamp(last.Bottom + 4, top, Math.Max(top, ActualHeight));
        if (bottom <= top || ActualWidth <= 32) return;
        var accent = TryFindResource("AccentBrush") as Brush ?? SystemColors.HighlightBrush;
        var wash = accent.Clone(); wash.Opacity = SystemParameters.HighContrast ? 0.25 : 0.12;
        context.DrawRoundedRectangle(wash, new Pen(accent, 1.5), new Rect(16, top, ActualWidth - 32, bottom - top), 3, 3);
    }
}
