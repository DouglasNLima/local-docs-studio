using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace LensDocsStudio.LayoutExperiment;

internal static class ExperimentAppearance
{
    [StructLayout(LayoutKind.Sequential)] private struct Contrast { public uint Size, Flags; public IntPtr Scheme; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint parameter, ref Contrast contrast, uint flags);
    [DllImport("user32.dll")] private static extern uint GetSysColor(int index);
    public static bool ActualHighContrast
    {
        get { var value = new Contrast { Size = (uint)Marshal.SizeOf<Contrast>() }; if (!SystemParametersInfo(0x42, value.Size, ref value, 0)) throw new Win32Exception(); return (value.Flags & 1) != 0; }
    }
    private static string Colour(int index) { var value = GetSysColor(index); return Color.FromRgb((byte)value, (byte)(value >> 8), (byte)(value >> 16)).ToString(); }
    public static string Capture(string preference) => ActualHighContrast ? "HC:" + string.Join('|', new[] { Colour(8), Colour(5), Colour(15), Colour(13), Colour(14), Colour(26) }) : preference;
    public static bool IsDark(string key)
    {
        if (!key.StartsWith("HC:", StringComparison.Ordinal)) return key == "Dark";
        var colour = (Color)ColorConverter.ConvertFromString(Slot(key, 1));
        return colour.R * .2126 + colour.G * .7152 + colour.B * .0722 < 128;
    }
    public static string Slot(string key, int index) => key[3..].Split('|')[index];
}
