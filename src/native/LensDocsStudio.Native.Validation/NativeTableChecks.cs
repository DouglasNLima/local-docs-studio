using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AngleSharp.Html.Parser;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Exports;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;
using MdTable = Markdig.Extensions.Tables.Table;

namespace LensDocsStudio.Native.Validation;

internal static class NativeTableChecks
{
    public static void Run(string output)
    {
        var workspace = Path.Combine(output, "table-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(workspace);
        var pixels = new byte[] { 24, 64, 180, 255, 40, 180, 72, 255 };
        var bitmap = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Bgra32, null, pixels, 8); bitmap.Freeze();
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var file = File.Create(Path.Combine(workspace, "image.png"))) encoder.Save(file);
        const string source = "# Excluded heading\n\n![Unrelated](missing.png)\n\n```mermaid\nnot a supported diagram\n```\n\n" +
            "| Name | Value |\n|---|---|\n| ![Café][asset] | =1+1 |\n| Identifier | 000123 |\n\n[asset]: image.png\n\nExcluded conclusion.";
        var path = Path.Combine(workspace, "source.md"); File.WriteAllText(path, source);
        var parsed = new MarkdownService().Parse(source); var table = parsed.Syntax.Descendants<MdTable>().Single();
        var selection = new ExportTableSelection(table.Span.Start, table.Span.Length);
        var prepared = new ExportPreparationService(new MarkdownService(), new DiagramService(), new NativeSvgService())
            .Prepare(source, path, workspace, false, table: selection);
        if (prepared.Images.Count != 1 || prepared.Diagrams.Count != 0 || !prepared.Images.ContainsKey("image.png"))
            throw new IOException("Table preparation included resources outside the selected table.");
        var copied = new ClipboardExportService().PrepareAsync(source, path, workspace, false, table: selection).GetAwaiter().GetResult();
        var html = new HtmlParser().ParseDocument(HtmlClipboardFormat.Decode(copied.Html));
        var image = html.QuerySelector("img") ?? throw new IOException("The table's reference image was not embedded.");
        var imageBytes = Convert.FromBase64String(image.GetAttribute("src")![("data:image/png;base64,".Length)..]);
        using var input = new MemoryStream(imageBytes);
        var decoded = new FormatConvertedBitmap(BitmapFrame.Create(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad), PixelFormats.Bgra32, null, 0);
        var actualPixels = new byte[8]; decoded.CopyPixels(actualPixels, 8, 0);
        if (!actualPixels.SequenceEqual(pixels) || copied.PlainText.Contains("Excluded", StringComparison.Ordinal)
            || html.QuerySelectorAll("table").Length != 1 || !copied.PlainText.Contains("Identifier\t000123", StringComparison.Ordinal)
            || !html.QuerySelector("table")!.HasAttribute("x:str")) throw new IOException("Selected table copy lost pixels, literal cells or its content boundary.");
        var received = new List<ExportTableSelection>();
        var rendered = new NativeMarkdownRenderer(new NativeSvgService()).Render(parsed, false, new Dictionary<FencedCodeBlock, DiagramResult>(), _ => { }, (_, _) => { }, copyTable: received.Add);
        var nativeTable = (System.Windows.Documents.Table)rendered.SourceElements[table.Span.Start];
        ((MenuItem)nativeTable.ContextMenu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        if (received.Single() != selection || new TextRange(nativeTable.ContentStart, nativeTable.ContentEnd).Text.Contains("Copy table", StringComparison.Ordinal))
            throw new IOException("The native table action selected a different source span or leaked into document text.");
        var oversized = TableContextMenu.Create(table, source + new string('x', 4 * 1024 * 1024), received.Add);
        var disabled = (MenuItem)oversized.Items[0]; disabled.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        if (disabled.IsEnabled || received.Count != 1) throw new IOException("Oversized table copying was not disabled before dispatch.");
        var broken = source.Replace("[asset]: image.png", "[asset]: selected-missing.png", StringComparison.Ordinal);
        try
        {
            new ClipboardExportService().PrepareAsync(broken, path, workspace, false, table: selection).GetAwaiter().GetResult();
            throw new IOException("Missing content inside the selected table did not stop preparation.");
        }
        catch (FileNotFoundException) { }
        if (File.ReadAllText(path) != source) throw new IOException("Table copying altered source files.");
        File.WriteAllText(Path.Combine(output, "native-table.json"), JsonSerializer.Serialize(new
        {
            Passed = true,
            Checks = new[] { "Only selected-table resources are prepared; unrelated missing images and invalid diagrams do not block copying",
                "Referenced images retain decoded pixels and literal-cell metadata in formatted table output",
                "Native table context actions dispatch exact source positions without entering document text",
                "Oversized actions cannot dispatch and missing selected images fail without source writes" }
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
