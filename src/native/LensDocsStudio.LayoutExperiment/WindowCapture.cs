using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace LensDocsStudio.LayoutExperiment;

internal static class WindowCapture
{
    [StructLayout(LayoutKind.Sequential)] private struct WindowRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out WindowRect rectangle);
    [DllImport("user32.dll")] private static extern IntPtr GetWindowDC(IntPtr window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int width, int height);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
    public static void Save(Window window, string path)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (!GetWindowRect(handle, out var rectangle)) throw new IOException("Shown window bounds are unavailable.");
        var windowDc = GetWindowDC(handle); var memoryDc = CreateCompatibleDC(windowDc);
        var bitmap = CreateCompatibleBitmap(windowDc, rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top);
        var old = SelectObject(memoryDc, bitmap);
        try
        {
            if (!PrintWindow(handle, memoryDc, 2)) throw new IOException("Shown HWND capture failed.");
            var pixels = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(pixels)); using var file = File.Create(path); encoder.Save(file);
        }
        finally { SelectObject(memoryDc, old); DeleteObject(bitmap); DeleteDC(memoryDc); ReleaseDC(handle, windowDc); }
    }
}
