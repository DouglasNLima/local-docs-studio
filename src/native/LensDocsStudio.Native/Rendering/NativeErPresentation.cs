using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;
using LensDocsStudio.Core.ErDiagrams;
using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Native.Rendering;

public sealed record NativeErHit(string Identity, Rect Bounds);
public sealed record NativeErConnection(string Identity, Geometry Path, Rect? LabelBounds, Point First, Point Second);
/// <summary>Canonical drawing and verified hit geometry from one renderer-owned revision. Imported SVG never grants editing authority.</summary>
public sealed record NativeErPresentation(DrawingImage Drawing, IReadOnlyList<NativeErHit> Entities,
    IReadOnlyList<NativeErHit> Attributes, IReadOnlyList<NativeErConnection> Relationships)
{
    public static NativeErPresentation Create(ErDiagramModel model, DiagramResult render, NativeSvgService svg)
    {
        ErSemanticValidation.ValidateRendering(model, render);
        var safe = svg.Prepare(render.Svg!);
        using var reader = XmlReader.Create(new StringReader(safe), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 8_000_000 });
        var root = XDocument.Load(reader).Root!;
        if (root.Descendants().Count() > 30_000) throw new InvalidDataException("The canonical ER drawing exceeds the interaction geometry budget.");
        if (model.Entities.Count == 0)
        {
            if (root.Descendants().Any(item => Class(item, "entity") || Class(item, "er-relationship") || Class(item, "edge-label"))) Reject();
            // An empty canonical diagram has no pixels or hit targets. Retain its valid
            // source draft so deleting the last independent entity remains reversible.
            var blank = new DrawingImage(new DrawingGroup()); blank.Freeze(); return new(blank, [], [], []);
        }
        var drawing = svg.Render(render.Svg!); var origin = drawing.Drawing.Bounds.Location;
        var entities = new List<NativeErHit>(); var attributes = new List<NativeErHit>(); var connections = new List<NativeErConnection>();
        var boxes = root.Descendants().Where(item => Class(item, "entity")).ToArray();
        if (boxes.Length != model.Entities.Count) Reject();
        foreach (var entity in render.ErLayout!.Entities)
        {
            var source = model.Entity(entity.Id); var box = boxes.SingleOrDefault(item => Value(item, "data-id") == entity.Id);
            if (box is null || Value(box, "data-label") != entity.Label) Reject();
            var bounds = new Rect(entity.X - origin.X, entity.Y - origin.Y, entity.Width, entity.Height); Check(bounds);
            entities.Add(new(source.Identity, bounds));
            for (var row = 0; row < source.Attributes.Count; row++)
            {
                var rowBounds = new Rect(bounds.X, bounds.Y + entity.HeaderHeight + row * entity.RowHeight, bounds.Width, entity.RowHeight); Check(rowBounds);
                attributes.Add(new(source.Attributes[row].Identity, rowBounds));
            }
        }
        var ordered = ErSemanticValidation.RelationshipOrder(model, render.ErLayout);
        var paths = root.Descendants().Where(item => Class(item, "er-relationship")).ToArray();
        var labels = root.Descendants().Where(item => Class(item, "edge-label")).ToArray(); var labelIndex = 0;
        if (paths.Length != ordered.Count) Reject();
        for (var i = 0; i < ordered.Count; i++)
        {
            var source = ordered[i]; var path = paths[i]; var positioned = render.ErLayout.Relationships[i];
            if (Value(path, "data-entity1") != source.Entity1 || Value(path, "data-entity2") != source.Entity2
                || Value(path, "data-cardinality1") != source.Cardinality1.ToString().ToLowerInvariant() || Value(path, "data-cardinality2") != source.Cardinality2.ToString().ToLowerInvariant()
                || Value(path, "data-identifying") != (source.Identifying ? "true" : "false") || Value(path, "data-label") != source.Label || positioned.Points.Count < 2) Reject();
            var geometry = Geometry.Parse(Value(path, "d")).Clone(); geometry.Transform = new TranslateTransform(-origin.X, -origin.Y); geometry.Freeze(); Check(geometry.Bounds);
            var first = new Point(positioned.Points[0].X - origin.X, positioned.Points[0].Y - origin.Y); var second = new Point(positioned.Points[^1].X - origin.X, positioned.Points[^1].Y - origin.Y);
            var flat = geometry.GetFlattenedPathGeometry(); flat.GetPointAtFractionLength(0, out var start, out _); flat.GetPointAtFractionLength(1, out var end, out _);
            if ((start - first).Length > .1 || (end - second).Length > .1) Reject();
            Rect? labelBounds = null;
            if (source.Label.Length > 0)
            {
                if (labelIndex >= labels.Length || Value(labels[labelIndex], "data-label") != source.Label) Reject();
                var label = labels[labelIndex++]; var rect = label.Descendants().FirstOrDefault(item => item.Name.LocalName == "rect");
                if (rect is null) Reject(); labelBounds = new Rect(Number(rect!, "x") - origin.X, Number(rect!, "y") - origin.Y, Number(rect!, "width"), Number(rect!, "height")); Check(labelBounds.Value);
            }
            connections.Add(new(source.Identity, geometry, labelBounds, first, second));
        }
        if (labelIndex != labels.Length) Reject();
        return new(drawing, entities.AsReadOnly(), attributes.AsReadOnly(), connections.AsReadOnly());
    }
    private static void Check(Rect bounds) { if (bounds.IsEmpty || !double.IsFinite(bounds.X + bounds.Y + bounds.Width + bounds.Height) || Math.Abs(bounds.X) > 100_000 || Math.Abs(bounds.Y) > 100_000 || bounds.Width > 100_000 || bounds.Height > 100_000) Reject(); }
    private static string Value(XElement item, string name) => (string?)item.Attribute(name) ?? "";
    private static double Number(XElement item, string name) => double.Parse(Value(item, name), CultureInfo.InvariantCulture);
    private static bool Class(XElement item, string value) => Value(item, "class").Split(' ').Contains(value);
    [System.Diagnostics.CodeAnalysis.DoesNotReturn] private static void Reject() => throw new InvalidDataException("Canonical ER metadata or occurrence geometry cannot be verified. Use the exact element list or Edit source.");
}
