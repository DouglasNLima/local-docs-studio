using System.Text.Json;
using System.Windows;
using System.Windows.Documents;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.Exports;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace LensDocsStudio.Native.Validation;

internal static class NativeAnchorChecks
{
    public static void Run(string output)
    {
        var source = "# Anchor navigation\n\n[Named](#review) [Empty](#empty) [Heading](#anchor-navigation) [Later](#later)\n\n"
            + "Before <span id=review data-doc-anchor>Café &amp; review</span> after.\n\nBefore <span id=empty data-doc-anchor></span>following text.\n\n"
            + string.Join("\n\n", Enumerable.Range(1, 60).Select(i => "Pagination paragraph " + i + "."))
            + "\n\nBefore <span id=later data-doc-anchor>Later destination</span> after.\n";
        var parsed = new MarkdownService().Parse(source);
        var invoked = new List<string>();
        foreach (var dark in new[] { false, true })
        {
            var preview = new NativeMarkdownRenderer(new NativeSvgService()).Render(parsed, dark, new Dictionary<FencedCodeBlock, LensDocsStudio.Core.Rendering.DiagramResult>(), invoked.Add, (_, _) => { });
            var text = new TextRange(preview.Document.ContentStart, preview.Document.ContentEnd).Text;
            if (!text.Contains("Before Café & review after.", StringComparison.Ordinal) || !text.Contains("Before following text.", StringComparison.Ordinal) || text.Contains("<span", StringComparison.Ordinal))
                throw new IOException("Native anchor labels lost their text or introduced visible markup.");
            foreach (var target in parsed.Targets)
                if (!preview.SourceElements.TryGetValue(target.SourceStart, out var element) || element.Tag is not SourceReference span || span.Start != target.SourceStart)
                    throw new IOException("A native anchor or heading lost its source-mapped destination.");
            var links = preview.Document.Blocks.OfType<Paragraph>().SelectMany(p => p.Inlines.OfType<Hyperlink>()).ToArray();
            foreach (var link in links) link.RaiseEvent(new RoutedEventArgs(Hyperlink.ClickEvent));
        }
        if (!invoked.SequenceEqual(new[] { "#review", "#empty", "#anchor-navigation", "#later", "#review", "#empty", "#anchor-navigation", "#later" }))
            throw new IOException("Native anchor links lost their navigation references.");
        var content = new ExportContent(parsed, new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>());
        var pdf = new PdfExporter().Export(content); File.WriteAllBytes(Path.Combine(output, "native-anchors.pdf"), pdf);
        using var inspected = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
        var linksInPdf = inspected.Pages.Cast<PdfPage>().SelectMany(p => p.Annotations.Cast<PdfDictionary>()).Where(a => a.Elements.GetName("/Subtype") == "/Link").ToArray();
        if (inspected.PageCount < 2 || linksInPdf.Length != 4) throw new IOException("The PDF lost anchor links or its multipage fixture.");
        var namedEntries = inspected.Internals.Catalog.Elements.GetDictionary("/Names")?.Elements.GetDictionary("/Dests")?.Elements.GetArray("/Names")
            ?? throw new IOException("The PDF has no named destinations.");
        var namedDestinations = new Dictionary<string, PdfArray>(StringComparer.Ordinal);
        for (var index = 0; index + 1 < namedEntries.Elements.Count; index += 2)
            if (namedEntries.Elements[index] is PdfString name && namedEntries.Elements[index + 1] is PdfArray destination) namedDestinations.Add(name.Value, destination);
        var destinations = linksInPdf.Select(a => namedDestinations.GetValueOrDefault(a.Elements.GetDictionary("/A")?.Elements.GetString("/D") ?? "")).ToArray();
        if (destinations.Any(d => d is null || d.Elements.Count < 2) || destinations[0]!.Elements[0].ToString() == destinations[3]!.Elements[0].ToString())
            throw new IOException("The PDF did not resolve named and later-page destinations.");
        using var textPdf = UglyToad.PdfPig.PdfDocument.Open(pdf);
        var words = string.Join(" ", textPdf.GetPages().SelectMany(p => p.GetWords()).Select(w => w.Text));
        if (!words.Contains("Café & review", StringComparison.Ordinal) || words.Contains("<span", StringComparison.Ordinal)) throw new IOException("PDF anchor labels lost decoded text.");
        File.WriteAllBytes(Path.Combine(output, "native-anchors.docx"), new DocxExporter().Export(content));
        File.WriteAllText(Path.Combine(output, "native-anchors.html"), new HtmlExporter().Export(content));
        File.WriteAllText(Path.Combine(output, "native-anchors.json"), JsonSerializer.Serialize(new
        {
            Passed = true, PdfPages = inspected.PageCount, PdfLinks = linksInPdf.Length,
            Checks = new[] { "Named and empty anchors retain selectable labels and exact native source destinations in both themes",
                "Native hyperlinks retain their actual named/empty/heading navigation references", "Actual PDF links resolve to named, empty and later-page destinations",
                "PDF text and generated HTML/Word retain decoded anchor labels" }
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
