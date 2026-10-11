using Mermaider;
using Mermaider.Models;

namespace LensDocsStudio.Core.Rendering;

public sealed record DiagramResult(string Source, string? Svg, string? Error)
{
    public bool Success => Svg is not null;
}

public sealed class DiagramService
{
    private readonly object gate = new();
    private readonly Dictionary<(string Source, bool Dark), DiagramResult> cache = new();
    private readonly Queue<(string Source, bool Dark)> order = new();
    private int cachedCharacters;
    public DiagramResult Render(string source, bool dark = false)
    {
        if (source.Length > 100_000) return new(source, null, "The diagram exceeds the 100,000-character rendering limit.");
        lock (gate) if (cache.TryGetValue((source, dark), out var existing)) return existing;
        var result = RenderUncached(source, dark);
        var cost = source.Length + (result.Svg?.Length ?? 0);
        if (cost <= 4_000_000)
        {
            lock (gate)
            {
                if (cache.ContainsKey((source, dark))) return cache[(source, dark)];
                while (order.Count > 0 && (cache.Count >= 64 || cachedCharacters + cost > 4_000_000))
                {
                    var oldest = order.Dequeue(); var removed = cache[oldest]; cache.Remove(oldest);
                    cachedCharacters -= removed.Source.Length + (removed.Svg?.Length ?? 0);
                }
                cache.Add((source, dark), result); order.Enqueue((source, dark)); cachedCharacters += cost;
            }
        }
        return result;
    }
    private static DiagramResult RenderUncached(string source, bool dark)
    {
        if (System.Text.RegularExpressions.Regex.IsMatch(source, @"(?m)(?:-->|---|==>|-\.->|-->>|->>)\s*(?:%%[^\r\n]*)?$"))
            return new(source, null, "An arrow is missing its destination. Complete the connection and try again.");
        try
        {
            var svg = MermaidRenderer.RenderSvg(source, new RenderOptions
            {
                Bg = dark ? "#202124" : "#FAFAFA",
                Fg = dark ? "#F3F4F6" : "#202124",
                Accent = "#FF883E",
                Font = "Segoe UI",
                MonoFont = "Consolas",
                Limits = new ResourceLimits { MaxInputLength = 100_000, MaxElements = 500, RenderDeadline = TimeSpan.FromSeconds(3) }
            });
            return new(source, svg, null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new(source, null, "Diagram could not be rendered: " + ex.Message[..Math.Min(ex.Message.Length, 300)]);
        }
    }
}
