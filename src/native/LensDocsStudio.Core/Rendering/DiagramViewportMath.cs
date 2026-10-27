namespace LensDocsStudio.Core.Rendering;

/// <summary>Viewport transforms preserve drawing coordinates and never alter exported content.</summary>
public static class DiagramViewportMath
{
    public static bool BeginsPan(double x, double y) => double.IsFinite(x) && double.IsFinite(y) && x * x + y * y >= 25;
    public static bool NearSegment(double x, double y, double ax, double ay, double bx, double by, double tolerance)
    {
        var dx = bx - ax; var dy = by - ay; var square = dx * dx + dy * dy;
        var fraction = square == 0 ? 0 : Math.Clamp(((x - ax) * dx + (y - ay) * dy) / square, 0, 1);
        var distanceX = x - ax - fraction * dx; var distanceY = y - ay - fraction * dy;
        return distanceX * distanceX + distanceY * distanceY <= tolerance * tolerance;
    }
    public static double ZoomOffset(double offset, double pointer, double padding, double before, double after)
    {
        if (!double.IsFinite(offset) || !double.IsFinite(pointer) || !double.IsFinite(padding) || !double.IsFinite(before)
            || !double.IsFinite(after) || before <= 0 || after <= 0) throw new ArgumentOutOfRangeException(nameof(before));
        return Math.Max(0, (offset + pointer - padding) * after / before + padding - pointer);
    }
    public static double PanOffset(double original, double origin, double pointer, double extent, double viewport)
        => Math.Clamp(original + origin - pointer, 0, Math.Max(0, extent - viewport));
}
