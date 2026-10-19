using System.Windows.Media;

namespace LensDocsStudio.LayoutExperiment;

internal static class SelectionInk
{
    public static DrawingGroup Create(DrawingGroup drawing, string appearance)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(appearance.StartsWith("HC:", StringComparison.Ordinal)
            ? ExperimentAppearance.Slot(appearance, 4) : "#FFFFFF")); brush.Freeze();
        var foregrounds = new HashSet<Brush>(); Collect(drawing);
        return Paint(drawing);
        void Collect(DrawingGroup group) { foreach (var child in group.Children) { if (child is GlyphRunDrawing glyph && glyph.ForegroundBrush is { } ink) foregrounds.Add(ink); else if (child is DrawingGroup nested) Collect(nested); } }
        DrawingGroup Paint(DrawingGroup group)
        {
            // Share frozen images/geometry. Deep cloning a decoded bitmap can recreate
            // its WIC backing; selection only needs a new glyph foreground.
            var result = new DrawingGroup { Transform = group.Transform, ClipGeometry = group.ClipGeometry,
                Opacity = group.Opacity, OpacityMask = group.OpacityMask, GuidelineSet = group.GuidelineSet };
            foreach (var child in group.Children)
            {
                if (child is GlyphRunDrawing glyph) result.Children.Add(new GlyphRunDrawing(brush, glyph.GlyphRun));
                else if (child is DrawingGroup nested) result.Children.Add(Paint(nested));
                else if (child is ImageDrawing) result.Children.Add(child);
                else if (child is GeometryDrawing shape && (shape.Pen is not null || shape.Brush is not null && foregrounds.Contains(shape.Brush)))
                {
                    var pen = shape.Pen?.Clone(); if (pen is not null) pen.Brush = brush;
                    result.Children.Add(new GeometryDrawing(shape.Brush is null ? null : brush, pen, shape.Geometry));
                }
            }
            result.Freeze(); return result;
        }
    }
}
