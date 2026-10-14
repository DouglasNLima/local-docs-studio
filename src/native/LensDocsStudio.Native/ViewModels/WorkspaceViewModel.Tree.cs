using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LensDocsStudio.Native.ViewModels;

public partial class WorkspaceViewModel
{
    [ObservableProperty] private string workspaceFilter = "";
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
