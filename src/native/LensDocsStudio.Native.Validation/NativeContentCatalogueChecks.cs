using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LensDocsStudio.Core.Content;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static class NativeContentCatalogueChecks
{
    public static void Run(string output)
    {
        foreach (var dark in new[] { false, true })
        foreach (var size in new[] { new Size(980, 730), new Size(720, 560) })
        {
            var window = new ContentCatalogueWindow(); var content = (Grid)window.Content; window.Content = null;
            ThemeService.Apply(content.Resources, dark ? "Dark" : "Light");
            var surface = new Border { Background = (Brush)content.Resources["WindowBrush"], Child = content };
            System.Windows.Documents.TextElement.SetForeground(surface, (Brush)content.Resources["TextBrush"]);
            using var host = new HwndSource(new HwndSourceParameters("Native content catalogue validation")
            { Width = (int)size.Width, Height = (int)size.Height, WindowStyle = unchecked((int)0x80000000) });
            host.RootVisual = surface;
            void Layout() { surface.Measure(size); surface.Arrange(new Rect(size)); surface.UpdateLayout(); }
            Layout();
            var filter = (TextBox)window.FindName("Filter"); var entries = (ListBox)window.FindName("Entries");
            var preview = (TextBox)window.FindName("Preview"); var use = (Button)window.FindName("UseButton");
            filter.Text = "DevOps User Story"; Layout();
            if (entries.Items.Count != 1 || !use.IsEnabled) throw new IOException("The catalogue did not filter to its generator.");
            var fields = (StackPanel)window.FindName("Fields");
            var title = fields.Children.OfType<TextBox>().Single(field => (string)field.Tag == "title"); title.Text = "Native Café requirements";
            var developer = fields.Children.OfType<TextBox>().Single(field => (string)field.Tag == "developer"); developer.Text = "Zoë";
            Layout();
            if (!preview.Text.Contains("Conclusion: Native Café requirements", StringComparison.Ordinal) || !preview.Text.Contains("Developer: Zoë", StringComparison.Ordinal)
                || preview.ActualHeight < 90 || entries.ActualHeight < 100 || use.TranslatePoint(new Point(0, use.ActualHeight), surface).Y > size.Height)
                throw new IOException("The generator lost source fields or usable controls at the validated size.");
            var peer = new System.Windows.Automation.Peers.ListBoxAutomationPeer(entries);
            if (!peer.GetChildren().Any(child => child.GetName() == "DevOps User Story / Task Conclusion")) throw new IOException("The catalogue lost accessible entry names.");
            foreach (var field in fields.Children.OfType<TextBox>())
                if (string.IsNullOrWhiteSpace(AutomationProperties.GetName(field))) throw new IOException("A generator field has no accessible name.");
            foreach (var label in fields.Children.OfType<Label>().Append((Label)window.FindName("FindLabel")))
                if (label.Foreground is not SolidColorBrush foreground || surface.Background is not SolidColorBrush background
                    || NativeSyntaxChecks.Contrast(foreground.Color, background.Color) < 4.5) throw new IOException("The catalogue lost readable field labels.");
            var capture = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32); capture.Render(surface);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(capture));
            using (var file = File.Create(Path.Combine(output, "native-catalogue-" + (dark ? "dark" : "light") + "-" + size.Width + ".png"))) encoder.Save(file);
            filter.Text = "No such template";
            if (entries.Items.Count != 0 || use.IsEnabled || preview.Text.Length != 0) throw new IOException("An unavailable template remained selectable.");
            filter.Text = "DevOps User Story";
            if (!preview.Text.Contains("Native Café requirements", StringComparison.Ordinal)) throw new IOException("Filtering discarded generator field edits.");
            filter.Clear(); ((ComboBox)window.FindName("Kind")).SelectedIndex = 1;
            ((ComboBox)window.FindName("Category")).SelectedItem = "Mermaid";
            if (entries.Items.Count != 15 || (string)use.Content != "Insert snippet" || !((TextBlock)window.FindName("Status")).Text.Contains("matching type", StringComparison.Ordinal))
                throw new IOException("Mermaid snippets lost category filtering or insertion guidance.");
            foreach (var family in new[] { "xychart", "radar", "sankey" })
                if (!entries.Items.Cast<CatalogueEntry>().Any(entry => entry.Id == "mermaid-snippet/" + family)) throw new IOException("The chart snippet is missing from its actual native category: " + family);
            foreach (var family in new[] { "architecture", "c4", "block" })
                if (!entries.Items.Cast<CatalogueEntry>().Any(entry => entry.Id == "mermaid-snippet/" + family + "-native")) throw new IOException("The topology snippet is missing from its actual native category: " + family);
            host.RootVisual = null; surface.Child = null; window.Close();
        }

        var parser = new MarkdownService(); var diagrams = new DiagramService(); var native = new NativeSvgService();
        var results = new List<object>();
        var diagramDirectory = Path.Combine(output, "catalogue-diagrams"); Directory.CreateDirectory(diagramDirectory);
        foreach (var entry in BuiltInContentCatalogue.Entries)
        {
            var generated = BuiltInContentCatalogue.Generate(entry, date: new DateOnly(2026, 10, 12));
            var source = generated.Content;
            if (entry.Format == "mermaid" && entry.IsSnippet)
                source = (entry.Id switch {
                    "mermaid-snippet/sequenceNote" => "sequenceDiagram\nparticipant User\nparticipant App\nUser->>App: Review\n",
                    "mermaid-snippet/xychart" => "xychart-beta\nx-axis [A,B,C]\n",
                    "mermaid-snippet/radar" => "radar-beta\naxis A,B,C\n",
                    "mermaid-snippet/sankey" => "sankey-beta\n",
                    "mermaid-snippet/kanban-native" => "kanban\n    todo[To do]\n",
                    "mermaid-snippet/requirement-native" => "requirementDiagram\n",
                    "mermaid-snippet/gitgraph-native" => "gitGraph LR:\n",
                    "mermaid-snippet/architecture-native" => "architecture-beta\n",
                    "mermaid-snippet/c4-native" => "C4Context\n",
                    "mermaid-snippet/block-native" => "block-beta\n",
                    _ => "flowchart LR\nA[Source] --> B[Preview]\n" }) + source;
            var blocks = parser.Parse(source, entry.Format == "mermaid").Syntax.Descendants<FencedCodeBlock>()
                .Where(block => string.Equals(block.Info, "mermaid", StringComparison.OrdinalIgnoreCase)).ToArray();
            for (var index = 0; index < blocks.Length; index++)
            foreach (var dark in new[] { false, true })
            {
                var result = diagrams.Render(blocks[index].Lines.ToString(), dark);
                if (!result.Success) throw new IOException("Built-in diagram " + entry.Id + " failed: " + result.Error);
                var drawing = native.Render(result.Svg!); var png = NativeSvgService.ToPng(drawing, 1);
                if (png.Length < 100 || drawing.Width < 10 || drawing.Height < 10) throw new IOException("A built-in diagram produced no meaningful native drawing.");
                var name = entry.Id.Replace('/', '-') + "-" + index + "-" + (dark ? "dark" : "light");
                File.WriteAllBytes(Path.Combine(diagramDirectory, name + ".png"), png);
                File.WriteAllText(Path.Combine(diagramDirectory, name + ".svg"), native.Prepare(result.Svg!));
                results.Add(new { Entry = entry.Id, Diagram = index, Dark = dark, drawing.Width, drawing.Height, PngBytes = png.Length });
            }
        }
        if (results.Count < 40) throw new IOException("The built-in catalogue diagram corpus is unexpectedly incomplete.");
        File.WriteAllText(Path.Combine(output, "native-catalogue.json"), JsonSerializer.Serialize(new
        {
            Passed = true, Checks = new[] { "Normal/minimum light/dark layouts retain editable accessible fields and complete source previews",
                "Filtering retains generator edits, unavailable entries are disabled and Mermaid snippets explain matching source context",
                "Every complete built-in Mermaid example and contextual snippet renders as native light/dark SVG and PNG" },
            DiagramCases = results.Count, Results = results, Limitation = "Interactive creation and insertion require desktop CI."
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
