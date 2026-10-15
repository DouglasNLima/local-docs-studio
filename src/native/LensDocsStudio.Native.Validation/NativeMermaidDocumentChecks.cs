using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.Validation;

internal static class NativeMermaidDocumentChecks
{
    public static IReadOnlyList<string> Run()
    {
        var checks = new List<string>();
        const string block = "```mermaid\r\nflowchart TD\r\nA[Original] --> B[Keep]\r\n```\r\n";
        const string source = "Before\r\n\r\n" + block + "\r\nMiddle\r\n\r\n" + block;
        var owner = new DocumentViewModel(source) { DisplayName = "Synthetic.md" };
        var target = MermaidBlockSource.Find(new MarkdownService().Parse(source))[1];
        var session = new MermaidDocumentEditSession(owner, target); var draft = target.Source.Replace("Original", "Updated", StringComparison.Ordinal);
        owner.EditorDocument.Insert(0, "Outside\r\n\r\n");
        session.Apply(draft, true);
        var expected = "Outside\r\n\r\n" + source[..target.Body.Start] + draft + source[target.Body.End..];
        Check(owner.EditorDocument.Text == expected, "AvalonEdit history relocates the intended identical diagram after surrounding changes");
        owner.EditorDocument.UndoStack.Undo();
        Check(owner.EditorDocument.Text == "Outside\r\n\r\n" + source, "One native document Undo restores the entire diagram and retains the independent surrounding edit");
        owner.EditorDocument.UndoStack.Redo(); Check(owner.EditorDocument.Text == expected, "Native document Redo restores the applied source");
        Reject(() => session.Apply(draft, true), "A second Apply through an obsolete session is rejected");
        Reject(() => new MermaidDocumentEditSession(owner, target).Apply(draft, false), "A closed owning tab cannot receive a visual Apply");

        var conflict = new DocumentViewModel(source);
        var conflictSession = new MermaidDocumentEditSession(conflict, target);
        conflict.EditorDocument.Replace(target.Body.Start + 3, 1, "X"); var changed = conflict.EditorDocument.Text;
        Reject(() => conflictSession.Apply(draft, true), "An overlapping original-source edit rejects Apply");
        Check(conflict.EditorDocument.Text == changed, "Rejected Apply preserves the externally edited in-memory source");

        var renamed = new DocumentViewModel(source) { DisplayName = "Before.md" };
        var renamedSession = new MermaidDocumentEditSession(renamed, target); renamed.DisplayName = "After.md";
        Reject(() => renamedSession.Apply(draft, true), "Renamed unsaved-document identities are rejected without source loss");

        var switchedOwner = new DocumentViewModel(source); var otherTab = new DocumentViewModel("Other document");
        var switchedSession = new MermaidDocumentEditSession(switchedOwner, target); switchedOwner.ExternalChange = true;
        switchedSession.Apply(draft, true);
        Check(otherTab.EditorDocument.Text == "Other document" && switchedOwner.EditorDocument.Text.Contains("Updated", StringComparison.Ordinal), "A switched active tab cannot redirect the owning-document edit");
        Check(switchedOwner.ExternalChange && switchedOwner.IsDirty, "Visual Apply retains ordinary unsaved state and disk-conflict protection");

        const string raw = "flowchart TD\r\nA[Keep] --> B[Source]\r\n";
        var standalone = new DocumentViewModel(raw) { DisplayName = "Synthetic.mmd" };
        var rawTarget = MermaidBlockSource.Find(new MarkdownService().Parse(raw, true), true).Single();
        new MermaidDocumentEditSession(standalone, rawTarget).Apply(raw.Replace("Keep", "Changed", StringComparison.Ordinal), true);
        Check(!standalone.EditorDocument.Text.Contains("```", StringComparison.Ordinal), "Standalone Apply never saves a synthetic Markdown fence");
        standalone.EditorDocument.UndoStack.Undo(); Check(standalone.EditorDocument.Text == raw, "Standalone source is restored exactly by one native Undo");
        return checks;

        void Check(bool passed, string message) { if (!passed) throw new IOException(message); checks.Add(message); }
        void Reject(Action action, string message)
        { try { action(); } catch (ArgumentException) { checks.Add(message); return; } throw new IOException(message); }
    }
}
