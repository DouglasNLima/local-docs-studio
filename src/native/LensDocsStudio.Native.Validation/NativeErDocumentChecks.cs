using LensDocsStudio.Core.Mermaid; using LensDocsStudio.Core.Parsing; using LensDocsStudio.Native.Editor; using LensDocsStudio.Native.ViewModels;
namespace LensDocsStudio.Native.Validation;
internal static partial class NativeErChecks
{
    private static void DocumentChecks(List<string> checks)
    {
        var body = "erDiagram\r\n A[\"Keep\"] {\r\n string id PK\r\n }\r\n A ||--o{ A : Repeat\r\n";
        foreach (var (opening, closing) in new[] { ("```mermaid", "```"), ("~~~~mermaid", "~~~~"), ("::: mermaid", ":::") })
        {
            var block = opening + "\r\n" + body + closing + "\r\n"; var source = "Before\r\n\r\n" + block + "\r\nMiddle\r\n\r\n" + block;
            var owner = new DocumentViewModel(source) { DisplayName = "Unsaved state.md" }; var target = MermaidBlockSource.Find(new MarkdownService().Parse(source))[1];
            var first = new MermaidDocumentEditSession(owner, target); var competing = new MermaidDocumentEditSession(owner, target); var changedBody = body.Replace("Keep", "Changed", StringComparison.Ordinal);
            owner.EditorDocument.Insert(0, "Outside\r\n\r\n"); first.Apply(changedBody, true); var expected = "Outside\r\n\r\n" + source[..target.Body.Start] + changedBody + source[target.Body.End..];
            Check(owner.EditorDocument.Text == expected, opening + ": exact repeated ER container relocation"); owner.EditorDocument.UndoStack.Undo(); Check(owner.EditorDocument.Text == "Outside\r\n\r\n" + source, opening + ": one document Undo"); owner.EditorDocument.UndoStack.Redo(); Check(owner.EditorDocument.Text == expected, opening + ": exact document Redo");
            Reject(() => competing.Apply(changedBody, true), "Competing ER editor cannot overwrite a source revision");
            var conflict = new DocumentViewModel(source); var session = new MermaidDocumentEditSession(conflict, target); conflict.EditorDocument.Insert(target.Body.Start + 1, "changed"); var retained = conflict.EditorDocument.Text;
            Reject(() => session.Apply(changedBody, true), "Overlapping external source changes reject ER Apply"); Check(conflict.EditorDocument.Text == retained, "Rejected ER Apply is atomic");
            Reject(() => new MermaidDocumentEditSession(new(source), target).Apply(changedBody, false), "Closed ER owner rejects Apply");
            var raced = new DocumentViewModel(source); var racedSession = new MermaidDocumentEditSession(raced, target); var prepared = racedSession.PrepareAsync(changedBody).GetAwaiter().GetResult(); raced.EditorDocument.Insert(0, "Race"); Reject(() => racedSession.Commit(prepared, true), "Final ER Apply checks AvalonEdit ownership/version");
            Reject(() => new MermaidDocumentEditSession(new(source), target).Apply(body + closing + "\r\nEscaped", true), "ER draft cannot close its Markdown wrapper");
        }
        foreach (var extension in new[] { ".mmd", ".mermaid" })
        {
            var owner = new DocumentViewModel(body) { DisplayName = "ER" + extension }; var target = MermaidBlockSource.Find(new MarkdownService().Parse(body, true), true).Single(); new MermaidDocumentEditSession(owner, target).Apply(body.Replace("Keep", "Changed", StringComparison.Ordinal), true);
            Check(!owner.EditorDocument.Text.Contains("```", StringComparison.Ordinal), extension + ": ER Apply preserves raw portable source"); owner.EditorDocument.UndoStack.Undo(); Check(owner.EditorDocument.Text == body, extension + ": exact ER source Undo");
        }
        void Check(bool value, string message) { if (!value) throw new IOException(message); checks.Add(message); }
        void Reject(Action action, string message) { try { action(); } catch (ArgumentException) { checks.Add(message); return; } throw new IOException(message); }
    }
}
