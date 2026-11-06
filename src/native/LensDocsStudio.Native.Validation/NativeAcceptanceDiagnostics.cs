using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace LensDocsStudio.Native.Validation;

/// <summary>Opt-in phase evidence for disposable acceptance processes; never product telemetry.</summary>
internal static class NativeAcceptanceDiagnostics
{
    private static readonly object Gate = new();
    private static readonly string? Output = Environment.GetEnvironmentVariable("LENS_NATIVE_ACCEPTANCE_TRACE");
    private static readonly long Started = Stopwatch.GetTimestamp();
    private static int sequence;
    public static bool Enabled => !string.IsNullOrEmpty(Output);

    public static void CheckJournal(string output)
    {
        if (!Enabled || Path.GetFullPath(Output!) != Path.GetFullPath(output) || Directory.Exists(output))
            throw new ArgumentException("Use a fresh trace directory matching LENS_NATIVE_ACCEPTANCE_TRACE.");
        Mark("control/starting");
        using var stop = new CancellationTokenSource();
        using var opened = new ManualResetEventSlim();
        var reads = 0;
        var reader = Task.Run(() =>
        {
            // Keep a real observer handle open throughout the writes. Replacing
            // the latest-state file failed under this Windows sharing condition.
            using var stream = new FileStream(Path.Combine(output, "phases.jsonl"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            opened.Set();
            var buffer = new byte[16384];
            while (!stop.IsCancellationRequested)
            {
                stream.Seek(Math.Max(0, stream.Length - buffer.Length), SeekOrigin.Begin);
                if (stream.Read(buffer) > 0) Interlocked.Increment(ref reads);
                Thread.Yield();
            }
        });
        try
        {
            if (!opened.Wait(TimeSpan.FromSeconds(5))) throw new IOException("Diagnostic observer did not start.");
            for (var index = 0; index < 1000; index++) Mark("control/concurrent-write", new { Index = index });
        }
        finally { stop.Cancel(); if (!reader.Wait(TimeSpan.FromSeconds(5))) throw new IOException("Diagnostic observer did not stop."); }
        var records = File.ReadAllLines(Path.Combine(output, "phases.jsonl"));
        if (records.Length != 1001 || reads == 0) throw new IOException("Diagnostic journal lost records or did not exercise concurrent reads.");
        for (var index = 0; index < records.Length; index++)
        {
            using var record = JsonDocument.Parse(records[index]);
            if (record.RootElement.GetProperty("Sequence").GetInt32() != index + 1 || records[index].Length > 4096)
                throw new IOException("Diagnostic records are incomplete or exceed their bound.");
        }
        Console.WriteLine("Diagnostic journal checks passed: 1,001 complete records with a concurrent Windows observer handle.");
    }

    public static void Mark(string phase, object? observation = null)
    {
        if (!Enabled) return;
        if (phase.Length > 160) throw new ArgumentException("Acceptance diagnostic phase is too long.");
        lock (Gate)
        {
            if (++sequence > 2048) throw new IOException("Acceptance diagnostic event budget exceeded.");
            Directory.CreateDirectory(Output!);
            var json = JsonSerializer.Serialize(new
            {
                Sequence = sequence, Phase = phase, ProcessId = Environment.ProcessId,
                ManagedThread = Environment.CurrentManagedThreadId,
                ElapsedMilliseconds = Stopwatch.GetElapsedTime(Started).TotalMilliseconds,
                TimestampUtc = DateTimeOffset.UtcNow, Observation = observation,
                HarnessSource = typeof(NativeAcceptanceDiagnostics).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                ProductSource = typeof(LensDocsStudio.Native.MainWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            });
            if (json.Length > 4096) throw new IOException("Acceptance diagnostic observation exceeds its bounded size.");
            File.AppendAllText(Path.Combine(Output!, "phases.jsonl"), json + Environment.NewLine);
        }
    }
}
