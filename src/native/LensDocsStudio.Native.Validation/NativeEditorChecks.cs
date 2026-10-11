using System.Text.Json;
using ICSharpCode.AvalonEdit;
using LensDocsStudio.Native.Editor;

namespace LensDocsStudio.Native.Validation;

internal static class NativeEditorChecks
{
    public static void Run(string output)
    {
        const string source = "Before selected after";
        var editor = new TextEditor { Text = source }; editor.Document.UndoStack.ClearAll(); editor.Select(7, 8);
        EditorFormattingService.Apply(editor, "bold");
        if (editor.Text != "Before **selected** after" || editor.SelectedText != "selected") throw new IOException("Native bold did not retain the selected body.");
        EditorFormattingService.Apply(editor, "bold");
        if (editor.Text != source) throw new IOException("Native bold did not toggle from the retained selection.");
        editor.Document.UndoStack.Undo();
        if (editor.Text != "Before **selected** after") throw new IOException("Undo did not restore one complete formatting action.");
        editor.Document.UndoStack.Undo();
        if (editor.Text != source) throw new IOException("Undo did not restore the original source.");
        editor.Document.UndoStack.Redo();
        if (editor.Text != "Before **selected** after") throw new IOException("Redo did not replay the formatting action.");
        editor.IsReadOnly = true; EditorFormattingService.Apply(editor, "italic");
        if (editor.Text != "Before **selected** after") throw new IOException("Formatting changed a read-only editor.");
        editor.IsReadOnly = false; editor.Text = "Before\r\n"; editor.Document.UndoStack.ClearAll(); editor.Select(8, 0);
        EditorFormattingService.Apply(editor, "heading");
        if (editor.Text != "Before\r\n## " || editor.SelectionLength != 0 || editor.CaretOffset != 11)
            throw new IOException("An empty heading did not leave a usable typing caret after the marker.");
        editor.Document.Insert(editor.CaretOffset, "Title");
        if (editor.Text != "Before\r\n## Title") throw new IOException("Heading typing overwrote its marker.");
        editor.Text = source; editor.Document.UndoStack.ClearAll(); editor.Select(source.Length, 0);
        EditorFormattingService.Apply(editor, "callout:NOTE");
        if (editor.SelectedText != "Write the note here." || !editor.Text.Contains("> [!NOTE]\n> Write the note here.", StringComparison.Ordinal))
            throw new IOException("Native callout insertion did not select its editable body.");
        editor.SelectedText = "Review evidence.";
        if (!editor.Text.Contains("> Review evidence.", StringComparison.Ordinal)) throw new IOException("Callout typing changed its structural marker.");
        editor.Document.UndoStack.Undo(); editor.Document.UndoStack.Undo();
        if (editor.Text != source) throw new IOException("Undo did not restore the text preceding callout insertion and typing.");
        File.WriteAllText(Path.Combine(output, "native-editor.json"), JsonSerializer.Serialize(new
        {
            Passed = true,
            Checks = new[] { "Inline formatting retains selection and toggles", "Undo and Redo operate on complete formatting actions",
                "Read-only editors reject formatting", "Line commands leave a typing caret after their marker", "Callout typing replaces only its body and Undo restores original source" }
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
