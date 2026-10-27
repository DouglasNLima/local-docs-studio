using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Threading;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private readonly PreviewLoadingState previewLoading = new();
    private readonly Stopwatch previewLoadingClock = Stopwatch.StartNew();
    private readonly DispatcherTimer previewLoadingDelay = new(DispatcherPriority.Background);
    private long announcedPreviewLoading = -1;

    private PreviewLoadingContext? LoadingContext() => model.ActiveDocument is { } tab && model.Layout != "Editor" && !closing
        ? new(tab.Id, diagnosticSourceGeneration,
            $"{model.Theme}/{ThemeService.IsDark(model.Theme)}/{SystemParameters.HighContrast}/{CurrentDiagramTheme}", model.Layout) : null;

    private void InitialisePreviewLoading()
    {
        previewLoadingDelay.Tick += (_, _) => { previewLoadingDelay.Stop(); PresentPreviewLoading(); };
        Closed += (_, _) => { previewLoadingDelay.Stop(); previewLoading.Reset(); };
    }

    private long QueuePreviewLoading(bool start = false)
    {
        if (LoadingContext() is not { } context) { ResetPreviewLoading(); return previewLoading.Generation; }
        // A failed first attachment can have prepared models without an accepted visible document.
        // Retrying it must keep outgoing content masked, even if model assignment had begun.
        var accepted = renderedDocumentId == context.Document && rendered is not null && !previewLoading.MasksContent;
        if (previewLoading.Context?.Document != context.Document) announcedPreviewLoading = -1;
        var ticket = start ? previewLoading.Start(context, accepted, previewLoadingClock.Elapsed)
            : previewLoading.Queue(context, accepted, previewLoadingClock.Elapsed + previewTimer.Interval);
        PresentPreviewLoading();
        return ticket;
    }

    private void PresentPreviewLoading()
    {
        previewLoadingDelay.Stop();
        PreviewContentHost.Visibility = previewLoading.MasksContent ? Visibility.Hidden : Visibility.Visible;
        PreviewLoading.Present(previewLoading, previewLoadingClock.Elapsed);
        AutomationProperties.SetItemStatus(Preview, previewLoading.Busy ? "Busy; preview is being updated"
            : previewLoading.Phase == PreviewLoadingPhase.Failed ? "Preview unavailable" : "Ready");
        if (previewLoading.IsVisible(previewLoadingClock.Elapsed))
        {
            if (announcedPreviewLoading != previewLoading.Generation)
            {
                // One busy announcement per uninterrupted document load; internal stages are quiet.
                if (announcedPreviewLoading < 0) AnnouncePreviewLoading(previewLoading.Refresh ? "Updating preview" : "Loading preview");
                announcedPreviewLoading = previewLoading.Generation;
            }
        }
        else if (previewLoading.Busy)
        {
            previewLoadingDelay.Interval = TimeSpan.FromMilliseconds(Math.Max(1,
                (PreviewLoadingState.VisibilityDelay - (previewLoadingClock.Elapsed - previewLoading.PendingSince)).TotalMilliseconds));
            previewLoadingDelay.Start();
        }
    }

    private void LoadingPhase(long ticket, PreviewLoadingPhase phase)
    {
        if (previewLoading.Advance(ticket, phase)) PresentPreviewLoading();
    }

    private void FinishPreviewLoading(long ticket, PreviewLoadingPhase outcome, bool warnings = false)
    {
        if (!previewLoading.Advance(ticket, outcome)) return;
        PresentPreviewLoading();
        if (announcedPreviewLoading >= 0 && outcome != PreviewLoadingPhase.Cancelled)
            AnnouncePreviewLoading(outcome == PreviewLoadingPhase.Failed ? "Preview could not be updated"
                : warnings ? "Preview ready with rendering errors" : "Preview ready");
        announcedPreviewLoading = -1;
    }

    private void ResetPreviewLoading()
    {
        previewLoading.Reset(); announcedPreviewLoading = -1; PresentPreviewLoading();
    }

    private void AnnouncePreviewLoading(string text)
    {
        AutomationProperties.SetLiveSetting(PreviewStatusLabel, AutomationLiveSetting.Polite);
        AutomationProperties.SetItemStatus(PreviewStatusLabel, text);
        if (UIElementAutomationPeer.FromElement(PreviewStatusLabel) is { } peer)
            peer.RaiseNotificationEvent(AutomationNotificationKind.Other, AutomationNotificationProcessing.MostRecent, text, "PreviewLoading");
    }

    private async void RetryPreview(object sender, RoutedEventArgs e) => await UpdatePreviewAsync();
}
