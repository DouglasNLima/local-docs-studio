using System.Windows;
using LensDocsStudio.Core.Updates;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private UpdatesViewModel? updates;
    private UpdatesWindow? updatesWindow;
    private void InitialiseUpdates()
    {
        updates = new(model, InstallUpdateAsync);
        updates.UpdateAvailable += message => model.Status = message;
    }
    private void OpenUpdates(object sender, RoutedEventArgs e)
    {
        if (updates is null) return;
        if (updatesWindow is not null) { updatesWindow.Activate(); return; }
        updatesWindow = new(updates) { Owner = this };
        updatesWindow.Closed += (_, _) => updatesWindow = null;
        updatesWindow.Show();
    }
    private async Task InstallUpdateAsync(DownloadedUpdate update, UpdateEnvironment environment)
    {
        if (model.IsBusy || closePending) { model.Status = "Finish the current document operation before installing."; return; }
        if (!dialogs.Confirm("Install native version " + update.Candidate.Manifest.Version + "?\n\nLens Docs Studio will save your session, ask about unsaved edits and close. The verified installation wizard will then open. You can cancel the wizard.", "Install native update")) return;
        closePending = true; IsEnabled = false;
        try
        {
            SaveViewport();
            if (!await model.PrepareCloseAsync()) return;
            await model.UpdatePreferencesAsync(p => p with { Window = WindowStateService.Capture(this) });
            await NativeUpdateLauncher.PrepareAsync(update, environment, model.StateDirectory, CancellationToken.None);
            CompleteClose();
        }
        finally { if (!closing) { closePending = false; IsEnabled = true; } }
    }
    private void CompleteClose()
    {
        closing = true; previewCancellation?.Cancel(); previewTimer.Stop(); recoveryTimer.Stop();
        updates?.Dispose(); model.Dispose(); Close();
    }
}
