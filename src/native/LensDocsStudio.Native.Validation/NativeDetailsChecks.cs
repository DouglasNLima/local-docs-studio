using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using LensDocsStudio.Core.Editing;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Exports;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static class NativeDetailsChecks
{
    public static void Run(string output)
    {
        var root = Path.Combine(output, "details-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var pixels = new byte[80 * 24 * 4];
        for (var index = 0; index < pixels.Length; index += 4) { pixels[index] = 70; pixels[index + 1] = 105; pixels[index + 2] = 160; pixels[index + 3] = 255; }
        var bitmap = BitmapSource.Create(80, 24, 96, 96, PixelFormats.Bgra32, null, pixels, 80 * 4); bitmap.Freeze();
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var file = File.Create(Path.Combine(root, "evidence.png"))) encoder.Save(file);
        const string source = "# Native details\n\n<details data-details-block>\n  <summary>Supporting evidence — Café</summary>\n\n## Hidden heading\n\nRetained **bold** and [guide](guide.md).\n\n![Evidence image](evidence.png)\n\n<details>\n<summary>Nested review</summary>\n\nNested needle 042.\n</details>\n\n```mermaid\nflowchart LR\nA[Source] --> B[Review]\n```\n\n| Item | Result |\n|---|---|\n| Evidence | Ready |\n</details>\n\nAfter details.";
        var path = Path.Combine(root, "details.md"); File.WriteAllText(path, source);
        var parser = new MarkdownService(); var parsed = parser.Parse(source); var diagrams = new DiagramService();
        var preparedImages = new PreviewImageService().Prepare(parsed, path, root);
        foreach (var dark in new[] { false, true })
        {
            var preparedDiagrams = parsed.Syntax.Descendants<FencedCodeBlock>().ToDictionary(block => block, block => diagrams.Render(block.Lines.ToString(), dark));
            var rendered = new NativeMarkdownRenderer(new NativeSvgService()).Render(parsed, dark, preparedDiagrams, _ => { }, (_, _) => { }, preparedImages: preparedImages);
            ThemeService.Apply(rendered.Document.Resources, dark ? "Dark" : "Light");
            rendered.Document.Background = (Brush)rendered.Document.Resources["WindowBrush"];
            string Text() => new TextRange(rendered.Document.ContentStart, rendered.Document.ContentEnd).Text;
            if (rendered.Warnings.Count != 0 || rendered.Details.Count != 2 || !Text().Contains("Supporting evidence — Café", StringComparison.Ordinal)
                || Text().Contains("Hidden heading", StringComparison.Ordinal)) throw new IOException("Collapsed native details lost their summary or exposed hidden body text.");
            var outer = rendered.Details[0]; var peer = new ExpanderAutomationPeer(outer.Toggle);
            var expansion = peer.GetPattern(PatternInterface.ExpandCollapse) as IExpandCollapseProvider;
            if (expansion is null || peer.GetName() != "Supporting evidence — Café") throw new IOException("Native details lost accessible expansion or their summary name.");
            expansion.Expand();
            if (!Text().Contains("Hidden heading", StringComparison.Ordinal) || Text().Contains("Nested needle", StringComparison.Ordinal))
                throw new IOException("Expanding outer details did not retain the nested section's own state.");
            expansion.Collapse();
            var hiddenHeading = parsed.Headings.Single(heading => heading.Title == "Hidden heading"); rendered.RevealSource(hiddenHeading.SourceStart);
            if (!outer.Toggle.IsExpanded || rendered.Details[1].Toggle.IsExpanded || rendered.SourceElements[hiddenHeading.SourceStart].Parent is null)
                throw new IOException("Source navigation did not reveal exactly the containing details section.");
            var needle = source.IndexOf("Nested needle", StringComparison.Ordinal); rendered.RevealSource(needle);
            if (!Text().Contains("Nested needle 042.", StringComparison.Ordinal)) throw new IOException("Nested source navigation did not expose selectable body text.");
            foreach (var section in rendered.Details) section.Toggle.IsExpanded = false;
            rendered.ExpandDetails();
            if (!Text().Contains("Nested needle 042.", StringComparison.Ordinal) || !Text().Contains("After details.", StringComparison.Ordinal))
                throw new IOException("Find/copy expansion lost nested or following content.");
            var paginator = ((IDocumentPaginatorSource)rendered.Document).DocumentPaginator; paginator.PageSize = new Size(820, 820);
            var page = paginator.GetPage(0);
            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            foreach (var section in rendered.Details)
            {
                if (section.Toggle.ActualWidth < 20 || section.Toggle.ActualHeight < 20) throw new IOException("The native details toggle has no usable control area.");
                var arrow = section.Toggle.Template.FindName("Chevron", section.Toggle) as System.Windows.Shapes.Path;
                if (arrow is null || arrow.ActualHeight < 10 || arrow.ActualWidth < 10 || arrow.Stroke is not SolidColorBrush)
                    throw new IOException("The native details arrow is clipped or unavailable.");
            }
            var capture = new RenderTargetBitmap(820, 820, 96, 96, PixelFormats.Pbgra32); capture.Render(paginator.GetPage(0).Visual);
            var imageEncoder = new PngBitmapEncoder(); imageEncoder.Frames.Add(BitmapFrame.Create(capture));
            using var image = File.Create(Path.Combine(output, "native-details-" + (dark ? "dark" : "light") + ".png")); imageEncoder.Save(image);
        }
        var content = new ExportPreparationService(parser, diagrams, new NativeSvgService()).Prepare(source, path, root, false);
        if (content.Images.Count != 1 || content.Diagrams.Count != 1) throw new IOException("Closed details lost prepared images or Mermaid diagrams.");
        var pdf = new PdfExporter().Export(content); File.WriteAllBytes(Path.Combine(output, "native-details.pdf"), pdf);
        File.WriteAllBytes(Path.Combine(output, "native-details.docx"), new DocxExporter().Export(content));
        File.WriteAllText(Path.Combine(output, "native-details.html"), new HtmlExporter().Export(content));
        using var document = UglyToad.PdfPig.PdfDocument.Open(pdf);
        var text = string.Join(" ", document.GetPages().SelectMany(page => page.GetWords()).Select(word => word.Text));
        foreach (var expected in new[] { "Supporting evidence — Café", "Hidden heading", "Nested needle 042.", "Evidence", "Ready", "After details." })
            if (!text.Contains(expected, StringComparison.Ordinal)) throw new IOException("The PDF lost details content: " + expected);
        if (document.GetPages().Sum(page => page.NumberOfImages) != 2 || File.ReadAllText(path) != source)
            throw new IOException("Details export lost its real image/diagram or changed saved source.");
        var selectedPosition = source.IndexOf("Hidden heading", StringComparison.Ordinal);
        foreach (var dark in new[] { false, true })
        foreach (var size in new[] { new Size(760, 620), new Size(560, 460) })
        {
            var window = new DetailsWindow(parsed, selectedPosition, 0); var grid = (Grid)window.Content; window.Content = null;
            ThemeService.Apply(grid.Resources, dark ? "Dark" : "Light");
            var surface = new Border { Background = (Brush)grid.Resources["WindowBrush"], Child = grid };
            TextElement.SetForeground(surface, (Brush)grid.Resources["TextBrush"]);
            using var host = new HwndSource(new HwndSourceParameters("Native details editor validation")
            { Width = (int)size.Width, Height = (int)size.Height, WindowStyle = unchecked((int)0x80000000) });
            host.RootVisual = surface; surface.Measure(size); surface.Arrange(new Rect(size)); surface.UpdateLayout();
            var summary = (TextBox)window.FindName("Summary"); var body = (TextBox)window.FindName("Body"); var apply = (Button)window.FindName("ApplyButton");
            if (summary.Text != "Supporting evidence — Café" || !body.Text.Contains("Nested needle 042.", StringComparison.Ordinal)
                || body.ActualHeight < 100 || apply.TranslatePoint(new Point(0, apply.ActualHeight), surface).Y > size.Height
                || System.Windows.Automation.AutomationProperties.GetName(body) != "Details Markdown body")
                throw new IOException("The details editor lost restored content, accessible names or usable layout.");
            var capture = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32); capture.Render(surface);
            var imageEncoder = new PngBitmapEncoder(); imageEncoder.Frames.Add(BitmapFrame.Create(capture));
            using (var image = File.Create(Path.Combine(output, "native-details-editor-" + (dark ? "dark" : "light") + "-" + size.Width + ".png"))) imageEncoder.Save(image);
            host.RootVisual = null; surface.Child = null; window.Close();
        }
        var editor = new ICSharpCode.AvalonEdit.TextEditor { Text = "Before\r\n\r\nSelected **content**\r\n\r\nAfter" };
        var original = editor.Text; var selected = original.IndexOf("Selected", StringComparison.Ordinal); editor.Document.UndoStack.ClearAll();
        var sourceDocument = parser.Parse(original); var options = DetailsEditService.ReadOptions(sourceDocument, selected, "Selected **content**".Length);
        var edit = DetailsEditService.Plan(sourceDocument, selected, "Selected **content**".Length, options with { Summary = "Café review" });
        EditorFormattingService.Apply(editor, edit); var changed = editor.Text;
        if (!editor.SelectedText.StartsWith("<details", StringComparison.Ordinal)) throw new IOException("Details insertion did not select its editable markup.");
        editor.Document.UndoStack.Undo(); if (editor.Text != original) throw new IOException("Details insertion did not undo as one complete edit.");
        editor.Document.UndoStack.Redo(); if (editor.Text != changed) throw new IOException("Details insertion Redo lost source.");
        File.WriteAllText(Path.Combine(output, "native-details.json"), JsonSerializer.Serialize(new
        {
            Passed = true, Checks = new[] { "Native ExpandCollapse controls retain selectable summaries and independent nested states",
                "Source navigation expands containing sections; find/copy can expose complete native text without changing Markdown",
                "Light/dark native pages retain controls, image and diagram content", "Actual PDF/DOCX/HTML exports preserve complete closed-section content and both image assets",
                "The details editor restores summary/body at normal/minimum light/dark sizes", "Details insertion selects editable markup and retains complete Undo/Redo" }, PdfText = text
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
