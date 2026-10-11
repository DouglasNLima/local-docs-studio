using ICSharpCode.AvalonEdit;
using LensDocsStudio.Core.Editing;

namespace LensDocsStudio.Native.Editor;

public static class EditorFormattingService
{
    public static void Apply(TextEditor editor, string action)
    {
        if (editor.IsReadOnly) return;
        var edit = new MarkdownEditService().Plan(editor.Text, editor.SelectionStart, editor.SelectionLength, action);
        Apply(editor, edit);
    }
    public static void Apply(TextEditor editor, MarkdownEdit edit)
    {
        if (editor.IsReadOnly) return;
        var vertical = editor.VerticalOffset; var horizontal = editor.HorizontalOffset;
        using (editor.Document.RunUpdate()) editor.Document.Replace(edit.Start, edit.Length, edit.Replacement);
        editor.Select(edit.SelectionStart, edit.SelectionLength);
        editor.ScrollToVerticalOffset(vertical); editor.ScrollToHorizontalOffset(horizontal); editor.Focus();
    }
}
