using System.Diagnostics;
using System.Runtime.CompilerServices;
using LensDocsStudio.Core.Diagnostics;

namespace LensDocsStudio.Core.Tests;

internal static class RenderingProbe
{
    private static ActivityListener? listener;
    [ModuleInitializer]
    internal static void Initialise()
    {
        listener = new ActivityListener {
            ShouldListenTo = source => source.Name == "LensDocsStudio.Mermaid",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => SaveTimeline.Event("render.phase.started", new { activity.OperationName, activity.Id, activity.ParentId }),
            ActivityStopped = activity => SaveTimeline.Event("render.phase.stopped", new { activity.OperationName, activity.Id, activity.ParentId, Milliseconds = activity.Duration.TotalMilliseconds, Tags = activity.TagObjects.ToDictionary(pair => pair.Key, pair => pair.Value) })
        };
        ActivitySource.AddActivityListener(listener);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => SaveTimeline.Flush();
    }
}
