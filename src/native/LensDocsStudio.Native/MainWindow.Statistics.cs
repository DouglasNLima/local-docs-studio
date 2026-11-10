using System.Windows.Automation;
using System.Windows.Threading;
using LensDocsStudio.Core.Review;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private readonly DispatcherTimer statisticsTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly SemaphoreSlim statisticsGate = new(1, 1);
    private CancellationTokenSource? statisticsCancellation;
    private long statisticsGeneration;
    private bool statisticsClosed;
    private const string StatisticsHelp = "Readable text, including image descriptions; excludes code and Mermaid. Characters include normalised spaces and count Unicode scalars. Reading time uses 220 words per minute. Open Document Review.";

    private void InitialiseStatistics()
    {
        model.DocumentChanged += StatisticsDocumentChanged;
        statisticsTimer.Tick += StatisticsTick;
        Closed += (_, _) =>
        {
            statisticsClosed = true; statisticsGeneration++;
            statisticsTimer.Stop(); statisticsCancellation?.Cancel();
            model.DocumentChanged -= StatisticsDocumentChanged;
            statisticsTimer.Tick -= StatisticsTick;
        };
        QueueStatistics();
    }

    private void StatisticsDocumentChanged(object? sender, EventArgs args) => QueueStatistics();
    private async void StatisticsTick(object? sender, EventArgs args) => await UpdateStatisticsAsync();

    private void QueueStatistics()
    {
        statisticsGeneration++;
        statisticsCancellation?.Cancel(); statisticsTimer.Stop();
        if (statisticsClosed || closing) return;
        DocumentStatisticsButton.IsEnabled = model.ActiveDocument is not null;
        SetStatisticsStatus(model.ActiveDocument is null ? "No document" : "Counting…");
        if (model.ActiveDocument is not null) statisticsTimer.Start();
    }

    private void SetStatisticsStatus(string summary)
    {
        DocumentStatisticsButton.Content = summary;
        DocumentStatisticsButton.ToolTip = summary + ". " + StatisticsHelp;
        AutomationProperties.SetName(DocumentStatisticsButton, summary + (model.ActiveDocument is null ? "" : ". Open Document Review"));
        AutomationProperties.SetHelpText(DocumentStatisticsButton, StatisticsHelp);
    }

    private async Task UpdateStatisticsAsync()
    {
        statisticsTimer.Stop();
        if (statisticsClosed || closing || model.ActiveDocument is not { } tab) return;
        statisticsCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        statisticsCancellation = cancellation;
        var token = cancellation.Token;
        var generation = statisticsGeneration;
        var document = tab.EditorDocument;
        var snapshot = document.CreateSnapshot();
        var identity = tab.FilePath ?? tab.DisplayName;
        var standalone = Path.GetExtension(identity)?.ToLowerInvariant() is ".mmd" or ".mermaid";
        try
        {
            await statisticsGate.WaitAsync(token);
            DocumentStatistics result;
            try
            {
                result = await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();
                    if (snapshot.TextLength > DocumentReviewService.MaximumSourceCharacters)
                        throw new IOException("Document statistics support up to 4 million source characters.");
                    return DocumentStatisticsService.Measure(snapshot.Text, standalone, token);
                }, token);
            }
            finally { statisticsGate.Release(); }
            if (Current()) SetStatisticsStatus(result.Summary);
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is IOException or System.Text.RegularExpressions.RegexMatchTimeoutException)
        { if (Current()) SetStatisticsStatus("Statistics unavailable"); }
        finally { if (ReferenceEquals(statisticsCancellation, cancellation)) statisticsCancellation = null; }

        bool Current() => !statisticsClosed && !closing && !token.IsCancellationRequested
            && generation == statisticsGeneration && ReferenceEquals(statisticsCancellation, cancellation)
            && model.ActiveDocument == tab && tab.EditorDocument == document
            && document.Version.CompareAge(snapshot.Version) == 0 && (tab.FilePath ?? tab.DisplayName) == identity;
    }
}
