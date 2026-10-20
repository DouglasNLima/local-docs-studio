using System.Text.RegularExpressions;

namespace LensDocsStudio.Core.SourceAuthoring;

public sealed record SourceSection(string Identity, string Name, Match? Tokens);

/// <summary>Source group boundaries only. Families supply the occurrences proved safe to carry.</summary>
public static class SourceSections
{
    public static int End(SourceText text, IReadOnlyList<SourceSection> sections, SourceSection section)
    {
        var index = sections.ToList().FindIndex(s => s.Identity == section.Identity);
        return index + 1 < sections.Count ? text.AttachedStart(sections[index + 1].Identity) : text.Lines.Count;
    }
    public static void Safe(SourceText text, int start, int end, IEnumerable<string> supported)
    {
        var keys = supported.ToHashSet();
        if (text.Lines.Skip(start).Take(end - start).Any(l => l.Content.Length > 0 && !l.Comment && !keys.Contains(l.Key)))
            throw new ArgumentException("An unknown directive or unproved source attachment protects this structural operation. Inspect original source; local field edits remain available.");
        if (end > start && text.Lines[end - 1].Ending.Length == 0) throw new ArgumentException("An unterminated source group protects this operation. Add its line ending in Edit source first.");
    }
    public static SourceText MoveTo(SourceText text, int start, int end, int destination)
    {
        if (destination >= start && destination <= end) throw new ArgumentException("Choose a different destination section.");
        var group = text.Lines.Skip(start).Take(end - start).ToArray();
        var removed = text.Splice(start, end - start, []);
        return removed.Splice(destination > end ? destination - (end - start) : destination, 0, group);
    }
    public static SourceText ChangeSection(SourceText text, IReadOnlyList<SourceSection> sections, string identity, int direction, bool delete, IEnumerable<string> supported)
    {
        var section = sections.SingleOrDefault(s => s.Identity == identity && s.Tokens is not null) ?? throw new ArgumentException("Select a named section. The unsectioned group cannot be removed or reordered.");
        var start = text.AttachedStart(identity); var end = End(text, sections, section); Safe(text, start, end, supported);
        CheckTail(text, start, end);
        if (delete) return text.Splice(start, end - start, []);
        var index = sections.ToList().FindIndex(s => s.Identity == identity); var other = index + Math.Sign(direction);
        if (other < 1 || other >= sections.Count) throw new ArgumentException("There is no named section in that direction.");
        var next = sections[other]; var nextStart = text.AttachedStart(next.Identity); var nextEnd = End(text, sections, next); Safe(text, nextStart, nextEnd, supported); CheckTail(text, nextStart, nextEnd);
        return SourceRowMoves.Swap(text, sections[Math.Min(index, other)].Identity, sections[Math.Max(index, other)].Identity,
            index < other ? end : nextEnd, index < other ? nextEnd : end);
    }
    public static void CheckTail(SourceText text, int start, int end)
    {
        var i = end - 1; while (i >= start && text.Lines[i].Content.Length == 0) i--;
        if (i >= start && text.Lines[i].Comment) throw new ArgumentException("A trailing comment has unproved attachment; this structural operation is protected.");
    }
}
