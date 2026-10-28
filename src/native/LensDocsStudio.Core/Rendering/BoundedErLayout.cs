// Adapted from the pinned Mermaider 0.15.1 ER adapter (MIT). See PinnedMermaider/NOTICE.md.
using Mermaider.Models;
using LensDocsStudio.Core.Rendering.PinnedMermaider;
using Mermaider.Layout;
using Sugiyama;

namespace LensDocsStudio.Core.Rendering;

/// <summary>
/// The pinned ER adapter with cancellable, bounded crossing search.
/// Parser, node sizing, routing and SVG generation still use Mermaider/Sugiyama 0.15.1.
/// </summary>
internal static class BoundedErLayout
{
    private static double LabelBoxWidth(double width) => width * 1.06 + 18.25;
    private const double Padding = 20;
    // Shared entity geometry (class / ER / requirement): 36px header, 28px rows, the EntityGrid column grid.
    private const double HeaderHeight = 36;
    private const double RowHeight = 28;
    private const double NodeSpacing = 20;
    private const double LayerSpacing = 72;

    internal static PositionedErDiagram Layout(ErDiagram diagram, IGraphLayoutProvider inner, CancellationToken cancellationToken, int restarts)
    {
        if (diagram.Entities.Count == 0)
            return new PositionedErDiagram { Width = 0, Height = 0, Entities = [], Relationships = [] };

        cancellationToken.ThrowIfCancellationRequested();
        // Ask the pinned renderer for exact entity sizing without its expensive relationship search.
        // No source is rewritten and the original semantic attributes remain attached.
        var sized = inner.LayoutEr(diagram with { Relationships = [] }).Entities.ToDictionary(item => item.Id);
        var entitySizes = new Dictionary<string, (double Width, double Height)>();
        foreach (var entity in diagram.Entities)
        {
            var degree = diagram.Relationships.Count(r => r.Entity1 != r.Entity2 && (r.Entity1 == entity.Id || r.Entity2 == entity.Id));
            entitySizes[entity.Id] = (Math.Max(sized[entity.Id].Width, degree > 4 ? degree * 18 + 24 : 0), sized[entity.Id].Height);
        }

        var layoutNodes = new List<LayoutNode>(diagram.Entities.Count);
        foreach (var entity in diagram.Entities)
        {
            var (w, h) = entitySizes[entity.Id];
            layoutNodes.Add(new LayoutNode(entity.Id, w, h));
        }

        var layoutEdges = new List<LayoutEdge>(diagram.Relationships.Count);
        var layoutEdgeRelIndices = new List<int>(diagram.Relationships.Count);
        for (var i = 0; i < diagram.Relationships.Count; i++)
        {
            var rel = diagram.Relationships[i];
            if (rel.Entity1 == rel.Entity2)
                continue; // self-loops carry no layout info; skip to avoid Sugiyama height inflation
            double labelW = 0, labelH = 0;
            if (rel.Label.Length > 0)
            {
                var metrics = TextMetrics.MeasureMultiline(rel.Label.AsSpan(), 12, 500);
                labelW = LabelBoxWidth(metrics.Width) + 6;
                labelH = metrics.Height + 8;
            }
            layoutEdges.Add(new LayoutEdge(rel.Entity1, rel.Entity2, labelW, labelH));
            layoutEdgeRelIndices.Add(i);
        }

        var layoutDir = diagram.Direction switch
        {
            Direction.LR => LayoutDirection.LR,
            Direction.RL => LayoutDirection.RL,
            Direction.BT => LayoutDirection.BT,
            _ => LayoutDirection.TD, // default TD matches mermaid.js behaviour
        };

        // Labels sit on their own column in the gap (never stacked), so the gap only has to fit one label plus the stubs and
        // crow's-foot markers: the Sugiyama layout sizes each gap from its labels (label extent + 56) and uses LayerSpacing
        // for gaps that carry none.
        const double effectiveLayerSpacing = LayerSpacing;

        var layoutGraph = new LayoutGraph(layoutDir, layoutNodes, layoutEdges, []);
        var componentCount = CountConnectedComponents(layoutNodes, layoutEdges);
        // 1-3 components: one row; 4+: grid with ceil(sqrt(n)) per row
        var maxPerRow = componentCount <= 3 ? componentCount : (int)Math.Ceiling(Math.Sqrt(componentCount));
        using var graphPhase = MermaidDiagnostics.Measure("renderer.ER.sugiyama-layout-routing");
        var maxNodes = new ResourceLimits().MaxNodesAfterLayout;
        var result = SugiyamaLayout.Compute(layoutGraph, new LayoutOptions
        {
            Padding = Padding,
            NodeSpacing = NodeSpacing,
            LayerSpacing = effectiveLayerSpacing,
            CrossingIterations = 8,
            StrictTopDownFanout = true,
            ForceBottomExitFanOut = true,
            MaxComponentsPerRow = maxPerRow,
            TightSourceLayering = true,
            SeparateComponents = false,
            UseRealFirstTiebreaker = true,
            UseModelOrderForVirtualNodes = restarts == 0,
            PortAwareLayout = true,
            CrossingRestarts = restarts,
            CancellationToken = cancellationToken,
            MaxNodeCount = maxNodes,
        });

        graphPhase?.Dispose();
        cancellationToken.ThrowIfCancellationRequested();
        return ExtractPositioned(result, diagram, layoutEdgeRelIndices);
    }

    /// <summary>Connected components (clusters) numbered in order of their first entity; every isolated entity is its own cluster.</summary>
    private static Dictionary<string, int> AssignClusters(ErDiagram diagram)
    {
        var parent = diagram.Entities.ToDictionary(e => e.Id, e => e.Id);
        string Find(string id) { while (parent[id] != id) id = parent[id]; return id; }
        foreach (var relationship in diagram.Relationships) parent[Find(relationship.Entity2)] = Find(relationship.Entity1);
        var numbers = new Dictionary<string, int>(); var result = new Dictionary<string, int>();
        foreach (var entity in diagram.Entities) {
            var ancestor = Find(entity.Id);
            if (!numbers.TryGetValue(ancestor, out var number)) numbers[ancestor] = number = numbers.Count;
            result[entity.Id] = number;
        }
        return result;
    }

    private static int CountConnectedComponents(List<LayoutNode> nodes, List<LayoutEdge> edges)
    {
        var adj = new Dictionary<string, HashSet<string>>(nodes.Count);
        foreach (var n in nodes)
            adj[n.Id] = [];
        foreach (var e in edges)
        {
            if (adj.TryGetValue(e.Source, out var s))
                _ = s.Add(e.Target);
            if (adj.TryGetValue(e.Target, out var t))
                _ = t.Add(e.Source);
        }
        var visited = new HashSet<string>(nodes.Count);
        var count = 0;
        var stack = new Stack<string>();
        foreach (var n in nodes)
        {
            if (!visited.Add(n.Id))
                continue;
            count++;
            stack.Push(n.Id);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                foreach (var nb in adj[cur])
                {
                    if (visited.Add(nb))
                        stack.Push(nb);
                }
            }
        }
        return count;
    }

    private static PositionedErDiagram ExtractPositioned(LayoutResult result, ErDiagram diagram, IReadOnlyList<int> layoutEdgeRelIndices)
    {
        var nodeLookup = result.Nodes.ToDictionary(n => n.Id);
        var edgeLookup = result.Edges.ToDictionary(e => e.OriginalIndex);
        var positionedEntities = new List<PositionedErEntity>(diagram.Entities.Count);
        var clusters = AssignClusters(diagram);

        foreach (var entity in diagram.Entities)
        {
            if (!nodeLookup.TryGetValue(entity.Id, out var n))
                throw new InvalidDataException("ER layout omitted an entity.");
            positionedEntities.Add(new PositionedErEntity
            {
                Id = entity.Id,
                Label = entity.Label,
                Attributes = entity.Attributes,
                X = n.X,
                Y = n.Y,
                Width = n.Width,
                Height = n.Height,
                HeaderHeight = HeaderHeight,
                RowHeight = RowHeight,
                Cluster = clusters[entity.Id],
            });
        }

        // layoutEdgeRelIndices[edgeIdx] → index in diagram.Relationships; self-loops were filtered out
        var positionedRels = new List<PositionedErRelationship>(layoutEdgeRelIndices.Count);
        for (var edgeIdx = 0; edgeIdx < layoutEdgeRelIndices.Count; edgeIdx++)
        {
            var rel = diagram.Relationships[layoutEdgeRelIndices[edgeIdx]];
            if (!edgeLookup.TryGetValue(edgeIdx, out var edge))
                throw new InvalidDataException("ER layout omitted a relationship occurrence.");

            var points = edge.Points.Select(p => new Point(p.X, p.Y)).ToList();
            Point? labelPos = edge.LabelPosition is { } lp ? new Point(lp.X, lp.Y) : null;
            positionedRels.Add(new PositionedErRelationship
            {
                Entity1 = rel.Entity1,
                Entity2 = rel.Entity2,
                Cardinality1 = rel.Cardinality1,
                Cardinality2 = rel.Cardinality2,
                Label = rel.Label,
                Identifying = rel.Identifying,
                Points = points,
                LabelPosition = labelPos,
            });
        }

        // The pinned router can turn below a short source while a taller neighbour in
        // the same layer still occupies that row. Keep its ports/columns and move only
        // an obstructed outgoing bend into the existing inter-layer gap.
        for (var i = 0; i < positionedRels.Count; i++)
            positionedRels[i] = ClearOutgoingBend(positionedRels[i], positionedEntities);

        // Self-loops carry no layout info (filtered from Sugiyama). Draw each as a rectangular loop out of the side with more free room,
        // like any other edge: straight stubs for the markers, with the label sitting on the outer vertical so it stays connected.
        var loopsPerSide = new Dictionary<(string Id, bool Left), int>();
        foreach (var rel in diagram.Relationships)
        {
            if (rel.Entity1 != rel.Entity2)
                continue;
            if (!nodeLookup.TryGetValue(rel.Entity1, out var n))
                throw new InvalidDataException("ER layout omitted a self-referential entity.");

            var freeLeft = double.MaxValue;
            var freeRight = double.MaxValue;
            foreach (var other in result.Nodes)
            {
                if (other.Id == n.Id || other.Y + other.Height <= n.Y || other.Y >= n.Y + n.Height)
                    continue;
                if (other.X + other.Width <= n.X)
                    freeLeft = Math.Min(freeLeft, n.X - (other.X + other.Width));
                else if (other.X >= n.X + n.Width)
                    freeRight = Math.Min(freeRight, other.X - (n.X + n.Width));
            }

            var left = freeLeft >= freeRight;
            var dir = left ? -1 : 1;
            var sideX = left ? n.X : n.X + n.Width;
            // The loop must be deep enough that the label pill (centred on the outer vertical) clears the markers beside the entity.
            var labelBox = rel.Label.Length > 0
                ? LabelBoxWidth(TextMetrics.MeasureMultiline(rel.Label.AsSpan(), 12, 500).Width)
                : 0;
            var loopOut = 36 + (labelBox / 2);
            var nth = loopsPerSide.GetValueOrDefault((n.Id, left));
            loopsPerSide[(n.Id, left)] = nth + 1;
            var centreY = n.Y + (n.Height / 2);
            var halfSpan = Math.Clamp(n.Height * 0.3, 24, 40);
            var exitY = centreY - halfSpan;
            var entryY = centreY + halfSpan;
            var outerX = sideX + (dir * (loopOut + (nth * 24)));
            var labelPos = rel.Label.Length > 0 ? new Point(outerX, centreY) : (Point?)null;
            positionedRels.Add(new PositionedErRelationship
            {
                Entity1 = rel.Entity1,
                Entity2 = rel.Entity2,
                Cardinality1 = rel.Cardinality1,
                Cardinality2 = rel.Cardinality2,
                Label = rel.Label,
                Identifying = rel.Identifying,
                Points = [new Point(sideX, exitY), new Point(outerX, exitY), new Point(outerX, entryY), new Point(sideX, entryY)],
                LabelPosition = labelPos,
            });
        }

        return FitLabelsInCanvas(new PositionedErDiagram
        {
            Width = result.Width,
            Height = result.Height,
            Entities = positionedEntities,
            Relationships = positionedRels,
        });
    }

    internal static PositionedErRelationship ClearOutgoingBend(PositionedErRelationship edge, IReadOnlyList<PositionedErEntity> entities)
    {
        var points = edge.Points;
        if (points.Count < 4) return edge;
        const double epsilon = .01;
        var vertical = Math.Abs(points[0].X - points[1].X) < epsilon;
        if (!vertical && Math.Abs(points[0].Y - points[1].Y) >= epsilon) return edge;
        var sign = Math.Sign(vertical ? points[1].Y - points[0].Y : points[1].X - points[0].X);
        if (sign == 0) return edge;
        double Along(Point p) => sign * (vertical ? p.Y : p.X);
        double Across(Point p) => vertical ? p.X : p.Y;
        Point At(Point p, double along) => vertical ? new(p.X, sign * along) : new(sign * along, p.Y);
        var original = Along(points[1]); var end = Along(points[3]);
        if (Math.Abs(Along(points[2]) - original) >= epsilon || Math.Abs(Across(points[2]) - Across(points[3])) >= epsilon || end <= original) return edge;
        var obstacles = entities.Where(n => n.Id != edge.Entity1 && n.Id != edge.Entity2).ToArray();
        var candidate = points.ToArray(); var exit = original;
        for (var attempt = 0; attempt <= obstacles.Length; attempt++)
        {
            var blocked = obstacles.Where(n => Crosses(candidate[1], candidate[2], n) || Crosses(candidate[2], candidate[3], n)).ToArray();
            if (blocked.Length == 0)
            {
                if (exit == original || entities.Any(n => Crosses(candidate[0], candidate[1], n)
                    || Crosses(candidate[1], candidate[2], n) || Crosses(candidate[2], candidate[3], n))) return edge;
                var label = edge.LabelPosition;
                if (label is { } position)
                {
                    var along = Along(position);
                    if (Math.Abs(along - original) < epsilon) label = At(position, exit);
                    else if (Math.Abs(Across(position) - Across(points[2])) < epsilon && along > original && along < end)
                        label = At(position, exit + (along - original) * (end - exit) / (end - original));
                }
                return edge with { Points = candidate, LabelPosition = label };
            }
            var next = blocked.Max(n => Math.Max(Along(new(n.X, n.Y)), Along(new(n.X + n.Width, n.Y + n.Height)))) + 28;
            // Preserve the final approach and its cardinality marker; never reverse a segment.
            if (next <= exit || next >= end - 18) return edge;
            exit = next; candidate[1] = At(points[1], exit); candidate[2] = At(points[2], exit);
        }
        return edge;

        static bool Crosses(Point a, Point b, PositionedErEntity n) => Math.Abs(a.X - b.X) < epsilon
            ? a.X > n.X + epsilon && a.X < n.X + n.Width - epsilon && Math.Max(a.Y, b.Y) > n.Y + epsilon && Math.Min(a.Y, b.Y) < n.Y + n.Height - epsilon
            : Math.Abs(a.Y - b.Y) < epsilon && a.Y > n.Y + epsilon && a.Y < n.Y + n.Height - epsilon && Math.Max(a.X, b.X) > n.X + epsilon && Math.Min(a.X, b.X) < n.X + n.Width - epsilon;
    }

    /// <summary>Labels and self-loop paths can extend past entities; keep the complete horizontal canvas.</summary>
    private static PositionedErDiagram FitLabelsInCanvas(PositionedErDiagram d)
    {
        var minX = d.Entities.Count > 0 ? d.Entities.Min(e => e.X) : double.MaxValue;
        var maxX = d.Entities.Count > 0 ? d.Entities.Max(e => e.X + e.Width) : 0;
        foreach (var r in d.Relationships)
        {
            foreach (var point in r.Points)
            {
                minX = Math.Min(minX, point.X);
                maxX = Math.Max(maxX, point.X);
            }
            if (r.Label.Length == 0 || r.LabelPosition is not { } lp)
                continue;
            var m = TextMetrics.MeasureMultiline(r.Label.AsSpan(), 12, 500);
            var half = LabelBoxWidth(m.Width) / 2;
            minX = Math.Min(minX, lp.X - half);
            maxX = Math.Max(maxX, lp.X + half);
        }

        var shift = minX < Padding ? Padding - minX : 0;
        var width = Math.Max(d.Width + shift, maxX + shift + Padding);
        if (shift == 0 && Math.Abs(width - d.Width) < 0.01)
            return d;

        return d with
        {
            Width = width,
            Entities = d.Entities.Select(e => e with { X = e.X + shift }).ToList(),
            Relationships = d.Relationships.Select(r => r with
            {
                Points = r.Points.Select(p => new Point(p.X + shift, p.Y)).ToList(),
                LabelPosition = r.LabelPosition is { } lp ? new Point(lp.X + shift, lp.Y) : null,
            }).ToList(),
        };
    }

}
