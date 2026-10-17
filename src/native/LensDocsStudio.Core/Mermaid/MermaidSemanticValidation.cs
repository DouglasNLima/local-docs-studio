using System.Text.Json;
using System.Text.Json.Nodes;
using Mermaider;

namespace LensDocsStudio.Core.Mermaid;

/// <summary>Compares the complete pinned-parser graph, allowing only the requested semantic delta.
/// JSON is an internal comparison format, never an authoring or Mermaid serialisation format.</summary>
internal static class MermaidSemanticValidation
{
    private static readonly JsonSerializerOptions Options = new() { IncludeFields = true };
    internal static bool RenderingAgrees(MermaidDiagramModel model, Rendering.DiagramResult rendered)
    {
        var graph = rendered.FlowchartSemantics;
        if (!rendered.Success || graph is null || graph.Nodes.Count != model.Nodes.Count || graph.Edges.Count != model.Edges.Count
            || rendered.FlowchartGroups.Count != model.Subgraphs.Count) return false;
        var nodes = graph.Nodes;
        return model.Nodes.All(node => nodes.TryGetValue(node.Id, out var shown) && shown.Label == node.Label && shown.Shape.ToString() == MermaidSourceParser.RendererShape(node.Shape))
            && model.Edges.Select((edge, index) => graph.Edges[index].Source == edge.Source.Id && graph.Edges[index].Target == edge.Target.Id
                && (graph.Edges[index].Label ?? "") == edge.Label
                && graph.Edges[index].Style == (edge.Type == FlowConnectionType.Dotted ? Mermaider.Models.EdgeStyle.Dotted : edge.Type == FlowConnectionType.Thick ? Mermaider.Models.EdgeStyle.Thick : Mermaider.Models.EdgeStyle.Solid)
                && graph.Edges[index].HasArrowStart == (edge.Type == FlowConnectionType.Bidirectional) && graph.Edges[index].HasArrowEnd == (edge.Type != FlowConnectionType.Undirected)).All(value => value)
            && model.Subgraphs.All(group => rendered.FlowchartGroups.Any(identity => identity.Id == group.Id && identity.ParentId == group.ParentId
                && identity.NodeIds.SequenceEqual(model.Nodes.Where(node => node.Occurrences.Any(item => item.SubgraphId == group.Id))
                    .OrderBy(node => node.Occurrences.First(item => item.SubgraphId == group.Id).Span.Start).Select(node => node.Id))));
    }
    public static void Validate(MermaidDiagramModel before, MermaidDiagramModel after, MermaidEditOperation operation,
        IReadOnlyList<MermaidDocumentPatch> patches, CancellationToken cancellationToken)
    {
        before.Capabilities.Require(operation);
        if (!after.HasCompleteSourceMap || !after.HasSafeTransformations)
            throw new ArgumentException(after.Diagnostic ?? "The candidate has unknown semantic dependencies.");
        if (MermaidPatches.Apply(before.Source, patches) != after.Source)
            throw new ArgumentException("The candidate does not match its expected-text source patches.");
        ProtectSource(before, operation, patches);
        cancellationToken.ThrowIfCancellationRequested();
        var expected = JsonSerializer.SerializeToNode(MermaidRenderer.Parse(before.Source), Options)!.AsObject();
        var actual = JsonSerializer.SerializeToNode(MermaidRenderer.Parse(after.Source), Options)!.AsObject();
        cancellationToken.ThrowIfCancellationRequested();
        var expectedEdges = before.Edges.Select((edge, index) => (edge.Id, Value: expected["Edges"]![index]!.DeepClone()))
            .ToDictionary(item => item.Id, item => item.Value, StringComparer.Ordinal);
        var edgeOrder = before.Edges.Select(edge => edge.Id).ToList();
        switch (operation)
        {
            case UpdateMermaidNode update:
            {
                var old = before.Node(update.Id); var next = after.Node(update.NewId);
                if (next.Label != update.Label || next.Shape != update.Shape) Reject();
                if (update.NewId != update.Id) RenameNode(update.Id, update.NewId);
                var node = expected["Nodes"]![update.NewId]!;
                if (old.Label != update.Label) { node["Label"] = actual["Nodes"]![update.NewId]!["Label"]!.DeepClone(); node["IsMarkdown"] = false; }
                if (old.Shape != update.Shape) node["Shape"] = actual["Nodes"]![update.NewId]!["Shape"]!.DeepClone();
                break;
            }
            case AddMermaidNode add:
                if (after.Node(add.Id).Label != add.Label || after.Node(add.Id).Shape != add.Shape) Reject();
                expected["Nodes"]![add.Id] = actual["Nodes"]![add.Id]!.DeepClone();
                if (add.SubgraphId is not null) FindGroup(expected, add.SubgraphId)!["NodeIds"]!.AsArray().Add(add.Id);
                AdoptNodeOrder([add.Id]);
                if (expected["ClassDefs"]!["default"] is not null) expected["ClassAssignments"]![add.Id] = "default";
                break;
            case DeleteMermaidNodes delete:
                RemoveNodes(delete.Ids); break;
            case UpdateMermaidConnection update:
            {
                var old = before.Edge(update.Id); var next = after.Edge(update.Id);
                if (next.Source.Id != update.Source || next.Target.Id != update.Target || next.Label != update.Label || next.Type != update.Type) Reject();
                var edge = expectedEdges[update.Id]; var candidate = actual["Edges"]![after.Edges.ToList().FindIndex(item => item.Id == update.Id)]!;
                if (old.Label != update.Label) edge["Label"] = candidate["Label"]?.DeepClone();
                if (old.Type != update.Type)
                    foreach (var field in new[] { "Style", "HasArrowStart", "HasArrowEnd" }) edge[field] = candidate[field]!.DeepClone();
                if (old.Source.Id != update.Source || old.Target.Id != update.Target)
                { edge["Source"] = update.Source; edge["Target"] = update.Target; edgeOrder.Remove(update.Id); edgeOrder.Add(update.Id); }
                break;
            }
            case DeleteMermaidConnection delete:
                expectedEdges.Remove(delete.Id); edgeOrder.Remove(delete.Id); break;
            case AddMermaidConnection add:
            {
                var added = after.Edges.Where(edge => !expectedEdges.ContainsKey(edge.Id)).ToArray();
                if (added.Length != 1 || added[0].Source.Id != add.Source || added[0].Target.Id != add.Target || added[0].Label != add.Label || added[0].Type != add.Type) Reject();
                var id = added[0].Id; expectedEdges.Add(id, actual["Edges"]![after.Edges.ToList().FindIndex(edge => edge.Id == id)]!.DeepClone()); edgeOrder.Add(id); break;
            }
            case SetMermaidStyle style:
                MergeStyle("NodeStyles", style.Id, $"fill:{style.Fill},stroke:{style.Stroke},color:{style.Colour}"); break;
            case SetMermaidStyleProperties style:
                MergeStyle("NodeStyles", style.Id, style.Properties); break;
            case SetMermaidClass assign:
                expected["ClassAssignments"]![assign.Id] = assign.ClassName; break;
            case AddMermaidClassDefinition definition:
                MergeStyle("ClassDefs", definition.ClassName, definition.Properties); break;
            case RenameMermaidSubgraph rename:
                if (after.Subgraph(rename.Id).Label != rename.Label) Reject();
                FindGroup(expected, rename.Id)!["Label"] = FindGroup(actual, rename.Id)!["Label"]!.DeepClone(); break;
            case ChangeMermaidDirection direction:
                if (direction.SubgraphId is null) expected["Direction"] = actual["Direction"]!.DeepClone();
                else FindGroup(expected, direction.SubgraphId)!["Direction"] = FindGroup(actual, direction.SubgraphId)!["Direction"]!.DeepClone();
                break;
            case MoveMermaidNode move:
                foreach (var group in AllGroups(expected)) RemoveId(group["NodeIds"]!.AsArray(), move.Id);
                if (move.SubgraphId is not null) FindGroup(expected, move.SubgraphId)!["NodeIds"]!.AsArray().Add(move.Id);
                AdoptNodeOrder([move.Id]); break;
            case AddMermaidSubgraph add:
                GroupArray(expected, add.ParentId).Add(FindGroup(actual, add.Id)!.DeepClone()); break;
            case DeleteMermaidSubgraph delete:
            {
                var old = before.Subgraph(delete.Id); var group = FindGroup(expected, delete.Id)!; var parent = GroupArray(expected, old.ParentId);
                if (delete.DeleteContents)
                {
                    var descendants = before.Subgraphs.Where(item => item.Opening.Start >= old.Opening.Start && item.Closing.End <= old.Closing.End).Select(item => item.Id).ToHashSet();
                    RemoveNodes(before.Nodes.Where(node => node.Occurrences.Any(item => item.SubgraphId is not null && descendants.Contains(item.SubgraphId))).Select(node => node.Id).ToArray());
                }
                else
                {
                    if (old.ParentId is not null)
                        foreach (var id in group["NodeIds"]!.AsArray().Select(value => value!.GetValue<string>()))
                            if (!FindGroup(expected, old.ParentId)!["NodeIds"]!.AsArray().Any(value => value!.GetValue<string>() == id)) FindGroup(expected, old.ParentId)!["NodeIds"]!.AsArray().Add(id);
                    foreach (var child in group["Children"]!.AsArray()) parent.Add(child!.DeepClone());
                }
                parent.Remove(group); AdoptNodeOrder(before.Nodes.Select(node => node.Id).ToArray()); break;
            }
            default: throw new ArgumentException("This operation has no complete semantic validation rule.");
        }
        if (!edgeOrder.SequenceEqual(after.Edges.Select(edge => edge.Id))) Reject();
        expected["Edges"] = new JsonArray(edgeOrder.Select(id => expectedEdges[id].DeepClone()).ToArray());
        if (!JsonNode.DeepEquals(expected, actual))
            throw new ArgumentException("The complete Mermaid semantics changed beyond the requested operation. The source and history were preserved.");

        void MergeStyle(string field, string id, string properties)
        {
            var target = expected[field]![id] as JsonObject ?? new JsonObject();
            if (target.Parent is null) expected[field]![id] = target;
            foreach (var property in properties.TrimEnd(';').Split(','))
            { var parts = property.Split(':', 2, StringSplitOptions.TrimEntries); target[parts[0]] = parts[1]; }
        }
        void RenameNode(string oldId, string newId)
        {
            var nodes = expected["Nodes"]!.AsObject(); var value = nodes[oldId]!.DeepClone(); nodes.Remove(oldId); value["Id"] = newId; nodes[newId] = value;
            foreach (var edge in expectedEdges.Values)
                foreach (var field in new[] { "Source", "Target" }) if (edge[field]!.GetValue<string>() == oldId) edge[field] = newId;
            RenameId(expected["NodeOrder"]!.AsArray(), oldId, newId);
            foreach (var group in AllGroups(expected)) RenameId(group["NodeIds"]!.AsArray(), oldId, newId);
            foreach (var field in new[] { "ClassAssignments", "NodeStyles" })
            { var map = expected[field]!.AsObject(); if (map[oldId] is { } item) { var copy = item.DeepClone(); map.Remove(oldId); map[newId] = copy; } }
        }
        void RemoveNodes(IReadOnlyList<string> ids)
        {
            foreach (var id in ids)
            {
                expected["Nodes"]!.AsObject().Remove(id); expected["ClassAssignments"]!.AsObject().Remove(id); expected["NodeStyles"]!.AsObject().Remove(id);
                RemoveId(expected["NodeOrder"]!.AsArray(), id);
                foreach (var group in AllGroups(expected)) RemoveId(group["NodeIds"]!.AsArray(), id);
            }
            foreach (var edge in before.Edges.Where(edge => ids.Contains(edge.Source.Id) || ids.Contains(edge.Target.Id)))
            { expectedEdges.Remove(edge.Id); edgeOrder.Remove(edge.Id); }
        }
        void AdoptNodeOrder(IReadOnlyList<string> changed)
        {
            var prior = expected["NodeOrder"]!.AsArray().Select(value => value!.GetValue<string>()).Where(id => !changed.Contains(id));
            var next = actual["NodeOrder"]!.AsArray().Select(value => value!.GetValue<string>()).Where(id => !changed.Contains(id));
            if (!prior.SequenceEqual(next)) Reject();
            expected["NodeOrder"] = actual["NodeOrder"]!.DeepClone();
        }
    }
    private static void ProtectSource(MermaidDiagramModel model, MermaidEditOperation operation, IReadOnlyList<MermaidDocumentPatch> patches)
    {
        foreach (var region in model.ProtectedRegions)
            foreach (var patch in patches.Where(patch => patch.Span.Start < region.Span.End && patch.Span.End > region.Span.Start))
            {
                // Only explicitly authorised, exact property patches may touch a chain statement.
                var property = operation is UpdateMermaidNode or UpdateMermaidConnection;
                var chain = model.Edges.Count(edge => edge.Statement == region.Span) > 1;
                if (!property || !chain || patch.Span.Start < region.Span.Start || patch.Span.End > region.Span.End)
                    throw new ArgumentException("This operation would modify protected source.");
            }
    }
    private static IEnumerable<JsonObject> AllGroups(JsonObject graph) => Descendants(graph["Subgraphs"]!.AsArray());
    private static IEnumerable<JsonObject> Descendants(JsonArray groups)
    {
        foreach (var group in groups.Cast<JsonObject>())
        { yield return group; foreach (var child in Descendants(group["Children"]!.AsArray())) yield return child; }
    }
    private static JsonObject? FindGroup(JsonObject graph, string id) => AllGroups(graph).FirstOrDefault(group => group["Id"]!.GetValue<string>() == id);
    private static JsonArray GroupArray(JsonObject graph, string? parent) => parent is null ? graph["Subgraphs"]!.AsArray() : FindGroup(graph, parent)!["Children"]!.AsArray();
    private static void RemoveId(JsonArray ids, string id) { for (var i = ids.Count - 1; i >= 0; i--) if (ids[i]!.GetValue<string>() == id) ids.RemoveAt(i); }
    private static void RenameId(JsonArray ids, string before, string after) { for (var i = 0; i < ids.Count; i++) if (ids[i]!.GetValue<string>() == before) ids[i] = after; }
    private static void Reject() => throw new ArgumentException("The candidate does not have the requested semantic identities and values.");
}
