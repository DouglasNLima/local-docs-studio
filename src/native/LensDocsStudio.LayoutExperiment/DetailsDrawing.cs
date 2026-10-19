using System.Windows;
using System.Windows.Media;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.LayoutExperiment;

internal static class DetailsDrawing
{
    public static DrawingImage Create(bool expanded, string appearance)
    {
        // Use the real public native control template, including its checked
        // chrome. It is owned/measured only on this STA; only frozen paint leaves.
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(appearance.StartsWith("HC:", StringComparison.Ordinal)
            ? ExperimentAppearance.Slot(appearance, 0) : appearance == "Dark" ? "#E8EAED" : "#202124")); brush.Freeze();
        var toggle = new DetailsToggle { IsExpanded = expanded, Foreground = brush };
        toggle.Measure(new Size(30, 30)); toggle.Arrange(new Rect(0, 0, 30, 30)); toggle.UpdateLayout();
        var group = new DrawingGroup();
        using (var dc = group.Open()) Copy(toggle, dc);
        group.Freeze(); var image = new DrawingImage(group); image.Freeze(); return image;

        static void Copy(Visual visual, DrawingContext context)
        {
            // Keep the fixed control box even where its chrome is transparent.
            if (visual is DetailsToggle) context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 30, 30));
            if (VisualTreeHelper.GetDrawing(visual) is { } drawing) context.DrawDrawing(drawing.CloneCurrentValue());
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(visual); index++)
            {
                if (VisualTreeHelper.GetChild(visual, index) is not Visual child) continue;
                var transform = child.TransformToAncestor(visual);
                var origin = transform.Transform(new Point()); var x = transform.Transform(new Point(1, 0)); var y = transform.Transform(new Point(0, 1));
                context.PushTransform(new MatrixTransform(x.X - origin.X, x.Y - origin.Y, y.X - origin.X, y.Y - origin.Y, origin.X, origin.Y));
                Copy(child, context); context.Pop();
            }
        }
    }
}
