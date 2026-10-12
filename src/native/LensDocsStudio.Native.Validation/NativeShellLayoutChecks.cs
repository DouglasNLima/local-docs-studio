using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;
using LensDocsStudio.Native.ViewModels;
using ICSharpCode.AvalonEdit;

namespace LensDocsStudio.Native.Validation;

internal static class NativeShellLayoutChecks
{
    public static void Run(string output)
    {
        // MainWindow resolves its BasedOn styles during construction, as it does in App.OnStartup.
        var application = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var resources = application.Resources;
        var shutdownMode = application.ShutdownMode;
        application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try { RunCases(output, application); }
        finally { application.Resources = resources; application.ShutdownMode = shutdownMode; }
    }
    private static void RunCases(string output, Application application)
    {
        var checks = new List<object>();
        foreach (var dark in new[] { false, true })
        foreach (var size in new[] { new Size(860, 540), new Size(1280, 780) })
        {
            application.Resources = new ResourceDictionary();
            ThemeService.Apply(application.Resources, dark ? "Dark" : "Light");
            var state = Path.Combine(output, "shell-layout-" + Guid.NewGuid().ToString("N"));
            var dialogs = new DesktopDialogs();
            var model = new WorkspaceViewModel(new DocumentStore(Path.Combine(state, "writes")), new SessionStore(state), new WorkspaceService(), dialogs);
            model.NewDocumentCommand.Execute(null);
            var window = new MainWindow(model, dialogs, []);
            try
            {
                var content = (DockPanel)window.Content; window.Content = null; content.DataContext = model;
                var surface = new Border { Child = content };
                surface.Resources.MergedDictionaries.Add(window.Resources);
                surface.Background = (Brush)application.Resources["WindowBrush"];
                System.Windows.Documents.TextElement.SetForeground(surface, (Brush)application.Resources["TextBrush"]);
                var editor = (TextEditor)window.FindName("Editor");
                editor.TextArea.TextView.LineTransformers.OfType<MarkdownHighlighting>().Single().Dark = dark;
                const string source = "# Accessible workspace\n\nCurrent source and a selectable preview.\n\n## Working safely\n\n- Keep edits recoverable.\n- Keep actions reachable.";
                model.ActiveDocument!.EditorDocument.Text = source; editor.Document = model.ActiveDocument.EditorDocument;
                var parsed = new MarkdownService().Parse(source);
                var rendered = new NativeMarkdownRenderer(new NativeSvgService()).Render(parsed, dark,
                    new Dictionary<Markdig.Syntax.FencedCodeBlock, DiagramResult>(), _ => { }, (_, _) => { });
                ((DocumentPreview)window.FindName("Preview")).Document = rendered.Document;
                ((ListBox)window.FindName("Outline")).ItemsSource = parsed.Headings;
                ((FrameworkElement)window.FindName("FindBar")).Visibility = Visibility.Visible;
                ((TextBox)window.FindName("ReplaceText")).Visibility = Visibility.Visible;
                ((Button)window.FindName("ReplaceButton")).Visibility = Visibility.Visible;
                ((TextBox)window.FindName("FindText")).Text = "recoverable";
                ((TextBox)window.FindName("ReplaceText")).Text = "portable";
                model.Status = string.Concat(Enumerable.Repeat("Exporting current documents, diagrams and portable workspace images. ", 4));
                model.PreviewStatus = "Preparing the latest large document preview"; model.IsBusy = true;
                using var host = new HwndSource(new HwndSourceParameters("Native shell layout validation")
                    { Width = (int)size.Width, Height = (int)size.Height, WindowStyle = unchecked((int)0x80000000) });
                host.RootVisual = surface; surface.Measure(size); surface.Arrange(new Rect(size)); surface.UpdateLayout();
                var prefix = "native-shell-" + (dark ? "dark" : "light") + "-" + size.Width;
                foreach (var scale in new[] { 1d, 1.5, 2d })
                {
                    var bitmap = new RenderTargetBitmap((int)(size.Width * scale), (int)(size.Height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                    bitmap.Render(surface); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var file = File.Create(Path.Combine(output, prefix + "-" + (int)(scale * 100) + ".png")); encoder.Save(file);
                }
                var required = Descendants(surface).OfType<Button>().Where(button => button.Content is "Cancel operation" or "Next" or "Replace" or "Close").ToArray();
                if (required.Length != 4) throw new IOException("The native shell is missing a required operation/search control.");
                var bounds = required.Select(button => new { Label = (string)button.Content, Bounds = new Rect(button.TranslatePoint(new(), surface), button.RenderSize),
                    AccessibleName = new ButtonAutomationPeer(button).GetName(), button.IsEnabled }).ToArray();
                File.WriteAllText(Path.Combine(output, prefix + ".json"), JsonSerializer.Serialize(bounds, new JsonSerializerOptions { WriteIndented = true }));
                foreach (var control in bounds)
                    if (control.Bounds.Width < 25 || control.Bounds.Height < 20 || control.Bounds.Left < 0 || control.Bounds.Top < 0
                        || control.Bounds.Right > size.Width || control.Bounds.Bottom > size.Height || control.AccessibleName != control.Label || !control.IsEnabled)
                        throw new IOException("Native " + control.Label + " is clipped, disabled or inaccessible at " + size + ": " + control.Bounds);
                if (editor.Text != source) throw new IOException("Native layout changed the source.");
                checks.Add(new { Theme = dark ? "Dark" : "Light", Width = size.Width, Height = size.Height, Controls = bounds });
                host.RootVisual = null; surface.Child = null;
            }
            finally { typeof(MainWindow).GetMethod("CompleteClose", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null); }
        }
        File.WriteAllText(Path.Combine(output, "native-shell-layout.json"), JsonSerializer.Serialize(new { Passed = true, Cases = checks,
            Limitation = "Hidden native windows with 100/150/200% raster output; this does not certify physical monitor DPI changes or screen-reader speech." }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
