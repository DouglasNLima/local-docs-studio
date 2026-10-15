using System.Diagnostics;
using LensDocsStudio.Core.Exports;

namespace LensDocsStudio.Native.Platform;

public sealed class ExportShellService
{
    public static ProcessStartInfo OpenFile(SavedExport saved) => new(saved.Path) { UseShellExecute = true };
    public static ProcessStartInfo ShowInFolder(SavedExport saved)
    {
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe")) { UseShellExecute = true };
        start.Arguments = "/select,\"" + saved.Path + "\"";
        return start;
    }
    public async Task InvokeAsync(ExportSessionHistory history, SavedExport saved, bool reveal, CancellationToken cancellationToken = default)
    {
        await history.VerifyAsync(saved, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        Process.Start(reveal ? ShowInFolder(saved) : OpenFile(saved));
    }
}
