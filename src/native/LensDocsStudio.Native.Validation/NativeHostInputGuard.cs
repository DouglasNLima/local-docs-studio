using System.Diagnostics;
using System.Runtime.InteropServices;
using FlaUI.Core.AutomationElements;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static void AssertHostForeground(int processId, Window? window = null, AutomationElement? focus = null)
    {
        var foreground = GetForegroundWindow();
        GetWindowThreadProcessId(foreground, out var owner);
        var expectedWindow = window?.Properties.NativeWindowHandle.Value;
        var normalConsole = HostNormalConsole();
        var expectedFocus = focus?.Properties.HasKeyboardFocus.Value;
        if (owner != processId || expectedWindow is not null && foreground != expectedWindow || !normalConsole
            || expectedFocus is false)
            throw new HostFindInputBlockedException("Expected candidate PID/HWND/focus must remain foreground on the unlocked Default console desktop; no activation or input retry is permitted. "
                + $"Expected PID={processId}, observed PID={owner}, expected HWND={expectedWindow?.ToInt64()}, foreground HWND={foreground.ToInt64()}, normal console={normalConsole}, required control focused={expectedFocus}.");
    }

    private static bool HostNormalConsole()
    {
        var desktop = HostFindOpenInputDesktop(0, false, 1);
        var accessible = false;
        if (desktop != IntPtr.Zero)
        {
            var buffer = Marshal.AllocHGlobal(1024);
            try { if (HostFindGetUserObjectInformation(desktop, 2, buffer, 1024, out _)) accessible = Marshal.PtrToStringUni(buffer) == "Default"; }
            finally { Marshal.FreeHGlobal(buffer); HostFindCloseDesktop(desktop); }
        }
        return accessible && Process.GetCurrentProcess().SessionId == HostActiveConsoleSession();
    }

    // Only before the first action of a new launch. The existing 20-second Wait
    // budget observes an explicit operator activation; later guards never wait or retry.
    private static void AwaitHostOperatorForeground(int processId, Window window, string executable, string output, string name)
    {
        using var process = Process.GetProcessById(processId);
        var start = process.StartTime.ToUniversalTime(); var hwnd = window.Properties.NativeWindowHandle.Value;
        WriteNew(Path.Combine(output, name + "-operator-ready.json"), new { atUtc = DateTimeOffset.UtcNow, status = "WAITING_FOR_OPERATOR_BEFORE_INPUT",
            processId, startUtc = start, hwnd = hwnd.ToInt64(), executable, actionSent = false, observationBudgetSeconds = 20 });
        Console.WriteLine("HOST READY: activate the new synthetic Lens window now; no input has been sent. Existing 20-second observation budget applies.");
        var observations = new List<object>();
        try { Wait(() =>
        {
            process.Refresh(); GetWindowThreadProcessId(hwnd, out var owner);
            if (process.HasExited || process.StartTime.ToUniversalTime() != start || process.MainModule?.FileName != executable
                || owner != processId || window.Properties.ProcessId.Value != processId || !HostNormalConsole())
                throw new HostFindInputBlockedException("New-session identity or normal console changed during operator handoff; no input sent.");
            var foreground = GetForegroundWindow();
            GetWindowThreadProcessId(foreground, out var foregroundProcess);
            if (observations.Count < 256) observations.Add(new { atUtc = DateTimeOffset.UtcNow,
                targetHwnd = hwnd.ToInt64(), foregroundHwnd = foreground.ToInt64(), foregroundProcess, matches = foreground == hwnd });
            return foreground == hwnd;
        }, "Operator did not activate the exact new synthetic window within the unchanged observation budget; no input sent."); }
        catch (InvalidOperationException error) when (error is not HostFindInputBlockedException)
        { throw new HostFindInputBlockedException(error.Message); }
        finally { WriteNew(Path.Combine(output, name + "-operator-observations.json"), new { processId, startUtc = start,
            actionSent = false, observationBudgetSeconds = 20, observations, scope = "Read-only window/process identifiers; no foreign application title or content" }); }
        AssertHostForeground(processId, window);
        WriteNew(Path.Combine(output, name + "-operator-foreground.json"), new { atUtc = DateTimeOffset.UtcNow, status = "PASS", processId, startUtc = start,
            hwnd = hwnd.ToInt64(), foreground = GetForegroundWindow().ToInt64(), actionSent = false });
    }
}
