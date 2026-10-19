using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.LayoutExperiment;

internal static class ClipboardChecks
{
    private static string ReadStream(Stream stream) { stream.Position = 0; return new StreamReader(stream).ReadToEnd().TrimEnd('\0'); }
    public static async Task ProbeStock(ExperimentWindow window, string output)
    {
        var parsed = new MarkdownService().Parse("- **first**\n- *second*\n\n  continuation\n\n| A | B |\n|---|---|\n| x | y |");
        var result = await new NativeMarkdownRenderer(new NativeSvgService()).RenderAsync(parsed, false, new Dictionary<Markdig.Syntax.FencedCodeBlock, LensDocsStudio.Core.Rendering.DiagramResult>(), _ => { }, (_, _) => { });
        var viewer = new FlowDocumentScrollViewer { Width = 640, Height = 300, IsToolBarVisible = false, Document = result.Document };
        window.Content = viewer; window.UpdateLayout(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var list = result.Document.Blocks.OfType<System.Windows.Documents.List>().Single();
        var first = (Run)((Paragraph)list.ListItems.FirstListItem.Blocks.FirstBlock).Inlines.FirstInline;
        var last = (Run)((Paragraph)list.ListItems.LastListItem.Blocks.FirstBlock).Inlines.FirstInline;
        var samples = new List<object>();
        foreach (var (name, start, end) in new[] { ("literal-only", first.ContentStart, first.ContentEnd), ("first-paragraph", first.ContentStart, first.ContentEnd.GetNextInsertionPosition(LogicalDirection.Forward)!),
            ("cross-item-literals", first.ContentStart, last.ContentEnd), ("whole-document", result.Document.ContentStart, result.Document.ContentEnd) })
        {
            viewer.Selection.Select(start, end); viewer.Focus(); ApplicationCommands.Copy.Execute(null, viewer);
            var text = Clipboard.GetText(); var data = Clipboard.GetDataObject()!;
            var raw = data.GetData(DataFormats.Rtf);
            var rtf = raw switch { string value => value, Stream stream => ReadStream(stream), byte[] bytes => Encoding.UTF8.GetString(bytes), _ => throw new IOException("Stock RTF missing or unsupported type: " + raw?.GetType().Name) };
            File.WriteAllText(Path.Combine(output, name + ".rtf"), rtf);
            var document = new FlowDocument(); var range = new TextRange(document.ContentStart, document.ContentEnd);
            if (rtf.Length > 0) range.Load(new MemoryStream(Encoding.UTF8.GetBytes(rtf)), DataFormats.Rtf);
            samples.Add(new { Name = name, Text = text, RangeText = viewer.Selection.Text, Formats = data.GetFormats(false), Rtf = rtf, DecodedRtfText = range.Text, XamlType = data.GetData(DataFormats.Xaml)?.GetType().Name, RtfType = raw?.GetType().Name });
        }
        Program.Write(Path.Combine(output, "stock-clipboard-oracle.json"), samples);
    }
}
