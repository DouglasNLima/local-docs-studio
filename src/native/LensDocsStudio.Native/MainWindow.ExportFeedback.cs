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
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException)
        { model.Status = "The exported file is unavailable or has changed. Export again, or check its location in Recent exports."; }
    }
    private void RecentExportsOpened(object sender, RoutedEventArgs e)
    {
        if (!ReferenceEquals(sender, e.Source) || sender is not MenuItem menu) return;
        menu.Items.Clear();
        if (model.ExportHistory.Entries.Count == 0) { menu.Items.Add(new MenuItem { Header = "No exports this session", IsEnabled = false }); return; }
        foreach (var saved in model.ExportHistory.Entries)
        {
            var item = new MenuItem { Header = saved.Details, ToolTip = saved.Path };
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
