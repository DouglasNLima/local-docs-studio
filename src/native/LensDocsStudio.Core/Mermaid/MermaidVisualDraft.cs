using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Core.Mermaid;

public sealed record MermaidDraftResult(bool Success, string? Error);
public sealed record MermaidDraftSnapshot(string Source, IReadOnlyList<string> Selection);

/// <summary>Private source-based history. Failed and obsolete validation never changes the draft or its history.</summary>
public sealed class MermaidVisualDraft
{
    private readonly MermaidSourceParser parser = new();
    private readonly MermaidTransformationService transformations = new();
    private readonly DiagramService diagrams;
    private readonly List<MermaidDraftSnapshot> history = [];
    private int index, revision;
    private const int MaximumHistoryCharacters = 8_000_000;
    public MermaidDiagramModel Model { get; private set; }
    public string Source => Model.Source;
    public string OriginalSource { get; }
    public bool IsDirty => Source != OriginalSource;
    public IReadOnlyList<string> Selection { get; private set; } = [];
    public bool CanUndo => index > 0;
    public bool CanRedo => index + 1 < history.Count;

    public MermaidVisualDraft(string source, DiagramService? diagramService = null)
    {
        diagrams = diagramService ?? new(); OriginalSource = source; Model = parser.Parse(source); history.Add(new(source, []));
    }
    public void Select(IEnumerable<string> selection)
    { Selection = selection.Distinct(StringComparer.Ordinal).Take(MermaidSourceParser.MaximumElements).ToArray(); history[index] = new(Source, Selection); }
    public async Task<MermaidDraftResult> ExecuteAsync(MermaidEditOperation operation, CancellationToken cancellationToken = default)
    {
        try
        {
            var changes = transformations.Plan(Model, operation);
            return await ValidateAndCommitAsync(MermaidPatches.Apply(Source, changes), cancellationToken);
        }
        catch (ArgumentException ex) { return new(false, ex.Message); }
    }
    public async Task<MermaidDraftResult> ValidateAndCommitAsync(string candidate, CancellationToken cancellationToken = default)
    {
        var version = ++revision;
        var result = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested(); var parsed = parser.Parse(candidate, cancellationToken);
            if (!parsed.CanEdit) return (parsed, error: parsed.Diagnostic ?? "This source is protected.");
            cancellationToken.ThrowIfCancellationRequested(); var rendered = diagrams.Render(candidate);
            cancellationToken.ThrowIfCancellationRequested(); return (parsed, error: rendered.Error);
        }, cancellationToken);
        if (version != revision) return new(false, "A newer draft superseded this validation.");
        if (result.error is not null) return new(false, result.error);
        if (candidate == Source) return new(true, null);
        Model = result.parsed;
        if (index + 1 < history.Count) history.RemoveRange(index + 1, history.Count - index - 1);
        Selection = Selection.Where(Exists).ToArray(); history.Add(new(candidate, Selection)); index++;
        while (history.Count > 100 || history.Sum(item => item.Source.Length) > MaximumHistoryCharacters && history.Count > 2)
        { history.RemoveAt(0); index--; }
        return new(true, null);
    }
    public void Undo() { if (CanUndo) Restore(--index); }
    public void Redo() { if (CanRedo) Restore(++index); }
    private void Restore(int value) { revision++; var snapshot = history[value]; Model = parser.Parse(snapshot.Source); Selection = snapshot.Selection.ToArray(); }
    private bool Exists(string id) => Model.Nodes.Any(node => "node:" + node.Id == id) || Model.Edges.Any(edge => edge.Id == id)
        || Model.Subgraphs.Any(group => "group:" + group.Id == id);
}
