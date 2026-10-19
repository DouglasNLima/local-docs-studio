using System.Text.Json;
using System.Text.Json.Nodes;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Rendering;
using Mermaider;
using Mermaider.Models;
using MermaidSubgraph = Mermaider.Models.MermaidSubgraph;

namespace LensDocsStudio.Core.StateDiagrams;

/// <summary>Checks the entire pinned parser graph, including protected notes, styles, hierarchy and redirections.</summary>
public static class StateSemanticValidation
{
    private static readonly JsonSerializerOptions Options = new() { IncludeFields = true };
    private static MermaidGraph Graph(StateDiagramModel model)
    {
        MermaidGraph graph;
        using (MermaidDiagnostics.Measure("renderer.parse.state-validation")) graph = MermaidRenderer.Parse(model.Source);
        return MermaidEntityLayoutProvider.DecodeSemantics(graph);
    }
    private static JsonObject Json(MermaidGraph graph) => JsonSerializer.SerializeToNode(graph, Options)!.AsObject();
    public static void ValidateRendering(StateDiagramModel model, DiagramResult render)
    {
        using var diagnosticPhase = LensDocsStudio.Core.Rendering.MermaidDiagnostics.Measure("semantic.validation.State");
        if (!model.IsValid || !render.Success || render.Source != model.Source || render.FlowchartSemantics is null || render.FlowchartLayout is null)
            throw new ArgumentException(render.Error ?? string.Join(" ", model.Diagnostics) + " The canonical State rendering is unavailable.");
        var graph = Graph(model);
        if (!JsonNode.DeepEquals(Json(graph), Json(render.FlowchartSemantics))) Reject();
        if (graph.Nodes.Count != model.Nodes.Count + model.PseudoNodes.Count || graph.Edges.Count != model.Transitions.Count
            || render.FlowchartLayout.Edges.Count != graph.Edges.Count) Reject();
        foreach (var node in model.Nodes)
        {
            var shape = node.Kind == "state" ? NodeShape.Rounded : node.Kind == "choice" ? NodeShape.Diamond : NodeShape.ForkJoin;
            if (!graph.Nodes.TryGetValue(node.Id, out var parsed) || parsed.Label != node.Label || parsed.Shape != shape) Reject();
        }
        foreach (var pseudo in model.PseudoNodes)
            if (!graph.Nodes.TryGetValue(pseudo.RendererId, out var parsed) || parsed.Shape != (pseudo.Initial ? NodeShape.StateStart : NodeShape.StateEnd)) Reject();
        for (var i = 0; i < model.Transitions.Count; i++)
        {
            var item = model.Transitions[i]; var edge = graph.Edges[i]; var positioned = render.FlowchartLayout.Edges[i];
            if (positioned.Source != edge.Source || positioned.Target != edge.Target || positioned.Label != edge.Label || positioned.Style != edge.Style
                || positioned.HasArrowStart != edge.HasArrowStart || positioned.HasArrowEnd != edge.HasArrowEnd) Reject();
            var source = model.RendererId(item.Source); var target = model.RendererId(item.Destination);
            if (graph.SubgraphEdgeRedirections.TryGetValue(i, out var redirect))
            { source = redirect.SourceSubgraph is null ? source : edge.Source; target = redirect.TargetSubgraph is null ? target : edge.Target; }
            if (edge.Source != source || edge.Target != target || (edge.Label ?? "") != item.Label) Reject();
        }
        var groups = Flatten(graph.Subgraphs, null).ToArray();
        if (groups.Length != model.Groups.Count) Reject();
        foreach (var group in model.Groups)
        {
            var parsed = groups.SingleOrDefault(item => item.Group.Id == group.Id);
            if (parsed.Group is null || parsed.Parent != group.Parent || parsed.Group.Label != group.Label) Reject();
            var members = model.Nodes.Where(node => node.References.Any(item => item.Scope == group.Id)).Select(node => node.Id)
                .Concat(model.PseudoNodes.Where(item => item.Scope == group.Id).Select(item => item.RendererId)).Order();
            if (!members.SequenceEqual(parsed.Group.NodeIds.Order())) Reject();
        }
    }
    private static IEnumerable<(MermaidSubgraph Group, string? Parent)> Flatten(IEnumerable<MermaidSubgraph> groups, string? parent)
    { foreach (var group in groups) { yield return (group, parent); foreach (var child in Flatten(group.Children, group.Id)) yield return child; } }
    internal static void ValidateEdit(StateDiagramModel before, StateDiagramModel after, StateEditOperation operation, StateEditPlan plan)
    {
        if (!after.IsValid || after.HasUnknownDependencies || MermaidPatches.Apply(before.Source, plan.Patches) != after.Source) Reject();
        var expected = Json(Graph(before)); var actual = Json(Graph(after));
        var nodes = expected["Nodes"]!.AsObject();
        var order = expected["NodeOrder"]!.AsArray();
        void Rename(string from, string to)
        {
            var node = nodes[from]!.DeepClone(); nodes.Remove(from); node["Id"] = to; nodes[to] = node;
            for (var i = 0; i < order.Count; i++) if (order[i]!.GetValue<string>() == from) order[i] = to;
            foreach (var edge in expected["Edges"]!.AsArray())
            { if (edge!["Source"]!.GetValue<string>() == from) edge["Source"] = to; if (edge["Target"]!.GetValue<string>() == from) edge["Target"] = to; }
        }
        void AddNode(string id)
        { if (nodes.ContainsKey(id) || actual["Nodes"]![id] is null) Reject(); nodes[id] = actual["Nodes"]![id]!.DeepClone(); order.Add(id); }
        void RemoveNode(string id)
        { nodes.Remove(id); var entry = order.FirstOrDefault(item => item!.GetValue<string>() == id); if (entry is not null) order.Remove(entry); }
        switch (operation)
        {
            case UpdateStateNode update:
                if (after.Node(update.NewId).Label != update.Label) Reject();
                if (update.Id != update.NewId) Rename(update.Id, update.NewId);
                nodes[update.NewId]!["Label"] = update.Label;
                // A previously implicit state's declaration moves only that state's first occurrence.
                if (before.Node(update.Id).Declaration is null && (before.Node(update.Id).Label != update.Label || update.Id != update.NewId))
                {
                    var newOrder = actual["NodeOrder"]!.AsArray();
                    if (!order.Where(item => item!.GetValue<string>() != update.NewId).Select(item => item!.GetValue<string>())
                        .SequenceEqual(newOrder.Where(item => item!.GetValue<string>() != update.NewId).Select(item => item!.GetValue<string>()))) Reject();
                    expected["NodeOrder"] = newOrder.DeepClone();
                }
                break;
            case AddStateNode add:
                if (after.Node(add.Id).Label != add.Label || after.Node(add.Id).Kind != "state") Reject(); AddNode(add.Id); break;
            case DuplicateStateNode duplicate:
                if (after.Node(duplicate.NewId).Label != before.Node(duplicate.Id).Label) Reject(); AddNode(duplicate.NewId); break;
            case DeleteStateNode delete: RemoveNode(delete.Id); break;
            case UpdateStateTransition update:
            {
                var index = before.Transitions.ToList().FindIndex(item => item.Identity == update.Identity);
                var next = after.Transitions.Single(item => item.Identity == update.Identity);
                if (!SameEndpoint(after, next.Source, update.Source, true) || !SameEndpoint(after, next.Destination, update.Destination, false) || next.Label != update.Label) Reject();
                var edge = expected["Edges"]![index]!; var candidate = actual["Edges"]![index]!;
                if (before.Transitions[index].Source != next.Source) edge["Source"] = candidate["Source"]!.DeepClone();
                if (before.Transitions[index].Destination != next.Destination) edge["Target"] = candidate["Target"]!.DeepClone();
                if (before.Transitions[index].Label != next.Label) edge["Label"] = candidate["Label"]?.DeepClone();
                break;
            }
            case AddStateTransition add:
                var added = after.Transitions[^1];
                if (!SameEndpoint(after, added.Source, add.Source, true) || !SameEndpoint(after, added.Destination, add.Destination, false) || added.Label != add.Label) Reject();
                expected["Edges"]!.AsArray().Add(actual["Edges"]!.AsArray()[^1]!.DeepClone()); break;
            case DeleteStateTransition delete:
                expected["Edges"]!.AsArray().RemoveAt(before.Transitions.ToList().FindIndex(item => item.Identity == delete.Identity)); break;
            case UpdateStateGroupLabel update:
                if (after.Groups.Single(item => item.Id == update.Id).Label != update.Label) Reject();
                SetGroupLabel(expected["Subgraphs"]!.AsArray(), update.Id, update.Label); break;
            default: Reject(); break;
        }
        if (operation is AddStateTransition or UpdateStateTransition or DeleteStateTransition)
        {
            // Only pseudo-nodes may appear/disappear as a consequence of changing a transition.
            foreach (var removed in before.PseudoNodes.Where(item => after.PseudoNodes.All(next => next.Identity != item.Identity))) RemoveNode(removed.RendererId);
            foreach (var added in after.PseudoNodes.Where(item => before.PseudoNodes.All(old => old.Identity != item.Identity))) AddNode(added.RendererId);
            if (!order.Where(item => !item!.GetValue<string>().StartsWith('_')).Select(item => item!.GetValue<string>())
                .SequenceEqual(actual["NodeOrder"]!.AsArray().Where(item => !item!.GetValue<string>().StartsWith('_')).Select(item => item!.GetValue<string>()))) Reject();
            expected["NodeOrder"] = actual["NodeOrder"]!.DeepClone();
        }
        if (!JsonNode.DeepEquals(expected, actual)) Reject();
    }
    private static bool SameEndpoint(StateDiagramModel model, string actual, string requested, bool initial) => actual == requested
        || requested == "[*]" && model.PseudoNodes.Any(item => item.Identity == actual && item.Initial == initial && item.Scope is null);
    private static void SetGroupLabel(JsonArray groups, string id, string label)
    { foreach (var group in groups) { if (group!["Id"]!.GetValue<string>() == id) group["Label"] = label; SetGroupLabel(group["Children"]!.AsArray(), id, label); } }
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Reject() => throw new ArgumentException("The complete State semantics do not match the requested edit or canonical rendering. The draft is unchanged; use Edit source.");
}
