using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static void CheckLargeEditorNavigation(Window window, AutomationElement editor, string output, string source)
    {
        var original = ReadEditorText(editor); var saved = File.ReadAllBytes(source);
        var clipboard = NativeUiClipboard.GetDataObject(); uint owned = 0;
        const string marker = "Final wrapped document target";
        var longSource = "# Large source search\n\n" + string.Concat(Enumerable.Range(0, 2000).Select(index =>
            $"Paragraph {index}: **portable Markdown** retains source, selection and Unicode Café. "
            + "Wrapped virtual lines need their actual measured height before a distant selection can be revealed.\n\n")) + marker;
        try
        {
            Layout("Editor"); FocusWindow(window); editor.Click();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            NativeUiClipboard.SetText(longSource); owned = GetClipboardSequenceNumber();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_V);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == longSource, "The large editor fixture did not retain exact source.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.HOME);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
            Find(window, "FindText").AsTextBox().Text = marker; Find(window, "FindNext").Click();
            Wait(() => editor.Patterns.Text.Pattern.GetSelection().Any(range => range.GetText(-1) == marker
                && range.GetBoundingRectangles().Any(rectangle => rectangle.Width > 0 && rectangle.Height > 0 && editor.BoundingRectangle.Contains(rectangle))),
                "Find did not reveal its exact final selection inside the long editor viewport.");
            window.CaptureToFile(Path.Combine(output, "native-large-editor-search.png"));
            Find(window, "CloseFind").Click();
            FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == original, "Long-document search damaged the source insertion's Undo.");
            Layout("Split");
            Wait(() => Find(window, "PreviewStatus").Name.StartsWith("Preview updated", StringComparison.Ordinal), "Original preview did not resume after the large editor search.");
            if (!File.ReadAllBytes(source).AsSpan().SequenceEqual(saved)) throw new IOException("Long-document search changed the saved file.");
        }
        finally { RestoreClipboard(clipboard, owned); }
        void Layout(string name) { Find(window, "ViewMenu").Click(); Find(window, "Layout" + name).Patterns.Invoke.Pattern.Invoke(); }
    }
}
