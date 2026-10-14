using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using LensDocsStudio.Native.Controls;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native.Validation;

internal static class NativeToolbarChecks
{
    public static void Run(string output)
    {
        var application = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var resources = application.Resources;
        var cases = new List<object>();
        try
        {
            foreach (var theme in new[] { "Light", "Dark" })
            {
                application.Resources = new ResourceDictionary();
                ThemeService.Apply(application.Resources, theme);
                var pressedForeground = (System.Windows.Media.SolidColorBrush)application.Resources["PressedTextBrush"];
                var pressedBackground = (System.Windows.Media.SolidColorBrush)application.Resources["PressedBrush"];
                if (NativeSyntaxChecks.Contrast(pressedForeground.Color, pressedBackground.Color) < 4.5)
                    throw new IOException("Pressed command text and icons lost readable contrast in the " + theme + " theme.");
                foreach (var file in new[] { "CommandIcons", "DesktopStyles" })
                    application.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"pack://application:,,,/LensDocsStudio.Native;component/Resources/{file}.xaml") });
                foreach (var width in new[] { 180d, 320d, 500d })
                {
                    var toolbar = new ToolBar { Style = (Style)application.Resources["Desktop.Toolbar"] };
                    foreach (var icon in new[] { "Bold", "Italic", "Strike", "Link", "Image", "Table", "Mermaid", "Undo", "Redo" })
                        toolbar.Items.Add(new Button { Content = new CommandIcon { Kind = icon } });
                    toolbar.Measure(new(width, 48)); toolbar.Arrange(new(new Point(), new Size(width, 48))); toolbar.UpdateLayout();
                    var more = (ToggleButton)toolbar.Template.FindName("Overflow", toolbar);
                    var panel = (ToolBarPanel)toolbar.Template.FindName("PART_ToolBarPanel", toolbar);
                    var overflow = toolbar.Items.Cast<Button>().Count(ToolBar.GetIsOverflowItem);
                    var bounds = new Rect(more.TranslatePoint(new(), toolbar), more.RenderSize);
                    var observation = new { Theme = theme, Width = width, ToolbarWidth = toolbar.ActualWidth, PanelWidth = panel.ActualWidth,
                        toolbar.HasOverflowItems, OverflowCount = overflow, MoreVisibility = more.Visibility.ToString(), MoreBounds = bounds.ToString() };
                    cases.Add(observation);
                    if (width < 320 && (overflow == 0 || !toolbar.HasOverflowItems || more.Visibility != Visibility.Visible || bounds.Right > width || bounds.Width < 20))
                        throw new IOException("Native toolbar overflow did not retain its accessible command button: " + JsonSerializer.Serialize(observation));
                }
            }
        }
        finally
        {
            application.Resources = resources; Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "native-toolbar-layout.json"), JsonSerializer.Serialize(cases, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
