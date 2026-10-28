using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Xml.Linq;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

internal static class NativeComplexErChecks
{
    public static void Run(string output, string root)
    {
        Directory.CreateDirectory(output);
        using var recorder = new MermaidPhaseRecorder();
        var rows = new List<object>(); var failures = new List<string>(); var validated = false;
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/native-complex-er/manifest.json")));
        try
        {
            foreach (var fixture in manifest.RootElement.EnumerateArray())
            {
                var name = fixture.GetProperty("name").GetString()!;
                var source = File.ReadAllText(Path.Combine(root, "tests/fixtures/native-complex-er", name + ".mmd"));
                var service = new DiagramService();
                for (var run = 0; run < 4; run++)
                {
                    if (run == 2) service = new();
                    using var scenario = MermaidDiagnostics.Measure($"scenario.{name}.{run}");
                    using var process = Process.GetCurrentProcess(); var cpu = process.TotalProcessorTime;
                    var allocated = GC.GetTotalAllocatedBytes(); var watch = Stopwatch.StartNew();
                    var result = service.Render(source); watch.Stop(); process.Refresh();
                    rows.Add(new { name, run, Cache = run % 2 == 1, result.Success, result.FailureKind, result.Error,
                        Milliseconds = watch.Elapsed.TotalMilliseconds, CpuMilliseconds = (process.TotalProcessorTime - cpu).TotalMilliseconds,
                        AllocatedBytes = GC.GetTotalAllocatedBytes() - allocated, process.PeakWorkingSet64, process.PrivateMemorySize64 });
                    if (!result.Success) { failures.Add(name + ": " + result.Error); continue; }
                    var entities = fixture.GetProperty("entities").GetInt32(); var edges = fixture.GetProperty("relationships").GetInt32(); var attributes = fixture.GetProperty("attributes").GetInt32();
                    if (result.Source != source || result.ErLayout?.Entities.Count != entities || result.ErLayout.Relationships.Count != edges
                        || result.ErLayout.Entities.Sum(x => x.Attributes.Count) != attributes) throw new IOException("Complex ER geometry/source count mismatch: " + name);
                    if (run != 0) continue;
                    var xml = XDocument.Parse(result.Svg!);
                    if (xml.Descendants().Count(x => (string?)x.Attribute("class") == "entity") != entities
                        || xml.Descendants().Count(x => (string?)x.Attribute("class") == "er-relationship") != edges)
                        throw new IOException("Complex ER SVG omitted an occurrence: " + name);
                    var svg = new NativeSvgService(); var drawing = svg.Render(result.Svg!);
                    if (!drawing.IsFrozen || drawing.Width <= 0 || drawing.Height <= 0) throw new IOException("Complex ER native drawing is invalid.");
                    // Export the complete canvas inside the existing pixel budget, without clipping or truncation.
                    var scale = Math.Min(1, Math.Min(8000 / drawing.Width, Math.Min(8000 / drawing.Height, Math.Sqrt(16_000_000 / (drawing.Width * drawing.Height)))));
                    var png = NativeSvgService.ToPng(drawing, scale);
                    File.WriteAllBytes(Path.Combine(output, name + ".png"), png);
                    File.WriteAllText(Path.Combine(output, name + ".svg"), svg.Prepare(result.Svg!));
                    var inspection = DiagramInspection.Create(result, svg);
                    if (inspection?.Entities.Count != entities || inspection.Connections.Count != edges) throw new IOException("Complex ER inspection correspondence is incomplete: " + name);
                    if (inspection.Connections.Select(connection => connection.Identity).Distinct().Count() != edges)
                        throw new IOException("Complex ER inspection merged relationship occurrences: " + name);
                    if (!png.AsSpan().SequenceEqual(NativeSvgService.ToPng(inspection.Drawing, scale))) throw new IOException("Inspection changed exported pixels: " + name);
                    rows.Add(new { name, NativeConversion = true, CompletePng = true, SvgEntities = entities, SvgRelationships = edges,
                        Attributes = attributes, InspectionEntities = inspection.Entities.Count, InspectionRelationships = inspection.Connections.Count,
                        drawing.Width, drawing.Height, ExportScale = scale, PngSha256 = Convert.ToHexString(SHA256.HashData(png)), SourceSha256 = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(source))) });
                }
            }
            validated = true;
        }
        finally
        {
            recorder.Write(Path.Combine(output, "phases.json"));
            File.WriteAllText(Path.Combine(output, "complex-er.json"), JsonSerializer.Serialize(new { Status = validated && failures.Count == 0 ? "PASS" : "FAIL", Provenance = "Deterministic synthetic fixtures; not the original SPC sources", rows, failures }, new JsonSerializerOptions { WriteIndented = true }));
        }
        if (failures.Count != 0) throw new IOException(string.Join(Environment.NewLine, failures));
    }
}
