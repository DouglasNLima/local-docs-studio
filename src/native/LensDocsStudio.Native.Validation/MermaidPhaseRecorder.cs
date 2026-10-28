using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace LensDocsStudio.Native.Validation;

internal sealed class MermaidPhaseRecorder : IDisposable
{
    private readonly ConcurrentQueue<object> phases = new();
    private readonly ActivityListener listener;
    private readonly int uiThread = Environment.CurrentManagedThreadId;
    public MermaidPhaseRecorder()
    {
        listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "LensDocsStudio.Mermaid",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => phases.Enqueue(new { Phase = activity.OperationName, StartedUtc = activity.StartTimeUtc,
                Milliseconds = activity.Duration.TotalMilliseconds, SpanId = activity.SpanId.ToHexString(), ParentSpanId = activity.ParentSpanId.ToHexString(),
                Tags = activity.TagObjects.ToDictionary(pair => pair.Key, pair => pair.Value) })
        };
        ActivitySource.AddActivityListener(listener);
    }
    public void Write(string path) => File.WriteAllText(path, JsonSerializer.Serialize(new
    {
        UiThreadId = uiThread,
        Method = "Opt-in ActivitySource scopes; timings are inclusive and nested stages must not be added twice. Thread start/end tags distinguish dispatcher and workers. Per-thread allocations are deliberately unavailable across a thread change. No source text, document identity, paths or UIA transport is recorded.",
        Boundaries = new[] {
            "Source scanners; semantic/correspondence validation; pinned parsing before the existing graph layout callback; existing graph layout; SVG generation after layout; SVG sanitisation; SharpVectors Read; native drawing freeze/wrapper; geometry correspondence; overlay creation; native first presentation; first interaction; selection/inspector update; private source validation; cleanup.",
            "Mermaider 0.15.1 exposes graph layout callbacks for Flowchart/State/Class/ER, not the Sequence pipeline. Sequence parsing/layout/SVG remains a labelled renderer.pipeline span; no parser-only time is invented.",
            "SharpVectors Read creates native DrawingGroup geometry internally. That read is timed together; final WPF freeze/image wrapping is separate. Instrumentation itself does not alter the converter, graph engine or production budgets."
        },
        Phases = phases.ToArray()
    }, new JsonSerializerOptions { WriteIndented = true }));
    public void Dispose() => listener.Dispose();
}
