using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LensDocsStudio.Native.UxProbe;

// Only the disposable hosted runner guard in Program permits this input adapter.
internal static class Desktop
{
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputUnion Value; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyInput Key;
    }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyInput { public ushort Key, Scan; public uint Flags, Time; public UIntPtr Extra; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] input, int size);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int width, int height);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr value);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern bool BitBlt(IntPtr target, int x, int y, int width, int height, IntPtr source, int sx, int sy, uint operation);
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();
    [DllImport("user32.dll")] internal static extern uint GetSysColor(int index);

    internal static void OwnsForeground()
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out var process);
        if (process != Environment.ProcessId) throw new IOException("Foreground ownership lost; no input sent or sample accepted.");
    }
    private static void Send(params Input[] input)
    {
        OwnsForeground();
        if (SendInput((uint)input.Length, input, Marshal.SizeOf<Input>()) != input.Length)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows did not accept every input record.");
    }
    internal static void Key(ushort key, params ushort[] modifiers)
    {
        var inputs = new List<Input>();
        foreach (var modifier in modifiers) inputs.Add(K(modifier, 0));
        inputs.Add(K(key, 0)); inputs.Add(K(key, 2));
        foreach (var modifier in modifiers.Reverse()) inputs.Add(K(modifier, 2));
        Send(inputs.ToArray());
    }
    private static Input K(ushort key, uint flags) => new() { Type = 1, Value = new() { Key = new() { Key = key, Flags = flags } } };
    internal static void Text(string text)
    {
        Send(text.SelectMany(c => new[] {
            new Input { Type = 1, Value = new() { Key = new() { Scan = c, Flags = 4 } } },
            new Input { Type = 1, Value = new() { Key = new() { Scan = c, Flags = 6 } } }
        }).ToArray());
    }
    internal static void Move(Point point)
    {
        OwnsForeground();
        if (!SetCursorPos((int)point.X, (int)point.Y)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    internal static void Click() => Send(new Input { Value = new() { Mouse = new() { Flags = 2 } } }, new Input { Value = new() { Mouse = new() { Flags = 4 } } });
    internal static void Wheel(int delta) => Send(new Input { Value = new() { Mouse = new() { Flags = 0x800, Data = unchecked((uint)delta) } } });

    internal sealed record Frame(string Sha256, int Width, int Height, int DistinctColours, BitmapSource Bitmap);
    internal static Frame Capture(Rect bounds)
    {
        OwnsForeground();
        if (bounds.IsEmpty || bounds.Width < 2 || bounds.Height < 2 || bounds.Width > 3840 || bounds.Height > 2160)
            throw new IOException("Invalid or unbounded visible capture rectangle.");
        var result = DwmFlush();
        if (result < 0) Marshal.ThrowExceptionForHR(result);
        int width = (int)bounds.Width, height = (int)bounds.Height;
        var screen = GetDC(IntPtr.Zero); var target = CreateCompatibleDC(screen);
        var bitmap = CreateCompatibleBitmap(screen, width, height); var previous = SelectObject(target, bitmap);
        try
        {
            if (screen == IntPtr.Zero || target == IntPtr.Zero || bitmap == IntPtr.Zero ||
                !BitBlt(target, 0, 0, width, height, screen, (int)bounds.X, (int)bounds.Y, 0x40CC0020))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Visible desktop capture failed.");
            var image = new FormatConvertedBitmap(Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions()), PixelFormats.Bgra32, null, 0);
            image.Freeze();
            var pixels = new byte[width * height * 4]; image.CopyPixels(pixels, width * 4, 0);
            var colours = new HashSet<int>();
            for (var i = 0; i < pixels.Length && colours.Count < 32; i += 4) colours.Add(BitConverter.ToInt32(pixels, i));
            if (colours.Count < 2) throw new IOException("A blank capture cannot establish a visible response.");
            return new(Convert.ToHexString(SHA256.HashData(pixels)), width, height, colours.Count, image);
        }
        finally { SelectObject(target, previous); DeleteObject(bitmap); DeleteDC(target); ReleaseDC(IntPtr.Zero, screen); }
    }
    internal static void Save(Frame frame, string path)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(frame.Bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
}
