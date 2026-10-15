using ICSharpCode.AvalonEdit.Document;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.Editor;

/// <summary>Identity stays with the existing document and AvalonEdit version chain, including identical blocks.</summary>
public sealed record MermaidPreparedDocumentEdit(ITextSourceVersion Version, MermaidSourceSpan Span, string Expected, string Replacement);

public sealed class MermaidDocumentEditSession(DocumentViewModel owner, MermaidBlockSource target, Func<string, string, MermaidSourceSpan, string>? format = null)
{
    public const int MaximumDocumentCharacters = 4_000_000;
    private readonly ITextSourceVersion version = owner.EditorDocument.Version;
    private readonly string? path = owner.EffectivePath;
    private readonly string? displayName = owner.DisplayName;
    public DocumentViewModel Owner { get; } = owner;
    public MermaidBlockSource Target { get; } = target;
    public void Apply(string draft, bool ownerIsOpen)
        => Commit(PrepareAsync(draft).GetAwaiter().GetResult(), ownerIsOpen);
    public async Task<MermaidPreparedDocumentEdit> PrepareAsync(string draft, CancellationToken cancellationToken = default)
    {
        CheckIdentity(); var current = Owner.EditorDocument.Text; var currentVersion = Owner.EditorDocument.Version;
        if (current.Length > MaximumDocumentCharacters) throw new ArgumentException("Visual Apply supports documents up to 4 million characters. Use the owning source editor for larger documents.");
        var changes = version.GetChangesTo(currentVersion)
            .Select(change => new MermaidSourceChange(change.Offset, change.RemovalLength, change.InsertionLength));
        var span = Target.Relocate(current, changes); var replacement = format?.Invoke(draft, current, span) ?? Target.Replacement(draft);
        if (!Target.Standalone)
        {
            var candidate = current[..span.Start] + replacement + current[span.End..];
            if (candidate.Length > MaximumDocumentCharacters) throw new ArgumentException("The resulting document exceeds the 4-million-character visual Apply budget. Use the owning source editor.");
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var blocks = MermaidBlockSource.Find(new MarkdownService().Parse(candidate));
                var matching = format is null
                    ? blocks.Count(block => block.Body.Start == span.Start && block.Source == replacement)
                    : blocks.Count(block => block.Container.Start >= span.Start && block.Body.End <= span.Start + replacement.Length
                        && block.Source.TrimEnd('\r', '\n') == draft.TrimEnd('\r', '\n'));
                cancellationToken.ThrowIfCancellationRequested();
                if (matching != 1) throw new ArgumentException("The draft would close or change its Markdown container, or its surrounding syntax changed. Use a longer fence or reopen the owning source.");
            }, cancellationToken).ConfigureAwait(false);
        }
        cancellationToken.ThrowIfCancellationRequested(); return new(currentVersion, span, span.Read(current), replacement);
    }
    public void Commit(MermaidPreparedDocumentEdit edit, bool ownerIsOpen)
    {
        if (!ownerIsOpen) throw new ArgumentException("The owning document has been closed. Copy the draft into an open document or cancel.");
        CheckIdentity();
        if (edit.Version.CompareAge(Owner.EditorDocument.Version) != 0 || edit.Span.Read(Owner.EditorDocument.Text) != edit.Expected)
            throw new ArgumentException("The document changed during final validation. Its source was not replaced; retry Apply against the current document.");
        if (edit.Expected == edit.Replacement) return;
        using (Owner.EditorDocument.RunUpdate()) Owner.EditorDocument.Replace(edit.Span.Start, edit.Span.Length, edit.Replacement);
    }
    private void CheckIdentity()
    { if (Owner.IsSaving || Owner.EffectivePath != path || Owner.DisplayName != displayName) throw new ArgumentException("The owning file is being saved or its identity changed. Reopen the current diagram."); }
    public int SourceOffset() => Target.Relocate(Owner.EditorDocument.Text, version.GetChangesTo(Owner.EditorDocument.Version)
        .Select(change => new MermaidSourceChange(change.Offset, change.RemovalLength, change.InsertionLength))).Start;
}
