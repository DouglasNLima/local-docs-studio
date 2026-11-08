// Read-only Win32 observation. No WPF, Windows Forms, windows, input or setters.
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class Program
{
    private const uint GetWorkArea = 0x30, GetHighContrast = 0x42;
    private const int MaximumMonitors = 8, MaximumModes = 256;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, IncludeFields = true };

    private static int Main()
    {
        try
        {
            if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" ||
                Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted" ||
                Environment.GetEnvironmentVariable("RUNNER_OS") != "Windows" ||
                Environment.GetEnvironmentVariable("GOAL77_READ_ONLY_DISCOVERY") != "true")
                throw new InvalidOperationException("Only explicitly guarded hosted read-only discovery is permitted.");
            if (Marshal.SizeOf<Rect>() != 16 || Marshal.SizeOf<MonitorInfo>() != 104 ||
                Marshal.SizeOf<DisplayDevice>() != 840 || Marshal.SizeOf<DevMode>() != 220 ||
                Marshal.SizeOf<HighContrast>() != 16 || IntPtr.Size != 8)
                throw new InvalidOperationException("Win32 x64 observation ABI differs.");
            var before = Snapshot();
            var after = Snapshot(); // Two fixed reads; no waiting or retry-until-equal.
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name).Order().ToArray();
            if (assemblies.Any(n => n is "PresentationFramework" or "PresentationCore" or "WindowsBase" or "System.Windows.Forms"))
                throw new InvalidOperationException("A forbidden UI assembly was loaded.");
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                schemaVersion = 1, kind = "READ_ONLY_WIN32_DESKTOP", processId = Environment.ProcessId,
                processPath = Environment.ProcessPath, processSha256 = Hash(Environment.ProcessPath!),
                assemblySha256 = Hash(Assembly.GetExecutingAssembly().Location),
                sourceRevision = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
                runtimeVersion = Environment.Version.ToString(), corePath = typeof(object).Assembly.Location,
                coreSha256 = Hash(typeof(object).Assembly.Location), assemblies, before, after,
                qualificationProcesses = 0, windowCreation = false, wpfResourceAccess = false,
                inputChanges = false, displayChanges = false, appearanceChanges = false
            }, Json));
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 2; }
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static void Check(bool success) { if (!success) throw new Win32Exception(Marshal.GetLastWin32Error()); }
    private static void CheckHr(int value) { if (value < 0) Marshal.ThrowExceptionForHR(value); }

    private static object Snapshot()
    {
        var started = Stopwatch.GetTimestamp();
        CheckHr(GetProcessDpiAwareness(IntPtr.Zero, out var awareness));
        var context = GetThreadDpiAwarenessContext();
        var threadAwareness = GetAwarenessFromDpiAwarenessContext(context);
        Check(SystemParametersInfoRect(GetWorkArea, 0, out var primaryWork, 0));
        var monitors = new List<object>();
        Exception? callbackFailure = null;
        MonitorCallback callback = (IntPtr handle, IntPtr _, ref Rect __, IntPtr ___) =>
        {
            try
            {
                if (monitors.Count >= MaximumMonitors) throw new IOException("Monitor observation bound exceeded.");
                var info = new MonitorInfo { size = (uint)Marshal.SizeOf<MonitorInfo>(), device = "" };
                Check(GetMonitorInfoW(handle, ref info));
                CheckHr(GetScaleFactorForMonitor(handle, out var scale));
                var device = new DisplayDevice { size = (uint)Marshal.SizeOf<DisplayDevice>() };
                Check(EnumDisplayDevicesW(info.device, 0, ref device, 0));
                var current = Mode(info.device, -1);
                var modes = new List<object>();
                var complete = false;
                for (var index = 0; index <= MaximumModes; index++)
                {
                    var mode = EmptyMode();
                    if (!EnumDisplaySettingsW(info.device, index, ref mode)) { complete = true; break; }
                    if (index == MaximumModes) throw new IOException("Display-mode observation bound exceeded.");
                    modes.Add(ModeData(mode));
                }
                monitors.Add(new { info.device, primary = (info.flags & 1) != 0,
                    monitorPixels = info.monitor, workPixels = info.work, scalePercent = scale,
                    monitorDevice = new { device.name, device.description, device.id, device.key, device.flags },
                    currentMode = ModeData(current), enumeratedModes = modes, modesComplete = complete });
                return true;
            }
            catch (Exception error) { callbackFailure = error; return false; }
        };
        var enumerated = EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        if (callbackFailure is not null) throw callbackFailure;
        Check(enumerated);
        Check(ProcessIdToSessionId((uint)Environment.ProcessId, out var session));
        var input = OpenInputDesktop(0, false, 1); // DESKTOP_READOBJECTS; no switch/write rights.
        Check(input != IntPtr.Zero);
        string inputName;
        try { inputName = ObjectName(input); }
        finally { Check(CloseDesktop(input)); }
        return new
        {
            started, primaryWorkPixels = primaryWork,
            dpi = new { processAwareness = awareness, threadAwareness, system = GetDpiForSystem(),
                declaration = "System aware apphost; no DPI-setting API" },
            monitors, sessionId = session, activeConsoleSession = WTSGetActiveConsoleSessionId(),
            remoteSession = GetSystemMetrics(0x1000) != 0,
            desktop = ObjectName(GetThreadDesktop(GetCurrentThreadId())), inputDesktop = inputName,
            windowStation = ObjectName(GetProcessWindowStation()),
            windowConstraintsPixels = new { maximumTrackWidth = GetSystemMetrics(59), maximumTrackHeight = GetSystemMetrics(60),
                minimumTrackWidth = GetSystemMetrics(34), minimumTrackHeight = GetSystemMetrics(35) },
            appearance = ReadAppearance(), finished = Stopwatch.GetTimestamp()
        };
    }

    private static object ReadAppearance()
    {
        var state = new HighContrast { size = (uint)Marshal.SizeOf<HighContrast>() };
        Check(SystemParametersInfoContrast(GetHighContrast, state.size, ref state, 0));
        string? scheme = null;
        var wasNull = state.scheme == IntPtr.Zero;
        try
        {
            if (!wasNull)
            {
                var value = new StringBuilder();
                for (var index = 0; ; index++)
                {
                    if (index >= 32768) throw new IOException("Returned scheme exceeded the UTF-16 bound.");
                    var character = (char)Marshal.ReadInt16(state.scheme, index * 2);
                    if (character == '\0') break;
                    value.Append(character);
                }
                scheme = value.ToString();
            }
        }
        finally
        {
            // HIGHCONTRASTW documents system allocation and LocalFree ownership.
            if (!wasNull && LocalFree(state.scheme) != IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        var theme = new StringBuilder(260); var colour = new StringBuilder(260); var size = new StringBuilder(260);
        var themeResult = GetCurrentThemeName(theme, theme.Capacity, colour, colour.Capacity, size, size.Capacity);
        var colours = new Dictionary<string, uint>();
        foreach (var index in new[] { 2, 3, 5, 6, 7, 8, 9, 13, 14, 15, 16, 17, 18, 20, 26 })
            colours[index.ToString(System.Globalization.CultureInfo.InvariantCulture)] = GetSysColor(index);
        return new { flags = state.flags, enabled = (state.flags & 1) != 0, schemePointerWasNull = wasNull,
            schemeKind = wasNull ? "NULL" : scheme!.Length == 0 ? "EMPTY" : "NAMED", scheme,
            getBufferReleased = true, colours, theme = new { hresult = themeResult,
                file = theme.ToString(), colour = colour.ToString(), size = size.ToString() } };
    }

    private static string ObjectName(IntPtr handle)
    {
        var value = new StringBuilder(256);
        Check(GetUserObjectInformationW(handle, 2, value, value.Capacity * 2, out _));
        return value.ToString();
    }
    private static DevMode EmptyMode() => new() { size = (ushort)Marshal.SizeOf<DevMode>(), deviceName = "", formName = "" };
    private static DevMode Mode(string device, int index) { var mode = EmptyMode(); Check(EnumDisplaySettingsW(device, index, ref mode)); return mode; }
    private static object ModeData(DevMode mode) => new { width = mode.width, height = mode.height,
        bitsPerPixel = mode.bits, refreshHz = mode.frequency, x = mode.x, y = mode.y, orientation = mode.orientation };

    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int left, top, right, bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct MonitorInfo
    {
        public uint size; public Rect monitor, work; public uint flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string device;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct DisplayDevice
    {
        public uint size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string description;
        public uint flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string key;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string deviceName;
        public ushort spec, driver, size, extra; public uint fields; public int x, y; public uint orientation, fixedOutput;
        public short colour, duplex, yResolution, ttOption, collate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string formName;
        public ushort logPixels; public uint bits, width, height, displayFlags, frequency, icmMethod, icmIntent,
            mediaType, ditherType, reserved1, reserved2, panningWidth, panningHeight;
    }
    [StructLayout(LayoutKind.Sequential)] private struct HighContrast { public uint size, flags; public IntPtr scheme; }
    private delegate bool MonitorCallback(IntPtr monitor, IntPtr dc, ref Rect rect, IntPtr data);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)] private static extern bool SystemParametersInfoRect(uint action, uint parameter, out Rect value, uint flags);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)] private static extern bool SystemParametersInfoContrast(uint action, uint parameter, ref HighContrast value, uint flags);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorCallback callback, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool GetMonitorInfoW(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool EnumDisplayDevicesW(string name, uint index, ref DisplayDevice device, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool EnumDisplaySettingsW(string name, int index, ref DevMode mode);
    [DllImport("shcore.dll")] private static extern int GetProcessDpiAwareness(IntPtr process, out int awareness);
    [DllImport("shcore.dll")] private static extern int GetScaleFactorForMonitor(IntPtr monitor, out int factor);
    [DllImport("user32.dll")] private static extern IntPtr GetThreadDpiAwarenessContext();
    [DllImport("user32.dll")] private static extern int GetAwarenessFromDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] private static extern uint GetDpiForSystem();
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern uint GetSysColor(int index);
    [DllImport("user32.dll")] private static extern IntPtr GetThreadDesktop(uint thread);
    [DllImport("user32.dll")] private static extern IntPtr GetProcessWindowStation();
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool GetUserObjectInformationW(IntPtr handle, int index, StringBuilder buffer, int length, out uint needed);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr LocalFree(IntPtr memory);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool ProcessIdToSessionId(uint process, out uint session);
    [DllImport("kernel32.dll")] private static extern uint WTSGetActiveConsoleSessionId();
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] private static extern int GetCurrentThemeName(StringBuilder file, int fileCount, StringBuilder colour, int colourCount, StringBuilder size, int sizeCount);
}
