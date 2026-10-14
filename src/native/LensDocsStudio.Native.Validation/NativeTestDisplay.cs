using System.Runtime.InteropServices;
using System.Text.Json;

namespace LensDocsStudio.Native.Validation;

/// <summary>Temporary display mode on a disposable hosted Windows runner only.</summary>
internal sealed class NativeTestDisplay : IDisposable
{
    private Mode original;
    private bool changed;
    private readonly string output;
    private IntPtr taskbar;
    private uint originalTaskbarState;
    private bool taskbarChanged;
    private bool disposed;
    private readonly List<string> restorationErrors = [];
    public NativeTestDisplay(string output)
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" || Environment.GetEnvironmentVariable("RUNNER_OS") != "Windows"
            || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted")
            throw new InvalidOperationException("Display changes require disposable GitHub-hosted Windows.");
        this.output = output;
        original = new Mode { Size = (short)Marshal.SizeOf<Mode>() };
        var available = EnumDisplaySettings(null, -1, ref original);
        var requested = original;
        // Leave room for the largest requested application window, its frame and
        // the hosted taskbar. Negotiate an actual driver-supported temporary mode.
        var attempts = new List<object>(); var supported = -1; var applied = -1;
        foreach (var (width, height) in new[] { (1920, 1200), (2048, 1536), (2560, 1440), (1920, 1080) })
        {
            requested.Width = width; requested.Height = height; requested.Fields = 0x180000;
            supported = available ? ChangeDisplaySettings(ref requested, 2) : -1;
            applied = supported == 0 ? ChangeDisplaySettings(ref requested, 0) : supported;
            attempts.Add(new { Width = width, Height = height, TestResult = supported, ApplyResult = applied });
            if (applied == 0) { changed = true; break; }
        }
        if (changed && requested.Height == 1080)
        {
            // The hosted adapter's maximum mode leaves no room below a 1080-high
            // window. Temporarily auto-hide its taskbar and restore the exact shell
            // state on disposal; this operation cannot run on an ordinary desktop.
            taskbar = FindWindow("Shell_TrayWnd", null);
            if (taskbar == IntPtr.Zero) { Dispose(); throw new IOException("The disposable taskbar could not be located."); }
            var data = TaskbarData(); originalTaskbarState = (uint)SHAppBarMessage(4, ref data).ToUInt64();
            data.Parameter = new IntPtr(originalTaskbarState | 1); SHAppBarMessage(10, ref data); taskbarChanged = true;
            if (((uint)SHAppBarMessage(4, ref data).ToUInt64() & 1) == 0)
            { Dispose(); throw new IOException("The disposable taskbar did not accept temporary auto-hide."); }
        }
        File.WriteAllText(Path.Combine(output, "display.json"), JsonSerializer.Serialize(new {
            Available = available, OriginalWidth = original.Width, OriginalHeight = original.Height, RequestedWidth = requested.Width,
            RequestedHeight = requested.Height, TestResult = supported, ApplyResult = applied, Attempts = attempts, TemporaryChange = changed,
            DisplayRegistryChanged = false, TaskbarTemporarilyAutoHidden = taskbarChanged, OriginalTaskbarState = originalTaskbarState }));
        if (!changed) throw new InvalidOperationException("The disposable display driver cannot contain the complete 1920×1080 window and taskbar. Preserve display.json; no clipped capture is accepted.");
    }
    public bool TaskbarOverlaps(System.Drawing.Rectangle rectangle)
    {
        if (!taskbarChanged || !GetWindowRect(taskbar, out var bounds)) return false;
        return rectangle.IntersectsWith(System.Drawing.Rectangle.FromLTRB(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom));
    }
    private AppBarData TaskbarData() => new() { Size = (uint)Marshal.SizeOf<AppBarData>(), Window = taskbar };
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        if (taskbarChanged)
        {
            try
            {
                var data = TaskbarData(); data.Parameter = new IntPtr(originalTaskbarState); SHAppBarMessage(10, ref data);
                if ((uint)SHAppBarMessage(4, ref data).ToUInt64() != originalTaskbarState) restorationErrors.Add("The disposable taskbar state did not restore exactly.");
            }
            catch (Exception error) when (error is not OutOfMemoryException) { restorationErrors.Add(error.Message); }
            finally { taskbarChanged = false; }
        }
        if (changed)
        {
            try { if (ChangeDisplaySettings(ref original, 0) != 0) restorationErrors.Add("The disposable display mode did not restore."); }
            catch (Exception error) when (error is not OutOfMemoryException) { restorationErrors.Add(error.Message); }
            finally { changed = false; }
        }
        File.WriteAllText(Path.Combine(output, "display-restoration.json"), JsonSerializer.Serialize(new {
            Restored = restorationErrors.Count == 0, OriginalTaskbarState = originalTaskbarState, Errors = restorationErrors }));
    }
    public void Complete() { Dispose(); if (restorationErrors.Count != 0) throw new IOException(string.Join(" ", restorationErrors)); }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct AppBarData
    {
        public uint Size; public IntPtr Window; public uint Callback, Edge; public Rect Bounds; public IntPtr Parameter;
    }
    [DllImport("shell32.dll")] private static extern UIntPtr SHAppBarMessage(uint message, ref AppBarData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string className, string? title);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr window, out Rect bounds);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Mode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
        public short SpecVersion, DriverVersion, Size, DriverExtra;
        public int Fields, PositionX, PositionY, Orientation, FixedOutput;
        public short Colour, Duplex, YResolution, TTOption, Collate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Form;
        public short LogPixels;
        public int BitsPerPixel, Width, Height, DisplayFlags, Frequency, IcmMethod, IcmIntent, MediaType, DitherType, Reserved1, Reserved2, PanningWidth, PanningHeight;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplaySettings(string? device, int mode, ref Mode data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ChangeDisplaySettings(ref Mode data, int flags);
}
