using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LensDocsStudio.Native.Validation;

// Explicit, controlled diagnostic schedule on disposable hosted Windows only.
internal sealed class NativeOutlineFocusProbe(Process process) : IDisposable
{
    public static NativeOutlineFocusProbe? StartIfRequested()
    {
        if (Environment.GetEnvironmentVariable("LENS_NATIVE_OUTLINE_FOCUS_PROBE") is not ("original" or "foreground-restored")) return null;
        var child = Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, ArgumentList = { "--outline-focus-holder" } })!;
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(5))
        {
            GetWindowThreadProcessId(GetForegroundWindow(), out var pid);
            if (pid == child.Id) return new(child);
            if (child.HasExited) break;
            Thread.Sleep(25);
        }
        if (!child.HasExited) child.Kill(true);
        child.Dispose(); throw new IOException("The controlled focus schedule could not acquire the foreground window.");
    }
    public void Dispose() { if (!process.HasExited) { process.Kill(true); process.WaitForExit(); } process.Dispose(); }
    public static int Hold()
    {
        var window = CreateWindowEx(0, "STATIC", "Controlled native focus fixture", 0x10CF0000, 100, 100, 320, 160, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (window == IntPtr.Zero) return 1;
        try
        {
            SetForegroundWindow(window);
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed < TimeSpan.FromSeconds(30))
            {
                while (PeekMessage(out var message, IntPtr.Zero, 0, 0, 1)) { TranslateMessage(ref message); DispatchMessage(ref message); }
                Thread.Sleep(10);
            }
            return 0;
        }
        finally { DestroyWindow(window); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct Message { public IntPtr Window; public uint Id; public UIntPtr WParam; public IntPtr LParam; public uint Time; public int X, Y; public uint Private; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr CreateWindowEx(uint extended, string className, string title, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out int pid);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out Message message, IntPtr window, uint minimum, uint maximum, uint remove);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref Message message);
}
