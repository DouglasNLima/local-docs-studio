using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Core.Tests;

public sealed class DiagramViewportTests
{
    [Fact] public void Zoom_retains_the_same_drawing_coordinate_under_the_pointer()
    {
        const double original = 430, pointer = 170, padding = 24, before = 1.5, after = 2.25;
        var next = DiagramViewportMath.ZoomOffset(original, pointer, padding, before, after);
        Assert.Equal((original + pointer - padding) / before, (next + pointer - padding) / after, 8);
    }
    [Theory] [InlineData(300, 100, 150, 1200, 500, 250)] [InlineData(0, 100, 300, 1200, 500, 0)]
    [InlineData(600, 100, -400, 1200, 500, 700)] [InlineData(0, 100, 20, 200, 500, 0)]
    public void Panning_is_bounded_by_the_actual_drawing_extent(double offset, double origin, double pointer, double extent, double viewport, double expected)
        => Assert.Equal(expected, DiagramViewportMath.PanOffset(offset, origin, pointer, extent, viewport));
    [Fact] public void Invalid_zoom_does_not_produce_a_non_finite_viewport()
        => Assert.Throws<ArgumentOutOfRangeException>(() => DiagramViewportMath.ZoomOffset(0, 20, 24, 0, 1));
}
