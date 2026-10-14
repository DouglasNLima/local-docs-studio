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

    private void InitialiseSynchronisation()
    {
        syncTimer.Tick += SyncTick;
        Editor.TextArea.SelectionChanged += EditorSelectionChanged;
        Editor.TextArea.Caret.PositionChanged += EditorCaretChanged;
        Editor.TextArea.TextView.ScrollOffsetChanged += EditorScrollChanged;
        Preview.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(PreviewScrollChanged), true);
        Preview.LayoutUpdated += PreviewLayoutUpdated;
        Editor.PreviewMouseWheel += UserScrollInput; Preview.PreviewMouseWheel += UserScrollInput;
        Closed += (_, _) =>
        {
            CancelSynchronisation(); syncTimer.Tick -= SyncTick;
            Editor.TextArea.SelectionChanged -= EditorSelectionChanged; Editor.TextArea.Caret.PositionChanged -= EditorCaretChanged;
            Editor.TextArea.TextView.ScrollOffsetChanged -= EditorScrollChanged;
            Preview.LayoutUpdated -= PreviewLayoutUpdated;
            Preview.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(PreviewScrollChanged));
            Editor.PreviewMouseWheel -= UserScrollInput; Preview.PreviewMouseWheel -= UserScrollInput;
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
    }
    private void CancelSynchronisation()
    {
        syncGeneration++; syncTimer.Stop(); pendingSync = null; applyingSync = false;
        PreviewNavigationService.CancelPending(Preview); semanticHighlight?.Show(null);
    }
    private void UserScrollInput(object sender, MouseWheelEventArgs e)
    {
        if (!applyingSync) return;
        CancelSynchronisation();
        if (DesktopState.SyncScroll && BothPanesVisible) QueueSynchronisation(ReferenceEquals(sender, Editor) ? "editor-scroll" : "preview-scroll");
    }
    private void EditorSelectionChanged(object? sender, EventArgs e)
    {
        if (!applyingSync && BothPanesVisible && DesktopState.SyncSelection && Editor.IsKeyboardFocusWithin) QueueSynchronisation("editor-selection");
    }
    private void EditorCaretChanged(object? sender, EventArgs e)
    {
        if (!applyingSync && BothPanesVisible && DesktopState.FollowCaret && Editor.IsKeyboardFocusWithin && Editor.SelectionLength == 0) QueueSynchronisation("caret");
    }
    private void EditorScrollChanged(object? sender, EventArgs e)
    {
        if (!applyingSync && BothPanesVisible && DesktopState.SyncScroll && (Editor.IsKeyboardFocusWithin || Editor.IsMouseOver)) QueueSynchronisation("editor-scroll");
    }
    private void PreviewSelectionChanged(object? sender, EventArgs e)
    {
        if (!applyingSync && BothPanesVisible && DesktopState.SyncSelection && Preview.IsKeyboardFocusWithin) QueueSynchronisation("preview-selection");
    }
    private void PreviewScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalChange != 0 && !applyingSync && BothPanesVisible && DesktopState.SyncScroll && (Preview.IsKeyboardFocusWithin || Preview.IsMouseOver))
            QueueSynchronisation("preview-scroll");
    }
    private void QueueSynchronisation(string origin)
    {
        if (!SyncCurrent) return;
        if (pendingSync?.EndsWith("selection", StringComparison.Ordinal) == true && origin.EndsWith("scroll", StringComparison.Ordinal)) return;
        pendingSync = origin;
        if (!syncTimer.IsEnabled) syncTimer.Start();
    }
    private async void SyncTick(object? sender, EventArgs e)
    {
        syncTimer.Stop(); var origin = pendingSync; pendingSync = null;
        if (origin is null || !SyncCurrent || !BothPanesVisible || applyingSync) return;
        var generation = ++syncGeneration; var result = rendered!;
        applyingSync = true;
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
                }
                result.RevealSource(offset);
                var pointer = result.TextMap.TryPreviewRange(offset, 0, out var literal, out _) ? literal : syncAnchors?.AtSource(offset)?.ContentStart;
                if (pointer is not null) await PreviewNavigationService.BringIntoViewAsync(Preview, pointer);
            }
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException)
        { if (SyncCurrent) model.Status = "Synchronisation is waiting for the current layout: " + error.Message; }
        finally
        {
            if (!Dispatcher.HasShutdownStarted)
            {
                try { await Dispatcher.InvokeAsync(() => { if (syncGeneration == generation) applyingSync = false; }, DispatcherPriority.Background); }
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
    private async void PreviewZoomChanged(object sender, RoutedEventArgs e)
    {
        var difference = (sender as FrameworkElement)?.Tag as string == "in" ? 10 : -10;
        Preview.Zoom = Math.Clamp(Preview.Zoom + difference, 50, 200);
        var zoom = (int)Preview.Zoom;
        await PersistDesktop(current => current with { Preview = (current.Preview ?? new()) with { Zoom = zoom } });
    }
    private void FindPreviewFromToolbar(object sender, RoutedEventArgs e) { previewFocused = true; ShowFind(false); }
}
