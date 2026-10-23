using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace LensDocsStudio.Native.Validation;

internal static class NativeUiFailureDiagnostics
{
    public static object ObserveProcess(Process process)
    {
        try
        {
            process.Refresh(); var exited = process.HasExited;
            return new { Id = process.Id, Exited = exited, ExitCode = exited ? (int?)process.ExitCode : null,
                Responding = exited ? (bool?)null : process.Responding };
        }
        catch (Exception error) when (error is not OutOfMemoryException) { return new { Error = error.ToString() }; }
    }
    public static object ObserveDialogue(IntPtr handle)
    {
        var title = new StringBuilder(512); GetWindowText(handle, title, title.Capacity);
        return new { Handle = handle.ToInt64(), Exists = IsWindow(handle), Visible = IsWindowVisible(handle), Title = title.ToString() };
    }
    public static object ObserveWindows(int processId)
    {
        var windows = new List<object>();
        var foreground = GetForegroundWindow();
        GetWindowThreadProcessId(foreground, out var foregroundProcess);
        EnumWindows((handle, _) =>
        {
            GetWindowThreadProcessId(handle, out var ownerProcess);
            if (ownerProcess != processId) return true;
            var title = new StringBuilder(512); GetWindowText(handle, title, title.Capacity);
            windows.Add(new { Handle = handle.ToInt64(), Title = title.ToString(), Visible = IsWindowVisible(handle),
                Enabled = IsWindowEnabled(handle), Owner = GetWindow(handle, 4).ToInt64(), Foreground = handle == foreground });
            return true;
        }, IntPtr.Zero);
        return new { Foreground = foreground.ToInt64(), ForegroundProcessId = foregroundProcess, ProcessWindows = windows };
    }

    // Only the test-owned fixture process is passed here. Avoid UIA when its provider is unresponsive.
    public static void Capture(Process? process, string output)
    {
        if (process is null || process.HasExited) return;
        try
        {
            if (GetWindowRect(process.MainWindowHandle, out var rectangle)
                && rectangle.Right > rectangle.Left && rectangle.Bottom > rectangle.Top)
            {
                using var image = new Bitmap(rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top);
                using var graphics = Graphics.FromImage(image);
                graphics.CopyFromScreen(rectangle.Left, rectangle.Top, 0, 0, image.Size);
                image.Save(Path.Combine(output, "native-ui-failure-desktop.png"), ImageFormat.Png);
            }
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(output, "native-ui-failure-capture.txt"), ex.ToString()); }
        try
        {
            using var dump = File.Create(Path.Combine(output, "native-ui-failure-threads.dmp"));
            // Native thread stacks and thread state, without a full managed heap/environment dump.
            if (!MiniDumpWriteDump(process.SafeHandle, (uint)process.Id, dump.SafeFileHandle, 0x1000, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(output, "native-ui-failure-dump.txt"), ex.ToString()); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRectangle { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr window, out NativeRectangle rectangle);
    [DllImport("dbghelp.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MiniDumpWriteDump(SafeProcessHandle process, uint processId, SafeFileHandle file, uint type, IntPtr exception, IntPtr streams, IntPtr callback);
    private delegate bool EnumWindowCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsWindowEnabled(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
}
