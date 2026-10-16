using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using System.Windows.Media;
using System.Runtime.CompilerServices;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Automation.Text;

namespace LensDocsStudio.Native.Rendering;

public static class PreviewNavigationService
{
    private sealed class NavigationState
    {
        public CancellationTokenSource? Pending;
    }
    private static readonly ConditionalWeakTable<FlowDocumentReader, NavigationState> States = new();
    private sealed class NativeInsertionIndex(FlowDocument document)
    {
        public int Symbols { get; } = document.ContentStart.GetOffsetToPosition(document.ContentEnd);
        public List<(TextPointer Position, int Units)> Checkpoints { get; } = [(document.ContentStart.GetInsertionPosition(LogicalDirection.Forward), 0)];
    }
    private static readonly ConditionalWeakTable<FlowDocument, NativeInsertionIndex> InsertionIndices = new();
    public static void CancelPending(FlowDocumentReader viewer)
    { if (States.TryGetValue(viewer, out var state)) state.Pending?.Cancel(); }

    public static Task<bool> BringIntoViewAsync(FlowDocumentReader viewer, TextPointer position, TextPointer? selectionEnd = null, Action<string>? diagnostic = null)
    {
        viewer.VerifyAccess();
        var document = viewer.Document;
        if (document is null || !position.IsInSameDocument(document.ContentStart)
            || selectionEnd is not null && !selectionEnd.IsInSameDocument(document.ContentStart)) return Task.FromResult(false);
        var state = States.GetOrCreateValue(viewer);
        state.Pending?.Cancel();
        var cancellation = new CancellationTokenSource(); state.Pending = cancellation;
        var originalSelectionStart = viewer.Selection?.Start;
        var originalSelectionEnd = viewer.Selection?.End;
        var originalSelectionEmpty = viewer.Selection?.IsEmpty == true;
        return viewer.Dispatcher.InvokeAsync(Navigate, DispatcherPriority.Loaded).Task.Unwrap();

        // Plain navigation may outlive deferred restoration of an empty reader caret.
        // Actual user input cancels Pending; a newer selected range always supersedes us.
        bool Current() => !cancellation.IsCancellationRequested && viewer.Document == document
            && position.IsInSameDocument(document.ContentStart)
            && (originalSelectionStart is null || viewer.Selection is { } selection
                && (selectionEnd is null && originalSelectionEmpty && selection.IsEmpty
                    || selection.Start.CompareTo(originalSelectionStart) == 0 && selection.End.CompareTo(originalSelectionEnd!) == 0));

        async Task<bool> Navigate()
        {
            try
            {
                if (!Current()) return false;
                viewer.ApplyTemplate(); viewer.UpdateLayout();
                if (viewer.ViewingMode == FlowDocumentReaderViewingMode.Scroll)
                {
                    if (FindViewer<FlowDocumentScrollViewer>(viewer) is not { } scrolling) return false;
                    var shown = false;
                    await scrolling.Dispatcher.InvokeAsync(() =>
                    {
                        if (!Current()) return;
                        if (selectionEnd is not null)
                        {
                            if (viewer.Selection is not { } selection) return;
                            selection.Select(position, selectionEnd);
                        }
                        BringIntoScrollView(scrolling, position);
                        shown = true;
                    }, DispatcherPriority.Loaded);
                    return shown && !cancellation.IsCancellationRequested && viewer.Document == document;
                }
                var paginator = (DynamicDocumentPaginator)((IDocumentPaginatorSource)document).DocumentPaginator;
                var page = await GetPageNumber(paginator, position, cancellation.Token);
                diagnostic?.Invoke($"computed page {page + 1}, count {paginator.PageCount}, size {paginator.PageSize}, text offset {document.ContentStart.GetOffsetToPosition(position)}");
                if (!Current() || page < 0 || viewer.ViewingMode == FlowDocumentReaderViewingMode.Scroll) return false;
                if (FindViewer<FlowDocumentPageViewer>(viewer) is not { } paged) return false;
                var selected = false;
                await viewer.Dispatcher.InvokeAsync(async () =>
                {
                    if (!Current() || viewer.ViewingMode == FlowDocumentReaderViewingMode.Scroll || paged.Document != document) return;
                    // Publish the paginator's new page count through WPF binding before
                    // GoToPage; otherwise a freshly replaced reader silently rejects it.
                    diagnostic?.Invoke($"before page selection: reader {viewer.PageNumber}/{viewer.PageCount}, native {paged.MasterPageNumber}/{paged.PageCount}, allowed {paged.CanGoToPage(page + 1)}, size {paginator.PageSize}");
                    paged.GoToPage(page + 1); paged.UpdateLayout();
                    diagnostic?.Invoke($"after page selection: reader {viewer.PageNumber}/{viewer.PageCount}, native {paged.MasterPageNumber}/{paged.PageCount}, size {paginator.PageSize}");
                    if (selectionEnd is null)
                    {
                        // A page number resets WPF's internal content anchor. Its next
                        // layout can re-anchor at the preceding page boundary. The native
                        // text provider scrolls the exact character without selecting it.
                        var anchored = await ScrollExactNativeRangeAsync(document, position, Current, cancellation.Token);
                        diagnostic?.Invoke("exact native range anchor " + anchored);
                        if (!anchored) return;
                    }
                    if (selectionEnd is not null)
                    {
                        if (viewer.Selection is not { } selection) return;
                        selection.Select(position, selectionEnd);
                    }
                    // The exact text position already chose the page. Bringing its parent
                    // paragraph into view would jump back to that paragraph's first page.
                    paged.UpdateLayout();
                    selected = true;
                }, DispatcherPriority.Loaded).Task.Unwrap();
                return selected && !cancellation.IsCancellationRequested && viewer.Document == document;
            }
            catch (OperationCanceledException) { return false; }
            finally
            {
                if (ReferenceEquals(state.Pending, cancellation)) state.Pending = null;
                cancellation.Dispose();
            }
        }
    }

    private static async Task<bool> ScrollExactNativeRangeAsync(FlowDocument document, TextPointer position, Func<bool> current, CancellationToken cancellation)
    {
        var peer = ContentElementAutomationPeer.CreatePeerForElement(document);
        if (peer?.GetPattern(PatternInterface.Text) is not ITextProvider provider) return false;
        var index = InsertionIndices.GetValue(document, key => new(key));
        if (index.Symbols != document.ContentStart.GetOffsetToPosition(document.ContentEnd))
        {
            InsertionIndices.Remove(document); index = InsertionIndices.GetValue(document, key => new(key));
        }
        var target = position.GetInsertionPosition(LogicalDirection.Forward);
        var low = 0; var high = index.Checkpoints.Count;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (index.Checkpoints[middle].Position.CompareTo(target) <= 0) low = middle + 1; else high = middle;
        }
        var (cursor, units) = index.Checkpoints[Math.Max(0, low - 1)];
        var work = 0;
        // UIA Character movement uses these same native insertion boundaries,
        // including Unicode clusters and structural paragraph/object positions.
        // Sparse checkpoints belong only to this immutable native document.
        while (cursor.CompareTo(target) < 0)
        {
            if (cursor.GetNextInsertionPosition(LogicalDirection.Forward) is not { } next) return false;
            cursor = next; units++;
            if (units % 1024 == 0 && cursor.CompareTo(index.Checkpoints[^1].Position) > 0) index.Checkpoints.Add((cursor, units));
            if (++work % 4096 == 0)
            {
                await document.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background, cancellation);
                if (!current()) return false;
            }
        }
        var range = provider.DocumentRange.Clone();
        while (units > 0)
        {
            var chunk = Math.Min(4096, units);
            if (range.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, chunk) != chunk) return false;
            units -= chunk;
            if (units > 0)
            {
                await document.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background, cancellation);
                if (!current()) return false;
            }
        }
        if (!current()) return false;
        range.MoveEndpointByRange(TextPatternRangeEndpoint.End, range, TextPatternRangeEndpoint.Start);
        range.ScrollIntoView(true);
        return true;
    }

    private static async Task<int> GetPageNumber(DynamicDocumentPaginator paginator, TextPointer position, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var request = new object();
        var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Completed(object? sender, GetPageNumberCompletedEventArgs e)
        {
            if (!ReferenceEquals(e.UserState, request)) return;
            if (e.Cancelled) completion.TrySetCanceled(cancellationToken);
            else if (e.Error is { } error) completion.TrySetException(error);
            else completion.TrySetResult(e.PageNumber);
        }
        paginator.GetPageNumberCompleted += Completed;
        try
        {
            paginator.GetPageNumberAsync(position, request);
            using var registration = cancellationToken.Register(() =>
            {
                paginator.CancelAsync(request);
                completion.TrySetCanceled(cancellationToken);
            });
            return await completion.Task;
        }
        finally { paginator.GetPageNumberCompleted -= Completed; }
    }

    private static T? FindViewer<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent is T matching) return matching;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            if (FindViewer<T>(VisualTreeHelper.GetChild(parent, index)) is { } child) return child;
        return null;
    }

    public static async Task BringIntoViewAsync(FlowDocumentScrollViewer viewer, TextPointer position)
    {
        var document = viewer.Document;
        if (!position.IsInSameDocument(document.ContentStart)) return;
        await viewer.Dispatcher.InvokeAsync(() =>
        {
            if (viewer.Document != document || !position.IsInSameDocument(document.ContentStart)) return;
            BringIntoScrollView(viewer, position);
        }, DispatcherPriority.Loaded);
    }

    private static void BringIntoScrollView(FlowDocumentScrollViewer viewer, TextPointer position)
    {
        viewer.UpdateLayout();
        ((FrameworkContentElement?)position.Paragraph ?? position.Parent as FrameworkContentElement)?.BringIntoView();
        viewer.UpdateLayout();
        var rectangle = position.GetCharacterRect(LogicalDirection.Forward);
        if (rectangle.IsEmpty || viewer.Template.FindName("PART_ContentHost", viewer) is not ScrollViewer scrolling) return;
        scrolling.ScrollToVerticalOffset(Math.Max(0, scrolling.VerticalOffset + rectangle.Top - scrolling.ViewportHeight / 3));
    }
}
