using Mermaider;
using Mermaider.Models;

namespace LensDocsStudio.Core.Rendering;

public sealed record FlowchartGroupIdentity(string Id, string? ParentId, IReadOnlyList<string> NodeIds);

public enum DiagramFailureKind { None, Syntax, UnsupportedSyntax, ResourceLimit, Deadline, Layout, Svg, NativeConversion }

public sealed record DiagramResult(string Source, string? Svg, string? Error)
{
    public bool Success => Svg is not null;
    public DiagramFailureKind FailureKind { get; init; }
    public bool CanRetry => FailureKind is DiagramFailureKind.Deadline or DiagramFailureKind.Layout or DiagramFailureKind.Svg or DiagramFailureKind.NativeConversion;
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
    // The pinned ER router still allocates heavily after bounded crossing search.
    // Shown-WPF acceptance measured 4.06 s; retain a bounded ER allowance (see Goal #52 receipt).
    internal static TimeSpan RenderingBudget(string source) => TimeSpan.FromSeconds(
        SourceAuthoring.ChartSource.Header(source).Equals("erDiagram", StringComparison.OrdinalIgnoreCase) ? 6 : 3);
    private readonly Func<string, string, CancellationToken, DiagramResult> renderer;
    public DiagramService() : this(RenderUncached) { }
    internal DiagramService(Func<string, string, CancellationToken, DiagramResult> renderer) => this.renderer = renderer;
    private readonly object gate = new();
    private readonly Dictionary<(string Source, string Theme), DiagramResult> cache = new();
    private readonly Queue<(string Source, string Theme)> order = new();
    private int cachedCharacters;
    public DiagramResult Render(string source, bool dark = false, string theme = "Auto", CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resolvedTheme = DiagramThemes.ResolveName(theme, dark);
        if (source.Length > 100_000) return new(source, null, "The diagram exceeds the 100,000-character rendering limit.") { FailureKind = DiagramFailureKind.ResourceLimit };
        lock (gate) if (cache.TryGetValue((source, resolvedTheme), out var existing)) return existing;
        if (SourceAuthoring.ExpansionViSource.ResourceProblem(source) is { } expansionProblem) return new(source, null, expansionProblem) { FailureKind = DiagramFailureKind.ResourceLimit };
        if (SourceAuthoring.TopologySource.ResourceProblem(source) is { } topologyProblem) return new(source, null, topologyProblem) { FailureKind = DiagramFailureKind.ResourceLimit };
        if (SourceAuthoring.ChartSource.Header(source).Equals("radar-beta", StringComparison.OrdinalIgnoreCase))
        {
            foreach (System.Text.RegularExpressions.Match ticks in SourceAuthoring.SourceText.Pattern(@"(?m)^[ \t]*ticks[ \t]+(?<count>[0-9]+)[ \t]*\r?$").Matches(source))
                if (!int.TryParse(ticks.Groups["count"].Value, out var count) || count > 100)
                    return new(source, null, "Radar tick controls exceed the supported 100-control resource budget. Correct the original source.") { FailureKind = DiagramFailureKind.ResourceLimit };
        }
        var result = renderer(source, resolvedTheme, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var cost = source.Length + (result.Svg?.Length ?? 0);
        // Only successful output and deterministic authoring failures are reusable.
        // A deadline or converter failure must permit an explicit unchanged-source retry.
        if ((result.Success || result.FailureKind is DiagramFailureKind.Syntax or DiagramFailureKind.UnsupportedSyntax) && cost <= 4_000_000)
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
        var deadline = RenderingBudget(source);
        using var diagnosticPhase = MermaidDiagnostics.Measure("renderer.pipeline");
        IDisposable? parsingPhase = null, svgPhase = null;
        if (!SourceAuthoring.ChartSource.IsChart(source) && !SourceAuthoring.WorkflowSource.IsWorkflow(source) && !SourceAuthoring.TopologySource.IsTopology(source) && !SourceAuthoring.ExpansionViSource.IsFamily(source) && System.Text.RegularExpressions.Regex.IsMatch(source, @"(?m)(?:-->|---|==>|-\.->|-->>|->>)\s*(?:%%[^\r\n]*)?$"))
            return new(source, null, "An arrow is missing its destination. Complete the connection and try again.") { FailureKind = DiagramFailureKind.Syntax };
        try
        {
            var colours = DiagramThemes.Colours(theme);
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            budget.CancelAfter(deadline);
            var layout = new MermaidEntityLayoutProvider(MermaidRenderer.LayoutProvider, budget.Token);
            var options = new RenderOptions
            {
                Bg = colours.Background, Fg = colours.Foreground, Accent = colours.Accent,
                Surface = colours.Surface, Border = colours.Border, Line = colours.Line,
                Font = "Segoe UI",
                MonoFont = "Consolas",
                LayoutProvider = layout,
                Limits = new ResourceLimits { MaxInputLength = 100_000, MaxElements = 500, RenderDeadline = deadline }
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
            return new(source, null, "Rendering resource limit reached: " + ex.Message[..Math.Min(ex.Message.Length, 300)]) { FailureKind = DiagramFailureKind.ResourceLimit };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("MaxNodesAfterLayout", StringComparison.Ordinal))
        {
            return new(source, null, "Rendering resource limit reached: " + ex.Message[..Math.Min(ex.Message.Length, 300)]) { FailureKind = DiagramFailureKind.ResourceLimit };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(source, null, $"Rendering resource deadline reached ({deadline.TotalSeconds:0} seconds). Retry the preview or reduce the diagram size.") { FailureKind = DiagramFailureKind.Deadline };
        }
        catch (MermaidParseException ex) when (ex.Message.Contains("not yet supported", StringComparison.OrdinalIgnoreCase))
        {
            return new(source, null, "Unsupported diagram syntax: " + ex.Message[..Math.Min(ex.Message.Length, 300)]) { FailureKind = DiagramFailureKind.UnsupportedSyntax };
        }
        catch (MermaidParseException ex)
        {
            return new(source, null, "Diagram syntax could not be parsed: " + ex.Message[..Math.Min(ex.Message.Length, 300)]) { FailureKind = DiagramFailureKind.Syntax };
        }
        catch (NotSupportedException ex)
        {
            return new(source, null, "Unsupported diagram syntax: " + ex.Message[..Math.Min(ex.Message.Length, 300)]) { FailureKind = DiagramFailureKind.UnsupportedSyntax };
        }
        catch (MermaidSvgException ex)
        {
            return new(source, null, "Diagram SVG generation failed: " + ex.Message[..Math.Min(ex.Message.Length, 300)]) { FailureKind = DiagramFailureKind.Svg };
        }
        catch (Exception ex) when (ex is not OutOfMemoryException && ex is not OperationCanceledException)
        {
            return new(source, null, "Diagram could not be rendered: " + ex.Message[..Math.Min(ex.Message.Length, 300)]) { FailureKind = DiagramFailureKind.Layout };
        }
        finally { svgPhase?.Dispose(); parsingPhase?.Dispose(); }
    }
}
