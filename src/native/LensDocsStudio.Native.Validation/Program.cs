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
        if (args.FirstOrDefault() == "--office")
            return OfficeClipboardChecks.Run(args.ElementAtOrDefault(1) ?? "artifacts/native/office");
        if (args.FirstOrDefault() == "--authenticode")
        {
            try { NativeAuthenticodeChecks.Run(args[1]); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--export-allocation")
        {
            var allocationOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(allocationOutput);
            try { NativeExportAllocationChecks.Run(allocationOutput); Console.WriteLine("Native export allocation checks passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--external-links")
        {
            var linkOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(linkOutput);
            try { NativeExternalLinkChecks.Run(linkOutput); Console.WriteLine("Native external link checks passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--diagram-contrast")
        {
            var contrastOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(contrastOutput);
            try { NativeDiagramContrastChecks.Run(contrastOutput); Console.WriteLine("Native diagram contrast checks passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--docs-site")
        {
            var siteOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation");
            Directory.CreateDirectory(siteOutput);
            try { NativeDocsSiteChecks.Run(siteOutput); Console.WriteLine("Native Docs Site integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--progress")
        {
            var progressOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(progressOutput);
            try { NativeProgressChecks.Run(progressOutput); Console.WriteLine("Native progress integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--anchors")
        {
            var anchorOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(anchorOutput);
            try { NativeAnchorChecks.Run(anchorOutput); Console.WriteLine("Native anchor integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--context")
        {
            var contextOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(contextOutput);
            try { NativeContextChecks.Run(contextOutput); Console.WriteLine("Native context integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--table")
        {
            var tableOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(tableOutput);
            try { NativeTableChecks.Run(tableOutput); Console.WriteLine("Native table integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--figures")
        {
            var figureOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(figureOutput);
            try { NativeFigureChecks.Run(figureOutput); Console.WriteLine("Native figure integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--word-template")
        {
            var templateOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(templateOutput);
            try { NativeWordTemplateChecks.Run(templateOutput); Console.WriteLine("Native Word template integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--template-library")
        {
            var libraryOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(libraryOutput);
            try { NativeTemplateLibraryChecks.Run(libraryOutput); Console.WriteLine("Native template library integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--content-library")
        {
            var contentOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(contentOutput);
            try { NativeLibraryContentChecks.Run(contentOutput); NativeContentLibraryChecks.Run(contentOutput); Console.WriteLine("Native library content integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--catalogue")
        {
            var catalogueOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(catalogueOutput);
            try { NativeContentCatalogueChecks.Run(catalogueOutput); Console.WriteLine("Native content catalogue integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--details")
        {
            var detailsOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(detailsOutput);
            try { NativeDetailsChecks.Run(detailsOutput); Console.WriteLine("Native details integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--maths")
        {
            var mathsOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(mathsOutput);
            try { NativeMathChecks.Run(mathsOutput); Console.WriteLine("Native maths integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--artefacts")
        {
            var artifactOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(artifactOutput);
            try { NativeArtifactChecks.Run(artifactOutput, args.ElementAtOrDefault(2)); Console.WriteLine("Native artefact integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--folder-move")
        {
            var folderOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(folderOutput);
            try { NativeFolderMoveChecks.Run(folderOutput); Console.WriteLine("Native folder move review passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--workspace-map")
        {
            var mapOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(mapOutput);
            try { NativeWorkspaceMapChecks.Run(mapOutput); Console.WriteLine("Native workspace map integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--wikilinks")
        {
            var wikiOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(wikiOutput);
            try { NativeWikiLinkChecks.Run(wikiOutput); Console.WriteLine("Native wikilink integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--help-guide")
        {
            var helpOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(helpOutput);
            try { NativeHelpChecks.Run(helpOutput); Console.WriteLine("Native help integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--settings")
        {
            var settingsOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(settingsOutput);
            try { NativeSettingsChecks.Run(settingsOutput); Console.WriteLine("Native settings integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--layout")
        {
            var layoutOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(layoutOutput);
            try { NativeLayoutChecks.Run(layoutOutput); Console.WriteLine("Native layout integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--preview-navigation")
        {
            var navigationOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(navigationOutput);
            try { NativePreviewNavigationChecks.Run(navigationOutput); Console.WriteLine("Native preview navigation passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--preview-scheduling")
        {
            var schedulingOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(schedulingOutput);
            try { NativePreviewSchedulingChecks.Run(schedulingOutput); Console.WriteLine("Native preview scheduling passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--clipboard-apartments")
        {
            var apartmentOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(apartmentOutput);
            try { NativeClipboardApartmentChecks.Run(apartmentOutput); Console.WriteLine("Native clipboard apartment validation passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--shell-layout")
        {
            var shellOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(shellOutput);
            try { NativeShellLayoutChecks.Run(shellOutput); Console.WriteLine("Native shell layout passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--performance")
        {
            var performanceOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(performanceOutput);
            try { NativePerformanceChecks.Run(performanceOutput); Console.WriteLine("Native performance observations passed correctness checks."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--workspace")
        {
            var workspaceOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(workspaceOutput);
            try { NativeWorkspaceChecks.Run(workspaceOutput); Console.WriteLine("Native workspace monitoring passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--review")
        {
            var reviewOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(reviewOutput);
            try { NativeReviewChecks.Run(reviewOutput); Console.WriteLine("Native review integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--editor-colours")
        {
            var colourOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(colourOutput);
            try { NativeEditorColourChecks.Run(colourOutput); Console.WriteLine("Native editor colour integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--syntax")
        {
            var syntaxOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(syntaxOutput);
            try { NativeSyntaxChecks.Run(syntaxOutput); Console.WriteLine("Native syntax integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
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
        try { NativeDiagramContrastChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native diagram label contrast: " + ex.Message); }
        try { NativeSvgChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native SVG safety: " + ex.Message); }
        try { NativeAssetChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native asset persistence: " + ex.Message); }
        try { NativePreviewChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native preview preparation: " + ex.Message); }
        try { NativeMarkdownChecks.Run(output, repository); }
        catch (Exception ex) { failures.Add("Native Markdown semantics: " + ex.Message); }
        try { NativeEditorChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native editor formatting: " + ex.Message); }
        try { NativeLayoutChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native document layout: " + ex.Message); }
        try { NativePreviewSchedulingChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native preview scheduling: " + ex.Message); }
        try { NativeClipboardApartmentChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native clipboard apartments: " + ex.Message); }
        try { NativeShellLayoutChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native shell layout: " + ex.Message); }
        try { NativePerformanceChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native performance observations: " + ex.Message); }
        try { NativeExportAllocationChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native export allocation: " + ex.Message); }
        try { NativeExternalLinkChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native external links: " + ex.Message); }
        try { NativeDocsSiteChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native Docs Site export: " + ex.Message); }
        try { NativeProgressChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native progress semantics: " + ex.Message); }
        try { NativeAnchorChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native anchor navigation: " + ex.Message); }
        try { NativeContextChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native context actions: " + ex.Message); }
        try { NativeSyntaxChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native syntax colouring: " + ex.Message); }
        try { NativeEditorColourChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native editor colours: " + ex.Message); }
        try { NativeTableChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native table copying: " + ex.Message); }
        try { NativeReviewChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native document review: " + ex.Message); }
        try { NativeWorkspaceChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native workspace monitoring: " + ex.Message); }
        try { NativeFigureChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native image figures: " + ex.Message); }
        try { NativeWordTemplateChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native Word templates: " + ex.Message); }
        try { NativeTemplateLibraryChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native template library: " + ex.Message); }
        try { NativeLibraryContentChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native library content: " + ex.Message); }
        try { NativeContentLibraryChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native content library: " + ex.Message); }
        try { NativeContentCatalogueChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native content catalogue: " + ex.Message); }
        try { NativeDetailsChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native details sections: " + ex.Message); }
        try { NativeMathChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native maths: " + ex.Message); }
        try { NativeArtifactChecks.Run(output, repository); }
        catch (Exception ex) { failures.Add("Native artefacts: " + ex.Message); }
        try { NativeWikiLinkChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native wikilinks: " + ex.Message); }
        try { NativeWorkspaceMapChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native workspace map: " + ex.Message); }
        try { NativeFolderMoveChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native folder move: " + ex.Message); }
        try { NativeHelpChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native help: " + ex.Message); }
        try { NativeSettingsChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native settings: " + ex.Message); }
        try { NativePreviewNavigationChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native preview navigation: " + ex.Message); }
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
            var exportWatch = Stopwatch.StartNew();
            var content = new ExportPreparationService(parser, diagrams, native).Prepare(File.ReadAllText(sourcePath), sourcePath, Path.GetDirectoryName(sourcePath), false);
            var prepareMilliseconds = exportWatch.Elapsed.TotalMilliseconds;
            exportWatch.Restart(); var docx = new DocxExporter().Export(content); var docxMilliseconds = exportWatch.Elapsed.TotalMilliseconds;
            File.WriteAllBytes(Path.Combine(output, "mixed.docx"), docx);
            using (var package = WordprocessingDocument.Open(new MemoryStream(docx), false))
            {
                var errors = new OpenXmlValidator().Validate(package).Select(e => e.Description).ToArray();
                if (errors.Length != 0) throw new InvalidDataException(string.Join("; ", errors));
                if (!package.MainDocumentPart!.ImageParts.Any()) throw new InvalidDataException("The DOCX has no rendered diagram image.");
            }
            exportWatch.Restart(); var pdf = new PdfExporter().Export(content); var pdfMilliseconds = exportWatch.Elapsed.TotalMilliseconds;
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
            exportWatch.Restart(); var html = new HtmlExporter().Export(content); var htmlMilliseconds = exportWatch.Elapsed.TotalMilliseconds;
            File.WriteAllText(Path.Combine(output, "mixed.html"), html);
            File.WriteAllText(Path.Combine(output, "export-performance.json"), JsonSerializer.Serialize(new {
                Fixture = "tests/fixtures/mixed.md", SourceBytes = new FileInfo(sourcePath).Length, ReadAndPrepareMilliseconds = prepareMilliseconds,
                DocxMilliseconds = docxMilliseconds, PdfMilliseconds = pdfMilliseconds, HtmlMilliseconds = htmlMilliseconds,
                DocxBytes = docx.Length, PdfBytes = pdf.Length, HtmlCharacters = html.Length,
                Limitation = "Warm validation-process observations; export durations exclude file writes and separate schema/content validation."
            }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Exports generated and DOCX schema validated: {docx.Length} DOCX bytes, {pdf.Length} PDF bytes.");
        }
        catch (Exception ex) { failures.Add("Export validation: " + ex.Message); }
        File.WriteAllText(Path.Combine(output, "validation-summary.json"), JsonSerializer.Serialize(new { DiagramCases = count, Failures = failures }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Native validation: {count} diagram cases and export checks; {failures.Count} failures. Evidence: {output}");
        foreach (var failure in failures) Console.WriteLine(failure);
        return failures.Count == 0 ? 0 : 1;
    }
}
