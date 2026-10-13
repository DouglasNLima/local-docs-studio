using System.Text;
using System.Text.Json;
using System.Windows.Controls;
using ContinuousViewer.Model;
using DocumentFormat.OpenXml.Packaging;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.Exports;

namespace ContinuousViewer;

internal static class ExportChecks
{
    public static void Run(string output)
    {
        Directory.CreateDirectory(output); HarnessSupport.Pixel(output); var checks = new List<object>();
        foreach (var fixture in FixtureCatalogue.All.Where(f => f.Name is "list" or "table" or "mixed"))
        {
            using var viewer = new VirtualDocumentView(); using var host = HarnessSupport.Host(viewer);
            var model = LogicalDocument.Build(new MarkdownService().Parse(fixture.Source)); viewer.SetDocument(model); HarnessSupport.Layout(viewer); HarnessSupport.Idle();
            var actual = VirtualDocumentView.Descendants<ListBoxItem>(viewer).Count();
            foreach (var format in new[] { "DOCX", "PDF", "HTML" })
            {
                var task = new DocumentExportService().ExportAsync(format, model.Parsed.Source, Path.Combine(output, "synthetic.md"), output, false, null);
                HarnessSupport.Pump(task); var bytes = task.Result; File.WriteAllBytes(Path.Combine(output, fixture.Name + "." + format.ToLowerInvariant()), bytes);
                string text;
                if (format == "DOCX")
                {
                    using var stream = new MemoryStream(bytes); using var word = WordprocessingDocument.Open(stream, false);
                    text = (word.MainDocumentPart?.Document ?? throw new InvalidDataException("The generated DOCX body is absent.")).InnerText;
                }
                else if (format == "PDF")
                {
                    using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes);
                    File.WriteAllText(Path.Combine(output, fixture.Name + "-raw-pdf-text.txt"), string.Join("\n", pdf.GetPages().Select(p => p.Text)));
                    // PdfPig's raw Text concatenates glyphs; inspect its extracted word sequence for layout spaces.
                    text = string.Join(" ", pdf.GetPages().SelectMany(p => p.GetWords()).Select(w => w.Text));
                }
                else text = Encoding.UTF8.GetString(bytes);
                var complete = text.Contains(fixture.Last, StringComparison.Ordinal); var unchanged = viewer.Document!.Parsed.Source == fixture.Source;
                checks.Add(new { fixture.Name, format, bytes = bytes.Length, actualNativeItemContainers = actual,
                    logicalPieces = model.Pieces.Length, includesDistantContent = complete, originalSourcePreserved = unchanged,
                    scope = "Established complete-source export pipeline; no dependence on realised native controls; no Office/destination certification." });
                File.WriteAllText(Path.Combine(output, "exports.json"), JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true }));
                if (!complete || !unchanged || actual >= 100) throw new IOException("Virtualised presentation produced an incomplete " + format + " export for " + fixture.Name);
            }
        }
        Console.WriteLine("Complete logical DOCX/PDF/HTML export: nine checks passed.");
    }
}
