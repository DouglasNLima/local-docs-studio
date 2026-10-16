using System.Windows;
using System.Windows.Controls;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.State;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.ViewModels;
using Microsoft.Win32;

namespace LensDocsStudio.Native;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel model;
    private readonly string exportRecovery;
    private readonly Func<string, byte[], Task>? exportCompleted;
    private readonly Action? exportStarting;
    private CancellationTokenSource? transfer;
    private bool closeRequested;
    public SettingsWindow(SettingsViewModel model, string exportRecovery, Func<string, byte[], Task>? exportCompleted = null, Action? exportStarting = null)
    {
        this.model = model; this.exportRecovery = exportRecovery;
        this.exportCompleted = exportCompleted;
        this.exportStarting = exportStarting;
        InitializeComponent(); DataContext = model; WindowStateService.FitInitialWindow(this);
        var applied = false;
        model.Applied += () => { applied = true; DialogResult = true; };
        Closing += (_, args) =>
        {
            args.Cancel = model.IsSaving && !applied;
            if (transfer is not null) { closeRequested = true; transfer.Cancel(); }
        };
        Loaded += (_, _) => Theme.Focus();
    }
    private void SearchChanged(object sender, TextChangedEventArgs e)
    {
        if (SettingsSections is null) return;
        var terms = SettingsSearch.Text.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var count = 0;
        foreach (var section in SettingsSections.Children.OfType<StackPanel>())
        {
            var matches = 0;
            foreach (var field in section.Children.OfType<FrameworkElement>().Where(field => field.Tag is string))
            {
                var keywords = section.Tag + " " + field.Tag;
                var visible = terms.All(term => keywords.Contains(term, StringComparison.CurrentCultureIgnoreCase));
                field.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                if (visible) matches++;
            }
            section.Visibility = matches == 0 ? Visibility.Collapsed : Visibility.Visible; count += matches;
        }
        NoSettingsMatches.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SettingsScroller.ScrollToTop();
    }
    private async void ImportSettings(object sender, RoutedEventArgs e)
    {
        if (!model.CanEdit) return;
        var picker = new OpenFileDialog { Title = "Import appearance settings", Filter = "Lens appearance settings|*.json", CheckFileExists = true };
        if (picker.ShowDialog(this) != true) return;
        await Transfer(async token =>
        {
            await using var file = new FileStream(picker.FileName, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, true);
            if (file.Length > SettingsTransferFormat.MaximumBytes) throw new InvalidDataException("The settings file exceeds 64 KB.");
            var bytes = new byte[checked((int)file.Length)];
            await file.ReadExactlyAsync(bytes, token);
            token.ThrowIfCancellationRequested(); model.Import(bytes);
        });
    }
    private async void ExportSettings(object sender, RoutedEventArgs e)
    {
        if (!model.CanEdit) return;
        exportStarting?.Invoke();
        var picker = new SaveFileDialog { Title = "Export appearance settings", Filter = "Lens appearance settings|*.json",
            FileName = "lens-docs-studio-settings.json", DefaultExt = ".json", AddExtension = true, OverwritePrompt = true };
        if (picker.ShowDialog(this) != true) { model.Status = "Settings export cancelled."; return; }
        await Transfer(async token =>
        {
            var bytes = SettingsTransferFormat.Write(model.Current);
            if (!Path.GetExtension(picker.FileName).Equals(".json", StringComparison.OrdinalIgnoreCase)) throw new IOException("Use the .json extension when exporting appearance settings.");
            if (File.Exists(picker.FileName) && new FileInfo(picker.FileName).Length > SettingsTransferFormat.MaximumBytes)
                throw new IOException("The existing destination exceeds 64 KB. Choose a new settings filename.");
            var expected = await ExportFileStore.ReadExistingAsync(picker.FileName, token);
            await new ExportFileStore(exportRecovery).WriteAsync(picker.FileName, bytes, expected, token);
            token.ThrowIfCancellationRequested();
            if (exportCompleted is not null) await exportCompleted(picker.FileName, bytes);
            model.Status = "Selected appearance choices exported. Device settings are saved only when you choose Apply.";
        });
    }
    private async Task Transfer(Func<CancellationToken, Task> action)
    {
        if (!model.CanEdit) return;
        using var cancellation = new CancellationTokenSource(); transfer = cancellation; model.IsSaving = true;
        try { await action(cancellation.Token); }
        catch (OperationCanceledException) { model.Status = "Settings transfer cancelled."; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        { model.Status = "Settings transfer failed: " + error.Message; }
        finally { transfer = null; model.IsSaving = false; if (closeRequested) Close(); }
    }
}
