using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static void CheckPagination(Window window, AutomationElement editor, UIA3Automation automation, int process,
        string state, string output, string source)
    {
        var original = ReadEditorText(editor); var saved = File.ReadAllBytes(source);
        var preferencesPath = Path.Combine(state, "preferences.json");
        UserPreferences Read() => JsonSerializer.Deserialize<UserPreferences>(ReadObservedText(preferencesPath))!;
        var originalPreferences = Read();
        var previousClipboard = NativeUiClipboard.GetDataObject(); uint owned = 0;
        var fixtures = new[]
        {
            (Marker: "Item 4000", Name: "paginated", Content: "# Paginated native reading\n\n" + string.Join('\n', Enumerable.Range(1, 4000)
                .Select(index => $"- Item {index:D4}: Native documentation retains **portable text** and source-aware navigation."))),
            (Marker: "Final retained target.", Name: "single-block", Content: string.Concat(
                Enumerable.Repeat("Native documentation preserves every word. ", 8192)) + "Final retained target.")
        };
        try
        {
            foreach (var (marker, name, large) in fixtures)
            {
                Layout("Editor"); FocusWindow(window); editor.Click();
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                NativeUiClipboard.SetText(large); owned = GetClipboardSequenceNumber();
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_V);
                Wait(() => ReadEditorText(editor) == large, "The paginated reading fixture did not retain exact editor source.");
                Layout("Preview");
                Wait(() => Find(window, "PreviewStatus").Name.StartsWith("Preview updated", StringComparison.Ordinal)
                    && PageControlsVisible(), "Automatic reading did not expose native pages for the long document.");
                // Logical viewport restoration can correctly retain the final source
                // region after replacing this tab's text. Establish page one explicitly
                // before testing the unchanged Next/Previous page contract.
                // FlowDocumentReader's container cannot accept UIA SetFocus. These
                // text-only fixtures expose its real pointer surface without diagram actions.
                var previewStart = Find(window, "DocumentPreview"); previewStart.Click();
                var firstRange = previewStart.Patterns.Text.Pattern.DocumentRange.FindText(name == "paginated" ? "Paginated native reading" : "Native documentation", false, false)
                    ?? throw new IOException("The paginated fixture's first native text range is unavailable.");
                firstRange.ScrollIntoView(true);
                Wait(() => Find(window, "PreviewStatus").Name.StartsWith("Preview updated", StringComparison.Ordinal)
                    && Find(window, "PreviewPageNumber").Name.StartsWith("Page 1 of ", StringComparison.Ordinal)
                    && Find(window, "PreviewNextPage").IsEnabled, "Automatic reading did not expose native page navigation at the explicitly requested first page.");
                Find(window, "PreviewNextPage").Patterns.Invoke.Pattern.Invoke();
                Wait(() => Find(window, "PreviewPageNumber").Name.StartsWith("Page 2 of ", StringComparison.Ordinal), "Next page did not change the native reading page.");
                Find(window, "PreviewPreviousPage").Patterns.Invoke.Pattern.Invoke();
                Wait(() => Find(window, "PreviewPageNumber").Name.StartsWith("Page 1 of ", StringComparison.Ordinal), "Previous page did not restore the first page.");
                Find(window, "DocumentPreview").Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
                Find(window, "FindText").AsTextBox().Text = marker; Find(window, "FindNext").Click();
                Wait(() =>
                {
                    var preview = Find(window, "DocumentPreview");
                    return preview.Patterns.Text.Pattern.GetSelection().Any(range => range.GetText(-1).EndsWith(marker, StringComparison.Ordinal)
                        && range.GetBoundingRectangles().Any(rectangle => rectangle.Width > 0 && rectangle.Height > 0 && preview.BoundingRectangle.Contains(rectangle)))
                        && !Find(window, "PreviewPageNumber").Name.StartsWith("Page 1 of ", StringComparison.Ordinal);
                }, "Preview Find did not expose its exact final target inside the native page.");
                CheckVisiblePreviewSelection(window, output, marker, name == "paginated" ? "native-preview-selection" : "native-single-block-selection");
                window.CaptureToFile(Path.Combine(output, "native-" + name + "-preview-search.png"));
                Find(window, "CloseFind").Click();
                if (name == "paginated")
                {
                    ReadingMode("Continuous");
                    Wait(() => !PageControlsVisible(), "Explicit Continuous was overridden for the 4,000-item document.");
                    Wait(() => window.FindFirstDescendant(cf => cf.ByAutomationId("ContinuousGuidance")) is { IsOffscreen: false },
                        "Explicit Continuous has no non-blocking guidance for the large document.");
                    Find(window, "DocumentPreview").Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
                    Find(window, "FindText").AsTextBox().Text = marker; Find(window, "FindNext").Click();
                    Wait(() =>
                    {
                        var continuous = Find(window, "DocumentPreview");
                        return continuous.Patterns.Text.Pattern.GetSelection().Any(range => range.GetText(-1).EndsWith(marker, StringComparison.Ordinal)
                            && range.GetBoundingRectangles().Any(rectangle => rectangle.Width > 0 && rectangle.Height > 0
                                && continuous.BoundingRectangle.Contains(rectangle))) && !PageControlsVisible();
                    }, "Continuous Find did not reveal and select the final list item.");
                    CheckVisiblePreviewSelection(window, output, marker, "native-continuous-preview-selection");
                    window.CaptureToFile(Path.Combine(output, "native-continuous-preview-search.png"));
                    Find(window, "CloseFind").Click(); Layout("Split");
                    Wait(() => !PageControlsVisible() && ReadEditorText(editor) == large,
                        "Split layout changed the explicit Continuous mode or exact source.");
                    ReadingMode("Automatic");
                }
                Layout("Editor"); FocusWindow(window); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
                Wait(() => ReadEditorText(editor) == original, "Paged search or navigation damaged the source insertion's Undo.");
                Layout("Split");
                Wait(() => Find(window, "PreviewStatus").Name.StartsWith("Preview updated", StringComparison.Ordinal)
                    && !PageControlsVisible(), "Automatic reading did not return to continuous preview for the restored small document.");
            }
            ReadingMode("Pages"); Wait(PageControlsVisible, "Explicit pages did not expose native page controls for the small document.");
            ReadingMode("Continuous"); Wait(() => !PageControlsVisible(), "Continuous reading did not hide page controls.");
            ReadingMode(originalPreferences.Preview?.ReadingMode ?? "Automatic");
            var after = Read();
            if (WorkspaceSettings.From(after) != WorkspaceSettings.From(originalPreferences) || after.AutomaticUpdateChecks != originalPreferences.AutomaticUpdateChecks
                || after.UpdateChannel != originalPreferences.UpdateChannel || ReadEditorText(editor) != original || !File.ReadAllBytes(source).AsSpan().SequenceEqual(saved))
                throw new IOException("Reading modes changed appearance choices, source files, insertion Undo or update consent.");
        }
        finally { RestoreClipboard(previousClipboard, owned); }

        bool PageControlsVisible() => window.FindFirstDescendant(cf => cf.ByAutomationId("PreviewPageNumber")) is { } page && !page.IsOffscreen;
        void Layout(string name) { Find(window, "ViewMenu").Click(); Find(window, "Layout" + name).Patterns.Invoke.Pattern.Invoke(); }
        void ReadingMode(string mode)
        {
            var settings = OpenSettingsWindow(window, automation, process);
            var choice = Find(settings, "SettingsReadingMode").AsComboBox(); choice.Focus(); choice.Select(mode); choice.Collapse();
            Find(settings, "SettingsApply").Patterns.Invoke.Pattern.Invoke();
            Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByName("Settings"))) is null,
                "The reading-mode settings dialogue did not close.");
            if (Read().Preview?.ReadingMode != mode) throw new IOException("The selected reading mode was not saved.");
        }
    }

    private static void CheckVisiblePreviewSelection(Window window, string output, string marker, string evidenceName)
    {
        var query = Find(window, "FindText"); query.Focus();
        Wait(() => query.Properties.HasKeyboardFocus.Value, "Find did not retain keyboard focus in its query box.");
        var range = Find(window, "DocumentPreview").Patterns.Text.Pattern.GetSelection()
            .Single(range => range.GetText(-1).EndsWith(marker, StringComparison.Ordinal));
        var bounds = range.GetBoundingRectangles().First(rectangle => rectangle.Width > 0 && rectangle.Height > 0);
        var windowBounds = window.BoundingRectangle;
        var region = System.Drawing.Rectangle.FromLTRB(bounds.Left - windowBounds.Left, bounds.Top - windowBounds.Top,
            bounds.Right - windowBounds.Left, bounds.Bottom - windowBounds.Top);
        // Native paint follows the focus notification; never focus the preview to make this pass.
        Thread.Sleep(150);
        using var selected = window.Capture();
        var collapsed = range.Clone();
        collapsed.MoveEndpointByRange(TextPatternRangeEndpoint.End, collapsed, TextPatternRangeEndpoint.Start);
        collapsed.Select();
        var changedPixels = 0;
        try
        {
            Wait(() =>
            {
                using var plain = window.Capture(); changedPixels = 0;
                for (var y = region.Top; y < region.Bottom; y++)
                for (var x = region.Left; x < region.Right; x++)
                {
                    var left = selected.GetPixel(x, y); var right = plain.GetPixel(x, y);
                    if (Math.Abs(left.R - right.R) + Math.Abs(left.G - right.G) + Math.Abs(left.B - right.B) > 6) changedPixels++;
                }
                return changedPixels > region.Width * region.Height / 5;
            }, "The preview Find range has no visible highlight while the query retains keyboard focus.");
            if (!query.Properties.HasKeyboardFocus.Value) throw new IOException("Checking preview selection moved focus away from Find.");
        }
        finally { range.Select(); }
        Thread.Sleep(150);
        File.WriteAllText(Path.Combine(output, evidenceName + ".json"), JsonSerializer.Serialize(new
        { passed = true, marker, bounds, changedPixels, focusRemainsInQuery = query.Properties.HasKeyboardFocus.Value },
            new JsonSerializerOptions { WriteIndented = true }));
    }
}
