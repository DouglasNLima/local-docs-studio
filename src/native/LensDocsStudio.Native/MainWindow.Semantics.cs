using System.Windows;
using LensDocsStudio.Core.Editing;
using LensDocsStudio.Native.Editor;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
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
