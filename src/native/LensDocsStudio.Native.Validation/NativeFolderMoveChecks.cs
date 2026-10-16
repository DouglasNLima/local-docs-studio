using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native.Validation;

internal static class NativeFolderMoveChecks
{
    public static void Run(string output)
    {
        var root = Path.Combine(output, "folder-move-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path.Combine(root, "docs", "assets"));
        Directory.CreateDirectory(Path.Combine(root, "reference"));
        File.WriteAllText(Path.Combine(root, "docs", "guide.md"), "# Guide\n\n[Home](../README.md)");
        File.WriteAllText(Path.Combine(root, "README.md"), "# Home\n\n[[docs/guide|Guide]]");
        File.WriteAllBytes(Path.Combine(root, "docs", "assets", "sample.dat"), [1, 2, 3]);
        var workspace = new WorkspacePath(root);
        var plan = new WorkspaceFolderMoveService(new(Path.Combine(root, "state", "writes")), Path.Combine(root, "state", "moves"), Path.Combine(root, "state"))
            .PlanAsync(workspace, Path.Combine(root, "docs"), "reference/Guide folder").GetAwaiter().GetResult();
        foreach (var dark in new[] { false, true })
        foreach (var size in new[] { new Size(760, 620), new Size(540, 420) })
        {
            var window = new MoveFolderWindow(workspace, plan); var content = (Grid)window.Content; window.Content = null;
            ThemeService.Apply(content.Resources, dark ? "Dark" : "Light");
            var surface = new Border { Background = (Brush)content.Resources["WindowBrush"], Child = content };
            System.Windows.Documents.TextElement.SetForeground(surface, (Brush)content.Resources["TextBrush"]);
            using var host = new HwndSource(new HwndSourceParameters("Native folder move review") { Width = (int)size.Width, Height = (int)size.Height, WindowStyle = unchecked((int)0x80000000) });
            host.RootVisual = surface; surface.Measure(size); surface.Arrange(new Rect(size)); surface.UpdateLayout();
            var summary = (TextBlock)window.FindName("MoveSummary"); var affected = (ListBox)window.FindName("AffectedDocuments");
            var update = (CheckBox)window.FindName("UpdateLinks"); var warnings = (TextBlock)window.FindName("Warnings");
            if (!summary.Text.Contains("Files: 2", StringComparison.Ordinal) || !summary.Text.Contains("reference", StringComparison.Ordinal)
                || affected.Items.Count != 2 || affected.ActualHeight < 40 || !warnings.Text.Contains("never merged or replaced", StringComparison.Ordinal)
                || AutomationProperties.GetName(affected) != "Documents in the folder move review") throw new IOException("The folder move review lost file counts, document paths, safety feedback or usable layout.");
            update.IsChecked = false; if (window.ShouldUpdateLinks) throw new IOException("Folder review ignored the explicit link-update choice."); update.IsChecked = true;
            var footer = content.Children.OfType<StackPanel>().Single();
            foreach (var button in footer.Children.OfType<Button>())
            {
                var edge = button.TranslatePoint(new Point(button.ActualWidth, button.ActualHeight), surface);
                if (button.ActualWidth < 20 || edge.X > size.Width || edge.Y > size.Height) throw new IOException("A native folder review action is clipped.");
            }
            var capture = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32); capture.Render(surface);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(capture));
            using (var file = File.Create(Path.Combine(output, "native-folder-move-" + (dark ? "dark" : "light") + "-" + size.Width + ".png"))) encoder.Save(file);
            host.RootVisual = null; surface.Child = null; window.Close();
        }
        if (Directory.Exists(Path.Combine(root, "state")) || File.ReadAllText(Path.Combine(root, "README.md")) != "# Home\n\n[[docs/guide|Guide]]")
            throw new IOException("Reviewing a folder move changed source or recovery state.");
        File.WriteAllText(Path.Combine(output, "native-folder-move.json"), JsonSerializer.Serialize(new { Passed = true, Checks = new[] {
            "Native folder review exposes original/destination paths, file counts and affected documents",
            "Explicit link-update choice remains private before applying the move",
            "Normal and minimum client layouts fit actions and accessible lists in both themes",
            "Review alone preserves source files and creates no recovery writes" } }));
    }
}
