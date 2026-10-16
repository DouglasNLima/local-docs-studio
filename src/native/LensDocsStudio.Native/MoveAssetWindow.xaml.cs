using System.Windows;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native;

public partial class MoveAssetWindow : Window
{
    public MoveAssetWindow(WorkspacePath workspace, WorkspaceAssetMovePlan plan)
    {
        InitializeComponent(); WindowStateService.FitInitialWindow(this);
        MoveSummary.Text = Path.GetRelativePath(workspace.Root, plan.Source.Path) + "\n→ " + Path.GetRelativePath(workspace.Root, plan.Destination);
        AffectedDocuments.ItemsSource = plan.Documents.Select(c => Path.GetRelativePath(workspace.Root, c.Original.Path) + " — " + c.ChangedLinks + " reference changes").ToArray();
    }
    private void Apply(object sender, RoutedEventArgs e) => DialogResult = true;
}
