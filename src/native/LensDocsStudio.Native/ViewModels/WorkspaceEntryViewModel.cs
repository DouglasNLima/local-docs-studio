using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Native.ViewModels;

public partial class WorkspaceEntryViewModel(WorkspaceEntry entry) : ObservableObject
{
    [ObservableProperty] private string name = entry.Name;
    [ObservableProperty] private string path = entry.Path;
    public bool IsDirectory { get; } = entry.IsDirectory;
    public string IconKind => IsDirectory ? "Folder" : "Document";
    [ObservableProperty] private bool isExpanded = true;
    [ObservableProperty] private bool isVisible = true;
    [ObservableProperty] private string documentIndicator = "";
    public ObservableCollection<WorkspaceEntryViewModel> Children { get; } = new(entry.Children.Select(child => new WorkspaceEntryViewModel(child)));

    // Reuse surviving nodes so native folder expansion, selection and focus remain attached.
    internal static void Reconcile(ObservableCollection<WorkspaceEntryViewModel> current, IReadOnlyList<WorkspaceEntry> incoming)
    {
        var existing = current.ToDictionary(item => item.Path, StringComparer.OrdinalIgnoreCase);
        var retained = new HashSet<WorkspaceEntryViewModel>();
        var ordered = new List<WorkspaceEntryViewModel>(incoming.Count);
        foreach (var entry in incoming)
        {
            if (existing.TryGetValue(entry.Path, out var node) && node.IsDirectory == entry.IsDirectory)
            {
                node.Name = entry.Name; node.Path = entry.Path;
                Reconcile(node.Children, entry.Children);
                retained.Add(node);
            }
            else node = new(entry);
            ordered.Add(node);
        }
        for (var index = current.Count - 1; index >= 0; index--)
            if (!retained.Contains(current[index])) current.RemoveAt(index);
        for (var index = 0; index < ordered.Count; index++)
        {
            var node = ordered[index];
            if (index < current.Count && ReferenceEquals(current[index], node)) continue;
            var previous = retained.Contains(node) ? current.IndexOf(node) : -1;
            if (previous < 0) current.Insert(index, node);
            else current.Move(previous, index);
        }
    }
}
