using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

// No colour reads, resource evaluation or dispatcher pumping until product Loaded.
public static class StartupHook
{
    private static string output = "";
    public static void Initialize()
    {
        output = Environment.GetEnvironmentVariable("RCA_SURFACE_OUTPUT") ?? "";
        if (output.Length == 0) return;
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((sender, _) =>
        {
            if (sender is not Window window || window.GetType().FullName != "LensDocsStudio.Native.MainWindow") return;
            window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => Snapshot(window, null, "loaded")));
            window.AddHandler(UIElement.PreviewMouseDownEvent, new MouseButtonEventHandler((_, e) =>
            {
                var node = e.OriginalSource as DependencyObject;
                while (node is not null && node is not ButtonBase) node = VisualTreeHelper.GetParent(node);
                if (node is ButtonBase button && AutomationProperties.GetName(button) == "New document (Ctrl+N)")
                    window.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() => Snapshot(window, button, "pressed")));
            }), true);
        }), true);
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index); yield return child;
            foreach (var item in Descendants(child)) yield return item;
        }
    }
    private static object? Colour(Brush? brush) => brush is SolidColorBrush solid ? solid.Color.ToString() : brush?.ToString();
    private static double? Contrast(Brush? foreground, Brush? background)
    {
        if (foreground is not SolidColorBrush first || background is not SolidColorBrush second) return null;
        double Luminance(Color colour)
        {
            double Channel(byte value) { var n = value / 255d; return n <= .04045 ? n / 12.92 : Math.Pow((n + .055) / 1.055, 2.4); }
            return .2126 * Channel(colour.R) + .7152 * Channel(colour.G) + .0722 * Channel(colour.B);
        }
        var a = Luminance(first.Color); var b = Luminance(second.Color); return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }
    private static IEnumerable<object> Dictionaries(ResourceDictionary dictionary)
    {
        yield return new { identity = RuntimeHelpers.GetHashCode(dictionary), source = dictionary.Source?.ToString() };
        foreach (var child in dictionary.MergedDictionaries) foreach (var item in Dictionaries(child)) yield return item;
    }
    private static void Snapshot(Window window, ButtonBase? target, string boundary)
    {
        try
        {
            var start = Stopwatch.GetTimestamp();
            var buttons = target is null ? Descendants(window).OfType<ButtonBase>().Where(b => AutomationProperties.GetName(b) == "New document (Ctrl+N)") : new[] { target };
            var commands = buttons.Select(button => new {
                name = AutomationProperties.GetName(button), button.IsPressed, button.IsMouseOver, button.IsKeyboardFocused, button.IsKeyboardFocusWithin,
                foreground = Colour(button.Foreground), background = Colour((button.Template.FindName("Surface", button) as Border)?.Background),
                contrast = Contrast(button.Foreground, (button.Template.FindName("Surface", button) as Border)?.Background), requiredContrast = 4.5,
                foregroundSource = DependencyPropertyHelper.GetValueSource(button, Control.ForegroundProperty).BaseValueSource.ToString(),
                styleMatchesDesktopCommand = ReferenceEquals(button.Style, button.TryFindResource("Desktop.CommandButton")),
                icons = Descendants(button).Where(child => child.GetType().FullName == "LensDocsStudio.Native.Controls.CommandIcon")
                    .Select(icon => new { foreground = Colour(icon.GetType().GetProperty("Foreground")?.GetValue(icon) as Brush),
                        contrast = Contrast(icon.GetType().GetProperty("Foreground")?.GetValue(icon) as Brush, (button.Template.FindName("Surface", button) as Border)?.Background) }).ToArray()
            }).ToArray();
            var palette = new[] { "PressedTextBrush", "PressedBrush" }.ToDictionary(key => key, key => {
                var brush = (SolidColorBrush)window.FindResource(key); var expression = BindingOperations.GetBindingExpression(brush, SolidColorBrush.ColorProperty);
                return new { colour = brush.Color.ToString(), requested = expression?.ParentBinding.Source?.GetType().GetProperty("Colour")?.GetValue(expression.ParentBinding.Source)?.ToString(),
                    status = expression?.Status.ToString(), brush.IsFrozen, identity = RuntimeHelpers.GetHashCode(brush) };
            });
            var report = new { boundary, nonAtomic = true, startTicks = start, endTicks = Stopwatch.GetTimestamp(), frequency = Stopwatch.Frequency, utc = DateTimeOffset.UtcNow,
                source = Environment.GetEnvironmentVariable("RCA_SOURCE"), tree = Environment.GetEnvironmentVariable("RCA_TREE"), overlay = Environment.GetEnvironmentVariable("RCA_OVERLAY"),
                processId = Environment.ProcessId, sessionId = Process.GetCurrentProcess().SessionId, commands, palette, highContrast = SystemParameters.HighContrast,
                wpfHighlightText = SystemColors.HighlightTextColor.ToString(), wpfHighlight = SystemColors.HighlightColor.ToString(),
                dictionaries = Dictionaries(Application.Current.Resources).ToArray(),
                applicationAssembly = new { path = window.GetType().Assembly.Location, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(window.GetType().Assembly.Location))) } };
            File.WriteAllText(Path.Combine(output, boundary + ".json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception error) { File.WriteAllText(Path.Combine(output, boundary + "-unsupported.json"), JsonSerializer.Serialize(new { error = error.ToString() })); }
    }
}
