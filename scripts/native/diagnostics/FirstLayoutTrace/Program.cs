using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Diagnostics.Tracing.Etlx;

if (args is ["--self-test"]) { Checks.Run(); return; }
if (args.Length != 2) throw new ArgumentException("Provide the retained .nettrace and a new report path.");
var input = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args[1]);
if (File.Exists(output) || File.Exists(Path.ChangeExtension(input, ".nettrace.etlx")))
    throw new IOException("Previous diagnostic evidence must not be replaced.");
if (new FileInfo(input).Length > 256 * 1024 * 1024) throw new IOException("Trace exceeds the 256 MiB analysis bound.");
var report = Reader.Read(input);
using var retainedTrace = File.OpenRead(input);
using var destination = new FileStream(output, FileMode.CreateNew, FileAccess.Write);
JsonSerializer.Serialize(destination, new
{
    Schema = 1,
    Status = "DIAGNOSTIC_ONLY",
    TraceSha256 = Convert.ToHexString(SHA256.HashData(retainedTrace)),
    Boundary = "Sample counts on the marker thread, not exclusive CPU milliseconds. Inclusive frames overlap. Unresolved/native frames and profiler overhead limit attribution. No acceptance verdict is inferred.",
    report.Markers,
    Phases = report.Phases.Select(p => new
    {
        p.Ordinal, p.Thread, p.StartMs, p.EndMs, DurationMs = p.EndMs - p.StartMs,
        p.Samples, p.UnresolvedSamples, p.TruncatedStacks,
        ExclusiveCategories = p.Categories,
        InclusiveFrames = p.Inclusive.OrderByDescending(x => x.Value).Take(80),
        LeafFrames = p.Leaves.OrderByDescending(x => x.Value).Take(40)
    })
}, new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine($"Analysed {report.Phases.Count} complete first-layout intervals; diagnostic samples only.");

static class Reader
{
    public static Attribution Read(string path)
    {
        var result = new Attribution();
        using var trace = new TraceLog(TraceLog.CreateFromEventPipeDataFile(path));
        if (trace.EventsLost > 0) throw new IOException("Trace events were lost; complete attribution is unavailable.");
        var events = 0;
        foreach (var e in trace.Events)
        {
            if (++events > 10_000_000) throw new IOException("Event count exceeds the analysis bound.");
            if (e.ProviderName is "LensDocsStudio-Native-ContinuousHarness" or "LensDocsStudio-Native-PreviewConstruction")
                result.Marker(e.PayloadValue(0)?.ToString() ?? e.EventName, e.TimeStampRelativeMSec, e.ThreadID);
            if (!e.ProviderName.Contains("SampleProfiler", StringComparison.Ordinal) || !result.IsCurrentThread(e.ThreadID)) continue;
            var names = new List<string>();
            var stack = e.CallStack();
            for (; stack is not null && names.Count < 128; stack = stack.Caller)
                names.Add(stack.CodeAddress.FullMethodName ?? "");
            result.Sample(e.ThreadID, names, stack is not null);
        }
        result.Complete();
        return result;
    }
}

sealed class Attribution
{
    private Phase? current;
    private int samples;
    public List<Marker> Markers { get; } = [];
    public List<Phase> Phases { get; } = [];
    public bool IsCurrentThread(int thread) => current?.Thread == thread;

    public void Marker(string name, double time, int thread)
    {
        if (name.Length > 160 || Markers.Count >= 100_000) throw new IOException("Marker bound exceeded.");
        Markers.Add(new(name, time, thread));
        if (name == "harness/first-layout-start")
        {
            if (current is not null) throw new IOException("Overlapping first-layout markers.");
            current = new() { Ordinal = Phases.Count, Thread = thread, StartMs = time };
            Phases.Add(current);
        }
        if (name == "harness/first-layout-end")
        {
            if (current is null || current.Thread != thread || time < current.StartMs)
                throw new IOException("Unpaired first-layout end marker.");
            current.EndMs = time;
            current = null;
        }
    }

    public void Sample(int thread, IReadOnlyList<string> names, bool truncated)
    {
        if (!IsCurrentThread(thread)) return;
        if (++samples > 200_000) throw new IOException("Sample count exceeds the analysis bound.");
        var p = current!;
        p.Samples++;
        if (names.Any(string.IsNullOrEmpty) || names.Count == 0) p.UnresolvedSamples++;
        if (truncated) p.TruncatedStacks++;
        foreach (var name in names.Where(n => n.Length > 0).Distinct()) Increment(p.Inclusive, name);
        if (names.Count > 0) Increment(p.Leaves, names[0].Length == 0 ? "[unresolved]" : names[0]);
        Increment(p.Categories, Category(names));
    }

    public static string Category(IReadOnlyList<string> names)
    {
        // Assign each sample once. Inclusive call-tree counts remain separate.
        if (names.Any(n => n.StartsWith("MS.Internal.Documents.FlowDocumentView.MeasureOverride(", StringComparison.Ordinal))) return "WPF document measure";
        if (names.Any(n => n.StartsWith("MS.Internal.Documents.FlowDocumentView.ArrangeOverride(", StringComparison.Ordinal))) return "WPF document arrange";
        if (names.Any(n => n.StartsWith("System.Windows.Controls.FlowDocumentScrollViewer.DocumentChanged(", StringComparison.Ordinal))) return "Viewer document attachment";
        return "Other or unresolved";
    }

    public void Complete()
    {
        if (current is not null || Phases.Count == 0 || Phases.Any(p => p.Samples == 0))
            throw new IOException("Incomplete markers or missing UI-thread samples; attribution is inconclusive.");
    }
    private static void Increment(Dictionary<string, int> counts, string name)
    {
        if (counts.Count >= 50_000 && !counts.ContainsKey(name)) throw new IOException("Frame cardinality exceeds the analysis bound.");
        counts[name] = counts.GetValueOrDefault(name) + 1;
    }
}

sealed record Marker(string Name, double TimeMs, int Thread);
sealed class Phase
{
    public int Ordinal, Thread, Samples, UnresolvedSamples, TruncatedStacks;
    public double StartMs, EndMs;
    public Dictionary<string, int> Inclusive = new(), Leaves = new(), Categories = new();
}

static class Checks
{
    public static void Run()
    {
        var a = new Attribution();
        a.Sample(7, ["outside"], false);
        a.Marker("harness/first-layout-start", 10, 7);
        a.Sample(8, ["wrong thread"], false);
        a.Sample(7, ["leaf", "leaf", "MS.Internal.Documents.FlowDocumentView.MeasureOverride(Size)"], false);
        a.Sample(7, ["", "MS.Internal.Documents.FlowDocumentView.ArrangeOverride(Size)"], true);
        a.Marker("harness/first-layout-end", 20, 7);
        a.Sample(7, ["outside"], false);
        a.Complete();
        var p = a.Phases.Single();
        Require(p.Samples == 2 && p.Inclusive["leaf"] == 1 && p.Categories.Values.Sum() == 2, "Thread, boundary and recursive-frame accounting");
        Require(p.UnresolvedSamples == 1 && p.TruncatedStacks == 1, "Uncertainty accounting");
        Require(Attribution.Category(["System.Windows.Controls.FlowDocumentScrollViewer.DocumentChanged(x)", "System.Windows.Controls.FlowDocumentScrollViewer.AttachTextEditor()"]) == "Viewer document attachment", "Observed WPF attachment callback attribution");
        Reject(() => new Attribution().Complete());
        Reject(() => new Attribution().Marker("harness/first-layout-end", 10, 7));
        Reject(() => { var b = new Attribution(); b.Marker("harness/first-layout-start", 10, 7); b.Complete(); });
        Reject(() => { var b = new Attribution(); b.Marker("harness/first-layout-start", 10, 7); b.Marker("harness/first-layout-end", 20, 8); });
        Reject(() => { var b = new Attribution(); b.Marker("harness/first-layout-start", 10, 7); b.Marker("harness/first-layout-start", 11, 7); });
        Reject(() => { var b = new Attribution(); b.Marker("harness/first-layout-start", 10, 7); b.Marker("harness/first-layout-end", 9, 7); });
        Console.WriteLine("PASS: 9 attribution controls; missing/overlapping/wrong-thread evidence fails closed.");
    }
    private static void Require(bool condition, string name) { if (!condition) throw new IOException(name); }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (IOException) { return; }
        throw new IOException("Invalid attribution was accepted.");
    }
}
