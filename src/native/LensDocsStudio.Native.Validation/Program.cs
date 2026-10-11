using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Rendering;
using LensDocsStudio.Native.Exports;
using LensDocsStudio.Core.Exports;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.FirstOrDefault() == "--ui")
            return NativeUiChecks.Run(args[1], args.ElementAtOrDefault(2) ?? "artifacts/native/ui");
        if (args.FirstOrDefault() == "--reject-unsigned")
        {
            try
            {
                new LensDocsStudio.Native.Platform.AuthenticodeVerifier().VerifyAsync(Path.GetFullPath(args[1]), [new("CN=Development test", new string('A', 64))], default).GetAwaiter().GetResult();
                Console.Error.WriteLine("Unsigned installer was incorrectly accepted."); return 1;
            }
            catch (IOException ex) when (ex.Message.StartsWith("Windows rejected the installer signature", StringComparison.Ordinal))
            { Console.WriteLine("Actual Windows Authenticode verification rejected the unsigned installer: " + ex.Message); return 0; }
        }
        var output = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/native/validation");
        var repository = Path.GetFullPath(args.ElementAtOrDefault(1) ?? ".");
        Directory.CreateDirectory(output);
        var parser = new MarkdownService();
        var diagrams = new DiagramService();
        var native = new NativeSvgService();
        var results = new List<object>();
        var failures = new List<string>();
        try { NativeSvgChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native SVG safety: " + ex.Message); }
        try { NativeAssetChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native asset persistence: " + ex.Message); }
        try { NativePreviewChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native preview preparation: " + ex.Message); }
        var count = 0;
        foreach (var path in Directory.EnumerateFiles(Path.Combine(repository, "tests", "fixtures"), "*", SearchOption.AllDirectories)
            .Where(p => Path.GetExtension(p) is ".md" or ".mmd" or ".mermaid").Order())
        {
            var text = File.ReadAllText(path);
            var standalone = Path.GetExtension(path) is ".mmd" or ".mermaid";
            var document = parser.Parse(text, standalone);
            foreach (var code in document.Syntax.Descendants<FencedCodeBlock>().Where(c => c.Info is "mermaid" or "mmd"))
            {
                foreach (var dark in new[] { false, true })
                {
                    var name = $"{++count:D3}-{Path.GetFileNameWithoutExtension(path)}-{(dark ? "dark" : "light")}";
                    var watch = Stopwatch.StartNew();
                    var diagram = diagrams.Render(code.Lines.ToString(), dark);
                    // The web autocomplete fixture intentionally contains only the incomplete header "flo".
                    var expectedInvalid = Path.GetFileName(path) is "broken-mermaid.md" or "autocomplete.mmd";
                    var valid = diagram.Success;
                    string? error = diagram.Error;
                    int bytes = 0;
                    if (valid)
                    {
                        try
                        {
                            var drawing = LensDocsStudio.Native.Platform.StaWorker.RunAsync(() => native.Render(diagram.Svg!)).GetAwaiter().GetResult();
                            var png = NativeSvgService.ToPng(drawing);
                            File.WriteAllBytes(Path.Combine(output, name + ".png"), png);
                            File.WriteAllText(Path.Combine(output, name + ".svg"), native.Prepare(diagram.Svg!));
                            bytes = png.Length;
                            if (bytes < 100 || drawing.Width < 10 || drawing.Height < 10) throw new InvalidDataException("No meaningful native drawing.");
                        }
                        catch (Exception ex) { valid = false; error = ex.Message; }
                    }
                    var passed = expectedInvalid ? !diagram.Success : valid;
                    if (!passed) failures.Add(name + ": " + error);
                    results.Add(new { Fixture = Path.GetRelativePath(repository, path), Line = code.Line + 1, Theme = dark ? "Dark" : "Light", Passed = passed, ExpectedInvalid = expectedInvalid, Error = error, PngBytes = bytes, ElapsedMilliseconds = watch.ElapsedMilliseconds });
                }
            }
        }
        if (count == 0) failures.Add("No Mermaid fixtures discovered.");
        var cacheWatch = Stopwatch.StartNew();
        const string cacheSource = "flowchart LR\nA[Edit source] --> B[Reuse unchanged diagram]";
        var cachedDiagram = diagrams.Render(cacheSource);
        var cachedDrawing = native.Render(cachedDiagram.Svg!);
        var firstRenderMilliseconds = cacheWatch.Elapsed.TotalMilliseconds;
        cacheWatch.Restart();
        var reused = true;
        for (var repeat = 0; repeat < 20; repeat++)
        {
            var nextDiagram = diagrams.Render(cacheSource);
            reused &= ReferenceEquals(cachedDiagram, nextDiagram) && ReferenceEquals(cachedDrawing, native.Render(nextDiagram.Svg!));
        }
        if (!reused) failures.Add("Unchanged diagram rendering did not reuse its native drawing.");
        File.WriteAllText(Path.Combine(output, "preview-cache.json"), JsonSerializer.Serialize(new { FirstRenderMilliseconds = firstRenderMilliseconds, TwentyCachedRendersMilliseconds = cacheWatch.Elapsed.TotalMilliseconds, Reused = reused }));
        File.WriteAllText(Path.Combine(output, "mermaid-corpus.json"), JsonSerializer.Serialize(new { Total = count, Failures = failures, Results = results }, new JsonSerializerOptions { WriteIndented = true }));
        try
        {
            var sourcePath = Path.Combine(repository, "tests", "fixtures", "mixed.md");
            var content = new ExportPreparationService(parser, diagrams, native).Prepare(File.ReadAllText(sourcePath), sourcePath, Path.GetDirectoryName(sourcePath), false);
            var docx = new DocxExporter().Export(content);
            File.WriteAllBytes(Path.Combine(output, "mixed.docx"), docx);
            using (var package = WordprocessingDocument.Open(new MemoryStream(docx), false))
            {
                var errors = new OpenXmlValidator().Validate(package).Select(e => e.Description).ToArray();
                if (errors.Length != 0) throw new InvalidDataException(string.Join("; ", errors));
                if (!package.MainDocumentPart!.ImageParts.Any()) throw new InvalidDataException("The DOCX has no rendered diagram image.");
            }
            var pdf = new PdfExporter().Export(content);
            File.WriteAllBytes(Path.Combine(output, "mixed.pdf"), pdf);
            if (!System.Text.Encoding.ASCII.GetString(pdf, 0, 5).Equals("%PDF-")) throw new InvalidDataException("The local PDF header is invalid.");
            using (var inspected = UglyToad.PdfPig.PdfDocument.Open(pdf))
            {
                var pages = inspected.GetPages().ToArray();
                var text = string.Join(" ", pages.SelectMany(p => p.GetWords()).Select(w => w.Text));
                if (!text.Contains("Export Fixture", StringComparison.Ordinal) || !text.Contains("Checklist", StringComparison.Ordinal))
                    throw new InvalidDataException("The PDF lost required document text.");
                if (!pages.Any(p => p.NumberOfImages > 0)) throw new InvalidDataException("The PDF lost the Mermaid image.");
                File.WriteAllText(Path.Combine(output, "pdf-inspection.json"), JsonSerializer.Serialize(new { Pages = pages.Length, Images = pages.Sum(p => p.NumberOfImages), Text = text }, new JsonSerializerOptions { WriteIndented = true }));
            }
            File.WriteAllText(Path.Combine(output, "mixed.html"), new HtmlExporter().Export(content));
            Console.WriteLine($"Exports generated and DOCX schema validated: {docx.Length} DOCX bytes, {pdf.Length} PDF bytes.");
        }
        catch (Exception ex) { failures.Add("Export validation: " + ex.Message); }
        File.WriteAllText(Path.Combine(output, "validation-summary.json"), JsonSerializer.Serialize(new { DiagramCases = count, Failures = failures }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Native validation: {count} diagram cases and export checks; {failures.Count} failures. Evidence: {output}");
        foreach (var failure in failures) Console.WriteLine(failure);
        return failures.Count == 0 ? 0 : 1;
    }
}
