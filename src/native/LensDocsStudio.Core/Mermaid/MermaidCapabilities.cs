using System.Runtime.CompilerServices;

namespace LensDocsStudio.Core.Mermaid;

public enum MermaidOperationKind
{
    Inspect, NodeLabel, NodeShape, NodeId, DeleteNode, DuplicateNode, AddNode, NodeClass, NodeStyle,
    MoveNode, ConnectionLabel, ConnectionType, Reconnect, DeleteConnection, AddConnection,
    SubgraphLabel, AddSubgraph, DeleteSubgraph, Direction, ClassDefinition
}

public sealed record MermaidOperationCapability(string? Target, MermaidOperationKind Kind, bool Permitted,
    IReadOnlyList<MermaidSourceSpan> SourceSpans, IReadOnlyList<MermaidSourceSpan> ProtectedDependencies,
    string Diagnostic)
{
    public string ValidationRequirements => "Exact expected-text patches, complete parser semantic delta, protected-source preservation and bounded canonical rendering.";
}

/// <summary>Revision-local dependency inventory. Unknown statements block transformations, never imply independence.</summary>
public sealed class MermaidCapabilityAnalysis
{
    private static readonly ConditionalWeakTable<MermaidDiagramModel, MermaidCapabilityAnalysis> Cache = new();
    private readonly MermaidDiagramModel model;
    private readonly HashSet<MermaidSourceSpan> chains;
    public bool HasOrdinalStyles { get; }
    public bool DependenciesKnown => model.HasCompleteSourceMap;
    internal static MermaidCapabilityAnalysis For(MermaidDiagramModel model) => Cache.GetValue(model, value => new(value));
    private MermaidCapabilityAnalysis(MermaidDiagramModel model)
    {
        this.model = model;
        chains = model.Edges.GroupBy(edge => edge.Statement).Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet();
        HasOrdinalStyles = model.ProtectedRegions.Any(region => region.Span.Read(model.Source).StartsWith("linkStyle", StringComparison.Ordinal));
    }
    public MermaidOperationCapability Query(MermaidOperationKind kind, string? target = null)
    {
        var nodeKind = kind is MermaidOperationKind.Inspect or MermaidOperationKind.NodeLabel or MermaidOperationKind.NodeShape or MermaidOperationKind.NodeId
            or MermaidOperationKind.DeleteNode or MermaidOperationKind.DuplicateNode or MermaidOperationKind.NodeClass or MermaidOperationKind.NodeStyle or MermaidOperationKind.MoveNode;
        var edgeKind = kind is MermaidOperationKind.Inspect or MermaidOperationKind.ConnectionLabel or MermaidOperationKind.ConnectionType or MermaidOperationKind.Reconnect or MermaidOperationKind.DeleteConnection;
        var groupKind = kind is MermaidOperationKind.Inspect or MermaidOperationKind.SubgraphLabel or MermaidOperationKind.DeleteSubgraph or MermaidOperationKind.Direction;
        var node = nodeKind ? model.Nodes.FirstOrDefault(item => item.Id == target) : null;
        var edge = edgeKind ? model.Edges.FirstOrDefault(item => item.Id == target) : null;
        var group = groupKind ? model.Subgraphs.FirstOrDefault(item => item.Id == target) : null;
        var spans = node is not null ? node.Occurrences.Select(item => item.Span).ToArray()
            : edge is not null ? new[] { edge.Statement } : group is not null ? new[] { group.Opening, group.Closing } : [];
        var dependencies = node is not null ? node.Occurrences.Where(item => chains.Contains(item.Statement)).Select(item => item.Statement).Distinct().ToArray()
            : edge is not null && chains.Contains(edge.Statement) ? new[] { edge.Statement } : [];
        string? reason = null;
        if (kind == MermaidOperationKind.Inspect)
            return new(target, kind, target is null || node is not null || edge is not null || group is not null, spans, dependencies, "Canonical identities permit inspection independently of source transformation permission.");
        if (!DependenciesKnown || !model.HasSafeTransformations) reason = model.Diagnostic ?? "Source dependencies cannot be completely established.";
        else
        {
            switch (kind)
            {
                case MermaidOperationKind.NodeLabel or MermaidOperationKind.NodeShape:
                    if (node is null) reason = "Select an existing node.";
                    else if (node.Occurrences.Count(item => item.LabelSpan is not null) > 1)
                        reason = "Repeated declarations are protected. The first occurrence remains authoritative.";
                    break;
                case MermaidOperationKind.NodeId or MermaidOperationKind.DeleteNode or MermaidOperationKind.MoveNode:
                    if (node is null) reason = "Select an existing node.";
                    else if (dependencies.Length > 0) reason = "This node is referenced by a protected chain. Its identity, deletion and membership require source editing.";
                    else if (node.Occurrences.Any(item => item.LabelSpan is not null && item != node.Declaration)) reason = "Repeated declarations protect this node's identity and membership.";
                    else if (kind == MermaidOperationKind.MoveNode && node.Occurrences.Any(item => item.SubgraphId is not null && model.Edges.Any(connection => connection.Statement == item.Statement)))
                        reason = "Move this node's connections outside subgraphs in source mode first. Its existing membership is preserved.";
                    else if (kind == MermaidOperationKind.DeleteNode && HasOrdinalStyles) reason = "Deleting incident connections could shift ordinal linkStyle assignments.";
                    break;
                case MermaidOperationKind.NodeClass or MermaidOperationKind.NodeStyle or MermaidOperationKind.DuplicateNode:
                    if (node is null) reason = "Select an existing node.";
                    break;
                case MermaidOperationKind.ConnectionLabel or MermaidOperationKind.ConnectionType:
                    if (edge is null) reason = "Select an existing connection.";
                    break;
                case MermaidOperationKind.Reconnect or MermaidOperationKind.DeleteConnection:
                    if (edge is null) reason = "Select an existing connection.";
                    else if (dependencies.Length > 0) reason = "A single chain connection cannot be removed or reconnected without rewriting its statement.";
                    else if (HasOrdinalStyles) reason = "Changing connection order could shift ordinal linkStyle assignments.";
                    else if (kind == MermaidOperationKind.Reconnect && edge.Source.SubgraphId is not null || kind == MermaidOperationKind.Reconnect && edge?.Target.SubgraphId is not null)
                        reason = "Reconnecting an internal group statement could change membership. Use source editing.";
                    break;
                case MermaidOperationKind.AddConnection:
                    if (HasOrdinalStyles) reason = "Connection insertion is protected while ordinal or default linkStyle assignments exist.";
                    else if (model.Nodes.Count + model.Edges.Count + model.Subgraphs.Count >= MermaidSourceParser.MaximumElements) reason = "The diagram has reached the 500-element editing budget.";
                    break;
                case MermaidOperationKind.AddNode:
                    if (model.Nodes.Count >= MermaidSourceParser.MaximumNodes || model.Nodes.Count + model.Edges.Count + model.Subgraphs.Count >= MermaidSourceParser.MaximumElements)
                        reason = "The diagram has reached its bounded node or element editing budget.";
                    break;
                case MermaidOperationKind.SubgraphLabel:
                    if (group is null) reason = "Select an existing subgraph.";
                    break;
                case MermaidOperationKind.Direction or MermaidOperationKind.DeleteSubgraph:
                    if (!model.CanEdit) reason = "Group structure and direction remain protected in partially editable diagrams.";
                    break;
                case MermaidOperationKind.AddSubgraph:
                    if (!model.CanEdit) reason = "Add a unique independent node; group insertion remains protected in a partially editable diagram.";
                    break;
            }
        }
        return new(target, kind, reason is null, spans, dependencies, reason ?? "Editable after complete candidate validation.");
    }
    public IReadOnlyList<MermaidOperationCapability> ForOperation(MermaidEditOperation operation) => operation switch
    {
        UpdateMermaidNode update => NodeUpdates(update),
        AddMermaidNode => [Query(MermaidOperationKind.AddNode)],
        DeleteMermaidNodes delete => delete.Ids.Select(id => Query(MermaidOperationKind.DeleteNode, id)).ToArray(),
        AddMermaidConnection => [Query(MermaidOperationKind.AddConnection)],
        UpdateMermaidConnection update => EdgeUpdates(update),
        DeleteMermaidConnection delete => [Query(MermaidOperationKind.DeleteConnection, delete.Id)],
        SetMermaidStyle style => [Query(MermaidOperationKind.NodeStyle, style.Id)],
        SetMermaidStyleProperties style => [Query(MermaidOperationKind.NodeStyle, style.Id)],
        SetMermaidClass style => [Query(MermaidOperationKind.NodeClass, style.Id)],
        MoveMermaidNode move => [Query(MermaidOperationKind.MoveNode, move.Id)],
        RenameMermaidSubgraph rename => [Query(MermaidOperationKind.SubgraphLabel, rename.Id)],
        AddMermaidSubgraph => [Query(MermaidOperationKind.AddSubgraph)],
        DeleteMermaidSubgraph delete => [Query(MermaidOperationKind.DeleteSubgraph, delete.Id)],
        ChangeMermaidDirection direction => [Query(MermaidOperationKind.Direction, direction.SubgraphId)],
        AddMermaidClassDefinition => [Query(MermaidOperationKind.ClassDefinition)],
        _ => [new(null, MermaidOperationKind.Inspect, false, [], [], "This operation has no source capability proof.")]
    };
    public void Require(MermaidEditOperation operation)
    {
        // Even a no-op request must establish a valid identity and known baseline.
        if (!DependenciesKnown || !model.HasSafeTransformations) throw new ArgumentException(model.Diagnostic ?? "Source dependencies are unknown.");
        var denied = ForOperation(operation).FirstOrDefault(capability => !capability.Permitted);
        if (denied is not null) throw new ArgumentException(denied.Diagnostic);
    }
    private IReadOnlyList<MermaidOperationCapability> NodeUpdates(UpdateMermaidNode update)
    {
        var node = model.Node(update.Id); var results = new List<MermaidOperationCapability>();
        if (update.Label != node.Label) results.Add(Query(MermaidOperationKind.NodeLabel, update.Id));
        if (update.Shape != node.Shape) results.Add(Query(MermaidOperationKind.NodeShape, update.Id));
        if (update.NewId != node.Id) results.Add(Query(MermaidOperationKind.NodeId, update.Id));
        return results;
    }
    private IReadOnlyList<MermaidOperationCapability> EdgeUpdates(UpdateMermaidConnection update)
    {
        var edge = model.Edge(update.Id); var results = new List<MermaidOperationCapability>();
        if (update.Label != edge.Label) results.Add(Query(MermaidOperationKind.ConnectionLabel, update.Id));
        if (update.Type != edge.Type) results.Add(Query(MermaidOperationKind.ConnectionType, update.Id));
        if (update.Source != edge.Source.Id || update.Target != edge.Target.Id) results.Add(Query(MermaidOperationKind.Reconnect, update.Id));
        return results;
    }
}
