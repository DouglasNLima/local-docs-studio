using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Interop;

namespace LensDocsStudio.Native.Validation;

internal static class NativeWindowDiscoveryChecks
{
    public static void Run(string output)
    {
        var caption = "Lens native discovery Café Ω " + Guid.NewGuid().ToString("N");
        var matches = new List<string>(); string windowClass;
        using (var host = new HwndSource(new HwndSourceParameters(caption)
            { Width = 64, Height = 32, WindowStyle = unchecked((int)0x80000000) }))
        {
            if (IsWindowVisible(host.Handle)) throw new IOException("The discovery fixture must remain hidden.");
            var buffer = new StringBuilder(256);
            if (GetClassName(host.Handle, buffer, buffer.Capacity) == 0) throw new IOException("The hidden fixture class is unavailable.");
            windowClass = buffer.ToString();
            if (!NativeWindowDiscovery.FindTopLevelWindows(windowClass, caption).SequenceEqual(new[] { host.Handle }))
                throw new IOException("Native discovery did not return the exact hidden fixture handle and Unicode caption.");
            matches.Add("Exact native class and Unicode caption return only the owned hidden handle");
            if (NativeWindowDiscovery.FindTopLevelWindows(windowClass + "-different", caption).Count != 0)
                throw new IOException("Native discovery accepted a different window class.");
            matches.Add("Different classes remain excluded");
            if (NativeWindowDiscovery.FindTopLevelWindows(windowClass, caption + "-different").Count != 0)
                throw new IOException("Native discovery accepted a different caption.");
            matches.Add("Different captions remain excluded");
        }
        if (NativeWindowDiscovery.FindTopLevelWindows(windowClass, caption).Count != 0)
            throw new IOException("A destroyed hidden window remained discoverable.");
        matches.Add("Disposed windows do not remain in the result");
        File.WriteAllText(Path.Combine(output, "native-window-discovery.json"), JsonSerializer.Serialize(new
        { Passed = true, Checks = matches, ForegroundWindowCreated = false,
            Limitation = "Win32 enumeration of one owned hidden HWND. Actual Inno class/provider attachment requires hosted installer validation." },
            new JsonSerializerOptions { WriteIndented = true }));
    }

    [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint window, StringBuilder name, int capacity);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);
}
