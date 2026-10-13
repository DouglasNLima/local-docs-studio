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
        const string source = "Before selected after\r\n";
        var checks = new List<string>();
        foreach (var kind in new[] { "draft", "ready", "blocked", "deprecated", "beta", "review", "shortcut", "rule" })
        {
            var editor = new TextEditor { Text = source }; editor.Document.UndoStack.ClearAll(); editor.Select(7, 8);
            var document = new MarkdownService().Parse(source);
            var edit = kind switch
            {
                "shortcut" => InlineLabelEditService.Shortcut(document, 7, 8, "Ctrl + Shift + É"),
                "rule" => new MarkdownEditService().Plan(source, 7, 8, "rule"),
                _ => InlineLabelEditService.Badge(document, 7, 8, kind, "Café & *literal* Ω")
            };
            EditorFormattingService.Apply(editor, edit);
            var formatted = editor.Text;
            if (formatted != source.Remove(7, 8).Insert(7, edit.Replacement)
                || editor.SelectionStart != edit.SelectionStart || editor.SelectionLength != edit.SelectionLength)
                throw new IOException("Native label insertion changed surrounding source or selection: " + kind);
            if (kind != "rule")
            {
                var semantic = new MarkdownService().Parse(formatted).Syntax.Descendants<DocumentSemanticInline>().Single();
                if (semantic.Text != (kind == "shortcut" ? "Ctrl + Shift + É" : "Café & *literal* Ω"))
                    throw new IOException("Native label text was interpreted as markup: " + kind);
            }
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
