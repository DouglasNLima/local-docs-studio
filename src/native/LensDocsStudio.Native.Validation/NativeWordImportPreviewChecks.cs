using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static class NativeWordImportPreviewChecks
{
    public static void Run(string output, NativeWordImportFixture fixture)
    {
        var application = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var resources = application.Resources; var context = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        try
        {
            application.Resources = new ResourceDictionary(); ThemeService.Apply(application.Resources, "Light");
            var styles = new ResourceDictionary { Source = new Uri("pack://application:,,,/LensDocsStudio.Native;component/Rendering/DocumentPreview.xaml") };
            var imported = new DocumentImportService().ImportDocx(fixture.Bytes);
            var parsed = new MarkdownService().Parse(imported.Markdown);
            var reference = MarkdownLinks.Read(imported.Markdown).Single(link => link.IsImage).Reference;
            using var stream = new MemoryStream(fixture.Image);
            var pixels = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad); pixels.Freeze();
            var result = new NativeMarkdownRenderer(new NativeSvgService()).Render(parsed, false,
                new Dictionary<FencedCodeBlock, DiagramResult>(), _ => { }, (_, _) => { },
                preparedImages: new Dictionary<string, PreviewImage> { [reference] = new(pixels, null) });
            var decorations = result.Document.Blocks.OfType<Paragraph>().SelectMany(paragraph => paragraph.Inlines).ToArray();
            if (!decorations.Any(span => new TextRange(span.ContentStart, span.ContentEnd).Text == "Removed"
                    && span.TextDecorations.Any(decoration => decoration.Location == TextDecorationLocation.Strikethrough))
                || !decorations.Any(span => new TextRange(span.ContentStart, span.ContentEnd).Text == "underlined"
                    && span.TextDecorations.Any(decoration => decoration.Location == TextDecorationLocation.Underline)))
                throw new IOException("Imported Word decorations are missing from the native preview.");
            var viewer = new DocumentPreview { Style = (Style)styles["DocumentPreviewStyle"], Zoom = 125 };
            using var host = new HwndSource(new HwndSourceParameters("Imported Word preview validation")
                { Width = 380, Height = 408, WindowStyle = unchecked((int)0x80000000) });
            host.RootVisual = viewer; viewer.SetPreview(result, parsed.Source.Length, "Pages"); Layout(); Pump(); Layout();
            Capture("native-word-import-text.png");
            var peer = FrameworkElementAutomationPeer.CreatePeerForElement(viewer);
            var treeImages = CountTreeImages(peer, new HashSet<AutomationPeer>());
            var provider = peer.GetPattern(PatternInterface.Text) as ITextProvider
                ?? throw new IOException("The imported Word preview has no native text provider.");
            if (!provider.DocumentRange.GetText(-1).Contains("Imported Café", StringComparison.Ordinal))
                throw new IOException("The imported heading is missing from accessible preview text.");
            // Requesting range children initialises the document peer's embedded-object range.
            // Direct hidden-HWND calls can have null interop providers; inspect the resulting WPF peers here.
            var rawChildren = provider.DocumentRange.GetChildren();
            var documentPeer = ContentElementAutomationPeer.CreatePeerForElement(result.Document);
            var embedded = (documentPeer.GetChildren() ?? []).Select(child => new
            {
                Name = child.GetName(), Type = child.GetAutomationControlType().ToString()
            }).ToArray();
            var accessibleImage = embedded.SingleOrDefault(child => child.Type == nameof(AutomationControlType.Image));
            if (accessibleImage?.Name != "Portable pixel")
                throw new IOException("Text-range children do not expose the imported image description: " + JsonSerializer.Serialize(embedded));
            var image = result.Media.Single().Image;
            var navigation = PreviewNavigationService.BringIntoViewAsync(viewer, ((InlineUIContainer)image.Parent).ElementStart);
            var timer = Stopwatch.StartNew();
            while (!navigation.IsCompleted)
            {
                if (timer.Elapsed > TimeSpan.FromSeconds(25)) throw new TimeoutException("Imported Word image pagination exceeded its deadline.");
                Pump();
            }
            if (!navigation.GetAwaiter().GetResult()) throw new IOException("The imported Word image could not be revealed on its page.");
            Layout(); Pump(); Layout();
            var bounds = image.TransformToAncestor(viewer).TransformBounds(new Rect(image.RenderSize));
            var footer = (FrameworkElement)viewer.Template.FindName("PageControls", viewer);
            var bottom = footer.TranslatePoint(new Point(), viewer).Y;
            if (bounds.Width <= 0 || bounds.Height <= 0 || bounds.Left < -1 || bounds.Top < -1
                || bounds.Right > viewer.ActualWidth + 1 || bounds.Bottom > bottom + 1 || parsed.Source != imported.Markdown)
                throw new IOException("The imported Word image is clipped or its source changed.");
            Capture("native-word-import-preview.png");
            File.WriteAllText(Path.Combine(output, "native-word-import-preview.json"), JsonSerializer.Serialize(new
            {
                Passed = true, RegularTreeImagesBeforeTextChildren = treeImages, Embedded = embedded, ImagePage = viewer.PageNumber,
                RawProviderSlots = rawChildren.Length, NullRawProviders = rawChildren.Count(child => child is null),
                NativeImageWithinPageBounds = true, ImageBounds = bounds.ToString(), SourcePreserved = true,
                ImportedStrikethroughAndUnderline = true,
                Limitation = "Hidden HWND and native text-provider contract; actual visible desktop delivery is verified separately."
            }, new JsonSerializerOptions { WriteIndented = true }));
            host.RootVisual = null;

            void Layout() { viewer.Measure(new Size(380, 408)); viewer.Arrange(new Rect(0, 0, 380, 408)); viewer.UpdateLayout(); }
            void Capture(string name)
            {
                var bitmap = new RenderTargetBitmap(380, 408, 96, 96, PixelFormats.Pbgra32); bitmap.Render(viewer);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var destination = File.Create(Path.Combine(output, name)); encoder.Save(destination);
            }
        }
        finally { application.Resources = resources; SynchronizationContext.SetSynchronizationContext(context); }
    }

    private static int CountTreeImages(AutomationPeer peer, HashSet<AutomationPeer> seen)
    {
        if (!seen.Add(peer)) return 0;
        if (seen.Count > 1024) throw new IOException("The Word preview automation tree exceeded the fixture limit.");
        return (peer.GetAutomationControlType() == AutomationControlType.Image ? 1 : 0)
            + (peer.GetChildren()?.Sum(child => CountTreeImages(child, seen)) ?? 0);
    }

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }
}
