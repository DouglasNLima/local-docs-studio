using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static void CheckOutlineModernisation(Window window, AutomationElement editor, string output, string source)
    {
        var original = ReadEditorText(editor); var bytes = File.ReadAllBytes(source);
        const string fixture = "# Root\n\n## Branch\n\n### Leaf\n\n## Other\n\nPortable outline content.\n";
        var clipboard = NativeUiClipboard.GetDataObject(); uint owned = 0;
        Exception? primary = null;
        var outline = Find(window, "Outline").AsListBox(); var filter = Find(window, "OutlineFilter").AsTextBox();
        try
        {
            editor.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            NativeUiClipboard.SetText(fixture); owned = GetClipboardSequenceNumber();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_V);
            Wait(() => ReadEditorText(editor) == fixture && outline.Items.Length == 4, "The native outline did not expose its current hierarchy.");
            var previewStatus = Find(window, "PreviewStatus").Name;
            Group("Collapse all heading groups"); Wait(() => outline.Items.Length == 1 && outline.Items[0].Name == "Root", "Collapsing heading groups retained hidden descendants.");
            filter.Text = "Leaf"; Wait(() => outline.Items.Length == 1 && outline.Items[0].Name == "Leaf", "Outline filtering did not reveal a matching collapsed descendant.");
            filter.Text = ""; Wait(() => outline.Items.Length == 1 && outline.Items[0].Name == "Root", "Clearing outline filtering lost its retained collapsed groups.");
            Group("Expand all heading groups"); Wait(() => outline.Items.Length == 4, "Expanding outline groups lost a current heading.");
            outline.Items.Single(item => item.Name == "Branch").Click();
            Wait(() => Find(window, "CaretStatus").Name.StartsWith("Ln 3,", StringComparison.Ordinal), "Outline navigation did not reach the selected heading's exact source line.");
            // Mouse navigation deliberately returns editor focus. Focus the real outline
            // for its independent keyboard group command.
            outline.Focus(); Keyboard.Type(VirtualKeyShort.LEFT);
            Wait(() => outline.Items.Length == 3 && outline.Items.All(item => item.Name != "Leaf"), "Outline Left did not collapse its selected heading group.");
            Keyboard.Type(VirtualKeyShort.RIGHT); Wait(() => outline.Items.Length == 4, "Outline Right did not restore its selected heading group.");
            editor.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            Wait(() => outline.SelectedItem?.Name == "Other", "The current outline section did not follow the actual editor caret.");
            if (Find(window, "PreviewStatus").Name != previewStatus) throw new IOException("Outline controls reparsed or rebuilt the preview.");
            if (ReadEditorText(editor) != fixture || !File.ReadAllBytes(source).SequenceEqual(bytes)) throw new IOException("Outline controls changed source content.");
            File.WriteAllText(Path.Combine(output, "native-outline-modernisation.json"), JsonSerializer.Serialize(new {
                Passed = true, HierarchyCollapseExpand = true, FilterKeepsCollapsedGroups = true, ExactSourceLine = true,
                KeyboardGroups = true, CurrentCaretSection = true, NoPreviewRebuild = true, SourceBytesPreserved = true }));
        }
        catch (Exception error)
        {
            primary = error; File.WriteAllText(Path.Combine(output, "native-outline-modernisation-failure.json"), JsonSerializer.Serialize(new { Error = error.ToString() }));
            try { window.CaptureToFile(Path.Combine(output, "native-outline-modernisation-failure.png")); } catch (Exception) { }
            throw;
        }
        finally
        {
            try
            {
                filter.Text = ""; Group("Expand all heading groups"); editor.Focus();
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
                Wait(() => ReadEditorText(editor) == original, "The outline fixture did not retain its complete insertion Undo.");
            }
            catch (Exception error) when (primary is not null)
            { File.WriteAllText(Path.Combine(output, "native-outline-modernisation-cleanup.json"), JsonSerializer.Serialize(new { Error = error.ToString() })); }
            finally { RestoreClipboard(clipboard, owned); }
        }
        void Group(string name) => (window.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName(name)))
            ?? throw new IOException("The actual outline group command is unavailable: " + name)).AsButton().Invoke();
    }
}
