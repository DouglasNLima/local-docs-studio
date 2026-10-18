using Mermaider;
using Mermaider.Models;

namespace LensDocsStudio.Core.Rendering;

public sealed record FlowchartGroupIdentity(string Id, string? ParentId, IReadOnlyList<string> NodeIds);

public sealed record DiagramResult(string Source, string? Svg, string? Error)
{
    public bool Success => Svg is not null;
    public string? Theme { get; init; }
    public PositionedGraph? FlowchartLayout { get; init; }
    public MermaidGraph? FlowchartSemantics { get; init; }
    public ErDiagram? ErSemantics { get; init; }
    public PositionedErDiagram? ErLayout { get; init; }
    public ClassDiagram? ClassSemantics { get; init; }
    public PositionedClassDiagram? ClassLayout { get; init; }
    public IReadOnlyList<FlowchartGroupIdentity> FlowchartGroups { get; init; } = [];
}

public sealed class DiagramService
{
    private readonly object gate = new();
    private readonly Dictionary<(string Source, string Theme), DiagramResult> cache = new();
    private readonly Queue<(string Source, string Theme)> order = new();
    private int cachedCharacters;
    public DiagramResult Render(string source, bool dark = false, string theme = "Auto", CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resolvedTheme = DiagramThemes.ResolveName(theme, dark);
        if (source.Length > 100_000) return new(source, null, "The diagram exceeds the 100,000-character rendering limit.");
        lock (gate) if (cache.TryGetValue((source, resolvedTheme), out var existing)) return existing;
        var result = RenderUncached(source, resolvedTheme, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var cost = source.Length + (result.Svg?.Length ?? 0);
        if (cost <= 4_000_000)
        {
            lock (gate)
            {
                if (cache.ContainsKey((source, resolvedTheme))) return cache[(source, resolvedTheme)];
                while (order.Count > 0 && (cache.Count >= 64 || cachedCharacters + cost > 4_000_000))
                {
                    var oldest = order.Dequeue(); var removed = cache[oldest]; cache.Remove(oldest);
                    cachedCharacters -= removed.Source.Length + (removed.Svg?.Length ?? 0);
                }
                cache.Add((source, resolvedTheme), result); order.Enqueue((source, resolvedTheme)); cachedCharacters += cost;
            }
        }
        return result;
    }
    private static DiagramResult RenderUncached(string source, string theme, CancellationToken cancellationToken)
    {
        if (System.Text.RegularExpressions.Regex.IsMatch(source, @"(?m)(?:-->|---|==>|-\.->|-->>|->>)\s*(?:%%[^\r\n]*)?$"))
            return new(source, null, "An arrow is missing its destination. Complete the connection and try again.");
        try
        {
            var colours = DiagramThemes.Colours(theme);
            var layout = new MermaidEntityLayoutProvider(MermaidRenderer.LayoutProvider);
            var options = new RenderOptions
            {
                Bg = colours.Background, Fg = colours.Foreground, Accent = colours.Accent,
                Surface = colours.Surface, Border = colours.Border, Line = colours.Line,
                Font = "Segoe UI",
                MonoFont = "Consolas",
                LayoutProvider = layout,
                Limits = new ResourceLimits { MaxInputLength = 100_000, MaxElements = 500, RenderDeadline = TimeSpan.FromSeconds(3) }
            };
            var prepared = SequenceActivationSource.Prepare(source);
            string svg;
            if (cancellationToken.CanBeCanceled)
            {
                using var output = new MemoryStream();
                MermaidRenderer.RenderSvgAsync(prepared, output, options, cancellationToken).GetAwaiter().GetResult();
                svg = System.Text.Encoding.UTF8.GetString(output.ToArray());
            }
            else svg = MermaidRenderer.RenderSvg(prepared, options);
            return new(source, svg, null) { Theme = theme, FlowchartLayout = layout.Flowchart, FlowchartSemantics = layout.FlowchartSemantics, FlowchartGroups = layout.Groups, ErSemantics = layout.ErSemantics, ErLayout = layout.ErLayout, ClassSemantics = layout.ClassSemantics, ClassLayout = layout.ClassLayout };
        }
        catch (Exception ex) when (ex is not OutOfMemoryException && (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested))
        {
            return new(source, null, "Diagram could not be rendered: " + ex.Message[..Math.Min(ex.Message.Length, 300)]);
        }
    }
}
