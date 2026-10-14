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
        requested.Width = 1920; requested.Height = 1080; requested.Fields = 0x180000;
        var supported = available ? ChangeDisplaySettings(ref requested, 2) : -1;
        var applied = supported == 0 ? ChangeDisplaySettings(ref requested, 0) : supported;
        changed = applied == 0;
        File.WriteAllText(Path.Combine(output, "display.json"), JsonSerializer.Serialize(new {
            Available = available, OriginalWidth = original.Width, OriginalHeight = original.Height, RequestedWidth = 1920,
            RequestedHeight = 1080, TestResult = supported, ApplyResult = applied, TemporaryChange = changed, RegistryChanged = false }));
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
