using System.Diagnostics;
using System.Runtime.InteropServices;
using FlaUI.Core.AutomationElements;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static void AssertHostForeground(int processId, Window? window = null, AutomationElement? focus = null)
    {
        var foreground = GetForegroundWindow();
        GetWindowThreadProcessId(foreground, out var owner);
        var desktop = HostFindOpenInputDesktop(0, false, 1);
        var accessible = false;
        if (desktop != IntPtr.Zero)
        {
            var buffer = Marshal.AllocHGlobal(1024);
            try { if (HostFindGetUserObjectInformation(desktop, 2, buffer, 1024, out _)) accessible = Marshal.PtrToStringUni(buffer) == "Default"; }
            finally { Marshal.FreeHGlobal(buffer); HostFindCloseDesktop(desktop); }
        }
        if (owner != processId || window is not null && foreground != window.Properties.NativeWindowHandle.Value || !accessible
            || Process.GetCurrentProcess().SessionId != HostActiveConsoleSession() || focus is not null && !focus.Properties.HasKeyboardFocus.Value)
            throw new HostFindInputBlockedException("Expected candidate PID/HWND/focus must remain foreground on the unlocked Default console desktop; no activation or input retry is permitted.");
    }
}
