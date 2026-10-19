using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace LensDocsStudio.LayoutExperiment;

internal static class WordUnits
{
    [StructLayout(LayoutKind.Sequential)] private struct Analysis { public ushort ScriptAndFlags, State; }
    [StructLayout(LayoutKind.Sequential)] private struct ScriptItem { public int Position; public Analysis Analysis; }
    [DllImport("usp10.dll", CharSet = CharSet.Unicode)] private static extern int ScriptItemize(string text, int length, int maximum,
        IntPtr control, IntPtr state, [Out] ScriptItem[] items, out int count);
    [DllImport("usp10.dll", CharSet = CharSet.Unicode)] private static extern int ScriptBreak(string text, int length, ref Analysis analysis, [Out] byte[] attributes);
    public static ImmutableArray<int> Create(string text, CancellationToken cancellation)
    {
        if (text.Length == 0) return [0];
        var items = new ScriptItem[text.Length + 2];
        Marshal.ThrowExceptionForHR(ScriptItemize(text, text.Length, items.Length, IntPtr.Zero, IntPtr.Zero, items, out var count));
        var starts = new SortedSet<int> { 0, text.Length };
        for (var item = 0; item < count; item++)
        {
            cancellation.ThrowIfCancellationRequested();
            var start = items[item].Position; var end = items[item + 1].Position; var content = text[start..end]; var attributes = new byte[content.Length];
            // ScriptBreak does not mark the first character of every script item.
            // Preserve a word start after whitespace when script itemisation splits it.
            if (content.Length > 0 && !char.IsWhiteSpace(content[0]) && content[0] != '\uFFFC' && (start == 0 || char.IsWhiteSpace(text[start - 1]))) starts.Add(start);
            Marshal.ThrowExceptionForHR(ScriptBreak(content, content.Length, ref items[item].Analysis, attributes));
            for (var at = 0; at < attributes.Length; at++)
                if ((attributes[at] & 8) != 0 && !char.IsWhiteSpace(content[at]) && content[at] != '\uFFFC') starts.Add(start + at);
        }
        return starts.ToImmutableArray();
    }
}
