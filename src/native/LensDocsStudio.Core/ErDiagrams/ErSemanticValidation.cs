using System.Text.Json;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Rendering;
using Mermaider.Models;

namespace LensDocsStudio.Core.ErDiagrams;

public static class ErSemanticValidation
{
    public static bool Equal(ErDiagram first, ErDiagram second) => JsonSerializer.Serialize(first) == JsonSerializer.Serialize(second);
    public static void ValidateRendering(ErDiagramModel model, DiagramResult render)
    {
        if (!model.IsValid || !render.Success || render.Source != model.Source || render.ErSemantics is null || render.ErLayout is null || !Equal(model.Semantic, render.ErSemantics))
            throw new ArgumentException(render.Error ?? "The complete pinned ER semantics cannot be reconciled with every source occurrence. Use Edit source.");
        var layout = render.ErLayout;
        if (layout.Entities.Count != model.Entities.Count || layout.Entities.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != model.Entities.Count
            || layout.Relationships.Count != model.Relationships.Count) Reject();
        foreach (var entity in layout.Entities)
        {
            var source = model.Entity(entity.Id);
            if (entity.Label != source.Label || JsonSerializer.Serialize(entity.Attributes) != JsonSerializer.Serialize(source.Attributes.Select(item => item.Semantic))) Reject();
        }
        _ = RelationshipOrder(model, layout);
    }
    public static IReadOnlyList<ErRelationshipModel> RelationshipOrder(ErDiagramModel model, PositionedErDiagram layout)
    {
        // Verified at 71b6484: ordinary edges in input order, then self-loops in input order.
        // Verify every complete tuple, including duplicate occurrences, before allowing hit targets.
        var ordered = model.Relationships.Where(item => item.Entity1 != item.Entity2).Concat(model.Relationships.Where(item => item.Entity1 == item.Entity2)).ToArray();
        if (ordered.Length != layout.Relationships.Count) Reject();
        for (var i = 0; i < ordered.Length; i++)
        { var positioned = layout.Relationships[i]; if (ordered[i].Semantic != new ErRelationship(positioned.Entity1, positioned.Entity2, positioned.Cardinality1, positioned.Cardinality2, positioned.Label, positioned.Identifying)) Reject(); }
        return Array.AsReadOnly(ordered);
    }
    internal static void ValidateEdit(ErDiagramModel before, ErDiagramModel after, ErEditPlan plan)
    {
        if (plan.Revision != before.Revision || !after.IsValid || MermaidPatches.Apply(before.Source, plan.Patches) != after.Source || !Equal(plan.Expected, after.Semantic)) Reject();
        foreach (var statement in before.Statements.Where(item => item.Protected))
        {
            var retained = after.Statements.SingleOrDefault(item => item.Identity == statement.Identity);
            if (retained is null || retained.Span.Read(after.Source) != statement.Span.Read(before.Source) || !retained.Protected) Reject();
        }
    }
    private static void Reject() => throw new ArgumentException("The complete ER source, endpoint cardinalities, occurrence order or canonical layout does not match the requested edit. The draft is unchanged.");
}
