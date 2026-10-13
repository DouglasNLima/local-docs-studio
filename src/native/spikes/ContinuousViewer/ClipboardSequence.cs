using System.Runtime.InteropServices;

namespace ContinuousViewer;

internal static partial class ClipboardSequence
{
    [LibraryImport("user32.dll", EntryPoint = "GetClipboardSequenceNumber")]
    internal static partial uint Read();
}
