using System.Windows;
using System.Windows.Controls;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Native.Platform;
using Microsoft.Win32;

namespace LensDocsStudio.Native;

public partial class WordTemplatesWindow : Window
{
    private readonly WordTemplateLibrary library;
    private readonly string exportRecovery;
    private readonly Guid? currentId;
    private IReadOnlyList<string> warnings = [];
    private CancellationTokenSource? operation;
    private bool busy, closeRequested;
    public byte[]? SelectedBytes { get; private set; }
    public Guid? SelectedId { get; private set; }
    public string SelectedName { get; private set; } = "Default Word styles";
    public string CurrentName { get; private set; }
    private WordTemplateEntry? Selected => TemplateList.SelectedItem as WordTemplateEntry;

    public WordTemplatesWindow(WordTemplateLibrary library, WordTemplateListing listing, string exportRecovery, Guid? currentId, string currentName)
    {
        this.library = library; this.exportRecovery = exportRecovery; this.currentId = currentId; CurrentName = currentName;
        InitializeComponent(); WindowStateService.FitInitialWindow(this); CurrentStyles.Text = "Current styles: " + currentName;
        Populate(listing, currentId); SetControls();
        Closing += (_, e) => { if (busy) { closeRequested = true; operation?.Cancel(); e.Cancel = true; } };
    }
    private void Populate(WordTemplateListing listing, Guid? selectedId)
    {
        warnings = listing.Warnings; TemplateList.ItemsSource = listing.Entries;
        TemplateList.SelectedItem = listing.Entries.FirstOrDefault(entry => entry.Record.Id == selectedId) ?? listing.Entries.FirstOrDefault();
        ShowSelection();
    }
    private void SelectionChanged(object sender, SelectionChangedEventArgs e) { if (LibraryStatus is not null) { ShowSelection(); SetControls(); } }
    private void ShowSelection()
    {
        LibraryStatus.Text = string.Join("\n", warnings.Take(3).Concat(Selected?.Record.Warnings ?? []));
        if (TemplateList.Items.Count == 0) LibraryStatus.Text = (Archived.IsChecked == true ? "No archived templates." : "Import a DOCX to keep its reusable formatting here.")
            + (LibraryStatus.Text.Length == 0 ? "" : "\n" + LibraryStatus.Text);
    }
    private void SetControls()
    {
        ImportButton.IsEnabled = RefreshButton.IsEnabled = Archived.IsEnabled = DefaultButton.IsEnabled = TemplateList.IsEnabled = !busy;
        RenameButton.IsEnabled = CopyButton.IsEnabled = ArchiveButton.IsEnabled = !busy && Selected is not null;
        UseButton.IsEnabled = !busy && Selected is { Archived: false };
        ArchiveButton.Content = Archived.IsChecked == true ? "Restore" : "Archive";
        CancelButton.IsEnabled = busy; Progress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }
    private async Task<bool> Run(Func<CancellationToken, Task> action)
    {
        if (busy) return false;
        busy = true; operation = new CancellationTokenSource(); SetControls();
        try { await action(operation.Token); return true; }
        catch (OperationCanceledException) { LibraryStatus.Text = "Operation cancelled. Existing templates are retained."; return false; }
        catch (Exception ex) when (ex is not OutOfMemoryException) { LibraryStatus.Text = ex.Message; return false; }
        finally
        {
            operation.Dispose(); operation = null; busy = false; SetControls();
            if (closeRequested) DialogResult = false;
        }
    }
    private async Task Reload(CancellationToken token, Guid? selectedId = null)
    {
        var archived = Archived.IsChecked == true;
        var listing = await Task.Run(() => library.ListAsync(archived, token), token); Populate(listing, selectedId);
    }
    private async void Refresh(object sender, RoutedEventArgs e) => await Run(token => Reload(token, Selected?.Record.Id));
    private async void ViewChanged(object sender, RoutedEventArgs e) { if (IsLoaded && !busy) await Run(token => Reload(token)); }
    private void CancelOperation(object sender, RoutedEventArgs e) => operation?.Cancel();
    private async void Import(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Title = "Import Word template to library", Filter = "Word documents|*.docx" };
        if (picker.ShowDialog(this) != true) return;
        var name = new TextPromptWindow("Name Word template", "Name for this template in your local library", Path.GetFileNameWithoutExtension(picker.FileName), "Import",
            "Template name", "Use 1–120 characters. The name is stored locally.") { Owner = this };
        if (name.ShowDialog() != true) return;
        await Run(async token =>
        {
            LibraryStatus.Text = "Validating and importing template…";
            var entry = await library.ImportAsync(picker.FileName, name.Value, token);
            Archived.IsChecked = false; await Reload(token, entry.Record.Id);
            LibraryStatus.Text = "Imported " + entry.Name + ". " + string.Join(" ", entry.Record.Warnings);
        });
    }
    private async void Rename(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } entry) return;
        var name = new TextPromptWindow("Rename Word template", "Template name", entry.Name, "Rename", "Template name", "Use 1–120 characters. The name is stored locally.") { Owner = this };
        if (name.ShowDialog() != true) return;
        await Run(async token =>
        {
            var renamed = await library.RenameAsync(entry, name.Value, token);
            if (renamed.Record.Id == currentId) { CurrentName = renamed.Name; CurrentStyles.Text = "Current styles: " + CurrentName; }
            await Reload(token, renamed.Record.Id); LibraryStatus.Text = "Template renamed.";
        });
    }
    private async void ArchiveOrRestore(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } entry) return;
        await Run(async token =>
        {
            if (entry.Archived) await library.RestoreAsync(entry, token); else await library.ArchiveAsync(entry, token);
            await Reload(token);
            LibraryStatus.Text = entry.Archived ? "Template restored to the active library." : "Template archived. Show archived templates to restore it."
                + (entry.Record.Id == currentId ? " The selected session copy remains in use until you choose other styles." : "");
        });
    }
    private async void SaveCopy(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } entry) return;
        var invalid = Path.GetInvalidFileNameChars(); var filename = string.Concat(entry.Name.Select(character => invalid.Contains(character) ? '_' : character)).Trim(' ', '.');
        var picker = new SaveFileDialog { Title = "Save Word template copy", Filter = "Word template document|*.docx", DefaultExt = ".docx", AddExtension = true,
            FileName = (filename.Length == 0 ? "Word template" : filename) + ".docx" };
        if (picker.ShowDialog(this) != true) return;
        await Run(async token =>
        {
            if (!Path.GetExtension(picker.FileName).Equals(".docx", StringComparison.OrdinalIgnoreCase)) throw new IOException("Use the .docx extension for a Word template copy.");
            var expected = await ExportFileStore.ReadExistingAsync(picker.FileName, token); var prepared = await library.ReadAsync(entry, token);
            await new ExportFileStore(exportRecovery).WriteAsync(picker.FileName, prepared.Bytes, expected, token);
            LibraryStatus.Text = "Saved reusable formatting to " + Path.GetFileName(picker.FileName) + ".";
        });
    }
    private async void UseSelected(object sender, RoutedEventArgs e)
    {
        if (Selected is not { Archived: false } entry) return;
        if (await Run(async token =>
        {
            LibraryStatus.Text = "Validating selected template…"; var prepared = await library.ReadAsync(entry, token);
            SelectedBytes = prepared.Bytes; SelectedName = entry.Name; SelectedId = entry.Record.Id;
        }) && IsVisible) DialogResult = true;
    }
    private void UseDefault(object sender, RoutedEventArgs e) { SelectedBytes = null; SelectedId = null; SelectedName = "Default Word styles"; DialogResult = true; }
}
