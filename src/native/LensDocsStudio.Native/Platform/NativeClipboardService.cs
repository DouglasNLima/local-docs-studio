using System.Runtime.InteropServices;
using System.Windows;
using LensDocsStudio.Core.Exports;

namespace LensDocsStudio.Native.Platform;

public static class NativeClipboardService
{
    public static uint Sequence => GetClipboardSequenceNumber();
    public static void WriteRendered(ExportedClipboard content, uint expectedSequence)
    {
        if (Sequence != expectedSequence) throw new IOException("The clipboard changed while content was being prepared. Copy again to replace it.");
        var data = new DataObject();
        data.SetData(DataFormats.UnicodeText, content.PlainText, false);
        data.SetData(DataFormats.Html, content.Html, false);
        Clipboard.SetDataObject(data, true);
    }
    [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
}
