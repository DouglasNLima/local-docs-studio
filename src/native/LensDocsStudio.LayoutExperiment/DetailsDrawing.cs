using System.Windows;
using System.Windows.Media;

namespace LensDocsStudio.LayoutExperiment;

internal static class DetailsDrawing
{
    public static DrawingImage Create(bool expanded, string appearance)
    {
        // Same fixed 30-DIP native DetailsToggle box and 1.5-DIP template chevron.
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(appearance.StartsWith("HC:", StringComparison.Ordinal)
            ? ExperimentAppearance.Slot(appearance, 0) : appearance == "Dark" ? "#E8EAED" : "#202124")); brush.Freeze();
        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 30, 30));
            var path = new StreamGeometry(); using (var geometry = path.Open())
            {
                geometry.BeginFigure(expanded ? new Point(19, 13) : new Point(13, 11), false, false);
                geometry.LineTo(expanded ? new Point(15, 17) : new Point(17, 15), true, false);
                geometry.LineTo(expanded ? new Point(11, 13) : new Point(13, 19), true, false);
            }
            path.Freeze(); dc.DrawGeometry(null, new Pen(brush, 1.5), path);
        }
        group.Freeze(); var image = new DrawingImage(group); image.Freeze(); return image;
    }
}
