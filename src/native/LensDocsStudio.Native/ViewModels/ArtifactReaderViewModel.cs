using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LensDocsStudio.Core.Imports;

namespace LensDocsStudio.Native.ViewModels;

public sealed record ArtifactReaderItem(ArtifactEntry Entry, string Title, string Path, string Group, string EvidenceLevel);

public partial class ArtifactReaderViewModel : ObservableObject
{
    private readonly ArtifactReaderItem[] items;
    [ObservableProperty] private string query = "";
    [ObservableProperty] private string group = "All groups";
    [ObservableProperty] private string evidence = "All evidence labels";
    [ObservableProperty] private string status = "";
    [ObservableProperty] private ICollectionView? entries;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(OpenCommand))] private ArtifactReaderItem? selectedItem;
    public string Title { get; }
    public string Summary { get; }
    public string Provenance { get; }
    public string Notes { get; }
    public string Warnings { get; }
    public IReadOnlyList<string> Groups { get; } = new[] { "All groups" }.Concat(ArtifactBundleManifest.Groups).ToArray();
    public IReadOnlyList<string> EvidenceLevels { get; }
    public bool CanOpen => SelectedItem is not null;
    public event Action<ArtifactEntry>? OpenRequested;

    public ArtifactReaderViewModel(ArtifactBundle bundle)
    {
        Title = bundle.Title.Length > 0 ? bundle.Title : bundle.SourceTool.Length > 0 ? bundle.SourceTool : "Artefact bundle";
        Summary = bundle.Summary;
        Provenance = string.Join(" · ", new[] { string.Join(' ', new[] { bundle.SourceTool, bundle.SourceToolVersion }.Where(value => value.Length > 0)),
            bundle.GeneratedAtUtc.Length > 0 ? "Generated UTC: " + bundle.GeneratedAtUtc : "", bundle.EntryDocument.Length > 0 ? "Entry: " + bundle.EntryDocument : "" }.Where(value => value.Length > 0));
        Notes = "Evidence labels are displayed as supplied by the bundle."
            + (bundle.HasCandidateFindings ? " Candidate findings remain candidate findings." : "");
        Warnings = string.Join("\n", bundle.MetadataWarnings.Concat(bundle.Warnings.Select(warning => string.Join(" · ", new[] { warning.Code, warning.Message, warning.EvidenceLevel }.Where(value => value.Length > 0)))));
        var groupOrder = ArtifactBundleManifest.Groups.Select((value, index) => (value, index)).ToDictionary(item => item.value, item => item.index);
        items = bundle.Entries.Select(entry => new ArtifactReaderItem(entry, entry.DisplayTitle, entry.Path, entry.Group, entry.EvidenceLevel))
            .OrderBy(item => groupOrder[item.Group]).ThenBy(item => item.Entry.Order ?? double.PositiveInfinity)
            .ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase).ThenBy(item => item.Path, StringComparer.Ordinal).ToArray();
        EvidenceLevels = new[] { "All evidence labels" }.Concat(bundle.EvidenceLevels).ToArray();
        Refresh();
    }
    partial void OnQueryChanged(string value) => Refresh();
    partial void OnGroupChanged(string value) => Refresh();
    partial void OnEvidenceChanged(string value) => Refresh();
    private void Refresh()
    {
        var query = Query.Trim(); if (query.Length > 256) query = query[..256];
        var selected = SelectedItem;
        var filtered = items.Where(item => (Group == "All groups" || item.Group == Group)
            && (Evidence == "All evidence labels" || item.EvidenceLevel == Evidence)
            && (query.Length == 0 || string.Join(' ', item.Title, item.Path, item.Group, item.Entry.Kind, item.EvidenceLevel).Contains(query, StringComparison.CurrentCultureIgnoreCase))).ToArray();
        var view = new ListCollectionView(filtered); view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ArtifactReaderItem.Group))); Entries = view;
        SelectedItem = selected is not null && filtered.Contains(selected) ? selected : filtered.FirstOrDefault();
        Status = filtered.Length == 0 ? "No matching items. Clear filters to view the imported bundle." : $"{filtered.Length} of {items.Length} imported items · Metadata is available for this import session.";
    }
    [RelayCommand(CanExecute = nameof(CanOpen))] private void Open() { if (SelectedItem is { } selected) OpenRequested?.Invoke(selected.Entry); }
    [RelayCommand] private void Reset() { Query = ""; Group = "All groups"; Evidence = "All evidence labels"; }
}
