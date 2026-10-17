namespace LensDocsStudio.Core.Mermaid;

public readonly record struct MermaidSourceSpan(int Start, int Length)
{
    public int End => Start + Length;
    public string Read(string source) => source.Substring(Start, Length);
}

public enum FlowNodeShape { Process, Decision, Rounded, Terminal, Circle, DoubleCircle, Subroutine, Cylinder, Hexagon }
public enum FlowConnectionType { Directed, Undirected, Dotted, Thick, Bidirectional }
public enum MermaidCompatibility { Editable, PartiallyEditable, Protected, Invalid }

public sealed record MermaidOccurrence(string Id, MermaidSourceSpan IdSpan, MermaidSourceSpan Span,
    MermaidSourceSpan? LabelSpan, FlowNodeShape Shape, MermaidSourceSpan Statement, string? SubgraphId, string? ClassName);
public sealed record MermaidNode(string Id, string Label, FlowNodeShape Shape, IReadOnlyList<MermaidOccurrence> Occurrences)
{
    // The pinned parser honours the first occurrence, including an implicit bare node.
    public MermaidOccurrence? Declaration => Occurrences.FirstOrDefault() is { LabelSpan: not null } first ? first : null;
    public string? SubgraphId => Occurrences.Select(item => item.SubgraphId).FirstOrDefault(id => id is not null);
}
public sealed record MermaidEdge(string Id, MermaidOccurrence Source, MermaidOccurrence Target,
    string Label, FlowConnectionType Type, MermaidSourceSpan ConnectionSpan, MermaidSourceSpan? LabelSpan,
    MermaidSourceSpan Statement)
{
    public bool InlineLabel { get; init; }
}
public sealed record MermaidSubgraph(string Id, string Label, string? ParentId, MermaidSourceSpan Opening,
    MermaidSourceSpan Heading, MermaidSourceSpan Closing, MermaidSourceSpan? DirectionSpan, string? Direction);
public sealed record MermaidStyle(string Kind, string Value, IReadOnlyList<(string Id, MermaidSourceSpan Span)> Targets,
    MermaidSourceSpan Statement);
public sealed record MermaidProtectedRegion(MermaidSourceSpan Span, string Reason);

/// <summary>The original source and its verified editable spans. Rendering never supplies editing identities.</summary>
public sealed record MermaidDiagramModel(string Source, string Direction, MermaidSourceSpan DirectionSpan,
    IReadOnlyList<MermaidNode> Nodes, IReadOnlyList<MermaidEdge> Edges, IReadOnlyList<MermaidSubgraph> Subgraphs,
    IReadOnlyList<MermaidStyle> Styles, IReadOnlyList<MermaidProtectedRegion> ProtectedRegions,
    MermaidCompatibility Compatibility, string? Diagnostic)
{
    public string DiagramType { get; init; } = "Flowchart";
    public bool CanEdit => Compatibility == MermaidCompatibility.Editable;
    public bool HasSafeTransformations => Compatibility is MermaidCompatibility.Editable or MermaidCompatibility.PartiallyEditable;
    public bool HasCompleteSourceMap { get; init; }
    public MermaidCapabilityAnalysis Capabilities => MermaidCapabilityAnalysis.For(this);
    public string NewLine => Source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
    public string Indentation
    {
        get
        {
            var first = Nodes.SelectMany(node => node.Occurrences).FirstOrDefault();
            if (first is null) return "    ";
            var line = Source.LastIndexOf('\n', Math.Max(0, first.Statement.Start - 1));
            var prefix = Source[(line + 1)..first.Statement.Start];
            return prefix.All(character => character is ' ' or '\t') ? prefix : "    ";
        }
    }
    public MermaidNode Node(string id) => Nodes.FirstOrDefault(node => node.Id == id)
        ?? throw new ArgumentException("Select an existing node.");
    public MermaidEdge Edge(string id) => Edges.FirstOrDefault(edge => edge.Id == id)
        ?? throw new ArgumentException("Select an existing connection.");
    public MermaidSubgraph Subgraph(string id) => Subgraphs.FirstOrDefault(group => group.Id == id)
        ?? throw new ArgumentException("Select an existing subgraph.");
    public string? ClassForNode(string id) => Styles.Where(style => style.Kind == "class" && style.Targets.Any(target => target.Id == id))
        .Select(style => (Start: style.Statement.Start, Name: style.Value))
        .Concat(Node(id).Occurrences.Where(item => item.ClassName is not null).Select(item => (Start: item.Span.Start, Name: item.ClassName!)))
        .OrderBy(item => item.Start).LastOrDefault().Name;
    public string EffectiveDirection(string? groupId)
    {
        while (groupId is not null)
        { var group = Subgraph(groupId); if (group.Direction is { } value) return value; groupId = group.ParentId; }
        return Direction;
    }
}

public sealed record MermaidDocumentPatch(MermaidSourceSpan Span, string Expected, string Replacement);

public static class MermaidPatches
{
    public static string Apply(string source, IEnumerable<MermaidDocumentPatch> changes)
    {
        var patches = changes.OrderByDescending(patch => patch.Span.Start).ThenByDescending(patch => patch.Span.Length).ToArray();
        var boundary = source.Length;
        int? previousStart = null;
        foreach (var patch in patches)
        {
            if (patch.Span.Start < 0 || patch.Span.Length < 0 || patch.Span.End > boundary
                || previousStart == patch.Span.Start
                || patch.Span.Read(source) != patch.Expected)
                throw new ArgumentException("The diagram changed or its source edits overlap. Reopen the current source.");
            boundary = patch.Span.Start;
            previousStart = patch.Span.Start;
        }
        var result = new System.Text.StringBuilder(source);
        foreach (var patch in patches) result.Remove(patch.Span.Start, patch.Span.Length).Insert(patch.Span.Start, patch.Replacement);
        return result.ToString();
    }
}
