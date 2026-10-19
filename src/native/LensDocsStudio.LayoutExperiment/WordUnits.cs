// Classification rules and Latin table adapted from dotnet/wpf v10.0.0
// SelectionWordBreaker.cs, MIT licence; see THIRD-PARTY-NOTICES.txt.
// Public GetStringTypeW supplies character data; no framework internals are called.
using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace LensDocsStudio.LayoutExperiment;

internal static class WordUnits
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetStringTypeW(uint type, string text, int length, [Out] ushort[] classifications);
    public static ImmutableArray<int> Create(string text, CancellationToken cancellation)
    {
        if (text.Length == 0) return [0];
        var type1 = new ushort[text.Length]; var type3 = new ushort[text.Length]; var classes = new byte[text.Length];
        if (!GetStringTypeW(1, text, text.Length, type1) || !GetStringTypeW(4, text, text.Length, type3)) throw new Win32Exception();
        for (var at = 0; at < text.Length; at++)
        {
            if ((at & 255) == 0) cancellation.ThrowIfCancellationRequested(); var value = text[at];
            classes[at] = value < 256 ? Latin[value] : value is >= '\uAC00' and <= '\uD7FF' or >= '\u0E00' and <= '\u0E7F' ? (byte)0
                : value == '\uFFFC' ? (byte)0x42 : (type1[at] & 8) != 0 ? (byte)((type1[at] & 64) != 0 ? 0x12 : 0x14)
                : (type1[at] & 16) != 0 && (type3[at] & 0x207) == 0 ? (byte)1 : (byte)0;
        }
        var starts = ImmutableArray.CreateBuilder<int>(); starts.Add(0);
        for (var at = 1; at < text.Length; at++)
        {
            if ((at & 255) == 0) cancellation.ThrowIfCancellationRequested();
            if ((classes[at] & 15) == 2 && text[at] != '\uFFFC') continue;
            if (text[at] == '\r' || !SameClass(at) && !MidLetter(at - 1) && !MidLetter(at)) starts.Add(at);
        }
        starts.Add(text.Length); return starts.ToImmutable();
        bool MidLetter(int at) => at > 0 && at + 1 < text.Length && text[at] is '\'' or '\u2019' or '\u00AD'
            && (classes[at - 1] & 15) == 0 && (classes[at + 1] & 15) == 0;
        bool SameClass(int at)
        {
            var left = type3[at - 1]; var right = type3[at]; var leftIdeographic = (left & 0x130) != 0; var rightIdeographic = (right & 0x130) != 0;
            if (!leftIdeographic && !rightIdeographic) return (classes[at - 1] & 15) == (classes[at] & 15);
            var delta = (left & 0x1F0) ^ (right & 0x1F0);
            return leftIdeographic && rightIdeographic && (left & 0xF0) != 0 && delta is 0 or 0x80 or 0x20 or 0xA0;
        }
    }
    private static readonly byte[] Latin =
    [
        0,0,0,0,0,0,0,0x14, 0,0x13,0x14,0x14,0x14,0x14,0,0,
        0,0,0,0,0,0,0,0, 0,0,0,0,0,0,0,0,
        0x32,1,1,1,1,1,1,1, 1,1,1,1,1,0x41,1,1,
        0,0,0,0,0,0,0,0, 0,0,1,1,1,1,1,1,
        1,0,0,0,0,0,0,0, 0,0,0,0,0,0,0,0,
        0,0,0,0,0,0,0,0, 0,0,0,1,1,1,1,1,
        1,0,0,0,0,0,0,0, 0,0,0,0,0,0,0,0,
        0,0,0,0,0,0,0,0, 0,0,0,1,1,1,1,0,
        0,0,0,0,0,0,0,0, 0,0,0,0,0,0,0,0,
        0,0,0,0,0,0,0,0, 0,0,0,0,0,0,0,0,
        0x12,1,1,1,1,1,1,1, 1,1,1,1,1,1,1,1,
        1,1,1,1,1,1,1,1, 1,1,1,1,1,1,1,1,
        0,0,0,0,0,0,0,0, 0,0,0,0,0,0,0,0,
        0,0,0,0,0,0,0,1, 0,0,0,0,0,0,0,0,
        0,0,0,0,0,0,0,0, 0,0,0,0,0,0,0,0,
        0,0,0,0,0,0,0,0, 0,0,0,0,0,0,0,0
    ];
}
