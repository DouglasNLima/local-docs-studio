using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using LensDocsStudio.Native.Rendering;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.UxProbe;

// Separate foreground journey: the inherited mixed-document journey remains unchanged.
internal sealed partial class Probe
{
    private bool ComparisonReady(DocumentViewModel owner, bool secondary, string? marker = null)
    {
        var reader = secondary ? Field<DocumentPreview>("comparisonPreview")! : preview;
        var result = Field<PreviewResult>(secondary ? "comparisonPreviewResult" : "rendered");
        var version = Field<ITextSourceVersion>(secondary ? "comparisonRenderedVersion" : "renderedVersion");
        if (result is null || result.Document != reader.Document || !reader.IsVisible || !reader.IsMeasureValid || !reader.IsArrangeValid
            || Field<Guid?>(secondary ? "comparisonRenderedId" : "renderedDocumentId") != owner.Id || version is null
            || !version.BelongsToSameDocumentAs(owner.EditorDocument.Version) || version.CompareAge(owner.EditorDocument.Version) != 0
            || !Program.ComparisonStatusReady(reader, secondary)) return false;
        if (UIElementAutomationPeer.CreatePeerForElement(reader)?.GetPattern(PatternInterface.Text) is not ITextProvider provider
            || !provider.GetVisibleRanges().Any(r => !string.IsNullOrWhiteSpace(r.GetText(128)) && r.GetBoundingRectangles().Length >= 4)) return false;
        if (marker is null) return result.TextMap.VisibleSource(reader.ActualHeight) is not null;
        var sourceOffset = owner.EditorDocument.Text.IndexOf(marker, StringComparison.Ordinal);
        if (!result.TextMap.TryPreviewRange(sourceOffset, marker.Length, out var from, out var to) || from is null || to is null
            || new TextRange(from, to).Text != marker || !from.HasValidLayout) return false;
        var rectangle = from.GetCharacterRect(LogicalDirection.Forward);
        return !rectangle.IsEmpty && rectangle.Height > 0 && rectangle.Top >= 0 && rectangle.Bottom <= reader.ActualHeight;
    }

    private async Task ComparisonMode(int pane, string mode)
    {
        currentAction = $"comparison-mode-{pane}-{mode}";
        await ClickControl("ComparisonMode" + pane);
        Desktop.Key(mode == "Editor" ? (ushort)0x24 : (ushort)0x23); Desktop.Key(0x0D);
        await Until(() => (string?)((ComboBox)Control("ComparisonMode" + pane)).SelectedItem == mode);
    }

    private async Task ComparisonFindEnd(DocumentViewModel owner)
    {
        await PointAt(preview); Desktop.Click(); await Until(() => preview.IsKeyboardFocusWithin);
        Desktop.Key(0x46, 0x11); await Until(() => ((FrameworkElement)window.FindName("FindText")).IsVisible);
        Desktop.Key(0x41, 0x11);
        await Measure("comparison-find-end", "warm", 0, () => { Desktop.Text("ReadableEnd"); Desktop.Key(0x0D); },
            () => FindQueryReady("ReadableEnd", 1) && ComparisonReady(owner, false, "ReadableEnd"));
        Desktop.Key(0x1B); await Until(() => !((FrameworkElement)window.FindName("FindBar")).IsVisible);
    }

    private async Task ComparisonEvidence(string stage)
    {
        await Ui(() =>
        {
            transitions?.Boundary("usable/" + stage);
            var active = model.ActiveDocument!;
            var secondary = Field<DocumentPreview>("comparisonPreview")!;
            object Reader(DocumentPreview reader, PreviewResult? result) => new {
                attachedMatches = reader.Document == result?.Document, reader.IsVisible, reader.IsMeasureValid, reader.IsArrangeValid,
                reader.ActualWidth, reader.ActualHeight, reader.Zoom, mode = reader.ViewingMode.ToString(),
                itemStatus = AutomationProperties.GetItemStatus(reader), source = result?.TextMap.VisibleSource(reader.ActualHeight),
                textPattern = reader.IsVisible && UIElementAutomationPeer.CreatePeerForElement(reader)?.GetPattern(PatternInterface.Text) is ITextProvider provider
                    ? provider.GetVisibleRanges().Select(r => new { meaningful = !string.IsNullOrWhiteSpace(r.GetText(128)), rectangles = r.GetBoundingRectangles() }).ToArray() : null,
                media = result?.Media.Select(m => new { m.Image.ActualWidth, m.Image.ActualHeight }).ToArray() };
            Record(new { kind = "comparison-evidence", stage, active = active.Id, renderedOwner = Field<Guid?>("renderedDocumentId"),
                secondaryOwner = Field<Guid?>("comparisonRenderedId"), generation = Field<long>("comparisonGeneration"), model.PreviewStatus,
                editor.CaretOffset, editor.SelectionStart, editor.SelectionLength,
                buffers = model.Documents.Select(d => new { d.Id, sha256 = Program.Hash(Encoding.UTF8.GetBytes(d.EditorDocument.Text)), d.IsDirty }).ToArray(),
                primary = Reader(preview, Field<PreviewResult>("rendered")), secondary = Reader(secondary, Field<PreviewResult>("comparisonPreviewResult")) });
        });
        Desktop.Save(Desktop.Capture(Desktop.ClientBounds(windowHandle)), Path.Combine(output, stage + ".png"));
    }

    internal async Task RunComparison(long startup)
    {
        await Until(() => window.IsLoaded && !model.IsBusy && model.ActiveDocument?.EditorDocument.Text == source && editor.IsVisible);
        await Ui(() => { window.Activate(); Desktop.SetForegroundWindow(new WindowInteropHelper(window).Handle); });
        Desktop.OwnsForeground(); windowHandle = await Ui(() => new WindowInteropHelper(window).Handle);
        var first = await Ui(() => model.ActiveDocument!);
        var second = await Ui(() => model.Documents.Single(d => d != first));
        var otherSource = await Ui(() => second.EditorDocument.Text);
        var mode = Environment.GetEnvironmentVariable("LENS_NATIVE_COMPARISON_MODE") ?? "Continuous";
        var scenario = Environment.GetEnvironmentVariable("LENS_NATIVE_COMPARISON_SCENARIO") ?? "close";
        await ClickControl("LayoutPreview"); await Until(() => ComparisonReady(first, false));
        Desktop.Key(0x46, 0x12);
        await Until(() => ((Menu)window.FindName("ApplicationMenu")).Items.OfType<MenuItem>().First().IsSubmenuOpen);
        Desktop.Key(0x42);
        await Until(() => System.Windows.Input.Keyboard.FocusedElement is MenuItem item && AutomationProperties.GetAutomationId(item) == "OpenBeside");
        Desktop.Key(0x0D); await Until(() => Field<object>("comparisonSlots") is not null);
        await Until(() => ComparisonReady(first, false) && ComparisonReady(second, true));

        if (scenario == "ownership")
        {
            await ComparisonMode(0, "Editor"); await ComparisonMode(1, "Editor");
            await FocusEditor(); Desktop.Key(0x24, 0x11); Desktop.Text("X"); await Until(() => editor.Text == "X" + source);
            Desktop.Key(0x5A, 0x11); await Until(() => editor.Text == source);
            Desktop.Key(0x59, 0x11); await Until(() => editor.Text == "X" + source);
            Desktop.Key(0x5A, 0x11); await Until(() => editor.Text == source);
            Desktop.Key(0x24, 0x11); Desktop.Key(0x27); Desktop.Key(0x27, 0x10);
            await Until(() => editor.CaretOffset == 2 && editor.SelectionStart == 1 && editor.SelectionLength == 1);
            Desktop.Key(0x46, 0x11); await Until(() => ((FrameworkElement)window.FindName("FindText")).IsVisible);
            Desktop.Key(0x41, 0x11); Desktop.Text("NeedleOne"); await Until(() => FindQueryReady("NeedleOne", 2));
            Desktop.Key(0x1B);
            var firstSelection = await Ui(() => (editor.CaretOffset, editor.SelectionStart, editor.SelectionLength));
            await ClickControl("ActivateComparison1"); await Until(() => model.ActiveDocument == second && editor.Text == otherSource);
            await FocusEditor(); Desktop.Key(0x23, 0x11); Desktop.Key(0x25, 0x10);
            await Until(() => editor.SelectionLength == 1 && editor.CaretOffset == otherSource.Length - 1);
            Desktop.Key(0x46, 0x11); await Until(() => ((FrameworkElement)window.FindName("FindText")).IsVisible);
            Desktop.Key(0x41, 0x11); Desktop.Text("OtherSource"); await Until(() => FindQueryReady("OtherSource", 1)); Desktop.Key(0x1B);
            await ClickControl("ActivateComparison0");
            await Until(() => model.ActiveDocument == first && ((TextBox)window.FindName("FindText")).Text == "NeedleOne");
            if (await Ui(() => (editor.CaretOffset, editor.SelectionStart, editor.SelectionLength)) != firstSelection)
                throw new IOException("The incoming pane overwrote the first document's independent caret or selection.");
            await ComparisonEvidence("editor-editor");
            await ComparisonMode(1, "Preview"); await Until(() => ComparisonReady(second, true));
            await ComparisonEvidence("editor-preview");
            await ComparisonMode(0, "Preview"); await Until(() => ComparisonReady(first, false));
        }
        await ComparisonFindEnd(first);
        currentAction = "comparison-activate-secondary";
        await ClickControl("ActivateComparison1"); await Until(() => model.ActiveDocument == second && ComparisonReady(second, false));
        await ComparisonFindEnd(second);
        currentAction = "comparison-return-primary";
        await ClickControl("ActivateComparison0");
        await Until(() => model.ActiveDocument == first && ComparisonReady(first, false, "ReadableEnd") && ComparisonReady(second, true, "ReadableEnd"));
        await ComparisonEvidence("preview-preview-deep");

        if (scenario == "resize")
        {
            currentAction = "comparison-keyboard-resize";
            var splitter = await Ui(() => Tree(window).OfType<GridSplitter>().Single(e => e.IsVisible && AutomationProperties.GetName(e) == "Resize comparison panes"));
            await PointAt(splitter);
            Desktop.Click();
            await Until(() => splitter.IsKeyboardFocusWithin);
            var before = await Ui(() => preview.RenderSize);
            var direction = await Ui(() => splitter.ResizeDirection == GridResizeDirection.Rows ? (ushort)0x28 : (ushort)0x27);
            for (var i = 0; i < 8; i++) Desktop.Key(direction);
            await Until(() => preview.RenderSize != before && ComparisonReady(first, false, "ReadableEnd") && ComparisonReady(second, true, "ReadableEnd"));
            await ComparisonEvidence("resized-comparison");
            currentAction = "comparison-pointer-resize";
            await PointAt(splitter);
            var dragStart = await Ui(() => splitter.PointToScreen(new Point(splitter.ActualWidth / 2, splitter.ActualHeight / 2)));
            var dragEnd = await Ui(() => splitter.ResizeDirection == GridResizeDirection.Rows
                ? new Point(dragStart.X, dragStart.Y - 40) : new Point(dragStart.X - 40, dragStart.Y));
            before = await Ui(() => preview.RenderSize);
            Desktop.LeftDown();
            try
            {
                await Until(() => splitter.IsMouseCaptured);
                Desktop.Move(dragEnd);
                await Until(() => preview.RenderSize != before);
            }
            finally { Desktop.LeftUp(); }
            await Until(() => ComparisonReady(first, false, "ReadableEnd") && ComparisonReady(second, true, "ReadableEnd"));
            await ComparisonEvidence("dragged-comparison");
            currentAction = "comparison-zoom-in";
            await PointAt(await Ui(() => Tree(window).OfType<Button>().First(e => e.IsVisible && AutomationProperties.GetName(e) == "Zoom in")));
            Desktop.Click(); await Until(() => preview.Zoom > 100 && ComparisonReady(first, false, "ReadableEnd"));
            await ComparisonEvidence("zoomed-comparison");
            currentAction = "comparison-reset-zoom";
            await ClickControl("PreviewZoomLabel"); await Until(() => preview.Zoom == 100 && ComparisonReady(first, false, "ReadableEnd"));
            await ComparisonEvidence("restored-comparison-zoom");
        }
        if (scenario == "pending")
        {
            currentAction = "rapid-comparison-switch";
            for (var i = 0; i < 8; i++) Desktop.Key(0x09, 0x11);
            await Until(() => model.ActiveDocument == first && ComparisonReady(first, false) && ComparisonReady(second, true));
            await ComparisonMode(0, "Editor"); await ComparisonMode(0, "Preview");
            Record(new { kind = "pending-close-observation", layoutPending = await Ui(() => Field<object>("previewLayoutCancellation") is not null) });
        }
        await PointAt(await Ui(() => Control("CloseComparison")));
        await Measure("close-comparison", "warm", 0, Desktop.Click,
            () => Field<object>("comparisonSlots") is null && model.ActiveDocument == first && ComparisonReady(first, false, "ReadableEnd"));
        await ComparisonEvidence("closed-comparison");
        if (!await Ui(() => first.EditorDocument.Text == source && second.EditorDocument.Text == otherSource && model.Documents.Contains(first) && model.Documents.Contains(second))
            || Program.Hash(File.ReadAllBytes(path)) != fixture.Sha256.ToUpperInvariant() || File.ReadAllText(second.FilePath!) != otherSource)
            throw new IOException("Comparison altered fixture files, source buffers or document ownership.");
        Program.Write(output, "comparison-completion.json", new { verdict = "PASS", scenario, mode, sampleCount, secondaryCompletionStatus = Program.SecondaryCompletionStatusAvailable,
            elapsedMs = Stopwatch.GetElapsedTime(startup).TotalMilliseconds, fixture.Sha256, sourceIntegrity = "PASS",
            scope = "Foreground Windows input, desktop captures, source mapping and UIA; bounded evidence, not proof of eliminating every intermittent failure" });
    }
}
