using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private readonly ExportShellService exportShell = new();
    private async void OpenCompletedExport(object sender, RoutedEventArgs e) => await UseExportAsync(model.CompletedExport, false);
    private async void RevealCompletedExport(object sender, RoutedEventArgs e) => await UseExportAsync(model.CompletedExport, true);
    private async Task UseExportAsync(SavedExport? saved, bool reveal)
    {
        if (saved is null) return;
        try { await exportShell.InvokeAsync(model.ExportHistory, saved, reveal); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException or System.Runtime.InteropServices.COMException)
        { model.Status = "The exported file is unavailable or has changed. Export again, or check its location in Recent exports."; }
    }
    private void InitialiseExportFeedback()
    {
        var history = (System.Collections.Specialized.INotifyCollectionChanged)model.ExportHistory.Entries;
        history.CollectionChanged += Changed;
        Closed += (_, _) => history.CollectionChanged -= Changed;
        RefreshRecentExports();
        void Changed(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => RefreshRecentExports();
    }
    private void RefreshRecentExports()
    {
        var menu = RecentExportsMenu;
        menu.Items.Clear();
        if (model.ExportHistory.Entries.Count == 0) menu.Items.Add(new MenuItem { Header = "No exports this session", IsEnabled = false });
        foreach (var saved in model.ExportHistory.Entries)
        {
            var item = new MenuItem { Header = new TextBlock { Text = saved.Details, MaxWidth = 320, TextTrimming = TextTrimming.CharacterEllipsis }, ToolTip = saved.Path };
            System.Windows.Automation.AutomationProperties.SetName(item, saved.Details);
            var open = new MenuItem { Header = "Open file" }; open.Click += async (_, _) => await UseExportAsync(saved, false);
            var reveal = new MenuItem { Header = "Show in folder" }; reveal.Click += async (_, _) => await UseExportAsync(saved, true);
            var copy = new MenuItem { Header = "Copy path" };
            copy.Click += async (_, _) =>
            {
                try { await model.ExportHistory.VerifyAsync(saved); Clipboard.SetText(saved.Path); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
                { model.Status = "The export path could not be copied. Check that the exported file is still available and unchanged."; }
            };
            item.Items.Add(open); item.Items.Add(reveal); item.Items.Add(copy); menu.Items.Add(item);
        }
    }
}
