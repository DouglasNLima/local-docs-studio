namespace LensDocsStudio.Core.SourceAuthoring;

public static class SourceRowMoves
{
    public static SourceText Swap(SourceText text, string first, string second, int firstEnd = -1, int secondEnd = -1)
    {
        var a = text.AttachedStart(first); var b = text.AttachedStart(second);
        var ae = firstEnd < 0 ? text.Index(first) + 1 : firstEnd; var be = secondEnd < 0 ? text.Index(second) + 1 : secondEnd;
        if (a >= b || ae > b || be > text.Lines.Count) throw new ArgumentException("Source groups overlap; this move is protected.");
        if (text.Lines.Skip(ae).Take(b - ae).Any(line => line.Content.Length > 0))
            throw new ArgumentException("An intervening directive or ambiguously attached comment protects this move.");
        if (text.Lines[ae - 1].Ending.Length == 0 || text.Lines[be - 1].Ending.Length == 0)
            throw new ArgumentException("This move would change an unterminated source group. Add its line ending in Edit source first.");
        return text.Splice(a, be - a, text.Lines.Skip(b).Take(be - b).Concat(text.Lines.Skip(ae).Take(b - ae)).Concat(text.Lines.Skip(a).Take(ae - a)));
    }
}
