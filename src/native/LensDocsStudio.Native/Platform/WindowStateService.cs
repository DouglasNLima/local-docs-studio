using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Native.Platform;

public static class WindowStateService
{
    public static void FitInitialWindow(Window window)
    {
        CaptionThemeService.Attach(window);
        var area = SystemParameters.WorkArea;
        window.MinWidth = Math.Min(window.MinWidth, area.Width);
        window.MinHeight = Math.Min(window.MinHeight, area.Height);
        window.Width = Math.Min(window.Width, area.Width);
        window.Height = Math.Min(window.Height, area.Height);
        if (window.WindowStartupLocation == WindowStartupLocation.Manual) window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }
    public static WindowBounds? Capture(Window window)
    {
        var placement = new Placement { Length = (uint)Marshal.SizeOf<Placement>() };
        if (!GetWindowPlacement(new WindowInteropHelper(window).Handle, ref placement)) return null;
        var rect = placement.Normal;
        return new(rect.Left, rect.Top, rect.Right, rect.Bottom, placement.ShowCommand == 3);
    }
    public static void Restore(Window window, WindowBounds? bounds)
    {
        if (bounds is null || (long)bounds.Right - bounds.Left is < 400 or > 32768 || (long)bounds.Bottom - bounds.Top is < 300 or > 32768) return;
        var placement = new Placement { Length = (uint)Marshal.SizeOf<Placement>(), ShowCommand = bounds.Maximised ? 3u : 1u,
            Minimum = new() { X = -1, Y = -1 }, Maximum = new() { X = -1, Y = -1 },
            Normal = new() { Left = bounds.Left, Top = bounds.Top, Right = bounds.Right, Bottom = bounds.Bottom } };
        // Windows adjusts placement when a previous monitor is no longer available.
        _ = SetWindowPlacement(new WindowInteropHelper(window).Handle, ref placement);
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Placement
    {
        public uint Length, Flags, ShowCommand;
        public Point Minimum, Maximum;
        public Rect Normal;
    }
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowPlacement(IntPtr window, ref Placement placement);
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPlacement(IntPtr window, ref Placement placement);
}
