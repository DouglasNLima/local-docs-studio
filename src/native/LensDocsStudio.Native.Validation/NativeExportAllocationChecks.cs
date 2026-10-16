using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Exports;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

/// <summary>In-memory/isolated-file export checks; never opens a window or touches the system clipboard.</summary>
internal static class NativeExportAllocationChecks
{
    public static void Run(string output)
    {
        var root = Path.Combine(output, "export-allocation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null,
            new byte[] { 62, 136, 255, 255, 40, 40, 40, 255, 120, 120, 120, 255, 240, 240, 240, 255 }, 8);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var file = File.Create(Path.Combine(root, "image.png"))) encoder.Save(file);
        const string source = "# Repeated images\n\n![First](image.png)\n\n![Second](image.png)";
        var path = Path.Combine(root, "document.md"); File.WriteAllText(path, source);
        var preparation = new ExportPreparationService(new MarkdownService(), new DiagramService(), new NativeSvgService());
        var content = preparation.Prepare(source, path, root, false);
        if (content.Images.Count != 1) throw new IOException("Native preparation did not reuse its image resource.");
        var image = content.Images.Values.Single();
        var singleCharacters = ((long)image.PngBytes.Length + 2) / 3 * 4;
        var limited = content with { AllocationLimits = new(MaximumImageCharacters: singleCharacters + 1) };
        var checks = new List<string>();
        Rejected("HTML repeated-image bound", () => new HtmlExporter().Export(limited));
        Rejected("Word repeated-image bound", () => new DocxExporter().Export(limited));
        Rejected("PDF repeated-image bound", () => new PdfExporter().Export(limited));
        Rejected("Rendered copy repeated-image bound", () => new ClipboardExporter().Export(limited));
        Rejected("Docs Site repeated-image bound", () => new DocsSiteExporter().PreparePage("document.md", limited));
        var pdf = new PdfExporter().Export(content);
        File.WriteAllBytes(Path.Combine(root, "repeated-images.pdf"), pdf);
        using (var inspected = UglyToad.PdfPig.PdfDocument.Open(pdf))
        {
            var text = string.Join(" ", inspected.GetPages().SelectMany(p => p.GetWords()).Select(w => w.Text));
            var imageCount = inspected.GetPages().Sum(p => p.GetImages().Count());
            if (!text.Contains("Repeated images", StringComparison.Ordinal) || imageCount != 2)
                throw new IOException($"The PDF did not retain both repeated images and its heading: images={imageCount}, text={text}.");
        }
        checks.Add("PDF repeated-image fidelity");
        Rejected("PDF bounded output", () => new PdfExporter().Export(content with { AllocationLimits = new(MaximumOutputBytes: 128) }));
        try { new PdfExporter().Export(content, new CancellationToken(true)); throw new IOException("Cancelled PDF export completed."); }
        catch (OperationCanceledException) { checks.Add("PDF cancellation before generation"); }
        if (File.ReadAllText(path) != source) throw new IOException("Export validation altered its source.");
        checks.Add("Source preservation");
        File.WriteAllText(Path.Combine(output, "native-export-allocation-results.json"), JsonSerializer.Serialize(new { passed = checks.Count, checks }, new JsonSerializerOptions { WriteIndented = true }));

        void Rejected(string label, Action action)
        {
            try { action(); }
            catch (IOException) { checks.Add(label); return; }
            throw new IOException(label + " did not reject excess allocation.");
        }
    }
}
