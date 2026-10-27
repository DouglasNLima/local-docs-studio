using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Core.Tests;

public sealed class PreviewLoadingStateTests
{
    private static readonly PreviewLoadingContext A = new(Guid.NewGuid(), 1, "Light/Auto/False", "Split");
    private static readonly TimeSpan Start = TimeSpan.FromSeconds(1);

    [Fact]
    public void Fast_preview_has_no_indicator_flash()
    {
        var state = new PreviewLoadingState(); var ticket = state.Queue(A, false, Start);
        Assert.True(state.MasksContent); Assert.False(state.IsVisible(Start + TimeSpan.FromMilliseconds(199)));
        state.Advance(ticket, PreviewLoadingPhase.Completed);
        Assert.False(state.IsVisible(Start + TimeSpan.FromSeconds(1))); Assert.False(state.MasksContent);
    }

    [Theory]
    [InlineData(PreviewLoadingPhase.Completed)]
    [InlineData(PreviewLoadingPhase.Failed)]
    [InlineData(PreviewLoadingPhase.Cancelled)]
    public void Slow_request_stops_on_every_terminal_outcome(PreviewLoadingPhase outcome)
    {
        var state = new PreviewLoadingState(); var ticket = state.Start(A, false, Start);
        Assert.True(state.IsVisible(Start + PreviewLoadingState.VisibilityDelay));
        state.Advance(ticket, outcome);
        Assert.False(state.Busy); Assert.False(state.IsVisible(Start + TimeSpan.FromSeconds(5)));
        Assert.False(state.Advance(ticket, PreviewLoadingPhase.Diagrams));
    }

    [Fact]
    public void Stale_document_completion_cannot_hide_new_loading_state()
    {
        var state = new PreviewLoadingState(); var a = state.Start(A, true, Start);
        var b = state.Queue(A with { Document = Guid.NewGuid() }, false, Start + TimeSpan.FromSeconds(1));
        Assert.False(state.Advance(a, PreviewLoadingPhase.Completed));
        Assert.True(state.MasksContent); Assert.True(state.Busy); Assert.Equal(b, state.Generation);
    }

    [Fact]
    public void Rapid_revisions_preserve_refresh_content_and_busy_delay()
    {
        var state = new PreviewLoadingState(); var old = state.Start(A, true, Start);
        var context = A with { Revision = 2 }; var next = state.Queue(context, true, Start + TimeSpan.FromMilliseconds(150));
        Assert.False(state.Advance(old, PreviewLoadingPhase.Cancelled));
        Assert.Equal(next, state.Start(context, true, Start + TimeSpan.FromMilliseconds(180)));
        Assert.True(state.IsVisible(Start + TimeSpan.FromMilliseconds(201)));
        Assert.True(state.Refresh); Assert.False(state.MasksContent);
    }

    [Fact]
    public void Appearance_layout_or_close_invalidates_previous_request()
    {
        var state = new PreviewLoadingState(); var old = state.Start(A, true, Start);
        var next = state.Start(A with { Appearance = "HighContrast", Layout = "Preview" }, true, Start);
        Assert.NotEqual(old, next); Assert.False(state.Advance(old, PreviewLoadingPhase.Failed));
        state.Reset(); Assert.False(state.Advance(next, PreviewLoadingPhase.Completed)); Assert.Null(state.Context);
    }

    [Fact]
    public void Explicit_retry_of_same_revision_owns_a_new_generation()
    {
        var state = new PreviewLoadingState(); var old = state.Start(A, false, Start);
        var retry = state.Start(A, false, Start);
        Assert.NotEqual(old, retry); Assert.False(state.Advance(old, PreviewLoadingPhase.Cancelled));
        Assert.True(state.Busy);
    }
}
