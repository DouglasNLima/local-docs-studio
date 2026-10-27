using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Document;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private readonly DispatcherTimer syncTimer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(60) };
    private string? pendingSync;
    private bool applyingSync;
    private string? applyingSyncOrigin;
    private long syncGeneration;
    private TextSelection? observedSelection;
    private PreviewSemanticAnchors? syncAnchors;
    private PreviewBlockHighlight? semanticHighlight;
    private ITextSourceVersion? renderedVersion;
    private ITextSourceVersion? parsedVersion;
    private Guid? parsedDocumentId;
    private int lastPreviewPage;
    private bool updatingSyncControls;
    private bool SyncCurrent => model.ActiveDocument is { } active && active.Id == renderedDocumentId && rendered is not null
        && Editor.Document == active.EditorDocument && renderedVersion is not null && Editor.Document.Version.CompareAge(renderedVersion) == 0;
    private bool BothPanesVisible => model.Layout == "Split" && Editor.IsVisible && Preview.IsVisible;

    private (int Start, int Length)? CapturePreviewSelection()
    {
        if (rendered is null || renderedVersion is null || model.ActiveDocument?.Id != renderedDocumentId
            || Preview.Selection is not { IsEmpty: false } selection
            || DesktopState.SyncSelection && Editor.IsKeyboardFocusWithin
            || !rendered.TextMap.TrySourceRange(selection.Start, selection.End, out var start, out var length)) return null;
        var current = Editor.Document.Version;
        var from = renderedVersion.MoveOffsetTo(current, start, AnchorMovementType.AfterInsertion);
        var to = renderedVersion.MoveOffsetTo(current, start + length, AnchorMovementType.BeforeInsertion);
        return from >= 0 && to > from && to <= Editor.Document.TextLength ? (from, to - from) : null;
    }

    private void RestorePreviewSelection((int Start, int Length)? range)
    {
        if (range is not { } saved || rendered is null) return;
        rendered.RevealSource(saved.Start); rendered.RevealSource(saved.Start + saved.Length);
        if (!rendered.TextMap.TryPreviewRange(saved.Start, saved.Length, out var from, out var to) || Preview.Selection is not { } selection) return;
        var previousApplying = applyingSync; var previousOrigin = applyingSyncOrigin;
        applyingSync = true; applyingSyncOrigin = "restore-selection";
        try { selection.Select(from!, to!); }
        finally { applyingSync = previousApplying; applyingSyncOrigin = previousOrigin; }
    }

    private void InitialiseSynchronisation()
    {
        syncTimer.Tick += SyncTick;
        Editor.TextArea.SelectionChanged += EditorSelectionChanged;
        Editor.TextArea.Caret.PositionChanged += EditorCaretChanged;
        Editor.TextArea.TextView.ScrollOffsetChanged += EditorScrollChanged;
        Preview.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(PreviewScrollChanged), true);
        Preview.LayoutUpdated += PreviewLayoutUpdated;
        Editor.PreviewMouseWheel += UserScrollInput; Preview.PreviewMouseWheel += UserScrollInput;
        Editor.PreviewMouseDown += UserSelectionInput; Preview.PreviewMouseDown += UserSelectionInput;
        Closed += (_, _) =>
        {
            CancelSynchronisation(); syncTimer.Tick -= SyncTick;
            Editor.TextArea.SelectionChanged -= EditorSelectionChanged; Editor.TextArea.Caret.PositionChanged -= EditorCaretChanged;
            Editor.TextArea.TextView.ScrollOffsetChanged -= EditorScrollChanged;
            Preview.LayoutUpdated -= PreviewLayoutUpdated;
            Preview.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(PreviewScrollChanged));
            Editor.PreviewMouseWheel -= UserScrollInput; Preview.PreviewMouseWheel -= UserScrollInput;
            Editor.PreviewMouseDown -= UserSelectionInput; Preview.PreviewMouseDown -= UserSelectionInput;
            if (observedSelection is not null) observedSelection.Changed -= PreviewSelectionChanged;
        };
    }
    private void PreviewLayoutUpdated(object? sender, EventArgs e)
    {
        if (semanticHighlight is null && AdornerLayer.GetAdornerLayer(Preview) is { } layer)
        { semanticHighlight = new(Preview); layer.Add(semanticHighlight); }
        if (Preview.Selection != observedSelection)
        {
            if (observedSelection is not null) observedSelection.Changed -= PreviewSelectionChanged;
            observedSelection = Preview.Selection;
            if (observedSelection is not null) observedSelection.Changed += PreviewSelectionChanged;
        }
        if (Preview.PageNumber != lastPreviewPage)
        {
            lastPreviewPage = Preview.PageNumber;
            if (!applyingSync && BothPanesVisible && DesktopState.SyncScroll && Preview.IsKeyboardFocusWithin) QueueSynchronisation("preview-scroll");
        }
    }
    private void InstallSyncPreview(ITextSourceVersion version)
    {
        CancelSynchronisation(); renderedVersion = version;
        syncAnchors = rendered is null ? null : new(rendered);
        semanticHighlight?.Show(null);
        if (semanticHighlight is null && AdornerLayer.GetAdornerLayer(Preview) is { } layer)
        { semanticHighlight = new(Preview); layer.Add(semanticHighlight); }
        if (pendingViewportDocument is { } documentId && renderedDocumentId == documentId)
        {
            var resultToRestore = rendered; var tab = model.ActiveDocument!; var generation = viewportGeneration; var restoreSyncGeneration = syncGeneration;
            var offsetToRestore = Math.Clamp(tab.PreviewSourceOffset, 0, tab.EditorDocument.TextLength);
            applyingSync = true; applyingSyncOrigin = "restore-preview";
            Dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    if (generation != viewportGeneration || restoreSyncGeneration != syncGeneration || model.ActiveDocument != tab || rendered != resultToRestore || !SyncCurrent) return;
                    var pointer = offsetToRestore == 0 ? resultToRestore!.Document.ContentStart
                        : resultToRestore!.TextMap.TryPreviewRange(offsetToRestore, 0, out var literal, out _) ? literal : syncAnchors?.AtSource(offsetToRestore)?.ContentStart;
                    if (pointer is not null) await PreviewNavigationService.BringIntoViewAsync(Preview, pointer);
                }
                finally
                {
                    if (generation == viewportGeneration && model.ActiveDocument == tab && rendered == resultToRestore)
                    {
                        pendingViewportDocument = null; applyingSync = false; applyingSyncOrigin = null;
                        // Actual text selection can arrive while the incoming reading
                        // position is restoring. Reconcile it against this immutable map;
                        // an empty caret must not override a returning tab's viewport.
                        if (DesktopState.SyncSelection && Editor.IsKeyboardFocusWithin && Editor.SelectionLength > 0)
                            QueueSynchronisation("editor-selection");
                    }
                }
            }, DispatcherPriority.Loaded);
            return;
        }
        // An edit can retain the same selected span while replacing its contents.
        // Reconcile that current input once the new immutable map is installed;
        // it need not raise another SelectionChanged event.
        if (QueueCurrentEditorInput()) return;
        var result = rendered; var offset = model.ActiveDocument?.PreviewSourceOffset ?? 0; var restorationGeneration = syncGeneration;
        if (offset > 0)
            Dispatcher.BeginInvoke(async () =>
            {
                if (restorationGeneration != syncGeneration || !SyncCurrent || rendered != result || !Preview.IsVisible) return;
                var pointer = result!.TextMap.TryPreviewRange(offset, 0, out var literal, out _) ? literal : syncAnchors?.AtSource(offset)?.ContentStart;
                if (pointer is null) return;
                var generation = ++syncGeneration; applyingSync = true; applyingSyncOrigin = "restore-preview";
                try { await PreviewNavigationService.BringIntoViewAsync(Preview, pointer); }
                finally { if (syncGeneration == generation) { applyingSync = false; applyingSyncOrigin = null; } }
            }, DispatcherPriority.Loaded);
    }
    private void CancelSynchronisation()
    {
        syncGeneration++; syncTimer.Stop(); pendingSync = null; applyingSync = false; applyingSyncOrigin = null;
        PreviewNavigationService.CancelPending(Preview); EditorNavigationService.CancelPending(Editor); semanticHighlight?.Show(null);
    }
    private void UserScrollInput(object sender, MouseWheelEventArgs e)
    {
        CancelSynchronisation();
        if (DesktopState.SyncScroll && BothPanesVisible) QueueSynchronisation(ReferenceEquals(sender, Editor) ? "editor-scroll" : "preview-scroll");
    }
    private void UserSelectionInput(object sender, MouseButtonEventArgs e) => CancelSynchronisation();
    private void EditorSelectionChanged(object? sender, EventArgs e)
    {
        if (BothPanesVisible && DesktopState.SyncSelection && Editor.IsKeyboardFocusWithin) QueueFocusedSynchronisation("editor-selection");
    }
    private void EditorCaretChanged(object? sender, EventArgs e)
    {
        if (BothPanesVisible && DesktopState.FollowCaret && Editor.IsKeyboardFocusWithin && (Editor.SelectionLength == 0 || !DesktopState.SyncSelection)) QueueFocusedSynchronisation("caret");
    }
    private void EditorScrollChanged(object? sender, EventArgs e)
    {
        if (Editor.IsVisible && (!applyingSync || Editor.IsKeyboardFocusWithin)) recoveryTimer.Start();
        if (!BothPanesVisible || !DesktopState.SyncScroll) return;
        // Fresh input in the focused pane must supersede an asynchronous alignment already
        // running in the other pane. Hover-only events retain the feedback-loop guard.
        if (Editor.IsKeyboardFocusWithin && (!applyingSync || applyingSyncOrigin is not ("preview-scroll" or "preview-selection"))) QueueFocusedSynchronisation("editor-scroll");
        else if (!applyingSync && Editor.IsMouseOver) QueueSynchronisation("editor-scroll");
    }
    private void PreviewSelectionChanged(object? sender, EventArgs e)
    {
        if (BothPanesVisible && DesktopState.SyncSelection && Preview.IsKeyboardFocusWithin) QueueFocusedSynchronisation("preview-selection");
    }
    private void PreviewScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalChange == 0) return;
        if (Preview.IsVisible && (!applyingSync || Preview.IsKeyboardFocusWithin)) recoveryTimer.Start();
        if (!BothPanesVisible || !DesktopState.SyncScroll) return;
        if (Preview.IsKeyboardFocusWithin && (!applyingSync || applyingSyncOrigin is not ("editor-scroll" or "editor-selection" or "caret" or "restore-preview"))) QueueFocusedSynchronisation("preview-scroll");
        else if (!applyingSync && Preview.IsMouseOver) QueueSynchronisation("preview-scroll");
    }
    private void QueueSynchronisation(string origin)
    {
        if (restoringNavigation || bindingViewport || pendingViewportDocument is not null || !SyncCurrent) return;
        if (pendingSync?.EndsWith("selection", StringComparison.Ordinal) == true && origin.EndsWith("scroll", StringComparison.Ordinal)) return;
        pendingSync = origin;
        if (!syncTimer.IsEnabled) syncTimer.Start();
    }
    private void QueueFocusedSynchronisation(string origin)
    {
        // Only the originating pane has keyboard focus. Programmatic changes in the
        // other pane cannot feed back, while fresh input supersedes viewport restoration.
        if (applyingSync) CancelSynchronisation();
        QueueSynchronisation(origin);
    }
    private bool QueueCurrentEditorInput()
    {
        if (!BothPanesVisible || !Editor.IsKeyboardFocusWithin) return false;
        if (DesktopState.SyncSelection && Editor.SelectionLength > 0)
        { QueueFocusedSynchronisation("editor-selection"); return true; }
        if (DesktopState.FollowCaret)
        { QueueFocusedSynchronisation("caret"); return true; }
        return false;
    }
    private async void SyncTick(object? sender, EventArgs e)
    {
        syncTimer.Stop(); var origin = pendingSync; pendingSync = null;
        if (restoringNavigation || origin is null || !SyncCurrent || !BothPanesVisible || applyingSync) return;
        var generation = ++syncGeneration; var result = rendered!;
        applyingSync = true; applyingSyncOrigin = origin;
        TraceViewport("sync-start " + origin);
        try
        {
            if (origin == "preview-selection" && DesktopState.SyncSelection && Preview.Selection is { IsEmpty: false } selection)
            {
                if (result.TextMap.TrySourceRange(selection.Start, selection.End, out var start, out var length))
                { Editor.Select(start, length); semanticHighlight?.Show(null); await EditorNavigationService.BringIntoViewAsync(Editor, start); }
                else if (syncAnchors?.AtPointer(selection.Start) is { } source)
                { Editor.Select(source.Start, Math.Clamp(source.End + 1 - source.Start, 0, Editor.Document.TextLength - source.Start)); model.Status = "Selection synchronised as a source block."; }
            }
            else if (origin == "editor-selection" && DesktopState.SyncSelection)
            {
                var start = Editor.SelectionStart; var length = Editor.SelectionLength;
                if (length == 0)
                {
                    semanticHighlight?.Show(null);
                    if (result.TextMap.TryPreviewRange(Editor.CaretOffset, 0, out var caret, out _) && Preview.Selection is { } empty) empty.Select(caret!, caret!);
                    return;
                }
                result.RevealSource(start);
                if (result.TextMap.TryPreviewRange(start, length, out var from, out var to) && Preview.Selection is { } target)
                { semanticHighlight?.Show(null); await PreviewNavigationService.BringIntoViewAsync(Preview, from!, to!); }
                else if (syncAnchors?.AtSource(start) is { } block)
                {
                    Preview.Selection?.Select(block.ContentStart, block.ContentStart);
                    await PreviewNavigationService.BringIntoViewAsync(Preview, block.ContentStart);
                    if (SyncCurrent && syncGeneration == generation) { semanticHighlight?.Show(block); model.Status = "Selection synchronised as a preview block."; }
                }
            }
            else if (origin == "preview-scroll" && DesktopState.SyncScroll && VisiblePreviewSource() is { } previewSource)
                await EditorNavigationService.BringIntoViewAsync(Editor, previewSource);
            else if (origin == "caret" && DesktopState.FollowCaret || origin == "editor-scroll" && DesktopState.SyncScroll)
            {
                var offset = Editor.CaretOffset;
                if (origin == "editor-scroll")
                {
                    var view = Editor.TextArea.TextView;
                    if (!view.VisualLinesValid) return;
                    var position = view.GetPositionFloor(new Point(0, view.VerticalOffset + view.ActualHeight / 3));
                    if (position is null) return;
                    offset = Editor.Document.GetOffset(position.Value.Line, position.Value.Column);
                    // Prefer a section heading already visible above the scroll point. Its following
                    // paragraph can start on the next printed page; keep that section identifiable.
                    // Long paragraphs/code without a visible heading still use their exact source point.
                    if (view.GetPositionFloor(new Point(0, view.VerticalOffset)) is { } first)
                    {
                        var visibleStart = Editor.Document.GetOffset(first.Line, first.Column);
                        var heading = parsed?.Headings.LastOrDefault(candidate => candidate.SourceStart >= visibleStart && candidate.SourceStart <= offset);
                        if (heading is not null) offset = heading.SourceStart;
                    }
                }
                result.RevealSource(offset);
                var pointer = result.TextMap.TryPreviewRange(offset, 0, out var literal, out _) ? literal : syncAnchors?.AtSource(offset)?.ContentStart;
                if (pointer is not null) await PreviewNavigationService.BringIntoViewAsync(Preview, pointer,
                    diagnostic: viewportDiagnostics ? message => TraceViewport("sync-navigation " + origin + " source " + offset + " " + message) : null);
            }
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException)
        { if (SyncCurrent) model.Status = "Synchronisation is waiting for the current layout: " + error.Message; }
        finally
        {
            if (!Dispatcher.HasShutdownStarted)
            {
                try { await Dispatcher.InvokeAsync(() => { if (syncGeneration == generation) { applyingSync = false; applyingSyncOrigin = null; TraceViewport("sync-complete " + origin); } }, DispatcherPriority.Background); }
                catch (TaskCanceledException) when (Dispatcher.HasShutdownStarted) { }
            }
        }
    }
    private async void SyncPreferenceChanged(object sender, RoutedEventArgs e)
    {
        if (updatingSyncControls || sender is not CheckBox { Tag: string tag } choice) return;
        var enabled = choice.IsChecked == true; CancelSynchronisation();
        await PersistDesktop(current => current with { Desktop = tag switch {
            "caret" => (current.Desktop ?? new()) with { FollowCaret = enabled },
            "scroll" => (current.Desktop ?? new()) with { SyncScroll = enabled },
            _ => (current.Desktop ?? new()) with { SyncSelection = enabled } } });
    }
    private void RefreshSyncControls()
    {
        updatingSyncControls = true;
        try { FollowCaretControl.IsChecked = DesktopState.FollowCaret; SyncScrollControl.IsChecked = DesktopState.SyncScroll; SyncSelectionControl.IsChecked = DesktopState.SyncSelection; }
        finally { updatingSyncControls = false; }
    }
    private async void ReadingModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updatingSyncControls || ReadingModeControl.SelectedItem is not string mode) return;
        CancelSynchronisation();
        await PersistDesktop(current => current with { Preview = (current.Preview ?? new()) with { ReadingMode = mode } });
        Preview.ApplyReadingMode(mode);
    }
    private int? VisiblePreviewSource() => Preview.ViewingMode == FlowDocumentReaderViewingMode.Scroll
        ? rendered?.TextMap.VisibleSource(Preview.ActualHeight) ?? syncAnchors?.VisibleSource(Preview) : syncAnchors?.VisibleSource(Preview);
    private void PreviewZoomChanged(object sender, RoutedEventArgs e)
    {
        var difference = (sender as FrameworkElement)?.Tag as string == "in" ? 10 : -10;
        Preview.Zoom = Math.Clamp(Preview.Zoom + difference, 50, 200);

    }
    private void FindPreviewFromToolbar(object sender, RoutedEventArgs e) { previewFocused = true; ShowFind(false); }
}
