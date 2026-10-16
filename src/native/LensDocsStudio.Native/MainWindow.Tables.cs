using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Editing;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.Editor;
using System.Windows;
using Microsoft.Win32;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private async void EditTable(object sender, RoutedEventArgs e)
    {
        if (model.ActiveDocument is not { IsSaving: false } tab || Editor.IsReadOnly) return;
        if (Path.GetExtension(tab.EffectivePath)?.ToLowerInvariant() is ".mmd" or ".mermaid")
        { model.Status = "Insert and edit tables in a Markdown document."; return; }
        var source = tab.EditorDocument.Text; var start = Editor.SelectionStart; var length = Editor.SelectionLength;
        if (source.Length > TableEditService.MaximumSourceCharacters)
        { model.Status = "The table editor supports documents up to 4 million characters."; return; }
        await model.RunAsync(async () =>
        {
            var session = await Task.Run(() => TableEditService.Open(new MarkdownService().Parse(source), start, length), model.OperationToken);
            model.OperationToken.ThrowIfCancellationRequested();
            if (Changed() || Editor.SelectionStart != start || Editor.SelectionLength != length)
            { model.Status = "The document or selection changed; open the table editor again."; return; }
            var window = new TableEditorWindow(session, length > 0) { Owner = this };
            if (window.ShowDialog() != true || window.Edit is not { } edit)
            { Editor.Focus(); model.Status = "Table editing cancelled."; return; }
            if (Changed()) { model.Status = "The document changed; the table was not applied. Open its editor again."; return; }
            if (source.AsSpan(edit.Start, edit.Length).SequenceEqual(edit.Replacement))
            { Editor.Focus(); model.Status = "The table is unchanged."; return; }
            EditorFormattingService.Apply(Editor, edit); model.Status = session.IsExisting ? "Table updated." : "Table inserted.";
        });
        bool Changed() => model.ActiveDocument != tab || tab.EditorDocument.Text != source || tab.IsSaving || Editor.IsReadOnly;
    }

    private async Task ExportTableCsvAsync(ExportTableSelection selection, string source, Guid documentId)
    {
        if (model.ActiveDocument is not { } tab || tab.Id != documentId || tab.EditorDocument.Text != source)
        { model.Status = "The preview is out of date. Wait for the updated table and export again."; return; }
        await model.RunAsync(async () =>
        {
            model.Status = "Preparing table CSV…";
            var result = await Task.Run(() => new TableCsvExporter().Export(source, selection, model.OperationToken), model.OperationToken);
            model.OperationToken.ThrowIfCancellationRequested();
            if (model.ActiveDocument != tab || tab.EditorDocument.Text != source)
            { model.Status = "The document changed. Select the updated table and export again."; return; }
            var picker = new SaveFileDialog
            {
                Title = "Export table CSV", Filter = "CSV table|*.csv", DefaultExt = ".csv", AddExtension = true,
                FileName = Path.GetFileNameWithoutExtension(tab.FilePath ?? "document") + "-table-" + result.TableNumber + ".csv"
            };
            if (picker.ShowDialog(this) != true) { model.Status = "Table CSV export cancelled."; return; }
            if (!Path.GetExtension(picker.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            { dialogs.Error("Use the .csv extension for a table export."); return; }
            var expected = await ExportFileStore.ReadExistingAsync(picker.FileName, model.OperationToken);
            await WriteExportAsync(picker.FileName, result.Bytes, expected, model.OperationToken);
            await model.CompleteExportAsync(picker.FileName, result.Bytes, "Table exported as UTF-8 CSV");
        });
    }
}
