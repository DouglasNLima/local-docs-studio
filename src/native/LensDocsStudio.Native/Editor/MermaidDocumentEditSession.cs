using ICSharpCode.AvalonEdit.Document;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.Editor;

/// <summary>Identity stays with the existing document and AvalonEdit version chain, including identical blocks.</summary>
public sealed class MermaidDocumentEditSession(DocumentViewModel owner, MermaidBlockSource target, Func<string, string>? format = null)
{
    private readonly ITextSourceVersion version = owner.EditorDocument.Version;
    private readonly string? path = owner.EffectivePath;
    public DocumentViewModel Owner { get; } = owner;
    public MermaidBlockSource Target { get; } = target;
    public void Apply(string draft, bool ownerIsOpen)
    {
        if (!ownerIsOpen) throw new ArgumentException("The owning document has been closed. Copy the draft into an open document or cancel.");
        if (Owner.IsSaving || Owner.EffectivePath != path) throw new ArgumentException("The owning file is being saved or its identity changed. Reopen the current diagram.");
        var changes = version.GetChangesTo(Owner.EditorDocument.Version)
            .Select(change => new MermaidSourceChange(change.Offset, change.RemovalLength, change.InsertionLength));
        var span = Target.Relocate(Owner.EditorDocument.Text, changes);
        var replacement = format?.Invoke(draft) ?? Target.Replacement(draft);
        if (span.Read(Owner.EditorDocument.Text) == replacement) return;
        using (Owner.EditorDocument.RunUpdate()) Owner.EditorDocument.Replace(span.Start, span.Length, replacement);
    }
}
