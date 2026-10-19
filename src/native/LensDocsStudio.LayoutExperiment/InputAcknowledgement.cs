using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace LensDocsStudio.LayoutExperiment;

/// <summary>Test-only real-input ordering barrier, independent of UIA transport.</summary>
internal sealed class InputAcknowledgement : IDisposable
{
    private const int Message = 0x8000 + 0x739;
    [DllImport("user32.dll", SetLastError = true)] private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    private readonly MemoryMappedFile mapping;
    private readonly MemoryMappedViewAccessor view;
    private readonly HwndSource? source;
    private readonly Dispatcher? dispatcher;
    private long sequence;
    public string Name { get; }
    public List<object> Receipts { get; } = [];
    public InputAcknowledgement(Window window)
    {
        Name = "Local\\LensLayoutInput-" + Environment.ProcessId + "-" + Guid.NewGuid().ToString("N");
        mapping = MemoryMappedFile.CreateNew(Name, 32); view = mapping.CreateViewAccessor(); dispatcher = window.Dispatcher;
        source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle); source.AddHook(Hook);
    }
    public InputAcknowledgement(string name)
    { Name = name; mapping = MemoryMappedFile.OpenExisting(name); view = mapping.CreateViewAccessor(); }
    public void AfterRealInput(IntPtr window)
    {
        var next = ++sequence;
        if (!PostMessage(window, Message, new IntPtr(next), IntPtr.Zero)) throw new System.ComponentModel.Win32Exception();
        IndependentUiaClient.Wait(() => view.ReadInt64(0) == next);
    }
    private IntPtr Hook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != Message) return IntPtr.Zero;
        var requested = Stopwatch.GetTimestamp(); var next = wParam.ToInt64();
        _ = dispatcher!.InvokeAsync(() =>
        {
            var delivered = Stopwatch.GetTimestamp();
            Receipts.Add(new { Sequence = next, Requested = requested, Delivered = delivered, Thread = Environment.CurrentManagedThreadId,
                Milliseconds = (delivered - requested) * 1000d / Stopwatch.Frequency });
            view.Write(8, delivered); view.Write(0, next);
        }, DispatcherPriority.Input);
        handled = true; return IntPtr.Zero;
    }
    public void Dispose() { source?.RemoveHook(Hook); view.Dispose(); mapping.Dispose(); }
}
