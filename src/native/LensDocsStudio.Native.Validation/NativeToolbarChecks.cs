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
            foreach (var width in new[] { 860d, 1024d, 1440d, 1920d })
            {
                var fitted = DesktopPanelSizing.Fit(width, true, 600, 600);
                if (fitted.Sidebar < 150 || fitted.Outline < 160 || fitted.Sidebar > 600 || fitted.Outline > 600
                    || width - 26 - fitted.Sidebar - fitted.Outline < 519.9)
                    throw new IOException("Combined persisted panel widths displaced the minimum usable document panes.");
            }
            if (DesktopPanelSizing.Fit(1920, true, 600, 600) != new DesktopPanelWidths(600, 600)
                || DesktopPanelSizing.Fit(860, true, 0, 0) != new DesktopPanelWidths(0, 0))
                throw new IOException("Panel sizing lost preferred wide-window widths or hidden-panel space.");
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
                    using var host = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("Native toolbar geometry validation")
                        { Width = (int)width, Height = 48, WindowStyle = unchecked((int)0x80000000) });
                    host.RootVisual = toolbar;
                    toolbar.Measure(new(width, 48)); toolbar.Arrange(new(new Point(), new Size(width, 48))); toolbar.UpdateLayout();
                    var more = (ToggleButton)toolbar.Template.FindName("Overflow", toolbar);
                    var panel = (ToolBarPanel)toolbar.Template.FindName("PART_ToolBarPanel", toolbar);
                    var overflow = toolbar.Items.Cast<Button>().Count(ToolBar.GetIsOverflowItem);
                    var bounds = new Rect(more.TranslatePoint(new(), toolbar), more.RenderSize);
                    var moreIcon = (CommandIcon)more.Content;
                    var painted = new System.Windows.Media.Imaging.RenderTargetBitmap(18, 18, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                    var drawing = new System.Windows.Media.DrawingVisual();
                    using (var context = drawing.RenderOpen()) context.DrawRectangle(new System.Windows.Media.VisualBrush(moreIcon), null, new Rect(0, 0, 18, 18));
                    painted.Render(drawing);
                    var pixels = new byte[18 * 18 * 4]; painted.CopyPixels(pixels, 18 * 4, 0);
                    var ink = Enumerable.Range(0, 18 * 18).Count(index => pixels[index * 4 + 3] > 0);
                    var observation = new { Theme = theme, Width = width, ToolbarWidth = toolbar.ActualWidth, PanelWidth = panel.ActualWidth,
                        toolbar.HasOverflowItems, OverflowCount = overflow, MoreVisibility = more.Visibility.ToString(), MoreBounds = bounds.ToString(),
                        MoreIconWidth = moreIcon.ActualWidth, MoreIconHeight = moreIcon.ActualHeight, MorePaintedPixels = ink,
                        MoreGeometry = (moreIcon.TryFindResource("Icon.More") as System.Windows.Media.Geometry)?.Bounds.ToString(),
                        MoreForeground = moreIcon.Foreground.ToString(), MoreVisible = moreIcon.IsVisible };
                    cases.Add(observation);
                    if (width < 320 && (overflow == 0 || !toolbar.HasOverflowItems || more.Visibility != Visibility.Visible || bounds.Right > width || bounds.Width < 20
                        || moreIcon.ActualWidth < 12 || moreIcon.ActualHeight < 12 || ink < 6))
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
