using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Core.ErDiagrams;

/// <summary>ER-specific bounded private history. Only fully validated candidates become visible.</summary>
public sealed class ErVisualDraft
{
    private readonly ErDiagramSourceParser parser = new();
    private readonly ErTransformationService transformations = new();
    private readonly DiagramService diagrams;
    private readonly Func<ErDiagramModel, CancellationToken, Task>? validatePresentation;
    private sealed record Snapshot(ErDiagramModel Model, string? Selection, long Identity);
    private readonly List<Snapshot> history = [];
    private int index, generation;
    private long nextIdentity;
    public ErDiagramModel Model { get; private set; }
    public string Source => Model.Source;
    public string OriginalSource { get; }
    public string? Selection { get; private set; }
    public bool CanUndo => index > 0;
    public bool CanRedo => index + 1 < history.Count;
    public long HistoryIdentity => history[index].Identity;
    public int HistoryCount => history.Count;
    public ErVisualDraft(string source, DiagramService? diagrams = null, Func<ErDiagramModel, CancellationToken, Task>? validatePresentation = null)
    { this.diagrams = diagrams ?? new(); this.validatePresentation = validatePresentation; OriginalSource = source; Model = parser.Parse(source); history.Add(new(Model, null, 0)); }
    public void Select(string? identity) { Selection = identity; history[index] = history[index] with { Selection = identity }; }
    public async Task<ErEditResult> ExecuteAsync(ErEditOperation operation, CancellationToken cancellationToken = default)
    {
        var version = ++generation; var original = Model;
        try
        {
            var candidate = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested(); Validate(original, cancellationToken);
                var plan = transformations.Plan(original, operation);
                var parsed = parser.Parse(MermaidPatches.Apply(original.Source, plan.Patches), cancellationToken);
                if (parsed.Statements.Count != plan.Identities.Count) throw new ArgumentException("The edit changed an unplanned ER source statement.");
                parsed = Identify(parsed, plan.Identities);
                ErSemanticValidation.ValidateEdit(original, parsed, plan); Validate(parsed, cancellationToken); return parsed;
            }, cancellationToken);
            if (validatePresentation is not null) await validatePresentation(candidate, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (version != generation || !ReferenceEquals(Model, original)) return new(false, "A newer draft superseded this operation.");
            // Reordering byte-identical rows is still a real occurrence-identity edit.
            // Its private selection/history must move even though portable source is equal.
            if (candidate.Source != Source || operation is MoveErAttribute) Commit(candidate); return new(true, null);
        }
        catch (Exception error) when (Recoverable(error, cancellationToken)) { return new(false, Message(error)); }
    }
    public async Task<ErEditResult> ValidateSourceAsync(string source, CancellationToken cancellationToken = default)
    {
        var version = ++generation;
        try
        {
            var candidate = await Task.Run(() => { var parsed = parser.Parse(source, cancellationToken); Validate(parsed, cancellationToken); return parsed; }, cancellationToken);
            if (validatePresentation is not null) await validatePresentation(candidate, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (version != generation) return new(false, "A newer draft superseded source validation.");
            if (candidate.Source != Source) { Selection = null; Commit(candidate); } return new(true, null);
        }
        catch (Exception error) when (Recoverable(error, cancellationToken)) { return new(false, Message(error)); }
    }
    public async Task<ErEditResult> ValidateAsync(CancellationToken cancellationToken = default)
    {
        var version = generation; var current = Model;
        try { await Task.Run(() => Validate(current, cancellationToken), cancellationToken); if (validatePresentation is not null) await validatePresentation(current, cancellationToken); cancellationToken.ThrowIfCancellationRequested(); return version == generation ? new(true, null) : new(false, "The draft changed during validation."); }
        catch (Exception error) when (Recoverable(error, cancellationToken)) { return new(false, Message(error)); }
    }
    private void Validate(ErDiagramModel model, CancellationToken cancellationToken)
    { cancellationToken.ThrowIfCancellationRequested(); ErSemanticValidation.ValidateRendering(model, diagrams.Render(model.Source, cancellationToken: cancellationToken)); cancellationToken.ThrowIfCancellationRequested(); }
    private static bool Recoverable(Exception error, CancellationToken token) => error is not OutOfMemoryException
        && (error is not OperationCanceledException || token.IsCancellationRequested);
    private static string Message(Exception error) => error is OperationCanceledException ? "ER validation was cancelled; the draft is unchanged." : error.Message;
    private void Commit(ErDiagramModel candidate)
    {
        Model = candidate;
        if (!Model.Entities.Any(item => item.Identity == Selection) && !Model.Relationships.Any(item => item.Identity == Selection) && !Model.Entities.SelectMany(item => item.Attributes).Any(item => item.Identity == Selection)) Selection = null;
        if (index + 1 < history.Count) history.RemoveRange(index + 1, history.Count - index - 1);
        history.Add(new(Model, Selection, ++nextIdentity)); index++;
        while (history.Count > 100 || history.Count > 2 && history.Sum(item => item.Model.Source.Length) > 8_000_000) { history.RemoveAt(0); index--; }
    }
    public void Undo() { generation++; if (CanUndo) Restore(--index); }
    public void Redo() { generation++; if (CanRedo) Restore(++index); }
    private void Restore(int position) { Model = history[position].Model; Selection = history[position].Selection; }
    private static ErDiagramModel Identify(ErDiagramModel parsed, IReadOnlyList<string> identities)
    {
        var mapping = parsed.Statements.Select((item, index) => (item.Identity, Next: item with { Identity = identities[index] })).ToDictionary(item => item.Identity, item => item.Next);
        return parsed with
        {
            Statements = parsed.Statements.Select(item => mapping[item.Identity]).ToArray(),
            Entities = parsed.Entities.Select(item => item with {
                Declarations = item.Declarations.Select(declaration => mapping[declaration.Identity]).ToArray(),
                Attributes = item.Attributes.Select(attribute => attribute with { Identity = mapping[attribute.Identity].Identity, Statement = mapping[attribute.Identity] }).ToArray() }).ToArray(),
            Relationships = parsed.Relationships.Select(item => item with { Identity = mapping[item.Identity].Identity, Statement = mapping[item.Identity] }).ToArray()
        };
    }
}
