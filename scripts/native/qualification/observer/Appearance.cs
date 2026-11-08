using System.ComponentModel;
using System.Runtime.InteropServices;

namespace LensDocsStudio.Native.Qualification;

// Win32 only. This type must not reference WPF, SystemEvents or resource dictionaries.
internal static class Appearance
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Native { public uint Size, Flags; public IntPtr Scheme; }
    internal sealed record Snapshot(uint Flags, string Scheme) { public bool Enabled => (Flags & 1) != 0; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint parameter, ref Native state, uint flags);
    internal static Snapshot Read()
    {
        var native = new Native { Size = (uint)Marshal.SizeOf<Native>() };
        if (!SystemParametersInfo(0x42, native.Size, ref native, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return new(native.Flags, Marshal.PtrToStringUni(native.Scheme) ?? "");
    }
    internal static void Restore(Snapshot state)
    {
        var scheme = Marshal.StringToHGlobalUni(state.Scheme);
        try
        {
            var native = new Native { Size = (uint)Marshal.SizeOf<Native>(), Flags = state.Flags, Scheme = scheme };
            if (!SystemParametersInfo(0x43, native.Size, ref native, 2)) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (Read() != state) throw new IOException("Original OS appearance restoration could not be verified.");
        }
        finally { Marshal.FreeHGlobal(scheme); }
    }
    internal static void Set(bool enabled)
    {
        var state = Read(); Restore(state with { Flags = enabled ? state.Flags | 1u : state.Flags & ~1u });
    }
}
