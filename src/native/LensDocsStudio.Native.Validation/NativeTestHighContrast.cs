using System.ComponentModel;
using System.Runtime.InteropServices;

namespace LensDocsStudio.Native.Validation;

/// <summary>Temporarily exercise actual high contrast on a disposable hosted desktop.</summary>
internal sealed class NativeTestHighContrast : IDisposable
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct HighContrast { public uint Size; public uint Flags; public IntPtr Scheme; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint parameter, ref HighContrast contrast, uint flags);
    private HighContrast original;
    private readonly IntPtr scheme;
    private bool restored;
    public NativeTestHighContrast()
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true"
            || Environment.GetEnvironmentVariable("RUNNER_OS") != "Windows"
            || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted")
            throw new InvalidOperationException("Actual high-contrast validation requires a disposable GitHub-hosted Windows runner.");
        original = new() { Size = (uint)Marshal.SizeOf<HighContrast>() };
        if (!SystemParametersInfo(0x42, original.Size, ref original, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        scheme = Marshal.StringToHGlobalUni(Marshal.PtrToStringUni(original.Scheme) ?? ""); original.Scheme = scheme;
        var enabled = original; enabled.Flags |= 1;
        // Broadcast the temporary change, without SPIF_UPDATEINIFILE or registry persistence.
        if (!SystemParametersInfo(0x43, enabled.Size, ref enabled, 2))
        { Marshal.FreeHGlobal(scheme); throw new Win32Exception(Marshal.GetLastWin32Error()); }
    }
    public void Dispose()
    {
        if (restored) return;
        restored = true;
        try { if (!SystemParametersInfo(0x43, original.Size, ref original, 2)) throw new Win32Exception(Marshal.GetLastWin32Error()); }
        finally { Marshal.FreeHGlobal(scheme); }
    }
}
