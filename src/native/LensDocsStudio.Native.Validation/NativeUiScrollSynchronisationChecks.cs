using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static void CheckScrollSynchronisation(Window window, AutomationElement editor, string state, string output, string source)
    {
        var original = ReadEditorText(editor); var saved = File.ReadAllBytes(source);
        var prior = JsonSerializer.Deserialize<UserPreferences>(File.ReadAllText(Path.Combine(state, "preferences.json")))!;
        var fixture = "# Native scrolling fixture\n\n" + string.Concat(Enumerable.Range(1, 90).Select(index =>
            $"## Region {index:000}\n\nPortable source region {index:000} keeps **formatted content** and local document context.\n\n"
            + (index % 15 == 0 ? "| Item | Detail |\n|---|---|\n| One | Native table |\n| Two | More content |\n| Three | Different height |\n\n" : "")));
        var clipboard = NativeUiClipboard.GetDataObject(); uint owned = 0;
        var preview = Find(window, "DocumentPreview"); var observations = new List<object>();
        Exception? primaryFailure = null;
        Find(window, "ToggleSidebar").AsButton().Invoke(); Find(window, "ToggleOutline").AsButton().Invoke();
        try
        {
            editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            NativeUiClipboard.SetText(fixture); owned = GetClipboardSequenceNumber();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_V);
            Wait(() => ReadEditorText(editor) == fixture && preview.Patterns.Text.Pattern.DocumentRange.GetText(-1).Contains("Region 090", StringComparison.Ordinal),
                "The scroll fixture did not reach its actual native preview.");
            Find(window, "SyncSelectionControl").AsCheckBox().IsChecked = false;
            Find(window, "FollowCaretControl").AsCheckBox().IsChecked = false;
            Find(window, "SyncScrollControl").AsCheckBox().IsChecked = true;
            foreach (var mode in new[] { "Continuous", "Pages" })
            {
                var reading = Find(window, "PreviewReadingMode").AsComboBox(); reading.Select(mode); reading.Collapse();
                FocusEditor(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.HOME);
                var caret = Find(window, "CaretStatus").Name;
                var previewStatus = Find(window, "PreviewStatus").Name;
                var editorFocus = editor.Automation.FocusedElement();
                // Native scrollbar input is intentionally independent of the app's semantic alignment.
                editor.Patterns.Scroll.Pattern.SetScrollPercent(-1, 72);
                Wait(() => VisibleRegions(editor).Length > 0, "The editor's actual visible source regions were unavailable.");
                var regions = VisibleRegions(editor); var anchor = regions[Math.Min(regions.Length - 1, regions.Length / 3)];
                Wait(() => Visible(preview, anchor), "Editor scrolling did not reveal its corresponding semantic preview region.");
                if (Find(window, "CaretStatus").Name != caret || !editor.Automation.Compare(editorFocus, editor.Automation.FocusedElement()))
                    throw new IOException("Editor scroll synchronisation moved the caret or stole keyboard focus.");

                var focusBox = Range(preview, anchor).GetBoundingRectangles().First(box => box.Width > 0 && box.Height > 0);
                Mouse.Click(new System.Drawing.Point((int)(focusBox.Left + focusBox.Width / 2), (int)(focusBox.Top + focusBox.Height / 2)));
                var previewFocus = preview.Automation.FocusedElement(); caret = Find(window, "CaretStatus").Name;
                Range(preview, "Region 015").ScrollIntoView(true);
                Wait(() => Visible(preview, "Region 015"), "The native preview did not reveal its requested scroll target.");
                var previewRegions = VisibleRegions(preview);
                if (previewRegions.Length == 0) throw new IOException("The scrolled native preview has no visible semantic headings.");
                var reverseAnchor = previewRegions[mode == "Pages" ? 0 : Math.Min(previewRegions.Length - 1, previewRegions.Length / 3)];
                Wait(() => Visible(editor, reverseAnchor), "Preview scrolling did not reveal its corresponding visible source region.");
                if (Find(window, "CaretStatus").Name != caret || !preview.Automation.Compare(previewFocus, preview.Automation.FocusedElement()))
                    throw new IOException("Preview scroll synchronisation moved the caret or stole keyboard focus.");
                if (Find(window, "PreviewStatus").Name != previewStatus) throw new IOException("Scroll synchronisation rebuilt the preview.");

                FocusEditor();
                foreach (var percent in new[] { 15d, 75d, 25d, 80d }) editor.Patterns.Scroll.Pattern.SetScrollPercent(-1, percent);
                Wait(() => VisibleRegions(editor).Length > 0, "Rapid scrolling left no visible source anchors.");
                regions = VisibleRegions(editor); anchor = regions[Math.Min(regions.Length - 1, regions.Length / 3)];
                Wait(() => Visible(preview, anchor), "Rapid scrolling did not coalesce to the current source region.");
                observations.Add(new { ReadingMode = mode, EditorToPreview = true, PreviewToEditor = true, RapidLatestRegion = anchor,
                    CaretPreserved = true, FocusPreserved = true, NoPreviewRebuild = true });
            }
            Find(window, "SyncScrollControl").AsCheckBox().IsChecked = false;
            Find(window, "FollowCaretControl").AsCheckBox().IsChecked = true;
            FocusEditor(); Range(editor, "Region 055").Select(); Keyboard.Type(VirtualKeyShort.RIGHT);
            Wait(() => Visible(preview, "Region 055"), "Independent follow-caret did not reveal the current source block.");
            if (!ReadPreferences().Desktop!.FollowCaret || ReadPreferences().Desktop!.SyncScroll || ReadPreferences().Desktop!.SyncSelection)
                throw new IOException("The independent follow-caret setting changed another synchronisation preference.");
            Find(window, "FollowCaretControl").AsCheckBox().IsChecked = false;
            Find(window, "SyncScrollControl").AsCheckBox().IsChecked = true;
            editor.Patterns.Scroll.Pattern.SetScrollPercent(-1, 40);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_N);
            Wait(() => ReadEditorText(editor).Length == 0, "The cancellation fixture did not switch to its new document.");
            Thread.Sleep(180);
            if (ReadEditorText(editor).Length != 0 || editor.Patterns.Text.Pattern.GetSelection().Any(range => range.GetText(-1).Length > 0))
                throw new IOException("Delayed synchronisation from an old tab changed the replacement document.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_W);
            Wait(() => ReadEditorText(editor) == fixture, "Closing the cancellation fixture did not restore its surviving source.");
            if (!File.ReadAllBytes(source).SequenceEqual(saved)) throw new IOException("Scroll or caret synchronisation changed saved source bytes.");
            File.WriteAllText(Path.Combine(output, "native-scroll-synchronisation.json"), JsonSerializer.Serialize(new {
                Passed = true, Observations = observations, IndependentFollowCaret = true, TabCancellation = true, SourceBytesPreserved = true }));
        }
        catch (Exception error)
        {
            primaryFailure = error;
            File.WriteAllText(Path.Combine(output, "native-scroll-synchronisation-failure.json"), JsonSerializer.Serialize(new {
                Error = error.ToString(), Observations = observations }));
            try
            {
                window.CaptureToFile(Path.Combine(output, "native-scroll-synchronisation-failure.png"));
                File.WriteAllText(Path.Combine(output, "native-scroll-synchronisation-geometry.json"), JsonSerializer.Serialize(new {
                    EditorVisible = VisibleRegions(editor), PreviewVisible = VisibleRegions(preview), Caret = Find(window, "CaretStatus").Name }));
            }
            catch (Exception diagnosticError)
            { File.WriteAllText(Path.Combine(output, "native-scroll-synchronisation-diagnostic-error.txt"), diagnosticError.ToString()); }
            throw;
        }
        finally
        {
            try
            {
                Find(window, "FollowCaretControl").AsCheckBox().IsChecked = false; Find(window, "SyncScrollControl").AsCheckBox().IsChecked = false;
                var reading = Find(window, "PreviewReadingMode").AsComboBox(); reading.Select(prior.Preview?.ReadingMode ?? "Automatic"); reading.Collapse();
                editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
                Wait(() => ReadEditorText(editor) == original, "The scroll fixture did not retain its exact insertion Undo.");
                Find(window, "ToggleSidebar").AsButton().Invoke(); Find(window, "ToggleOutline").AsButton().Invoke();
            }
            catch (Exception error) when (primaryFailure is not null)
            { File.WriteAllText(Path.Combine(output, "native-scroll-synchronisation-cleanup.json"), JsonSerializer.Serialize(new { Error = error.ToString() })); }
            finally { RestoreClipboard(clipboard, owned); }
        }
        UserPreferences ReadPreferences() => JsonSerializer.Deserialize<UserPreferences>(File.ReadAllText(Path.Combine(state, "preferences.json")))!;
        // AvalonEdit does not implement GetVisibleRanges. Its text ranges expose
        // actual screen geometry, so inspect our distinct fixture headings directly.
        string[] VisibleRegions(AutomationElement pane) => Enumerable.Range(1, 90).Select(index => $"Region {index:000}").Where(text => Visible(pane, text)).ToArray();
        void FocusEditor()
        {
            editor.Click(); editor.Focus();
            Wait(() => editor.Properties.HasKeyboardFocus.Value, "The actual editor did not acquire focus before its scrolling input.");
        }
        FlaUI.Core.ITextRange Range(AutomationElement pane, string text) => pane.Patterns.Text.Pattern.DocumentRange.FindText(text, false, false) ?? throw new IOException("Missing source region: " + text);
        bool Visible(AutomationElement pane, string text) => Range(pane, text).GetBoundingRectangles().Any(box => box.Width > 0 && box.Height > 0 && pane.BoundingRectangle.Contains(box));
    }
}
