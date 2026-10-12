using System.ComponentModel;
using System.Windows;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.ViewModels;
using Microsoft.Win32;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private sealed record ArtifactContext(string Root, ArtifactBundle Bundle);
    private ArtifactContext? artifactContext;
    private ArtifactBundle? ArtifactFor(string root) => artifactContext is { } context && string.Equals(context.Root, root, StringComparison.OrdinalIgnoreCase) ? context.Bundle : null;
    private void InitialiseArtifactContext()
    {
        PropertyChangedEventHandler changed = (_, args) =>
        {
            if (args.PropertyName == nameof(WorkspaceViewModel.WorkspaceRoot) && artifactContext?.Root != model.WorkspaceRoot) SetArtifactContext(null);
        };
        model.PropertyChanged += changed; Closed += (_, _) => model.PropertyChanged -= changed;
    }
    private void SetArtifactContext(ArtifactContext? context)
    {
        artifactContext = context;
        ArtifactReaderMenu.IsEnabled = ArtifactReviewPackMenu.IsEnabled = context is not null;
    }
    private async void OpenArtifactReader(object sender, RoutedEventArgs e)
    {
        if (artifactContext is not { } context || ArtifactFor(model.WorkspaceRoot ?? "") is null) return;
        var reader = new ArtifactReaderWindow(new(context.Bundle)) { Owner = this };
        if (reader.ShowDialog() != true || reader.SelectedEntry is not { } selected) return;
        await model.RunAsync(async () =>
        {
            if (artifactContext != context || model.WorkspaceRoot != context.Root || context.Bundle.Find(selected.Path) is null)
                throw new IOException("Import this bundle again to use its metadata in the current workspace.");
            var path = new WorkspacePath(context.Root).Resolve(selected.Path);
            if (!File.Exists(path) || !ArtifactBundleManifest.IsArtifactDocument(path)) throw new IOException("The selected imported document is no longer available.");
            await model.OpenPathAsync(path);
        });
    }
    private async void ExportArtifactReviewPack(object sender, RoutedEventArgs e)
    {
        if (model.WorkspaceRoot is not { } root || ArtifactFor(root) is not { } bundle) return;
        var picker = new SaveFileDialog { Title = "Export artefact review pack", Filter = "Artefact review ZIP|*.zip", FileName = "artefact-review-pack.zip", AddExtension = true, DefaultExt = ".zip" };
        if (picker.ShowDialog(this) != true) return;
        var edited = model.Documents.Where(document => document.FilePath is not null).ToDictionary(document => document.FilePath!, document => document.EditorDocument.Text, StringComparer.OrdinalIgnoreCase);
        await model.RunAsync(async () =>
        {
            var expected = await ExportFileStore.ReadExistingAsync(picker.FileName, model.OperationToken);
            model.Status = "Preparing artefact review pack…";
            var bytes = await bundles.ExportReviewPackAsync(new(root), bundle, edited, model.OperationToken, model.StateDirectory);
            await WriteExportAsync(picker.FileName, bytes, expected, model.OperationToken);
            model.Status = "Artefact review pack exported with current source and supplied evidence labels. Candidate findings remain candidate findings.";
        });
    }
}
