using System.Windows.Automation;
using System.Windows.Input;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private PreviewViewportAnchor? comparisonLayoutAnchor;
    private CancellationTokenSource? comparisonLayoutCancellation;
    private string? comparisonReflowStatus;

    private void BeginComparisonReflow()
    {
        if (comparisonSlots is null) return;
        comparisonReflowStatus = previewLayoutStatus ?? previewBeforeEditorStatus ?? model.PreviewStatus;
        CapturePreviewLayoutAnchor();
        if (comparisonLayoutCancellation is null)
            comparisonLayoutAnchor = comparisonPreviewResult is { } result
                && AutomationProperties.GetItemStatus(comparisonPreview) == "Ready"
                ? PreviewViewportAnchor.Capture(comparisonPreview, result) : null;
        CaptureComparisonView();
        CancelSynchronisation(); CancelComparisonLayout();
        if (previewLayoutAnchor is not null)
        {
            model.PreviewStatus = "Updating preview layout…";
            AutomationProperties.SetItemStatus(Preview, "Busy; preview layout is being updated");
        }
        if (comparisonLayoutAnchor is not null)
            AutomationProperties.SetItemStatus(comparisonPreview, "Busy; comparison layout is being updated");
    }

    private void FinishComparisonReflow()
    {
        if (comparisonSlots is null) return;
        _ = RestorePreviewLayoutAsync(comparisonReflowStatus ?? model.PreviewStatus);
        _ = RestoreComparisonLayoutAsync();
    }

    private void ComparisonResizeKey(object sender, KeyEventArgs args)
    {
        if (args.Key is not (Key.Left or Key.Right or Key.Up or Key.Down)) return;
        // Observe before the native GridSplitter class handles keyboard resizing.
        // Both restorations wait for its ordinary layout pass after this event.
        BeginComparisonReflow(); FinishComparisonReflow();
    }

    private void CancelComparisonLayout()
    {
        var cancellation = comparisonLayoutCancellation;
        comparisonLayoutCancellation = null;
        cancellation?.Cancel();
    }

    private async Task RestoreComparisonLayoutAsync()
    {
        if (comparisonSlots is null || comparisonLayoutAnchor is not { } anchor
            || comparisonPreviewResult is not { } result || anchor.Document != result.Document) return;
        var slot = comparisonSlots[1 - comparisonActive]; var owner = slot.Document;
        var version = owner.EditorDocument.Version; var generation = comparisonGeneration;
        using var cancellation = new CancellationTokenSource();
        comparisonLayoutCancellation = cancellation;
        bool Current() => !closing && !cancellation.IsCancellationRequested && comparisonLayoutCancellation == cancellation
            && comparisonSlots?[1 - comparisonActive] == slot && slot.Mode == "Preview" && comparisonGeneration == generation
            && comparisonRenderedId == owner.Id && comparisonPreviewResult == result
            && version.BelongsToSameDocumentAs(owner.EditorDocument.Version) && owner.EditorDocument.Version.CompareAge(version) == 0
            && comparisonPreview.Document == result.Document;
        AutomationProperties.SetItemStatus(comparisonPreview, "Busy; comparison layout is being updated");
        try
        {
            var restored = await anchor.RestoreAsync(comparisonPreview, Current, cancellation.Token);
            if (Current()) AutomationProperties.SetItemStatus(comparisonPreview,
                restored ? "Ready" : "Comparison preview position could not be restored");
        }
        finally { if (comparisonLayoutCancellation == cancellation) comparisonLayoutCancellation = null; }
    }
}
