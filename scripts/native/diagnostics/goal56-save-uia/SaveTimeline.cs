using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace LensDocsStudio.Core.Diagnostics;

// Copied into disposable diagnostic builds only. Never included by a release project.
public static class SaveTimeline
{
    private static readonly string? directory = Environment.GetEnvironmentVariable("LENS_DIAGNOSTIC_DIRECTORY");
    public static readonly string Session = Environment.GetEnvironmentVariable("LENS_DIAGNOSTIC_SESSION") ?? "unassigned";
    private static readonly Channel<object> events = Channel.CreateUnbounded<object>(new() { SingleReader = true });
    private static readonly ConcurrentDictionary<string, string> pending = new();
    private static readonly AsyncLocal<string?> operation = new();
    private static readonly AsyncLocal<string?> save = new();
    private static readonly MemoryMappedFile? mapping = OpenMapping();
    private static readonly MemoryMappedViewAccessor? shared = mapping?.CreateViewAccessor();
    private static readonly Task writer = Task.Run(WriteEventsAsync);
    private static long sequence, ordinal, markerVersion;
    public static Func<object>? NativeState { get; set; }
    public static string? CurrentOperation => operation.Value;
    public static string? CurrentSave => save.Value;
    public static string[] Pending => pending.OrderBy(pair => pair.Key).Select(pair => pair.Key + ":" + pair.Value).ToArray();
    public static bool Saving => pending.Values.Contains("save");

    private static MemoryMappedFile? OpenMapping()
    {
        if (directory is null || !OperatingSystem.IsWindows()) return null;
        return MemoryMappedFile.CreateOrOpen("Local\\LensGoal56-" + Session, 1024);
    }
    public static void Mark(string value)
    {
        if (shared is null) return;
        // A sequence lock retains an inconsistent result instead of waiting on the app.
        var version = Interlocked.Add(ref markerVersion, 2);
        shared.Write(0, version - 1);
        Thread.MemoryBarrier();
        var bytes = Encoding.ASCII.GetBytes(value);
        shared.WriteArray(16, bytes, 0, bytes.Length); shared.Write(8, bytes.Length);
        Thread.MemoryBarrier();
        shared.Write(0, version);
    }
    public static string Marker()
    {
        if (shared is null) return "unavailable";
        var first = shared.ReadInt64(0); Thread.MemoryBarrier(); var count = shared.ReadInt32(8);
        if ((first & 1) != 0 || count is < 0 or > 128) return "inconsistent";
        var bytes = new byte[count]; shared.ReadArray(16, bytes, 0, count);
        Thread.MemoryBarrier();
        return first == shared.ReadInt64(0) ? Encoding.ASCII.GetString(bytes) : "inconsistent";
    }
    public static int BarrierState => shared?.ReadInt32(256) ?? -1;
    public static void Event(string phase, object? data = null)
    {
        if (directory is null) return;
        var number = Interlocked.Increment(ref sequence);
        if (number > 30000) return;
        events.Writer.TryWrite(new { Sequence = number, Utc = DateTimeOffset.UtcNow, Tick = Stopwatch.GetTimestamp(),
            Frequency = Stopwatch.Frequency, Process = Environment.ProcessId, Thread = Environment.CurrentManagedThreadId,
            Session, Operation = operation.Value, Save = save.Value, Marker = Marker(), Phase = phase, Data = data });
    }
    public static IDisposable Begin(string kind)
    {
        var parent = operation.Value; var parentSave = save.Value;
        var id = $"p{Environment.ProcessId}-op{Interlocked.Increment(ref ordinal):D5}";
        operation.Value = id; if (kind == "save") save.Value = id;
        pending[id] = kind; Event(kind + ".started", new { Parent = parent, Trigger = Marker() });
        return new Scope(() => { Event(kind + ".ended"); pending.TryRemove(id, out _); operation.Value = parent; save.Value = parentSave; });
    }
    public static object Error(Exception error) => new { Type = error.GetType().FullName, error.Message,
        HResult = $"0x{error.HResult:X8}", Stack = error.ToString() };
    public static async Task PersistenceBarrierAsync(string name)
    {
        if (name != "preferences.json" || CurrentSave is null || Environment.GetEnvironmentVariable("LENS_DIAGNOSTIC_MODE") != "barrier" || !OperatingSystem.IsWindows()) return;
        using var release = new EventWaitHandle(false, EventResetMode.ManualReset, "Local\\LensGoal56-release-" + Session);
        Event("synthetic-barrier.entered", new { name }); shared?.Write(256, 1);
        var released = await Task.Run(() => release.WaitOne(TimeSpan.FromSeconds(10))).ConfigureAwait(false);
        Event("synthetic-barrier.released", new { Released = released }); shared?.Write(256, 2);
        if (!released) throw new TimeoutException("The diagnostic-only persistence barrier was not released.");
    }
    public static void ReleaseBarrier()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var release = new EventWaitHandle(false, EventResetMode.ManualReset, "Local\\LensGoal56-release-" + Session); release.Set();
    }
    public static void Flush()
    {
        // Only called after a test/failure, never before an acceptance operation.
        var checkpoint = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        events.Writer.TryWrite(checkpoint); checkpoint.Task.Wait(TimeSpan.FromSeconds(2));
    }
    private static async Task WriteEventsAsync()
    {
        if (directory is null) return;
        Directory.CreateDirectory(directory);
        await using var file = new StreamWriter(Path.Combine(directory, $"timeline-{Environment.ProcessId}.jsonl"), false, new UTF8Encoding(false)) { AutoFlush = true };
        await file.WriteLineAsync(JsonSerializer.Serialize(new { Phase = "process.identity", Session, Process = Environment.ProcessId,
            Utc = DateTimeOffset.UtcNow, Runtime = RuntimeInformation.FrameworkDescription, OS = RuntimeInformation.OSDescription }));
        await foreach (var item in events.Reader.ReadAllAsync())
        {
            if (item is TaskCompletionSource checkpoint) { await file.FlushAsync(); checkpoint.TrySetResult(); }
            else await file.WriteLineAsync(JsonSerializer.Serialize(item));
        }
    }
    private sealed class Scope(Action close) : IDisposable { public void Dispose() => close(); }
}
