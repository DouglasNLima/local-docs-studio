using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Core.StateDiagrams;

/// <summary>State-specific bounded private history. Only fully validated candidates become visible.</summary>
public sealed class StateVisualDraft
{
    private readonly StateDiagramSourceParser parser = new();
    private readonly StateTransformationService transformations = new();
    private readonly DiagramService diagrams;
    private readonly Func<StateDiagramModel, CancellationToken, Task>? validatePresentation;
    private sealed record Snapshot(StateDiagramModel Model, string? Selection, long Identity);
    private readonly List<Snapshot> history = [];
    private int index, generation;
    private long nextIdentity;
    public StateDiagramModel Model { get; private set; }
    public string Source => Model.Source;
    public string OriginalSource { get; }
    public string? Selection { get; private set; }
    public bool CanUndo => index > 0;
    public bool CanRedo => index + 1 < history.Count;
    public long HistoryIdentity => history[index].Identity;
    public int HistoryCount => history.Count;
    public StateVisualDraft(string source, DiagramService? diagrams = null, Func<StateDiagramModel, CancellationToken, Task>? validatePresentation = null)
    { this.diagrams = diagrams ?? new(); this.validatePresentation = validatePresentation; OriginalSource = source; Model = parser.Parse(source); history.Add(new(Model, null, 0)); }
    public void Select(string? identity) { Selection = identity; history[index] = history[index] with { Selection = identity }; }
    public async Task<StateEditResult> ExecuteAsync(StateEditOperation operation, CancellationToken cancellationToken = default)
    {
        var version = ++generation; var original = Model;
        try
        {
            var candidate = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested(); Validate(original, cancellationToken);
                var plan = transformations.Plan(original, operation);
                var parsed = parser.Parse(MermaidPatches.Apply(original.Source, plan.Patches), cancellationToken);
                if (parsed.Statements.Count != plan.Identities.Count) throw new ArgumentException("The edit changed an unplanned State source statement.");
                parsed = Identify(parsed, plan.Identities);
                StateSemanticValidation.ValidateEdit(original, parsed, operation, plan); Validate(parsed, cancellationToken); return parsed;
            }, cancellationToken);
            if (validatePresentation is not null) await validatePresentation(candidate, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (version != generation || !ReferenceEquals(Model, original)) return new(false, "A newer draft superseded this operation.");
            if (candidate.Source != Source) Commit(candidate); return new(true, null);
        }
        catch (Exception error) when (Recoverable(error, cancellationToken)) { return new(false, Message(error)); }
    }
    public async Task<StateEditResult> ValidateSourceAsync(string source, CancellationToken cancellationToken = default)
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
    public async Task<StateEditResult> ValidateAsync(CancellationToken cancellationToken = default)
    {
        var version = generation; var current = Model;
        try { await Task.Run(() => Validate(current, cancellationToken), cancellationToken); if (validatePresentation is not null) await validatePresentation(current, cancellationToken); cancellationToken.ThrowIfCancellationRequested(); return version == generation ? new(true, null) : new(false, "The draft changed during validation."); }
        catch (Exception error) when (Recoverable(error, cancellationToken)) { return new(false, Message(error)); }
    }
    private void Validate(StateDiagramModel model, CancellationToken cancellationToken)
    { cancellationToken.ThrowIfCancellationRequested(); StateSemanticValidation.ValidateRendering(model, diagrams.Render(model.Source, cancellationToken: cancellationToken)); cancellationToken.ThrowIfCancellationRequested(); }
    private static bool Recoverable(Exception error, CancellationToken token) => error is not OutOfMemoryException
        && (error is not OperationCanceledException || token.IsCancellationRequested);
    private static string Message(Exception error) => error is OperationCanceledException ? "State validation was cancelled; the draft is unchanged." : error.Message;
    private void Commit(StateDiagramModel candidate)
    {
        Model = candidate;
        if (!Model.Nodes.Any(item => item.Identity == Selection) && !Model.Transitions.Any(item => item.Identity == Selection)
            && !Model.Groups.Any(item => item.Identity == Selection) && !Model.PseudoNodes.Any(item => item.Identity == Selection) && !Model.Statements.Any(item => item.Identity == Selection)) Selection = null;
        if (index + 1 < history.Count) history.RemoveRange(index + 1, history.Count - index - 1);
        history.Add(new(Model, Selection, ++nextIdentity)); index++;
        while (history.Count > 100 || history.Count > 2 && history.Sum(item => item.Model.Source.Length) > 8_000_000) { history.RemoveAt(0); index--; }
    }
    public void Undo() { generation++; if (CanUndo) Restore(--index); }
    public void Redo() { generation++; if (CanRedo) Restore(++index); }
    private void Restore(int position) { Model = history[position].Model; Selection = history[position].Selection; }
    private static StateDiagramModel Identify(StateDiagramModel parsed, IReadOnlyList<string> identities)
    {
        var mapping = parsed.Statements.Select((item, index) => (item.Identity, Next: item with { Identity = identities[index] })).ToDictionary(item => item.Identity, item => item.Next);
        return parsed with
        {
            Statements = parsed.Statements.Select(item => mapping[item.Identity]).ToArray(),
            Nodes = parsed.Nodes.Select(item => item with { First = mapping[item.First.Identity], Declarations = item.Declarations.Select(declaration => mapping[declaration.Identity]).ToArray(),
                References = item.References.Select(reference => reference with { StatementIdentity = mapping[reference.StatementIdentity].Identity }).ToArray() }).ToArray(),
            Transitions = parsed.Transitions.Select(item => item with { Identity = mapping[item.Identity].Identity, Statement = mapping[item.Identity] }).ToArray(),
            Groups = parsed.Groups.Select(item => item with { Opening = mapping[item.Opening.Identity], Closing = item.Closing is null ? null : mapping[item.Closing.Identity] }).ToArray()
        };
    }
}
