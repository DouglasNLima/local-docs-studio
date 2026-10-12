using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.Validation;

internal static class NativeWorkspaceMapChecks
{
    public static void Run(string output)
    {
        var root = Path.Combine(output, "workspace-map-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path.Combine(root, "docs"));
        var home = Path.Combine(root, "README.md"); var guide = Path.Combine(root, "docs", "Guide.md");
        File.WriteAllText(home, "# Saved home"); File.WriteAllText(guide, "# Guide\n\n[Home](../README.md)");
        const string current = "# Current home\n\n[[docs/Guide|Open guide]] [Missing](missing.md)";
        var map = new WorkspaceMapService(new DocumentStore(Path.Combine(root, "state")))
            .BuildAsync(new(root), new Dictionary<string, string> { [home] = current }).GetAwaiter().GetResult();
        foreach (var dark in new[] { false, true })
        foreach (var size in new[] { new Size(1060, 820), new Size(760, 560) })
        {
            var model = new WorkspaceMapViewModel(map); var window = new WorkspaceMapWindow(model);
            var content = (Grid)window.Content; window.Content = null; content.DataContext = model;
            ThemeService.Apply(content.Resources, dark ? "Dark" : "Light");
            var surface = new Border { Background = (Brush)content.Resources["WindowBrush"], Child = content };
            System.Windows.Documents.TextElement.SetForeground(surface, (Brush)content.Resources["TextBrush"]);
            using var host = new HwndSource(new HwndSourceParameters("Native workspace map validation") { Width = (int)size.Width, Height = (int)size.Height, WindowStyle = unchecked((int)0x80000000) });
            host.RootVisual = surface;
            void Layout() { surface.Measure(size); surface.Arrange(new Rect(size)); surface.UpdateLayout(); }
            Layout();
            var query = (TextBox)window.FindName("Query"); var filter = (ComboBox)window.FindName("Filter");
            var documents = (ListBox)window.FindName("Documents"); var incoming = (ListBox)window.FindName("Incoming"); var outgoing = (ListBox)window.FindName("Outgoing");
            query.Text = "Current home"; Layout();
            if (documents.Items.Count != 1 || model.SelectedDocument?.Path != "README.md" || incoming.Items.Count != 1 || outgoing.Items.Count != 2
                || documents.ActualHeight < 120 || incoming.ActualHeight < 45 || outgoing.ActualHeight < 45)
                throw new IOException("The native map lost current draft titles, relationship lists or usable controls.");
            outgoing.SelectedIndex = 1; Layout();
            if (!model.CanGoToSource || model.SelectedLink?.Reference != "missing.md") throw new IOException("Map link selection lost its exact source target.");
            foreach (var control in new FrameworkElement[] { query, filter, documents, incoming, outgoing })
                if (string.IsNullOrWhiteSpace(AutomationProperties.GetName(control))) throw new IOException("A map control has no accessible name.");
            if (!new ListBoxAutomationPeer(documents).GetChildren().Any(child => child.GetName().Contains("Current home", StringComparison.Ordinal)))
                throw new IOException("Map document titles are missing from native accessibility.");
            foreach (var button in Visuals(content).OfType<Button>().Where(button => AutomationProperties.GetAutomationId(button).StartsWith("Map", StringComparison.Ordinal)))
            {
                var edge = button.TranslatePoint(new Point(button.ActualWidth, button.ActualHeight), surface);
                if (button.ActualWidth < 20 || edge.X > size.Width || edge.Y > size.Height)
                    throw new IOException("A workspace map action is clipped: " + button.Content);
            }
            var capture = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32); capture.Render(surface);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(capture));
            using (var file = File.Create(Path.Combine(output, "native-map-" + (dark ? "dark" : "light") + "-" + size.Width + ".png"))) encoder.Save(file);
            query.Text = "missing-map-document-39128"; Layout();
            if (documents.Items.Count != 0 || incoming.Items.Count != 0 || outgoing.Items.Count != 0 || model.CanOpen || model.CanGoToSource)
                throw new IOException("Empty map search retained a stale navigation target.");
            model.ClearCommand.Execute(null); filter.SelectedItem = "Has review notes"; Layout();
            if (documents.Items.Count != 1 || model.SelectedDocument?.Path != "README.md") throw new IOException("Map review filtering lost unresolved source links.");
            host.RootVisual = null; surface.Child = null; window.Close();
        }
        var navigation = new WorkspaceMapViewModel(map) { Query = "Current home" }; (string Path, int Start, int Length)? requested = null;
        navigation.OpenRequested += (path, start, length) => requested = (path, start, length);
        navigation.SelectedLink = navigation.Outgoing.Single(link => link.Reference == "missing.md"); navigation.GoToSourceCommand.Execute(null);
        if (requested is not { Path: "README.md" } target || current.Substring(target.Start, target.Length) != "[Missing](missing.md)")
            throw new IOException("Map navigation changed the original Markdown source position.");
        foreach (var dark in new[] { false, true })
        {
            var diagram = WorkspaceMapService.Diagram(map); var rendered = new DiagramService().Render(diagram.Source, dark);
            if (!rendered.Success) throw new IOException(rendered.Error);
            var drawing = new NativeSvgService().Render(rendered.Svg!);
            var bytes = NativeSvgService.ToPng(drawing);
            if (bytes.Length < 200 || drawing.Width < 10 || !drawing.IsFrozen) throw new IOException("Map diagrams have no native exportable drawing.");
            File.WriteAllBytes(Path.Combine(output, "native-map-diagram-" + (dark ? "dark" : "light") + ".png"), bytes);
        }
        if (File.ReadAllText(home) != "# Saved home" || Directory.Exists(Path.Combine(root, "state"))) throw new IOException("Read-only map operations wrote source or recovery data.");
        File.WriteAllText(Path.Combine(output, "native-workspace-map.json"), JsonSerializer.Serialize(new { Passed = true, Checks = new[] {
            "Current draft titles, incoming/outgoing links and exact source selection use native controls",
            "Empty search clears stale navigation and unresolved-link filters retain source documents",
            "Both themes at normal and minimum client sizes fit accessible map actions",
            "Workspace diagrams render to native frozen drawings and PNG pixels without changing source" } }));
    }
    private static IEnumerable<DependencyObject> Visuals(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index); yield return child;
            foreach (var descendant in Visuals(child)) yield return descendant;
        }
    }
}
