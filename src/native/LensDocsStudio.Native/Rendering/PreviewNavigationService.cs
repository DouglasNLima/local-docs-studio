using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using System.Windows.Media;
using System.Runtime.CompilerServices;

namespace LensDocsStudio.Native.Rendering;

public static class PreviewNavigationService
{
    private sealed class NavigationState
    {
        public CancellationTokenSource? Pending;
    }
    private static readonly ConditionalWeakTable<FlowDocumentReader, NavigationState> States = new();
    public static void CancelPending(FlowDocumentReader viewer)
    { if (States.TryGetValue(viewer, out var state)) state.Pending?.Cancel(); }

    public static Task<bool> BringIntoViewAsync(FlowDocumentReader viewer, TextPointer position, TextPointer? selectionEnd = null)
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
        return viewer.Dispatcher.InvokeAsync(Navigate, DispatcherPriority.Loaded).Task.Unwrap();

        // Plain navigation may outlive the reader's deferred selection restoration.
        // Actual user input cancels Pending; selection operations also guard their range.
        bool Current() => !cancellation.IsCancellationRequested && viewer.Document == document
            && position.IsInSameDocument(document.ContentStart)
            && (selectionEnd is null || originalSelectionStart is null || viewer.Selection is { } selection
                && selection.Start.CompareTo(originalSelectionStart) == 0 && selection.End.CompareTo(originalSelectionEnd!) == 0);

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
                if (!Current() || page < 0 || viewer.ViewingMode == FlowDocumentReaderViewingMode.Scroll) return false;
                if (FindViewer<FlowDocumentPageViewer>(viewer) is not { } paged) return false;
                paged.GoToPage(page + 1); paged.UpdateLayout();
                var selected = false;
                await viewer.Dispatcher.InvokeAsync(() =>
                {
                    if (!Current()) return;
                    if (selectionEnd is not null)
                    {
                        if (viewer.Selection is not { } selection) return;
                        selection.Select(position, selectionEnd);
                    }
                    // The exact text position already chose the page. Bringing its parent
                    // paragraph into view would jump back to that paragraph's first page.
                    paged.UpdateLayout();
                    selected = true;
                }, DispatcherPriority.Loaded);
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
