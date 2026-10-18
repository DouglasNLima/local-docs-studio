using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;
using LensDocsStudio.Core.ClassDiagrams;
using LensDocsStudio.Core.Rendering;
using Mermaider.Models;
using Point = System.Windows.Point;

namespace LensDocsStudio.Native.Rendering;

public sealed record NativeClassHit(string Identity, Rect Bounds);
public sealed record NativeClassConnection(string Identity, Geometry Path, Rect? LabelBounds, Point First, Point Second);
/// <summary>The actual canonical drawing with independently verified occurrence overlays. Never lays out classes.</summary>
public sealed record NativeClassPresentation(DrawingImage Drawing, IReadOnlyList<NativeClassHit> Classes,
    IReadOnlyList<NativeClassHit> Members, IReadOnlyList<NativeClassConnection> Relationships, IReadOnlyList<NativeClassHit> Containers)
{
    public static NativeClassPresentation Create(ClassDiagramModel model, DiagramResult render, NativeSvgService svg)
    {
        ClassDiagramSemanticValidation.ValidateRendering(model, render);
        var safe = svg.Prepare(render.Svg!);
        using var reader = XmlReader.Create(new StringReader(safe), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 8_000_000 });
        var root = XDocument.Load(reader).Root!;
        if (root.Descendants().Count() > 30_000) throw new InvalidDataException("The canonical Class drawing exceeds the interaction geometry budget.");
        if (model.Classes.Count == 0)
        { var blank = new DrawingImage(new DrawingGroup()); blank.Freeze(); return new(blank, [], [], [], []); }
        var drawing = svg.Render(render.Svg!); var origin = drawing.Drawing.Bounds.Location; var layout = render.ClassLayout!;
        var classes = new List<NativeClassHit>(); var members = new List<NativeClassHit>(); var relationships = new List<NativeClassConnection>(); var containers = new List<NativeClassHit>();
        // Preserve the full canonical lollipop drawing. Its synthetic layout identities
        // deliberately have no editable hit regions; the source list remains inspectable.
        if (model.Relationships.Any(item => item.Semantic.Type == ClassRelationType.Lollipop)) return new(drawing, [], [], [], []);
        var boxes = root.Descendants().Where(item => HasClass(item, "class-node")).ToArray();
        if (boxes.Length != model.Classes.Count) Reject();
        foreach (var positioned in layout.Classes)
        {
            var source = model.Class(positioned.Id); var box = boxes.SingleOrDefault(item => Value(item, "data-id") == positioned.Id);
            if (box is null || Value(box, "data-label") != source.Label || Value(box, "data-annotation") != (source.Annotation ?? "")) Reject();
            var frame = box!.Elements().FirstOrDefault(item => item.Name.LocalName == "rect");
            if (frame is null || Math.Abs(Number(frame, "x") - positioned.X) > .1 || Math.Abs(Number(frame, "y") - positioned.Y) > .1 || Math.Abs(Number(frame, "width") - positioned.Width) > .1 || Math.Abs(Number(frame, "height") - positioned.Height) > .1) Reject();
            var bounds = new Rect(positioned.X - origin.X, positioned.Y - origin.Y, positioned.Width, positioned.HeaderHeight); Check(bounds); classes.Add(new(source.Identity, bounds));
            void Rows(IReadOnlyList<ClassDiagramMember> rows, double top, double height)
            {
                var rowHeight = rows.Count == 0 ? 0 : Math.Min(28, height / rows.Count);
                if (rows.Count > 0 && rowHeight <= 0) Reject();
                for (var i = 0; i < rows.Count; i++)
                {
                    var row = rows[i]; var semantic = row.Semantic; var centre = top + i * rowHeight + rowHeight / 2;
                    var name = semantic.IsMethod ? semantic.Name + "(" + semantic.Params + ")" : semantic.Name;
                    // Validate the renderer's real text centre and occurrence order. Multi-line
                    // or unfamiliar packed rows stay list-only; never guess a source occurrence.
                    var text = box.Descendants().Where(item => item.Name.LocalName == "text" && item.Attribute("y") is not null && Math.Abs(Number(item, "y") - centre) < .1 && item.Value == name).ToArray();
                    if (name.Contains('\n') || text.Length != 1 || text[0].Elements().Any()) continue;
                    var rowBounds = new Rect(bounds.X, centre - rowHeight / 2 - origin.Y, positioned.Width, rowHeight); Check(rowBounds); members.Add(new(row.Identity, rowBounds));
                }
            }
            Rows(source.Attributes, positioned.Y + positioned.HeaderHeight, positioned.AttrHeight);
            Rows(source.Methods, positioned.Y + positioned.HeaderHeight + positioned.AttrHeight, positioned.MethodHeight);
        }
        var ordered = ClassDiagramSemanticValidation.RelationshipOrder(model, layout);
        var paths = root.Descendants().Where(item => HasClass(item, "class-relationship")).ToArray();
        var labels = root.Descendants().Where(item => HasClass(item, "edge-label") && item.Attribute("data-from") is not null).ToArray(); var labelIndex = 0;
        if (paths.Length != ordered.Count) Reject();
        for (var i = 0; i < ordered.Count; i++)
        {
            var source = ordered[i]; var semantic = source.Semantic; var positioned = layout.Relationships[i]; var path = paths[i];
            if (Value(path, "data-from") != semantic.From || Value(path, "data-to") != semantic.To || Value(path, "data-type") != semantic.Type.ToString().ToLowerInvariant()
                || Value(path, "data-marker-at") != semantic.MarkerAt.ToString().ToLowerInvariant() || Value(path, "data-label") != (semantic.Label ?? "") || positioned.Points.Count < 2) Reject();
            var geometry = Geometry.Parse(Value(path, "d")).Clone(); geometry.Transform = new TranslateTransform(-origin.X, -origin.Y); geometry.Freeze(); Check(geometry.Bounds);
            var first = new Point(positioned.Points[0].X - origin.X, positioned.Points[0].Y - origin.Y); var second = new Point(positioned.Points[^1].X - origin.X, positioned.Points[^1].Y - origin.Y);
            var flat = geometry.GetFlattenedPathGeometry(); flat.GetPointAtFractionLength(0, out var start, out _); flat.GetPointAtFractionLength(1, out var end, out _);
            if ((start - first).Length > .1 || (end - second).Length > .1) Reject();
            Rect? labelBounds = null;
            if (semantic.Label is not null || semantic.FromCardinality is not null || semantic.ToCardinality is not null)
            {
                if (labelIndex >= labels.Length) Reject(); var label = labels[labelIndex++];
                if (Value(label, "data-from") != semantic.From || Value(label, "data-to") != semantic.To) Reject();
                if (semantic.Label is not null)
                {
                    var rect = label.Descendants().FirstOrDefault(item => item.Name.LocalName == "rect");
                    if (rect is not null) { labelBounds = new Rect(Number(rect, "x") - origin.X, Number(rect, "y") - origin.Y, Number(rect, "width"), Number(rect, "height")); Check(labelBounds.Value); }
                }
            }
            relationships.Add(new(source.Identity, geometry, labelBounds, first, second));
        }
        if (labelIndex != labels.Length) Reject();
        if (layout.Namespaces.Count != model.Namespaces.Count) Reject();
        for (var i = 0; i < layout.Namespaces.Count; i++)
        {
            var positioned = layout.Namespaces[i]; var source = model.Namespaces[i];
            if (positioned.Name != source.Name || !root.Descendants().Any(item => HasClass(item, "ns-box") && Value(item, "data-id") == source.Name)) Reject();
            var bounds = new Rect(positioned.X - origin.X, positioned.Y - origin.Y, positioned.Width, 28); Check(bounds); containers.Add(new(source.Identity, bounds));
        }
        // Notes are inspectable from their exact source occurrence in the accessible list.
        // No fabricated canvas target is supplied when renderer note identity is absent.
        return new(drawing, classes.AsReadOnly(), members.AsReadOnly(), relationships.AsReadOnly(), containers.AsReadOnly());
    }
    private static void Check(Rect bounds) { if (bounds.IsEmpty || !double.IsFinite(bounds.X + bounds.Y + bounds.Width + bounds.Height) || Math.Abs(bounds.X) > 100_000 || Math.Abs(bounds.Y) > 100_000 || bounds.Width > 100_000 || bounds.Height > 100_000) Reject(); }
    private static string Value(XElement item, string name) => (string?)item.Attribute(name) ?? "";
    private static double Number(XElement item, string name) => double.Parse(Value(item, name), CultureInfo.InvariantCulture);
    private static bool HasClass(XElement item, string value) => Value(item, "class").Split(' ').Contains(value);
    [System.Diagnostics.CodeAnalysis.DoesNotReturn] private static void Reject() => throw new InvalidDataException("Canonical Class metadata or source occurrence geometry cannot be verified. Use the exact element list or Edit source.");
}
