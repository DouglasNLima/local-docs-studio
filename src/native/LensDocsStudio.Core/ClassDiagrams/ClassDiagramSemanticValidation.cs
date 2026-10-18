using System.Text.Json;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Rendering;
using Mermaider.Models;

namespace LensDocsStudio.Core.ClassDiagrams;

public static class ClassDiagramSemanticValidation
{
    public static bool Equal(ClassDiagram first, ClassDiagram second) => JsonSerializer.Serialize(first) == JsonSerializer.Serialize(second);
    public static void ValidateRendering(ClassDiagramModel model, DiagramResult render)
    {
        using var diagnosticPhase = LensDocsStudio.Core.Rendering.MermaidDiagnostics.Measure("semantic.validation.Class");
        if (!model.IsValid) throw new ArgumentException(model.Diagnostics.FirstOrDefault() ?? "Repair invalid Class source using Edit source.");
        if (!render.Success || render.Source != model.Source || render.ClassSemantics is null || render.ClassLayout is null || !Equal(model.Semantic, render.ClassSemantics))
            throw new ArgumentException(render.Error ?? "The complete pinned Class semantics cannot be reconciled with the original source. Use Edit source.");
        var layout = render.ClassLayout;
        // Lollipop may split a declared target into a synthetic circle. Its canonical
        // rendering is retained, but exact structural authoring is protected.
        if (model.Relationships.Any(item => item.Semantic.Type == ClassRelationType.Lollipop)) return;
        if (layout.Classes.Count != model.Classes.Count || layout.Classes.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != model.Classes.Count
            || layout.Relationships.Count != model.Relationships.Count) Reject();
        foreach (var cls in layout.Classes)
        {
            var expected = model.Class(cls.Id).Semantic;
            var actual = new ClassNode { Id = cls.Id, Label = cls.Label, Annotation = cls.Annotation, Attributes = cls.Attributes, Methods = cls.Methods };
            if (JsonSerializer.Serialize(expected) != JsonSerializer.Serialize(actual)) Reject();
        }
        _ = RelationshipOrder(model, layout);
    }
    public static IReadOnlyList<ClassDiagramRelationship> RelationshipOrder(ClassDiagramModel model, PositionedClassDiagram layout)
    {
        // Pinned LightweightClassLayoutEngine maps OriginalIndex in exact input order,
        // including self-loops and duplicates. Compare the complete tuple before mapping.
        if (model.Relationships.Count != layout.Relationships.Count) Reject();
        for (var i = 0; i < model.Relationships.Count; i++)
        {
            var value = layout.Relationships[i];
            if (model.Relationships[i].Semantic != new ClassRelationship(value.From, value.To, value.Type, value.MarkerAt, value.Label, value.FromCardinality, value.ToCardinality)) Reject();
        }
        return model.Relationships;
    }
    internal static void ValidateEdit(ClassDiagramModel before, ClassDiagramModel after, ClassEditPlan plan)
    {
        if (plan.Revision != before.Revision || !after.IsValid || MermaidPatches.Apply(before.Source, plan.Patches) != after.Source || !Equal(plan.Expected, after.Semantic)) Reject();
        foreach (var statement in before.Statements.Where(item => item.Protected))
        {
            var retained = after.Statements.SingleOrDefault(item => item.Identity == statement.Identity);
            if (retained is null || retained.Span.Read(after.Source) != statement.Span.Read(before.Source) || !retained.Protected) Reject();
        }
    }
    private static void Reject() => throw new ArgumentException("Complete Class semantics, occurrence order, namespace membership or relationship marker/cardinality ownership does not match. The draft is unchanged.");
}
