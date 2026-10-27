using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native.Validation;

internal static class NativeThemeResourceChecks
{
    public static void Run(string output)
    {
        Directory.CreateDirectory(output);
        var application = Application.Current;
        if (application is null) { var native = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown }; native.InitializeComponent(); application = native; }
        var other = new ResourceDictionary();
        ThemeService.Apply(other, "Light");
        var otherText = (SolidColorBrush)other["TextBrush"];
        var otherColour = otherText.Color;
        ThemeService.Apply(application.Resources, "Light");
        var text = new TextBlock();
        var window = new Window { Content = text };
        CaptionThemeService.Attach(window);
        var handle = new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
        text.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        SolidColorBrush? owned = null;
        var samples = new List<object>();
        try
        {
            foreach (var theme in new[] { "Light", "Dark", "Light", "Dark", "Light" })
            {
                ThemeService.Apply(application.Resources, theme);
                var brush = (SolidColorBrush)application.Resources["TextBrush"];
                var expected = SystemParameters.HighContrast ? SystemColors.WindowTextColor
                    : (Color)ColorConverter.ConvertFromString(theme == "Dark" ? "#F3F4F6" : "#202124");
                if (brush.Color != expected || text.Foreground is not SolidColorBrush visible || visible.Color != expected)
                    throw new IOException($"A dynamic resource consumer retained an obsolete theme colour: {theme}, expected {expected}, resource {brush.Color}, consumer {(text.Foreground as SolidColorBrush)?.Color}.");
                if (owned is not null && !ReferenceEquals(owned, brush)) throw new IOException("Theme changes replaced an owned colour brush.");
                if (ReferenceEquals(otherText, brush) || otherText.Color != otherColour)
                    throw new IOException("Theme resources crossed their owner dictionaries.");
                owned = brush;
                var supported = DwmGetWindowAttribute(handle, 35, out var caption, sizeof(uint)) >= 0;
                var expectedCaption = SystemParameters.HighContrast ? uint.MaxValue : theme == "Dark" ? 0x00242120u : 0x00FAFAFAu;
                if (supported && caption != expectedCaption) throw new IOException("The native caption retained an obsolete theme colour.");
                samples.Add(new { Theme = theme, Colour = brush.Color.ToString(), ConsumerCurrent = true, OwnerIsolated = true, BrushRetained = true,
                    DwmCaptionSupported = supported, CaptionColour = caption.ToString("X8"), CaptionMatches = supported && caption == expectedCaption });
            }
        }
        finally { window.Close(); }
        File.WriteAllText(Path.Combine(output, "theme-resources.json"), JsonSerializer.Serialize(new {
            Passed = true, Samples = samples, ActualHighContrast = SystemParameters.HighContrast,
            Scope = "Unshown WPF resource-consumer/ownership regression only; actual shown appearance and OS transitions are separate checks."
        }));
    }
    [System.Runtime.InteropServices.DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmGetWindowAttribute(IntPtr window, uint attribute, out uint value, uint size);
}
