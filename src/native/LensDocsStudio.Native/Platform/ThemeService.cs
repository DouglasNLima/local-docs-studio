using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace LensDocsStudio.Native.Platform;

public static class ThemeService
{
    public static bool IsDark(string theme)
    {
        if (theme == "Dark") return true;
        if (theme == "Light") return false;
        return Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is int value && value == 0;
    }
    public static void Apply(ResourceDictionary resources, string theme)
    {
        var dark = IsDark(theme);
        foreach (var (key, light, night) in new[] {
            ("WindowBrush", "#FAFAFA", "#202124"), ("PanelBrush", "#F3F4F6", "#292B2F"),
            ("TextBrush", "#202124", "#F3F4F6"), ("BorderBrush", "#D1D5DB", "#51545B"), ("AccentBrush", "#FF883E", "#FF883E") })
            resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? night : light));
    }
}
