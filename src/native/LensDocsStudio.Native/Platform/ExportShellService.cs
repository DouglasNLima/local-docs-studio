using System.Diagnostics;
using System.Runtime.InteropServices;
using LensDocsStudio.Core.Exports;

namespace LensDocsStudio.Native.Platform;

public sealed class ExportShellService
{
    public static ProcessStartInfo OpenFile(SavedExport saved) => new(saved.Path) { UseShellExecute = true };
    public async Task InvokeAsync(ExportSessionHistory history, SavedExport saved, bool reveal, CancellationToken cancellationToken = default)
    {
        await history.VerifyAsync(saved, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (reveal) await StaWorker.RunAsync(() => { Reveal(saved.Path); return true; }, cancellationToken);
        else Process.Start(OpenFile(saved));
    }

    private static void Reveal(string path)
    {
        Marshal.ThrowExceptionForHR(CoInitializeEx(IntPtr.Zero, 2));
        IntPtr item = IntPtr.Zero, folder = IntPtr.Zero;
        try
        {
            Marshal.ThrowExceptionForHR(SHParseDisplayName(path, IntPtr.Zero, out item, 0, out _));
            Marshal.ThrowExceptionForHR(SHParseDisplayName(Path.GetDirectoryName(path)!, IntPtr.Zero, out folder, 0, out _));
            // Name the parent and its child explicitly, including on a freshly started Explorer shell.
            Marshal.ThrowExceptionForHR(SHOpenFolderAndSelectItems(folder, 1, [ILFindLastID(item)], 0));
        }
        finally
        {
            if (folder != IntPtr.Zero) Marshal.FreeCoTaskMem(folder);
            if (item != IntPtr.Zero) Marshal.FreeCoTaskMem(item);
            CoUninitialize();
        }
    }

    [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved, uint flags);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SHParseDisplayName(string name, IntPtr context, out IntPtr item, uint attributes, out uint actualAttributes);
    [DllImport("shell32.dll")] private static extern IntPtr ILFindLastID(IntPtr item);
    [DllImport("shell32.dll")] private static extern int SHOpenFolderAndSelectItems(IntPtr folder, uint count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] IntPtr[] children, uint flags);
}
