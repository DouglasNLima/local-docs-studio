using System.Windows;
using System.Windows.Media;
using LensDocsStudio.Core.ErDiagrams;
using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Native.Rendering;

public sealed record InspectionEntity(string Identity, string Description, Geometry Shape);
public sealed record InspectionConnection(string Identity, string First, string Second, string Description, Geometry Path, Rect? LabelBounds);

/// <summary>Read-only geometry verified against one canonical renderer result. No imported SVG or edit authority.</summary>
public sealed class DiagramInspection
{
    public DrawingImage Drawing { get; }
    public IReadOnlyList<InspectionEntity> Entities { get; }
    public IReadOnlyList<InspectionConnection> Connections { get; }
    public Geometry ConnectionClip { get; }
    private sealed record HitPath(string Identity, Rect Bounds, (Point First, Point Second)[] Segments);
    private readonly HitPath[] hitPaths;
    private readonly (InspectionEntity Entity, Rect Bounds)[] hitEntities;
    private DiagramInspection(DrawingImage drawing, IEnumerable<InspectionEntity> entities, IEnumerable<InspectionConnection> connections)
    {
        Drawing = drawing; Entities = Array.AsReadOnly(entities.ToArray()); Connections = Array.AsReadOnly(connections.ToArray());
        hitEntities = Entities.Select(entity => (entity, entity.Shape.Bounds)).ToArray();
        var segmentCount = 0;
        hitPaths = Connections.Select(edge =>
        {
            // Flatten once at a quarter drawing-DIP tolerance, below the minimum
            // hit tolerance. Identities still come exclusively from verification.
            var flat = edge.Path.GetFlattenedPathGeometry(.25, ToleranceType.Absolute);
            var transform = flat.Transform?.Value ?? Matrix.Identity; var segments = new List<(Point, Point)>();
            foreach (var figure in flat.Figures)
            {
                var start = transform.Transform(figure.StartPoint); var previous = start;
                foreach (var part in figure.Segments)
                {
                    IEnumerable<Point> points = part switch { PolyLineSegment line => line.Points, LineSegment line => new[] { line.Point }, _ => throw new InvalidDataException("Unsupported flattened inspection path.") };
                    foreach (var point in points)
                    {
                        if (++segmentCount > 100_000) throw new InvalidDataException("The inspection path budget was exceeded.");
                        var next = transform.Transform(point); segments.Add((previous, next)); previous = next;
                    }
                }
                if (figure.IsClosed) segments.Add((previous, start));
            }
            return new HitPath(edge.Identity, edge.Path.Bounds, segments.ToArray());
        }).ToArray();
        var excluded = new GeometryGroup { FillRule = FillRule.Nonzero };
        foreach (var entity in Entities) excluded.Children.Add(entity.Shape);
        foreach (var edge in Connections) if (edge.LabelBounds is { } bounds) excluded.Children.Add(new RectangleGeometry(bounds));
        ConnectionClip = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(new Rect(0, 0, drawing.Width, drawing.Height)), excluded);
        ConnectionClip.Freeze();
    }

    public static DiagramInspection? Create(DiagramResult result, NativeSvgService svg, CancellationToken token = default)
    {
        if (!result.Success || result.Source.Length > 100_000) return null;
        if (result.ErLayout is { Entities.Count: <= 512, Relationships.Count: <= 2048 })
        {
            var model = new ErDiagramSourceParser().Parse(result.Source, token);
            var view = NativeErPresentation.Create(model, result, svg, token);
            return new(view.Drawing, view.Entities.Select(hit =>
            {
                var shape = new RectangleGeometry(hit.Bounds); shape.Freeze();
                return new InspectionEntity(hit.Identity, model.Entities.Single(entity => entity.Identity == hit.Identity).Label, shape);
            }), view.Relationships.Select(hit =>
            {
                var edge = model.Relationship(hit.Identity);
                var description = $"{model.Entity(edge.Entity1).Label} → {model.Entity(edge.Entity2).Label}";
                if (edge.Label.Length > 0) description += " · " + edge.Label;
                description += " · " + ErDiagramSourceParser.Notation(edge.Cardinality1, true) + (edge.Identifying ? "--" : "..") + ErDiagramSourceParser.Notation(edge.Cardinality2, false);
                return new InspectionConnection(hit.Identity, "entity:" + edge.Entity1, "entity:" + edge.Entity2, description, hit.Path, hit.LabelBounds);
            }));
        }
        if (result.FlowchartLayout is { Nodes.Count: <= 512, Edges.Count: <= 2048 })
        {
            var view = NativeMermaidPresentation.Create(result, svg, token);
            // Groups can be connection endpoints, but only verified node targets are
            // selectable here. Group hit boxes would cover unrelated child nodes.
            var nodes = view.Elements.Values.Where(item => item.Key.StartsWith("node:", StringComparison.Ordinal)).ToArray();
            var identities = nodes.Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
            return new(view.Drawing, nodes.Select(item => new InspectionEntity(item.Key,
                view.Graph.Nodes.Single(node => "node:" + node.Id == item.Key).Label, item.Geometry)),
                view.Connections.Where(edge => identities.Contains("node:" + edge.Source) && identities.Contains("node:" + edge.Target)).Select(edge =>
                    new InspectionConnection("edge:" + edge.Ordinal, "node:" + edge.Source, "node:" + edge.Target,
                        edge.Source + " → " + edge.Target + (edge.Label.Length > 0 ? " · " + edge.Label : ""), edge.Path, edge.LabelBounds)));
        }
        return null;
    }

    public string? Hit(Point point, double scale)
    {
        // Overlapping labels/paths are ambiguous. Fail closed rather than assigning
        // an arbitrary relationship. The accessible list always distinguishes them.
        string? hit = null;
        foreach (var edge in Connections)
        {
            if (edge.LabelBounds?.Contains(point) != true) continue;
            if (hit is not null) return null;
            hit = edge.Identity;
        }
        if (hit is not null) return hit;
        foreach (var entity in hitEntities)
        {
            if (!entity.Bounds.Contains(point) || !entity.Entity.Shape.FillContains(point)) continue;
            if (hit is not null) return null;
            hit = entity.Entity.Identity;
        }
        if (hit is not null) return hit;
        var tolerance = Math.Clamp(5 / Math.Max(.0001, scale), 2, 12);
        foreach (var edge in hitPaths)
        {
            var bounds = edge.Bounds; bounds.Inflate(tolerance, tolerance);
            if (!bounds.Contains(point)) continue;
            var near = false;
            foreach (var (first, second) in edge.Segments)
                if (DiagramViewportMath.NearSegment(point.X, point.Y, first.X, first.Y, second.X, second.Y, tolerance)) { near = true; break; }
            if (!near) continue;
            if (hit is not null) return null;
            hit = edge.Identity;
        }
        return hit;
    }

    public IReadOnlyList<InspectionConnection> Connected(string? identity) => identity is null ? []
        : Connections.Where(edge => edge.Identity == identity || edge.First == identity || edge.Second == identity).ToArray();
    public string? Description(string? identity) => Entities.FirstOrDefault(item => item.Identity == identity)?.Description
        ?? Connections.FirstOrDefault(item => item.Identity == identity)?.Description;
}
