using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Native.ViewModels;

public partial class WorkspaceMapViewModel : ObservableObject
{
    public WorkspaceMapResult Map { get; }
    public string Summary { get; }
    public string ScanNotes { get; }
    public IReadOnlyList<string> Filters { get; } = ["All documents", "No incoming links", "Has review notes"];
    [ObservableProperty] private string query = "";
    [ObservableProperty] private string filter = "All documents";
    [ObservableProperty] private string status = "";
    [ObservableProperty] private IReadOnlyList<WorkspaceMapDocument> documents = [];
    [ObservableProperty] private IReadOnlyList<WorkspaceMapLink> incoming = [];
    [ObservableProperty] private IReadOnlyList<WorkspaceMapLink> outgoing = [];
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(OpenCommand)), NotifyCanExecuteChangedFor(nameof(SelectedDiagramCommand))]
    private WorkspaceMapDocument? selectedDocument;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(GoToSourceCommand))] private WorkspaceMapLink? selectedLink;
    public bool CanOpen => SelectedDocument is not null;
    public bool CanGoToSource => SelectedLink is not null;
    public bool CanDiagram => Map.Documents.Count > 0;
    public event Action<string, int, int>? OpenRequested;
    public event Action<string?>? DiagramRequested;
    public WorkspaceMapViewModel(WorkspaceMapResult map)
    {
        Map = map;
        Summary = $"Documents read: {map.Documents.Count} · Links: {map.Links.Count} · Review notes: {map.Links.Count(link => link.State != WorkspaceMapLinkState.Resolved)}";
        ScanNotes = (map.Complete ? "Workspace scan complete." : "Partial workspace scan.")
            + " Current open drafts are included. No source files are changed."
            + (map.Warnings.Count == 0 ? "" : "\n" + string.Join("\n", map.Warnings));
        Refresh();
    }
    partial void OnQueryChanged(string value) => Refresh();
    partial void OnFilterChanged(string value) => Refresh();
    private void Refresh()
    {
        var query = Query.Trim(); if (query.Length > 256) query = query[..256];
        var previous = SelectedDocument;
        Documents = Map.Documents.Where(document => (Filter != "No incoming links" || document.Incoming == 0)
            && (Filter != "Has review notes" || document.Issues > 0)
            && (query.Length == 0 || document.Path.Contains(query, StringComparison.CurrentCultureIgnoreCase) || document.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase))).ToArray();
        SelectedDocument = previous is not null && Documents.Contains(previous) ? previous : Documents.FirstOrDefault();
        if (SelectedDocument is null) OnSelectedDocumentChanged(null);
        Status = Documents.Count == 0 ? "No matching documents. Clear the search or change its filter." : $"{Documents.Count} of {Map.Documents.Count} documents";
    }
    partial void OnSelectedDocumentChanged(WorkspaceMapDocument? value)
    {
        SelectedLink = null;
        Incoming = value is null ? [] : Map.Links.Where(link => link.To?.Equals(value.Path, StringComparison.OrdinalIgnoreCase) == true && link.From != value.Path).ToArray();
        Outgoing = value is null ? [] : Map.Links.Where(link => link.From == value.Path).ToArray();
    }
    [RelayCommand(CanExecute = nameof(CanOpen))] private void Open() { if (SelectedDocument is { } selected) OpenRequested?.Invoke(selected.Path, 0, 0); }
    [RelayCommand(CanExecute = nameof(CanGoToSource))] private void GoToSource() { if (SelectedLink is { } link) OpenRequested?.Invoke(link.From, link.Start, link.Length); }
    [RelayCommand(CanExecute = nameof(CanDiagram))] private void WorkspaceDiagram() => DiagramRequested?.Invoke(null);
    [RelayCommand(CanExecute = nameof(CanOpen))] private void SelectedDiagram() => DiagramRequested?.Invoke(SelectedDocument!.Path);
    [RelayCommand] private void Clear() { Query = ""; Filter = "All documents"; }
}
