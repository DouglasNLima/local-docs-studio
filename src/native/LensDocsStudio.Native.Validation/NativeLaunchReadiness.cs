using System.Diagnostics;
using System.Drawing;

namespace LensDocsStudio.Native.Validation;

internal sealed record NativeLaunchWindow(int ExpectedPid, long ProcessHandle, bool Exited, int? ExitCode,
    bool Exists, int NativePid, long Owner, bool Visible, bool Enabled, bool Responding,
    long UiaHandle, int UiaPid, bool EditorReady, bool HighContrast, Rectangle Bounds, Rectangle UiaBounds)
{
    public void VerifyIdentity(long pinnedHandle)
    {
        if (Exited) throw new InvalidOperationException("The launched process exited before window readiness; exit code " + ExitCode);
        if (ProcessHandle == 0) return;
        if (!Exists || NativePid != ExpectedPid || Owner != 0 || pinnedHandle != 0 && ProcessHandle != pinnedHandle
            || UiaHandle != 0 && (UiaHandle != ProcessHandle || UiaPid != ExpectedPid))
            throw new InvalidOperationException("Main window/process/HWND ownership mismatch.");
    }
    public bool Ready => ProcessHandle != 0 && UiaHandle == ProcessHandle && Visible && Enabled && Responding && EditorReady;
    public bool HasTargetBounds => Bounds.Width == 1280 && Bounds.Height == 800 && UiaBounds == Bounds;
}

internal static class NativeLaunchReadiness
{
    // The injected functions are read-only observations, apart from the single action below.
    // Deterministic controls exercise this same orchestration; they are not installed UI evidence.
    public static void ResizeOnce<T>(Stopwatch timer, TimeSpan budget, Func<NativeLaunchWindow> observe,
        Func<T> acquireTransform, Action<T> resize, Action<string, NativeLaunchWindow?, Exception?> record)
        where T : class
    {
        NativeLaunchWindow? last = null;
        T? transform = null;
        long pinnedHandle = 0;
        bool Observe(string phase, bool targetBounds)
        {
            try
            {
                last = observe(); record(phase, last, null); last.VerifyIdentity(pinnedHandle);
                if (last.ProcessHandle != 0) pinnedHandle = last.ProcessHandle;
                return last.Ready && (!targetBounds || last.HasTargetBounds);
            }
            catch (Exception error) { record(phase, last, error); throw; }
        }
        NativeUiChecks.Wait(() =>
        {
            if (!Observe("window-readiness", false)) return false;
            try { transform = acquireTransform(); record("transform-acquired", last, null); }
            catch (Exception error) { record("transform-acquisition", last, error); throw; }
            return Observe("before-resize", false);
        }, "Main window or real UIA Transform remained unavailable within the original launch budget.", timer, budget);

        if (timer.Elapsed >= budget) throw new InvalidOperationException("Launch budget expired before Resize.");
        // Never reissue a mutation after an uncertain provider result, even if it timed out.
        try { record("resize-issued", last, null); resize(transform!); }
        catch (Exception error) { record("resize-action-failed", last, error); throw; }
        NativeUiChecks.Wait(() => Observe("resized-bounds", true),
            "The single real UIA Resize did not produce exact 1280x800 owner bounds within the original launch budget.", timer, budget);
    }
}
