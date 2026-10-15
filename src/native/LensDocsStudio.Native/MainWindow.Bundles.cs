using System.Windows;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Core.Workspace;
using Microsoft.Win32;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private readonly MarkdownBundleService bundles = new();
    private async void ImportBundle(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Title = "Import Markdown bundle", Filter = "ZIP bundles|*.zip" };
        if (picker.ShowDialog(this) != true || dialogs.OpenFolder() is not { } parent) return;
        var destination = Path.Combine(parent, Path.GetFileNameWithoutExtension(picker.FileName) + "-import-" + Guid.NewGuid().ToString("N")[..8]);
        await model.RunAsync(async () =>
        {
            var result = await bundles.ImportAsync(picker.FileName, destination, model.OperationToken);
            await model.SetWorkspaceAsync(result.Root);
            SetArtifactContext(result.ArtifactBundle is { } bundle ? new(result.Root, bundle) : null);
            var first = result.ArtifactBundle is { EntryDocument.Length: > 0 } metadata ? new WorkspacePath(result.Root).Resolve(metadata.EntryDocument) : Directory.EnumerateFiles(result.Root, "*", SearchOption.AllDirectories)
                .Where(WorkspaceService.IsDocument).OrderByDescending(p => Path.GetFileName(p).Equals("README.md", StringComparison.OrdinalIgnoreCase)).First();
            await model.OpenPathAsync(first);
            model.Status = $"Imported {result.Documents} documents and {result.Assets} assets into a new folder."
                + (result.ArtifactBundle is not null ? " View → Artefact bundle shows supplied metadata." : "")
                + (result.Warnings.Count > 0 ? " Import notes: " + string.Join(" ", result.Warnings) : "");
        });
    }
    private async void ExportBundle(object sender, RoutedEventArgs e)
    {
        if (model.WorkspaceRoot is not { } root) { dialogs.Error("Open a workspace folder before exporting a Markdown bundle."); return; }
        model.DismissExportCommand.Execute(null);
        var picker = new SaveFileDialog { Title = "Export Markdown bundle", Filter = "ZIP bundle|*.zip", FileName = "documentation.zip", AddExtension = true };
        if (picker.ShowDialog(this) != true) { model.Status = "Markdown bundle export cancelled."; return; }
        if (!Path.GetExtension(picker.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase)) { dialogs.Error("Use the .zip extension for a Markdown bundle."); return; }
        var edited = model.Documents.Where(d => d.FilePath is not null).ToDictionary(d => d.FilePath!, d => d.EditorDocument.Text, StringComparer.OrdinalIgnoreCase);
        await model.RunAsync(async () =>
        {
            var expected = await ExportFileStore.ReadExistingAsync(picker.FileName, model.OperationToken);
            model.Status = "Preparing Markdown bundle…";
            var bytes = await bundles.ExportAsync(new WorkspacePath(root), edited, model.OperationToken, model.StateDirectory, activeExportProfile?.DevopsMarkdownExport == true);
            await WriteExportAsync(picker.FileName, bytes, expected, model.OperationToken);
            await model.CompleteExportAsync(picker.FileName, bytes, "Current editor text and portable assets included"
                + (activeExportProfile?.DevopsMarkdownExport == true ? "; Azure DevOps Mermaid formatting applied." : "."));
        });
    }
}
