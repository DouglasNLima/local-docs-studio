using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;

namespace LensDocsStudio.Native.Rendering;

internal sealed partial record PreviewViewportAnchor
{
    private Task<bool> RestorePageAsync(DocumentPreview viewer, Func<bool> current, CancellationToken cancellation)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var paginator = (DynamicDocumentPaginator)((IDocumentPaginatorSource)Document).DocumentPaginator;
        var request = new object(); var mode = viewer.ViewingMode; var zoom = viewer.Zoom;
        var started = false; var applied = false; var checking = false; var ready = false;
        int? page = null;
        CancellationTokenRegistration registration = default;
        void Finish(bool restored)
        {
            if (completion.Task.IsCompleted) return;
            viewer.LayoutUpdated -= Layout; viewer.Unloaded -= Unloaded;
            paginator.GetPageNumberCompleted -= PageNumber;
            registration.Dispose();
            completion.TrySetResult(restored);
            if (started && page is null) paginator.CancelAsync(request);
        }
        void Unloaded(object sender, RoutedEventArgs args) => Finish(false);
        void Layout(object? sender, EventArgs args) => Check();
        void PageNumber(object sender, GetPageNumberCompletedEventArgs args)
        {
            if (!ReferenceEquals(args.UserState, request)) return;
            if (args.Cancelled || args.Error is not null || args.PageNumber < 0) { Finish(false); return; }
            page = args.PageNumber + 1;
            viewer.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, Check);
        }
        void Check()
        {
            if (!ready || checking || completion.Task.IsCompleted) return;
            checking = true;
            try
            {
                if (cancellation.IsCancellationRequested || !current() || viewer.Document != Document
                    || viewer.ViewingMode != mode || viewer.Zoom != zoom) { Finish(false); return; }
                if (!viewer.IsLoaded || !viewer.IsVisible || !viewer.IsMeasureValid || !viewer.IsArrangeValid) return;
                if (!started)
                {
                    started = true;
                    // Resolve this source position in the new pagination, not the
                    // old page number. The request token cancels only this lookup.
                    paginator.GetPageNumberAsync(Position, request);
                    return;
                }
                if (page is not { } number || PreviewSemanticAnchors.Descendant<FlowDocumentPageViewer>(viewer) is not { } reader) return;
                if (!applied)
                {
                    if (!reader.CanGoToPage(number)) return;
                    applied = true;
                    if (reader.MasterPageNumber != number) { reader.GoToPage(number); return; }
                }
                if (!Position.HasValidLayout) return;
                var rectangle = Position.GetCharacterRect(LogicalDirection.Forward);
                if (!rectangle.IsEmpty && rectangle.Bottom > 0 && rectangle.Top < viewer.ActualHeight) Finish(true);
            }
            finally { checking = false; }
        }
        viewer.LayoutUpdated += Layout; viewer.Unloaded += Unloaded;
        paginator.GetPageNumberCompleted += PageNumber;
        registration = cancellation.Register(() => viewer.Dispatcher.BeginInvoke(() => Finish(false)));
        viewer.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => { ready = true; Check(); });
        return completion.Task;
    }
}
