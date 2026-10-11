using System.Windows;
using LensDocsStudio.Core.Review;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private async void ReviewDocument(object sender, RoutedEventArgs e)
    {
        if (model.ActiveDocument is not { } tab) return;
        var source = tab.EditorDocument.Text;
        var standalone = Path.GetExtension(tab.FilePath)?.ToLowerInvariant() is ".mmd" or ".mermaid";
        await model.RunAsync(async () =>
        {
            model.Status = "Reviewing current document…";
            var result = await Task.Run(() => new DocumentReviewService().Analyse(source, standalone, model.OperationToken), model.OperationToken);
            model.OperationToken.ThrowIfCancellationRequested();
            if (model.ActiveDocument != tab || tab.EditorDocument.Text != source)
            { model.Status = "The document changed. Run review again for its current text."; return; }
            var window = new DocumentReviewWindow(result) { Owner = this };
            if (window.ShowDialog() != true || window.SelectedIssue is not { } issue)
            { model.Status = "Document review closed."; return; }
            if (model.ActiveDocument != tab || tab.EditorDocument.Text != source)
            { model.Status = "The document changed. Run review again before navigating."; return; }
            CaptureCurrentNavigation();
            if (model.Layout == "Preview") SetLayout("Split");
            Editor.Select(issue.Start, issue.Length);
            Editor.ScrollToLine(Editor.Document.GetLineByOffset(issue.Start).LineNumber); Editor.Focus();
            VisitCurrentNavigation();
            model.Status = issue.Message;
        });
    }
}
