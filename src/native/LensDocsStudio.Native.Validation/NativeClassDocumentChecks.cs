using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeClassChecks
{
    private static void DocumentChecks(List<string> checks)
    {
        var body = "classDiagram\r\n class Document {\r\n +string Title\r\n +Open(path) Document\r\n }\r\n Document --> Document : Repeat\r\n";
        foreach (var (opening, closing) in new[] { ("```mermaid", "```"), ("~~~~mermaid", "~~~~"), ("::: mermaid", ":::") })
        {
            var block = opening + "\r\n" + body + closing + "\r\n"; var source = "Before\r\n\r\n" + block + "\r\nMiddle\r\n\r\n" + block;
            var owner = new DocumentViewModel(source) { DisplayName = "Unsaved Class.md" }; var target = MermaidBlockSource.Find(new MarkdownService().Parse(source))[1];
            var first = new MermaidDocumentEditSession(owner, target); var competing = new MermaidDocumentEditSession(owner, target); var changed = body.Replace("+string Title", "-int Revision", StringComparison.Ordinal);
            owner.EditorDocument.Insert(0, "Outside\r\n\r\n"); first.Apply(changed, true); var expected = "Outside\r\n\r\n" + source[..target.Body.Start] + changed + source[target.Body.End..];
            Check(owner.EditorDocument.Text == expected, opening + ": exact second identical Class container relocation");
            owner.EditorDocument.UndoStack.Undo(); Check(owner.EditorDocument.Text == "Outside\r\n\r\n" + source, opening + ": one document Undo");
            owner.EditorDocument.UndoStack.Redo(); Check(owner.EditorDocument.Text == expected, opening + ": exact document Redo");
            Reject(() => competing.Apply(changed, true), "Competing Class session cannot overwrite a changed revision");
            var conflict = new DocumentViewModel(source); var session = new MermaidDocumentEditSession(conflict, target); conflict.EditorDocument.Insert(target.Body.Start + 1, "external"); var retained = conflict.EditorDocument.Text;
            Reject(() => session.Apply(changed, true), "Overlapping external source rejects Class Apply"); Check(conflict.EditorDocument.Text == retained, "Rejected Class Apply retains exact external source");
            Reject(() => new MermaidDocumentEditSession(new(source), target).Apply(changed, false), "Closed Class owner rejects Apply");
            var raced = new DocumentViewModel(source); var racedSession = new MermaidDocumentEditSession(raced, target); var prepared = racedSession.PrepareAsync(changed).GetAwaiter().GetResult(); raced.EditorDocument.Insert(0, "Race");
            Reject(() => racedSession.Commit(prepared, true), "Final Class Apply verifies AvalonEdit version and owning document");
            Reject(() => new MermaidDocumentEditSession(new(source), target).Apply(body + closing + "\r\nEscaped", true), "Class draft cannot escape its Markdown wrapper");
        }
        foreach (var extension in new[] { ".mmd", ".mermaid" })
        {
            var owner = new DocumentViewModel(body) { DisplayName = "Class" + extension }; var target = MermaidBlockSource.Find(new MarkdownService().Parse(body, true), true).Single();
            new MermaidDocumentEditSession(owner, target).Apply(body.Replace("+string Title", "-int Revision", StringComparison.Ordinal), true);
            Check(!owner.EditorDocument.Text.Contains("```", StringComparison.Ordinal), extension + ": Class Apply preserves raw portable source"); owner.EditorDocument.UndoStack.Undo(); Check(owner.EditorDocument.Text == body, extension + ": exact Class source Undo");
        }
        void Check(bool value, string message) { if (!value) throw new IOException(message); checks.Add(message); }
        void Reject(Action action, string message) { try { action(); } catch (ArgumentException) { checks.Add(message); return; } throw new IOException(message); }
    }
}
