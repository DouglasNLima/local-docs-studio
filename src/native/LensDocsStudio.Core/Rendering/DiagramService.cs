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
        if (SourceAuthoring.TopologySource.ResourceProblem(source) is { } topologyProblem) return new(source, null, topologyProblem);
        if (SourceAuthoring.ChartSource.Header(source).Equals("radar-beta", StringComparison.OrdinalIgnoreCase))
        {
            foreach (System.Text.RegularExpressions.Match ticks in SourceAuthoring.SourceText.Pattern(@"(?m)^[ \t]*ticks[ \t]+(?<count>[0-9]+)[ \t]*\r?$").Matches(source))
                if (!int.TryParse(ticks.Groups["count"].Value, out var count) || count > 100)
                    return new(source, null, "Radar tick controls exceed the supported 100-control resource budget. Correct the original source.");
        }
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
        using var diagnosticPhase = MermaidDiagnostics.Measure("renderer.pipeline");
        IDisposable? parsingPhase = null, svgPhase = null;
        if (!SourceAuthoring.ChartSource.IsChart(source) && !SourceAuthoring.WorkflowSource.IsWorkflow(source) && !SourceAuthoring.TopologySource.IsTopology(source) && System.Text.RegularExpressions.Regex.IsMatch(source, @"(?m)(?:-->|---|==>|-\.->|-->>|->>)\s*(?:%%[^\r\n]*)?$"))
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
            var prepared = SequenceActivationSource.Prepare(SourceAuthoring.SourceText.PrepareRendererSource(source));
            if (MermaidDiagnostics.Activities.HasListeners() && !Sequence.SequenceSourceParser.IsSequence(source))
            {
                // The existing provider marks the actual parser/layout boundary. No second layout.
                parsingPhase = MermaidDiagnostics.Measure("renderer.parse-before-layout");
                layout.BeforeLayout = () => { parsingPhase?.Dispose(); parsingPhase = null; svgPhase?.Dispose(); svgPhase = null; };
                layout.AfterLayout = () => svgPhase = MermaidDiagnostics.Measure("renderer.svg-after-layout");
            }
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
        catch (MermaidResourceLimitException ex)
        {
            return new(source, null, "Rendering resource limit reached: " + ex.Message[..Math.Min(ex.Message.Length, 300)]);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(source, null, "Rendering resource deadline reached (3 seconds). Reduce the diagram size and validate again.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException && ex is not OperationCanceledException)
        {
            return new(source, null, "Diagram could not be rendered: " + ex.Message[..Math.Min(ex.Message.Length, 300)]);
        }
        finally { svgPhase?.Dispose(); parsingPhase?.Dispose(); }
    }
}
