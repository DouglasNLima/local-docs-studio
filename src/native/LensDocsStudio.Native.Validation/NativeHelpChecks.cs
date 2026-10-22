using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
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

internal static class NativeHelpChecks
{
    public static void Run(string output)
    {
        foreach (var dark in new[] { false, true })
        foreach (var size in new[] { new Size(980, 760), new Size(680, 520) })
        {
            var window = new HelpWindow(dark ? "Dark" : "Light"); var content = (Grid)window.Content; window.Content = null;
            ThemeService.Apply(content.Resources, dark ? "Dark" : "Light");
            var surface = new Border { Background = (Brush)content.Resources["WindowBrush"], Child = content };
            TextElement.SetForeground(surface, (Brush)content.Resources["TextBrush"]);
            using var host = new HwndSource(new HwndSourceParameters("Native help validation") { Width = (int)size.Width, Height = (int)size.Height, WindowStyle = unchecked((int)0x80000000) });
            host.RootVisual = surface;
            void Layout() { surface.Measure(size); surface.Arrange(new Rect(size)); surface.UpdateLayout(); }
            Layout();
            var query = (TextBox)window.FindName("Query"); var topics = (ListBox)window.FindName("Topics");
            var preview = (FlowDocumentScrollViewer)window.FindName("ContentViewer");
            query.Text = "browser library"; Layout();
            if (topics.Items.Count != 1 || topics.SelectedItem is not HelpTopic { Title: "Templates, snippets and browser migration" }
                || !new TextRange(preview.Document.ContentStart, preview.Document.ContentEnd).Text.Contains("Import JSON", StringComparison.Ordinal)
                || preview.ActualWidth < 300 || preview.ActualHeight < 250 || preview.Document.TextAlignment != TextAlignment.Left)
                throw new IOException("Help lost searchable body content, usable native layout or readable paragraph alignment.");
            if (string.IsNullOrWhiteSpace(AutomationProperties.GetName(query)) || string.IsNullOrWhiteSpace(AutomationProperties.GetName(topics)))
                throw new IOException("Help controls have no accessible names.");
            var capture = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32); capture.Render(surface);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(capture));
            using (var file = File.Create(Path.Combine(output, "native-help-" + (dark ? "dark" : "light") + "-" + size.Width + ".png"))) encoder.Save(file);
            query.Text = "no-topic-9218264"; Layout();
            if (topics.Items.Count != 0 || !new TextRange(preview.Document.ContentStart, preview.Document.ContentEnd).Text.Contains("No matching topics", StringComparison.Ordinal))
                throw new IOException("Empty help search retained an unrelated topic.");
            if (preview.Document.Foreground is not SolidColorBrush foreground || surface.Background is not SolidColorBrush background
                || NativeSyntaxChecks.Contrast(foreground.Color, background.Color) < 4.5) throw new IOException("Empty help results lost readable contrast.");
            query.Clear(); if (topics.Items.Count != 20 || topics.SelectedItem is null) throw new IOException("Clearing help search did not restore all twenty bundled topics.");
            query.Text = "XY Chart, Radar and Sankey visual authoring"; Layout();
            if (topics.Items.Count != 1 || topics.SelectedItem is not HelpTopic { Title: "XY Chart, Radar and Sankey visual authoring" }
                || !new TextRange(preview.Document.ContentStart, preview.Document.ContentEnd).Text.Contains("Rename node across proved", StringComparison.Ordinal))
                throw new IOException("The new chart help topic lost native search or source ownership guidance.");
            query.Text = "Kanban, Requirement and GitGraph visual authoring"; Layout();
            if (topics.Items.Count != 1 || !new TextRange(preview.Document.ContentStart, preview.Document.ContentEnd).Text.Contains("non-empty heads", StringComparison.Ordinal)) throw new IOException("Workflow help lost reference and ownership guidance.");
            query.Text = "Architecture, C4 and Block visual authoring"; Layout();
            var topologyHelp = new TextRange(preview.Document.ContentStart, preview.Document.ContentEnd).Text;
            if (topics.Items.Count != 1 || !new[] { "C4Context", "C4Container", "C4Component", "Rel_Back", "First-wins", "stale-owner" }.All(value => topologyHelp.Contains(value, StringComparison.Ordinal))) throw new IOException("Topology help lost supported variants, exact source or ownership guidance.");
            window.ApplyTheme(dark ? "Light" : "Dark");
            if (!SystemParameters.HighContrast && (preview.Document.Foreground is not SolidColorBrush changed || changed.Color == foreground.Color)) throw new IOException("An open guide did not follow native theme changes.");
            host.RootVisual = null; surface.Child = null; window.Close();
        }

        const string source = "## A heading that wraps naturally\n\nA narrow reading pane keeps ordinary word spacing and makes every diagram action available.\n\n```mermaid\nflowchart LR\nA[Write] --> B[Read]\n```\n";
        var parsed = new MarkdownService().Parse(source); var code = parsed.Syntax.Descendants<FencedCodeBlock>().Single();
        var diagram = new DiagramService().Render(code.Lines.ToString(), false);
        var rendered = new NativeMarkdownRenderer(new NativeSvgService()).Render(parsed, false, new Dictionary<FencedCodeBlock, DiagramResult> { [code] = diagram }, _ => { }, (_, _) => { });
        rendered.Document.Background = Brushes.White;
        var viewer = new FlowDocumentScrollViewer { Document = rendered.Document, IsToolBarVisible = false, Background = Brushes.White };
        using (var host = new HwndSource(new HwndSourceParameters("Narrow preview validation") { Width = 280, Height = 720, WindowStyle = unchecked((int)0x80000000) }))
        {
            host.RootVisual = viewer; viewer.Measure(new Size(280, 720)); viewer.Arrange(new Rect(0, 0, 280, 720)); viewer.UpdateLayout();
            var container = rendered.Document.Blocks.OfType<BlockUIContainer>().Single(); var panel = (StackPanel)container.Child;
            var actions = panel.Children.OfType<WrapPanel>().Single(); var buttons = actions.Children.OfType<Button>().ToArray();
            if (buttons.Length != 4 || rendered.Document.Blocks.OfType<Paragraph>().Any(paragraph => paragraph.TextAlignment != TextAlignment.Left))
                throw new IOException("Narrow preview lost actions or uses stretched paragraph spacing.");
            foreach (var button in buttons)
            {
                var edge = button.TranslatePoint(new Point(button.ActualWidth, button.ActualHeight), viewer);
                if (button.ActualWidth < 20 || edge.X > viewer.ActualWidth || edge.Y > viewer.ActualHeight)
                    throw new IOException("A diagram action is clipped in a narrow native preview: " + button.Content);
            }
            if (buttons[3].TranslatePoint(new Point(), actions).Y <= buttons[0].TranslatePoint(new Point(), actions).Y)
                throw new IOException("Diagram actions did not wrap in the narrow preview fixture.");
            var capture = new RenderTargetBitmap(280, 720, 96, 96, PixelFormats.Pbgra32); capture.Render(viewer);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(capture));
            using var file = File.Create(Path.Combine(output, "native-preview-narrow.png")); encoder.Save(file);
        }
        File.WriteAllText(Path.Combine(output, "native-help.json"), JsonSerializer.Serialize(new { Passed = true, Checks = new[] {
            "Bundled help searches complete topic content with native selectable text",
            "Normal and minimum native help layouts fit both themes and expose accessible controls",
            "Empty results clear stale content and remain readable; theme changes update an open guide",
            "Narrow preview keeps natural paragraph spacing and wraps all four diagram actions" } }));
    }
}
