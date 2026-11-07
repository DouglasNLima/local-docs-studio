using System.Windows.Automation;
using System.Windows.Threading;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private PreviewViewportAnchor? previewLayoutAnchor;
    private CancellationTokenSource? previewLayoutCancellation;
    private string? previewLayoutStatus;
    private string? interruptedPreviewLayoutStatus;

    private void CapturePreviewLayoutAnchor()
    {
        if (!SyncCurrent || rendered is null) { previewLayoutAnchor = null; return; }
        // A rapid second layout change retains the original source anchor while
        // its first reflow is pending, rather than capturing an intermediate viewport.
        if (previewLayoutCancellation is null && Preview.IsVisible)
            previewLayoutAnchor = PreviewViewportAnchor.Capture(Preview, rendered);
        if (previewLayoutAnchor?.Document != Preview.Document) previewLayoutAnchor = null;
        // Persist the intended source before geometry changes. A pane switch may
        // cancel presentation while the old page is still an intermediate view.
        if (previewLayoutAnchor is { } anchor && model.ActiveDocument is { } owner
            && rendered.TextMap.TrySourceOffset(anchor.Position, out var source))
            owner.PreviewSourceOffset = source;
    }

    private void CancelPreviewLayout()
    {
        if (previewLayoutCancellation is not { } cancellation) return;
        previewLayoutCancellation = null;
        cancellation.Cancel();
        interruptedPreviewLayoutStatus = previewLayoutStatus;
        previewLayoutStatus = null;
        // Cancellation is not a successful presentation. A replacement render or
        // layout owns its subsequent status; fresh input must never be overridden.
        model.PreviewStatus = "Preview layout changed";
        AutomationProperties.SetItemStatus(Preview, "Preview layout changed");
    }

    private async Task RestorePreviewLayoutAsync(string status, bool afterReattachment = false)
    {
        if (previewLayoutAnchor is not { } anchor || !SyncCurrent || anchor.Document != Preview.Document) return;
        var result = rendered; var owner = model.ActiveDocument; var generation = syncGeneration;
        using var cancellation = new CancellationTokenSource();
        interruptedPreviewLayoutStatus = null;
        previewLayoutCancellation = cancellation; previewLayoutStatus = status;
        model.PreviewStatus = "Updating preview layout…";
        AutomationProperties.SetItemStatus(Preview, "Busy; preview layout is being updated");
        bool Current() => !closing && previewLayoutCancellation == cancellation && generation == syncGeneration
            && model.ActiveDocument == owner && rendered == result && SyncCurrent && model.Layout != "Editor";
        try
        {
            // Reparenting comparison queues an Unloaded/Loaded pair even when
            // the same reader remains attached to this window. Let that lifecycle
            // finish before subscribing to unloading as a cancellation boundary.
            if (afterReattachment)
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded, cancellation.Token);
            if (!Current()) return;
            var restored = await anchor.RestoreAsync(Preview, Current, cancellation.Token);
            if (Current())
            {
                model.PreviewStatus = restored ? status : "Preview layout could not be restored. Retry preview.";
                AutomationProperties.SetItemStatus(Preview, restored ? "Ready" : "Preview unavailable");
                PublishPreviewDiagnostic(restored ? "layout-presented" : "layout-unavailable");
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally
        {
            if (previewLayoutCancellation == cancellation)
            { previewLayoutCancellation = null; previewLayoutStatus = null; }
        }
    }
}
