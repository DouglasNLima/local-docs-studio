using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ContinuousViewer;

internal static class HarnessSupport
{
    public const int Width = 640, Height = 300;
    public static HwndSource Host(UIElement viewer)
    {
        var host = new HwndSource(new HwndSourceParameters("Hidden Continuous feasibility validation")
            { Width = Width, Height = Height, WindowStyle = unchecked((int)0x80000000) }); // No WS_VISIBLE.
        host.RootVisual = viewer; return host;
    }
    public static void Layout(UIElement viewer)
    { viewer.Measure(new Size(Width, Height)); viewer.Arrange(new Rect(0, 0, Width, Height)); viewer.UpdateLayout(); }
    public static void Pump(Task task, int milliseconds = 60000)
    {
        if (task.IsCompleted) { task.GetAwaiter().GetResult(); return; }
        var dispatcher = Dispatcher.CurrentDispatcher; var frame = new DispatcherFrame(); var expired = false;
        var timer = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { expired = true; frame.Continue = false; };
        _ = task.ContinueWith(_ => dispatcher.BeginInvoke(DispatcherPriority.Send, () => frame.Continue = false), TaskScheduler.Default);
        timer.Start(); Dispatcher.PushFrame(frame); timer.Stop();
        if (expired) throw new TimeoutException("The unchanged 60-second diagnostic deadline was exceeded.");
        task.GetAwaiter().GetResult();
    }
    public static void Idle() => Pump(Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Task);
    public static void Raster(Visual viewer, string path)
    {
        var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(viewer);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
    public static void Pixel(string root) => File.WriteAllBytes(Path.Combine(root, "pixel.png"), Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/l9sAAAAASUVORK5CYII="));
}

internal sealed class InputProbe : IDisposable
{
    private readonly Stopwatch watch = Stopwatch.StartNew();
    private readonly DispatcherTimer timer;
    private double previous;
    public double Worst { get; private set; }
    public InputProbe()
    {
        timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) => Pulse(); timer.Start();
    }
    public void Pulse() { var now = watch.Elapsed.TotalMilliseconds; Worst = Math.Max(Worst, now - previous); previous = now; }
    public double Measure(Action work)
    {
        var at = watch.Elapsed.TotalMilliseconds; var serviced = -1d;
        var input = Dispatcher.CurrentDispatcher.InvokeAsync(() => { serviced = watch.Elapsed.TotalMilliseconds - at; Pulse(); }, DispatcherPriority.Input);
        work(); HarnessSupport.Pump(input.Task); return serviced;
    }
    public void Dispose() { Pulse(); timer.Stop(); }
}
