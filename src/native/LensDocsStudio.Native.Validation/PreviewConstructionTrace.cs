using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Runtime.InteropServices;

namespace LensDocsStudio.Native.Validation;

internal sealed class PreviewConstructionTrace(Stopwatch watch) : EventListener
{
    private readonly int threadId = Environment.CurrentManagedThreadId;
    private readonly double? initialCpu = ReadThreadCpuMilliseconds();
    private readonly long initialAllocation = GC.GetAllocatedBytesForCurrentThread();
    public List<object> Stages { get; } = [];
    public double? ThreadCpuMilliseconds => Environment.CurrentManagedThreadId == threadId ? ReadThreadCpuMilliseconds() - initialCpu : null;
    public long? AllocatedBytes => Environment.CurrentManagedThreadId == threadId ? GC.GetAllocatedBytesForCurrentThread() - initialAllocation : null;

    protected override void OnEventSourceCreated(EventSource source)
    {
        if (source.Name == "LensDocsStudio-Native-PreviewConstruction") EnableEvents(source, EventLevel.Verbose);
    }

    protected override void OnEventWritten(EventWrittenEventArgs args)
    {
        if (Environment.CurrentManagedThreadId == threadId && args.EventId == 1 && Stages.Count < 128)
            Stages.Add(new { Stage = args.Payload?[0]?.ToString(), SourceStart = args.Payload?[1], Milliseconds = watch.Elapsed.TotalMilliseconds,
                ThreadCpuMilliseconds, AllocatedBytes, GarbageCollections = Enumerable.Range(0, GC.MaxGeneration + 1).Select(GC.CollectionCount).ToArray() });
    }

    private static double? ReadThreadCpuMilliseconds()
    {
        // The current-thread pseudo-handle is borrowed, not owned. Times count
        // scheduled kernel/user work in 100 ns units, independently of wall time.
        // Windows accounting is coarser than Stopwatch: short-phase CPU deltas
        // are diagnostic context, never a replacement for the input deadline.
        return GetThreadTimes(GetCurrentThread(), out _, out _, out var kernel, out var user) ? (kernel + user) / 10000d : null;
    }
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetThreadTimes(IntPtr thread, out long creation, out long exit, out long kernel, out long user);
}
