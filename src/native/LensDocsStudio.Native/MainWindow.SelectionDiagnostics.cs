using System.Text.Json;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private readonly bool selectionDiagnostics = Environment.GetEnvironmentVariable("LENS_NATIVE_SELECTION_DIAGNOSTICS") == "1";
    private int selectionDiagnosticCount;
    private int selectionDiagnosticLayouts;
    private bool selectionObservationQueued;
    private FlowDocument? selectionDiagnosticDocument;
    private int selectionDiagnosticRenderRevision;
    private int selectionDiagnosticPosted;
    private int selectionDiagnosticCompleted;
    private int selectionDiagnosticAborted;

    private void InitialiseSelectionDiagnostics()
    {
        if (!selectionDiagnostics) return;
        Preview.LayoutUpdated += (_, _) => selectionDiagnosticLayouts++;
        Editor.LostKeyboardFocus += (_, _) => TraceSelection("editor-focus-lost");
        Preview.LostKeyboardFocus += (_, _) => TraceSelection("preview-focus-lost");
        Dispatcher.Hooks.OperationPosted += Posted;
        Dispatcher.Hooks.OperationCompleted += Completed;
        Dispatcher.Hooks.OperationAborted += Aborted;
        Closed += (_, _) =>
        {
            Dispatcher.Hooks.OperationPosted -= Posted;
            Dispatcher.Hooks.OperationCompleted -= Completed;
            Dispatcher.Hooks.OperationAborted -= Aborted;
        };
        void Posted(object? sender, DispatcherHookEventArgs e) => selectionDiagnosticPosted++;
        void Completed(object? sender, DispatcherHookEventArgs e) => selectionDiagnosticCompleted++;
        void Aborted(object? sender, DispatcherHookEventArgs e) => selectionDiagnosticAborted++;
    }

    // Opt-in, bounded, local-only evidence. Never record text, titles or paths.
    private void TraceSelection(string operation)
    {
        if (!selectionDiagnostics || selectionDiagnosticCount++ >= 2000) return;
        try
        {
            if (!ReferenceEquals(selectionDiagnosticDocument, Preview.Document))
            { selectionDiagnosticDocument = Preview.Document; selectionDiagnosticRenderRevision++; }
            Directory.CreateDirectory(model.StateDirectory);
            File.AppendAllText(Path.Combine(model.StateDirectory, "selection-events.jsonl"), JsonSerializer.Serialize(new {
                Operation = operation, At = System.Diagnostics.Stopwatch.GetTimestamp(),
                Document = model.ActiveDocument?.Id, RenderedDocument = renderedDocumentId, Layout = model.Layout,
                syncGeneration, pendingSync, applyingSyncOrigin, SyncCurrent, BothPanesVisible,
                restoringNavigation, bindingViewport, pendingViewportDocument,
                Start = Editor.SelectionStart, Length = Editor.SelectionLength, Caret = Editor.CaretOffset,
                EditorFocus = Editor.IsKeyboardFocusWithin, PreviewFocus = Preview.IsKeyboardFocusWithin,
                Mouse = System.Windows.Input.Mouse.LeftButton.ToString(),
                ReadingMode = Preview.ViewingMode.ToString(), Zoom = Preview.Zoom,
                SourceVersion = Editor.Document.Version.GetHashCode(),
                RenderedVersion = renderedVersion?.GetHashCode(),
                SourceGeneration = diagnosticSourceGeneration, AcceptedGeneration = diagnosticAcceptedGeneration, RenderRequest = diagnosticRequest,
                RenderRevision = selectionDiagnosticRenderRevision,
                PreviewIdentity = Preview.Document is { } identity ? RuntimeHelpers.GetHashCode(identity) : 0,
                LayoutUpdates = selectionDiagnosticLayouts,
                DispatcherPosted = selectionDiagnosticPosted, DispatcherCompleted = selectionDiagnosticCompleted,
                DispatcherAborted = selectionDiagnosticAborted,
                PreviewStart = Preview.Selection is { } selection && Preview.Document is { } document
                    && selection.Start.IsInSameDocument(document.ContentStart)
                    ? document.ContentStart.GetOffsetToPosition(selection.Start) : (int?)null
            }) + Environment.NewLine);
            if (!selectionObservationQueued && (operation.StartsWith("navigation ", StringComparison.Ordinal)
                || operation.StartsWith("mouse-up ", StringComparison.Ordinal) || operation == "preview-selection-changed"))
            {
                selectionObservationQueued = true;
                Dispatcher.BeginInvoke(() =>
                {
                    selectionObservationQueued = false;
                    ObserveSelectionRendering();
                }, DispatcherPriority.ContextIdle);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { Console.Error.WriteLine("Selection diagnostics could not be retained."); }
    }

    private void ObserveSelectionRendering()
    {
        if (!selectionDiagnostics || selectionDiagnosticCount++ >= 2000 || Dispatcher.HasShutdownStarted) return;
        try
        {
            var document = Preview.Document;
            var selection = Preview.Selection;
            if (document is null || selection is null || !selection.Start.IsInSameDocument(document.ContentStart)) return;
            TextPointer? from = null, to = null;
            var mapped = rendered is not null && rendered.TextMap.TryPreviewRange(Editor.SelectionStart, Editor.SelectionLength, out from, out to);
            // Geometry is observed only after normal dispatcher work. Do not focus,
            // scroll, force layout or select anything to prepare an observation.
            var inner = SelectionDiagnosticViewer(Preview);
            var provider = UIElementAutomationPeer.CreatePeerForElement(Preview)?.GetPattern(PatternInterface.Text) as ITextProvider;
            var ranges = provider?.GetSelection();
            var logicalStart = document.ContentStart.GetOffsetToPosition(selection.Start);
            var logicalEnd = document.ContentStart.GetOffsetToPosition(selection.End);
            File.AppendAllText(Path.Combine(model.StateDirectory, "selection-events.jsonl"), JsonSerializer.Serialize(new {
                Operation = "render-observation", At = System.Diagnostics.Stopwatch.GetTimestamp(), syncGeneration,
                Start = Editor.SelectionStart, Length = Editor.SelectionLength,
                SourceVersion = Editor.Document.Version.GetHashCode(), RenderedVersion = renderedVersion?.GetHashCode(),
                SourceGeneration = diagnosticSourceGeneration, AcceptedGeneration = diagnosticAcceptedGeneration, RenderRequest = diagnosticRequest,
                RenderRevision = selectionDiagnosticRenderRevision, SyncCurrent,
                Mapped = mapped, MappedStart = mapped ? document.ContentStart.GetOffsetToPosition(from!) : (int?)null,
                MappedEnd = mapped ? document.ContentStart.GetOffsetToPosition(to!) : (int?)null,
                LogicalStart = logicalStart, LogicalEnd = logicalEnd,
                LogicalSource = rendered?.TextMap.TrySourceRange(selection.Start, selection.End, out var source, out var length) == true
                    ? new { Start = source, Length = length } : null,
                EditorFocus = Editor.IsKeyboardFocusWithin, PreviewFocus = Preview.IsKeyboardFocusWithin,
                ReaderInactiveHighlight = Preview.IsInactiveSelectionHighlightEnabled,
                Viewer = inner?.GetType().Name, ViewerInactiveHighlight = inner is FlowDocumentScrollViewer continuous
                    ? continuous.IsInactiveSelectionHighlightEnabled : inner is FlowDocumentPageViewer paged ? paged.IsInactiveSelectionHighlightEnabled : (bool?)null,
                HasValidLayout = selection.Start.HasValidLayout && selection.End.HasValidLayout,
                StartScreen = SelectionScreenRectangle(selection.Start, inner, LogicalDirection.Forward),
                EndScreen = SelectionScreenRectangle(selection.End, inner, LogicalDirection.Backward),
                UiaRangeCount = ranges?.Length, UiaRectangles = ranges?.Select(range => range.GetBoundingRectangles()).ToArray(),
                // Compare transient strings only for small ranges; never retain them.
                UiaMatchesLogical = logicalEnd - logicalStart <= 4096 && ranges is { Length: 1 }
                    ? ranges[0].GetText(4097) == selection.Text : (bool?)null,
                EditorScroll = Editor.VerticalOffset, PreviewScroll = inner is FlowDocumentScrollViewer scrolling
                    && scrolling.Template.FindName("PART_ContentHost", scrolling) is ScrollViewer scroll ? scroll.VerticalOffset : (double?)null,
                Page = Preview.PageNumber, Zoom = Preview.Zoom, ReadingMode = Preview.ViewingMode.ToString(),
                LayoutUpdates = selectionDiagnosticLayouts, ObservationPriority = "ContextIdle"
            }) + Environment.NewLine);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        { /* A diagnostic observation must never change the selection workflow. */ }
    }

    private static FrameworkElement? SelectionDiagnosticViewer(DependencyObject parent)
    {
        if (parent is FlowDocumentScrollViewer or FlowDocumentPageViewer)
            return (FrameworkElement)parent;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            if (SelectionDiagnosticViewer(VisualTreeHelper.GetChild(parent, index)) is { } child) return child;
        return null;
    }

    private static double[]? SelectionScreenRectangle(TextPointer pointer, FrameworkElement? viewer, LogicalDirection direction)
    {
        if (!pointer.HasValidLayout || viewer is null) return null;
        var rectangle = pointer.GetCharacterRect(direction);
        if (rectangle.IsEmpty) return null;
        var top = viewer.PointToScreen(rectangle.TopLeft); var bottom = viewer.PointToScreen(rectangle.BottomRight);
        return [top.X, top.Y, bottom.X - top.X, bottom.Y - top.Y];
    }
}
