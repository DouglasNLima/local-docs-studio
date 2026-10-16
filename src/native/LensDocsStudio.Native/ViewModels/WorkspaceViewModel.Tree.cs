using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LensDocsStudio.Native.ViewModels;

public partial class WorkspaceViewModel
{
    private bool CanCloseWorkspace => WorkspaceRoot is not null && !IsBusy;
    [RelayCommand(CanExecute = nameof(CanCloseWorkspace))]
    private Task CloseWorkspaceAsync() => RunAsync(async () =>
    {
        StopMonitoring();
        WorkspaceRoot = null; Entries.Clear(); WorkspaceFilter = "";
        ApplyWorkspaceFilter();
        // Document roots and asset contexts belong to tabs, independently of the visible workspace.
        await PersistSessionAsync();
        Status = "Workspace closed. Open documents and unsaved changes are retained.";
    });

    [ObservableProperty] private string workspaceFilter = "";
    [ObservableProperty] private string workspaceEmptyMessage = "Open a folder to browse local Markdown, Mermaid and text files.";
    [RelayCommand] private void ClearWorkspaceFilter() => WorkspaceFilter = "";
    partial void OnWorkspaceFilterChanged(string value) => ApplyWorkspaceFilter();
    public void ApplyWorkspaceFilter()
    {
        var terms = WorkspaceFilter.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        bool Visit(WorkspaceEntryViewModel entry)
        {
            var descendants = false;
            foreach (var child in entry.Children) descendants |= Visit(child);
            var relative = WorkspaceRoot is null ? entry.Name : Path.GetRelativePath(WorkspaceRoot, entry.Path);
            entry.IsVisible = descendants || terms.All(term => relative.Contains(term, StringComparison.CurrentCultureIgnoreCase));
            return entry.IsVisible;
        }
        foreach (var entry in Entries) Visit(entry);
        WorkspaceEmptyMessage = WorkspaceRoot is null ? "Open a folder to browse local Markdown, Mermaid and text files."
            : Entries.Any(entry => entry.IsVisible) ? "" : terms.Length > 0 ? "No files match this filter." : "This workspace has no supported documents yet. Create a Markdown or Mermaid file to begin.";
        RefreshTreeIndicators();
    }
    public void RefreshTreeIndicators()
    {
        var open = Documents.Where(document => document.FilePath is not null)
            .GroupBy(document => document.FilePath!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        void Visit(WorkspaceEntryViewModel entry)
        {
            entry.DocumentIndicator = open.TryGetValue(entry.Path, out var document)
                ? document.ExternalChange ? "Changed externally" : document.IsDirty ? "Unsaved changes" : "" : "";
            foreach (var child in entry.Children) Visit(child);
        }
        foreach (var entry in Entries) Visit(entry);
    }
    [RelayCommand] private Task RefreshWorkspaceAsync()
    {
        var root = WorkspaceRoot;
        return root is null ? Task.CompletedTask : RunAsync(() => SetWorkspaceAsync(root));
    }
    [RelayCommand] private void ExpandWorkspace() => SetExpansion(true);
    [RelayCommand] private void CollapseWorkspace() => SetExpansion(false);
    private void SetExpansion(bool expanded)
    {
        void Visit(WorkspaceEntryViewModel entry) { entry.IsExpanded = expanded; foreach (var child in entry.Children) Visit(child); }
        foreach (var entry in Entries) Visit(entry);
    }
}
