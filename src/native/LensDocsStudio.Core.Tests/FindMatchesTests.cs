using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Core.Tests;

public sealed class FindMatchesTests
{
    [Fact] public void Match_indices_are_literal_case_insensitive_and_non_overlapping()
    {
        var matches = FindMatches.Collect("Café café CAFÉ", "café");
        Assert.Equal(new[] { 0, 5, 10 }, matches.Offsets);
        Assert.Equal(new[] { 0 }, FindMatches.Collect("aaa", "aa").Offsets);
    }
    [Fact] public void Both_directions_wrap_and_never_report_an_invalid_index()
    {
        var matches = FindMatches.Collect("a a a", "a");
        Assert.Equal(0, matches.Move(-1, 1)); Assert.Equal(2, matches.Move(-1, -1));
        Assert.Equal(0, matches.Move(2, 1)); Assert.Equal(2, matches.Move(0, -1));
        Assert.Equal(1, matches.Move(0, 1)); Assert.Equal(0, matches.Move(1, -1));
    }
    [Theory] [InlineData("")] [InlineData("absent")]
    public void Empty_or_missing_query_has_no_current_match(string query)
    { var matches = FindMatches.Collect("text", query); Assert.Empty(matches.Offsets); Assert.Equal(-1, matches.Move(-1, -1)); }
    [Fact] public void Replacement_recalculation_uses_new_snapshot_and_next_anchor()
    {
        var original = FindMatches.Collect("cat cat cat", "cat");
        var replaced = FindMatches.Collect("dog cat cat", "cat");
        Assert.Equal(3, original.Offsets.Count); Assert.Equal(2, replaced.Offsets.Count);
        Assert.Equal(0, replaced.AtOrAfter(3)); Assert.Equal(0, replaced.AtOrAfter(100));
    }
    [Fact] public void Cancelled_or_excessive_search_never_publishes_a_partial_count()
    {
        Assert.Throws<OperationCanceledException>(() => FindMatches.Collect("abc", "a", new(true)));
        Assert.False(FindMatches.Collect(new string('a', FindMatches.MaximumMatches + 1), "a").Complete);
        Assert.False(FindMatches.Collect(new string('a', FindMatches.MaximumCharacters + 1), "a").Complete);
    }
    [Theory] [InlineData(3, 3, false)] [InlineData(3, 4, true)] [InlineData(0, 5, true)] [InlineData(-6, 0, true)]
    public void Pan_threshold_uses_device_independent_distance(double x, double y, bool expected)
        => Assert.Equal(expected, DiagramViewportMath.BeginsPan(x, y));
    [Fact] public void Pan_is_clamped_and_zoom_keeps_the_pointer_anchor()
    {
        Assert.Equal(140, DiagramViewportMath.PanOffset(100, 50, 10, 1000, 500));
        Assert.Equal(0, DiagramViewportMath.PanOffset(0, 10, 500, 1000, 500));
        Assert.Equal(0, DiagramViewportMath.PanOffset(100, 10, 0, 100, 500));
        Assert.Equal(376, DiagramViewportMath.ZoomOffset(100, 200, 24, 1, 2));
    }
    [Fact] public void Connection_hit_distance_handles_segments_endpoints_and_zero_length()
    {
        Assert.True(DiagramViewportMath.NearSegment(5, 2, 0, 0, 10, 0, 3));
        Assert.False(DiagramViewportMath.NearSegment(5, 4, 0, 0, 10, 0, 3));
        Assert.True(DiagramViewportMath.NearSegment(11, 0, 0, 0, 10, 0, 2));
        Assert.False(DiagramViewportMath.NearSegment(15, 0, 0, 0, 10, 0, 2));
        Assert.True(DiagramViewportMath.NearSegment(1, 1, 0, 0, 0, 0, 2));
    }
}
