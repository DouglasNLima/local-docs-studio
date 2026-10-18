using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Core.ClassDiagrams;

/// <summary>Class-specific bounded, private snapshots. Validation failure never commits source or selection.</summary>
public sealed class ClassDiagramVisualDraft
{
    private readonly ClassDiagramSourceParser parser = new();
    private readonly ClassDiagramTransformationService transformations = new();
    private readonly DiagramService diagrams;
    private readonly Func<ClassDiagramModel, CancellationToken, Task>? validatePresentation;
    private sealed record Snapshot(ClassDiagramModel Model, string? Selection, long Identity);
    private readonly List<Snapshot> history = [];
    private int index, generation;
    private long nextIdentity;
    public ClassDiagramModel Model { get; private set; }
    public string Source => Model.Source;
    public string OriginalSource { get; }
    public string? Selection { get; private set; }
    public bool CanUndo => index > 0;
    public bool CanRedo => index + 1 < history.Count;
    public long HistoryIdentity => history[index].Identity;
    public int HistoryCount => history.Count;
    public ClassDiagramVisualDraft(string source, DiagramService? diagrams = null, Func<ClassDiagramModel, CancellationToken, Task>? validatePresentation = null, CancellationToken cancellationToken = default)
    { this.diagrams = diagrams ?? new(); this.validatePresentation = validatePresentation; OriginalSource = source; Model = parser.Parse(source, cancellationToken); history.Add(new(Model, null, 0)); }
    public void Select(string? identity) { Selection = identity; history[index] = history[index] with { Selection = identity }; }
    public async Task<ClassEditResult> ExecuteAsync(ClassDiagramEditOperation operation, CancellationToken cancellationToken = default)
    {
        var version = ++generation; var original = Model;
        try
        {
            var candidate = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested(); Validate(original, cancellationToken);
                var plan = transformations.Plan(original, operation);
                var parsed = parser.Parse(MermaidPatches.Apply(original.Source, plan.Patches), cancellationToken);
                if (parsed.Statements.Count != plan.StatementIdentities.Count || parsed.Classes.Count != plan.ClassIdentities.Count) throw new ArgumentException("The edit changed an unplanned Class occurrence.");
                parsed = Identify(parsed, plan);
                ClassDiagramSemanticValidation.ValidateEdit(original, parsed, plan); Validate(parsed, cancellationToken); return parsed;
            }, cancellationToken);
            if (validatePresentation is not null) await validatePresentation(candidate, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (version != generation || !ReferenceEquals(Model, original)) return new(false, "A newer draft superseded this operation.");
            if (candidate.Source != Source || operation is MoveClassMember) Commit(candidate); return new(true, null);
        }
        catch (Exception error) when (Recoverable(error, cancellationToken)) { return new(false, Message(error)); }
    }
    public async Task<ClassEditResult> ValidateSourceAsync(string source, CancellationToken cancellationToken = default)
    {
        var version = ++generation;
        try
        {
            var candidate = await Task.Run(() => { var parsed = parser.Parse(source, cancellationToken); Validate(parsed, cancellationToken); return parsed; }, cancellationToken);
            if (validatePresentation is not null) await validatePresentation(candidate, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested(); if (version != generation) return new(false, "A newer draft superseded source validation.");
            if (candidate.Source != Source) { Selection = null; Commit(candidate); } return new(true, null);
        }
        catch (Exception error) when (Recoverable(error, cancellationToken)) { return new(false, Message(error)); }
    }
    public async Task<ClassEditResult> ValidateAsync(CancellationToken cancellationToken = default)
    {
        var version = generation; var current = Model;
        try { await Task.Run(() => Validate(current, cancellationToken), cancellationToken); if (validatePresentation is not null) await validatePresentation(current, cancellationToken); cancellationToken.ThrowIfCancellationRequested(); return version == generation && ReferenceEquals(current, Model) ? new(true, null) : new(false, "The draft changed during validation."); }
        catch (Exception error) when (Recoverable(error, cancellationToken)) { return new(false, Message(error)); }
    }
    private void Validate(ClassDiagramModel model, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!model.IsValid) throw new ArgumentException(model.Diagnostics.FirstOrDefault() ?? "Repair invalid Class source using Edit source.");
        ClassDiagramSemanticValidation.ValidateRendering(model, diagrams.Render(model.Source, cancellationToken: cancellationToken)); cancellationToken.ThrowIfCancellationRequested();
    }
    private static bool Recoverable(Exception error, CancellationToken token) => error is not OutOfMemoryException && (error is not OperationCanceledException || token.IsCancellationRequested);
    private static string Message(Exception error) => error is OperationCanceledException ? "Class validation was cancelled; the draft is unchanged." : error.Message;
    private void Commit(ClassDiagramModel candidate)
    {
        Model = candidate;
        if (!Model.Classes.Any(item => item.Identity == Selection) && !Model.Relationships.Any(item => item.Identity == Selection) && !Model.Classes.SelectMany(item => item.Members).Any(item => item.Identity == Selection) && !Model.Namespaces.Any(item => item.Identity == Selection) && !Model.Notes.Any(item => item.Identity == Selection)) Selection = null;
        if (index + 1 < history.Count) history.RemoveRange(index + 1, history.Count - index - 1);
        history.Add(new(Model, Selection, ++nextIdentity)); index++;
        while (history.Count > 100 || history.Count > 2 && history.Sum(item => item.Model.Source.Length) > 8_000_000) { history.RemoveAt(0); index--; }
    }
    public void Undo() { generation++; if (CanUndo) Restore(--index); }
    public void Redo() { generation++; if (CanRedo) Restore(++index); }
    private void Restore(int position) { Model = history[position].Model; Selection = history[position].Selection; }
    private static ClassDiagramModel Identify(ClassDiagramModel parsed, ClassEditPlan plan)
    {
        var mapping = parsed.Statements.Select((item, i) => (item.Identity, Next: item with { Identity = plan.StatementIdentities[i] })).ToDictionary(item => item.Identity, item => item.Next);
        foreach (var key in mapping.Keys.ToArray()) if (mapping[key].NamespaceIdentity is { } ns) mapping[key] = mapping[key] with { NamespaceIdentity = mapping[ns].Identity };
        return parsed with {
            Statements = parsed.Statements.Select(item => mapping[item.Identity]).ToArray(),
            Classes = parsed.Classes.Select((item, i) => item with { Identity = plan.ClassIdentities[i], Declarations = item.Declarations.Select(value => mapping[value.Identity]).ToArray(), Members = item.Members.Select(member => member with { Identity = mapping[member.Identity].Identity, Statement = mapping[member.Identity] }).ToArray() }).ToArray(),
            Relationships = parsed.Relationships.Select(item => item with { Identity = mapping[item.Identity].Identity, Statement = mapping[item.Identity] }).ToArray(),
            Namespaces = parsed.Namespaces.Select(item => item with { Identity = mapping[item.Identity].Identity, Statement = mapping[item.Identity] }).ToArray(),
            Notes = parsed.Notes.Select(item => item with { Identity = mapping[item.Identity].Identity, Statement = mapping[item.Identity] }).ToArray()
        };
    }
}
