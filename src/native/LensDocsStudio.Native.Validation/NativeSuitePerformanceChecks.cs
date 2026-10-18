using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using LensDocsStudio.Core.ClassDiagrams;
using LensDocsStudio.Core.ErDiagrams;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.Sequence;
using LensDocsStudio.Core.StateDiagrams;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

/// <summary>The same binary can run against baseline/candidate Native and Core assemblies.
/// No windows, desktop input, clipboard or UIA transport is involved.</summary>
internal static class NativeSuitePerformanceChecks
{
    public static void Run(string output)
    {
        Directory.CreateDirectory(output); var observations = new List<object>();
        var conversion = typeof(NativeSvgService).GetMethod("RenderPrepared", BindingFlags.Static | BindingFlags.NonPublic,
            [typeof(string), typeof(bool)]) ?? throw new MissingMethodException("The established prepared conversion boundary was removed.");
        foreach (var family in new[] { "Flowchart", "Sequence", "State", "ER", "Class" })
        foreach (var large in new[] { false, true })
        {
            var source = Source(family, large); var diagramService = new DiagramService();
            var runs = new List<object>();
            for (var sample = 0; sample < 7; sample++)
            {
                using var process = Process.GetCurrentProcess(); process.Refresh(); var workingSet = process.WorkingSet64;
                var totalAllocated = GC.GetTotalAllocatedBytes(); var allocated = GC.GetAllocatedBytesForCurrentThread(); var timer = Stopwatch.StartNew();
                object model = family switch
                {
                    "Flowchart" => new MermaidSourceParser().Parse(source), "Sequence" => new SequenceSourceParser().Parse(source),
                    "State" => new StateDiagramSourceParser().Parse(source), "ER" => new ErDiagramSourceParser().Parse(source),
                    _ => new ClassDiagramSourceParser().Parse(source)
                };
                var scanning = timer.Elapsed.TotalMilliseconds;
                timer.Restart(); var render = diagramService.Render(source); var renderMs = timer.Elapsed.TotalMilliseconds;
                if (!render.Success) throw new IOException(family + ": required fixture failed: " + render.Error);
                var svg = new NativeSvgService(); timer.Restart(); var safe = svg.Prepare(render.Svg!); var sanitisation = timer.Elapsed.TotalMilliseconds;
                // Standalone conversion isolates SharpVectors and WPF drawing creation together.
                // It is not added to presentation time, which includes its own conversion/correspondence.
                timer.Restart(); var drawing = (DrawingImage)conversion.Invoke(null, [safe, false])!; var convertMs = timer.Elapsed.TotalMilliseconds;
                timer.Restart();
                DrawingImage actual = family switch
                {
                    "Flowchart" => NativeMermaidPresentation.Create(render, svg).Drawing,
                    "Sequence" => NativeSequencePresentation.Create((SequenceDiagramModel)model, render, svg).Drawing,
                    "State" => NativeStatePresentation.Create((StateDiagramModel)model, render, svg).Canonical.Drawing,
                    "ER" => NativeErPresentation.Create((ErDiagramModel)model, render, svg).Drawing,
                    _ => NativeClassPresentation.Create((ClassDiagramModel)model, render, svg).Drawing
                };
                var presentationMs = timer.Elapsed.TotalMilliseconds;
                if (!actual.IsFrozen || actual.Width <= 0 || !drawing.IsFrozen) throw new IOException("Unusable canonical drawing.");
                var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated; var allThreadBytes = GC.GetTotalAllocatedBytes() - totalAllocated;
                // Pixel comparison outside measured presentation, on every sample, preserves actual work.
                if (!NativeSvgService.ToPng(actual, .15).SequenceEqual(NativeSvgService.ToPng(drawing, .15)))
                    throw new IOException(family + ": canonical pixels changed.");
                process.Refresh();
                runs.Add(new { Sample = sample, Execution = sample == 0 ? "First fixture execution" : "Repeated same-source/new-editor-service execution",
                    SourceScanIncludingAdapterSemanticsMs = scanning, RendererIncludingParsingLayoutSvgMs = renderMs,
                    StandaloneSanitisationMs = sanitisation, StandaloneSharpVectorsAndDrawingMs = convertMs,
                    CompletePresentationMs = presentationMs, CurrentThreadAllocatedBytesBeforePixelCheck = bytes, AllThreadsAllocatedBytesBeforePixelCheck = allThreadBytes,
                    WorkingSetBefore = workingSet, WorkingSetAfter = process.WorkingSet64, CanonicalPixelsEqual = true });
                GC.KeepAlive(actual);
            }
            observations.Add(new { Family = family, Size = large ? "Larger" : "Smaller", SourceSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))),
                Source = source, Samples = runs });
        }
        var diagnostic = new DiagramService().Render(Source("ER", true, true));
        File.WriteAllText(Path.Combine(output, "suite-performance.json"), JsonSerializer.Serialize(new
        {
            NativeAssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(NativeSvgService).Assembly.Location))),
            HarnessSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(NativeSuitePerformanceChecks).Assembly.Location))),
            Host = new { Framework = RuntimeInformation.FrameworkDescription, OS = RuntimeInformation.OSDescription, Environment.ProcessorCount, Architecture = RuntimeInformation.ProcessArchitecture.ToString(), Stopwatch.Frequency },
            Method = "STA windowless diagnostic; seven samples per fixture; first execution and six repeats. Adapter scanning includes its existing semantic work. Renderer parsing/layout/SVG and native conversion/drawing are combined only where no independent public boundary exists. Presentation includes correspondence and conversion; standalone phases overlap it and must not be summed. No UIA, first-window or interaction latency claim. Pixel checks excluded from timing. No production GC changes.",
            Observations = observations, Er100Entities150Relationships = new { RenderSuccess = diagnostic.Success, diagnostic.Error, InteractionCompleted = false, Classification = "Renderer-only resource diagnostic; not a successful editor benchmark", LimitsUnchanged = true }
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
    internal static string Source(string family, bool large, bool diagnostic = false)
    {
        var n = large ? family switch { "Flowchart" => 100, "Sequence" => 20, "State" => 60, "ER" => diagnostic ? 100 : 30, _ => 100 } : family == "Flowchart" ? 20 : family == "Sequence" ? 5 : 10;
        var edges = large ? family switch { "Sequence" => 150, "ER" => diagnostic ? 150 : 60, "Class" => 150, _ => n + 10 } : family == "Sequence" ? 20 : 15;
        return family switch
        {
            "Flowchart" => "flowchart TD\n" + string.Concat(Enumerable.Range(0, n).Select(i => $"N{i}[\"Node {i} with context\"]\n")) + string.Concat(Enumerable.Range(0, edges).Select(i => $"N{i % n} --> N{(i * 7 + 1) % n}\n")),
            "Sequence" => "sequenceDiagram\n" + string.Concat(Enumerable.Range(0, n).Select(i => $"participant P{i} as Participant {i}\n")) + string.Concat(Enumerable.Range(0, edges).Select(i => $"P{i % n}->>P{(i + 1) % n}: Message {i} with context\n")),
            "State" => "stateDiagram-v2\n" + string.Concat(Enumerable.Range(0, n).Select(i => $"state \"State {i} with context\" as S{i}\n")) + string.Concat(Enumerable.Range(0, edges).Select(i => $"S{i % n} --> S{(i * 7 + 1) % n} : transition{i}\n")),
            "ER" => "erDiagram\n" + string.Concat(Enumerable.Range(0, n).Select(i => $"E{i} {{\n int id PK\n string description\n}}\n")) + string.Concat(Enumerable.Range(0, edges).Select(i => $"E{i % n} ||--o{{ E{(i * 7 + 1) % n} : relation{i}\n")),
            _ => "classDiagram\n" + string.Concat(Enumerable.Range(0, n).Select(i => $"class C{i} {{\n +string Description\n +Open(path, options) bool\n}}\n")) + string.Concat(Enumerable.Range(0, edges).Select(i => $"C{i % n} --> C{(i * 7 + 1) % n} : relation{i}\n"))
        };
    }
}
