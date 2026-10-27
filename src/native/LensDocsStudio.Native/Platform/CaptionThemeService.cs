using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LensDocsStudio.Native.Platform;

/// <summary>Styles the system-owned caption without replacing non-client controls or recreating windows.</summary>
public static class CaptionThemeService
{
    static CaptionThemeService()
    {
        // Constructors using FitInitialWindow attach before HWND creation. This
        // fallback also covers simple application-owned source/help dialogues.
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((sender, args) =>
        { if (sender is Window window && ReferenceEquals(args.OriginalSource, window)) Attach(window); }));
    }
    private static readonly ConditionalWeakTable<Window, Registration> Windows = new();
    private static string theme = "System";

    public static void Attach(Window window) => Windows.GetValue(window, owner => new Registration(owner));

    public static void Apply(string preference)
    {
        theme = preference;
        foreach (var entry in Windows) entry.Value.Update();
    }

    // Windows 11 documents attributes 20, 35 and 36. Older systems can reject
    // individual attributes; keep the native system caption as the fallback.
    private sealed class Registration
    {
        private readonly Window window;
        public Registration(Window window)
        {
            this.window = window;
            window.SourceInitialized += SourceInitialised;
            window.Closed += Closed;
            Update();
        }
        private void SourceInitialised(object? sender, EventArgs e) => Update();
        private void Closed(object? sender, EventArgs e)
        {
            window.SourceInitialized -= SourceInitialised;
            window.Closed -= Closed;
            Windows.Remove(window);
        }
        public void Update()
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;
            var highContrast = SystemParameters.HighContrast;
            var dark = !highContrast && ThemeService.IsDark(theme);
            Set(handle, 20, dark ? 1u : 0u);
            // Explicit colours also follow an app override when Windows uses the
            // opposite theme. In High Contrast return all colour authority to DWM.
            Set(handle, 35, highContrast ? uint.MaxValue : dark ? 0x00242120u : 0x00FAFAFAu);
            Set(handle, 36, highContrast ? uint.MaxValue : dark ? 0x00F6F4F3u : 0x00242120u);
        }
    }

    private static void Set(IntPtr handle, uint attribute, uint value)
    {
        try { _ = DwmSetWindowAttribute(handle, attribute, ref value, sizeof(uint)); }
        catch (DllNotFoundException) { /* Native system caption remains available. */ }
        catch (EntryPointNotFoundException) { /* Unsupported platform retains its caption. */ }
    }
    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(IntPtr window, uint attribute, ref uint value, uint size);
}
