using System.Diagnostics;
using System.Diagnostics.Tracing;

namespace LensDocsStudio.Native.Validation;

internal sealed class PreviewConstructionTrace(Stopwatch watch) : EventListener
{
    private readonly int threadId = Environment.CurrentManagedThreadId;
    public List<object> Stages { get; } = [];

    protected override void OnEventSourceCreated(EventSource source)
    {
        if (source.Name == "LensDocsStudio-Native-PreviewConstruction") EnableEvents(source, EventLevel.Verbose);
    }

    protected override void OnEventWritten(EventWrittenEventArgs args)
    {
        if (Environment.CurrentManagedThreadId == threadId && args.EventId == 1 && Stages.Count < 128)
            Stages.Add(new { Stage = args.Payload?[0]?.ToString(), SourceStart = args.Payload?[1], Milliseconds = watch.Elapsed.TotalMilliseconds });
    }
}
