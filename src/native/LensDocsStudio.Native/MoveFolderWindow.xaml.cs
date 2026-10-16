using System.Windows;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native;

public partial class MoveFolderWindow : Window
{
    public bool ShouldUpdateLinks => UpdateLinks.IsChecked == true;
    public MoveFolderWindow(WorkspacePath workspace, WorkspaceFolderMovePlan plan)
    {
        InitializeComponent(); WindowStateService.FitInitialWindow(this);
        MoveSummary.Text = Path.GetRelativePath(workspace.Root, plan.Source) + "\n→ " + Path.GetRelativePath(workspace.Root, plan.Destination)
            + $"\nFiles: {plan.Entries.Count(entry => !entry.IsDirectory)} · Folders: {plan.Entries.Count(entry => entry.IsDirectory)} · Document link edits: {plan.Documents.Sum(document => document.ChangedLinks)}";
        AffectedDocuments.ItemsSource = plan.Documents.Select(document => Path.GetRelativePath(workspace.Root, document.Original.Path)
            + (document.Destination == document.Original.Path ? "" : " → " + Path.GetRelativePath(workspace.Root, document.Destination))
            + $" · Link edits: {document.ChangedLinks}").ToArray();
        Warnings.Text = "The complete folder, including assets and empty folders, moves together. Existing destinations are never merged or replaced. "
            + "Changed contents stop the move; original document text is retained during the operation."
            + (plan.Warnings.Count == 0 ? "" : "\n\n" + string.Join("\n", plan.Warnings));
    }
    private void Apply(object sender, RoutedEventArgs e) => DialogResult = true;
}
