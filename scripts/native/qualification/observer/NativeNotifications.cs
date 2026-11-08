using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LensDocsStudio.Native.Qualification;

// An unshown native top-level window receives actual OS broadcasts on a dedicated
// native message thread. No WPF types/resources, dispatcher pumping, fake theme
// messages, theme application, cache priming or synchronous colour repair.
internal sealed class NativeNotifications : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint WindowProcedure(nint window, uint message, nuint wparam, nint lparam);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Style; public WindowProcedure Procedure; public int ClassBytes, WindowBytes;
        public nint Instance, Icon, Cursor, Background; public string? Menu; public string Name;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Message { public nint Window; public uint Id; public nuint Wparam; public nint Lparam; public uint Time; public int X, Y; public uint Private; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClass(ref WindowClass value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint CreateWindowEx(uint exStyle, string name, string title, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint DefWindowProc(nint window, uint message, nuint wparam, nint lparam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern int GetMessage(out Message message, nint window, uint minimum, uint maximum);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint DispatchMessage(ref Message message);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool PostThreadMessage(uint thread, uint message, nuint wparam, nint lparam);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool UnregisterClass(string name, nint instance);
    [DllImport("user32.dll")] private static extern uint GetSysColor(int index);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    private readonly List<object> events = [];
    private readonly ManualResetEventSlim ready = new();
    private readonly Thread thread;
    private readonly WindowProcedure procedure;
    private uint threadId;
    private Exception? failure;
    private bool overflow, disposed;
    private long started, finished;

    internal NativeNotifications()
    {
        procedure = Observe;
        thread = new Thread(Run) { IsBackground = true, Name = "Qualification Win32 notifications" };
        thread.Start();
        if (!ready.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Native notification observer did not initialise within 20 seconds.");
        if (failure is not null) throw new IOException("Native notification observer failed to initialise.", failure);
    }
    private nint Observe(nint window, uint message, nuint wparam, nint lparam)
    {
        if (message is 0x001a or 0x031a or 0x0320)
        {
            lock (events)
            {
                if (events.Count < 1024) events.Add(new { processId = Environment.ProcessId, threadId = GetCurrentThreadId(), tick = Stopwatch.GetTimestamp(), message,
                    win32Highlight = GetSysColor(13), win32HighlightText = GetSysColor(14) });
                else overflow = true;
            }
        }
        return DefWindowProc(window, message, wparam, lparam);
    }
    private void Run()
    {
        var name = "LensQualification-" + Guid.NewGuid().ToString("N"); var instance = GetModuleHandle(null); nint window = 0; bool registered = false;
        try
        {
            started = Stopwatch.GetTimestamp();
            threadId = GetCurrentThreadId();
            var cls = new WindowClass { Procedure = procedure, Instance = instance, Name = name };
            if (RegisterClass(ref cls) == 0) throw new Win32Exception(Marshal.GetLastWin32Error()); registered = true;
            window = CreateWindowEx(0, name, "", 0x80000000, 0, 0, 0, 0, 0, 0, instance, 0);
            if (window == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            ready.Set();
            int value;
            while ((value = GetMessage(out var message, 0, 0, 0)) > 0) DispatchMessage(ref message);
            if (value < 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        catch (Exception error) { failure = error; }
        finally
        {
            if (window != 0) DestroyWindow(window);
            if (registered) UnregisterClass(name, instance);
            finished = Stopwatch.GetTimestamp(); ready.Set();
        }
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        if (thread.IsAlive)
        {
            // WM_QUIT terminates our own message loop; never a theme notification.
            if (!PostThreadMessage(threadId, 0x0012, 0, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (!thread.Join(TimeSpan.FromSeconds(20))) throw new TimeoutException("Native notification observer cleanup exceeded 20 seconds.");
        }
        lock (events) Program.Write("native-notifications.json", new { processId = Environment.ProcessId, threadId, started, finished,
            events = events.ToArray(), overflow, error = failure?.ToString(),
            scope = "Passive Win32 broadcasts; no WPF resource/colour read; not a compositor or propagation guarantee" });
        ready.Dispose();
        if (failure is not null || overflow) throw new IOException("Native notification evidence is incomplete.", failure);
    }
}
