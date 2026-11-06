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
            var temporary = Path.Combine(Output!, "phase.tmp");
            File.WriteAllText(temporary, json);
            File.Move(temporary, Path.Combine(Output!, "phase.json"), true);
        }
    }
}
