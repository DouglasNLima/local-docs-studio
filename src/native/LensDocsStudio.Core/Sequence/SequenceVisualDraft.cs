using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Core.Sequence;

/// <summary>Private bounded source history. Validation and rendering finish before committing any state.</summary>
public sealed class SequenceVisualDraft
{
    private readonly SequenceSourceParser parser = new();
    private readonly SequenceTransformationService transformations = new();
    private readonly DiagramService diagrams;
    private sealed record Snapshot(SequenceDiagramModel Model, string? Selection, long Identity);
    private readonly List<Snapshot> history = [];
    private int index, generation;
    private long nextIdentity;
    public SequenceDiagramModel Model { get; private set; }
    public string Source => Model.Source;
    public string OriginalSource { get; }
    public string? Selection { get; private set; }
    public bool CanUndo => index > 0;
    public bool CanRedo => index + 1 < history.Count;
    public long HistoryIdentity => history[index].Identity;
    public int HistoryCount => history.Count;
    public SequenceVisualDraft(string source, DiagramService? diagrams = null)
    {
        this.diagrams = diagrams ?? new(); OriginalSource = source; Model = parser.Parse(source); history.Add(new(Model, null, 0));
    }
    public void Select(string? identity) { Selection = identity; history[index] = history[index] with { Selection = identity }; }
    public async Task<SequenceEditResult> ExecuteAsync(SequenceEditOperation operation, CancellationToken cancellationToken = default)
        => await ExecuteBatchAsync([operation], cancellationToken);
    public async Task<SequenceEditResult> ExecuteBatchAsync(IReadOnlyList<SequenceEditOperation> operations, CancellationToken cancellationToken = default)
    {
        var version = ++generation; var original = Model;
        try
        {
            var candidate = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (operations.Count is 0 or > 100) throw new ArgumentException("Choose between one and 100 bounded Sequence operations.");
                var parsed = original;
                foreach (var operation in operations)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var plan = transformations.Plan(parsed, operation);
                    var next = parser.Parse(MermaidPatches.Apply(parsed.Source, plan.Patches), cancellationToken);
                    if (next.Statements.Count != plan.Identities.Count) throw new ArgumentException("The edit changed unplanned source statements. Use Edit source.");
                    parsed = Identify(next, plan.Identities);
                }
                Validate(parsed, cancellationToken); return parsed;
            }, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (version != generation) return new(false, "A newer draft superseded this operation.");
            Commit(candidate); return new(true, null);
        }
        catch (ArgumentException error) { return new(false, error.Message); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return new(false, "Sequence validation was cancelled; the draft is unchanged."); }
    }
    public async Task<SequenceEditResult> ValidateSourceAsync(string source, CancellationToken cancellationToken = default)
    {
        var version = ++generation;
        try
        {
            var candidate = await Task.Run(() => { var parsed = parser.Parse(source, cancellationToken); Validate(parsed, cancellationToken); return parsed; }, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (generation != version) return new(false, "A newer draft superseded this validation.");
            if (source != Source) Commit(candidate); return new(true, null);
        }
        catch (ArgumentException error) { return new(false, error.Message); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return new(false, "Sequence validation was cancelled; the draft is unchanged."); }
    }
    public async Task<SequenceEditResult> ValidateAsync(CancellationToken cancellationToken = default)
    {
        var current = Model; var version = generation;
        try { await Task.Run(() => Validate(current, cancellationToken), cancellationToken); return version == generation ? new(true, null) : new(false, "The draft changed during validation."); }
        catch (ArgumentException error) { return new(false, error.Message); }
    }
    private void Validate(SequenceDiagramModel model, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!model.CanEdit) throw new ArgumentException(string.Join(" ", model.Diagnostics));
        SequenceRenderCorrespondence.Validate(model, diagrams.Render(model.Source, cancellationToken: cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
    }
    private void Commit(SequenceDiagramModel candidate)
    {
        Model = candidate;
        if (!Model.Statements.Any(item => item.Identity == Selection) && !Model.Participants.Any(item => "participant:" + item.Id == Selection)) Selection = null;
        if (index + 1 < history.Count) history.RemoveRange(index + 1, history.Count - index - 1);
        history.Add(new(Model, Selection, ++nextIdentity)); index++;
        while (history.Count > 100 || history.Count > 2 && history.Sum(item => item.Model.Source.Length) > 8_000_000) { history.RemoveAt(0); index--; }
    }
    public void Undo() { generation++; if (CanUndo) Restore(--index); }
    public void Redo() { generation++; if (CanRedo) Restore(++index); }
    private void Restore(int position) { Model = history[position].Model; Selection = history[position].Selection; }
    private static SequenceDiagramModel Identify(SequenceDiagramModel parsed, IReadOnlyList<string> identities)
    {
        var mapping = parsed.Statements.Select((item, index) => (item.Identity, Next: item with { Identity = identities[index] })).ToDictionary(item => item.Identity, item => item.Next);
        return parsed with
        {
            Statements = parsed.Statements.Select(item => mapping[item.Identity]).ToArray(),
            Participants = parsed.Participants.Select(item => item with { Declaration = item.Declaration is null ? null : mapping[item.Declaration.Identity] }).ToArray(),
            Messages = parsed.Messages.Select(item => item with { Identity = mapping[item.Identity].Identity, Statement = mapping[item.Identity] }).ToArray(),
            Notes = parsed.Notes.Select(item => item with { Identity = mapping[item.Identity].Identity, Statement = mapping[item.Identity] }).ToArray(),
            Activations = parsed.Activations.Select(item => item with { Identity = mapping[item.Statement.Identity].Identity + (item.Standalone ? "" : ":activation"), MessageIdentity = mapping[item.MessageIdentity].Identity, Statement = mapping[item.Statement.Identity] }).ToArray()
        };
    }
}
