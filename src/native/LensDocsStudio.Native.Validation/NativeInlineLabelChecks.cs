using System.Text.Json;
using ICSharpCode.AvalonEdit;
using LensDocsStudio.Core.Editing;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.Editor;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static class NativeInlineLabelChecks
{
    public static void Run(string output)
    {
        const string inlineSource = "Before selected after\r\n";
        var checks = new List<string>();
        foreach (var kind in new[] { "draft", "ready", "blocked", "deprecated", "beta", "review", "shortcut", "rule", "table-badge", "table-shortcut" })
        {
            var afterTable = kind.StartsWith("table-", StringComparison.Ordinal);
            var source = afterTable ? "| Area | Status |\r\n|---|---|\r\n| Clipboard | Ready |\r\n" : inlineSource;
            var start = afterTable ? source.Length : 7; var length = afterTable ? 0 : 8;
            var editor = new TextEditor { Text = source }; editor.Document.UndoStack.ClearAll(); editor.Select(start, length);
            var document = new MarkdownService().Parse(source);
            var edit = kind switch
            {
                "shortcut" or "table-shortcut" => InlineLabelEditService.Shortcut(document, start, length, "Ctrl + Shift + É"),
                "rule" => new MarkdownEditService().Plan(source, start, length, "rule"),
                _ => InlineLabelEditService.Badge(document, start, length, afterTable ? "ready" : kind, "Café & *literal* Ω")
            };
            EditorFormattingService.Apply(editor, edit);
            var formatted = editor.Text;
            if (formatted != source.Remove(start, length).Insert(start, edit.Replacement)
                || editor.SelectionStart != edit.SelectionStart || editor.SelectionLength != edit.SelectionLength)
                throw new IOException("Native label insertion changed surrounding source or selection: " + kind);
            if (kind != "rule")
            {
                var semantic = new MarkdownService().Parse(formatted).Syntax.Descendants<DocumentSemanticInline>().Single();
                if (semantic.Text != (kind is "shortcut" or "table-shortcut" ? "Ctrl + Shift + É" : "Café & *literal* Ω"))
                    throw new IOException("Native label text was interpreted as markup: " + kind);
            }
            if (afterTable && (new MarkdownService().Parse(formatted).Syntax.Descendants<Markdig.Extensions.Tables.Table>().Single().Count != 2
                || !formatted.StartsWith(source + "\r\n", StringComparison.Ordinal)))
                throw new IOException("Native label insertion changed its preceding table: " + kind);
            editor.Document.UndoStack.Undo();
            if (editor.Text != source) throw new IOException("One Undo did not restore native label source: " + kind);
            editor.Document.UndoStack.Redo();
            if (editor.Text != formatted) throw new IOException("Redo did not restore the whole native label: " + kind);
            editor.IsReadOnly = true; EditorFormattingService.Apply(editor, edit);
            if (editor.Text != formatted) throw new IOException("Label insertion changed a read-only document: " + kind);
            checks.Add(kind);
        }
        File.WriteAllText(Path.Combine(output, "native-inline-labels.json"), JsonSerializer.Serialize(new
        { Passed = true, Cases = checks, ExactSource = true, SelectionRetained = true, WholeUndoRedo = true, ReadOnlyRetained = true,
            Limitation = "Windowless AvalonEdit operations; actual menu, dialogue and preview interaction requires hosted desktop validation." }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
