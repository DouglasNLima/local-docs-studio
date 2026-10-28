using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.Rendering.PinnedMermaider;
using Mermaider;
using Mermaider.Layout;
using Mermaider.Models;

namespace LensDocsStudio.Core.Arrangement;

/// <summary>Arrangement never enters DiagramService's canonical cache. The pinned parser and SVG generator remain the only renderer.</summary>
public sealed class ArrangementEngine
{
    private readonly DiagramResult canonical;
    private readonly IReadOnlyDictionary<string, ArrangementBox> original;
    private readonly (string From, string To, string Label)[] edges;
    private readonly bool er;
    public ArrangementSnapshot Canonical { get; }
    public IReadOnlyDictionary<string, ArrangementBox> Boxes => original;

    public ArrangementEngine(DiagramResult canonical)
    {
        if (!canonical.Success || canonical.Source.Length > 100_000 || canonical.Svg!.Length > 8_000_000) throw new InvalidDataException("A successful bounded source-owned render is required before arranging.");
        this.canonical = canonical; er = canonical.ErLayout is not null;
        if (er)
        {
            original = canonical.ErLayout!.Entities.ToDictionary(n => n.Id, n => new ArrangementBox(n.X, n.Y, n.Width, n.Height), StringComparer.Ordinal);
            edges = canonical.ErLayout.Relationships.Select(e => (e.Entity1, e.Entity2, e.Label)).ToArray();
        }
        else if (canonical.FlowchartLayout is { } graph)
        {
            if (graph.Notes.Count != 0 || graph.Nodes.Any(n => n.Shape is NodeShape.Asymmetric or NodeShape.Parallelogram or NodeShape.ParallelogramAlt))
                throw new InvalidDataException("Arrangement is unavailable for notes and asymmetric port shapes. Canonical viewing and source editing remain available.");
            original = graph.Nodes.ToDictionary(n => n.Id, n => new ArrangementBox(n.X, n.Y, n.Width, n.Height), StringComparer.Ordinal);
            edges = graph.Edges.Select(e => (e.Source, e.Target, e.Label ?? "")).ToArray();
        }
        else throw new InvalidDataException("Arrangement currently supports ER and Flowchart diagrams.");
        if (original.Count is 0 or > ArrangementState.MaximumNodes || edges.Length > 1000 || edges.Any(e => !original.ContainsKey(e.From) || !original.ContainsKey(e.To)))
            throw new InvalidDataException("This diagram exceeds the arrangement budget or contains compound group connections. Use canonical rendering.");
        var state = new ArrangementState(original.Select(pair => new KeyValuePair<string, ArrangementPosition>(pair.Key, new(pair.Value.X, pair.Value.Y))), ArrangementStrategy.Canonical);
        using var measurementBudget = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        Canonical = new(canonical, state, Measure(canonical, measurementBudget.Token));
    }

    public ArrangementSnapshot Arrange(ArrangementState state, CancellationToken token = default)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(token); budget.CancelAfter(TimeSpan.FromSeconds(6));
        return ArrangeCore(state, budget.Token);
    }
    public ArrangementSnapshot Repaint(ArrangementSnapshot snapshot, CancellationToken token = default)
    {
        if (snapshot.Render.Source != canonical.Source) throw new InvalidDataException("The arrangement belongs to another source revision.");
        if (snapshot.Render.Theme == canonical.Theme) return snapshot;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(token); budget.CancelAfter(TimeSpan.FromSeconds(6));
        return Finish(snapshot.Render with { Theme = canonical.Theme }, snapshot.State, budget.Token);
    }

    private ArrangementSnapshot ArrangeCore(ArrangementState state, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (state.Positions.Count != original.Count || state.Positions.Keys.Any(id => !original.ContainsKey(id))) throw new InvalidDataException("The saved node identities do not match this diagram.");
        if (state.LayoutVariant != 0)
        {
            var alternative = Alternative(state.LayoutVariant - 1, token);
            if (!state.Positions.All(pair => alternative.State.Positions.TryGetValue(pair.Key, out var p) && p.X == pair.Value.X && p.Y == pair.Value.Y))
                throw new InvalidDataException("The saved routing variant no longer matches its positions. Explicitly re-associate the positions as a manual layout.");
            return new(alternative.Render, state, alternative.Quality);
        }
        if (state.Positions.All(pair => pair.Value.X == original[pair.Key].X && pair.Value.Y == original[pair.Key].Y)) return new(canonical, state, Canonical.Quality);
        var boxes = state.Positions.ToDictionary(pair => pair.Key, pair => original[pair.Key] with { X = pair.Value.X, Y = pair.Value.Y }, StringComparer.Ordinal);
        var nodes = boxes.ToArray();
        foreach (var node in nodes)
        {
            if (node.Value.X < 2 || node.Value.Y < 2 || node.Value.Right > ArrangementState.MaximumCoordinate || node.Value.Bottom > ArrangementState.MaximumCoordinate)
                throw new InvalidDataException("Keep the complete node inside the arrangement canvas limits.");
            foreach (var other in nodes) if (StringComparer.Ordinal.Compare(node.Key, other.Key) < 0 && node.Value.Intersects(other.Value, 4))
                throw new InvalidDataException("Nodes cannot overlap. The previous arrangement is retained.");
        }
        ValidateGroups(boxes);
        var routes = new List<IReadOnlyList<Point>>(); var labels = new List<Point?>(); var labelBoxes = new List<ArrangementBox>();
        var parallel = new Dictionary<(string, string), int>();
        for (var i = 0; i < edges.Length; i++)
        {
            token.ThrowIfCancellationRequested(); var edge = edges[i]; var pair = (edge.From, edge.To);
            var index = parallel.GetValueOrDefault(pair); parallel[pair] = index + 1;
            var route = ArrangementRouter.Route(edge.From, edge.To, boxes, i, index, er, token); routes.Add(route);
            for (var segment = 1; segment < route.Count; segment++)
                if (boxes.Any(node => node.Key != edge.From && node.Key != edge.To && ArrangementRouter.Crosses(route[segment - 1], route[segment], node.Value, 2)))
                    throw new InvalidDataException("A connection intersects an unrelated node; the move was rejected.");
            if (edge.Label.Length == 0) { labels.Add(null); continue; }
            var (width, height) = LabelSize(edge.Label);
            (Point Position, ArrangementBox Bounds) placed;
            try { placed = ArrangementRouter.Label(route, width, height, boxes.Values, labelBoxes); }
            catch (InvalidDataException)
            {
                // A bounded outside rail keeps a long/congested label attached to its actual
                // route. It can cost area and length; those costs remain visible in scoring.
                var rail = new ArrangementBox(boxes.Values.Max(b => b.Right) + 64,
                    Math.Max(boxes.Values.Max(b => b.Bottom), labelBoxes.Count == 0 ? 0 : labelBoxes.Max(b => b.Bottom)) + 48, width, height);
                route = ArrangementRouter.Route(edge.From, edge.To, boxes, i, index, er, token, rail); routes[^1] = route;
                placed = (new Point(rail.CentreX, rail.CentreY), rail);
            }
            labels.Add(placed.Position); labelBoxes.Add(placed.Bounds);
        }
        var widthExtent = boxes.Values.Max(b => b.Right); var heightExtent = boxes.Values.Max(b => b.Bottom);
        if (canonical.FlowchartLayout is { } compound)
            foreach (var group in compound.Groups) { widthExtent = Math.Max(widthExtent, group.X + group.Width); heightExtent = Math.Max(heightExtent, group.Y + group.Height); }
        foreach (var p in routes.SelectMany(route => route)) { widthExtent = Math.Max(widthExtent, p.X + 18); heightExtent = Math.Max(heightExtent, p.Y + 18); }
        foreach (var b in labelBoxes) { widthExtent = Math.Max(widthExtent, b.Right); heightExtent = Math.Max(heightExtent, b.Bottom); }
        if (widthExtent > ArrangementState.MaximumCoordinate || heightExtent > ArrangementState.MaximumCoordinate) throw new InvalidDataException("The complete routed diagram exceeds the canvas budget.");
        var result = canonical with { Svg = null };
        if (er)
            result = result with { ErLayout = canonical.ErLayout! with { Width = widthExtent + 24, Height = heightExtent + 24,
                Entities = Array.AsReadOnly(canonical.ErLayout!.Entities.Select(n => n with { X = boxes[n.Id].X, Y = boxes[n.Id].Y }).ToArray()),
                Relationships = Array.AsReadOnly(canonical.ErLayout.Relationships.Select((e, i) => e with { Points = routes[i], LabelPosition = labels[i] }).ToArray()) } };
        else
            result = result with { FlowchartLayout = canonical.FlowchartLayout! with { Width = widthExtent + 24, Height = heightExtent + 24,
                Nodes = Array.AsReadOnly(canonical.FlowchartLayout!.Nodes.Select(n => n with { X = boxes[n.Id].X, Y = boxes[n.Id].Y }).ToArray()),
                Edges = Array.AsReadOnly(canonical.FlowchartLayout.Edges.Select((e, i) => e with { Points = routes[i], LabelPosition = labels[i] }).ToArray()) } };
        return Finish(result, state, token);
    }

    private string RenderUsing(IGraphLayoutProvider provider, CancellationToken token)
    {
        // One deferred final render per accepted candidate/drop, never per pointer frame.
        var colours = DiagramThemes.Colours(canonical.Theme ?? "Light");
        var options = new RenderOptions { Bg = colours.Background, Fg = colours.Foreground, Accent = colours.Accent, Surface = colours.Surface,
            Border = colours.Border, Line = colours.Line, Font = "Segoe UI", MonoFont = "Consolas", RoundedEdges = false,
            LayoutProvider = provider, Limits = new ResourceLimits { MaxInputLength = 100_000, MaxElements = 500, RenderDeadline = TimeSpan.FromSeconds(6) } };
        using var stream = new MemoryStream();
        MermaidRenderer.RenderSvgAsync(SourceAuthoring.SourceText.PrepareRendererSource(canonical.Source), stream, options, token).GetAwaiter().GetResult();
        token.ThrowIfCancellationRequested();
        if (stream.Length > 8_000_000) throw new InvalidDataException("The arranged SVG exceeds the 8 MB presentation limit.");
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
    private ArrangementSnapshot Finish(DiagramResult result, ArrangementState state, CancellationToken token)
    {
        result = result with { Svg = RenderUsing(new FixedLayout(result), token) };
        var quality = Measure(result, token);
        if (quality.BodyIntersections != 0 || quality.LabelBodyOverlaps != 0) throw new InvalidDataException("Final arrangement geometry failed validation; the previous view is retained.");
        return new(result, state, quality);
    }

    private void ValidateGroups(IReadOnlyDictionary<string, ArrangementBox> boxes)
    {
        if (canonical.FlowchartLayout is not { } graph) return;
        static IEnumerable<PositionedGroup> Flatten(IEnumerable<PositionedGroup> groups)
        { foreach (var group in groups) { yield return group; foreach (var child in Flatten(group.Children)) yield return child; } }
        var groups = Flatten(graph.Groups).ToDictionary(g => g.Id);
        foreach (var (id, box) in boxes)
        {
            var membership = canonical.FlowchartGroups.Where(group => group.NodeIds.Contains(id)).ToArray();
            foreach (var member in membership)
            {
                var group = groups[member.Id];
                if (!new ArrangementBox(group.X + 8, group.Y + 32, group.Width - 16, group.Height - 40).Contains(box))
                    throw new InvalidDataException("Keep this node inside its existing subgraph. Moving compound groups is not supported.");
            }
            foreach (var group in groups.Values)
            {
                var boundary = new ArrangementBox(group.X, group.Y, group.Width, group.Height);
                // Containing ancestors are allowed, but a parent-owned node must not
                // visually enter a nested group to which its source does not belong.
                var belongsInside = membership.Any(member =>
                {
                    var own = groups[member.Id];
                    return boundary.Contains(new(own.X, own.Y, own.Width, own.Height));
                });
                if (!belongsInside && box.Intersects(boundary))
                    throw new InvalidDataException("A node cannot enter a subgraph to which its source does not belong.");
            }
        }
    }

    public IReadOnlyList<ArrangementSnapshot> Candidates(ArrangementState current, CancellationToken token = default)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(token); budget.CancelAfter(TimeSpan.FromSeconds(12));
        var candidates = new List<ArrangementSnapshot>();
        var grouped = canonical.FlowchartGroups.SelectMany(g => g.NodeIds).ToHashSet(StringComparer.Ordinal);
        var movable = original.Keys.Where(id => !current.Positions[id].Pinned && !grouped.Contains(id)).ToArray();
        if (movable.Length < 2) return candidates;
        // Fixed group boundaries are never translated or flattened by a candidate.
        if (grouped.Count > 0) return candidates;
        var currentSnapshot = ArrangeCore(current, budget.Token);
        var baseline = currentSnapshot.Quality;
        void Consider(ArrangementSnapshot candidate)
        {
            if (candidate.State.Positions.All(p => current.Positions[p.Key].X == p.Value.X && current.Positions[p.Key].Y == p.Value.Y)) return;
            if (candidate.Quality.Area < baseline.Area - 1 && !candidates.Any(c => c.State.Strategy == ArrangementStrategy.Compact && c.Quality.Area <= candidate.Quality.Area))
            { candidates.RemoveAll(c => c.State.Strategy == ArrangementStrategy.Compact); candidates.Add(new(candidate.Render, new(candidate.State.Positions, ArrangementStrategy.Compact, candidate.State.LayoutVariant), candidate.Quality)); }
            if (candidate.Quality.Crossings < baseline.Crossings && !candidates.Any(c => c.State.Strategy == ArrangementStrategy.FewerCrossings && c.Quality.Crossings <= candidate.Quality.Crossings))
            { candidates.RemoveAll(c => c.State.Strategy == ArrangementStrategy.FewerCrossings); candidates.Add(new(candidate.Render, new(candidate.State.Positions, ArrangementStrategy.FewerCrossings, candidate.State.LayoutVariant), candidate.Quality)); }
        }
        // Reuse the established layout engines first. Canonical defaults remain unchanged;
        // these source-preserving alternatives vary model order and bounded ER sweep effort.
        if (!current.Positions.Values.Any(p => p.Pinned))
        for (var variant = 0; variant < 3; variant++)
        {
            budget.Token.ThrowIfCancellationRequested();
            try
            {
                Consider(Alternative(variant, budget.Token));
            }
            catch (Exception error) when (error is InvalidDataException or MermaidResourceLimitException) { }
        }
        var orders = new[] { movable, movable.OrderByDescending(id => edges.Count(e => e.From == id || e.To == id)).ThenBy(id => id, StringComparer.Ordinal).ToArray() };
        var spacing = edges.Length == 0 ? 24 : 64;
        var maxWidth = original.Values.Max(b => b.Width) + spacing; var maxHeight = original.Values.Max(b => b.Height) + spacing;
        var columns = new[] { Math.Max(1, (int)Math.Ceiling(Math.Sqrt(original.Count))), Math.Max(1, (int)Math.Ceiling(Math.Sqrt(original.Count)) + 1), Math.Max(1, original.Count / 2) }.Distinct();
        foreach (var order in orders)
        foreach (var columnCount in columns)
        {
            budget.Token.ThrowIfCancellationRequested();
            var positions = current.Positions.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            var pinned = original.Where(pair => current.Positions[pair.Key].Pinned).Select(pair => pair.Value with { X = current.Positions[pair.Key].X, Y = current.Positions[pair.Key].Y }).ToList();
            var slot = 0;
            foreach (var id in order)
            {
                ArrangementBox box;
                do { box = original[id] with { X = 48 + slot % columnCount * maxWidth, Y = 48 + slot / columnCount * maxHeight }; slot++; }
                while (pinned.Any(p => box.Intersects(p, 80)) && slot < original.Count * 20);
                positions[id] = new(box.X, box.Y);
            }
            try
            {
                var candidate = ArrangeCore(new(positions, ArrangementStrategy.Compact), budget.Token);
                Consider(candidate);
            }
            catch (InvalidDataException) { /* Infeasible candidates never replace the accepted arrangement. */ }
        }
        if (candidates.Count == 2 && candidates[0].State.Positions.All(p => candidates[1].State.Positions[p.Key] == p.Value)) candidates.RemoveAt(0);
        return candidates.AsReadOnly();
    }

    private static (double Width, double Height) LabelSize(string label)
    { var metrics = TextMetrics.MeasureMultiline(label.AsSpan(), 12, 500); return (metrics.Width * 1.06 + 26, metrics.Height + 12); }

    private ArrangementSnapshot Alternative(int variant, CancellationToken token)
    {
        var provider = new AlternativeLayout(variant, token);
        var generated = RenderUsing(provider, token);
        var result = canonical with { Svg = generated, ErLayout = provider.Er, FlowchartLayout = provider.Flowchart };
        var positions = er ? provider.Er!.Entities.Select(n => new KeyValuePair<string, ArrangementPosition>(n.Id, new(n.X, n.Y)))
            : provider.Flowchart!.Nodes.Select(n => new KeyValuePair<string, ArrangementPosition>(n.Id, new(n.X, n.Y)));
        var quality = Measure(result, token);
        if (quality.BodyIntersections != 0 || quality.LabelBodyOverlaps != 0) throw new InvalidDataException("The alternative layout failed geometry validation.");
        var boxes = er ? result.ErLayout!.Entities.Select(n => new ArrangementBox(n.X, n.Y, n.Width, n.Height)).ToArray()
            : result.FlowchartLayout!.Nodes.Select(n => new ArrangementBox(n.X, n.Y, n.Width, n.Height)).ToArray();
        for (var index = 0; index < boxes.Length; index++)
        {
            token.ThrowIfCancellationRequested();
            var box = boxes[index];
            if (!double.IsFinite(box.Right) || !double.IsFinite(box.Bottom) || box.X < 0 || box.Y < 0
                || box.Right > ArrangementState.MaximumCoordinate || box.Bottom > ArrangementState.MaximumCoordinate
                || boxes.Take(index).Any(other => box.Intersects(other)))
                throw new InvalidDataException("The alternative layout contains overlapping or out-of-bounds nodes.");
        }
        return new(result, new(positions, ArrangementStrategy.Compact, variant + 1), quality);
    }

    private ArrangementQuality Measure(DiagramResult render, CancellationToken token)
    {
        var boxes = er ? render.ErLayout!.Entities.Select(n => (n.Id, Box: new ArrangementBox(n.X, n.Y, n.Width, n.Height))).ToArray()
            : render.FlowchartLayout!.Nodes.Select(n => (n.Id, Box: new ArrangementBox(n.X, n.Y, n.Width, n.Height))).ToArray();
        var paths = er ? render.ErLayout!.Relationships.Select(e => (From: e.Entity1, To: e.Entity2, e.Points, e.Label, e.LabelPosition)).ToArray()
            : render.FlowchartLayout!.Edges.Select(e => (From: e.Source, To: e.Target, e.Points, Label: e.Label ?? "", e.LabelPosition)).ToArray();
        var intersections = 0; var labelOverlaps = 0; var crossings = 0; var length = 0d; var bends = 0;
        for (var index = 0; index < paths.Length; index++)
        {
            token.ThrowIfCancellationRequested(); var path = paths[index];
            bends += Math.Max(0, path.Points.Count - 2);
            for (var i = 1; i < path.Points.Count; i++)
            {
                var a = path.Points[i - 1]; var b = path.Points[i]; length += Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
                intersections += boxes.Count(n => n.Id != path.From && n.Id != path.To && ArrangementRouter.Crosses(a, b, n.Box));
            }
            if (path.Label.Length > 0 && path.LabelPosition is { } p)
            { var (w, h) = LabelSize(path.Label); labelOverlaps += boxes.Count(n => n.Box.Intersects(new(p.X - w / 2, p.Y - h / 2, w, h))); }
            for (var other = 0; other < index; other++)
            {
                token.ThrowIfCancellationRequested();
                for (var a = 1; a < path.Points.Count; a++)
                {
                    token.ThrowIfCancellationRequested();
                    for (var b = 1; b < paths[other].Points.Count; b++)
                        if (ProperCrossing(path.Points[a - 1], path.Points[a], paths[other].Points[b - 1], paths[other].Points[b])) crossings++;
                }
            }
        }
        var width = er ? render.ErLayout!.Width : render.FlowchartLayout!.Width;
        var height = er ? render.ErLayout!.Height : render.FlowchartLayout!.Height;
        return new(intersections, labelOverlaps, crossings, width * height, width / Math.Max(1, height), length, bends);
    }
    private static bool ProperCrossing(Point a, Point b, Point c, Point d)
    {
        static double Side(Point p, Point q, Point r) => (q.X - p.X) * (r.Y - p.Y) - (q.Y - p.Y) * (r.X - p.X);
        return Side(a, b, c) * Side(a, b, d) < -.0001 && Side(c, d, a) * Side(c, d, b) < -.0001;
    }
    private sealed class FixedLayout(DiagramResult result) : IGraphLayoutProvider
    {
        public PositionedGraph LayoutFlowchart(MermaidGraph graph, RenderOptions? options = null, StrictStylingOptions? strict = null)
            => result.FlowchartLayout ?? throw new InvalidDataException("The arrangement family changed.");
        public PositionedErDiagram LayoutEr(ErDiagram diagram) => result.ErLayout ?? throw new InvalidDataException("The arrangement family changed.");
        public PositionedClassDiagram LayoutClass(ClassDiagram diagram) => throw new InvalidDataException("Class arrangement is unsupported.");
    }
    private sealed class AlternativeLayout(int variant, CancellationToken token) : IGraphLayoutProvider
    {
        internal PositionedErDiagram? Er { get; private set; }
        internal PositionedGraph? Flowchart { get; private set; }
        public PositionedGraph LayoutFlowchart(MermaidGraph graph, RenderOptions? options = null, StrictStylingOptions? strict = null)
        {
            token.ThrowIfCancellationRequested(); var decoded = MermaidEntityLayoutProvider.DecodeSemantics(graph);
            var nodes = variant == 0 ? decoded.Nodes.Reverse() : decoded.Nodes.OrderBy(pair => pair.Key, variant == 1 ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
            Flowchart = MermaidRenderer.LayoutProvider.LayoutFlowchart(decoded with { Nodes = nodes.ToDictionary(pair => pair.Key, pair => pair.Value) },
                (options ?? new()) with { NodeSpacing = variant == 0 ? 16 : 32, LayerSpacing = variant == 0 ? 48 : 88 }, strict);
            token.ThrowIfCancellationRequested(); return Flowchart;
        }
        public PositionedErDiagram LayoutEr(ErDiagram diagram)
        {
            var entities = variant == 0 ? diagram.Entities.Reverse() : diagram.Entities.OrderByDescending(n => diagram.Relationships.Count(e => e.Entity1 == n.Id || e.Entity2 == n.Id));
            Er = BoundedErLayout.Layout(diagram with { Entities = entities.ToArray() }, MermaidRenderer.LayoutProvider, token, variant == 2 ? 2 : 0, variant == 0 ? 16 : 28, variant == 0 ? 60 : 88);
            return Er;
        }
        public PositionedClassDiagram LayoutClass(ClassDiagram diagram) => throw new InvalidDataException("Class arrangement is unsupported.");
    }
}
