using System.Windows;
using System.Windows.Controls;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native;

public sealed record WorkspaceAssetAction(string Action, WorkspaceAsset? Asset);

public partial class WorkspaceAssetsWindow : Window
{
    private readonly WorkspacePath workspace;
    private readonly WorkspaceAssetAudit audit;
    private int selectionRevision;
    public WorkspaceAssetAction? SelectedAction { get; private set; }

    public WorkspaceAssetsWindow(WorkspacePath workspace, WorkspaceAssetAudit audit)
    {
        this.workspace = workspace; this.audit = audit;
        InitializeComponent(); WindowStateService.FitInitialWindow(this);
        AssetList.ItemsSource = audit.Assets;
        AuditStatus.Text = audit.Assets.Count + " images. " + (audit.UsageComplete ? "Documentation references checked." : "Usage checking is incomplete; removal is disabled.") +
            (audit.Warnings.Count == 0 ? "" : "\n" + string.Join("\n", audit.Warnings));
        RenameButton.IsEnabled = RemoveButton.IsEnabled = false;
        if (audit.Assets.Count != 0) AssetList.SelectedIndex = 0;
    }

    private void FilterChanged(object sender, TextChangedEventArgs e)
    {
        if (AssetList is null) return;
        AssetList.ItemsSource = audit.Assets.Where(a => a.RelativePath.Contains(Filter.Text, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (AssetList.Items.Count != 0) AssetList.SelectedIndex = 0;
    }
    private async void AssetSelected(object sender, SelectionChangedEventArgs e)
    {
        var revision = ++selectionRevision;
        AssetPreview.Source = null; AssetDetails.Text = ""; UsageList.ItemsSource = null;
        RenameButton.IsEnabled = RemoveButton.IsEnabled = false;
        if (AssetList.SelectedItem is not WorkspaceAsset asset) return;
        RenameButton.IsEnabled = asset.Exists && WorkspaceService.IsRasterImage(asset.Path);
        RemoveButton.IsEnabled = RenameButton.IsEnabled && audit.UsageComplete && asset.Uses.Count == 0;
        UsageList.ItemsSource = asset.Uses.Select(u => Path.GetRelativePath(workspace.Root, u.DocumentPath) + (u.Unsaved ? " (unsaved changes)" : "") + (u.Markdown ? "" : " (embedded HTML)")).ToArray();
        AssetDetails.Text = asset.RelativePath + (asset.Exists ? "\n" + asset.Bytes.ToString("N0") + " bytes · " + asset.Uses.Count + " references" : "\nMissing image; its references are listed below.");
        if (!asset.Exists) return;
        try
        {
            var image = await Task.Run(() =>
            {
                workspace.Resolve(asset.RelativePath);
                using var file = new FileStream(asset.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                return NativeRasterService.Read(file);
            });
            if (revision == selectionRevision) AssetPreview.Source = image;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { if (revision == selectionRevision) AssetDetails.Text += "\nPreview unavailable: " + ex.Message; }
    }
    private void Rename(object sender, RoutedEventArgs e) => Select("rename");
    private void Remove(object sender, RoutedEventArgs e) => Select("remove");
    private void Refresh(object sender, RoutedEventArgs e) { SelectedAction = new("refresh", null); DialogResult = true; }
    private void Select(string action)
    {
        if (AssetList.SelectedItem is not WorkspaceAsset asset) return;
        if (action == "remove" && !RemoveButton.IsEnabled || action == "rename" && !RenameButton.IsEnabled) return;
        SelectedAction = new(action, asset); DialogResult = true;
    }
}
