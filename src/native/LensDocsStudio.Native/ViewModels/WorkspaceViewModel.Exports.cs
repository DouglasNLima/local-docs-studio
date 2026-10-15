using LensDocsStudio.Core.Exports;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LensDocsStudio.Native.ViewModels;

public partial class WorkspaceViewModel
{
    public ExportSessionHistory ExportHistory { get; } = new();
    [ObservableProperty] private SavedExport? completedExport;
    partial void OnStatusChanged(string value) => CompletedExport = null;
    partial void OnIsBusyChanged(bool value) { if (value) CompletedExport = null; }
    [RelayCommand] private void DismissExport() => CompletedExport = null;
    public async Task CompleteExportAsync(string path, byte[] bytes, string? details = null)
    {
        OperationToken.ThrowIfCancellationRequested();
        var saved = await Task.Run(() => SavedExport.FromSavedBytes(path, bytes), OperationToken);
        OperationToken.ThrowIfCancellationRequested();
        ExportHistory.Remember(saved);
        Status = "Export completed — " + saved.Name + (string.IsNullOrEmpty(details) ? "" : ". " + details);
        CompletedExport = saved;
    }
}
