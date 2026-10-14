using System.Runtime.InteropServices;
using System.Text.Json;

namespace LensDocsStudio.Native.Validation;

/// <summary>Temporary display mode on a disposable hosted Windows runner only.</summary>
internal sealed class NativeTestDisplay : IDisposable
{
    private Mode original;
    private bool changed;
    public NativeTestDisplay(string output)
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" || Environment.GetEnvironmentVariable("RUNNER_OS") != "Windows"
            || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted")
            throw new InvalidOperationException("Display changes require disposable GitHub-hosted Windows.");
        original = new Mode { Size = (short)Marshal.SizeOf<Mode>() };
        var available = EnumDisplaySettings(null, -1, ref original);
        var requested = original;
        // Leave room for the largest requested application window, its frame and
        // the hosted taskbar. Negotiate an actual driver-supported temporary mode.
        var attempts = new List<object>(); var supported = -1; var applied = -1;
        foreach (var (width, height) in new[] { (1920, 1200), (2048, 1536), (2560, 1440) })
        {
            requested.Width = width; requested.Height = height; requested.Fields = 0x180000;
            supported = available ? ChangeDisplaySettings(ref requested, 2) : -1;
            applied = supported == 0 ? ChangeDisplaySettings(ref requested, 0) : supported;
            attempts.Add(new { Width = width, Height = height, TestResult = supported, ApplyResult = applied });
            if (applied == 0) { changed = true; break; }
        }
        File.WriteAllText(Path.Combine(output, "display.json"), JsonSerializer.Serialize(new {
            Available = available, OriginalWidth = original.Width, OriginalHeight = original.Height, RequestedWidth = requested.Width,
            RequestedHeight = requested.Height, TestResult = supported, ApplyResult = applied, Attempts = attempts, TemporaryChange = changed, RegistryChanged = false }));
        if (!changed) throw new InvalidOperationException("The disposable display driver cannot contain the complete 1920×1080 window and taskbar. Preserve display.json; no clipped capture is accepted.");
    }
    public void Dispose() { if (changed) { ChangeDisplaySettings(ref original, 0); changed = false; } }
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
