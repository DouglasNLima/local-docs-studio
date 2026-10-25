using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.Editor;

/// <summary>A captured UI target, not an editing model. All mutations still pass through the document edit session.</summary>
public sealed class MermaidCreationCoordinator
{
    public DocumentViewModel Owner { get; }
    public TextDocument Document { get; }
    public ITextSourceVersion Version { get; }
    public string Source { get; }
    public int SelectionStart { get; }
    public int SelectionLength { get; }
    public int CaretOffset { get; }
    public Selection? SourceSelection { get; }
    public bool Standalone { get; }
    private readonly string? path, name;

    public MermaidCreationCoordinator(DocumentViewModel owner, int selectionStart, int selectionLength, int caretOffset, Selection? sourceSelection = null)
    {
        Owner = owner; Document = owner.EditorDocument; Version = Document.Version; Source = Document.Text;
        SelectionStart = selectionStart; SelectionLength = selectionLength; CaretOffset = caretOffset;
        SourceSelection = sourceSelection;
        path = owner.EffectivePath; name = owner.DisplayName;
        Standalone = Path.GetExtension(path ?? name)?.ToLowerInvariant() is ".mmd" or ".mermaid";
        if (Source.Length > MermaidDocumentEditSession.MaximumDocumentCharacters)
            throw new ArgumentException("Visual authoring supports documents up to 4 million characters. Use Edit source.");
    }

    public string? CurrentProblem(DocumentViewModel? active, TextDocument currentDocument, bool readOnly, bool ownerIsOpen)
    {
        if (!ownerIsOpen) return "The owning document closed. Reopen the source before creating a diagram.";
        if (active != Owner || currentDocument != Document) return "The active document changed. Reopen Mermaid from the intended document.";
        if (readOnly || Owner.IsSaving) return "The owning document is read-only or being saved. Reopen Mermaid when it is editable.";
        if (path != Owner.EffectivePath || name != Owner.DisplayName) return "The owning file identity changed. Reopen Mermaid from the current file.";
        if (Version.CompareAge(Document.Version) != 0) return "The captured document version changed. Reopen Mermaid; no source was replaced.";
        return null;
    }

    public Task<MermaidCreationTarget> ResolveAsync(CancellationToken token = default) => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested();
        var result = MermaidCreationTarget.Resolve(new MarkdownService().Parse(Source, Standalone), SelectionStart, SelectionLength);
        token.ThrowIfCancellationRequested(); return result;
    }, token);

    public MermaidDocumentEditSession CreateSession(MermaidCreationTarget target)
    {
        if (Version.CompareAge(Document.Version) != 0) throw new ArgumentException("The captured source changed. Reopen Mermaid.");
        if (target.Route == MermaidCreationRoute.ExistingDiagram && target.Block is { } block) return new(Owner, block);
        if (target.Route != MermaidCreationRoute.NewDiagram) throw new ArgumentException(target.Explanation);
        var span = Standalone ? new MermaidSourceSpan(0, Source.Length) : new(target.InsertionOffset, 0);
        var insertion = new MermaidBlockSource(span, span, span.Read(Source), Standalone) { IsInsertion = true };
        return new(Owner, insertion, Standalone ? null : MermaidCreationTarget.FormatInsertion);
    }
}
