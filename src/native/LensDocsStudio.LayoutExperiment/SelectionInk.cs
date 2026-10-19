using System.Windows.Media;

namespace LensDocsStudio.LayoutExperiment;

internal static class SelectionInk
{
    public static DrawingGroup Create(DrawingGroup drawing, string appearance)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(appearance.StartsWith("HC:", StringComparison.Ordinal)
            ? ExperimentAppearance.Slot(appearance, 4) : "#FFFFFF")); brush.Freeze();
        return Paint(drawing);
        DrawingGroup Paint(DrawingGroup group)
        {
            // Share frozen images/geometry. Deep cloning a decoded bitmap can recreate
            // its WIC backing; selection only needs a new glyph foreground.
            var result = new DrawingGroup { Transform = group.Transform, ClipGeometry = group.ClipGeometry,
                Opacity = group.Opacity, OpacityMask = group.OpacityMask, GuidelineSet = group.GuidelineSet };
            foreach (var child in group.Children)
                result.Children.Add(child is GlyphRunDrawing glyph ? new GlyphRunDrawing(brush, glyph.GlyphRun)
                    : child is DrawingGroup nested ? Paint(nested) : child);
            result.Freeze(); return result;
        }
    }
}
