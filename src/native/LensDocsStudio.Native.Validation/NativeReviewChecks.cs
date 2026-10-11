using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LensDocsStudio.Core.Review;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native.Validation;

internal static class NativeReviewChecks
{
    public static void Run(string output)
    {
        const string source = "# Review Café\r\n\r\n### Skipped\r\n\r\nColor and TODO remain.\r\n\r\n![](missing.png)\r\n\r\n[Missing](#absent)";
        var result = new DocumentReviewService().Analyse(source);
        if (result.Issues.Count != 5) throw new IOException("The review fixture did not identify its five advisory notes.");
        foreach (var dark in new[] { false, true })
        {
            var window = new DocumentReviewWindow(result);
            var content = (Grid)window.Content; window.Content = null;
            ThemeService.Apply(content.Resources, dark ? "Dark" : "Light");
            var surface = new Border { Background = (Brush)content.Resources["WindowBrush"], Child = content };
            // Detached content must retain the foreground normally inherited from its owning window.
            System.Windows.Documents.TextElement.SetForeground(surface, (Brush)content.Resources["TextBrush"]);
            using var host = new HwndSource(new HwndSourceParameters("Native review validation") { Width = 800, Height = 600, WindowStyle = unchecked((int)0x80000000) });
            host.RootVisual = surface;
            surface.Measure(new Size(800, 600)); surface.Arrange(new Rect(0, 0, 800, 600)); surface.UpdateLayout();
            var list = (ListBox)window.FindName("Issues");
            var button = (Button)window.FindName("GoToButton");
            var marker = result.Issues.Single(issue => issue.Rule == "TODO/FIXME");
            list.SelectedItem = marker; surface.UpdateLayout();
            if (!button.IsEnabled || !ReferenceEquals(list.SelectedItem, marker) || source.Substring(marker.Start, marker.Length) != "TODO")
                throw new IOException("Native review selection did not retain the original source destination.");
            var container = (ListBoxItem?)list.ItemContainerGenerator.ContainerFromItem(marker);
            if (container is null || !AutomationProperties.GetName(container).Contains("TODO marker found", StringComparison.Ordinal))
                throw new IOException("The native review item lacks its accessible message and source position.");
            if (!VirtualizingPanel.GetIsVirtualizing(list) || !((TextBlock)window.FindName("Summary")).Text.Contains("2 headings", StringComparison.Ordinal))
                throw new IOException("Native review lost bounded list behaviour or document metrics.");
            var summary = (TextBlock)window.FindName("Summary");
            if (summary.Foreground is not SolidColorBrush foreground || surface.Background is not SolidColorBrush background
                || NativeSyntaxChecks.Contrast(foreground.Color, background.Color) < 4.5)
                throw new IOException("Native review text did not inherit readable window theme colours.");
            var bitmap = new RenderTargetBitmap(800, 600, 96, 96, PixelFormats.Pbgra32); bitmap.Render(surface);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(Path.Combine(output, "native-review-" + (dark ? "dark" : "light") + ".png"))) encoder.Save(file);
            host.RootVisual = null; surface.Child = null; window.Close();
        }
        var empty = new DocumentReviewWindow(new DocumentReviewService().Analyse("# Clean document"));
        if (((Button)empty.FindName("GoToButton")).IsEnabled || ((TextBlock)empty.FindName("ResultStatus")).Text != "No notes from these checks.")
            throw new IOException("A review without notes implied unsupported certification or enabled an invalid destination.");
        empty.Close();
        var limited = new DocumentReviewWindow(new(result.Metrics, result.Issues, true));
        if (!((TextBlock)limited.FindName("ResultStatus")).Text.Contains("limit was reached", StringComparison.Ordinal)) throw new IOException("Native review hid its incomplete result limit.");
        limited.Close();
        File.WriteAllText(Path.Combine(output, "native-review.json"), JsonSerializer.Serialize(new
        {
            Passed = true,
            Checks = new[] { "Native light/dark review controls retain metrics, virtualisation and accessible source locations",
                "Selecting a review note enables its exact original source destination",
                "Empty and limited results disclose their scope without implying complete document certification" },
            Limitation = "Control layout and bindings only; real dialogue navigation is validated separately in desktop CI."
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
