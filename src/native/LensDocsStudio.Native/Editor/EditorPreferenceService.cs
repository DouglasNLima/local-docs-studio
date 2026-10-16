using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Native.Editor;

public static class EditorPreferenceService
{
    public static void Apply(TextEditor editor, EditorPreferences preferences)
    {
        if (!(WorkspaceSettings.Default with { Editor = preferences }).IsValid) throw new ArgumentException("The editor preferences are unsupported.");
        var vertical = editor.VerticalOffset; var horizontal = editor.HorizontalOffset;
        editor.FontFamily = new FontFamily(preferences.FontFamily); editor.FontSize = preferences.FontSize;
        editor.WordWrap = preferences.WordWrap; editor.ShowLineNumbers = preferences.ShowLineNumbers;
        editor.Options.IndentationSize = preferences.IndentationSize; editor.Options.ConvertTabsToSpaces = preferences.ConvertTabsToSpaces;
        editor.UpdateLayout(); editor.ScrollToVerticalOffset(vertical); editor.ScrollToHorizontalOffset(horizontal);
    }
}
