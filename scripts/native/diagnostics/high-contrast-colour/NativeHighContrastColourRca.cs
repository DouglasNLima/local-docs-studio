using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using LensDocsStudio.Native.Platform;
using Microsoft.Win32;

namespace LensDocsStudio.Native.Validation;

// Experimental validation overlay only. Never included in a product/package.
internal static class NativeHighContrastColourRca
{
    private static readonly List<object> notifications = new();
    private static string output = "";
    private static ResourceDictionary? resources;
    [DllImport("user32.dll")] private static extern uint GetSysColor(int index);
    [StructLayout(LayoutKind.Sequential)] private struct HighContrast { public uint Size, Flags; public IntPtr Scheme; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint size, ref HighContrast value, uint flags);
    private static object Stamp() => new { utc = DateTimeOffset.UtcNow, ticks = Stopwatch.GetTimestamp(), frequency = Stopwatch.Frequency };
    private static Color OsColour(int index) { var rgb = GetSysColor(index); return Color.FromRgb((byte)rgb, (byte)(rgb >> 8), (byte)(rgb >> 16)); }
    private static object OsState()
    {
        var state = new HighContrast { Size = (uint)Marshal.SizeOf<HighContrast>() };
        if (!SystemParametersInfo(0x42, state.Size, ref state, 0)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        return new { enabled = (state.Flags & 1) != 0, state.Flags, scheme = Marshal.PtrToStringUni(state.Scheme),
            highlight = OsColour(13).ToString(), highlightText = OsColour(14).ToString(), window = OsColour(5).ToString(),
            control = OsColour(15).ToString(), windowText = OsColour(8).ToString(), activeBorder = OsColour(10).ToString() };
    }
    private static object Binary(string path) => new { path, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
        version = FileVersionInfo.GetVersionInfo(path).ProductVersion };
    private static IEnumerable<object> Dictionaries(ResourceDictionary dictionary)
    {
        yield return new { identity = RuntimeHelpers.GetHashCode(dictionary), source = dictionary.Source?.ToString() };
        foreach (var child in dictionary.MergedDictionaries) foreach (var item in Dictionaries(child)) yield return item;
    }
    private static object? Brush(string key)
    {
        if (resources?[key] is not SolidColorBrush brush) return null;
        var expression = BindingOperations.GetBindingExpression(brush, SolidColorBrush.ColorProperty);
        var source = expression?.ParentBinding.Source;
        return new { effective = brush.Color.ToString(), requested = source?.GetType().GetProperty("Colour")?.GetValue(source)?.ToString(),
            bindingSource = source?.GetType().FullName, bindingPath = expression?.ParentBinding.Path?.Path,
            bindingStatus = expression?.Status.ToString(), brush.IsFrozen, identity = RuntimeHelpers.GetHashCode(brush) };
    }
    private static void Snapshot(string boundary)
    {
        var start = Stamp();
        // Fixed read order. No assertion, cache refresh or theme application here.
        var osBefore = OsState();
        var wpfFlag = SystemParameters.HighContrast;
        var foreground = SystemColors.HighlightTextColor; var background = SystemColors.HighlightColor;
        var other = new { window = SystemColors.WindowColor.ToString(), control = SystemColors.ControlColor.ToString(),
            windowText = SystemColors.WindowTextColor.ToString(), activeBorder = SystemColors.ActiveBorderColor.ToString(), accent = SystemColors.AccentColor.ToString() };
        var palette = new[] { "WindowBrush", "PanelBrush", "TextBrush", "MutedTextBrush", "HoverBrush", "PressedBrush", "BorderBrush", "AccentBrush", "PressedTextBrush", "AccentTextBrush" }
            .ToDictionary(key => key, Brush);
        var effectiveRatio = resources?["PressedTextBrush"] is SolidColorBrush text && resources["PressedBrush"] is SolidColorBrush surface
            ? NativeSyntaxChecks.Contrast(text.Color, surface.Color) : (double?)null;
        Write(boundary, new { boundary, start, nonAtomic = true,
            readOrder = "OS before; WPF flag; WPF highlight text/background; other WPF colours; palette; OS after",
            osBefore, wpfFlag, wpfHighlightText = foreground.ToString(), wpfHighlight = background.ToString(), other, palette,
            effectiveRatio, wpfRatio = NativeSyntaxChecks.Contrast(foreground, background), requiredContrast = 4.5,
            win32Ratio = NativeSyntaxChecks.Contrast(OsColour(14), OsColour(13)),
            dictionaries = resources is null ? null : Dictionaries(resources).ToArray(), osAfter = OsState(), end = Stamp() });
    }
    private static void Write(string name, object value) => File.WriteAllText(Path.Combine(output, name + ".json"),
        JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    private static void IdleBoundary()
    {
        var frame = new DispatcherFrame();
        var expired = false;
        var timeout = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromSeconds(20) };
        timeout.Tick += (_, _) => { expired = true; frame.Continue = false; };
        timeout.Start();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        try { Dispatcher.PushFrame(frame); } finally { timeout.Stop(); }
        if (expired) throw new TimeoutException("The single dispatcher idle boundary was not reached within 20 seconds.");
    }
    private static void NotificationBoundary(int since)
    {
        // Attempt 1 proved that queue idle can precede the actual settings messages.
        // Observe a real WPF HighContrast change and a native theme notification,
        // then complete one idle operation. Never wait for a colour or ratio to pass.
        var frame = new DispatcherFrame();
        var complete = false;
        void Check()
        {
            var observed = notifications.Skip(since).Select(item => JsonSerializer.SerializeToElement(item)).ToArray();
            if (observed.Any(item => item.TryGetProperty("wpfProperty", out var property) && property.GetString() == "HighContrast")
                && observed.Any(item => item.TryGetProperty("message", out var message) && message.GetInt32() == 0x31A))
            { complete = true; frame.Continue = false; }
        }
        EventHandler operation = (_, _) => Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(Check));
        // The notifications below invoke this completion check only on relevant
        // native/WPF events. No polling loop or self-queuing dispatcher operation.
        notificationObserved += operation;
        var timeout = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromSeconds(20) };
        timeout.Tick += (_, _) => frame.Continue = false;
        timeout.Start();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(Check));
        try { Dispatcher.PushFrame(frame); }
        finally { timeout.Stop(); notificationObserved -= operation; }
        if (!complete) throw new TimeoutException("Actual WPF HighContrast and native WM_THEMECHANGED notifications were not both observed within 20 seconds.");
    }
    private static event EventHandler? notificationObserved;
    private static int Child(string mode, string executable, string childOutput)
    {
        var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
        foreach (var argument in new[] { "--hc-colour-rca", executable, childOutput, mode }) info.ArgumentList.Add(argument);
        using var child = Process.Start(info) ?? throw new IOException("Fresh diagnostic process did not start.");
        if (!child.WaitForExit(60000)) { child.Kill(true); throw new TimeoutException("Fresh diagnostic process exceeded its bound."); }
        return child.ExitCode;
    }
    public static int Run(string executable, string directory, string mode)
    {
        output = Path.GetFullPath(directory);
        if (Directory.Exists(output)) throw new IOException("Preserve existing diagnostic attempts; output already exists.");
        Directory.CreateDirectory(output);
        var exits = new Dictionary<string, int>();
        try
        {
            if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" || Environment.GetEnvironmentVariable("RUNNER_OS") != "Windows"
                || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted") throw new InvalidOperationException("Disposable hosted Windows execution only.");
            Write("identity", new { mode, at = Stamp(), source = Environment.GetEnvironmentVariable("RCA_SOURCE"), tree = Environment.GetEnvironmentVariable("RCA_TREE"),
                overlay = Environment.GetEnvironmentVariable("RCA_OVERLAY"), overlayTree = Environment.GetEnvironmentVariable("RCA_OVERLAY_TREE"),
                originalFailure = "41345774785/124965136051 FAILURE retained", processId = Environment.ProcessId, sessionId = Process.GetCurrentProcess().SessionId,
                image = Environment.GetEnvironmentVariable("ImageOS"), imageVersion = Environment.GetEnvironmentVariable("ImageVersion"),
                os = RuntimeInformation.OSDescription, runtime = RuntimeInformation.FrameworkDescription,
                assemblies = new[] { typeof(object).Assembly, typeof(Application).Assembly, typeof(Visual).Assembly, typeof(ThemeService).Assembly, typeof(NativeHighContrastColourRca).Assembly }.Select(a => Binary(a.Location)).ToArray(),
                currentTheme = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes", "CurrentTheme", null) });
            _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            if (mode == "fresh")
            {
                Snapshot("E-fresh-before-apply"); resources = new(); ThemeService.Apply(resources, "System"); Snapshot("E-fresh-after-apply");
                return 0;
            }
            var original = JsonSerializer.Serialize(OsState());
            Snapshot("A-normal");
            // Deliberately create an observable HWND only in the transition control.
            using var hwnd = new HwndSource(new HwndSourceParameters("Goal56 colour notification control") { Width = 1, Height = 1, WindowStyle = unchecked((int)0x80000000) });
            hwnd.AddHook((IntPtr handle, int message, IntPtr wp, IntPtr lp, ref bool handled) =>
            {
                if (message is 0x15 or 0x1A or 0x31A or 0x320)
                {
                    notifications.Add(new { at = Stamp(), message, wParam = wp.ToInt64(), handle = handle.ToInt64() });
                    notificationObserved?.Invoke(null, EventArgs.Empty);
                }
                return IntPtr.Zero;
            });
            SystemParameters.StaticPropertyChanged += (_, e) =>
            {
                notifications.Add(new { at = Stamp(), wpfProperty = e.PropertyName });
                notificationObserved?.Invoke(null, EventArgs.Empty);
            };
            var transitionStart = notifications.Count;
            using (var highContrast = new NativeTestHighContrast())
            {
                Snapshot("B-after-SPI"); resources = new(); ThemeService.Apply(resources, "System"); Snapshot("C-after-Apply");
                IdleBoundary(); Snapshot("D-after-single-idle");
                NotificationBoundary(transitionStart); Snapshot("D-after-actual-notifications");
                exits["fresh"] = Child("fresh", executable, Path.Combine(output, "fresh"));
                exits["surface"] = NativeUiChecks.ObserveHighContrastCommand(executable, Path.Combine(output, "surface"));
                transitionStart = notifications.Count;
            }
            NotificationBoundary(transitionStart);
            IdleBoundary(); Snapshot("restored");
            var restored = JsonSerializer.Serialize(OsState());
            Write("restoration", new { original = JsonSerializer.Deserialize<JsonElement>(original), restored = JsonSerializer.Deserialize<JsonElement>(restored), exactOsStateRestored = original == restored });
            if (original != restored) throw new IOException("Original High Contrast flag, scheme or system colours were not restored.");
            return exits.Values.All(code => code == 0) ? 0 : 1;
        }
        catch (Exception error) { Write("exception", new { at = Stamp(), error = error.ToString() }); return 1; }
        finally
        {
            Write("notifications", notifications);
            Write("result", new { exits, originalFailure = "FAILURE retained", diagnosticAcceptance = false, at = Stamp() });
            try
            {
                Write("loaded-modules", Process.GetCurrentProcess().Modules.Cast<ProcessModule>().Where(m => m.FileName.Contains("dotnet", StringComparison.OrdinalIgnoreCase)
                    || m.ModuleName.Contains("clr", StringComparison.OrdinalIgnoreCase) || m.ModuleName.Contains("wpf", StringComparison.OrdinalIgnoreCase)).Select(m => Binary(m.FileName)).ToArray());
            }
            catch (Exception error) { Write("modules-unsupported", new { error = error.ToString() }); }
        }
    }
}

internal static partial class NativeUiChecks
{
    internal static int ObserveHighContrastCommand(string executable, string directory)
    {
        Directory.CreateDirectory(directory);
        var state = Path.Combine(directory, "state"); Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new LensDocsStudio.Core.State.UserPreferences(Theme: "System", AutomaticUpdateChecks: false)));
        var info = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false };
        info.ArgumentList.Add("--state-directory"); info.ArgumentList.Add(state);
        info.Environment["DOTNET_STARTUP_HOOKS"] = Environment.GetEnvironmentVariable("RCA_OBSERVER") ?? throw new IOException("Observer assembly absent.");
        info.Environment["RCA_SURFACE_OUTPUT"] = Path.GetFullPath(directory);
        var capturesAttempted = 0; var capturesCompleted = 0; var held = false;
        using var process = Process.Start(info) ?? throw new IOException("Actual product did not start.");
        try
        {
            using var automation = new FlaUI.UIA3.UIA3Automation();
            using var app = FlaUI.Core.Application.Attach(process.Id);
            var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(20)) ?? throw new IOException("Actual product window absent.");
            Wait(() => File.Exists(Path.Combine(directory, "loaded.json")), "Actual product observer did not reach Loaded.");
            FocusWindow(window);
            var command = window.FindFirstDescendant(cf => cf.ByName("New document (Ctrl+N)")) ?? throw new IOException("Actual command absent.");
            if (command.IsOffscreen || !command.IsEnabled || !window.BoundingRectangle.Contains(command.BoundingRectangle)) throw new IOException("Actual command is not visible and enabled.");
            var bounds = command.BoundingRectangle;
            FlaUI.Core.Input.Mouse.MoveTo(new System.Drawing.Point((int)(bounds.Left + bounds.Width / 2), (int)(bounds.Top + bounds.Height / 2)));
            FlaUI.Core.Input.Mouse.Down(FlaUI.Core.Input.MouseButton.Left); held = true;
            Wait(() => File.Exists(Path.Combine(directory, "pressed.json")), "Actual pressed presentation was not observed.");
            capturesAttempted++; window.CaptureToFile(Path.Combine(directory, "actual-pressed-command.png")); capturesCompleted++;
            File.WriteAllText(Path.Combine(directory, "uia.json"), JsonSerializer.Serialize(new { command.Name, command.AutomationId, command.IsEnabled, command.IsOffscreen,
                command.Properties.HasKeyboardFocus.Value, bounds = command.BoundingRectangle, processId = process.Id, sessionId = process.SessionId,
                pressed = "Native WPF IsPressed recorded in pressed.json; UIA Invoke has no pressed-state property", capturesAttempted, capturesCompleted }));
            return 0;
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(directory, "exception.json"), JsonSerializer.Serialize(new { error = error.ToString(), capturesAttempted, capturesCompleted })); return 1;
        }
        finally
        {
            if (held) { FlaUI.Core.Input.Mouse.MoveTo(new System.Drawing.Point(0, 0)); FlaUI.Core.Input.Mouse.Up(FlaUI.Core.Input.MouseButton.Left); }
            if (!process.HasExited) { process.Kill(true); process.WaitForExit(10000); }
        }
    }
}
