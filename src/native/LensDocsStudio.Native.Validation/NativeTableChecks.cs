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
        var received = new List<ExportTableSelection>(); var csvSelections = new List<ExportTableSelection>();
        var rendered = new NativeMarkdownRenderer(new NativeSvgService()).Render(parsed, false, new Dictionary<FencedCodeBlock, DiagramResult>(), _ => { }, (_, _) => { }, copyTable: received.Add, exportTableCsv: csvSelections.Add);
        var nativeTable = (System.Windows.Documents.Table)rendered.SourceElements[table.Span.Start];
        ((MenuItem)nativeTable.ContextMenu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        ((MenuItem)nativeTable.ContextMenu.Items[1]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        if (received.Single() != selection || csvSelections.Single() != selection || new TextRange(nativeTable.ContentStart, nativeTable.ContentEnd).Text.Contains("Copy table", StringComparison.Ordinal))
            throw new IOException("The native table action selected a different source span or leaked into document text.");
        var oversized = TableContextMenu.Create(table, source + new string('x', 4 * 1024 * 1024), received.Add, csvSelections.Add);
        foreach (MenuItem disabled in oversized.Items)
        {
            disabled.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            if (disabled.IsEnabled || received.Count != 1 || csvSelections.Count != 1) throw new IOException("Oversized table actions were not disabled before dispatch.");
        }
        var broken = source.Replace("[asset]: image.png", "[asset]: selected-missing.png", StringComparison.Ordinal);
        try
        {
            new ClipboardExportService().PrepareAsync(broken, path, workspace, false, table: selection).GetAwaiter().GetResult();
            throw new IOException("Missing content inside the selected table did not stop preparation.");
        }
        catch (FileNotFoundException) { }
        var csv = new TableCsvExporter().Export(broken, selection);
        var csvPath = Path.Combine(workspace, "source-table-1.csv");
        var store = new ExportFileStore(Path.Combine(workspace, "recovery"));
        store.WriteAsync(csvPath, csv.Bytes, null).GetAwaiter().GetResult();
        var expectedCsv = System.Text.Encoding.UTF8.GetBytes("Name,Value\r\nCafé,=1+1\r\nIdentifier,000123");
        if (csv.TableNumber != 1 || !File.ReadAllBytes(csvPath).SequenceEqual(expectedCsv))
            throw new IOException("Saved table CSV lost Unicode, literal values or its resource-independent boundary.");
        var conflictPath = Path.Combine(workspace, "conflict.csv"); File.WriteAllBytes(conflictPath, csv.Bytes);
        var previous = ExportFileStore.ReadExistingAsync(conflictPath).GetAwaiter().GetResult();
        File.WriteAllText(conflictPath, "External revision");
        try
        {
            store.WriteAsync(conflictPath, csv.Bytes, previous).GetAwaiter().GetResult();
            throw new InvalidOperationException("CSV export overwrote an external revision.");
        }
        catch (IOException) { }
        if (File.ReadAllText(conflictPath) != "External revision") throw new IOException("CSV conflict handling lost the external revision.");
        var cancelledPath = Path.Combine(workspace, "cancelled.csv");
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            try { store.WriteAsync(cancelledPath, csv.Bytes, null, cancelled.Token).GetAwaiter().GetResult(); throw new IOException("Cancelled CSV export completed."); }
            catch (OperationCanceledException) { }
        }
        if (File.Exists(cancelledPath) || Directory.EnumerateFiles(workspace, "*.tmp").Any()) throw new IOException("Cancelled CSV export left a partial output.");
        if (File.ReadAllText(path) != source) throw new IOException("Table copying altered source files.");
        File.WriteAllText(Path.Combine(output, "native-table.json"), JsonSerializer.Serialize(new
        {
            Passed = true,
            Checks = new[] { "Only selected-table resources are prepared; unrelated missing images and invalid diagrams do not block copying",
                "Referenced images retain decoded pixels and literal-cell metadata in formatted table output",
                "Native table context actions dispatch exact source positions without entering document text",
                "Oversized actions cannot dispatch and missing selected images fail without source writes",
                "Guarded CSV output retains exact UTF-8 cells and image labels without reading missing resources",
                "CSV conflicts retain external bytes and cancellation leaves no partial output or source changes" }
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
