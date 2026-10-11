using LensDocsStudio.Core.Exports;
using Microsoft.Win32;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
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
            model.Status = "Table exported as UTF-8 CSV.";
        });
    }
}
