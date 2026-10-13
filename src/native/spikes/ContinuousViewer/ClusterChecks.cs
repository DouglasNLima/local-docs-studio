using System.Diagnostics;
using System.Text.Json;
using ContinuousViewer.Model;
using LensDocsStudio.Core.Parsing;

namespace ContinuousViewer;

internal static class ClusterChecks
{
    public static void Run(string output)
    {
        Directory.CreateDirectory(output);
        // One valid extended grapheme: no malformed UTF-16, artificial input truncation or omitted source.
        var source = "a" + new string('\u0301', 65536) + "\n\n## Distant cluster target\n\nComplete final text.";
        var model = LogicalDocument.Build(new MarkdownService().Parse(source));
        var filename = Path.Combine(output, "cluster.json");
        var observations = new Dictionary<string, object?>
        {
            ["sourceCharacters"] = source.Length, ["logicalPieces"] = model.Pieces.Length,
            ["maximumPieceCharacters"] = model.Pieces.Max(p => p.Length), ["declaredPieceBudget"] = LogicalDocument.PieceCharacters,
            ["sourcePreserved"] = model.Parsed.Source == source, ["stage"] = "BEFORE native layout", ["completed"] = false,
            ["scope"] = "Isolated hidden HWND. Oversized indivisible combining sequence; unchanged 250 ms input budget and 60-second deadline."
        };
        Save();
        using var stop = new ManualResetEventSlim();
        var watchdog = new Thread(() =>
        {
            if (stop.Wait(TimeSpan.FromSeconds(60))) return;
            File.WriteAllText(Path.Combine(output, "deadline.json"), JsonSerializer.Serialize(new
            { inputAcceptance = "FAIL", reason = "Native layout did not return within 60 seconds.", completed = false }));
            Process.GetCurrentProcess().Kill(); // Only this owned diagnostic process.
        }) { IsBackground = true }; watchdog.Start();
        try
        {
            using var viewer = new VirtualDocumentView(); using var host = HarnessSupport.Host(viewer);
            HarnessSupport.Layout(viewer); HarnessSupport.Idle(); using var input = new InputProbe();
            observations["firstLayoutInputMilliseconds"] = input.Measure(() => { viewer.SetDocument(model); HarnessSupport.Layout(viewer); });
            input.Dispose(); observations["worstInputMilliseconds"] = input.Worst;
            observations["inputAcceptance"] = (double)observations["firstLayoutInputMilliseconds"]! < 250 && input.Worst < 250 ? "PASS" : "FAIL";
            observations["completed"] = true; observations["stage"] = "AFTER native layout";
            observations["maximumPieceLayoutMilliseconds"] = viewer.MaximumPieceLayoutMilliseconds;
            observations["logicalTextPreserved"] = viewer.Document!.Text.StartsWith("a" + new string('\u0301', 65536), StringComparison.Ordinal);
            Save();
            HarnessSupport.Raster(viewer, Path.Combine(output, "cluster.png"));
        }
        catch (Exception error) { observations["error"] = error.ToString(); observations["inputAcceptance"] = "FAIL"; Save(); throw; }
        finally { stop.Set(); watchdog.Join(); }
        void Save() => File.WriteAllText(filename, JsonSerializer.Serialize(observations, new JsonSerializerOptions { WriteIndented = true }));
    }
}
