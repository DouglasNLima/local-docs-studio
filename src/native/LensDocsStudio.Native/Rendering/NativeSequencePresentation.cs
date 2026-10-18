using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.Sequence;

namespace LensDocsStudio.Native.Rendering;

public sealed record NativeSequenceTarget(string Identity, string Description, Rect Bounds);
/// <summary>Hit bounds from verified canonical SVG primitives. Drawing pixels are the unchanged preview conversion.</summary>
public sealed record NativeSequencePresentation(DrawingImage Drawing, IReadOnlyList<NativeSequenceTarget> Targets, IReadOnlyList<string> Diagnostics)
{
    public static NativeSequencePresentation Create(SequenceDiagramModel model, DiagramResult render, NativeSvgService svg) => Create(model, render, svg, default);
    public static NativeSequencePresentation Create(SequenceDiagramModel model, DiagramResult render, NativeSvgService svg, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var xml = SequenceRenderCorrespondence.Validate(model, render);
        // Use exactly the same sanitisation/conversion as the FlowDocument preview.
        var drawing = svg.RenderCanonical(render, svg.Prepare(render.Svg!), cancellationToken: cancellationToken); var origin = drawing.Drawing.Bounds.Location;
        var root = xml.Root!; var targets = new List<NativeSequenceTarget>(); var diagnostics = new List<string>();
        var blocks = SequenceRenderCorrespondence.Elements(root, "block").ToList();
        var sourceBlocks = model.Statements.Where(item => item.Kind == SequenceStatementKind.Block).ToArray();
        foreach (var block in sourceBlocks.Reverse())
        {
            if (sourceBlocks.Count(item => item.Read(model.Source, "type") == block.Read(model.Source, "type")
                && SequenceSourceParser.NormaliseLabel(item.Read(model.Source, "label")).ReplaceLineEndings(" ") == SequenceSourceParser.NormaliseLabel(block.Read(model.Source, "label")).ReplaceLineEndings(" ")) > 1)
            { diagnostics.Add("Repeated block headings have no unique canvas identity. Inspect their exact occurrences in the interaction list."); continue; }
            var element = blocks.FirstOrDefault(item => (string?)item.Attribute("data-type") == block.Read(model.Source, "type")
                && ((string?)item.Attribute("data-label") ?? "") == SequenceSourceParser.NormaliseLabel(block.Read(model.Source, "label")).ReplaceLineEndings(" "));
            if (element is null) continue; blocks.Remove(element);
            Add(element, block.Identity, "Protected " + block.Read(model.Source, "type") + " block");
        }
        var actors = SequenceRenderCorrespondence.Elements(root, "actor").Where(item => !SequenceRenderCorrespondence.HasClass(item, "actor-bottom")).ToArray();
        for (var index = 0; index < actors.Length; index++) Add(actors[index], "participant:" + model.Participants[index].Id, "Participant " + model.Participants[index].Label);
        var lifelines = SequenceRenderCorrespondence.Elements(root, "lifeline").ToArray();
        foreach (var line in lifelines)
        {
            var id = (string?)line.Attribute("data-actor");
            if (model.Participants.Any(item => item.Id == id)) Add(line, "participant:" + id, "Lifeline " + id);
        }
        var activations = SequenceRenderCorrespondence.Elements(root, "activation").ToArray();
        // Activation bars are linked only when the participant has exactly one simple lifecycle.
        foreach (var bar in activations)
        {
            var candidates = model.Activations.Where(item => item.Activate && item.Participant == (string?)bar.Attribute("data-actor")).ToArray();
            if (candidates.Length == 1) Add(bar, candidates[0].Identity, "Activation " + candidates[0].Participant);
            else diagnostics.Add("Several activation lifecycles share a participant. Inspect each triggering message or command in the interaction list.");
        }
        var messages = SequenceRenderCorrespondence.Elements(root, "message").ToArray();
        for (var index = 0; index < messages.Length; index++) Add(messages[index], model.Messages[index].Identity, "Message " + (index + 1) + ": " + model.Messages[index].Text);
        var notes = SequenceRenderCorrespondence.Elements(root, "sequence-note").ToArray();
        for (var index = 0; index < notes.Length; index++) Add(notes[index], model.Notes[index].Identity, "Note: " + model.Notes[index].Text);
        return new(drawing, targets.AsReadOnly(), diagnostics.Distinct().ToArray());

        void Add(XElement element, string identity, string description)
        {
            var bounds = Rect.Empty;
            foreach (var shape in element.DescendantsAndSelf())
            {
                // Ignore the actor's decorative transformed glyph; its enclosing canonical box is authoritative.
                if (shape.AncestorsAndSelf().TakeWhile(item => item != root).Any(item => item.Attribute("transform") is not null)) continue;
                Rect part;
                switch (shape.Name.LocalName)
                {
                    case "rect": part = new(Number(shape, "x"), Number(shape, "y"), Number(shape, "width"), Number(shape, "height")); break;
                    case "line": part = new(new Point(Number(shape, "x1"), Number(shape, "y1")), new Point(Number(shape, "x2"), Number(shape, "y2"))); part.Inflate(7, 7); break;
                    case "polyline": case "polygon":
                        part = Geometry.Parse("M" + ((string?)shape.Attribute("points") ?? "") + (shape.Name.LocalName == "polygon" ? "Z" : "")).Bounds; part.Inflate(5, 5); break;
                    default: continue;
                }
                bounds.Union(part);
            }
            if (bounds.IsEmpty) return;
            bounds.Offset(-origin.X, -origin.Y);
            if (!double.IsFinite(bounds.X) || !double.IsFinite(bounds.Y) || Math.Abs(bounds.X) > 1_000_000 || Math.Abs(bounds.Y) > 1_000_000
                || !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height) || bounds.Width > 1_000_000 || bounds.Height > 1_000_000)
                throw new InvalidDataException("The canonical Sequence hit target is outside the geometry budget.");
            targets.Add(new(identity, description, bounds));
        }
    }
    private static double Number(XElement element, string name)
    {
        if (!double.TryParse((string?)element.Attribute(name) ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value) || Math.Abs(value) > 1_000_000)
            throw new InvalidDataException("Invalid canonical Sequence coordinate.");
        return value;
    }
}
