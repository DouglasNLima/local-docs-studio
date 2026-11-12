using System.Windows;
using ICSharpCode.AvalonEdit.Document;
using LensDocsStudio.Core.Editing;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Review;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native;

public partial class TableOfContentsWindow : Window
{
    private readonly ITextSource snapshot;
    private readonly bool standalone;
    private readonly Func<bool> isCurrent;
    // Closing and immediately reopening a dialog must not overlap uncancellable parser calls.
    private static readonly SemaphoreSlim generationGate = new(1, 1);
    private readonly SemaphoreSlim worker = generationGate;
    private IReadOnlyList<DocumentHeading>? headings;
    private CancellationTokenSource? cancellation;
    private bool closed;
    private bool invalid;

    public TableOfContentsWindow(ITextSource snapshot, bool standalone, Func<bool> isCurrent,
        IReadOnlyList<DocumentHeading>? headings = null)
    {
        this.snapshot = snapshot; this.standalone = standalone; this.isCurrent = isCurrent; this.headings = headings;
        InitializeComponent(); WindowStateService.FitInitialWindow(this);
        StartLevel.ItemsSource = EndLevel.ItemsSource = Enumerable.Range(1, 6).Select(level => "H" + level).ToArray();
        StartLevel.SelectedIndex = 0; EndLevel.SelectedIndex = 5;
        Loaded += async (_, _) => { StartLevel.Focus(); await RefreshAsync(); };
        Closed += (_, _) => { closed = true; cancellation?.Cancel(); };
    }

    public void InvalidateSource()
    {
        invalid = true; cancellation?.Cancel();
        PreviewText.Clear(); CopyButton.IsEnabled = false;
        ResultStatus.Text = "The document changed. Close this dialog and open Copy TOC again.";
    }

    private async void OptionsChanged(object sender, RoutedEventArgs e)
    { if (IsLoaded) await RefreshAsync(); }

    private async Task RefreshAsync()
    {
        if (closed || invalid) return;
        if (!isCurrent()) { InvalidateSource(); return; }
        cancellation?.Cancel();
        using var request = new CancellationTokenSource(); cancellation = request;
        var token = request.Token;
        PreviewText.Clear(); CopyButton.IsEnabled = false; ResultStatus.Text = "Generating table of contents…";
        var options = new TableOfContentsOptions(StartLevel.SelectedIndex + 1, EndLevel.SelectedIndex + 1,
            (TableOfContentsStyle)ListStyle.SelectedIndex, NormaliseLevels.IsChecked == true, DetailsWrapper.IsChecked == true);
        try
        {
            await worker.WaitAsync(token);
            (IReadOnlyList<DocumentHeading> Headings, string Text) result;
            try
            {
                var cached = headings;
                result = await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();
                    if (snapshot.TextLength > DocumentReviewService.MaximumSourceCharacters)
                        throw new IOException("Table of contents supports up to 4 million source characters.");
                    var parsedHeadings = cached ?? new MarkdownService().Parse(snapshot.Text, standalone).Headings;
                    token.ThrowIfCancellationRequested();
                    return (parsedHeadings, TableOfContentsService.Create(parsedHeadings, options, token));
                }, token);
            }
            finally { worker.Release(); }
            if (!Current()) return;
            headings = result.Headings; PreviewText.Text = result.Text;
            CopyButton.IsEnabled = result.Text.Length > 0;
            ResultStatus.Text = CopyButton.IsEnabled ? "Copying does not change your document."
                : headings.Count == 0 ? "This document has no headings to copy." : "No headings in the selected levels.";
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is not OutOfMemoryException)
        { if (Current()) ResultStatus.Text = error is ArgumentException or IOException ? error.Message : "The table of contents could not be generated."; }
        finally
        {
            if (ReferenceEquals(cancellation, request)) cancellation = null;
            if (!closed && !invalid && !isCurrent()) InvalidateSource();
        }
        bool Current() => !closed && !invalid && !token.IsCancellationRequested && isCurrent() && ReferenceEquals(cancellation, request);
    }

    private void Copy(object sender, RoutedEventArgs e)
    {
        if (closed || invalid || !isCurrent()) { InvalidateSource(); return; }
        if (!CopyButton.IsEnabled) return;
        try { Clipboard.SetText(PreviewText.Text); ResultStatus.Text = "Table of contents copied."; }
        catch (Exception error) when (error is System.Runtime.InteropServices.ExternalException or System.Threading.ThreadStateException)
        { ResultStatus.Text = "Clipboard access is unavailable. Try copying again."; }
    }
}
