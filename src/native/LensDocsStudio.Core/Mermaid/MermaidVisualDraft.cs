using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Core.Mermaid;

public sealed record MermaidDraftResult(bool Success, string? Error);
public sealed record MermaidDraftSnapshot(string Source, IReadOnlyList<string> Selection, IReadOnlyList<string> EdgeIds, long HistoryIdentity = 0);

/// <summary>Private source-based history. Failed and obsolete validation never changes the draft or its history.</summary>
public sealed class MermaidVisualDraft
{
    private readonly MermaidSourceParser parser = new();
    private readonly MermaidTransformationService transformations = new();
    private readonly DiagramService diagrams;
    private readonly List<MermaidDraftSnapshot> history = [];
    private int index, revision, nextEdgeId;
    private const int MaximumHistoryCharacters = 8_000_000;
    public MermaidDiagramModel Model { get; private set; }
    public string Source => Model.Source;
    public long HistoryIdentity { get; private set; }
    public IReadOnlyList<long> HistoryIdentities => history.Select(snapshot => snapshot.HistoryIdentity).ToArray();
    public string OriginalSource { get; }
    public bool IsDirty => Source != OriginalSource;
    public IReadOnlyList<string> Selection { get; private set; } = [];
    public bool CanUndo => index > 0;
    public bool CanRedo => index + 1 < history.Count;

    public MermaidVisualDraft(string source, DiagramService? diagramService = null)
    {
        diagrams = diagramService ?? new(); OriginalSource = source; Model = parser.Parse(source); nextEdgeId = Model.Edges.Count; history.Add(Snapshot());
    }
    public void Select(IEnumerable<string> selection)
    { Selection = selection.Distinct(StringComparer.Ordinal).Take(MermaidSourceParser.MaximumElements).ToArray(); history[index] = Snapshot(); }
    public async Task<MermaidDraftResult> ExecuteAsync(MermaidEditOperation operation, CancellationToken cancellationToken = default)
        => await ExecuteBatchAsync([operation], cancellationToken);
    public async Task<MermaidDraftResult> ExecuteBatchAsync(IReadOnlyList<MermaidEditOperation> operations, CancellationToken cancellationToken = default)
    {
        try
        {
            if (operations.Count > MermaidSourceParser.MaximumElements) return new(false, "The edit exceeds the bounded operation limit.");
            var original = Model; var generation = ++revision; var candidateNextEdge = nextEdgeId;
            var candidate = await Task.Run(() =>
            {
                var current = original;
                foreach (var operation in operations)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var changes = transformations.Plan(current, operation);
                    foreach (var patch in changes)
                        if (current.ProtectedRegions.Any(region => patch.Span.Start < region.Span.End && patch.Span.End > region.Span.Start))
                            throw new ArgumentException("A protected source region would be changed. Use Edit source.");
                    var parsed = parser.Parse(MermaidPatches.Apply(current.Source, changes), cancellationToken);
                    current = PreserveEdgeIdentities(current, parsed, changes, operation, ref candidateNextEdge);
                    if (!current.CanEdit) break;
                }
                return current;
            }, cancellationToken);
            if (generation != revision) return new(false, "A newer draft superseded this operation.");
            if (!candidate.CanEdit) return new(false, candidate.Diagnostic);
            return await ValidateAndCommitCoreAsync(candidate.Source, candidate.Edges.Select(edge => edge.Id).ToArray(), cancellationToken);
        }
        catch (ArgumentException ex) { return new(false, ex.Message); }
    }
    public async Task<MermaidDraftResult> ValidateAndCommitAsync(string candidate, CancellationToken cancellationToken = default)
        => await ValidateAndCommitCoreAsync(candidate, null, cancellationToken);
    private async Task<MermaidDraftResult> ValidateAndCommitCoreAsync(string candidate, IReadOnlyList<string>? edgeIds, CancellationToken cancellationToken)
    {
        var version = ++revision;
        var result = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested(); var parsed = parser.Parse(candidate, cancellationToken);
            if (!parsed.CanEdit) return (parsed, error: parsed.Diagnostic ?? "This source is protected.");
            cancellationToken.ThrowIfCancellationRequested(); var rendered = diagrams.Render(candidate, cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested(); return (parsed, error: rendered.Error);
        }, cancellationToken);
        if (version != revision) return new(false, "A newer draft superseded this validation.");
        if (result.error is not null) return new(false, result.error);
        if (candidate == Source) return new(true, null);
        var parsedEdges = result.parsed.Edges.Select((edge, position) => edge with { Id = edgeIds is null ? "edge-" + nextEdgeId++ : edgeIds[position] }).ToArray();
        Model = result.parsed with { Edges = parsedEdges }; HistoryIdentity = version;
        foreach (var edge in parsedEdges) if (int.TryParse(edge.Id.AsSpan(5), out var number)) nextEdgeId = Math.Max(nextEdgeId, number + 1);
        if (index + 1 < history.Count) history.RemoveRange(index + 1, history.Count - index - 1);
        Selection = Selection.Where(Exists).ToArray(); history.Add(Snapshot()); index++;
        while (history.Count > 100 || history.Sum(item => item.Source.Length) > MaximumHistoryCharacters && history.Count > 2)
        { history.RemoveAt(0); index--; }
        return new(true, null);
    }
    public void Undo() { if (CanUndo) Restore(--index); }
    public void Redo() { if (CanRedo) Restore(++index); }
    private void Restore(int value)
    {
        revision++; var snapshot = history[value]; var parsed = parser.Parse(snapshot.Source);
        Model = parsed with { Edges = parsed.Edges.Select((edge, position) => edge with { Id = snapshot.EdgeIds[position] }).ToArray() };
        Selection = snapshot.Selection.ToArray(); HistoryIdentity = snapshot.HistoryIdentity;
    }
    private MermaidDraftSnapshot Snapshot() => new(Source, Selection.ToArray(), Model.Edges.Select(edge => edge.Id).ToArray(), HistoryIdentity);
    private static MermaidDiagramModel PreserveEdgeIdentities(MermaidDiagramModel before, MermaidDiagramModel after,
        IReadOnlyList<MermaidDocumentPatch> patches, MermaidEditOperation operation, ref int nextId)
    {
        var assigned = new Dictionary<int, string>();
        var byOffset = after.Edges.Select((edge, position) => (edge.ConnectionSpan.Start, Position: position)).ToDictionary(item => item.Start, item => item.Position);
        foreach (var edge in before.Edges)
        {
            var offset = edge.ConnectionSpan.Start; var delta = 0; var removed = false;
            foreach (var patch in patches.OrderBy(patch => patch.Span.Start))
            {
                if (patch.Span.Length > 0 && offset >= patch.Span.Start && offset < patch.Span.End) { removed = true; break; }
                if (patch.Span.End <= offset) delta += patch.Replacement.Length - patch.Span.Length;
            }
            if (removed) continue;
            if (byOffset.TryGetValue(offset + delta, out var position)) assigned[position] = edge.Id;
        }
        if (operation is UpdateMermaidConnection update && !assigned.Values.Contains(update.Id))
        {
            for (var position = after.Edges.Count - 1; position >= 0; position--)
            {
                var edge = after.Edges[position];
                if (assigned.ContainsKey(position) || edge.Source.Id != update.Source || edge.Target.Id != update.Target || edge.Label != update.Label || edge.Type != update.Type) continue;
                assigned[position] = update.Id; break;
            }
        }
        var edges = new List<MermaidEdge>();
        for (var position = 0; position < after.Edges.Count; position++)
            edges.Add(after.Edges[position] with { Id = assigned.TryGetValue(position, out var id) ? id : "edge-" + nextId++ });
        return after with { Edges = edges };
    }
    private bool Exists(string id) => Model.Nodes.Any(node => "node:" + node.Id == id) || Model.Edges.Any(edge => edge.Id == id)
        || Model.Subgraphs.Any(group => "group:" + group.Id == id);
}
