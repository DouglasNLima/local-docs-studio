using System.Windows.Automation;
using System.Windows.Controls;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private PreviewViewportAnchor? previewLayoutAnchor;
    private CancellationTokenSource? previewLayoutCancellation;
    private string? previewLayoutStatus;
    private long previewLayoutLoadingTicket;

    private void CapturePreviewLayoutAnchor()
    {
        if (!SyncCurrent || rendered is null) { previewLayoutAnchor = null; return; }
        // A rapid second layout change retains the original source anchor while
        // its first reflow is pending, rather than capturing an intermediate viewport.
        if (previewLayoutCancellation is null && Preview.IsVisible)
            previewLayoutAnchor = PreviewViewportAnchor.Capture(Preview, rendered);
        if (previewLayoutAnchor?.Document != Preview.Document) previewLayoutAnchor = null;
    }

    private void CancelPreviewLayout()
    {
        if (previewLayoutCancellation is not { } cancellation) return;
        previewLayoutCancellation = null;
        cancellation.Cancel();
        FinishPreviewLoading(previewLayoutLoadingTicket, PreviewLoadingPhase.Cancelled);
        previewLayoutStatus = null;
        // Cancellation is not a successful presentation. A replacement render or
        // layout owns its subsequent status; fresh input must never be overridden.
        model.PreviewStatus = "Preview layout changed";
        AutomationProperties.SetItemStatus(Preview, "Preview layout changed");
    }

    private async Task RestorePreviewLayoutAsync(string status)
    {
        if (previewLayoutAnchor is not { } anchor) return;
        // Settings can change the effective reading mode while Editor hides the
        // preview. A retained scroll anchor cannot restore Pages; keep its valid
        // document and loading state without starting an inapplicable request.
        if (!SyncCurrent || anchor.Document != Preview.Document
            || Preview.ViewingMode != FlowDocumentReaderViewingMode.Scroll)
        {
            previewLayoutAnchor = null;
            return;
        }
        var result = rendered; var owner = model.ActiveDocument; var generation = syncGeneration;
        var request = diagnosticRequest;
        var zoom = Preview.Zoom; var viewingMode = Preview.ViewingMode;
        var loadingTicket = QueuePreviewLoading(start: true);
        LoadingPhase(loadingTicket, PreviewLoadingPhase.Attaching);
        using var cancellation = new CancellationTokenSource();
        previewLayoutCancellation = cancellation; previewLayoutStatus = status;
        previewLayoutLoadingTicket = loadingTicket;
        model.PreviewStatus = "Updating preview layout…";
        AutomationProperties.SetItemStatus(Preview, "Busy; preview layout is being updated");
        bool Current() => !closing && previewLayoutCancellation == cancellation && generation == syncGeneration
            && request == diagnosticRequest && previewLoading.Generation == loadingTicket
            && model.ActiveDocument == owner && rendered == result && anchor.Document == Preview.Document
            && Preview.Zoom == zoom && Preview.ViewingMode == viewingMode
            && SyncCurrent && model.Layout != "Editor";
        try
        {
            var restored = await anchor.RestoreAsync(Preview, Current, cancellation.Token);
            if (Current())
            {
                model.PreviewStatus = restored ? status : "Preview layout could not be restored. Retry preview.";
                FinishPreviewLoading(loadingTicket, restored ? PreviewLoadingPhase.Completed : PreviewLoadingPhase.Failed,
                    result!.Warnings.Count != 0);
                AutomationProperties.SetItemStatus(Preview, restored ? "Ready" : "Preview unavailable");
                PublishPreviewDiagnostic(restored ? "layout-presented" : "layout-unavailable");
            }
            else if (previewLayoutCancellation == cancellation && previewLoading.Generation == loadingTicket)
                CancelPreviewLayout();
        }
        finally
        {
            if (previewLayoutCancellation == cancellation)
            {
                previewLayoutCancellation = null; previewLayoutStatus = null;
                FinishPreviewLoading(loadingTicket, PreviewLoadingPhase.Cancelled);
            }
        }
    }
}
