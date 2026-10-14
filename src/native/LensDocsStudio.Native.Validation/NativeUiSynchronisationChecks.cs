using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static void CheckSynchronisation(Window window, AutomationElement editor, string state, string output, string source)
    {
        var original = ReadEditorText(editor); var saved = File.ReadAllBytes(source);
        var before = JsonSerializer.Deserialize<UserPreferences>(File.ReadAllText(Path.Combine(state, "preferences.json")))!;
        const string fixture = "# Native sync fixture\n\naction **action** *action* [action](other.md) `action`\n\nCafé 😀 é action\n\nSoft\nbreak and hard  \nbreak.\n";
        var clipboard = NativeUiClipboard.GetDataObject(); uint owned = 0;
        var observations = new List<object>();
        Exception? originalFailure = null;
        var preview = Find(window, "DocumentPreview");
        Find(window, "ToggleSidebar").AsButton().Invoke(); Find(window, "ToggleOutline").AsButton().Invoke();
        try
        {
            FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            NativeUiClipboard.SetText(fixture); owned = GetClipboardSequenceNumber();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_V);
            Wait(() => ReadEditorText(editor) == fixture && preview.Patterns.Text.Pattern.DocumentRange.GetText(-1).Contains("Native sync fixture", StringComparison.Ordinal),
                "The synchronisation fixture did not reach the actual editor and native preview.");
            Find(window, "SyncSelectionControl").AsCheckBox().IsChecked = true;
            Wait(() => ReadPreferences().Desktop?.SyncSelection == true, "Selection synchronisation did not persist.");
            foreach (var mode in new[] { "Continuous", "Pages" })
            {
                var reading = Find(window, "PreviewReadingMode").AsComboBox(); reading.Select(mode); reading.Collapse();
                Wait(() => ReadPreferences().Preview?.ReadingMode == mode, "The explicit sync fixture reading mode did not persist.");
                editor.Focus();
                var editorMatch = Nth(editor, "action", 3); editorMatch.Select();
                Wait(() => preview.Patterns.Text.Pattern.GetSelection().Any(selected => selected.GetText(-1) == "action"
                    && selected.CompareEndpoints(TextPatternRangeEndpoint.Start, Nth(preview, "action", 3), TextPatternRangeEndpoint.Start) == 0
                    && selected.CompareEndpoints(TextPatternRangeEndpoint.End, Nth(preview, "action", 3), TextPatternRangeEndpoint.End) == 0),
                    "Editor-to-preview sync selected the wrong repeated occurrence.");
                if (!editor.Properties.HasKeyboardFocus.Value) throw new IOException("Editor-to-preview selection stole typing focus.");
                var selectedGeometry = preview.Patterns.Text.Pattern.GetSelection().SelectMany(range => range.GetBoundingRectangles()).ToArray();
                if (!selectedGeometry.Any(rectangle => rectangle.Width > 0 && rectangle.Height > 0 && preview.BoundingRectangle.Contains(rectangle)))
                    throw new IOException("The matching preview occurrence has no visible selection geometry.");

                preview.Focus(); var previewMatch = Nth(preview, "action", 4); previewMatch.Select();
                Wait(() => editor.Patterns.Text.Pattern.GetSelection().Any(selected => selected.GetText(-1) == "action"
                    && selected.CompareEndpoints(TextPatternRangeEndpoint.Start, Nth(editor, "action", 4), TextPatternRangeEndpoint.Start) == 0),
                    "Preview-to-editor sync selected the wrong repeated occurrence.");
                if (!preview.Properties.HasKeyboardFocus.Value) throw new IOException("Preview-to-editor selection stole keyboard focus.");
                editor.Focus(); Nth(editor, "Café 😀 é", 1).Select();
                Wait(() => preview.Patterns.Text.Pattern.GetSelection().Any(selected => selected.GetText(-1) == "Café 😀 é"),
                    "Unicode literal selection did not synchronise exactly.");
                observations.Add(new { ReadingMode = mode, RepeatedEditorToPreview = true, RepeatedPreviewToEditor = true, UnicodeExact = true,
                    FocusPreserved = true, Geometry = selectedGeometry });
            }
            Find(window, "SyncSelectionControl").AsCheckBox().IsChecked = false;
            Wait(() => ReadPreferences().Desktop?.SyncSelection == false, "Disabling selection synchronisation did not persist.");
            var retained = preview.Patterns.Text.Pattern.GetSelection().Select(range => range.GetText(-1)).ToArray();
            editor.Focus(); Nth(editor, "action", 2).Select(); Thread.Sleep(180);
            if (!preview.Patterns.Text.Pattern.GetSelection().Select(range => range.GetText(-1)).SequenceEqual(retained))
                throw new IOException("Disabled synchronisation still changed the preview selection.");
            if (ReadEditorText(editor) != fixture || !File.ReadAllBytes(source).SequenceEqual(saved)) throw new IOException("Selection synchronisation changed source bytes.");
            window.CaptureToFile(Path.Combine(output, "native-selection-synchronisation.png"));
            File.WriteAllText(Path.Combine(output, "native-selection-synchronisation.json"), JsonSerializer.Serialize(new {
                Passed = true, Observations = observations, DisabledSettingRespected = true, SourceBytesPreserved = true }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception error)
        {
            originalFailure = error;
            window.CaptureToFile(Path.Combine(output, "native-selection-synchronisation-failure.png"));
            File.WriteAllText(Path.Combine(output, "native-selection-synchronisation-failure.json"), JsonSerializer.Serialize(new { Error = error.ToString(), Observations = observations }));
            throw;
        }
        finally
        {
            try
            {
                var control = window.FindFirstDescendant(cf => cf.ByAutomationId("SyncSelectionControl"));
                if (control is not null) control.AsCheckBox().IsChecked = false;
                var reading = Find(window, "PreviewReadingMode").AsComboBox(); reading.Select(before.Preview?.ReadingMode ?? "Automatic"); reading.Collapse();
                FocusWindow(window); editor.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
                Wait(() => ReadEditorText(editor) == original, "Synchronisation fixture replacement did not retain exact Undo.");
                Find(window, "ToggleSidebar").AsButton().Invoke(); Find(window, "ToggleOutline").AsButton().Invoke();
            }
            catch (Exception error) when (originalFailure is not null)
            { File.WriteAllText(Path.Combine(output, "native-selection-synchronisation-cleanup.json"), JsonSerializer.Serialize(new { OriginalError = originalFailure.ToString(), CleanupError = error.ToString() })); }
            finally { RestoreClipboard(clipboard, owned); }
        }
        UserPreferences ReadPreferences() => JsonSerializer.Deserialize<UserPreferences>(File.ReadAllText(Path.Combine(state, "preferences.json")))!;
        FlaUI.Core.ITextRange Nth(AutomationElement pane, string text, int occurrence)
        {
            var remaining = pane.Patterns.Text.Pattern.DocumentRange.Clone();
            FlaUI.Core.ITextRange? match = null;
            for (var index = 0; index < occurrence; index++)
            {
                match = remaining.FindText(text, false, false) ?? throw new IOException($"Occurrence {occurrence} of '{text}' is absent from the native text pattern.");
                remaining.MoveEndpointByRange(TextPatternRangeEndpoint.Start, match, TextPatternRangeEndpoint.End);
            }
            return match!;
        }
    }
}
