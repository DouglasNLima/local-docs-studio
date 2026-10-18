using System.Reflection;
using System.Text.Json;
using System.Windows.Media;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

internal static class NativeSuiteCacheChecks
{
    public static void Run(string output)
    {
        Directory.CreateDirectory(output); var checks = new List<string>();
        var method = typeof(NativeSvgService).GetMethod("RenderCanonical", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var render = new DiagramService().Render("flowchart TD\nA[Context] --> B[Result]\n");
        var safe = new NativeSvgService().Prepare(render.Svg!);
        DrawingImage Convert(DiagramResult value, string prepared, bool identities = false, CancellationToken token = default)
        {
            try { return (DrawingImage)method.Invoke(new NativeSvgService(), [value, prepared, identities, token])!; }
            catch (TargetInvocationException error) when (error.InnerException is OperationCanceledException) { throw error.InnerException; }
        }
        void Check(bool condition, string message) { if (!condition) throw new IOException(message); checks.Add(message); }
        var first = Convert(render, safe); var second = Convert(render, safe);
        Check(first.IsFrozen && ReferenceEquals(first, second), "Separate editor services reuse the exact frozen canonical drawing");
        var worker = StaWorker.RunAsync(() => Convert(render, safe)).GetAwaiter().GetResult();
        Check(ReferenceEquals(first, worker) && worker.Drawing.IsFrozen, "Frozen drawing is shared safely across STA threads");
        Check(!ReferenceEquals(first, Convert(render with { Source = render.Source + "%% distinct source\n" }, safe)), "Exact source, including comments, partitions cache identity");
        Check(!ReferenceEquals(first, Convert(render with { Theme = "different theme identity" }, safe)), "Theme partitions cache identity even when prepared pixels coincide");
        Check(!ReferenceEquals(first, Convert(render, safe, true)), "Runtime identity conversion options partition cache identity");
        var other = new DiagramService().Render(render.Source, true);
        Check(!ReferenceEquals(first, Convert(other, new NativeSvgService().Prepare(other.Svg!))), "Dark prepared SVG never reuses a light drawing");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel(); var rejected = false;
        try { _ = Convert(render, safe, token: cancelled.Token); } catch (OperationCanceledException) { rejected = true; }
        Check(rejected, "Cancellation is honoured before a cache hit");
        var gate = (SemaphoreSlim)typeof(NativeSvgService).GetField("canonicalConversion", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        using var queued = new CancellationTokenSource(); gate.Wait();
        try
        {
            var waiting = StaWorker.RunAsync(() => Convert(render with { Source = render.Source + "%% queued\n" }, safe, token: queued.Token));
            queued.Cancel(); rejected = false;
            try { waiting.GetAwaiter().GetResult(); } catch (OperationCanceledException) { rejected = true; }
            Check(rejected, "Obsolete queued conversion cancels while conversion capacity is occupied");
        }
        finally { gate.Release(); }
        var baselineBytes = NativeSvgService.ToPng(first, 1);
        for (var i = 0; i < 24; i++) _ = Convert(render with { Source = render.Source + "%% bounded entry " + i }, safe);
        var cache = (System.Collections.IDictionary)typeof(NativeSvgService).GetField("canonicalDrawings", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var characters = (int)typeof(NativeSvgService).GetField("canonicalCharacters", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        Check(cache.Count <= 8 && characters <= 2_000_000, "Repeated editor conversions retain bounded entry and character inventories");
        var rebuilt = Convert(render, safe);
        Check(!ReferenceEquals(first, rebuilt) && NativeSvgService.ToPng(rebuilt, 1).SequenceEqual(baselineBytes), "Eviction reconverts faithfully without changing canonical pixels");
        var unsafeRejected = false;
        try { _ = new NativeSvgService().Prepare(render.Svg!.Replace("</svg>", "<script>alert(1)</script></svg>", StringComparison.Ordinal)); }
        catch (InvalidDataException) { unsafeRejected = true; }
        Check(unsafeRejected, "Active SVG is still rejected before conversion or cache lookup");
        File.WriteAllText(Path.Combine(output, "suite-cache.json"), JsonSerializer.Serialize(new { Status = "PASS", Checks = checks, CacheEntries = cache.Count, CachedCharacters = characters,
            Limitation = "An in-flight SharpVectors call completes; cancellation prevents cache insertion/result installation. Queued conversion waits are cancellable. No thread aborts or renderer budget changes." }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
