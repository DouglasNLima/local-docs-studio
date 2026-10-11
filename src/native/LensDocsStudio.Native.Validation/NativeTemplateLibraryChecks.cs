using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native.Validation;

internal static class NativeTemplateLibraryChecks
{
    public static void Run(string output)
    {
        var root = Path.Combine(output, "template-library-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var source = Path.Combine(root, "generic.docx");
        var pixels = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aX1sAAAAASUVORK5CYII=");
        var original = NativeWordTemplateChecks.Create(pixels); File.WriteAllBytes(source, original);
        var library = new WordTemplateLibrary(Path.Combine(root, "library"));
        var first = library.ImportAsync(source, "Café handbook").GetAwaiter().GetResult();
        library.ImportAsync(source, "Release documentation").GetAwaiter().GetResult();
        var listing = library.ListAsync().GetAwaiter().GetResult();
        foreach (var dark in new[] { false, true })
        {
            var window = new WordTemplatesWindow(library, listing, Path.Combine(root, "export-recovery"), first.Record.Id, first.Name);
            var content = (Grid)window.Content; window.Content = null; ThemeService.Apply(content.Resources, dark ? "Dark" : "Light");
            var surface = new Border { Background = (Brush)content.Resources["WindowBrush"], Child = content };
            System.Windows.Documents.TextElement.SetForeground(surface, (Brush)content.Resources["TextBrush"]);
            using var host = new HwndSource(new HwndSourceParameters("Native template library validation") { Width = 760, Height = 580, WindowStyle = unchecked((int)0x80000000) });
            host.RootVisual = surface; surface.Measure(new Size(760, 580)); surface.Arrange(new Rect(0, 0, 760, 580)); surface.UpdateLayout();
            var list = (ListBox)window.FindName("TemplateList"); var use = (Button)window.FindName("UseButton");
            if (list.SelectedItem is not WordTemplateEntry selected || selected.Record.Id != first.Record.Id || !use.IsEnabled
                || !((Button)window.FindName("ArchiveButton")).IsEnabled || ((Button)window.FindName("CancelButton")).IsEnabled)
                throw new IOException("The native template library lost its selected entry or action availability.");
            var peer = new System.Windows.Automation.Peers.ListBoxAutomationPeer(list);
            if (!peer.GetChildren().Any(child => child.GetName() == "Café handbook")) throw new IOException("A template name is missing from the native accessibility tree.");
            var current = (TextBlock)window.FindName("CurrentStyles");
            if (!current.Text.Contains(first.Name, StringComparison.Ordinal) || current.Foreground is not SolidColorBrush foreground || surface.Background is not SolidColorBrush background
                || NativeSyntaxChecks.Contrast(foreground.Color, background.Color) < 4.5) throw new IOException("The selected template label lost readable theme colours.");
            var capture = new RenderTargetBitmap(760, 580, 96, 96, PixelFormats.Pbgra32); capture.Render(surface);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(capture));
            using (var file = File.Create(Path.Combine(output, "native-template-library-" + (dark ? "dark" : "light") + ".png"))) encoder.Save(file);
            host.RootVisual = null; surface.Child = null; window.Close();
        }
        var empty = new WordTemplatesWindow(library, new([], []), root, null, "Default Word styles");
        if (((Button)empty.FindName("UseButton")).IsEnabled || ((Button)empty.FindName("RenameButton")).IsEnabled
            || !((Button)empty.FindName("ImportButton")).IsEnabled || !((TextBlock)empty.FindName("LibraryStatus")).Text.Contains("Import a DOCX", StringComparison.Ordinal))
            throw new IOException("The empty native template library did not explain its available action.");
        empty.Close();
        var namePrompt = new TextPromptWindow("Name Word template", "Name", first.Name, "Import", "Template name", "Use 1–120 characters. The name is stored locally.");
        if (AutomationProperties.GetName((TextBox)namePrompt.FindName("Input")) != "Template name"
            || ((TextBlock)namePrompt.FindName("Hint")).Text.Contains("workspace", StringComparison.Ordinal)) throw new IOException("The template naming dialogue incorrectly describes a workspace path.");
        namePrompt.Close();
        if (!File.ReadAllBytes(source).SequenceEqual(original)) throw new IOException("The template library changed its imported original.");
        File.WriteAllText(Path.Combine(output, "native-template-library.json"), JsonSerializer.Serialize(new
        {
            Passed = true, Checks = new[] { "Native light/dark template library retains selected entries, accessible names and readable session styles",
                "Empty library disables unavailable actions and offers import", "Template naming uses the correct accessible label and guidance; original files remain unchanged" },
            Limitation = "Control layout and bindings; complete import/rename/copy/archive/restore/select interactions require desktop CI."
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
