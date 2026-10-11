using Microsoft.VisualBasic.FileIO;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Native.Platform;

public static class RecycleService
{
    public static Task RemoveAsync(string isolatedPath) => StaWorker.RunAsync(() =>
    {
        if (!Environment.UserInteractive) throw new IOException("Deleting a document requires an interactive Windows session.");
        WorkspacePath.RejectReparsePoints(isolatedPath);
        // The caller has isolated the file and retained its source. Deny writes while Windows recycles it.
        using var held = new FileStream(isolatedPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        FileSystem.DeleteFile(isolatedPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
        return true;
    });
}
