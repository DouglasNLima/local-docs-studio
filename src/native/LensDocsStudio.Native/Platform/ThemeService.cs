using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace LensDocsStudio.Native.Platform;

public static class ThemeService
{
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
    public static void Apply(ResourceDictionary resources, string theme)
    {
        using var processing = System.Windows.Threading.Dispatcher.CurrentDispatcher.DisableProcessing();
        var dark = IsDark(theme);
        var variant = SystemParameters.HighContrast ? "Fluent.HC" : dark ? "Fluent.Dark" : "Fluent.Light";
        Themes.TryGetValue(resources, out var applied);
        if (applied?.Variant != variant || !resources.MergedDictionaries.Contains(applied.Dictionary))
        {
            if (applied is not null) resources.MergedDictionaries.Remove(applied.Dictionary);
            // WPF infers ThemeMode from a directly merged Fluent source and replaces that
            // dictionary during WM_THEMECHANGED. Keep one owner of these resources: our
            // deferred preference handler selects Light/Dark/HC inside an unlabelled wrapper.
            var wrapper = new ResourceDictionary();
            wrapper.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"pack://application:,,,/PresentationFramework.Fluent;component/Themes/{variant}.xaml", UriKind.Absolute) });
            resources.MergedDictionaries.Add(wrapper); Themes.Remove(resources); Themes.Add(resources, new(variant, wrapper));
        }
        foreach (var (key, light, night) in new[] {
            ("WindowBrush", "#FAFAFA", "#202124"), ("PanelBrush", "#F3F4F6", "#292B2F"),
            ("TextBrush", "#202124", "#F3F4F6"), ("MutedTextBrush", "#56606C", "#B5BBC5"),
            ("HoverBrush", "#EDEDED", "#383C43"), ("PressedBrush", "#DDE1E6", "#454B55"),
            ("BorderBrush", "#D1D5DB", "#51545B"), ("AccentBrush", "#FF883E", "#FF883E") })
            resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? night : light));
        resources["PressedTextBrush"] = resources["TextBrush"];
        if (SystemParameters.HighContrast)
        {
            resources["WindowBrush"] = SystemColors.WindowBrush; resources["PanelBrush"] = SystemColors.ControlBrush;
            resources["TextBrush"] = SystemColors.WindowTextBrush; resources["BorderBrush"] = SystemColors.ActiveBorderBrush;
            resources["AccentBrush"] = SystemColors.HighlightBrush;
            resources["AccentTextBrush"] = SystemColors.HighlightTextBrush;
            resources["MutedTextBrush"] = SystemColors.WindowTextBrush;
            resources["HoverBrush"] = SystemColors.ControlBrush;
            resources["PressedBrush"] = SystemColors.HighlightBrush;
            resources["PressedTextBrush"] = SystemColors.HighlightTextBrush;
        }
        else resources["AccentTextBrush"] = new SolidColorBrush(Color.FromRgb(32, 33, 36));
        foreach (var key in new[] { "AccentFillColorDefaultBrush", "AccentFillColorSecondaryBrush", "AccentFillColorTertiaryBrush", "AccentFillColorSelectedTextBackgroundBrush", "SystemFillColorAttentionBrush" })
            resources[key] = resources["AccentBrush"];
        resources["TextOnAccentFillColorPrimaryBrush"] = resources["AccentTextBrush"];
        resources["TextOnAccentFillColorSelectedTextBrush"] = resources["AccentTextBrush"];
    }
}
