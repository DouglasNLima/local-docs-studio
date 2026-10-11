using System.Windows;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Native.Exports;
using LensDocsStudio.Native.Platform;
using Microsoft.Win32;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private readonly DocumentExportService exports = new();
    private byte[]? wordTemplate;

    private async void ExportDocument(object sender, RoutedEventArgs e)
    {
        if (model.ActiveDocument is not { } tab) return;
        var format = (string)((FrameworkElement)sender).Tag;
        var extension = format.ToLowerInvariant();
        var picker = new SaveFileDialog { Title = "Export " + format, Filter = format + " document|*." + extension,
            FileName = Path.GetFileNameWithoutExtension(tab.FilePath ?? "document") + "." + extension,
            AddExtension = true, DefaultExt = "." + extension };
        if (picker.ShowDialog(this) != true) return;
        if (!Path.GetExtension(picker.FileName).Equals("." + extension, StringComparison.OrdinalIgnoreCase))
        { dialogs.Error("Use the ." + extension + " extension for this export."); return; }
        var source = tab.EditorDocument.Text;
        var sourcePath = tab.EffectivePath;
        var workspaceRoot = tab.EffectiveWorkspace;

        await model.RunAsync(async () =>
        {
            var expected = await ExportFileStore.ReadExistingAsync(picker.FileName, model.OperationToken);
            model.Status = "Preparing " + format + " export…";
            var bytes = await exports.ExportAsync(format, source, sourcePath, workspaceRoot, ThemeService.IsDark(model.Theme), wordTemplate, model.OperationToken);
            await WriteExportAsync(picker.FileName, bytes, expected, model.OperationToken);
            model.Status = "Exported " + Path.GetFileName(picker.FileName);
        });
    }

    private Task WriteExportAsync(string path, byte[] bytes, byte[]? expected, CancellationToken cancellationToken) =>
        new ExportFileStore(Path.Combine(model.StateDirectory, "export-recovery")).WriteAsync(path, bytes, expected, cancellationToken);

    private async void SelectWordTemplate(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Title = "Select Word template", Filter = "Word documents|*.docx" };
        if (picker.ShowDialog(this) != true) return;
        await model.RunAsync(async () =>
        {
            if (new FileInfo(picker.FileName).Length > 32 * 1024 * 1024) throw new IOException("The template exceeds the 32 MB import limit.");
            wordTemplate = await File.ReadAllBytesAsync(picker.FileName, model.OperationToken);
            model.Status = "Word template selected: " + Path.GetFileName(picker.FileName);
        });
    }
    private void ClearWordTemplate(object sender, RoutedEventArgs e) { wordTemplate = null; model.Status = "Default Word styles selected"; }
}
