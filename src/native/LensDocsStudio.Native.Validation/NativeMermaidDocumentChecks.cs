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
        const string partialBody = "flowchart LR\r\nA[Start] --> B[Review] --> C[Done]\r\nD[Independent]\r\n";
        foreach (var fence in new[] { "```", "~~~" })
        {
            var partialBlock = fence + "mermaid\r\n" + partialBody + fence + "\r\n";
            var partialDocument = "Before café\r\n" + partialBlock + "Between\r\n" + partialBlock + "After\r\n";
            var partialOwner = new DocumentViewModel(partialDocument) { DisplayName = "Identical partial.md" };
            var partialTarget = MermaidBlockSource.Find(new MarkdownService().Parse(partialDocument))[1];
            var partialDraft = new MermaidVisualDraft(partialTarget.Source);
            var editResult = Task.Run(() => partialDraft.ExecuteAsync(new UpdateMermaidNode("D", "Reviewed", FlowNodeShape.Process, "D"))).GetAwaiter().GetResult();
            Check(editResult.Success && partialDraft.CanApply, "Partial Core proof authorises an independent label in identical " + fence + " blocks");
            new MermaidDocumentEditSession(partialOwner, partialTarget).Apply(partialDraft.Source, true);
            Check(partialOwner.EditorDocument.Text == partialDocument[..partialTarget.Body.Start] + partialDraft.Source + partialDocument[partialTarget.Body.End..],
                "Partial Apply changes only the tracked second identical " + fence + " block, preserving its first block and surrounding Unicode");
            partialOwner.EditorDocument.UndoStack.Undo();
            Check(partialOwner.EditorDocument.Text == partialDocument, "One native Undo restores exact identical partial diagrams with " + fence + " fences");
        }
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
        var appended = new DocumentViewModel(raw); var appendedSession = new MermaidDocumentEditSession(appended, rawTarget);
        appended.EditorDocument.Insert(appended.EditorDocument.TextLength, "B --> C\r\n"); var appendedText = appended.EditorDocument.Text;
        Reject(() => appendedSession.Apply(raw.Replace("Keep", "Changed", StringComparison.Ordinal), true), "Appending to a standalone diagram invalidates the original whole-file identity");
        Check(appended.EditorDocument.Text == appendedText, "Rejected standalone append preserves the independently added source");
        var wrapped = new DocumentViewModel(source); var wrappedSession = new MermaidDocumentEditSession(wrapped, target);
        wrapped.EditorDocument.Insert(0, "````text\r\n"); var wrappedText = wrapped.EditorDocument.Text;
        Reject(() => wrappedSession.Apply(draft, true), "A surrounding outer fence cannot turn the tracked diagram into an editable literal code example");
        Check(wrapped.EditorDocument.Text == wrappedText, "Rejected surrounding-syntax changes leave all source untouched");
        const string collision = "flowchart TD\nA[\"First\n```\nLast\"]\n";
        Check(new MermaidSourceParser().Parse(collision).CanEdit, "Fence-collision fixture is valid Mermaid independently of its Markdown wrapper");
        var fenced = new DocumentViewModel(source); var fencedSession = new MermaidDocumentEditSession(fenced, target);
        Reject(() => fencedSession.Apply(collision, true), "Valid Mermaid cannot prematurely close its owning Markdown fence");
        Check(fenced.EditorDocument.Text == source, "Fence-collision rejection preserves the complete owning Markdown");
        var preparedOwner = new DocumentViewModel(source); var preparedSession = new MermaidDocumentEditSession(preparedOwner, target);
        var prepared = preparedSession.PrepareAsync(draft).GetAwaiter().GetResult(); preparedOwner.EditorDocument.Insert(0, "Changed during validation\r\n");
        var raced = preparedOwner.EditorDocument.Text;
        Reject(() => preparedSession.Commit(prepared, true), "A document change during asynchronous final validation rejects the prepared edit");
        Check(preparedOwner.EditorDocument.Text == raced, "A rejected prepared edit never overwrites a newer document revision");
        return checks;

        void Check(bool passed, string message) { if (!passed) throw new IOException(message); checks.Add(message); }
        void Reject(Action action, string message)
        { try { action(); } catch (ArgumentException) { checks.Add(message); return; } throw new IOException(message); }
    }
}
