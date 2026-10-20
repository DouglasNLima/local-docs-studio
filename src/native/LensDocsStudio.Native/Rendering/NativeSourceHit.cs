using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;

namespace LensDocsStudio.Native.Rendering;

public sealed record NativeSourceHit(string Identity, string Description, Rect Bounds, Geometry? Shape = null)
{
    public static Geometry? ShapeGeometry(XElement group, XElement root)
    {
        var element = group.Elements().FirstOrDefault(item => item.Name.LocalName is "path" or "circle");
        if (element is null || group.AncestorsAndSelf().Any(item => item.Attribute("transform") is not null)) return null;
        Geometry shape;
        double Number(string name) => double.Parse((string?)element.Attribute(name) ?? "0", CultureInfo.InvariantCulture);
        if (element.Name.LocalName == "path") shape = Geometry.Parse((string?)element.Attribute("d") ?? "").Clone();
        else
        {
            var circle = new EllipseGeometry(new Point(Number("cx"), Number("cy")), Number("r"), Number("r"));
            // The pinned Tonal one-slice chart paints a stroked circle (a donut), not a filled disc.
            // Widen the actual declared stroke so its hole and outer radius remain exact pointer geometry.
            shape = (string?)element.Attribute("fill") == "none" && Number("stroke-width") > 0
                ? circle.GetWidenedPathGeometry(new Pen(Brushes.Black, Number("stroke-width"))) : circle;
        }
        var viewBox = ((string?)root.Attribute("viewBox") ?? "0 0").Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        shape.Transform = new TranslateTransform(-double.Parse(viewBox[0], CultureInfo.InvariantCulture), -double.Parse(viewBox[1], CultureInfo.InvariantCulture));
        shape.Freeze(); return shape;
    }
    /// <summary>Only actual untransformed canonical shapes provide hit bounds. No inferred label identity.</summary>
    public static Rect? ShapeBounds(XElement group, XElement root)
    {
        if (group.AncestorsAndSelf().Any(element => element.Attribute("transform") is not null)) return null;
        var shape = group.Elements().FirstOrDefault(element => element.Name.LocalName is "rect" or "circle" or "ellipse" or "path" or "polygon");
        if (shape is null || shape.Attribute("transform") is not null) return null;
        double Number(string name, double fallback = 0) => double.TryParse((string?)shape.Attribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : fallback;
        Rect bounds;
        switch (shape.Name.LocalName)
        {
            case "rect": bounds = new(Number("x"), Number("y"), Math.Max(0, Number("width")), Math.Max(0, Number("height"))); break;
            case "circle": var radius = Number("r"); bounds = new(Number("cx") - radius, Number("cy") - radius, radius * 2, radius * 2); break;
            case "ellipse": var rx = Number("rx"); var ry = Number("ry"); bounds = new(Number("cx") - rx, Number("cy") - ry, rx * 2, ry * 2); break;
            case "path": bounds = Geometry.Parse((string?)shape.Attribute("d") ?? "").Bounds; break;
            default:
                var points = ((string?)shape.Attribute("points") ?? "").Split([' ', ',', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(value => double.Parse(value, CultureInfo.InvariantCulture)).ToArray();
                if (points.Length < 6 || points.Length % 2 != 0 || points.Any(value => !double.IsFinite(value))) return null;
                var xs = points.Where((_, index) => index % 2 == 0).ToArray(); var ys = points.Where((_, index) => index % 2 == 1).ToArray(); bounds = new(xs.Min(), ys.Min(), xs.Max() - xs.Min(), ys.Max() - ys.Min()); break;
        }
        var viewBox = ((string?)root.Attribute("viewBox") ?? "0 0").Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        if (viewBox.Length >= 2) bounds.Offset(-double.Parse(viewBox[0], CultureInfo.InvariantCulture), -double.Parse(viewBox[1], CultureInfo.InvariantCulture));
        return bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0 || !double.IsFinite(bounds.X + bounds.Y + bounds.Width + bounds.Height) ? null : bounds;
    }
}
