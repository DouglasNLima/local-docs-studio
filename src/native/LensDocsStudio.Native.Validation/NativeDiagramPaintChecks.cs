using System.Drawing;
using System.Text.Json;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

internal sealed record DiagramPaintObservation(bool Passed, Rectangle Region, int Background,
    int LeftSurface, int RightSurface, int LeftText, int RightText);

internal static class NativeDiagramPaintChecks
{
    // This gate is specific to the two labelled nodes in the desktop Forest fixture.
    // UIA exposes an Image before the compositor necessarily paints its drawing.
    public static DiagramPaintObservation ObserveForest(Bitmap bitmap, Rectangle region)
    {
        if (region.Width < 80 || region.Height < 20 || !new Rectangle(0, 0, bitmap.Width, bitmap.Height).Contains(region))
            return new(false, region, 0, 0, 0, 0, 0);
        var background = 0; var leftSurface = 0; var rightSurface = 0; var leftText = 0; var rightText = 0;
        for (var y = region.Top; y < region.Bottom; y++)
        for (var x = region.Left; x < region.Right; x++)
        {
            var pixel = bitmap.GetPixel(x, y); var left = x < region.Left + region.Width / 2;
            if (Near(pixel, 247, 252, 247, 3)) background++;
            if (Near(pixel, 231, 243, 231, 3)) { if (left) leftSurface++; else rightSurface++; }
            if (Near(pixel, 23, 53, 31, 16)) { if (left) leftText++; else rightText++; }
        }
        var area = (long)region.Width * region.Height;
        return new(background > area / 10 && leftSurface > area / 25 && rightSurface > area / 25 && leftText >= 5 && rightText >= 5,
            region, background, leftSurface, rightSurface, leftText, rightText);
    }

    public static void Run(string output)
    {
        using var forest = Render("Forest");
        var region = new Rectangle(0, 0, forest.Width, forest.Height);
        using var blank = new Bitmap(forest.Width, forest.Height);
        using (var graphics = Graphics.FromImage(blank)) graphics.Clear(Color.FromArgb(247, 252, 247));
        using var partial = new Bitmap(forest);
        using (var graphics = Graphics.FromImage(partial))
        using (var brush = new SolidBrush(Color.FromArgb(247, 252, 247)))
            graphics.FillRectangle(brush, region.Width / 2, 0, region.Width - region.Width / 2, region.Height);
        using var wrongTheme = Render("Default");
        var complete = ObserveForest(forest, region); var empty = ObserveForest(blank, region);
        var missingNode = ObserveForest(partial, region); var incorrect = ObserveForest(wrongTheme, region);
        var passed = complete.Passed && !empty.Passed && !missingNode.Passed && !incorrect.Passed;
        File.WriteAllText(Path.Combine(output, "native-diagram-paint.json"), JsonSerializer.Serialize(new
        { Passed = passed, Complete = complete, Blank = empty, MissingNode = missingNode, WrongTheme = incorrect }, new JsonSerializerOptions { WriteIndented = true }));
        if (!passed) throw new IOException("The diagram paint gate accepted missing geometry or rejected the complete Forest fixture.");

        static Bitmap Render(string theme)
        {
            var diagram = new DiagramService().Render("flowchart LR\nA[Write] --> B[Review]", theme: theme);
            var pixels = NativeSvgService.ToPng(new NativeSvgService().Render(diagram.Svg!), 1);
            using var input = new MemoryStream(pixels); using var source = new Bitmap(input);
            return new Bitmap(source);
        }
    }

    private static bool Near(Color pixel, int red, int green, int blue, int tolerance)
        => Math.Abs(pixel.R - red) <= tolerance && Math.Abs(pixel.G - green) <= tolerance && Math.Abs(pixel.B - blue) <= tolerance;
}
