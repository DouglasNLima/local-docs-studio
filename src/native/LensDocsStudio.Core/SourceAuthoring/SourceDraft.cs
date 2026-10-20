using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Core.SourceAuthoring;

/// <summary>Private bounded source snapshots. Each family owns scanning, transformations and correspondence.</summary>
public interface ISourceDraft
{
    SourceText Text { get; }
    string Source { get; }
    string OriginalSource { get; }
    string? Selection { get; }
    bool CanUndo { get; }
    bool CanRedo { get; }
    void Select(string? identity);
    void Undo();
    void Redo();
    Task<SourceAuthoringResult> ValidateAsync(CancellationToken cancellationToken = default);
    Task<SourceAuthoringResult> ValidateSourceAsync(string source, CancellationToken cancellationToken = default);
}

public abstract class SourceDraft<T> : ISourceDraft where T : class
{
    private sealed record Snapshot(SourceText Text, T Model, string? Selection);
    private readonly List<Snapshot> history = [];
    private int index, generation;
    protected readonly DiagramService Diagrams;
    public SourceText Text { get; private set; }
    public T Model { get; private set; }
    public string Source => Text.Source;
    public string OriginalSource { get; }
    public string? Selection { get; private set; }
    public bool CanUndo => index > 0;
    public bool CanRedo => index + 1 < history.Count;
    public Func<SourceText, T, CancellationToken, Task>? ValidatePresentation { get; set; }
    protected SourceDraft(string source, DiagramService? diagrams = null)
    {
        Diagrams = diagrams ?? new(); OriginalSource = source; Text = SourceText.Read(source); Model = Scan(Text);
        history.Add(new(Text, Model, null));
    }
    protected abstract T Scan(SourceText text);
    protected abstract void Validate(SourceText text, T model, DiagramResult rendered);
    protected abstract bool Contains(T model, string identity);
    public void Select(string? identity) { Selection = identity; history[index] = history[index] with { Selection = identity }; }
    public async Task<SourceAuthoringResult> ChangeAsync(Func<SourceText, T, SourceText> edit, CancellationToken cancellationToken = default, string? select = null)
    {
        var version = ++generation; var original = Text; var originalModel = Model;
        try
        {
            var candidate = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var changed = edit(original, originalModel); var model = Scan(changed);
                Validate(changed, model, Diagrams.Render(changed.Source, cancellationToken: cancellationToken));
                return (changed, model);
            }, cancellationToken);
            if (ValidatePresentation is { } presentation) await presentation(candidate.changed, candidate.model, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (version != generation || !ReferenceEquals(original, Text)) return new(false, "A newer draft superseded this operation.");
            if (candidate.changed.Source == Source && candidate.changed.Lines.Select(line => line.Key).SequenceEqual(Text.Lines.Select(line => line.Key))) return new(true, null);
            Text = candidate.changed; Model = candidate.model;
            Selection = select ?? Selection; if (Selection is not null && !Contains(Model, Selection)) Selection = null;
            if (index + 1 < history.Count) history.RemoveRange(index + 1, history.Count - index - 1);
            history.Add(new(Text, Model, Selection)); index++;
            while (history.Count > 100 || history.Count > 2 && history.Sum(item => item.Text.Source.Length) > 8_000_000) { history.RemoveAt(0); index--; }
            return new(true, null);
        }
        catch (Exception error) when (error is not OutOfMemoryException) { return new(false, error is OperationCanceledException ? "Validation cancelled; the draft is unchanged." : error.Message); }
    }
    public Task<SourceAuthoringResult> ValidateSourceAsync(string source, CancellationToken cancellationToken = default)
        => source == Source ? ValidateAsync(cancellationToken) : ChangeAsync((_, _) => SourceText.Read(source), cancellationToken);
    public async Task<SourceAuthoringResult> ValidateAsync(CancellationToken cancellationToken = default)
    {
        var version = generation; var current = Text; var model = Model;
        try
        {
            await Task.Run(() => Validate(current, model, Diagrams.Render(current.Source, cancellationToken: cancellationToken)), cancellationToken);
            if (ValidatePresentation is { } presentation) await presentation(current, model, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return version == generation && ReferenceEquals(Text, current) ? new(true, null) : new(false, "The draft changed during validation.");
        }
        catch (Exception error) when (error is not OutOfMemoryException) { return new(false, error is OperationCanceledException ? "Validation cancelled; the draft is unchanged." : error.Message); }
    }
    public void Undo() { generation++; if (CanUndo) Restore(--index); }
    public void Redo() { generation++; if (CanRedo) Restore(++index); }
    private void Restore(int position) { var item = history[position]; Text = item.Text; Model = item.Model; Selection = item.Selection; }
}
