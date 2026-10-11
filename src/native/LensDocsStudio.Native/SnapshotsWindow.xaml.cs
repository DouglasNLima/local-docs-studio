using System.Windows;
using System.Windows.Controls;
using LensDocsStudio.Core.State;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native;

public partial class SnapshotsWindow : Window
{
    private readonly DocumentSnapshotStore snapshots;
    private readonly string listingWarnings;
    private int selectionRevision;
    public DocumentSnapshotEntry? SelectedSnapshot => SnapshotList.SelectedItem as DocumentSnapshotEntry;
    public SnapshotsWindow(DocumentSnapshotStore snapshots, DocumentSnapshotListing listing)
    {
        this.snapshots = snapshots;
        listingWarnings = string.Join("\n", listing.Warnings.Take(3));
        InitializeComponent(); WindowStateService.FitInitialWindow(this);
        SnapshotList.ItemsSource = listing.Entries;
        SnapshotStatus.Text = listing.Entries.Count == 0 ? "No snapshots have been retained yet. Use File → Take snapshot to retain the current editor text." : listingWarnings;
        if (listing.Entries.Count != 0) SnapshotList.SelectedIndex = 0;
    }
    private async void SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var revision = ++selectionRevision;
        RestoreButton.IsEnabled = false;
        if (SelectedSnapshot is not { } entry) return;
        try
        {
            var record = await Task.Run(() => snapshots.ReadAsync(entry));
            if (revision != selectionRevision) return;
            const int previewLimit = 128 * 1024;
            SourceText.Text = record.Document.Text[..Math.Min(record.Document.Text.Length, previewLimit)];
            SnapshotStatus.Text = string.Join("\n", new[] { record.Document.Path ?? "Unsaved document", listingWarnings,
                record.Document.Text.Length > previewLimit ? "Preview shows the first 131,072 characters. Opening a recovered copy restores the entire source." : "" }.Concat(record.Warnings).Where(s => s.Length != 0));
            RestoreButton.IsEnabled = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { if (revision == selectionRevision) { SourceText.Clear(); SnapshotStatus.Text = ex.Message; } }
    }
    private void Restore(object sender, RoutedEventArgs e) { if (RestoreButton.IsEnabled && SelectedSnapshot is not null) DialogResult = true; }
}
