using System.Diagnostics;
using System.Text.Json;

namespace LensDocsStudio.Native.Validation;

/// <summary>Opt-in, bounded stack-only diagnostics for the hosted OS-transition case.</summary>
internal sealed class ContinuousTransitionWatch : IDisposable
{
    private readonly CancellationTokenSource stop = new();
    private readonly Task observer;
    private readonly string output;
    private readonly long started = Stopwatch.GetTimestamp();
    private long pulse = Stopwatch.GetTimestamp();
    private string phase = "starting";

    public ContinuousTransitionWatch(string output, string stackTool)
    {
        this.output = output;
        var token = stop.Token; var uiThread = Environment.CurrentManagedThreadId;
        observer = Task.Run(async () =>
        {
            try { await Task.Delay(TimeSpan.FromSeconds(60), token); }
            catch (OperationCanceledException) { return; }
            var snapshot = new
            {
                Status = "FAIL", Phase = Volatile.Read(ref phase), UiManagedThread = uiThread,
                CaseMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                LastInputGapMilliseconds = Stopwatch.GetElapsedTime(Interlocked.Read(ref pulse)).TotalMilliseconds,
                Deadline = "Independent 60-second hard bound for a non-interruptible validation phase; existing dispatcher deadlines remain 15 seconds",
                Scope = "Validation process only; stack method names, no heap dump or authored document content"
            };
            File.WriteAllText(Path.Combine(output, "transition-blocked.json"), JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
            try
            {
                var start = new ProcessStartInfo(stackTool) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                start.ArgumentList.Add("report"); start.ArgumentList.Add("--process-id"); start.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                using var report = Process.Start(start)!;
                var stdout = report.StandardOutput.ReadToEndAsync(); var stderr = report.StandardError.ReadToEndAsync();
                try { await report.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15)); }
                catch (TimeoutException) { report.Kill(entireProcessTree: true); await report.WaitForExitAsync(); }
                var text = await stdout; var errors = await stderr;
                File.WriteAllText(Path.Combine(output, "transition-managed-stacks.txt"), text[..Math.Min(text.Length, 65536)]);
                File.WriteAllText(Path.Combine(output, "transition-stack-tool.txt"), errors[..Math.Min(errors.Length, 4096)]);
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(output, "transition-stack-error.txt"), error.GetType().Name); }
            // Do not let an uninterruptible UI/OS call turn into a green liveness
            // check. The disposable runner owns restoration after this failed exit.
            Environment.Exit(1);
        });
    }

    public void Pulse() => Interlocked.Exchange(ref pulse, Stopwatch.GetTimestamp());
    public void Mark(string value)
    {
        if (value is not ("changing-os" or "os-changed" or "waiting-for-notification" or "waiting-for-preview" or "preview-ready" or "disabling-os" or "disabled-preview-ready" or "restoring-os" or "os-restored"))
            throw new ArgumentException("Unknown transition diagnostic phase.");
        Volatile.Write(ref phase, value);
        File.AppendAllText(Path.Combine(output, "transition-phases.txt"), value + " " + Stopwatch.GetElapsedTime(started).TotalMilliseconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + Environment.NewLine);
    }
    public void Dispose()
    {
        stop.Cancel();
        _ = observer.ContinueWith(_ => stop.Dispose(), TaskScheduler.Default);
    }
}
