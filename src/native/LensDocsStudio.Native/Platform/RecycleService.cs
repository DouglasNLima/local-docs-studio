using Microsoft.VisualBasic.FileIO;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Native.Platform;

public static class RecycleService
{
    public static Task RemoveFolderAsync(string isolatedPath) => StaWorker.RunAsync(() =>
    {
        if (!Environment.UserInteractive) throw new IOException("Deleting a folder requires an interactive Windows session.");
        var count = 0;
        void Check(string folder, int depth)
        {
            if (depth > 32) throw new IOException("Folder recycling is limited to 32 nested levels.");
            WorkspacePath.RejectReparsePoints(folder);
            foreach (var entry in Directory.EnumerateFileSystemEntries(folder))
            {
                if (++count > 20000) throw new IOException("Folder recycling is limited to 20,000 reviewed entries.");
                WorkspacePath.RejectReparsePoints(entry);
                if (Directory.Exists(entry)) Check(entry, depth + 1);
            }
        }
        Check(isolatedPath, 0);
        FileSystem.DeleteDirectory(isolatedPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
        return true;
    });
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
