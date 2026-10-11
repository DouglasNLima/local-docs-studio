using System.Windows;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Core.Workspace;
using Microsoft.Win32;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private async void ImportDocument(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Title = "Import document", Filter = "Supported documents|*.docx;*.html;*.htm;*.pdf" };
        if (picker.ShowDialog(this) != true || dialogs.OpenFolder() is not { } parent) return;
        await model.RunAsync(async () =>
        {
            var imported = await new DocumentImportService().ImportAsync(picker.FileName, model.OperationToken);
            var folder = Path.Combine(parent, Path.GetFileNameWithoutExtension(picker.FileName) + "-import-" + Guid.NewGuid().ToString("N")[..8]);
            WorkspacePath.RejectReparsePoints(folder);
            var staging = Path.Combine(parent, ".lens-import-" + Guid.NewGuid().ToString("N"));
            WorkspacePath.RejectReparsePoints(staging);
            Directory.CreateDirectory(staging);
            try
            {
                var stagedDocument = Path.Combine(staging, "document.md");
                var text = imported.Markdown;
                foreach (var asset in imported.Assets)
                {
                    var reference = await assets.ImportAsync(staging, stagedDocument, asset.Bytes, asset.SuggestedName, model.OperationToken);
                    text = text.Replace(asset.Reference, reference, StringComparison.Ordinal);
                }
                await new DocumentStore(Path.Combine(model.StateDirectory, "write-recovery")).SaveAsync(stagedDocument, text, null, model.OperationToken);
                model.OperationToken.ThrowIfCancellationRequested();
                Directory.Move(staging, folder);
            }
            finally
            {
                // Only this operation's freshly created staging folder is eligible for cleanup.
                if (Directory.Exists(staging)) { WorkspacePath.RejectReparsePoints(staging); Directory.Delete(staging, true); }
            }
            await model.SetWorkspaceAsync(folder);
            await model.OpenPathAsync(Path.Combine(folder, "document.md"));
            model.Status = imported.Warnings.Count == 0 ? "Imported into a new portable workspace" : imported.Warnings[0];
        });
    }
}
