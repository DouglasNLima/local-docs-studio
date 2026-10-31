using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace LensDocsStudio.Native.Validation;

// Disposable validation overlay only. No file IO occurs on the dispatcher until flushing.
internal sealed class NativePreviewLifecycleRecorder : EventListener
{
    private readonly object sync = new();
    private readonly Queue<object> events = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private long sequence;
    private long discarded;
    public Func<object>? Observe { get; set; }
    public Func<bool>? OnDispatcher { get; set; }
    public string Scenario { get; set; } = "initialisation";
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    protected override void OnEventSourceCreated(EventSource source)
    {
        if (source.Name == "LensDocsStudio-Native-PreviewConstruction") EnableEvents(source, EventLevel.Verbose);
    }

    protected override void OnEventWritten(EventWrittenEventArgs value)
    {
        if (value.Payload?.FirstOrDefault() is not string stage) return;
        if (!(stage.StartsWith("request/", StringComparison.Ordinal) || stage.StartsWith("shell/", StringComparison.Ordinal)
            || stage.StartsWith("theme-update-", StringComparison.Ordinal))) return;
        Record(stage, new { RequestArgument = value.Payload.Count > 1 ? value.Payload[1] : null });
    }

    public void Record(string name, object? detail = null)
    {
        // The observer is deliberately failure-isolated; diagnostic failure cannot turn acceptance green.
        try
        {
            object? observation = null;
            if (OnDispatcher?.Invoke() == true && Observe is { } observe)
            {
                try { observation = observe(); }
                catch (Exception error) { observation = new { ObservationError = error.ToString() }; }
            }
            lock (sync)
            {
                if (events.Count == 8192) { events.Dequeue(); discarded++; }
                events.Enqueue(new { Sequence = ++sequence, MonotonicTicks = Stopwatch.GetTimestamp(),
                    ElapsedMilliseconds = clock.Elapsed.TotalMilliseconds, Process = Environment.ProcessId,
                    ManagedThread = Environment.CurrentManagedThreadId, WindowsThread = GetCurrentThreadId(),
                    Scenario, Name = name, Detail = detail, State = observation });
            }
        }
        catch (Exception error) { Console.Error.WriteLine("Preview diagnostic event could not be recorded: " + error.GetType().Name); }
    }

    public void Flush(string path)
    {
        try
        {
            object[] rows; long removed;
            lock (sync) { rows = events.ToArray(); removed = discarded; }
            File.WriteAllText(path, JsonSerializer.Serialize(new { Stopwatch.Frequency, Discarded = removed,
                Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                Wpf = typeof(System.Windows.Window).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                    .Cast<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion,
                Events = rows },
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception error) { Console.Error.WriteLine("Preview diagnostic flush failed: " + error); }
    }
}
