using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Core.Tests;

public sealed class DiagramReliabilityTests
{
    [Fact]
    public void Transient_deadline_can_be_retried_and_success_is_cached()
    {
        var calls = 0;
        var service = new DiagramService((source, theme, _) => ++calls == 1
            ? new(source, null, "Deadline") { FailureKind = DiagramFailureKind.Deadline }
            : new(source, "<svg/>", null) { Theme = theme });
        Assert.True(service.Render("erDiagram").CanRetry);
        var retry = service.Render("erDiagram");
        Assert.True(retry.Success);
        Assert.Same(retry, service.Render("erDiagram"));
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(DiagramFailureKind.Layout)]
    [InlineData(DiagramFailureKind.Svg)]
    [InlineData(DiagramFailureKind.NativeConversion)]
    public void Transient_failures_never_poison_retries(DiagramFailureKind kind)
    {
        var calls = 0;
        var service = new DiagramService((source, _, _) => { calls++; return new(source, null, "Failure") { FailureKind = kind }; });
        Assert.False(service.Render("source").Success); Assert.False(service.Render("source").Success);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void Deterministic_syntax_failure_is_cached_but_pre_cancelled_call_is_rejected()
    {
        var calls = 0;
        var service = new DiagramService((source, _, _) => { calls++; return new(source, null, "Syntax") { FailureKind = DiagramFailureKind.Syntax }; });
        var first = service.Render("invalid"); Assert.Same(first, service.Render("invalid"));
        Assert.Throws<OperationCanceledException>(() => service.Render("invalid", cancellationToken: new(true)));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Cancellation_after_renderer_completion_never_enters_cache()
    {
        using var cancellation = new CancellationTokenSource(); var calls = 0;
        var service = new DiagramService((source, _, _) => { calls++; cancellation.Cancel(); return new(source, "<svg/>", null); });
        Assert.Throws<OperationCanceledException>(() => service.Render("source", cancellationToken: cancellation.Token));
        Assert.True(service.Render("source").Success); Assert.Equal(2, calls);
    }

    [Fact]
    public void Input_limit_and_syntax_have_distinct_categories()
    {
        var service = new DiagramService();
        Assert.Equal(DiagramFailureKind.ResourceLimit, service.Render(new string('x', 100_001)).FailureKind);
        Assert.Equal(DiagramFailureKind.Syntax, service.Render("").FailureKind);
        var oversized = "erDiagram\n" + string.Join('\n', Enumerable.Range(0, 501).Select(n => $"E{n} ||--o{{ E{n + 1} : owns"));
        Assert.Equal(DiagramFailureKind.ResourceLimit, service.Render(oversized).FailureKind);
    }

    [Fact]
    public void Measured_ER_allowance_does_not_change_other_diagram_deadlines()
    {
        Assert.Equal(TimeSpan.FromSeconds(6), DiagramService.RenderingBudget("erDiagram\nA ||--o{ B : owns"));
        Assert.Equal(TimeSpan.FromSeconds(3), DiagramService.RenderingBudget("flowchart TD\nA --> B"));
    }
}
