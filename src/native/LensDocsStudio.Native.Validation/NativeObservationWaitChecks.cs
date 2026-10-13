using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace LensDocsStudio.Native.Validation;

internal static class NativeObservationWaitChecks
{
    public static void Run(string output)
    {
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        foreach (var code in new[] { unchecked((int)0x80131505), unchecked((int)0x800705B4) })
        foreach (var wrapped in new[] { false, true })
        {
            var attempts = 0;
            Exception error = new COMException("Deterministic UIA observation timeout", code);
            if (wrapped) error = new TimeoutException("UIA Timeout", error);
            NativeUiChecks.Wait(() => { if (++attempts == 1) throw error; return true; }, "The provider did not become ready.", 1);
            if (attempts != 2) throw new IOException("The transient observation was not retried exactly once.");
            checks.Add($"Known timeout {code:X8}, wrapped={wrapped}, is retried as an observation");
        }
        foreach (var error in new Exception[]
        {
            new TimeoutException("Unrelated timeout"),
            new TimeoutException("Wrong provider error", new COMException("Access denied", unchecked((int)0x80070005))),
            new InvalidOperationException("Non-timeout wrapper", new COMException("Timeout", unchecked((int)0x80131505))),
            new ArgumentException("Invalid observation")
        })
        {
            var attempts = 0; Exception? caught = null;
            try { NativeUiChecks.Wait(() => { attempts++; throw error; }, "Unexpected retry", 1); }
            catch (Exception actual) { caught = actual; }
            if (!ReferenceEquals(caught, error) || attempts != 1) throw new IOException("An unrelated failure was swallowed or retried.");
            checks.Add($"Unrelated {error.GetType().Name} propagates unchanged without retry");
        }
        var persistent = new TimeoutException("UIA Timeout", new COMException("Timeout", unchecked((int)0x80131505)));
        var observed = 0; Exception? deadlineError = null;
        var timer = Stopwatch.StartNew();
        try { NativeUiChecks.Wait(() => { observed++; throw persistent; }, "Original observation deadline", 1); }
        catch (Exception error) { deadlineError = error; }
        if (deadlineError is not InvalidOperationException { Message: "Original observation deadline" }
            || !ReferenceEquals(deadlineError.InnerException, persistent) || observed < 2 || timer.Elapsed < TimeSpan.FromSeconds(1))
            throw new IOException("Persistent timeouts lost the original deadline failure or provider exception.");
        checks.Add("Persistent timeout fails at the original deadline and retains its last provider exception");
        File.WriteAllText(Path.Combine(output, "native-observation-wait.json"), JsonSerializer.Serialize(new
        {
            Passed = true, Checks = checks, DeadlineSeconds = 1, ElapsedMilliseconds = timer.Elapsed.TotalMilliseconds,
            PersistentAttempts = observed, InputActionsExecuted = 0,
            Limitation = "Injected provider observations; real installer provider readiness remains a hosted GUI check."
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
