using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace LensDocsStudio.LayoutExperiment;

/// <summary>Bounded diagnostic observations. Never pumps, pauses or resets a product clock.</summary>
internal sealed class OperationTrace : IDisposable
{
    private const int ProbeMessage = 0x8000 + 0x738;
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] internal static extern bool IsWindowEnabled(IntPtr window);
    private readonly ConcurrentQueue<object> events = new();
    private readonly Dispatcher dispatcher;
    private readonly HwndSource source;
    private readonly IntPtr handle;
    private readonly System.Threading.Timer timer;
    private readonly Dictionary<DispatcherOperation, long> operations = [];
    private long inputPending, nativePending;
    [StructLayout(LayoutKind.Sequential)] private struct BeforeMessage { public IntPtr LParam, WParam; public uint Message; public IntPtr Window; }
    [StructLayout(LayoutKind.Sequential)] private struct AfterMessage { public IntPtr Result, LParam, WParam; public uint Message; public IntPtr Window; }
    private delegate IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int kind, HookCallback callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    private readonly HookCallback beforeCallback, afterCallback;
    private readonly IntPtr beforeHook, afterHook;
    private readonly Stack<(IntPtr Window, uint Message, long Start)> messages = new();
    private int count, disposed;
    public object[] Events => events.ToArray();
    public OperationTrace(Window window)
    {
        dispatcher = window.Dispatcher; handle = new WindowInteropHelper(window).Handle;
        source = HwndSource.FromHwnd(handle); source.AddHook(Hook);
        beforeCallback = Before; afterCallback = After;
        beforeHook = SetWindowsHookEx(4, beforeCallback, IntPtr.Zero, GetCurrentThreadId());
        afterHook = SetWindowsHookEx(12, afterCallback, IntPtr.Zero, GetCurrentThreadId());
        if (beforeHook == IntPtr.Zero || afterHook == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
        dispatcher.Hooks.OperationStarted += Started;
        dispatcher.Hooks.OperationCompleted += Completed;
        Stage("trace-start", new { Handle = handle.ToInt64(), Stopwatch.Frequency, NativeThread = GetCurrentThreadId(), Utc = DateTimeOffset.UtcNow, Process = Environment.ProcessId,
            ProbeVersion = "parallel-input-native-v1", IntervalMilliseconds = 16 });
        timer = new System.Threading.Timer(_ => Pulse(), null, 16, 16);
    }
    private IntPtr Before(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var value = Marshal.PtrToStructure<BeforeMessage>(lParam);
            messages.Push((value.Window, value.Message, Stopwatch.GetTimestamp()));
            if (value.Message is 0x001A or 0x0015 or 0x031A) Stage("native-message-enter", new { value.Message, Handle = value.Window.ToInt64() });
        }
        return CallNextHookEx(beforeHook, code, wParam, lParam);
    }
    private IntPtr After(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && messages.Count > 0)
        {
            var value = Marshal.PtrToStructure<AfterMessage>(lParam); var start = messages.Pop();
            if (value.Window != start.Window || value.Message != start.Message) Stage("native-hook-pair-mismatch");
            else if (Elapsed(start.Start) >= 8 || value.Message is 0x001A or 0x0015 or 0x031A)
                Stage("native-message-exit", new { value.Message, Handle = value.Window.ToInt64(), StartTimestamp = start.Start, Milliseconds = Elapsed(start.Start) });
        }
        return CallNextHookEx(afterHook, code, wParam, lParam);
    }
    public void Stage(string name, object? detail = null)
    {
        if (Interlocked.Increment(ref count) <= 32768)
            events.Enqueue(new { Stage = name, Timestamp = Stopwatch.GetTimestamp(), Thread = Environment.CurrentManagedThreadId, Detail = detail });
    }
    private void Pulse()
    {
        if (Volatile.Read(ref disposed) != 0) return;
        var now = Stopwatch.GetTimestamp();
        if (Interlocked.CompareExchange(ref inputPending, now, 0) == 0)
        {
            Stage("input-posted", new { Scheduled = now });
            _ = dispatcher.InvokeAsync(() =>
            {
                var scheduled = Interlocked.Exchange(ref inputPending, 0);
                Stage("input-delivered", new { Scheduled = scheduled, Milliseconds = Elapsed(scheduled), OwnerEnabled = IsWindowEnabled(handle), Modal = ComponentDispatcher.IsThreadModal });
            }, DispatcherPriority.Input);
        }
        if (Interlocked.CompareExchange(ref nativePending, now, 0) == 0)
        {
            Stage("native-posted", new { Scheduled = now });
            if (!PostMessage(handle, ProbeMessage, IntPtr.Zero, IntPtr.Zero)) Stage("native-post-failed");
        }
    }
    private IntPtr Hook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == ProbeMessage)
        {
            var scheduled = Interlocked.Exchange(ref nativePending, 0);
            Stage("native-delivered", new { Scheduled = scheduled, Milliseconds = Elapsed(scheduled), OwnerEnabled = IsWindowEnabled(handle), Modal = ComponentDispatcher.IsThreadModal });
            handled = true;
        }
        else if (message is 0x001A or 0x0015 or 0x031A) Stage("appearance-message", new { Message = message });
        else if (message == 0x000A) Stage("owner-enable-message", new { Enabled = wParam != IntPtr.Zero });
        return IntPtr.Zero;
    }
    private static double Elapsed(long start) => (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
    private void Started(object? sender, DispatcherHookEventArgs args) => operations[args.Operation] = Stopwatch.GetTimestamp();
    private void Completed(object? sender, DispatcherHookEventArgs args)
    {
        if (operations.Remove(args.Operation, out var start) && Elapsed(start) >= 8)
            Stage("dispatcher-operation", new { StartTimestamp = start, Milliseconds = Elapsed(start), Priority = args.Operation.Priority.ToString(), Method = args.Operation.Task.Status.ToString() });
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        timer.Dispose();
        UnhookWindowsHookEx(beforeHook); UnhookWindowsHookEx(afterHook);
        dispatcher.Hooks.OperationStarted -= Started; dispatcher.Hooks.OperationCompleted -= Completed; source.RemoveHook(Hook);
        Stage("trace-end", new { InputOutstandingSince = inputPending, NativeOutstandingSince = nativePending, OutstandingOperations = operations.Count, DroppedEvents = Math.Max(0, count - 32768) });
    }
}
