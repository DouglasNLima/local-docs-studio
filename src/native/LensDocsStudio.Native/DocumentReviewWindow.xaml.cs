using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LensDocsStudio.Core.Review;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native;

public partial class DocumentReviewWindow : Window
{
    public DocumentReviewIssue? SelectedIssue { get; private set; }
    public DocumentReviewWindow(DocumentReviewResult result)
    {
        InitializeComponent(); WindowStateService.FitInitialWindow(this);
        var metrics = result.Metrics;
        Summary.Text = $"{Count(metrics.Words, "word")} · {metrics.ReadingMinutes} min read · {Count(metrics.Headings, "heading")} · {Count(metrics.Links, "link")} · {Count(metrics.Images, "image")} · {Count(metrics.Tables, "table")} · {Count(metrics.CodeBlocks, "code block")} · {Count(metrics.Diagrams, "diagram")}";
        ResultStatus.Text = result.Limited ? $"Showing {result.Issues.Count} notes; the review limit was reached."
            : result.Issues.Count == 0 ? "No notes from these checks." : $"{Count(result.Issues.Count, "advisory note")}. Choose one to inspect its source.";
        WorkspaceScope.Text = result.WorkspaceLinks switch
        {
            WorkspaceLinkReviewState.Complete => "Workspace document links checked against saved files and current open drafts.",
            WorkspaceLinkReviewState.Incomplete => "Some workspace document links could not be checked. Review the notes for details.",
            _ => "Save this document in a workspace to include workspace document links."
        };
        Issues.ItemsSource = result.Issues;
        Loaded += (_, _) => { if (Issues.Items.Count > 0) { Issues.SelectedIndex = 0; Issues.Focus(); } else CloseButton.Focus(); };
    }
    private static string Count(int count, string label) => count + " " + label + (count == 1 ? "" : "s");
    private void SelectionChanged(object sender, SelectionChangedEventArgs e)
    { if (GoToButton is not null) GoToButton.IsEnabled = Issues.SelectedItem is DocumentReviewIssue; }
    private void GoToSelected(object sender, RoutedEventArgs e)
    {
        if (Issues.SelectedItem is not DocumentReviewIssue issue) return;
        SelectedIssue = issue; DialogResult = true;
    }
    private void ResultKeyDown(object sender, KeyEventArgs e)
    { if (e.Key == Key.Enter) { GoToSelected(sender, e); e.Handled = true; } }
}
