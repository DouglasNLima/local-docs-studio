using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;

namespace LensDocsStudio.Native.Editor;

public static class EditorContextCommands
{
    public static void Prepare(ContextMenu menu, TextEditor editor, bool documentAvailable)
    {
        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            if (item.Command is RoutedCommand) item.CommandTarget = editor.TextArea;
            else item.IsEnabled = documentAvailable && !editor.IsReadOnly;
        }
        CommandManager.InvalidateRequerySuggested();
    }
}
