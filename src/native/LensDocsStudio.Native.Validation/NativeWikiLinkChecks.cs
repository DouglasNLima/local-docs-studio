using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using ICSharpCode.AvalonEdit;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static class NativeWikiLinkChecks
{
    public static void Run(string output)
    {
        const string source = "# Workspace links\n\nRead [[docs/Café guide.md#evidence|the Café guide]] and [[Summary|a summary]].\n\n`[[Literal code]]` stays as code.\n";
        var parsed = new MarkdownService().Parse(source);
        foreach (var dark in new[] { false, true })
        {
            string? request = null;
            var result = new NativeMarkdownRenderer(new NativeSvgService()).Render(parsed, dark, new Dictionary<FencedCodeBlock, DiagramResult>(),
                _ => throw new IOException("A wikilink used the external/ordinary link route."), (_, _) => { }, navigateWikiLink: target => request = target);
            var links = result.Document.Blocks.OfType<Paragraph>().SelectMany(paragraph => paragraph.Inlines.OfType<Hyperlink>()).ToArray();
            if (links.Length != 2 || AutomationProperties.GetName(links[0]) != "the Café guide") throw new IOException("Native wikilinks lost their aliases or accessible names.");
            links[0].RaiseEvent(new RoutedEventArgs(Hyperlink.ClickEvent));
            if (request != "docs/Café guide.md#evidence" || !result.ReadText().Contains("[[Literal code]]", StringComparison.Ordinal) || parsed.Source != source)
                throw new IOException("Native wiki dispatch lost exact source or changed literal code.");
            var background = new SolidColorBrush(dark ? Color.FromRgb(32, 33, 36) : Color.FromRgb(250, 250, 250));
            result.Document.Background = background;
            var viewer = new FlowDocumentScrollViewer { Document = result.Document, Background = background, IsToolBarVisible = false };
            using var host = new HwndSource(new HwndSourceParameters("Native wikilink validation") { Width = 720, Height = 320, WindowStyle = unchecked((int)0x80000000) });
            host.RootVisual = viewer; viewer.Measure(new Size(720, 320)); viewer.Arrange(new Rect(0, 0, 720, 320)); viewer.UpdateLayout();
            if (links[0].ContentStart.GetCharacterRect(LogicalDirection.Forward).IsEmpty) throw new IOException("The native wikilink has no visible text position.");
            var bitmap = new RenderTargetBitmap(720, 320, 96, 96, PixelFormats.Pbgra32); bitmap.Render(viewer);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(Path.Combine(output, "native-wikilinks-" + (dark ? "dark" : "light") + ".png"))) encoder.Save(file);
            host.RootVisual = null;
        }
        var catalogue = new WikiLinkIndex(["docs/Café guide.md", "one/Summary.md", "two/Summary.md"]);
        const string before = "Before\r\n[[Café";
        var editor = new TextEditor { Text = before }; editor.CaretOffset = before.Length; editor.Document.UndoStack.ClearAll();
        if (!EditorCompletionService.ApplyWiki(editor, catalogue, "docs/Café guide.md") || editor.Text != "Before\r\n[[docs/Café guide.md]]")
            throw new IOException("Native completion did not insert an unambiguous Unicode workspace path.");
        var completed = editor.Text; editor.Document.UndoStack.Undo(); if (editor.Text != before) throw new IOException("Wiki completion was not one complete Undo action.");
        editor.Document.UndoStack.Redo(); if (editor.Text != completed) throw new IOException("Wiki completion Redo changed source.");
        editor.Text = before; editor.CaretOffset = before.Length; editor.IsReadOnly = true;
        if (EditorCompletionService.ApplyWiki(editor, catalogue, "docs/Café guide.md") || editor.Text != before) throw new IOException("Wiki completion changed a read-only document.");
        editor.IsReadOnly = false; editor.Select(0, 2);
        if (EditorCompletionService.ApplyWiki(editor, catalogue, "docs/Café guide.md") || editor.Text != before) throw new IOException("Wiki completion overwrote an unrelated selection.");
        var content = new ExportContent(parsed, new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>());
        var bytes = new DocxExporter().Export(content); File.WriteAllBytes(Path.Combine(output, "native-wikilinks.docx"), bytes);
        using (var package = WordprocessingDocument.Open(new MemoryStream(bytes), false))
        {
            var main = package.MainDocumentPart ?? throw new IOException("Word output has no document part.");
            if (new OpenXmlValidator().Validate(package).Any() || main.Document?.InnerText.Contains("the Café guide", StringComparison.Ordinal) != true
                || main.HyperlinkRelationships.Any()) throw new IOException("Word export lost wiki aliases or created external actions.");
        }
        var plain = new NativeMarkdownRenderer(new NativeSvgService()).Render(parsed, false, new Dictionary<FencedCodeBlock, DiagramResult>(), _ => { }, (_, _) => { });
        if (!plain.ReadText().Contains("the Café guide", StringComparison.Ordinal) || plain.Document.Blocks.OfType<Paragraph>().Any(p => p.Inlines.OfType<Hyperlink>().Any()))
            throw new IOException("Read-only native rendering unexpectedly enabled workspace navigation.");
        File.WriteAllText(Path.Combine(output, "native-wikilinks.json"), JsonSerializer.Serialize(new { Passed = true, Checks = new[] {
            "Native links retain accessible aliases, exact target dispatch and literal code in both themes",
            "Read-only rendering preserves labels without enabling workspace actions",
            "Native completion preserves Unicode, CRLF, exact Undo/Redo and protected selections",
            "Actual schema-valid Word output retains labels without external relationships" } }));
    }
}
