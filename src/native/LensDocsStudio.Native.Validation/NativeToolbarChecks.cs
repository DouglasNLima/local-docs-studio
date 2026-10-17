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
                var ownedTheme = application.Resources.MergedDictionaries.Single();
                ThemeService.Apply(application.Resources, theme);
                // Read the .NET 10 evaluation property only to detect accidental WPF ownership.
                // Product code does not depend on or set this experimental API.
#pragma warning disable WPF0001
                if (application.ThemeMode != ThemeMode.None || ownedTheme.Source is not null
                    || ownedTheme.MergedDictionaries.Count != 1
                    || !ReferenceEquals(ownedTheme, application.Resources.MergedDictionaries.Single()))
                    throw new IOException("Native appearance resources acquired a second WPF theme owner or changed on an unchanged preference.");
                ThemeService.Apply(application.Resources, "System");
                if (application.ThemeMode != ThemeMode.None || application.Resources.MergedDictionaries.Count != 1)
                    throw new IOException("System appearance enabled automatic Fluent dictionary replacement alongside the native preference handler.");
#pragma warning restore WPF0001
                ThemeService.Apply(application.Resources, theme);
                var pressedForeground = (System.Windows.Media.SolidColorBrush)application.Resources["PressedTextBrush"];
                var pressedBackground = (System.Windows.Media.SolidColorBrush)application.Resources["PressedBrush"];
                if (NativeSyntaxChecks.Contrast(pressedForeground.Color, pressedBackground.Color) < 4.5)
                    throw new IOException("Pressed command text and icons lost readable contrast in the " + theme + " theme.");
                foreach (var file in new[] { "CommandIcons", "DesktopStyles" })
                    application.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"pack://application:,,,/LensDocsStudio.Native;component/Resources/{file}.xaml") });
                VerifyFileIcons(application, theme, output);
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
                    var toolbarPaint = new System.Windows.Media.Imaging.RenderTargetBitmap((int)width, 48, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                    toolbarPaint.Render(toolbar);
                    var toolbarPixels = new byte[(int)width * 48 * 4]; toolbarPaint.CopyPixels(toolbarPixels, (int)width * 4, 0);
                    var foreground = ((System.Windows.Media.SolidColorBrush)moreIcon.Foreground).Color;
                    var iconPosition = moreIcon.TranslatePoint(new(), toolbar);
                    var hostedInk = 0;
                    for (var y = Math.Max(0, (int)iconPosition.Y); y < Math.Min(48, (int)(iconPosition.Y + moreIcon.ActualHeight)); y++)
                    for (var x = Math.Max(0, (int)iconPosition.X); x < Math.Min((int)width, (int)(iconPosition.X + moreIcon.ActualWidth)); x++)
                    {
                        var pixel = (y * (int)width + x) * 4;
                        if (toolbarPixels[pixel + 3] == 255 && Math.Abs(toolbarPixels[pixel] - foreground.B) < 30
                            && Math.Abs(toolbarPixels[pixel + 1] - foreground.G) < 30 && Math.Abs(toolbarPixels[pixel + 2] - foreground.R) < 30) hostedInk++;
                    }
                    var observation = new { Theme = theme, Width = width, ToolbarWidth = toolbar.ActualWidth, PanelWidth = panel.ActualWidth,
                        toolbar.HasOverflowItems, OverflowCount = overflow, MoreVisibility = more.Visibility.ToString(), MoreBounds = bounds.ToString(),
                        MoreIconWidth = moreIcon.ActualWidth, MoreIconHeight = moreIcon.ActualHeight, MorePaintedPixels = ink, MorePixelsInsideToolbar = hostedInk,
                        MoreGeometry = (moreIcon.TryFindResource("Icon.More") as System.Windows.Media.Geometry)?.Bounds.ToString(),
                        MoreForeground = moreIcon.Foreground.ToString(), MoreVisible = moreIcon.IsVisible };
                    cases.Add(observation);
                    if (width < 320 && overflow == 0 || overflow > 0 && (!toolbar.HasOverflowItems || more.Visibility != Visibility.Visible || bounds.Right > width || bounds.Width < 20
                        || moreIcon.ActualWidth < 12 || moreIcon.ActualHeight < 12 || ink < 6 || hostedInk < 6))
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
    private static void VerifyFileIcons(Application application, string theme, string output)
    {
        var atlas = new StackPanel { Background = (System.Windows.Media.Brush)application.Resources["WindowBrush"] };
        var foreground = (System.Windows.Media.Brush)application.Resources["TextBrush"];
        var observations = new List<object>();
        foreach (var size in new[] { 16, 20 })
        {
            var signatures = new Dictionary<string, byte[]>();
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 8, 12, 8) }; atlas.Children.Add(row);
            foreach (var kind in new[] { "Markdown", "Mermaid", "PlainText", "Folder", "Document" })
            {
                var icon = new CommandIcon { Kind = kind, Width = size, Height = size, Foreground = foreground, Margin = new Thickness(4, 0, 8, 0) };
                var cell = new StackPanel { Orientation = Orientation.Horizontal, Width = 122 }; cell.Children.Add(icon);
                cell.Children.Add(new TextBlock { Text = kind, Foreground = foreground, VerticalAlignment = VerticalAlignment.Center }); row.Children.Add(cell);
            }
            atlas.Measure(new(650, 120)); atlas.Arrange(new Rect(0, 0, 650, 120)); atlas.UpdateLayout();
            foreach (var icon in row.Children.OfType<StackPanel>().Select(cell => (CommandIcon)cell.Children[0]))
            {
                var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(size, size, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                var visual = new System.Windows.Media.DrawingVisual();
                using (var drawing = visual.RenderOpen()) drawing.DrawRectangle(new System.Windows.Media.VisualBrush(icon), null, new Rect(0, 0, size, size));
                bitmap.Render(visual); var pixels = new byte[size * size * 4]; bitmap.CopyPixels(pixels, size * 4, 0);
                var ink = Enumerable.Range(0, size * size).Count(index => pixels[index * 4 + 3] > 0);
                if (ink < 20) throw new IOException("A workspace file icon has no legible native geometry at " + size + " pixels: " + icon.Kind);
                signatures.Add(icon.Kind, pixels); observations.Add(new { icon.Kind, Size = size, PaintedPixels = ink });
            }
            foreach (var first in signatures)
            foreach (var second in signatures.Where(pair => string.CompareOrdinal(pair.Key, first.Key) > 0))
                if (first.Value.SequenceEqual(second.Value)) throw new IOException("Monochrome workspace file identities are indistinguishable: " + first.Key + "/" + second.Key);
        }
        atlas.Measure(new(650, 120)); atlas.Arrange(new Rect(0, 0, 650, 120)); atlas.UpdateLayout();
        var capture = new System.Windows.Media.Imaging.RenderTargetBitmap(650, 120, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); capture.Render(atlas);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(capture));
        Directory.CreateDirectory(output);
        using (var file = File.Create(Path.Combine(output, "native-file-icons-" + theme.ToLowerInvariant() + ".png"))) encoder.Save(file);
        File.WriteAllText(Path.Combine(output, "native-file-icons-" + theme.ToLowerInvariant() + ".json"), JsonSerializer.Serialize(new { Passed = true, Monochrome = true, Icons = observations }));
    }
}
