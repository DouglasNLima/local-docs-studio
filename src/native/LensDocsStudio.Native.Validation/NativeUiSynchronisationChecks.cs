using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    // Real OS input is restricted by Program to disposable hosted Windows runners.
    public static int RunSelectionInteractions(string executable, string output, bool highContrast, bool diagnostics = true, bool expectColdInvisible = false) => Task.Run(() =>
    {
        output = Path.GetFullPath(output); Directory.CreateDirectory(output);
        using var clipboard = NativeUiClipboard.Start();
        var previousClipboard = NativeUiClipboard.GetDataObject(); uint ownedClipboard = 0;
        using var contrast = highContrast ? new NativeTestHighContrast() : null;
        using var automation = new FlaUI.UIA3.UIA3Automation();
        var state = Path.Combine(output, "state"); Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new UserPreferences(
            AutomaticUpdateChecks: false, Theme: "Light", Desktop: new(ShowSidebar: false, FollowCaret: true, SyncScroll: true, SyncSelection: true))));
        var source = SelectionTableFixture.Create(); var path = Path.Combine(output, "table.md"); File.WriteAllText(path, source);
        var saved = File.ReadAllBytes(path); var observations = new List<object>(); var inputTargets = new List<object>();
        var geometryObservationFailures = 0;
        var info = new System.Diagnostics.ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false };
        info.Environment["LENS_NATIVE_SELECTION_DIAGNOSTICS"] = diagnostics ? "1" : "0";
        foreach (var argument in new[] { "--state-directory", state, path }) info.ArgumentList.Add(argument);
        using var app = FlaUI.Core.Application.Launch(info); Window? window = null;
        try
        {
            window = app.GetMainWindow(automation, TimeSpan.FromSeconds(25)) ?? throw new IOException("Selection fixture window missing.");
            FocusWindow(window); var editor = Find(window, "MarkdownEditor"); var preview = Find(window, "DocumentPreview");
            _ = editor.FindAllDescendants();
            Wait(() => ReadEditorText(editor) == source && Find(window, "PreviewStatus").Name.StartsWith("Preview updated", StringComparison.Ordinal), "Selection table did not render.");
            if (highContrast && !NativeTestHighContrast.IsEnabledNow) throw new IOException("Actual Windows High Contrast is required.");
            foreach (var mode in new[] { "Continuous", "Pages" })
            {
                var reading = Find(window, "PreviewReadingMode").AsComboBox(); reading.Select(mode); reading.Collapse();
                Wait(() => (window.FindFirstDescendant(cf => cf.ByAutomationId("PreviewPageNumber")) is { IsOffscreen: false }) == (mode == "Pages"),
                    "The native reader did not apply its requested reading mode.");
                // Keep the viewports left by the preceding interaction. Read-only
                // UIA lookup chooses an already visible word; no Find, Select,
                // Focus, ScrollIntoView or viewport preparation occurs per input.
                NaturalSelections(mode);
                foreach (var row in new[] { 15, 1, 8, 2, 15, 1 })
                foreach (var word in new[] { "planning", "within", "proposer", "RED", "validity" })
                {
                    var occurrence = (row - 1) * (word == "planning" ? 4 : word == "within" ? 3 : word == "validity" ? 3 : 2) + 2;
                    // Heading capitalisation differs, so literal case-sensitive counts start in row Q-01.
                    var target = Occurrence(editor, word, occurrence); PrepareSource(target);
                    var rectangle = target.GetBoundingRectangles().First(box => box.Width > 0 && box.Height > 0 && editor.BoundingRectangle.IntersectsWith(box));
                    // Start with keyboard focus in the actual preview. No editor Focus/Select
                    // call precedes this single completed mouse word-selection operation.
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    Mouse.DoubleClick(new System.Drawing.Point((int)(rectangle.Left + rectangle.Width / 2), (int)(rectangle.Top + rectangle.Height / 2)));
                    AssertSelection(word, occurrence, "double-click", watch);
                }
                // A single drag, followed by keyboard selection, at a repeated occurrence.
                var dragTarget = Occurrence(editor, "planning", 2); PrepareSource(dragTarget);
                var box = dragTarget.GetBoundingRectangles().First(rect => rect.Width > 0 && rect.Height > 0);
                var dragWatch = System.Diagnostics.Stopwatch.StartNew();
                Mouse.MoveTo((int)box.Left, (int)(box.Top + box.Height / 2)); Mouse.Down(MouseButton.Left);
                try
                {
                    Mouse.MoveTo((int)(box.Left + box.Width / 2), (int)(box.Top + box.Height / 2));
                    Thread.Sleep(120); // Deliberately cross the unchanged 60 ms timer while dragging.
                    Mouse.MoveTo((int)box.Right, (int)(box.Top + box.Height / 2));
                }
                finally { Mouse.Up(MouseButton.Left); }
                AssertSelection("planning", 2, "drag", dragWatch);
                ClickSourceStart(Occurrence(editor, "planning", 2)); var keyWatch = System.Diagnostics.Stopwatch.StartNew();
                using (Keyboard.Pressing(VirtualKeyShort.SHIFT))
                    for (var character = 0; character < "planning".Length; character++) Keyboard.Type(VirtualKeyShort.RIGHT);
                AssertSelection("planning", 2, "keyboard", keyWatch);

                PrepareSource(Occurrence(editor, "planning", 1));
                ClickSourceStart(Occurrence(editor, "planning", 1));
                const string crossSource = "planning **planning** and *planning";
                using (Keyboard.Pressing(VirtualKeyShort.SHIFT))
                    for (var character = 0; character < crossSource.Length; character++) Keyboard.Type(VirtualKeyShort.RIGHT);
                var crossExpected = Occurrence(preview, "planning planning and planning", 1);
                Wait(() => preview.Patterns.Text.Pattern.GetSelection().Any(range => range.GetText(-1) == "planning planning and planning"
                    && range.CompareEndpoints(TextPatternRangeEndpoint.Start, crossExpected, TextPatternRangeEndpoint.Start) == 0
                    && range.CompareEndpoints(TextPatternRangeEndpoint.End, crossExpected, TextPatternRangeEndpoint.End) == 0), "Cross-inline keyboard selection did not map to its exact table cell.", 2);
                if (!editor.Properties.HasKeyboardFocus.Value || editor.Patterns.Text.Pattern.GetSelection().Single().GetText(-1) != crossSource)
                    throw new IOException("Cross-inline keyboard selection lost source endpoints or editor focus.");
                observations.Add(new { Mode = mode, Input = "cross-inline-keyboard", SourceOffset = source.IndexOf(crossSource, StringComparison.Ordinal), SourceLength = crossSource.Length, Rendered = "planning planning and planning", ActionCount = 1 });
                var zoom = window.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Zoom in")));
                zoom?.AsButton().Invoke();
                Wait(() => Find(window, "PreviewZoomLabel").Name.Contains("110 percent", StringComparison.Ordinal), "Preview zoom did not reach 110 percent.", 2);
                PrepareSource(Occurrence(editor, "validity", 2));
                var zoomBox = Occurrence(editor, "validity", 2).GetBoundingRectangles().First(rect => rect.Width > 0 && rect.Height > 0);
                var zoomWatch = System.Diagnostics.Stopwatch.StartNew();
                Mouse.DoubleClick(new System.Drawing.Point((int)(zoomBox.Left + zoomBox.Width / 2), (int)(zoomBox.Top + zoomBox.Height / 2)));
                AssertSelection("validity", 2, "zoom", zoomWatch);
                Find(window, "PreviewZoomLabel").AsButton().Invoke();

                PrepareSource(Occurrence(editor, "planning", 1));
                var firstBox = Occurrence(editor, "planning", 1).GetBoundingRectangles().First(rect => rect.Width > 0 && rect.Height > 0);
                var lastBox = Occurrence(editor, "planning", 3).GetBoundingRectangles().First(rect => rect.Width > 0 && rect.Height > 0);
                var rapidWatch = System.Diagnostics.Stopwatch.StartNew();
                DoubleClick(firstBox); DoubleClick(lastBox);
                AssertSelection("planning", 3, "rapid-alternation", rapidWatch);
                Thread.Sleep(180);
                AssertSelection("planning", 3, "rapid-settled", rapidWatch);

                // Replace the selected word immediately, before waiting for selection
                // navigation, then select its new literal after preview regeneration.
                PrepareSource(Occurrence(editor, "planning", 1));
                var editBox = Occurrence(editor, "planning", 1).GetBoundingRectangles().First(rect => rect.Width > 0 && rect.Height > 0);
                DoubleClick(editBox); NativeUiClipboard.SetText("revised"); ownedClipboard = GetClipboardSequenceNumber();
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_V);
                var original = source; var changedAt = source.IndexOf("planning", StringComparison.Ordinal);
                source = source.Remove(changedAt, 8).Insert(changedAt, "revised");
                Wait(() => ReadEditorText(editor) == source && preview.Patterns.Text.Pattern.DocumentRange.GetText(-1).Contains("revised", StringComparison.Ordinal), "Source edit during pending navigation did not regenerate the preview.");
                PrepareSource(Occurrence(editor, "revised", 1));
                var revisedBox = Occurrence(editor, "revised", 1).GetBoundingRectangles().First(rect => rect.Width > 0 && rect.Height > 0);
                var editWatch = System.Diagnostics.Stopwatch.StartNew(); DoubleClick(revisedBox);
                AssertSelection("revised", 1, "regenerated-preview", editWatch);
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z); source = original;
                Wait(() => ReadEditorText(editor) == source && !preview.Patterns.Text.Pattern.DocumentRange.GetText(-1).Contains("revised", StringComparison.Ordinal), "The selection edit did not retain exact Undo.");

                // Syntax-only selections must remove literal highlighting and identify
                // the source block rather than reuse a previous matching word.
                PrepareSource(Occurrence(editor, "planning", 2));
                var beforeSyntax = Occurrence(editor, "planning", 2).GetBoundingRectangles().First(rect => rect.Width > 0 && rect.Height > 0);
                var syntaxWatch = System.Diagnostics.Stopwatch.StartNew(); DoubleClick(beforeSyntax);
                AssertSelection("planning", 2, "before-syntax", syntaxWatch);
                var syntax = Occurrence(editor, "**", 1).GetBoundingRectangles().First(rect => rect.Width > 0 && rect.Height > 0);
                Mouse.MoveTo((int)syntax.Left, (int)(syntax.Top + syntax.Height / 2)); Mouse.Down(MouseButton.Left);
                try { Mouse.MoveTo((int)syntax.Right, (int)(syntax.Top + syntax.Height / 2)); }
                finally { Mouse.Up(MouseButton.Left); }
                Wait(() => editor.Patterns.Text.Pattern.GetSelection().Any(range => range.GetText(-1) == "**")
                    && preview.Patterns.Text.Pattern.GetSelection().All(range => range.GetText(-1).Length == 0)
                    && Find(window, "Status").Name == "Selection synchronised as a preview block.", "Syntax-only selection did not use its semantic block fallback.", 2);
                observations.Add(new { Mode = mode, Input = "syntax-drag", ExactSource = "**", PreviewLiteralCleared = true, SemanticFallback = true, ActionCount = 1 });

                // Preserve the reverse direction using a real preview drag as well.
                var reverse = Occurrence(preview, "RED", 2);
                if (!reverse.GetBoundingRectangles().Any(rect => rect.Width > 0 && rect.Height > 0 && preview.BoundingRectangle.Contains(rect)))
                    reverse.ScrollIntoView(true);
                System.Drawing.Rectangle reverseBox = default;
                Wait(() => (reverseBox = reverse.GetBoundingRectangles().FirstOrDefault(rect => rect.Width > 0 && rect.Height > 0 && preview.BoundingRectangle.Contains(rect))).Width > 0,
                    "Reverse-selection fixture has no visible preview text.", 2);
                Mouse.MoveTo(reverseBox.Left, reverseBox.Top + reverseBox.Height / 2); Mouse.Down(MouseButton.Left);
                try { Mouse.MoveTo(reverseBox.Right, reverseBox.Top + reverseBox.Height / 2); }
                finally { Mouse.Up(MouseButton.Left); }
                var reverseFocus = automation.FocusedElement(); var reverseSource = Occurrence(editor, "RED", 2);
                Wait(() => editor.Patterns.Text.Pattern.GetSelection().Any(range => range.GetText(-1) == "RED"
                    && range.CompareEndpoints(TextPatternRangeEndpoint.Start, reverseSource, TextPatternRangeEndpoint.Start) == 0
                    && range.CompareEndpoints(TextPatternRangeEndpoint.End, reverseSource, TextPatternRangeEndpoint.End) == 0), "Preview drag lost its exact source occurrence.", 2);
                if (!automation.Compare(reverseFocus, automation.FocusedElement()) || editor.Properties.HasKeyboardFocus.Value)
                    throw new IOException("Preview-to-editor drag stole its originating preview focus.");
                observations.Add(new { Mode = mode, Input = "preview-drag", ExactRepeatedOccurrence = true, FocusRetained = true, ActionCount = 1 });

                PrepareSource(Occurrence(editor, "planning", 2));
                var replacementBox = Occurrence(editor, "planning", 2).GetBoundingRectangles().First(rect => rect.Width > 0 && rect.Height > 0);
                DoubleClick(replacementBox); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_N);
                Wait(() => ReadEditorText(editor).Length == 0 && !preview.Patterns.Text.Pattern.DocumentRange.GetText(-1).Contains("Q-15", StringComparison.Ordinal), "Replacement document did not receive its own preview.");
                Thread.Sleep(180);
                if (editor.Patterns.Text.Pattern.GetSelection().Any(range => range.GetText(-1).Length > 0)
                    || preview.Patterns.Text.Pattern.GetSelection().Any(range => range.GetText(-1).Length > 0)) throw new IOException("Obsolete selection reached the replacement document.");
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_W);
                Wait(() => ReadEditorText(editor) == source && preview.Patterns.Text.Pattern.DocumentRange.GetText(-1).Contains("Q-15", StringComparison.Ordinal), "Original document did not return after cancellation.");
                observations.Add(new { Mode = mode, Input = "document-replacement", ObsoleteSelectionRejected = true, OriginalSourceRetained = true });
            }
            if (ReadEditorText(editor) != source || !saved.SequenceEqual(File.ReadAllBytes(path))) throw new IOException("Selection input changed source contents.");
            window.CaptureToFile(Path.Combine(output, "selection-table.png"));
            if (!diagnostics && File.Exists(Path.Combine(state, "selection-events.jsonl"))) throw new IOException("Disabled selection diagnostics still wrote an event log.");
            Write(new { Passed = true, ActualHighContrast = NativeTestHighContrast.IsEnabledNow, Diagnostics = diagnostics, Observations = observations, InputTargets = inputTargets, GeometryObservationFailures = geometryObservationFailures, SourceBytesPreserved = true, SelectionActionRetries = 0 });
            return 0;

            void AssertSelection(string word, int occurrence, string input, System.Diagnostics.Stopwatch watch)
            {
                var expected = Occurrence(preview, word, occurrence);
                bool Matches(FlaUI.Core.ITextRange selected) => selected.CompareEndpoints(TextPatternRangeEndpoint.Start, expected, TextPatternRangeEndpoint.Start) == 0
                    && selected.CompareEndpoints(TextPatternRangeEndpoint.End, expected, TextPatternRangeEndpoint.End) == 0;
                var expectedSource = Occurrence(editor, word, occurrence);
                Wait(() => editor.Patterns.Text.Pattern.GetSelection().Any(selected => selected.GetText(-1) == word
                    && selected.CompareEndpoints(TextPatternRangeEndpoint.Start, expectedSource, TextPatternRangeEndpoint.Start) == 0
                    && selected.CompareEndpoints(TextPatternRangeEndpoint.End, expectedSource, TextPatternRangeEndpoint.End) == 0),
                    "Source input did not select its intended occurrence; preview mapping has not been assessed.", 2);
                Wait(() => preview.Patterns.Text.Pattern.GetSelection().Any(Matches), $"Single {input} failed: {modeName()} {word} occurrence {occurrence}.", 2);
                var selectedSource = editor.Patterns.Text.Pattern.GetSelection().Single();
                var prefix = editor.Patterns.Text.Pattern.DocumentRange.Clone(); prefix.MoveEndpointByRange(TextPatternRangeEndpoint.End, selectedSource, TextPatternRangeEndpoint.Start);
                var offset = prefix.GetText(-1).Length;
                if (selectedSource.CompareEndpoints(TextPatternRangeEndpoint.Start, expectedSource, TextPatternRangeEndpoint.Start) != 0
                    || !editor.Properties.HasKeyboardFocus.Value) throw new IOException("Selection occurrence or originating editor focus changed.");
                var geometry = preview.Patterns.Text.Pattern.GetSelection().SelectMany(range => range.GetBoundingRectangles()).ToArray();
                if (!geometry.Any(rect => rect.Width > 0 && rect.Height > 0 && preview.BoundingRectangle.IntersectsWith(rect))) throw new IOException("Selected preview occurrence has no visible geometry.");
                if (ReadEditorText(editor) != source) throw new IOException("Selection operation changed editor contents.");
                observations.Add(new { Mode = modeName(), Input = input, Word = word, Occurrence = occurrence, SourceOffset = offset,
                    SourceLength = selectedSource.GetText(-1).Length, Rendered = preview.Patterns.Text.Pattern.GetSelection().Single().GetText(-1),
                    Milliseconds = watch.Elapsed.TotalMilliseconds, Geometry = geometry, FocusRetained = true,
                    ActionCount = input is "rapid-alternation" or "natural-rapid" ? 2 : input == "rapid-settled" ? 0 : 1 });
            }
            string modeName() => Find(window, "PreviewReadingMode").AsComboBox().SelectedItem?.Text ?? "unknown";
            void NaturalSelections(string mode)
            {
                var target = VisibleWord();
                Select(target, "natural-inherited");
                Thread.Sleep(150); // Observe native paint after the logical notification.
                window!.CaptureToFile(Path.Combine(output, mode + "-natural-first.png"));
                var firstRange = Occurrence(preview, "planning", target.Occurrence);
                var firstBox = firstRange.GetBoundingRectangles().First(rect => rect.Width > 0 && rect.Height > 0 && preview.BoundingRectangle.Contains(rect));
                var windowBox = window.BoundingRectangle;
                var paintRegion = new System.Drawing.Rectangle(firstBox.Left - windowBox.Left, firstBox.Top - windowBox.Top, firstBox.Width, firstBox.Height);
                using var selectedPaint = window.Capture();
                Select(target, "natural-repeat");
                var box = Box(target.Occurrence);
                Mouse.Click(new System.Drawing.Point(box.Left, box.Top + box.Height / 2));
                Wait(() => editor.Patterns.Text.Pattern.GetSelection().All(range => range.GetText(-1).Length == 0)
                    && preview.Patterns.Text.Pattern.GetSelection().All(range => range.GetText(-1).Length == 0),
                    "Natural single-click caret retained a stale highlight.", 2);
                observations.Add(new { Mode = mode, Input = "natural-caret", ActionCount = 1, PreviewLiteralCleared = true });
                Thread.Sleep(150);
                if (!firstRange.GetBoundingRectangles().Contains(firstBox)) throw new IOException("The cold-paint reference moved; no selection retry was attempted.");
                using (var plainPaint = window.Capture())
                {
                    var changed = 0;
                    for (var y = paintRegion.Top; y < paintRegion.Bottom; y++)
                    for (var x = paintRegion.Left; x < paintRegion.Right; x++)
                    {
                        var selected = selectedPaint.GetPixel(x, y); var plain = plainPaint.GetPixel(x, y);
                        if (Math.Abs(selected.R - plain.R) + Math.Abs(selected.G - plain.G) + Math.Abs(selected.B - plain.B) > 6) changed++;
                    }
                    var visible = changed > paintRegion.Width * paintRegion.Height / 5;
                    observations.Add(new { Mode = mode, Input = "natural-first-paint", ChangedPixels = changed, ScreenRectangle = firstBox,
                        HighlightVisible = visible, ExpectedInvisible = expectColdInvisible, ActionCount = 0 });
                    if (visible == expectColdInvisible) throw new IOException("Cold source selection has an unexpected painted highlight while editor focus is retained.");
                }
                box = Box(target.Occurrence); var watch = System.Diagnostics.Stopwatch.StartNew();
                Mouse.MoveTo(box.Left, box.Top + box.Height / 2); Mouse.Down(MouseButton.Left);
                try { Thread.Sleep(120); Mouse.MoveTo(box.Right, box.Top + box.Height / 2); }
                finally { Mouse.Up(MouseButton.Left); }
                AssertSelection("planning", target.Occurrence, "natural-drag", watch);
                // One explicit focus transition is itself part of the sequence.
                // No source row is prepared after it.
                FocusPreview(); Select(VisibleWord(), "natural-preview-focus");
                Mouse.MoveTo(editor.BoundingRectangle.Left + 90, editor.BoundingRectangle.Top + 150); Mouse.Scroll(-3);
                Thread.Sleep(180); Select(VisibleWord(), "natural-editor-wheel");
                Mouse.MoveTo(preview.BoundingRectangle.Left + 90, preview.BoundingRectangle.Top + 150); Mouse.Scroll(-2);
                Thread.Sleep(180); Select(VisibleWord(), "natural-preview-wheel");
                var first = VisibleWord();
                watch.Restart(); DoubleClick(first.Rectangle);
                // Observe the next visible word after the first input. AvalonEdit
                // may realise wrapped lines during that input; an old distant box
                // must never turn into Point.Empty and repeat the previous click.
                var last = VisibleWord(exceptOccurrence: first.Occurrence);
                inputTargets.Add(new { Mode = mode, Input = "natural-rapid", First = new { first.Occurrence, first.Rectangle }, Last = new { last.Occurrence, last.Rectangle } });
                DoubleClick(last.Rectangle);
                AssertSelection("planning", last.Occurrence, "natural-rapid", watch);
                window!.CaptureToFile(Path.Combine(output, mode + "-natural-selection.png"));

                (int Occurrence, System.Drawing.Rectangle Rectangle) VisibleWord(int exceptOccurrence = 0)
                {
                    (int Occurrence, System.Drawing.Rectangle Rectangle) found = default;
                    Wait(() =>
                    {
                        try
                        {
                            var remaining = editor.Patterns.Text.Pattern.DocumentRange.Clone();
                            for (var occurrence = 1; occurrence <= 60; occurrence++)
                            {
                                var match = remaining.FindText("planning", false, false) ?? throw new IOException("Natural fixture word is absent.");
                                remaining.MoveEndpointByRange(TextPatternRangeEndpoint.Start, match, TextPatternRangeEndpoint.End);
                                var rectangle = match.GetBoundingRectangles().FirstOrDefault(rect => rect.Width > 0 && rect.Height > 0 && editor.BoundingRectangle.Contains(rect));
                                if (rectangle.Width > 0 && occurrence != exceptOccurrence) { found = (occurrence, rectangle); return true; }
                            }
                        }
                        catch (Exception error) when (error is System.Runtime.InteropServices.COMException || error.GetType() == typeof(Exception))
                        {
                            // UIA can reject geometry while AvalonEdit realises a
                            // wrapped line. Repeat only this read, never the input.
                            geometryObservationFailures++;
                        }
                        return false;
                    }, "Natural source geometry remained unavailable; no viewport preparation or selection retry was attempted.", 2);
                    return found;
                }
                System.Drawing.Rectangle Box(int occurrence) => Occurrence(editor, "planning", occurrence).GetBoundingRectangles()
                    .FirstOrDefault(rect => rect.Width > 0 && rect.Height > 0 && editor.BoundingRectangle.Contains(rect));
                void Select((int Occurrence, System.Drawing.Rectangle Rectangle) item, string input)
                {
                    var current = Box(item.Occurrence);
                    if (current.Width == 0) throw new IOException("Natural source word moved before its single input; no retry was attempted.");
                    inputTargets.Add(new { Mode = mode, Input = input, item.Occurrence, Rectangle = current });
                    var timer = System.Diagnostics.Stopwatch.StartNew(); DoubleClick(current);
                    AssertSelection("planning", item.Occurrence, input, timer);
                }
            }
            void DoubleClick(System.Drawing.Rectangle rectangle)
            {
                if (rectangle.Width <= 0 || rectangle.Height <= 0) throw new IOException("Source input has no current geometry; no click or retry was attempted.");
                Mouse.DoubleClick(new System.Drawing.Point(rectangle.Left + rectangle.Width / 2, rectangle.Top + rectangle.Height / 2));
            }
            void ClickSourceStart(FlaUI.Core.ITextRange target)
            {
                var rectangle = target.GetBoundingRectangles().First(rect => rect.Width > 0 && rect.Height > 0 && editor.BoundingRectangle.IntersectsWith(rect));
                Mouse.Click(new System.Drawing.Point(rectangle.Left, rectangle.Top + rectangle.Height / 2));
                Wait(() => editor.Patterns.Text.Pattern.GetSelection().Any(range => range.GetText(-1).Length == 0
                    && range.CompareEndpoints(TextPatternRangeEndpoint.Start, target, TextPatternRangeEndpoint.Start) == 0), "Keyboard fixture did not start at its exact source boundary.", 2);
            }
            void PrepareSource(FlaUI.Core.ITextRange target)
            {
                // Use the app's existing explicit source navigation for setup. The
                // pinned AvalonEdit UIA ScrollIntoView is a no-op; its Select also
                // leaves the caret behind, and wrapped extents are estimated lazily.
                var prefix = editor.Patterns.Text.Pattern.DocumentRange.Clone(); prefix.MoveEndpointByRange(TextPatternRangeEndpoint.End, target, TextPatternRangeEndpoint.Start);
                var offset = prefix.GetText(-1).Length; var lineStart = source.LastIndexOf('\n', Math.Max(0, offset - 1)) + 1;
                var rowId = source.Substring(lineStart + 2, 4);
                editor.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
                Find(window, "FindText").AsTextBox().Text = rowId; Keyboard.Type(VirtualKeyShort.RETURN);
                Wait(() => editor.Patterns.Text.Pattern.GetSelection().Any(range => range.GetText(-1) == rowId), "Explicit source-row navigation did not complete.");
                Keyboard.Type(VirtualKeyShort.ESCAPE);
                Wait(() => target.GetBoundingRectangles().Any(rect => rect.Width > 0 && rect.Height > 0 && editor.BoundingRectangle.IntersectsWith(rect)), "Source viewport preparation did not expose its target.", 2);
                FocusPreview();
                Wait(() => preview.Patterns.Text.Pattern.GetSelection().All(range => range.GetText(-1).Length == 0), "Preview focus click did not clear the preparatory selection.", 2);
            }
            void FocusPreview()
            {
                System.Drawing.Rectangle visible = default;
                Wait(() => (visible = preview.Patterns.Text.Pattern.GetVisibleRanges().SelectMany(range => range.GetBoundingRectangles())
                    .FirstOrDefault(rect => rect.Width > 0 && rect.Height > 0 && preview.BoundingRectangle.Contains(rect))).Width > 0,
                    "Preview focus preparation has no visible native text.", 5);
                Mouse.Click(new System.Drawing.Point((int)(visible.Left + Math.Min(4, visible.Width / 2)), (int)(visible.Top + visible.Height / 2)));
            }
        }
        catch (Exception error)
        {
            window?.CaptureToFile(Path.Combine(output, "selection-table-failure.png"));
            Write(new { Passed = false, Error = error.ToString(), Diagnostics = diagnostics, Observations = observations, InputTargets = inputTargets, GeometryObservationFailures = geometryObservationFailures, SelectionActionRetries = 0 });
            return 1;
        }
        finally { if (!app.HasExited) app.Kill(); RestoreClipboard(previousClipboard, ownedClipboard); }
        void Write(object value) => File.WriteAllText(Path.Combine(output, "selection-interactions.json"), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        static FlaUI.Core.ITextRange Occurrence(AutomationElement pane, string word, int occurrence)
        {
            var remaining = pane.Patterns.Text.Pattern.DocumentRange.Clone(); FlaUI.Core.ITextRange? match = null;
            for (var i = 0; i < occurrence; i++)
            {
                match = remaining.FindText(word, false, false) ?? throw new IOException($"Missing {word} occurrence {occurrence}.");
                remaining.MoveEndpointByRange(TextPatternRangeEndpoint.Start, match, TextPatternRangeEndpoint.End);
            }
            return match!;
        }
    }).GetAwaiter().GetResult();

    private static void CheckSynchronisation(Window window, AutomationElement editor, string state, string output, string source)
    {
        var original = ReadEditorText(editor); var saved = File.ReadAllBytes(source);
        var before = JsonSerializer.Deserialize<UserPreferences>(ReadObservedText(Path.Combine(state, "preferences.json")))!;
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
            SetPreviewSynchronisation(window, "SyncSelectionControl", true);
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
            SetPreviewSynchronisation(window, "SyncSelectionControl", false);
            Wait(() => ReadPreferences().Desktop?.SyncSelection == false, "Disabling selection synchronisation did not persist.");
            var retained = preview.Patterns.Text.Pattern.GetSelection().Single().Clone();
            FocusEditor(); Nth(editor, "action", 2).Select(); Thread.Sleep(180);
            if (!RetainedSelection())
                throw new IOException("Disabled synchronisation still changed the preview selection.");
            SetPreviewSynchronisation(window, "SyncSelectionControl", true);
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
            SetPreviewSynchronisation(window, "SyncSelectionControl", false);
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
        UserPreferences ReadPreferences() => JsonSerializer.Deserialize<UserPreferences>(ReadObservedText(Path.Combine(state, "preferences.json")))!;
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
