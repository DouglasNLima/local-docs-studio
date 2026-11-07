using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Threading;
using LensDocsStudio.Native.Validation;

namespace LensDocsStudio.Native.UxProbe;

// Opt-in, test-only startup evidence. No source text, dispatcher polling or layout request.
internal sealed class StartupTrace : IDisposable
{
    private readonly bool enabled = Environment.GetEnvironmentVariable("LENS_NATIVE_STARTUP_TRACE") == "1";
    private readonly string file;
    private readonly Dispatcher dispatcher;
    private readonly CancellationTokenSource stop = new();
    private readonly object gate = new();
    private int records, dispatcherRecords;
    private readonly Task? observer;
    private string phase = "created";
    internal StartupTrace(string output, Dispatcher dispatcher)
    {
        file = Path.Combine(output, "startup.jsonl"); this.dispatcher = dispatcher;
        if (!enabled) return;
        if (File.Exists(file)) throw new IOException("Startup evidence must be fresh.");
        Mark("observer-started", new { Environment.ProcessId, Stopwatch.Frequency, Environment.ProcessorCount,
            apartment = Thread.CurrentThread.GetApartmentState().ToString(), managedThread = Environment.CurrentManagedThreadId,
            osContrast = NativeTestHighContrast.IsEnabledNow });
        dispatcher.Hooks.OperationPosted += Posted;
        dispatcher.Hooks.OperationStarted += Started;
        dispatcher.Hooks.OperationCompleted += Completed;
        observer = Task.Run(async () =>
        {
            var clock = Stopwatch.StartNew();
            try
            {
                foreach (var second in new[] { 1, 5, 30, 120, 220 })
                {
                    var remaining = TimeSpan.FromSeconds(second) - clock.Elapsed;
                    if (remaining > TimeSpan.Zero) await Task.Delay(remaining, stop.Token);
                    if (stop.IsCancellationRequested) break;
                    Record("native-observation", new { elapsedMs = clock.Elapsed.TotalMilliseconds,
                        lastPhase = Volatile.Read(ref phase), state = NativeRegressionObservation.Read(Environment.ProcessId) });
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception error) when (error is not OutOfMemoryException) { Record("observer-error", new { error = error.GetType().FullName }); }
        });
    }
    internal void Mark(string name, object? value = null)
    { if (!enabled) return; Volatile.Write(ref phase, name); Record(name, value); }
    private void Record(string boundary, object? value)
    {
        lock (gate)
        {
            if (++records > 128) throw new IOException("Startup observation budget exceeded.");
            var line = JsonSerializer.Serialize(new { tick = Stopwatch.GetTimestamp(), boundary, value });
            if (line.Length > 32768) throw new IOException("Startup record exceeds its bound.");
            File.AppendAllText(file, line + Environment.NewLine);
        }
    }
    private void Operation(string boundary, DispatcherHookEventArgs args)
    {
        if (Interlocked.Increment(ref dispatcherRecords) > 60) return;
        Record(boundary, new { operation = RuntimeHelpers.GetHashCode(args.Operation), priority = args.Operation.Priority.ToString(),
            status = args.Operation.Status.ToString(), managedThread = Environment.CurrentManagedThreadId });
    }
    private void Posted(object? sender, DispatcherHookEventArgs args) => Operation("dispatcher-posted", args);
    private void Started(object? sender, DispatcherHookEventArgs args) => Operation("dispatcher-started", args);
    private void Completed(object? sender, DispatcherHookEventArgs args) => Operation("dispatcher-completed", args);
    public void Dispose()
    {
        if (enabled)
        {
            dispatcher.Hooks.OperationPosted -= Posted; dispatcher.Hooks.OperationStarted -= Started; dispatcher.Hooks.OperationCompleted -= Completed;
            stop.Cancel();
            if (observer?.Wait(TimeSpan.FromSeconds(1)) == false) Record("observer-still-running", null);
        }
        stop.Dispose();
    }
}
