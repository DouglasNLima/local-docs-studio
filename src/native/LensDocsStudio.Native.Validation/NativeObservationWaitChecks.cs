using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace LensDocsStudio.Native.Validation;

internal static class NativeObservationWaitChecks
{
    public static void Run(string output)
    {
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        NativeLaunchReadinessChecks.Run(output);
        checks.Add("Shared launch budget distinguishes transient Transform reads from unsupported patterns, wrong owners, exited/unresponsive processes and uncertain actions; Resize is sent at most once");
        NativeExportObservationChecks.Run(output);
        checks.Add("Real PDF bytes and separate file/UI observations reject missing, wrong, incomplete, empty, error, cancelled and stale-provider outcomes");
        NativeUiChecks.CheckWorkspaceContextObservation(output);
        checks.Add("Transient session read locks do not imply lost roots; persistent root mismatches still fail without changing bytes or input actions");
        NativeUiChecks.CheckDesktopStateObservation(output);
        checks.Add("Locked/missing desktop state generations are observed within the original deadline; wrong choices and malformed JSON remain failures without input repetition");
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
        var win32Attempts = 0;
        var win32Timeout = new Win32Exception(1460);
        NativeUiChecks.Wait(() => { if (++win32Attempts == 1) throw win32Timeout; return true; }, "The Win32 provider did not become ready.", 1);
        if (win32Attempts != 2) throw new IOException("The native Win32 timeout was not retried exactly once.");
        checks.Add("Win32 ERROR_TIMEOUT (1460) is retried as an observation despite its generic HRESULT");
        foreach (var error in new Exception[]
        {
            new TimeoutException("Unrelated timeout"),
            new TimeoutException("Wrong provider error", new COMException("Access denied", unchecked((int)0x80070005))),
            new InvalidOperationException("Non-timeout wrapper", new COMException("Timeout", unchecked((int)0x80131505))),
            new Win32Exception(5),
            new Win32Exception(258),
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
        timer.Stop();
        var win32Observed = 0; Exception? win32DeadlineError = null;
        var win32Timer = Stopwatch.StartNew();
        try { NativeUiChecks.Wait(() => { win32Observed++; throw win32Timeout; }, "Original Win32 observation deadline", 1); }
        catch (Exception error) { win32DeadlineError = error; }
        if (win32DeadlineError is not InvalidOperationException { Message: "Original Win32 observation deadline" }
            || !ReferenceEquals(win32DeadlineError.InnerException, win32Timeout) || win32Observed < 2 || win32Timer.Elapsed < TimeSpan.FromSeconds(1))
            throw new IOException("Persistent Win32 timeouts lost the original deadline failure or provider exception.");
        checks.Add("Persistent Win32 ERROR_TIMEOUT fails at the original deadline and retains the provider exception");
        win32Timer.Stop();
        File.WriteAllText(Path.Combine(output, "native-observation-wait.json"), JsonSerializer.Serialize(new
        {
            Passed = true, Checks = checks, DeadlineSeconds = 1, ElapsedMilliseconds = timer.Elapsed.TotalMilliseconds,
            PersistentAttempts = observed, InputActionsExecuted = 0,
            Win32DeadlineSeconds = 1, Win32ElapsedMilliseconds = win32Timer.Elapsed.TotalMilliseconds, Win32PersistentAttempts = win32Observed,
            Limitation = "Injected provider observations; real installer provider readiness remains a hosted GUI check."
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
