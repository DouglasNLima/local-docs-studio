using System.Diagnostics;

namespace LensDocsStudio.Core.Rendering;

/// <summary>Opt-in, source-free timing boundaries. No listener means no timing objects or logs.</summary>
public static class MermaidDiagnostics
{
    public static ActivitySource Activities { get; } = new("LensDocsStudio.Mermaid");
    public static IDisposable? Measure(string phase)
    {
        var activity = Activities.StartActivity(phase, ActivityKind.Internal);
        return activity is null ? null : new Measurement(activity);
    }
    private sealed class Measurement : IDisposable
    {
        private Activity? activity;
        private readonly int thread = Environment.CurrentManagedThreadId;
        private readonly long allocated = GC.GetAllocatedBytesForCurrentThread();
        public Measurement(Activity value) { activity = value; value.SetTag("thread.start", thread); }
        public void Dispose()
        {
            var current = Interlocked.Exchange(ref activity, null); if (current is null) return;
            var end = Environment.CurrentManagedThreadId; current.SetTag("thread.end", end);
            if (end == thread) current.SetTag("allocated.current-thread.bytes", GC.GetAllocatedBytesForCurrentThread() - allocated);
            else current.SetTag("allocated.current-thread.unavailable", true);
            current.Dispose();
        }
    }
}
