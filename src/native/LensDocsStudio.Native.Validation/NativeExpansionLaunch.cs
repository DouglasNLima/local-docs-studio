using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace LensDocsStudio.Native.Validation;

internal static class NativeExpansionLaunch
{
    public static Window Prepare(Application app, UIA3Automation automation, Stopwatch timer,
        string fixture, string output, int ordinal, bool requireHighContrast)
    {
        var budget = TimeSpan.FromSeconds(25);
        var observations = new List<object>();
        var expectedSource = File.ReadAllText(fixture);
        var fileBefore = HashFile();
        var ownerExpectedHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(expectedSource)));
        string? ownerObservedHash = null, failure = null, captureError = null;
        var resizeCount = 0; var passed = false;
        Process? process = null;
        Window? main = null; nint handle = 0;
        try
        {
            process = Process.GetProcessById(app.ProcessId);
            NativeLaunchReadiness.ResizeOnce(timer, budget, Observe, () =>
            {
                var pattern = Provider(() => main!.Patterns.Transform.Pattern);
                if (!Provider(() => pattern.CanResize.Value)) throw new InvalidOperationException("The real Transform pattern does not support Resize.");
                return pattern;
            }, pattern => { LimitProviderCall(); resizeCount++; pattern.Resize(1280, 800); }, Record);
            if (HashFile() != fileBefore) throw new InvalidOperationException("Launch changed the saved fixture bytes.");
            passed = true; return main!;
        }
        catch (Exception error) { failure = error.ToString(); throw; }
        finally
        {
            var acceptanceElapsed = timer.Elapsed.TotalMilliseconds;
            // Evidence never satisfies a UIA assertion. Capture only the test-owned fixture window.
            try
            {
                process?.Refresh();
                if (process is not null && !process.HasExited && handle != 0 && GetWindowThreadProcessId(handle, out var pid) != 0 && pid == app.ProcessId)
                    NativeUiFailureDiagnostics.CaptureWindow(handle, Path.Combine(output, $"launch-{ordinal}.png"));
                else captureError = "No live owned HWND was available for a screenshot.";
            }
            catch (Exception error) { captureError = error.ToString(); }
            if (!passed) NativeUiFailureDiagnostics.Capture(process, output);
            File.WriteAllText(Path.Combine(output, $"launch-{ordinal}.json"), JsonSerializer.Serialize(new
            {
                Status = passed ? "PASS" : "FAIL", Phase = "Expansion owner launch", RestartOrdinal = ordinal,
                SourceRevision = typeof(NativeExpansionLaunch).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                    .Cast<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion,
                Pid = app.ProcessId, Hwnd = handle.ToInt64(), Process = process is null
                    ? new { Unavailable = true, Exited = app.HasExited, ExitCode = app.HasExited ? (int?)app.ExitCode : null }
                    : NativeUiFailureDiagnostics.ObserveProcess(process),
                BudgetMilliseconds = budget.TotalMilliseconds, AcceptanceElapsedMilliseconds = acceptanceElapsed,
                EvidenceElapsedMilliseconds = timer.Elapsed.TotalMilliseconds,
                ResizeActionCount = resizeCount, FileBeforeSha256 = fileBefore, FileAfterSha256 = HashFile(),
                OwnerExpectedSha256 = ownerExpectedHash, OwnerObservedSha256 = ownerObservedHash,
                Failure = failure, CaptureError = captureError, Observations = observations
            }, new JsonSerializerOptions { WriteIndented = true }));
            process?.Dispose();
        }
        string HashFile() { using var file = File.OpenRead(fixture); return Convert.ToHexStringLower(SHA256.HashData(file)); }
        void LimitProviderCall()
        {
            var remaining = budget - timer.Elapsed;
            if (remaining <= TimeSpan.Zero) throw new InvalidOperationException("The original launch budget has expired.");
            var limit = remaining < TimeSpan.FromSeconds(3) ? remaining : TimeSpan.FromSeconds(3);
            automation.TransactionTimeout = limit; automation.ConnectionTimeout = limit;
        }
        T Provider<T>(Func<T> read) { LimitProviderCall(); return read(); }
        void Record(string phase, NativeLaunchWindow? window, Exception? error) => observations.Add(new
        { Phase = phase, ElapsedMilliseconds = timer.Elapsed.TotalMilliseconds, Window = window, Error = error?.ToString(), ResizeActionCount = resizeCount });
        NativeLaunchWindow Observe()
        {
            LimitProviderCall(); process!.Refresh();
            var exited = process.HasExited;
            if (!exited) handle = process.MainWindowHandle;
            var exists = !exited && handle != 0 && IsWindow(handle);
            GetWindowThreadProcessId(handle, out var nativePid);
            var responsive = exists && SendMessageTimeout(handle, 0, 0, 0, 0x23,
                (uint)Math.Clamp((budget - timer.Elapsed).TotalMilliseconds, 1, 100), out _) != 0;
            GetWindowRect(handle, out var rectangle);
            var currentContrast = NativeTestHighContrast.IsEnabledNow;
            if (requireHighContrast && !currentContrast) throw new InvalidOperationException("Actual Windows High Contrast is no longer enabled.");
            var native = new NativeLaunchWindow(app.ProcessId, handle.ToInt64(), exited, exited ? process.ExitCode : null,
                exists, nativePid, GetWindow(handle, 4).ToInt64(), exists && IsWindowVisible(handle), exists && IsWindowEnabled(handle), responsive,
                0, 0, false, currentContrast, Rectangle.FromLTRB(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom), Rectangle.Empty);
            Record("native-window", native, null); native.VerifyIdentity(0);
            if (!native.Visible || !native.Enabled || !native.Responding) return native;
            main = Provider(() => automation.FromHandle(handle).AsWindow());
            var actualHandle = Provider(() => main.Properties.NativeWindowHandle.Value);
            var actualPid = Provider(() => main.Properties.ProcessId.Value);
            var uia = native with { UiaHandle = actualHandle.ToInt64(), UiaPid = actualPid, UiaBounds = Provider(() => main.BoundingRectangle) };
            Record("uia-window", uia, null); uia.VerifyIdentity(handle.ToInt64());
            var editor = Provider(() => main.FindFirstDescendant(cf => cf.ByAutomationId("MarkdownEditor")));
            var ready = editor is not null && Provider(() => editor.IsEnabled) && !Provider(() => editor.IsOffscreen);
            if (ready)
            {
                var valuePattern = Provider(() => editor!.Patterns.Value.Pattern);
                var ownerSource = Provider(() => valuePattern.Value.Value);
                ownerObservedHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(ownerSource)));
                ready = ownerSource == expectedSource;
            }
            return uia with { EditorReady = ready };
        }
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool IsWindow(nint handle);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint handle);
    [DllImport("user32.dll")] private static extern bool IsWindowEnabled(nint handle);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint handle, out int pid);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint handle, uint command);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint handle, out Rect rectangle);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SendMessageTimeout(nint handle, uint message, nuint wParam, nint lParam, uint flags, uint milliseconds, out nuint result);
}
