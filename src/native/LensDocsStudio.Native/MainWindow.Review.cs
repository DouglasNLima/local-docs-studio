using System.Windows;
using LensDocsStudio.Core.Review;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Editor;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private async void ReviewDocument(object sender, RoutedEventArgs e)
    {
        if (model.ActiveDocument is not { } tab) return;
        if (tab.EditorDocument.TextLength > DocumentReviewService.MaximumSourceCharacters)
        { model.Status = "Document review supports up to 4 million source characters."; return; }
        var source = tab.EditorDocument.Text;
        var standalone = Path.GetExtension(tab.FilePath)?.ToLowerInvariant() is ".mmd" or ".mermaid";
        var sourcePath = tab.FilePath; var workspaceRoot = tab.WorkspaceRoot;
        var captured = model.Documents.Where(document => document.FilePath is not null && workspaceRoot is not null && WorkspacePath.Contains(workspaceRoot, document.FilePath))
            .Select(document => (Document: document, Path: document.FilePath!, Text: document.EditorDocument.Text)).ToArray();
        var edited = captured.ToDictionary(item => item.Path, item => item.Text, StringComparer.OrdinalIgnoreCase);
        await model.RunAsync(async () =>
        {
            model.Status = "Reviewing current document…";
            var result = sourcePath is not null && workspaceRoot is not null
                ? await Task.Run(() => new WorkspaceDocumentReviewService(new DocumentStore(Path.Combine(model.StateDirectory, "review-recovery")), model.StateDirectory)
                    .AnalysePathAsync(source, sourcePath, workspaceRoot, edited, model.OperationToken), model.OperationToken)
                : await Task.Run(() => new DocumentReviewService().Analyse(source, standalone, model.OperationToken), model.OperationToken);
            model.OperationToken.ThrowIfCancellationRequested();
            if (Changed())
            { model.Status = "The document changed. Run review again for its current text."; return; }
            var window = new DocumentReviewWindow(result) { Owner = this };
            if (window.ShowDialog() != true || window.SelectedIssue is not { } issue)
            { model.Status = "Document review closed."; return; }
            if (Changed())
            { model.Status = "The document changed. Run review again before navigating."; return; }
            CaptureCurrentNavigation();
            if (model.Layout == "Preview") SetLayout("Split");
            Editor.Select(issue.Start, issue.Length);
            if (!await EditorNavigationService.BringIntoViewAsync(Editor, issue.Start)) return;
            Editor.Focus();
            VisitCurrentNavigation();
            model.Status = issue.Message;
        });
        bool Changed() => model.ActiveDocument != tab || tab.FilePath != sourcePath || tab.WorkspaceRoot != workspaceRoot || tab.EditorDocument.Text != source
            || captured.Any(item => !model.Documents.Contains(item.Document) || item.Document.FilePath != item.Path || item.Document.EditorDocument.Text != item.Text);
    }
}
