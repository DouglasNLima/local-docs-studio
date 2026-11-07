using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;

namespace LensDocsStudio.Native.Rendering;

/// <summary>A visible source position, rather than a pixel offset that changes meaning during reflow.</summary>
internal sealed record PreviewViewportAnchor(FlowDocument Document, TextPointer Position, double VerticalFraction)
{
    internal static PreviewViewportAnchor? Capture(DocumentPreview viewer, PreviewResult result)
    {
        if (viewer.Document != result.Document || !viewer.IsVisible || !viewer.IsMeasureValid || !viewer.IsArrangeValid
            || viewer.ViewingMode != FlowDocumentReaderViewingMode.Scroll || viewer.ActualHeight <= 0
            || result.TextMap.VisibleSource(viewer.ActualHeight) is not { } source
            || !result.TextMap.TryPreviewRange(source, 0, out var position, out _) || position is null || !position.HasValidLayout) return null;
        var rectangle = position.GetCharacterRect(LogicalDirection.Forward);
        if (rectangle.IsEmpty || rectangle.Bottom <= 0 || rectangle.Top >= viewer.ActualHeight) return null;
        return new(result.Document, position, Math.Clamp(rectangle.Top / viewer.ActualHeight, 0, 1));
    }

    internal Task<bool> RestoreAsync(DocumentPreview viewer, Func<bool> current, CancellationToken cancellation)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var zoom = viewer.Zoom;
        double? requestedOffset = null;
        var checking = false;
        CancellationTokenRegistration registration = default;
        void Finish(bool restored)
        {
            viewer.LayoutUpdated -= Layout;
            viewer.Unloaded -= Unloaded;
            registration.Dispose();
            completion.TrySetResult(restored);
        }
        void Unloaded(object sender, RoutedEventArgs args) => Finish(false);
        void Layout(object? sender, EventArgs args) => Check();
        void Check()
        {
            if (completion.Task.IsCompleted || checking) return;
            checking = true;
            try
            {
                if (cancellation.IsCancellationRequested || !current() || viewer.Document != Document
                    || viewer.ViewingMode != FlowDocumentReaderViewingMode.Scroll || viewer.Zoom != zoom)
                { Finish(false); return; }
                if (!viewer.IsVisible || !viewer.IsMeasureValid || !viewer.IsArrangeValid || !Position.HasValidLayout) return;
                if (PreviewSemanticAnchors.Descendant<FlowDocumentScrollViewer>(viewer) is not { } reader
                    || reader.Template?.FindName("PART_ContentHost", reader) is not ScrollViewer scrolling
                    || !scrolling.IsMeasureValid || !scrolling.IsArrangeValid || scrolling.ViewportHeight <= 0) return;
                var rectangle = Position.GetCharacterRect(LogicalDirection.Forward);
                if (rectangle.IsEmpty) return;
                if (requestedOffset is null)
                {
                    // Character rectangles include the reader's zoom; its scroll
                    // offsets remain in document units.
                    var displacement = (rectangle.Top - VerticalFraction * viewer.ActualHeight) / (zoom / 100);
                    var desired = Math.Clamp(scrolling.VerticalOffset + displacement,
                        0, scrolling.ScrollableHeight);
                    requestedOffset = desired;
                    if (Math.Abs(scrolling.VerticalOffset - desired) > 0.5)
                    {
                        // One native scroll request; normal WPF layout commits it. Do not
                        // force layout, reconstruct the document, or retry an empty viewport.
                        scrolling.ScrollToVerticalOffset(desired);
                        return;
                    }
                }
                if (Math.Abs(scrolling.VerticalOffset - requestedOffset.Value) <= 0.5
                    && rectangle.Bottom > 0 && rectangle.Top < viewer.ActualHeight) Finish(true);
            }
            finally { checking = false; }
        }
        viewer.LayoutUpdated += Layout;
        viewer.Unloaded += Unloaded;
        registration = cancellation.Register(() => viewer.Dispatcher.BeginInvoke(() => Finish(false)));
        // The layout mutation has already invalidated Measure/Arrange. Observe its
        // ordinary render pass before deciding where the source position now lies.
        viewer.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, Check);
        return completion.Task;
    }
}
