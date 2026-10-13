using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace LensDocsStudio.Native.Validation;

/// <summary>Locates the known test window before requesting any UI Automation provider.</summary>
internal static class NativeWindowDiscovery
{
    public static IReadOnlyList<nint> FindTopLevelWindows(string className, string captionFragment)
    {
        var matches = new List<nint>();
        var windowClass = new StringBuilder(256); var caption = new StringBuilder(4096);
        if (!EnumWindows((window, _) =>
        {
            windowClass.Clear();
            if (GetClassName(window, windowClass, windowClass.Capacity) == 0
                || !windowClass.ToString().Equals(className, StringComparison.Ordinal)) return true;
            caption.Clear();
            if (GetWindowText(window, caption, caption.Capacity) != 0
                && caption.ToString().Contains(captionFragment, StringComparison.Ordinal)) matches.Add(window);
            return true;
        }, 0)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Native test window enumeration failed.");
        return matches;
    }

    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool WindowCallback(nint window, nint parameter);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(WindowCallback callback, nint parameter);
    [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassName(nint window, StringBuilder name, int capacity);
    [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowText(nint window, StringBuilder text, int capacity);
}
