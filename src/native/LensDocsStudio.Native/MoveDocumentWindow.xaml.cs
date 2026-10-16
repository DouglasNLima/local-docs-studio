using System.Windows;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native;

public partial class MoveDocumentWindow : Window
{
    public bool ShouldUpdateLinks => UpdateLinks.IsChecked == true;
    public MoveDocumentWindow(WorkspacePath workspace, WorkspaceMovePlan plan)
    {
        InitializeComponent(); WindowStateService.FitInitialWindow(this);
        MoveSummary.Text = Path.GetRelativePath(workspace.Root, plan.Source.Path) + "\n→ " + Path.GetRelativePath(workspace.Root, plan.Destination);
        AffectedDocuments.ItemsSource = plan.OtherDocuments.Select(c => Path.GetRelativePath(workspace.Root, c.Original.Path) + " — " + c.ChangedLinks + " link changes")
            .Prepend(plan.UpdatedSource != plan.Source.Text ? "Moved document — rebase outgoing links and images" : "Moved document — source text unchanged").ToArray();
        Warnings.Text = plan.Warnings.Count == 0 ? "Existing destination files are never replaced. Original text is retained during the operation." : string.Join("\n", plan.Warnings.Take(4));
    }
    private void Apply(object sender, RoutedEventArgs e) => DialogResult = true;
}
