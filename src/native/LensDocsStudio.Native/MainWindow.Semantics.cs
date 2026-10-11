using System.Windows;
using LensDocsStudio.Core.Editing;
using LensDocsStudio.Native.Editor;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private async void EditImageFigure(object sender, RoutedEventArgs e)
    {
        if (model.ActiveDocument is not { IsSaving: false } tab || Editor.IsReadOnly) return;
        if (tab.EditorDocument.TextLength > 4 * 1024 * 1024) { model.Status = "The image figure editor supports documents up to 4 million characters."; return; }
        if (Path.GetExtension(tab.FilePath)?.ToLowerInvariant() is ".mmd" or ".mermaid")
        { model.Status = "Add image figures to a Markdown document."; return; }
        var source = tab.EditorDocument.Text; var start = Editor.SelectionStart; var length = Editor.SelectionLength;
        await model.RunAsync(async () =>
        {
            var document = await Task.Run(() => new LensDocsStudio.Core.Parsing.MarkdownService().Parse(source), model.OperationToken);
            model.OperationToken.ThrowIfCancellationRequested();
            if (Changed() || Editor.SelectionStart != start || Editor.SelectionLength != length)
            { model.Status = "The document or selection changed; open the image figure editor again."; return; }
            if (ImageFigureEditService.Find(document, start, length) is null)
            { model.Status = "Insert an image first, then place the caret inside its standalone Markdown or existing figure to add a caption."; return; }
            var window = new ImageFigureWindow(document, start, length) { Owner = this };
            if (window.ShowDialog() != true || window.Edit is not { } edit) return;
            if (Changed()) { model.Status = "The document changed; the image figure was not applied. Open the editor again."; return; }
            EditorFormattingService.Apply(Editor, edit); model.Status = "Image figure updated.";
        });
        bool Changed() => model.ActiveDocument != tab || tab.EditorDocument.Text != source || tab.IsSaving || Editor.IsReadOnly;
    }
    private async void InsertAnchor(object sender, RoutedEventArgs e)
    {
        if (model.ActiveDocument is not { IsSaving: false } tab || Editor.IsReadOnly) return;
        var source = tab.EditorDocument.Text; var start = Editor.SelectionStart; var length = Editor.SelectionLength;
        if (source.Length > 4 * 1024 * 1024) { model.Status = "The anchor editor supports documents up to 4 million characters."; return; }
        if (Path.GetExtension(tab.FilePath)?.ToLowerInvariant() is ".mmd" or ".mermaid")
        { model.Status = "Insert document anchors in a Markdown document."; return; }
        await model.RunAsync(async () =>
        {
            var document = await Task.Run(() => new LensDocsStudio.Core.Parsing.MarkdownService().Parse(source), model.OperationToken);
            model.OperationToken.ThrowIfCancellationRequested();
            if (model.ActiveDocument != tab || tab.EditorDocument.Text != source || tab.IsSaving || Editor.SelectionStart != start || Editor.SelectionLength != length)
            { model.Status = "The document or selection changed; open the anchor editor again."; return; }
            var window = new AnchorWindow(document, start, length) { Owner = this };
            if (window.ShowDialog() != true || window.Edit is not { } edit) return;
            if (model.ActiveDocument != tab || tab.EditorDocument.Text != source || tab.IsSaving || Editor.IsReadOnly)
            { model.Status = "The document changed; the anchor was not applied. Open the editor again."; return; }
            EditorFormattingService.Apply(Editor, edit); model.Status = "Document anchor applied.";
        });
    }
    private async void InsertProgress(object sender, RoutedEventArgs e)
    {
        if (model.ActiveDocument is not { IsSaving: false } tab || Editor.IsReadOnly) return;
        var source = tab.EditorDocument.Text; var start = Editor.SelectionStart; var length = Editor.SelectionLength;
        if (source.Length > 4 * 1024 * 1024) { model.Status = "The progress editor supports documents up to 4 million characters."; return; }
        if (Path.GetExtension(tab.FilePath)?.ToLowerInvariant() is ".mmd" or ".mermaid")
        { model.Status = "Insert progress indicators in a Markdown document."; return; }
        await model.RunAsync(async () =>
        {
            var document = await Task.Run(() => new LensDocsStudio.Core.Parsing.MarkdownService().Parse(source), model.OperationToken);
            model.OperationToken.ThrowIfCancellationRequested();
            if (model.ActiveDocument != tab || tab.EditorDocument.Text != source || tab.IsSaving || Editor.SelectionStart != start || Editor.SelectionLength != length)
            { model.Status = "The document or selection changed; open the progress editor again."; return; }
            var existing = ProgressEditService.Find(document, start, length);
            var options = existing is null ? new ProgressOptions(length is > 0 and <= 512 ? source.Substring(start, length) : "Progress")
                : new(existing.Label, (int)Math.Round(existing.Percentage), existing.Colour);
            var window = new ProgressWindow(options, existing is not null) { Owner = this };
            if (window.ShowDialog() != true) return;
            if (model.ActiveDocument != tab || tab.EditorDocument.Text != source || tab.IsSaving || Editor.IsReadOnly)
            { model.Status = "The document changed; progress was not applied. Open the editor again."; return; }
            EditorFormattingService.Apply(Editor, ProgressEditService.Plan(document, start, length, window.Options));
            model.Status = existing is null ? "Progress indicator inserted." : "Progress indicator updated.";
        });
    }
}
