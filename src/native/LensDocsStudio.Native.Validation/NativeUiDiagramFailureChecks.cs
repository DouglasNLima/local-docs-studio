using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static void CheckDiagramFailureActions(Window window, AutomationElement editor, string state, string output, string source)
    {
        var original = ReadEditorText(editor); var saved = File.ReadAllBytes(source);
        var preferencesPath = Path.Combine(state, "preferences.json");
        UserPreferences ReadPreferences()
        {
            using var input = new FileStream(preferencesPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return JsonSerializer.Deserialize<UserPreferences>(input)!;
        }
        var before = ReadPreferences(); var clipboard = NativeUiClipboard.GetDataObject(); uint owned = 0;
        const string body = "flowchart LR\nBroken -->";
        const string fixture = "# Diagram recovery\n\n```mermaid\n" + body + "\n```\n\nAfter the diagram.\n";
        var target = fixture.IndexOf("```mermaid", StringComparison.Ordinal);
        try
        {
            Layout("Split"); FocusWindow(window); editor.Click();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            NativeUiClipboard.SetText(fixture); owned = GetClipboardSequenceNumber();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_V);
            Wait(() => ReadEditorText(editor) == fixture, "The failed-diagram fixture did not retain its exact source.");
            Layout("Preview");
            Wait(() => window.FindFirstDescendant(cf => cf.ByAutomationId("DiagramError" + target))?.Name.Contains("missing its destination", StringComparison.Ordinal) == true,
                "The failed diagram did not expose its native accessible explanation.");
            Find(window, "DiagramErrorCopySource" + target).AsButton().Invoke();
            Wait(() => NativeUiClipboard.GetText() == body, "Copy source did not copy the failed diagram's exact code.");
            owned = GetClipboardSequenceNumber();
            var edit = Find(window, "DiagramErrorEditSource" + target); edit.Focus();
            Wait(() => edit.Properties.HasKeyboardFocus.Value, "Edit source cannot receive keyboard focus.");
            Keyboard.Type(VirtualKeyShort.SPACE);
            Wait(() => !editor.IsOffscreen && ReadPreferences().Layout == "Split", "Edit source did not reveal the editor from Preview layout.");
            // Type without refocusing the editor: the action must restore both focus and the exact source position.
            Keyboard.Type("X");
            Wait(() => ReadEditorText(editor) == fixture.Insert(target, "X"), "Edit source did not place keyboard input at the failed diagram's fence.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => ReadEditorText(editor) == fixture, "Editing a failed diagram did not retain the single-character Undo.");
            Wait(() => window.FindFirstDescendant(cf => cf.ByAutomationId("DiagramError" + target)) is not null, "The failed diagram did not return after Undo.");
            window.CaptureToFile(Path.Combine(output, "native-diagram-error.png"));
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => ReadEditorText(editor) == original, "Diagram recovery actions changed the fixture insertion's complete Undo.");
            Layout(before.Layout);
            var after = ReadPreferences();
            if (!File.ReadAllBytes(source).AsSpan().SequenceEqual(saved) || WorkspaceSettings.From(after) != WorkspaceSettings.From(before)
                || after.Layout != before.Layout || after.Theme != before.Theme
                || after.AutomaticUpdateChecks != before.AutomaticUpdateChecks || after.UpdateChannel != before.UpdateChannel)
                throw new IOException("Diagram recovery changed saved source, unrelated preferences or update consent.");
            File.WriteAllText(Path.Combine(output, "native-ui-diagram-errors.json"), JsonSerializer.Serialize(new
            { Passed = true, CopyExact = true, EditSourceOffset = target, KeyboardFocusAndPosition = true,
                EditorRevealed = true, CompleteUndo = true, SavedSourcePreserved = true, PreferencesRestored = true }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { RestoreClipboard(clipboard, owned); }

        void Layout(string value)
        {
            FocusWindow(window); Find(window, "ViewMenu").Click(); Find(window, "Layout" + value).Patterns.Invoke.Pattern.Invoke();
            Wait(() => ReadPreferences().Layout == value, "The diagram recovery layout did not persist.");
        }
    }
}
