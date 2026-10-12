using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace LensDocsStudio.Native.Validation;

internal static class NativeUiFailureDiagnostics
{
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
}
