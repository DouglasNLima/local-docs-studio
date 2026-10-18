using System.Windows;
using System.Windows.Media;
using System.Windows.Data;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Platform;

public static class ThemeService
{
    [StructLayout(LayoutKind.Sequential)]
    private struct HighContrastState { public uint Size; public uint Flags; public IntPtr Scheme; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint parameter, ref HighContrastState value, uint flags);

    internal static bool IsCurrentHighContrast(bool expected)
    {
        // WPF's cached property can lag a synchronous Windows settings broadcast.
        // Read only the OS flag at commit boundaries; never change system settings.
        var state = new HighContrastState { Size = (uint)Marshal.SizeOf<HighContrastState>() };
        return SystemParametersInfo(0x42, state.Size, ref state, 0) && ((state.Flags & 1) != 0) == expected;
    }
    private sealed record AppliedTheme(string Variant, ResourceDictionary Dictionary);
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ResourceDictionary, AppliedTheme> Themes = new();
    public static bool IsDark(string theme)
    {
        if (SystemParameters.HighContrast)
        {
            var colour = SystemColors.WindowColor;
            return colour.R * 0.2126 + colour.G * 0.7152 + colour.B * 0.0722 < 128;
        }
        if (theme == "Dark") return true;
        if (theme == "Light") return false;
        return Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is int value && value == 0;
    }
    // Binding prevents these resource brushes from being frozen. Each palette is
    // owned by one resource dictionary/dispatcher and has ten fixed colour slots.
    // Changing a slot updates its consumers without a whole-document resource walk.
    private sealed class ColourSlot : DependencyObject
    {
        public static readonly DependencyProperty ColourProperty = DependencyProperty.Register(nameof(Colour), typeof(Color), typeof(ColourSlot));
        public Color Colour { get => (Color)GetValue(ColourProperty); set => SetValue(ColourProperty, value); }
        public SolidColorBrush Brush { get; }
        public ColourSlot(Color colour)
        {
            Colour = colour; Brush = new();
            BindingOperations.SetBinding(Brush, SolidColorBrush.ColorProperty, new Binding(nameof(Colour)) { Source = this, Mode = BindingMode.OneWay });
        }
    }
    private sealed class Palette
    {
        private readonly Dictionary<string, ColourSlot> colours = new(StringComparer.Ordinal);
        public SolidColorBrush Set(string key, Color colour)
        {
            if (!colours.TryGetValue(key, out var slot)) colours.Add(key, slot = new(colour));
            else slot.Colour = colour;
            return slot.Brush;
        }
    }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ResourceDictionary, Palette> Palettes = new();

    public static void Apply(ResourceDictionary resources, string theme)
    {
        using var processing = System.Windows.Threading.Dispatcher.CurrentDispatcher.DisableProcessing();
        PreviewConstructionEvents.Log.Stage("theme-update-start");
        try
        {
            var dark = IsDark(theme); var highContrast = SystemParameters.HighContrast;
            var variant = highContrast ? "Fluent.HC" : dark ? "Fluent.Dark" : "Fluent.Light";
            Themes.TryGetValue(resources, out var applied);
            if (applied?.Variant != variant || !resources.MergedDictionaries.Contains(applied.Dictionary))
            {
                // Keep one owner of Fluent resources. A replacement sends one
                // invalidation instead of removing and adding the same owner.
                var wrapper = new ResourceDictionary();
                wrapper.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"pack://application:,,,/PresentationFramework.Fluent;component/Themes/{variant}.xaml", UriKind.Absolute) });
                var index = applied is null ? -1 : resources.MergedDictionaries.IndexOf(applied.Dictionary);
                if (index < 0) resources.MergedDictionaries.Add(wrapper); else resources.MergedDictionaries[index] = wrapper;
                Themes.Remove(resources); Themes.Add(resources, new(variant, wrapper));
            }
            var palette = Palettes.GetValue(resources, _ => new());
            foreach (var (key, light, night) in new[] {
                ("WindowBrush", "#FAFAFA", "#202124"), ("PanelBrush", "#F3F4F6", "#292B2F"),
                ("TextBrush", "#202124", "#F3F4F6"), ("MutedTextBrush", "#56606C", "#B5BBC5"),
                ("HoverBrush", "#EDEDED", "#383C43"), ("PressedBrush", "#DDE1E6", "#454B55"),
                ("BorderBrush", "#D1D5DB", "#51545B"), ("AccentBrush", "#FF883E", "#FF883E") })
            {
                var colour = highContrast ? key switch {
                    "WindowBrush" => SystemColors.WindowColor, "PanelBrush" or "HoverBrush" => SystemColors.ControlColor,
                    "TextBrush" or "MutedTextBrush" => SystemColors.WindowTextColor, "BorderBrush" => SystemColors.ActiveBorderColor,
                    _ => SystemColors.HighlightColor
                } : (Color)ColorConverter.ConvertFromString(dark ? night : light);
                Set(key, palette.Set(key, colour));
            }
            Set("PressedTextBrush", palette.Set("PressedTextBrush", highContrast ? SystemColors.HighlightTextColor
                : (Color)ColorConverter.ConvertFromString(dark ? "#F3F4F6" : "#202124")));
            Set("AccentTextBrush", palette.Set("AccentTextBrush", highContrast ? SystemColors.HighlightTextColor : Color.FromRgb(32, 33, 36)));
            foreach (var key in new[] { "AccentFillColorDefaultBrush", "AccentFillColorSecondaryBrush", "AccentFillColorTertiaryBrush", "AccentFillColorSelectedTextBackgroundBrush", "SystemFillColorAttentionBrush" })
                Set(key, resources["AccentBrush"]);
            Set("TextOnAccentFillColorPrimaryBrush", resources["AccentTextBrush"]);
            Set("TextOnAccentFillColorSelectedTextBrush", resources["AccentTextBrush"]);
            void Set(string key, object value) { if (!ReferenceEquals(resources[key], value)) resources[key] = value; }
        }
        finally { PreviewConstructionEvents.Log.Stage("theme-update-end"); }
    }
}
