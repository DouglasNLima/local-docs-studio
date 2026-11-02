using System.Reflection;
using System.Windows.Documents;

namespace LensDocsStudio.Native.Rendering;

internal static class PreviewSelectionVisuals
{
    // WPF's caret worker stops when its thread has no focused native text
    // selection. AvalonEdit does not supply one. Use the existing WPF visual
    // pipeline without focusing the reader or changing its thread-local owner.
    // This guarded .NET 10 bridge is covered by cold-reader screen-pixel checks.
    private static readonly MethodInfo? Activate = typeof(TextSelection)
        .GetInterface("System.Windows.Documents.ITextSelection")?.GetMethod("UpdateCaretAndHighlight");
    private static readonly FieldInfo? Caret = typeof(TextSelection)
        .GetField("_caretElement", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo? UpdateGeometry = Caret?.FieldType
        .GetMethod("UpdateSelection", BindingFlags.Instance | BindingFlags.NonPublic);

    public static bool Refresh(TextSelection selection)
    {
        if (Activate is null || Caret is null || UpdateGeometry is null
            || !selection.IsEmpty && (!selection.Start.HasValidLayout || !selection.End.HasValidLayout)) return false;
        try
        {
            Activate.Invoke(selection, null);
            if (Caret.GetValue(selection) is { } visual) UpdateGeometry.Invoke(visual, null);
            return true;
        }
        catch (Exception error) when (error is MethodAccessException or TargetException
            or TargetInvocationException { InnerException: InvalidOperationException or ArgumentException })
        { return false; }
    }
}
