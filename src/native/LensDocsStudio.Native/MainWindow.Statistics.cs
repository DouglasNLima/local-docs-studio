using System.Windows.Threading;
using LensDocsStudio.Core.Review;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private readonly DispatcherTimer statisticsTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private CancellationTokenSource? statisticsCancellation;
    private readonly SemaphoreSlim statisticsGate = new(1, 1);
    private long statisticsGeneration;
    private Guid? statisticsDocument;

    private void InitialiseStatistics()
    {
        model.DocumentChanged += (_, _) => QueueStatistics();
        statisticsTimer.Tick += async (_, _) =>
        {
            statisticsTimer.Stop();
            if (closing || model.ActiveDocument is not { } tab) return;
            var generation = statisticsGeneration;
            var cancellation = new CancellationTokenSource();
            statisticsCancellation = cancellation;
            var token = cancellation.Token;
            var version = tab.EditorDocument.Version;
            var source = tab.EditorDocument.Text;
            var standalone = Path.GetExtension(tab.FilePath ?? tab.DisplayName)?.ToLowerInvariant() is ".mmd" or ".mermaid";
            try
            {
                await statisticsGate.WaitAsync(token);
                DocumentStatistics result;
                try { result = await Task.Run(() => DocumentStatisticsService.Measure(source, standalone, token), token); }
                finally { statisticsGate.Release(); }
                if (Current())
                {
                    DocumentStatisticsButton.Content = result.Summary;
                    DocumentStatisticsButton.ToolTip = result.Summary + ". Open Document Review.";
                    System.Windows.Automation.AutomationProperties.SetName(DocumentStatisticsButton, result.Summary + ". Open Document Review");
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception error) when (error is IOException or System.Text.RegularExpressions.RegexMatchTimeoutException)
            { if (Current()) { DocumentStatisticsButton.Content = "Statistics unavailable"; DocumentStatisticsButton.ToolTip = error.Message; } }
            finally { if (ReferenceEquals(statisticsCancellation, cancellation)) statisticsCancellation = null; cancellation.Dispose(); }
            bool Current() => !closing && !token.IsCancellationRequested && generation == statisticsGeneration
                && model.ActiveDocument == tab && tab.EditorDocument.Version.CompareAge(version) == 0;
        };
        Closed += (_, _) => { statisticsTimer.Stop(); statisticsGeneration++; statisticsCancellation?.Cancel(); };
    }

    private void QueueStatistics()
    {
        statisticsGeneration++;
        statisticsCancellation?.Cancel();
        statisticsTimer.Stop();
        if (statisticsDocument != model.ActiveDocument?.Id)
        {
            statisticsDocument = model.ActiveDocument?.Id;
            DocumentStatisticsButton.Content = model.ActiveDocument is null ? "No document" : "Counting…";
            System.Windows.Automation.AutomationProperties.SetName(DocumentStatisticsButton, "Counting document statistics. Open Document Review");
        }
        DocumentStatisticsButton.IsEnabled = model.ActiveDocument is not null;
        if (!closing) statisticsTimer.Start();
    }
}
