using System.Collections.Concurrent;
using System.Diagnostics;
using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Core.Tests;

public sealed class MermaidDiagnosticsTests
{
    [Fact]
    public void Tracing_preserves_exact_source_canonical_output_and_activity_ownership()
    {
        const string source = "flowchart TD\r\n%% Exact Unicode context\r\nA[\"Résumé · café\"] --> B[Complete]\r\n";
        var baseline = new DiagramService().Render(source);
        var spans = new ConcurrentQueue<string>(); var previous = Activity.Current;
        using var listener = new ActivityListener
        {
            ShouldListenTo = value => value.Name == "LensDocsStudio.Mermaid",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => spans.Enqueue(activity.OperationName)
        };
        ActivitySource.AddActivityListener(listener);
        using var cancellation = new CancellationTokenSource();
        var traced = new DiagramService().Render(source, cancellationToken: cancellation.Token);
        Assert.True(baseline.Success, baseline.Error); Assert.True(traced.Success, traced.Error);
        Assert.Equal(source, traced.Source); Assert.Equal(baseline.Svg, traced.Svg);
        Assert.Same(previous, Activity.Current);
        Assert.Contains("renderer.parse-before-layout", spans); Assert.Contains("renderer.layout.graph", spans);
        Assert.Contains("renderer.svg-after-layout", spans); Assert.Contains("renderer.pipeline", spans);
    }
}
