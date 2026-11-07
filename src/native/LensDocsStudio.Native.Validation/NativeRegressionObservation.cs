using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace LensDocsStudio.Native.Validation;

// Test-owned processes only. No editor values, document text or environment dump.
internal static class NativeRegressionObservation
{
    internal static object Read(int processId)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            using var process = Process.GetProcessById(processId);
            process.Refresh();
            if (process.HasExited) return new { started, processId, exited = true, process.ExitCode };
            var windows = new List<object>();
            var foreground = GetForegroundWindow();
            EnumWindows((handle, unused) =>
            {
                var thread = GetWindowThreadProcessId(handle, out var owner);
                if (owner != processId || windows.Count >= 16) return true;
                var kind = new StringBuilder(128); GetClassName(handle, kind, kind.Capacity);
                var responding = SendMessageTimeout(handle, 0, 0, 0, 0x23, 50, out _) != 0;
                windows.Add(new { handle = handle.ToInt64(), thread, kind = kind.ToString(),
                    owner = GetWindow(handle, 4).ToInt64(), visible = IsWindowVisible(handle), enabled = IsWindowEnabled(handle),
                    foreground = handle == foreground, responding });
                return true;
            }, 0);
            var threads = new List<object>();
            foreach (ProcessThread thread in process.Threads)
            {
                if (threads.Count >= 64) break;
                try { threads.Add(new { thread.Id, state = thread.ThreadState.ToString(),
                    wait = thread.ThreadState == System.Diagnostics.ThreadState.Wait ? thread.WaitReason.ToString() : null,
                    cpuMs = thread.TotalProcessorTime.TotalMilliseconds }); }
                catch (InvalidOperationException) { }
                finally { thread.Dispose(); }
            }
            return new { started, finished = Stopwatch.GetTimestamp(), processId, exited = false,
                process.SessionId, process.PrivateMemorySize64, process.HandleCount, cpuMs = process.TotalProcessorTime.TotalMilliseconds,
                mainHandle = process.MainWindowHandle.ToInt64(), windows, threads,
                scope = "Native thread state and WM_NULL responsiveness; not a managed dispatcher stack" };
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        { return new { started, finished = Stopwatch.GetTimestamp(), processId, error = error.GetType().FullName, error.Message }; }
    }
    private delegate bool WindowCallback(nint handle, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, nint parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint handle, out int process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint handle, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint handle, uint command);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint handle);
    [DllImport("user32.dll")] private static extern bool IsWindowEnabled(nint handle);
    [DllImport("user32.dll")] private static extern nint SendMessageTimeout(nint handle, uint message, nuint wParam, nint lParam, uint flags, uint milliseconds, out nuint result);
}
