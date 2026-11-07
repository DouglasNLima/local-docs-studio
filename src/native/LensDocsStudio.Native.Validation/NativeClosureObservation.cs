using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace LensDocsStudio.Native.Validation;

internal static class NativeClosureObservation
{
    internal static void Close(Application application, Window main, string output)
    {
        // The uninstrumented path preserves the original action and predicate.
        if (Environment.GetEnvironmentVariable("LENS_NATIVE_CLOSURE_TRACE") != "1")
        { main.Close(); NativeUiChecks.Wait(() => application.HasExited, "Saved application did not close."); return; }
        var path = Path.Combine(output, "saved-closure.jsonl");
        if (File.Exists(path)) throw new IOException("Closure evidence must be fresh.");
        var gate = new object(); var count = 0; var processId = application.ProcessId;
        void Record(string phase, object? value = null)
        {
            lock (gate)
            {
                if (++count > 24) throw new IOException("Closure evidence exceeds its bound.");
                File.AppendAllText(path, JsonSerializer.Serialize(new { tick = Stopwatch.GetTimestamp(), phase,
                    processId, frequency = Stopwatch.Frequency, value }) + Environment.NewLine);
            }
        }
        Record("close-request");
        try
        {
            var titleBar = main.TitleBar; var close = titleBar?.CloseButton;
            Record("uia-close-target", new { handle = main.Properties.NativeWindowHandle.Value.ToInt64(),
                pid = main.Properties.ProcessId.Value, main.FrameworkType, main.IsEnabled,
                titleBar = titleBar is not null, closeButton = close is not null,
                closeEnabled = close?.IsEnabled, closeOffscreen = close?.IsOffscreen,
                windowPattern = main.Patterns.Window.IsSupported });
        }
        catch (Exception error) when (error is not OutOfMemoryException) { Record("uia-target-error", new { error = error.GetType().FullName }); }
        var timer = Stopwatch.StartNew();
        using var cancellation = new CancellationTokenSource();
        var observer = Task.Run(async () =>
        {
            try
            {
                foreach (var second in new[] { 0, 5, 15 })
                {
                    var remaining = TimeSpan.FromSeconds(second) - timer.Elapsed;
                    if (remaining > TimeSpan.Zero) await Task.Delay(remaining, cancellation.Token);
                    if (cancellation.IsCancellationRequested || timer.Elapsed >= TimeSpan.FromSeconds(20)) break;
                    Record("native-sample", new { elapsedMs = timer.Elapsed.TotalMilliseconds, state = NativeRegressionObservation.Read(processId) });
                    if (second == 0) ObserveUi();
                    if (second == 5)
                    {
                        using var process = Process.GetProcessById(processId);
                        if (!process.HasExited)
                        {
                            Record("thread-capture-start", new { elapsedMs = timer.Elapsed.TotalMilliseconds });
                            NativeUiFailureDiagnostics.Capture(process, output);
                            Record("thread-capture-finished", new { elapsedMs = timer.Elapsed.TotalMilliseconds });
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception error) when (error is not OutOfMemoryException) { Record("observer-error", new { error = error.GetType().FullName, error.Message }); }
        });
        try
        {
            main.Close();
            var waitTimer = Stopwatch.StartNew(); // Original deadline starts after Close returns.
            Record("close-returned", new { elapsedMs = timer.Elapsed.TotalMilliseconds });
            NativeUiChecks.Wait(() => application.HasExited, "Saved application did not close.", waitTimer, TimeSpan.FromSeconds(20));
            Record("process-exited", new { elapsedMs = timer.Elapsed.TotalMilliseconds });
        }
        catch
        {
            Record("close-or-exit-failed", new { elapsedMs = timer.Elapsed.TotalMilliseconds });
            throw;
        }
        finally
        {
            cancellation.Cancel();
            // Evidence completion cannot turn a failed deadline into a pass.
            if (!observer.Wait(TimeSpan.FromSeconds(1))) Record("observer-still-running");
        }
        void ObserveUi()
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                process.Refresh(); if (process.HasExited || process.MainWindowHandle == 0) return;
                using var automation = new UIA3Automation { TransactionTimeout = TimeSpan.FromMilliseconds(250), ConnectionTimeout = TimeSpan.FromMilliseconds(250) };
                var owner = automation.FromHandle(process.MainWindowHandle);
                var status = owner.FindFirstDescendant(cf => cf.ByAutomationId("Status"));
                var cancel = owner.FindFirstDescendant(cf => cf.ByAutomationId("CancelOperation"));
                var text = status?.Name;
                Record("uia-after-close", new { elapsedMs = timer.Elapsed.TotalMilliseconds,
                    statusPresent = status is not null, cancelPresent = cancel is not null,
                    busyCloseRejected = text == "Wait for the current operation to finish, or cancel it before closing.",
                    savedStatus = text?.StartsWith("Saved ", StringComparison.Ordinal), cancelEnabled = cancel?.IsEnabled,
                    owner.IsEnabled, owner.IsOffscreen, owner.Properties.ProcessId.Value });
            }
            catch (Exception error) when (error is not OutOfMemoryException) { Record("uia-observation-error", new { error = error.GetType().FullName }); }
        }
    }
}
