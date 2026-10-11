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
    private Guid? wordTemplateId;
    private string wordTemplateName = "Default Word styles";

    private async void ExportDocsSite(object sender, RoutedEventArgs e)
    {
        if (model.WorkspaceRoot is not { } root) { dialogs.Error("Open a workspace folder before exporting a Docs Site."); return; }
        var options = new DocsSiteWindow(Path.GetFileName(root)) { Owner = this };
        if (options.ShowDialog() != true) return;
        var picker = new SaveFileDialog { Title = "Save Docs Site", Filter = "Docs Site ZIP|*.zip", FileName = "documentation-site.zip", AddExtension = true, DefaultExt = ".zip" };
        if (picker.ShowDialog(this) != true) return;
        if (!Path.GetExtension(picker.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase)) { dialogs.Error("Use the .zip extension for a Docs Site."); return; }
        var edited = model.Documents.Where(d => d.FilePath is not null).ToDictionary(d => d.FilePath!, d => d.EditorDocument.Text, StringComparer.OrdinalIgnoreCase);
        await model.RunAsync(async () =>
        {
            var expected = await ExportFileStore.ReadExistingAsync(picker.FileName, model.OperationToken);
            var result = await new DocsSiteExportService().ExportAsync(root, edited, model.StateDirectory, options.Options,
                new Progress<string>(message => model.Status = message), model.OperationToken);
            await WriteExportAsync(picker.FileName, result.Bytes, expected, model.OperationToken);
            model.Status = $"Docs Site exported: {result.SourcePages} documents, {result.Diagrams} diagrams."
                + (result.Warnings.Count > 0 ? $" {result.Warnings.Count} links have targets outside this site." : "");
        });
    }

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
            var prepared = await new DocxTemplateService().ReadAsync(picker.FileName, model.OperationToken);
            wordTemplate = prepared.Bytes; wordTemplateId = null; wordTemplateName = Path.GetFileName(picker.FileName);
            model.Status = "Word template selected: " + Path.GetFileName(picker.FileName)
                + (prepared.Warnings.Count == 0 ? "" : ". " + string.Join(" ", prepared.Warnings));
        });
    }
    private void ClearWordTemplate(object sender, RoutedEventArgs e) { wordTemplate = null; wordTemplateId = null; wordTemplateName = "Default Word styles"; model.Status = "Default Word styles selected"; }

    private async void ManageWordTemplates(object sender, RoutedEventArgs e)
    {
        await model.RunAsync(async () =>
        {
            var library = new WordTemplateLibrary(Path.Combine(model.StateDirectory, "word-templates"));
            var listing = await Task.Run(() => library.ListAsync(cancellationToken: model.OperationToken), model.OperationToken);
            var window = new WordTemplatesWindow(library, listing, Path.Combine(model.StateDirectory, "export-recovery"), wordTemplateId, wordTemplateName) { Owner = this };
            var selected = window.ShowDialog(); wordTemplateName = window.CurrentName;
            if (selected != true) return;
            wordTemplate = window.SelectedBytes; wordTemplateId = window.SelectedId; wordTemplateName = window.SelectedName;
            model.Status = wordTemplate is null ? "Default Word styles selected" : "Word template selected: " + wordTemplateName;
        });
    }
}
