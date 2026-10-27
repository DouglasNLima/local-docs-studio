using System.Windows;
using System.Windows.Media;

namespace LensDocsStudio.Native.Rendering;

/// <summary>Transient viewport adornment. The canonical DrawingImage is never modified.</summary>
public sealed class DiagramInspectionOverlay : FrameworkElement
{
    public static readonly DependencyProperty HighlightProperty = DependencyProperty.Register(nameof(Highlight), typeof(Brush), typeof(DiagramInspectionOverlay), new FrameworkPropertyMetadata(Brushes.DarkOrange, FrameworkPropertyMetadataOptions.AffectsRender));
    public Brush Highlight { get => (Brush)GetValue(HighlightProperty); set => SetValue(HighlightProperty, value); }
    public DiagramInspection? Inspection { get; set; }
    private string? selected, hovered;
    private double scale = 1;
    public void Update(string? selection, string? hover, double zoom)
    {
        if (selected == selection && hovered == hover && scale == zoom) return;
        selected = selection; hovered = hover; scale = zoom; InvalidateVisual();
    }
    protected override void OnRender(DrawingContext context)
    {
        if (Inspection is not { } view) return;
        var edges = view.Connected(selected).Concat(view.Connected(hovered)).DistinctBy(edge => edge.Identity).ToArray();
        var pen = new Pen(Highlight, Math.Clamp(3 / scale, 2, 12));
        context.PushClip(view.ConnectionClip);
        foreach (var edge in edges) context.DrawGeometry(null, pen, edge.Path);
        context.Pop();
        var endpoints = edges.SelectMany(edge => new[] { edge.First, edge.Second }).ToHashSet(StringComparer.Ordinal);
        foreach (var entity in view.Entities)
        {
            if (entity.Identity != selected && entity.Identity != hovered && !endpoints.Contains(entity.Identity)) continue;
            var outline = new Pen(Highlight, Math.Clamp((entity.Identity == selected ? 4 : 2) / scale, 1.5, 12));
            if (entity.Identity != selected) outline.DashStyle = DashStyles.Dash;
            context.DrawGeometry(null, outline, entity.Shape);
        }
        // A persistent relationship also has a dashed label frame; selection is
        // distinguishable by shape/stroke as well as colour, including High Contrast.
        var chosen = view.Connections.FirstOrDefault(edge => edge.Identity == selected);
        if (chosen?.LabelBounds is { } label)
            context.DrawRectangle(null, new Pen(Highlight, Math.Clamp(2 / scale, 1, 8)) { DashStyle = DashStyles.Dash }, label);
    }
}
