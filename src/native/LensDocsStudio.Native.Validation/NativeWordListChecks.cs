using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Exports;
using LensDocsStudio.Native.Rendering;
using W = DocumentFormat.OpenXml.Wordprocessing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace LensDocsStudio.Native.Validation;

internal static class NativeWordListChecks
{
    public static void Run(string output, string repository)
    {
        var sourcePath = Path.Combine(repository, "tests", "fixtures", "native", "pdf-lists.md");
        var source = File.ReadAllText(sourcePath);
        var preparation = new ExportPreparationService(new MarkdownService(), new DiagramService(), new NativeSvgService());
        var content = preparation.Prepare(source, sourcePath, Path.GetDirectoryName(sourcePath)!, false);
        var bytes = new DocxExporter().Export(content);
        File.WriteAllBytes(Path.Combine(output, "native-word-lists.docx"), bytes);
        using var package = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        Validate(package);
        var paragraphs = package.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>().ToArray();
        var markers = paragraphs.Where(p => p.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value > 0).ToArray();
        if (markers.Length != 12 || paragraphs.Where(p => p.InnerText.Contains("Continuation", StringComparison.Ordinal))
            .Any(p => p.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value != 0))
            throw new IOException("Native Word preparation lost list item boundaries or continuation numbering overrides.");

        const string visualSource = "2. ```mermaid\n   flowchart LR\n   A[Write] --> B[Review]\n   ```\n\n   DiagramContinuation\n\n3. $$\n   x^2 + y^2\n   $$\n\n   FormulaContinuation\n\nAfterVisualLists";
        var visualPath = Path.Combine(output, "native-word-list-visuals.md"); File.WriteAllText(visualPath, visualSource);
        var visuals = preparation.Prepare(visualSource, visualPath, output, false);
        var visualBytes = new DocxExporter().Export(visuals);
        File.WriteAllBytes(Path.Combine(output, "native-word-list-visuals.docx"), visualBytes);
        using var visualPackage = WordprocessingDocument.Open(new MemoryStream(visualBytes), false);
        Validate(visualPackage);
        var visualParagraphs = visualPackage.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>().ToArray();
        var images = visualParagraphs.SelectMany(p => p.Descendants<DW.Inline>()).ToArray();
        if (visuals.Diagrams.Count != 1 || visuals.Formulae.Count != 1 || images.Length != 2
            || visualParagraphs.Count(p => p.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value > 0) != 2
            || visualParagraphs.Where(p => p.InnerText.EndsWith("Continuation", StringComparison.Ordinal))
                .Any(p => p.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value != 0)
            || images.Any(i => i.Extent?.Cx?.Value is null or <= 0 || i.Extent.Cx.Value > (11906 - 2268 - 360) * 635L))
            throw new IOException("Native diagrams or formulae lost list markers, continuations or available-width bounds in Word.");
        if (content.Document.Source != source || visuals.Document.Source != visualSource) throw new IOException("Word list preparation changed source.");
        File.WriteAllText(Path.Combine(output, "native-word-lists.json"), JsonSerializer.Serialize(new
        {
            Passed = true, MarkedItems = markers.Length, NativeDiagramImages = visuals.Diagrams.Count, NativeFormulaImages = visuals.Formulae.Count,
            Checks = new[] { "Native preparation retains list item boundaries and explicit unnumbered continuations",
                "First-block Mermaid and maths retain editable list numbering and bounded native images",
                "Both actual DOCX packages pass Open XML schema validation and source remains unchanged" },
            Paragraphs = paragraphs.Select(p => new { Text = p.InnerText, NumberingId = p.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value,
                Level = p.ParagraphProperties?.NumberingProperties?.NumberingLevelReference?.Val?.Value, LeftIndent = p.ParagraphProperties?.Indentation?.Left?.Value }),
            Limitation = "Package semantics and native preparation are checked; actual Microsoft Word pagination, wide-marker layout and screen-reader behaviour remain unverified."
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void Validate(WordprocessingDocument package)
    {
        var errors = new OpenXmlValidator().Validate(package).Select(e => e.Description).ToArray();
        if (errors.Length != 0) throw new IOException(string.Join(" ", errors));
    }
}
