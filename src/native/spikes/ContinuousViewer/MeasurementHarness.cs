using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ContinuousViewer.Model;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;

namespace ContinuousViewer;

internal static class MeasurementHarness
{
    public static void Run(string engine, string fixtureName, string output)
    {
        Directory.CreateDirectory(output); HarnessSupport.Pixel(output);
        var fixture = FixtureCatalogue.All.Single(f => f.Name == fixtureName);
        // Every process has a cold sample and an explicitly separate warm sample; never pool them.
        for (var repetition = 0; repetition < 2; repetition++)
        {
            var observations = new Dictionary<string, object?>
            {
                ["engine"] = engine, ["fixture"] = fixtureName, ["scenario"] = repetition == 0 ? "cold" : "warm",
                ["sourceUtf8Bytes"] = Encoding.UTF8.GetByteCount(fixture.Source), ["sourceSha256"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fixture.Source))),
                ["viewport"] = "640 × 300", ["runtime"] = Environment.Version.ToString(), ["os"] = Environment.OSVersion.ToString(),
                ["processors"] = Environment.ProcessorCount, ["inputBudgetMilliseconds"] = 250,
                ["nativeAssemblySha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(NativeMarkdownRenderer).Assembly.Location))),
                ["prototypeAssemblySha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(VirtualDocumentView).Assembly.Location)))
            };
            var filename = Path.Combine(output, observations["scenario"] + ".json");
            try
            {
                if (engine == "prototype") Prototype(fixture, output, observations, repetition);
                else if (engine == "baseline") Baseline(fixture, output, observations, repetition);
                else throw new ArgumentException("Unknown presentation engine.");
                observations["completed"] = true;
            }
            catch (Exception error)
            { observations["completed"] = false; observations["error"] = error.ToString(); }
            using var process = Process.GetCurrentProcess(); process.Refresh();
            observations["peakWorkingSetBytes"] = process.PeakWorkingSet64;
            File.WriteAllText(filename, JsonSerializer.Serialize(observations, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"{engine} {fixtureName} {observations["scenario"]}: input={observations.GetValueOrDefault("firstLayoutInputMilliseconds")} ms, {observations.GetValueOrDefault("inputAcceptance", "INCOMPLETE")}");
            if (!(bool)observations["completed"]!) break; // Preserve a timeout rather than retrying until green.
        }
    }
    private static void Prototype(ViewerFixture fixture, string output, Dictionary<string, object?> result, int repetition)
    {
        var allocated = GC.GetTotalAllocatedBytes(); var watch = Stopwatch.StartNew(); var overall = Stopwatch.StartNew();
        // Background parsing/projection is timed separately from actual dispatcher layout.
        var modelTask = Task.Run(() => LogicalDocument.Build(new MarkdownService().Parse(fixture.Source)));
        HarnessSupport.Pump(modelTask); var model = modelTask.Result;
        result["logicalReadinessMilliseconds"] = watch.Elapsed.TotalMilliseconds;
        var mediaTask = DocumentLoader.PrepareMedia(model, Path.Combine(output, "synthetic.md"), output);
        HarnessSupport.Pump(mediaTask); var media = mediaTask.Result;
        result["preparationMilliseconds"] = watch.Elapsed.TotalMilliseconds;
        using var viewer = new VirtualDocumentView(); using var host = HarnessSupport.Host(viewer);
        HarnessSupport.Layout(viewer); HarnessSupport.Idle();
        watch.Restart(); using var input = new InputProbe();
        result["firstLayoutInputMilliseconds"] = input.Measure(() => { viewer.SetDocument(model, media); HarnessSupport.Layout(viewer); });
        result["firstUsefulViewportMilliseconds"] = watch.Elapsed.TotalMilliseconds;
        result["endToEndFirstUsefulViewportMilliseconds"] = overall.Elapsed.TotalMilliseconds;
        result["readinessScope"] = "Complete logical document and first useful hidden-HWND viewport; deferred visuals have not all been created.";
        result["logicalPieces"] = model.Pieces.Length; result["initialRealised"] = viewer.Realised.Count;
        result["completeText"] = model.Text.Contains(fixture.Last, StringComparison.Ordinal); result["sourcePreserved"] = model.Parsed.Source == fixture.Source;
        result["allocatedThroughFirstViewportBytes"] = GC.GetTotalAllocatedBytes() - allocated;
        HarnessSupport.Raster(viewer, Path.Combine(output, (repetition == 0 ? "cold" : "warm") + "-initial.png"));
        var target = fixture.Source.LastIndexOf(fixture.Last, StringComparison.Ordinal); watch.Restart();
        var navigation = viewer.NavigateAsync(target, true); HarnessSupport.Pump(navigation); HarnessSupport.Layout(viewer); input.Pulse();
        result["navigationMilliseconds"] = watch.Elapsed.TotalMilliseconds; result["targetVisible"] = navigation.Result;
        result["selectedTarget"] = viewer.Selection!.Copy().UnicodeText.Contains(fixture.Last, StringComparison.Ordinal);
        result["distantRealised"] = viewer.Realised.Count;
        HarnessSupport.Raster(viewer, Path.Combine(output, (repetition == 0 ? "cold" : "warm") + "-distant.png"));
        var scrollDelays = new List<double>();
        foreach (var ratio in new[] { 0d, 0.9, 0.25, 1, 0.5, 0 })
            scrollDelays.Add(input.Measure(() => { var scroll = viewer.Scroller!; scroll.ScrollToVerticalOffset(scroll.ScrollableHeight * ratio); HarnessSupport.Layout(viewer); }));
        result["scrollInputMilliseconds"] = scrollDelays;
        result["peakRealised"] = viewer.PeakRealised; result["totalMaterialisations"] = viewer.Materialisations;
        result["maximumMaterialisationMilliseconds"] = viewer.MaximumMaterialisationMilliseconds;
        result["maximumPieceLayoutMilliseconds"] = viewer.MaximumPieceLayoutMilliseconds;
        result["actualNativeItemContainers"] = VirtualDocumentView.Descendants<System.Windows.Controls.ListBoxItem>(viewer).Count();
        result["actualVisualTreeNodes"] = VirtualDocumentView.Descendants<System.Windows.DependencyObject>(viewer).Count();
        input.Dispose(); result["worstDispatcherInputMilliseconds"] = input.Worst;
        result["inputAcceptance"] = (double)result["firstLayoutInputMilliseconds"]! < 250 && input.Worst < 250 && scrollDelays.Max() < 250 ? "PASS" : "FAIL";
        result["allocatedBytes"] = GC.GetTotalAllocatedBytes() - allocated;
        if (!navigation.Result || !(bool)result["selectedTarget"]! || viewer.PeakRealised >= Math.Min(200, model.Pieces.Length))
            throw new IOException("Distant content or bounded real container count failed.");
        host.RootVisual = null; viewer.Dispose(); HarnessSupport.Idle();
    }
    private static void Baseline(ViewerFixture fixture, string output, Dictionary<string, object?> result, int repetition)
    {
        var allocated = GC.GetTotalAllocatedBytes(); var watch = Stopwatch.StartNew(); var overall = Stopwatch.StartNew();
        var parsed = new MarkdownService().Parse(fixture.Source);
        var diagrams = parsed.Syntax.Descendants<FencedCodeBlock>().Where(c => c.Info is "mermaid" or "mmd")
            .ToDictionary(c => c, c => new DiagramService().Render(c.Lines.ToString()));
        var media = StaWorker.RunAsync(() => (Images: new PreviewImageService().Prepare(parsed, Path.Combine(output, "synthetic.md"), output),
            Drawings: diagrams.Where(pair => pair.Value.Success).ToDictionary(pair => pair.Key, pair => new NativeSvgService().Render(pair.Value.Svg!))));
        HarnessSupport.Pump(media); result["preparationMilliseconds"] = watch.Elapsed.TotalMilliseconds;
        var task = new NativeMarkdownRenderer(new NativeSvgService()).RenderAsync(parsed, false, diagrams, _ => { }, (_, _) => { },
            preparedDrawings: media.Result.Drawings, preparedImages: media.Result.Images);
        HarnessSupport.Pump(task); var rendered = task.Result;
        result["logicalReadinessMilliseconds"] = watch.Elapsed.TotalMilliseconds;
        var viewer = new FlowDocumentScrollViewer { IsToolBarVisible = false, Background = Brushes.White };
        using var host = HarnessSupport.Host(viewer); HarnessSupport.Layout(viewer); HarnessSupport.Idle();
        watch.Restart(); using var input = new InputProbe();
        result["firstLayoutInputMilliseconds"] = input.Measure(() => { viewer.Document = rendered.Document; HarnessSupport.Layout(viewer); });
        result["firstUsefulViewportMilliseconds"] = watch.Elapsed.TotalMilliseconds;
        result["endToEndFirstUsefulViewportMilliseconds"] = overall.Elapsed.TotalMilliseconds;
        result["allocatedThroughFirstViewportBytes"] = GC.GetTotalAllocatedBytes() - allocated;
        result["completeText"] = rendered.ReadText().Contains(fixture.Last, StringComparison.Ordinal); result["sourcePreserved"] = parsed.Source == fixture.Source;
        result["logicalPieces"] = rendered.SourceElements.Count;
        HarnessSupport.Raster(viewer, Path.Combine(output, (repetition == 0 ? "cold" : "warm") + "-initial.png"));
        // Keep the existing deadline and retain partial observations before an unbounded background-formatting failure.
        File.WriteAllText(Path.Combine(output, (repetition == 0 ? "cold" : "warm") + "-first-layout.json"), JsonSerializer.Serialize(result));
        HarnessSupport.Idle(); watch.Restart();
        var last = rendered.SourceElements.MaxBy(p => p.Key).Value; viewer.Selection.Select(last.ContentStart, last.ContentEnd);
        var navigation = PreviewNavigationService.BringIntoViewAsync(viewer, last.ContentStart); HarnessSupport.Pump(navigation);
        HarnessSupport.Layout(viewer); input.Pulse();
        var rect = last.ContentStart.GetCharacterRect(System.Windows.Documents.LogicalDirection.Forward);
        result["navigationMilliseconds"] = watch.Elapsed.TotalMilliseconds; result["targetVisible"] = !rect.IsEmpty && rect.Top >= 0 && rect.Top < viewer.ActualHeight;
        result["selectedTarget"] = viewer.Selection.Text.Contains(fixture.Last, StringComparison.Ordinal);
        input.Dispose(); result["worstDispatcherInputMilliseconds"] = input.Worst;
        result["inputAcceptance"] = (double)result["firstLayoutInputMilliseconds"]! < 250 && input.Worst < 250 ? "PASS" : "FAIL";
        result["allocatedBytes"] = GC.GetTotalAllocatedBytes() - allocated;
        host.RootVisual = null; viewer.Document = null;
    }
}
