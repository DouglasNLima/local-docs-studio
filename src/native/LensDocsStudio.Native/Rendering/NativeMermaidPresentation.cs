using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Rendering;
using Mermaider.Models;

namespace LensDocsStudio.Native.Rendering;

public sealed record NativeMermaidElement(string Key, Rect Bounds, Geometry Geometry)
{
    public string? Fill { get; init; }
    public string? Stroke { get; init; }
    public string? TextColour { get; init; }
}
public sealed record NativeMermaidConnection(int Ordinal, string Source, string Target, string Label, Geometry Path, Rect? LabelBounds);

/// <summary>The preview drawing and interaction geometry from the very same render. Never accepts imported SVG.</summary>
public sealed record NativeMermaidPresentation(DrawingImage Drawing, IReadOnlyDictionary<string, NativeMermaidElement> Elements,
    IReadOnlyList<NativeMermaidConnection> Connections, PositionedGraph Graph)
{
    public IReadOnlyList<FlowchartGroupIdentity> Membership { get; init; } = [];
    public IReadOnlyDictionary<string, string> SourceLabels { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<Mermaider.Models.MermaidEdge> SourceConnections { get; init; } = [];
    public static NativeMermaidPresentation Create(DiagramResult result, NativeSvgService svg) => Create(result, svg, default);
    public static NativeMermaidPresentation Create(DiagramResult result, NativeSvgService svg, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var graph = result.FlowchartLayout ?? throw new InvalidDataException("No canonical Flowchart layout is available. Use Edit source.");
        var generated = result.Svg ?? throw new InvalidDataException(result.Error);
        // Sanitise before interpreting metadata; the drawing uses the same existing native conversion as preview/export.
        var root = svg.PrepareDocument(generated).Root!;
        if (root.Descendants().Count() > 30_000) throw new InvalidDataException("The canonical drawing exceeds the interaction geometry budget.");
        // Inert native identities let session dragging translate the actual drawing groups,
        // without another SVG parse or render on selection, hover or pointer movement.
        var nodeIndex = 0;
        foreach (var element in root.Descendants().Where(e => HasClass(e, "node"))) element.SetAttributeValue("id", "lens-node-" + nodeIndex++);
        foreach (var element in root.Descendants().Where(e => HasClass(e, "subgraph")))
            element.SetAttributeValue("id", "lens-group-" + (string?)element.Attribute("data-id"));
        // The pinned canonical SVG paints group titles separately, above edges.
        // Keep that paint order while giving verified titles the group's session transform.
        foreach (var group in Groups(graph.Groups))
        {
            var titles = root.Elements().Where(element => element.Name.LocalName == "text"
                && Math.Abs(Number(element, "x") - group.X - 12) < .01
                && Math.Abs(Number(element, "y") - group.Y - 14) < .01
                && MetadataLabel(element.Value) == MetadataLabel(group.Label)).ToArray();
            if (titles.Length != 1) throw new InvalidDataException("The canonical subgraph title cannot be mapped safely: " + group.Id);
            var title = titles[0]; var wrapper = new XElement(root.Name.Namespace + "g", new XAttribute("id", "lens-group-label-" + group.Id));
            title.ReplaceWith(wrapper); wrapper.Add(title);
        }
        var edgeIndex = 0;
        foreach (var element in root.Descendants().Where(e => HasClass(e, "edge")).ToArray())
        { var wrapper = new XElement(root.Name.Namespace + "g", new XAttribute("id", "lens-edge-" + edgeIndex++)); element.ReplaceWith(wrapper); wrapper.Add(element); }
        var labelIdentity = 0;
        foreach (var element in root.Descendants().Where(e => HasClass(e, "edge-label"))) element.SetAttributeValue("id", "lens-label-" + labelIdentity++);
        var drawing = svg.RenderCanonical(result, root.ToString(SaveOptions.DisableFormatting), identities: true, cancellationToken: cancellationToken);
        var origin = drawing.Drawing.Bounds.Location;
        var shift = new TranslateTransform(-origin.X, -origin.Y);
        var elements = new Dictionary<string, NativeMermaidElement>(StringComparer.Ordinal);
        foreach (var node in graph.Nodes)
            Add("node:", node.Id, node.Label, new Rect(node.X, node.Y, node.Width, node.Height), "node");
        foreach (var group in Groups(graph.Groups))
            Add("group:", group.Id, group.Label, new Rect(group.X, group.Y, group.Width, group.Height), "subgraph");
        var paths = root.Descendants().Where(e => HasClass(e, "edge")).ToArray();
        var visible = graph.Edges.Select((edge, ordinal) => (edge, ordinal)).Where(item => item.edge.Style != EdgeStyle.Invisible && item.edge.Points.Count >= 2).ToArray();
        if (paths.Length != visible.Length) throw new InvalidDataException("Canonical connection metadata is incomplete. Use Edit source.");
        var labels = root.Descendants().Where(e => HasClass(e, "edge-label")).ToArray();
        var labelIndex = 0;
        var connections = new List<NativeMermaidConnection>();
        for (var index = 0; index < visible.Length; index++)
        {
            var (edge, ordinal) = visible[index]; var path = paths[index];
            ValidateEdge(path, edge);
            var geometry = Shape(path).Clone(); geometry.Transform = shift; geometry.Freeze();
            CheckBounds(geometry.Bounds);
            Rect? labelBounds = null;
            if (edge.Label is not null)
            {
                if (labelIndex >= labels.Length) throw new InvalidDataException("Canonical connection label metadata is incomplete.");
                var label = labels[labelIndex++]; ValidateEdge(label, edge);
                var rectangle = label.Descendants().FirstOrDefault(e => e.Name.LocalName == "rect");
                if (rectangle is not null) { var bounds = Shape(rectangle).Bounds; bounds.Offset(-origin.X, -origin.Y); CheckBounds(bounds); labelBounds = bounds; }
            }
            connections.Add(new(ordinal, edge.Source, edge.Target, edge.Label ?? "", geometry, labelBounds));
        }
        if (labelIndex != labels.Length) throw new InvalidDataException("Conflicting canonical connection labels.");
        return new(drawing, elements, connections.AsReadOnly(), graph) { Membership = result.FlowchartGroups,
            SourceLabels = result.FlowchartSemantics?.Nodes.ToDictionary(pair => pair.Key, pair => pair.Value.Label) ?? new Dictionary<string, string>(),
            SourceConnections = result.FlowchartSemantics?.Edges.ToArray() ?? [] };

        void Add(string prefix, string id, string label, Rect bounds, string kind)
        {
            CheckBounds(bounds);
            var candidates = root.Descendants().Where(e => HasClass(e, kind) && (string?)e.Attribute("data-id") == id).ToArray();
            if (candidates.Length != 1 || MetadataLabel((string?)candidates[0].Attribute("data-label") ?? "") != MetadataLabel(label))
                throw new InvalidDataException("Conflicting canonical element identity: " + id);
            var shapes = candidates[0].Elements().Where(e => e.Name.LocalName is "rect" or "circle" or "ellipse" or "polygon" or "path" or "line").ToArray();
            // Compound filled shapes (notably double circles) must remain clickable at their centre.
            var geometry = new GeometryGroup { FillRule = FillRule.Nonzero };
            foreach (var shape in shapes) geometry.Children.Add(Shape(shape));
            if (geometry.Children.Count == 0) throw new InvalidDataException("The canonical element has no safe hit geometry: " + id);
            geometry.Transform = shift; geometry.Freeze();
            bounds.Offset(-origin.X, -origin.Y);
            var text = candidates[0].Descendants().FirstOrDefault(element => element.Name.LocalName == "text");
            var element = new NativeMermaidElement(prefix + id, bounds, geometry)
            { Fill = Colour((string?)shapes[0].Attribute("fill")), Stroke = Colour((string?)shapes[0].Attribute("stroke")), TextColour = Colour((string?)text?.Attribute("fill")) };
            if (!elements.TryAdd(prefix + id, element)) throw new InvalidDataException("Duplicate canonical element identity: " + id);
        }
    }
    private static string? Colour(string? value) => NativeSvgService.TrySolidColour(value, out var colour) ? $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}" : null;

    // Source order is verified by the lexical adapter against Mermaider. Ordinals distinguish identical parallel edges.
    public bool Maps(MermaidDiagramModel model)
    {
        if (!model.HasCompleteSourceMap || Graph.Nodes.Count != model.Nodes.Count || Graph.Edges.Count != model.Edges.Count || SourceConnections.Count != model.Edges.Count
            || Groups(Graph.Groups).Count() != model.Subgraphs.Count) return false;
        return model.Nodes.All(node => Elements.ContainsKey("node:" + node.Id) && SourceLabels.TryGetValue(node.Id, out var label) && label == node.Label)
            && model.Subgraphs.All(group => Elements.ContainsKey("group:" + group.Id) && Membership.Any(identity => identity.Id == group.Id && identity.ParentId == group.ParentId))
            && model.Nodes.Where(node => node.SubgraphId is not null).All(node => Membership.Any(group => group.Id == node.SubgraphId && group.NodeIds.Contains(node.Id)))
            && model.Edges.Select((edge, ordinal) => Graph.Edges[ordinal].Source == edge.Source.Id && Graph.Edges[ordinal].Target == edge.Target.Id
                && SourceConnections[ordinal].Source == edge.Source.Id && SourceConnections[ordinal].Target == edge.Target.Id
                && (SourceConnections[ordinal].Label ?? "") == edge.Label && MatchesType(edge.Type, Graph.Edges[ordinal])).All(matches => matches);
    }
    private static bool MatchesType(FlowConnectionType type, PositionedEdge edge)
        => edge.Style == (type == FlowConnectionType.Dotted ? EdgeStyle.Dotted : type == FlowConnectionType.Thick ? EdgeStyle.Thick : EdgeStyle.Solid)
            && edge.HasArrowStart == (type == FlowConnectionType.Bidirectional) && edge.HasArrowEnd == (type != FlowConnectionType.Undirected);
    private static IEnumerable<PositionedGroup> Groups(IEnumerable<PositionedGroup> groups)
    { foreach (var group in groups) { yield return group; foreach (var child in Groups(group.Children)) yield return child; } }
    private static bool HasClass(XElement element, string value) => ((string?)element.Attribute("class") ?? "").Split(' ').Contains(value);
    // XML normalises literal attribute line breaks and tabs. IDs, source labels and rendered text stay untouched.
    private static string MetadataLabel(string label) => label.ReplaceLineEndings(" ").Replace('\t', ' ');
    private static void ValidateEdge(XElement element, PositionedEdge edge)
    {
        if ((string?)element.Attribute("data-from") != edge.Source || (string?)element.Attribute("data-to") != edge.Target
            || MetadataLabel((string?)element.Attribute("data-label") ?? "") != MetadataLabel(edge.Label ?? ""))
            throw new InvalidDataException("Canonical connection order or identity is ambiguous. Use Edit source.");
        if (HasClass(element, "edge") && ((string?)element.Attribute("data-style") != edge.Style.ToString().ToLowerInvariant()
            || (string?)element.Attribute("data-arrow-start") != (edge.HasArrowStart ? "true" : "false")
            || (string?)element.Attribute("data-arrow-end") != (edge.HasArrowEnd ? "true" : "false")))
            throw new InvalidDataException("Canonical connection style or arrow identity is ambiguous. Use Edit source.");
    }
    private static double Number(XElement element, string name, double fallback = 0)
    {
        var value = (string?)element.Attribute(name);
        if (value is null) return fallback;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number) || Math.Abs(number) > 1_000_000)
            throw new InvalidDataException("Invalid canonical coordinate.");
        return number;
    }
    private static Geometry Shape(XElement element)
    {
        // The pinned renderer emits absolute geometry. Fail closed if a future version adds a transform.
        if (element.AncestorsAndSelf().Any(e => e.Attribute("transform") is not null))
            throw new InvalidDataException("Unsupported canonical geometry transform. Use Edit source.");
        Geometry geometry = element.Name.LocalName switch
        {
            "rect" => new RectangleGeometry(new(Number(element, "x"), Number(element, "y"), Number(element, "width"), Number(element, "height")), Number(element, "rx"), Number(element, "ry")),
            "circle" => new EllipseGeometry(new System.Windows.Point(Number(element, "cx"), Number(element, "cy")), Number(element, "r"), Number(element, "r")),
            "ellipse" => new EllipseGeometry(new System.Windows.Point(Number(element, "cx"), Number(element, "cy")), Number(element, "rx"), Number(element, "ry")),
            "line" => new LineGeometry(new(Number(element, "x1"), Number(element, "y1")), new(Number(element, "x2"), Number(element, "y2"))),
            "path" => Geometry.Parse((string?)element.Attribute("d") ?? throw new InvalidDataException("Missing canonical path.")),
            "polygon" => Polygon((string?)element.Attribute("points") ?? ""),
            _ => throw new InvalidDataException("Unsupported canonical shape.")
        };
        CheckBounds(geometry.Bounds); return geometry;
    }
    private static Geometry Polygon(string text)
    {
        var values = text.Split([' ', ',', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        if (values.Length < 6 || values.Length > 2048 || values.Length % 2 != 0) throw new InvalidDataException("Invalid canonical polygon.");
        return Geometry.Parse("M" + string.Join(' ', values) + "Z");
    }
    private static void CheckBounds(Rect bounds)
    {
        if (bounds.IsEmpty || !double.IsFinite(bounds.X) || !double.IsFinite(bounds.Y) || !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height)
            || Math.Abs(bounds.X) > 1_000_000 || Math.Abs(bounds.Y) > 1_000_000 || bounds.Width > 1_000_000 || bounds.Height > 1_000_000)
            throw new InvalidDataException("Invalid canonical geometry bounds.");
    }
}
