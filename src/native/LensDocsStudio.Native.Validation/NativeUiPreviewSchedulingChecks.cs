using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static void CheckPreviewScheduling(Window window, AutomationElement editor, string output, string source)
    {
        var original = ReadEditorText(editor); var saved = ReadObservedText(source);
        var previous = NativeUiClipboard.GetDataObject(); uint owned = 0;
        const string marker = "Editor-only current preview fixture";
        var addition = "\n\n# " + marker + "\n\nCurrent selectable content.";
        try
        {
            Layout("Editor");
            Wait(() => Find(window, "PreviewStatus").Name == "Preview paused in Editor layout", "Editor layout did not pause preview rendering.");
            FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            NativeUiClipboard.SetText(addition); owned = GetClipboardSequenceNumber();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_V);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == original + addition, "The editor-only fixture did not retain exact source.");
            Find(window, "EditMenu").Click(); Find(window, "CopyRenderedText").Patterns.Invoke.Pattern.Invoke();
            Wait(() => NativeUiClipboard.GetText().Contains(marker, StringComparison.Ordinal), "Explicit text copying reused an obsolete hidden preview.");
            owned = GetClipboardSequenceNumber();
            window.CaptureToFile(Path.Combine(output, "native-editor-only-preview.png"));
            FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == original, "Preview text preparation damaged insertion Undo.");
            Find(window, "EditMenu").Click(); Find(window, "CopyRenderedText").Patterns.Invoke.Pattern.Invoke();
            Wait(() => GetClipboardSequenceNumber() != owned && !NativeUiClipboard.GetText().Contains(marker, StringComparison.Ordinal),
                "Text copying after Undo retained removed preview content.");
            owned = GetClipboardSequenceNumber();
            Layout("Preview");
            Wait(() => Find(window, "PreviewStatus").Name.StartsWith("Preview updated", StringComparison.Ordinal), "Showing the preview did not render current source.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
            Wait(() => Find(window, "FindContext").Name == "Find in preview", "Preview-only layout searched its hidden editor.");
            Keyboard.Type(VirtualKeyShort.ESCAPE);
            Layout("Editor"); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
            Wait(() => Find(window, "FindContext").Name == "Find in editor", "Editor-only layout searched its hidden preview.");
            Keyboard.Type(VirtualKeyShort.ESCAPE); Layout("Split");
            Wait(() => Find(window, "PreviewStatus").Name.StartsWith("Preview updated", StringComparison.Ordinal), "Split layout did not resume its preview.");
            if (ReadEditorText(editor) != original || ReadObservedText(source) != saved)
                throw new IOException("Preview scheduling, text copying or layout search changed the source.");
        }
        finally { RestoreClipboard(previous, owned); }
        void Layout(string name) { Find(window, "ViewMenu").Click(); Find(window, "Layout" + name).Patterns.Invoke.Pattern.Invoke(); }
    }
}
