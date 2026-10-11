using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace LensDocsStudio.Native.Platform;

public static class ThemeService
{
    private static ResourceDictionary? fluent;
    private static string? appliedTheme;
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
        var dark = IsDark(theme);
        var variant = theme == "System" ? "Fluent" : dark ? "Fluent.Dark" : "Fluent.Light";
        if (fluent is null || appliedTheme != variant)
        {
            if (fluent is not null) resources.MergedDictionaries.Remove(fluent);
            fluent = new ResourceDictionary { Source = new Uri($"pack://application:,,,/PresentationFramework.Fluent;component/Themes/{variant}.xaml", UriKind.Absolute) };
            resources.MergedDictionaries.Add(fluent); appliedTheme = variant;
        }
        foreach (var (key, light, night) in new[] {
            ("WindowBrush", "#FAFAFA", "#202124"), ("PanelBrush", "#F3F4F6", "#292B2F"),
            ("TextBrush", "#202124", "#F3F4F6"), ("BorderBrush", "#D1D5DB", "#51545B"), ("AccentBrush", "#FF883E", "#FF883E") })
            resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? night : light));
        if (SystemParameters.HighContrast)
        {
            resources["WindowBrush"] = SystemColors.WindowBrush; resources["PanelBrush"] = SystemColors.ControlBrush;
            resources["TextBrush"] = SystemColors.WindowTextBrush; resources["BorderBrush"] = SystemColors.ActiveBorderBrush;
            resources["AccentBrush"] = SystemColors.HighlightBrush;
            resources["AccentTextBrush"] = SystemColors.HighlightTextBrush;
        }
        else resources["AccentTextBrush"] = new SolidColorBrush(Color.FromRgb(32, 33, 36));
        foreach (var key in new[] { "AccentFillColorDefaultBrush", "AccentFillColorSecondaryBrush", "AccentFillColorTertiaryBrush", "AccentFillColorSelectedTextBackgroundBrush", "SystemFillColorAttentionBrush" })
            resources[key] = resources["AccentBrush"];
        resources["TextOnAccentFillColorPrimaryBrush"] = resources["AccentTextBrush"];
        resources["TextOnAccentFillColorSelectedTextBrush"] = resources["AccentTextBrush"];
    }
}
