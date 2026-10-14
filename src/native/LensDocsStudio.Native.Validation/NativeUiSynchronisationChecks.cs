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
                FocusEditor();
                var editorFocus = editor.Automation.FocusedElement();
                var editorMatch = Nth(editor, "action", 3); editorMatch.Select();
                Wait(() => preview.Patterns.Text.Pattern.GetSelection().Any(selected => selected.GetText(-1) == "action"
                    && selected.CompareEndpoints(TextPatternRangeEndpoint.Start, Nth(preview, "action", 3), TextPatternRangeEndpoint.Start) == 0
                    && selected.CompareEndpoints(TextPatternRangeEndpoint.End, Nth(preview, "action", 3), TextPatternRangeEndpoint.End) == 0),
                    "Editor-to-preview sync selected the wrong repeated occurrence.");
                if (!editor.Automation.Compare(editorFocus, editor.Automation.FocusedElement())) throw new IOException("Editor-to-preview selection stole typing focus.");
                var selectedGeometry = preview.Patterns.Text.Pattern.GetSelection().SelectMany(range => range.GetBoundingRectangles()).ToArray();
                if (!selectedGeometry.Any(rectangle => rectangle.Width > 0 && rectangle.Height > 0 && preview.BoundingRectangle.Contains(rectangle)))
                    throw new IOException("The matching preview occurrence has no visible selection geometry.");
                observations.Add(new { ReadingMode = mode, Phase = "Editor to preview", EditorToPreview = true,
                    EditorSelection = editor.Patterns.Text.Pattern.GetSelection().Select(range => range.GetText(-1)).ToArray(),
                    FocusName = editor.Automation.FocusedElement().Name, FocusBounds = editor.Automation.FocusedElement().BoundingRectangle });

                // Focus actual literal text, rather than a blank area of the reader.
                var focusRectangle = Nth(preview, "action", 3).GetBoundingRectangles().First(rectangle => rectangle.Width > 0 && rectangle.Height > 0);
                Mouse.Click(new System.Drawing.Point((int)(focusRectangle.Left + focusRectangle.Width / 2), (int)(focusRectangle.Top + focusRectangle.Height / 2)));
                var previewFocus = preview.Automation.FocusedElement(); var previewMatch = Nth(preview, "action", 4); previewMatch.Select();
                Wait(() => editor.Patterns.Text.Pattern.GetSelection().Any(selected => selected.GetText(-1) == "action"
                    && selected.CompareEndpoints(TextPatternRangeEndpoint.Start, Nth(editor, "action", 4), TextPatternRangeEndpoint.Start) == 0),
                    "Preview-to-editor sync selected the wrong repeated occurrence.");
                if (!preview.Automation.Compare(previewFocus, preview.Automation.FocusedElement())) throw new IOException("Preview-to-editor selection stole keyboard focus.");
                FocusEditor(); Nth(editor, "Café 😀 é", 1).Select();
                Wait(() => preview.Patterns.Text.Pattern.GetSelection().Any(selected => selected.GetText(-1) == "Café 😀 é"),
                    "Unicode literal selection did not synchronise exactly.");
                foreach (var pair in new[] {
                    (Source: "Native sync fixture", Rendered: "Native sync fixture", Occurrence: 1),
                    (Source: "action **action** *action", Rendered: "action action action", Occurrence: 1),
                    (Source: "Soft\nbreak and hard", Rendered: "Soft break and hard", Occurrence: 1),
                    (Source: "hard  \nbreak.", Rendered: "hard\r\nbreak.", Occurrence: 1),
                    (Source: "Café 😀 é action\n\nSoft", Rendered: "Café 😀 é action\r\nSoft", Occurrence: 1)
                })
                {
                    FocusEditor(); Nth(editor, pair.Source, pair.Occurrence).Select();
                    var sourceFocus = editor.Automation.FocusedElement();
                    Wait(() => preview.Patterns.Text.Pattern.GetSelection().Any(selected => selected.GetText(-1) == pair.Rendered),
                        "Heading, formatted multiword or soft-break selection did not reach its exact rendered text.");
                    if (!editor.Automation.Compare(sourceFocus, editor.Automation.FocusedElement())) throw new IOException("Formatted source selection lost its originating typing focus.");
                    var renderedRange = RenderedRange(pair.Rendered);
                    var rectangle = renderedRange.GetBoundingRectangles().First(box => box.Width > 0 && box.Height > 0);
                    Mouse.Click(new System.Drawing.Point((int)(rectangle.Left + rectangle.Width / 2), (int)(rectangle.Top + rectangle.Height / 2)));
                    var renderedFocus = preview.Automation.FocusedElement();
                    RenderedRange(pair.Rendered).Select();
                    Wait(() => editor.Patterns.Text.Pattern.GetSelection().Any(selected => selected.GetText(-1) == pair.Source),
                        "Rendered heading, formatted multiword or soft-break selection lost its exact source endpoints.");
                    if (!preview.Automation.Compare(renderedFocus, preview.Automation.FocusedElement())) throw new IOException("Formatted preview selection lost its originating keyboard focus.");
                    observations.Add(new { ReadingMode = mode, MultiwordSource = pair.Source, Rendered = pair.Rendered, BidirectionalExact = true });
                }
                FocusEditor(); Nth(editor, "action", 5).Select();
                Wait(() => preview.Patterns.Text.Pattern.GetSelection().Any(selected => selected.GetText(-1) == "action"
                    && selected.CompareEndpoints(TextPatternRangeEndpoint.Start, Nth(preview, "action", 5), TextPatternRangeEndpoint.Start) == 0),
                    "Inline code selection lost its exact repeated occurrence.");
                observations.Add(new { ReadingMode = mode, RepeatedEditorToPreview = true, RepeatedPreviewToEditor = true, UnicodeExact = true,
                    FocusPreserved = true, Geometry = selectedGeometry });
            }
            Find(window, "SyncSelectionControl").AsCheckBox().IsChecked = false;
            Wait(() => ReadPreferences().Desktop?.SyncSelection == false, "Disabling selection synchronisation did not persist.");
            var retained = preview.Patterns.Text.Pattern.GetSelection().Single().Clone();
            FocusEditor(); Nth(editor, "action", 2).Select(); Thread.Sleep(180);
            if (!RetainedSelection())
                throw new IOException("Disabled synchronisation still changed the preview selection.");
            Find(window, "SyncSelectionControl").AsCheckBox().IsChecked = true;
            Layout("Editor"); Wait(() => preview.IsOffscreen, "Editor layout did not hide its native preview.");
            FocusEditor(); Nth(editor, "action", 3).Select(); Thread.Sleep(180);
            Layout("Split"); Wait(() => !preview.IsOffscreen, "Split layout did not restore the native preview.");
            if (!RetainedSelection()) throw new IOException("Selection input while the preview was hidden changed its retained occurrence.");
            FocusEditor(); Nth(editor, "action", 4).Select(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_N);
            Wait(() => ReadEditorText(editor).Length == 0, "Selection cancellation did not activate its replacement tab.");
            Thread.Sleep(180);
            if (editor.Patterns.Text.Pattern.GetSelection().Any(range => range.GetText(-1).Length > 0))
                throw new IOException("Stale selection synchronisation modified the replacement document.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_W);
            Wait(() => ReadEditorText(editor) == fixture && preview.Patterns.Text.Pattern.DocumentRange.GetText(-1).Contains("Café 😀 é", StringComparison.Ordinal),
                "The surviving selection fixture did not restore its current preview.");
            FocusEditor(); Nth(editor, "Café 😀 é", 1).Select();
            // Keyboard automation emits separate UTF-16 input events. Use the actual
            // atomic text-paste command for this one-edit/one-Undo Unicode fixture.
            NativeUiClipboard.SetText("Résumé 😀"); owned = GetClipboardSequenceNumber();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_V);
            Wait(() => ReadEditorText(editor) == fixture.Replace("Café 😀 é", "Résumé 😀", StringComparison.Ordinal)
                && preview.Patterns.Text.Pattern.DocumentRange.GetText(-1).Contains("Résumé 😀", StringComparison.Ordinal), "A valid source edit did not reach its current mapped preview.");
            Nth(editor, "Résumé 😀", 1).Select();
            Wait(() => preview.Patterns.Text.Pattern.GetSelection().Any(range => range.GetText(-1) == "Résumé 😀"), "Selection used stale offsets after a valid Unicode source edit.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => ReadEditorText(editor) == fixture, "The mapped Unicode edit did not retain its exact Undo.");
            Find(window, "SyncSelectionControl").AsCheckBox().IsChecked = false;
            if (ReadEditorText(editor) != fixture || !File.ReadAllBytes(source).SequenceEqual(saved)) throw new IOException("Selection synchronisation changed source bytes.");
            window.CaptureToFile(Path.Combine(output, "native-selection-synchronisation.png"));
            File.WriteAllText(Path.Combine(output, "native-selection-synchronisation.json"), JsonSerializer.Serialize(new {
                Passed = true, Observations = observations, DisabledSettingRespected = true, HiddenPaneRespected = true,
                TabCancellation = true, ValidUnicodeEditRemapped = true, SourceBytesPreserved = true }, new JsonSerializerOptions { WriteIndented = true }));

            bool RetainedSelection() => preview.Patterns.Text.Pattern.GetSelection() is { Length: 1 } selection
                && selection[0].CompareEndpoints(TextPatternRangeEndpoint.Start, retained, TextPatternRangeEndpoint.Start) == 0
                && selection[0].CompareEndpoints(TextPatternRangeEndpoint.End, retained, TextPatternRangeEndpoint.End) == 0;
        }
        catch (Exception error)
        {
            originalFailure = error;
            window.CaptureToFile(Path.Combine(output, "native-selection-synchronisation-failure.png"));
            File.WriteAllText(Path.Combine(output, "native-selection-synchronisation-failure.json"), JsonSerializer.Serialize(new {
                Error = error.ToString(), Observations = observations, EditorBounds = editor.BoundingRectangle, PreviewBounds = preview.BoundingRectangle,
                FocusName = editor.Automation.FocusedElement().Name, FocusBounds = editor.Automation.FocusedElement().BoundingRectangle,
                EditorSelection = editor.Patterns.Text.Pattern.GetSelection().Select(range => range.GetText(-1)).ToArray(),
                PreviewSelection = preview.Patterns.Text.Pattern.GetSelection().Select(range => range.GetText(-1)).ToArray() }));
            throw;
        }
        finally
        {
            try
            {
                var control = window.FindFirstDescendant(cf => cf.ByAutomationId("SyncSelectionControl"));
                if (control is not null) control.AsCheckBox().IsChecked = false;
                Layout("Split");
                var reading = Find(window, "PreviewReadingMode").AsComboBox(); reading.Select(before.Preview?.ReadingMode ?? "Automatic"); reading.Collapse();
                FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
                Wait(() => ReadEditorText(editor) == original, "Synchronisation fixture replacement did not retain exact Undo.");
                Find(window, "ToggleSidebar").AsButton().Invoke(); Find(window, "ToggleOutline").AsButton().Invoke();
            }
            catch (Exception error) when (originalFailure is not null)
            { File.WriteAllText(Path.Combine(output, "native-selection-synchronisation-cleanup.json"), JsonSerializer.Serialize(new { OriginalError = originalFailure.ToString(), CleanupError = error.ToString() })); }
            finally { RestoreClipboard(clipboard, owned); }
        }
        void FocusEditor()
        {
            editor.Click(); editor.Focus();
            Wait(() => editor.Properties.HasKeyboardFocus.Value, "The actual editor did not acquire typing focus before its selection input.");
        }
        void Layout(string layout) => (window.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByAutomationId("Layout" + layout)))
            ?? throw new IOException("The native layout command is missing: " + layout)).AsButton().Invoke();
        FlaUI.Core.ITextRange RenderedRange(string text)
        {
            var lines = text.Split("\r\n", StringSplitOptions.None);
            var range = Nth(preview, lines[0], 1);
            if (lines.Length > 1) range.MoveEndpointByRange(TextPatternRangeEndpoint.End, Nth(preview, lines[^1], 1), TextPatternRangeEndpoint.End);
            return range;
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
