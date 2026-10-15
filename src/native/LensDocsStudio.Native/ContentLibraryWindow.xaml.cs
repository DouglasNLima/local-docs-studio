using System.Windows;
using System.Windows.Controls;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.State;
using LensDocsStudio.Native.Platform;
using Microsoft.Win32;

namespace LensDocsStudio.Native;

public sealed record ContentLibraryEntry(LocalLibraryKind Kind, string Id, string Label, LocalLibraryItem? Item = null, LocalExportProfile? Profile = null)
{
    public string Description => Kind switch { LocalLibraryKind.Template => Item?.Name ?? "New Markdown document", LocalLibraryKind.Snippet => "Insert into your current document", _ => "Apply export defaults for this session" };
    public string Source => Item?.Content ?? (Profile is { } profile ? "Azure DevOps Mermaid: " + (profile.DevopsMarkdownExport ? "Enabled" : "Disabled")
        + (profile.DocsSite is { } site ? "\n\nDocs Site title: " + site.Title + "\nTheme: " + site.Theme + "\n\n" + site.Description : "") : "");
}

public partial class ContentLibraryWindow : Window
{
    private readonly LocalLibraryStore store;
    private readonly string exportRecovery;
    private readonly Func<string, byte[], Task>? exportCompleted;
    private LocalLibrarySnapshot snapshot;
    private CancellationTokenSource? operation;
    private bool ready, busy, closeRequested;
    public ContentLibraryEntry? SelectedEntry { get; private set; }
    private ContentLibraryEntry? Selected => Entries.SelectedItem as ContentLibraryEntry;

    public ContentLibraryWindow(LocalLibraryStore store, LocalLibrarySnapshot snapshot, string exportRecovery, LocalLibraryKind kind = LocalLibraryKind.Template, Func<string, byte[], Task>? exportCompleted = null)
    {
        this.store = store; this.snapshot = snapshot; this.exportRecovery = exportRecovery;
        this.exportCompleted = exportCompleted;
        InitializeComponent(); WindowStateService.FitInitialWindow(this); Kind.SelectedIndex = (int)kind;
        ready = true; Populate();
        Closing += (_, e) => { if (busy) { closeRequested = true; operation?.Cancel(); e.Cancel = true; } };
    }
    private void Populate(string? selectedId = null)
    {
        var library = Archived.IsChecked == true ? snapshot.State.Archived : snapshot.State.Active;
        var kind = (LocalLibraryKind)Kind.SelectedIndex;
        var entries = kind switch
        {
            LocalLibraryKind.Template => library.Templates.Select(item => new ContentLibraryEntry(kind, item.Id, item.Label, item)),
            LocalLibraryKind.Snippet => library.Snippets.Select(item => new ContentLibraryEntry(kind, item.Id, item.Label, item)),
            _ => library.Profiles.Select(profile => new ContentLibraryEntry(kind, profile.Id, profile.Label, Profile: profile))
        };
        var shown = entries.Where(entry => entry.Label.Contains(Filter.Text.Trim(), StringComparison.OrdinalIgnoreCase)).OrderBy(entry => entry.Label, StringComparer.OrdinalIgnoreCase).ToArray();
        Entries.ItemsSource = shown; Entries.SelectedItem = shown.FirstOrDefault(entry => entry.Id == selectedId) ?? shown.FirstOrDefault();
        Status.Text = shown.Length == 0 ? "No matching entries. Save content from the File menu or import a library JSON export."
            : $"{shown.Length} " + (shown.Length == 1 ? "entry." : "entries.") + " Templates open in new tabs; snippets insert into the current document. Imported relative links need their original workspace files.";
        Selection(); SetControls();
    }
    private void FilterChanged(object sender, RoutedEventArgs e) { if (ready && !busy) Populate(); }
    private void SelectionChanged(object sender, SelectionChangedEventArgs e) { if (ready) { Selection(); SetControls(); } }
    private void Selection()
    {
        var source = Selected?.Source ?? "";
        Preview.Text = source.Length <= 32768 ? source : source[..32768] + "\n\nPreview shortened. The complete source is retained.";
    }
    private void SetControls()
    {
        Kind.IsEnabled = Filter.IsEnabled = Entries.IsEnabled = Archived.IsEnabled = ImportButton.IsEnabled = ExportButton.IsEnabled = RefreshButton.IsEnabled = !busy;
        RenameButton.IsEnabled = ArchiveButton.IsEnabled = !busy && Selected is not null;
        UseButton.IsEnabled = !busy && Selected is not null && Archived.IsChecked != true;
        UseButton.Content = Kind.SelectedIndex switch { 1 => "Insert snippet", 2 => "Apply profile", _ => "Create document" };
        ArchiveButton.Content = Archived.IsChecked == true ? "Restore" : "Archive";
        Progress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed; CancelButton.IsEnabled = busy;
        CancelButton.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }
    private async Task Run(Func<CancellationToken, Task> action)
    {
        if (busy) return; busy = true; operation = new(); SetControls();
        try { await action(operation.Token); }
        catch (OperationCanceledException) { Status.Text = "Operation cancelled. Existing entries are retained."; }
        catch (Exception exception) when (exception is not OutOfMemoryException) { Status.Text = exception.Message; }
        finally { operation.Dispose(); operation = null; busy = false; SetControls(); if (closeRequested) DialogResult = false; }
    }
    private async void Import(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Title = "Import content library", Filter = "Lens Docs Studio library|*.json" };
        if (picker.ShowDialog(this) != true) return;
        await Run(async token =>
        {
            Status.Text = "Reading and merging library entries…";
            var result = await Task.Run(() => store.ImportFileAsync(snapshot, picker.FileName, token), token); snapshot = result.Snapshot;
            Archived.IsChecked = false; Filter.Clear(); Populate();
            Status.Text = $"Imported {result.Added} entries; {result.AlreadyPresent} were already present. Existing entries and the original JSON file were retained.";
        });
    }
    private async void Export(object sender, RoutedEventArgs e)
    {
        var archived = Archived.IsChecked == true;
        var picker = new SaveFileDialog { Title = "Export content library", Filter = "Lens Docs Studio library|*.json", DefaultExt = ".json", AddExtension = true,
            FileName = archived ? "lens-docs-studio-archived-library.json" : "lens-docs-studio-library.json" };
        if (picker.ShowDialog(this) != true) return;
        await Run(async token =>
        {
            if (!Path.GetExtension(picker.FileName).Equals(".json", StringComparison.OrdinalIgnoreCase)) throw new IOException("Use the .json extension for a library export.");
            var expected = await ExportFileStore.ReadExistingAsync(picker.FileName, token);
            var bytes = await Task.Run(() => LocalLibraryFormat.Write(archived ? snapshot.State.Archived : snapshot.State.Active), token);
            await new ExportFileStore(exportRecovery).WriteAsync(picker.FileName, bytes, expected, token);
            token.ThrowIfCancellationRequested();
            if (exportCompleted is not null) await exportCompleted(picker.FileName, bytes);
            Status.Text = "Exported all " + (archived ? "archived" : "active") + " templates, snippets and profiles to " + Path.GetFileName(picker.FileName) + ".";
        });
    }
    private async void Rename(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } entry) return;
        var name = new TextPromptWindow("Rename library entry", "Library name", entry.Label, "Rename", "Library name", "Use 1–160 characters. Source content remains unchanged.") { Owner = this };
        if (name.ShowDialog() != true) return;
        var archived = Archived.IsChecked == true; var value = name.Value;
        await Run(async token => { snapshot = await Task.Run(() => store.RenameAsync(snapshot, entry.Kind, entry.Id, value, archived, token), token); Populate(entry.Id); Status.Text = "Library entry renamed."; });
    }
    private async void ArchiveOrRestore(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } entry) return; var archive = Archived.IsChecked != true;
        await Run(async token => { snapshot = await Task.Run(() => store.MoveAsync(snapshot, entry.Kind, entry.Id, archive, token), token); Populate(); Status.Text = archive ? "Entry archived. Show archived entries to restore it." : "Entry restored to the active library."; });
    }
    private async void Refresh(object sender, RoutedEventArgs e) => await Run(async token => { snapshot = await Task.Run(() => store.ReadAsync(token), token); Populate(); });
    private void CancelOperation(object sender, RoutedEventArgs e) => operation?.Cancel();
    private void UseSelected(object sender, RoutedEventArgs e) { if (Selected is not null && Archived.IsChecked != true) { SelectedEntry = Selected; DialogResult = true; } }
}
