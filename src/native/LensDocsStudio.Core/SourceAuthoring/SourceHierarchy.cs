namespace LensDocsStudio.Core.SourceAuthoring;

public sealed record SourceHierarchyRow(string Identity, int Indent, string? Parent, int Depth);

/// <summary>Complete lossless source groups; family scanners supply the proved hierarchy and indentation meaning.</summary>
public static class SourceHierarchy
{
    public static IReadOnlyList<SourceHierarchyRow> Link(IEnumerable<(string Identity, int Indent)> input)
    {
        var rows = new List<SourceHierarchyRow>(); var stack = new List<SourceHierarchyRow>();
        foreach (var (identity, indent) in input)
        {
            while (stack.Count > 0 && stack[^1].Indent >= indent) stack.RemoveAt(stack.Count - 1);
            var row = new SourceHierarchyRow(identity, indent, stack.LastOrDefault()?.Identity, stack.Count); rows.Add(row); stack.Add(row);
        }
        return rows;
    }
    public static IReadOnlyList<SourceHierarchyRow> Subtree(IReadOnlyList<SourceHierarchyRow> rows, string identity)
    {
        var at = rows.ToList().FindIndex(r => r.Identity == identity); if (at < 0) throw new ArgumentException("Select an existing hierarchy occurrence.");
        return rows.Skip(at).TakeWhile((r, index) => index == 0 || r.Depth > rows[at].Depth).ToArray();
    }
    public static (int Start, int End) Bounds(SourceText text, IReadOnlyList<SourceHierarchyRow> rows, string identity)
    {
        var group = Subtree(rows, identity); var start = text.AttachedStart(identity); var end = text.Index(group[^1].Identity) + 1;
        WorkflowSource.CompleteGroup(text, start, end, group.Select(r => r.Identity)); SourceRowMoves.CheckTrailingAttachment(text, group[^1].Identity);
        var attached = TopologySource.ProvedRows(text, group.Select(r => r.Identity));
        if (text.Lines.Skip(start).Take(end - start).Any(l => l.Comment && !attached.Contains(l.Key))) throw new ArgumentException("A comment has unproved subtree attachment. Inspect source before structural editing.");
        if (text.Lines.Skip(start).Take(end - start).Any(l => l.Content.Length == 0)) throw new ArgumentException("A blank line has unproved subtree attachment. Inspect source before structural editing.");
        return (start, end);
    }
    public static SourceText Delete(SourceText text, IReadOnlyList<SourceHierarchyRow> rows, string identity)
    { var (start, end) = Bounds(text, rows, identity); return text.Splice(start, end - start, []); }
    public static SourceText Move(SourceText text, IReadOnlyList<SourceHierarchyRow> rows, string identity, int direction)
    {
        var row = rows.Single(r => r.Identity == identity); var siblings = rows.Where(r => r.Parent == row.Parent).ToArray(); var at = Array.IndexOf(siblings, row); var next = at + Math.Sign(direction);
        if (next < 0 || next >= siblings.Length) throw new ArgumentException("There is no sibling subtree in that direction.");
        var a = Bounds(text, rows, identity); var b = Bounds(text, rows, siblings[next].Identity);
        var low = Math.Min(a.Start, b.Start); var high = Math.Max(a.End, b.End);
        if (text.Lines.Skip(low).Take(high - low).Any(l => l.Content.Length == 0)) throw new ArgumentException("An intervening blank line has unproved attachment. Inspect source.");
        return SourceRowMoves.Swap(text, direction < 0 ? siblings[next].Identity : identity, direction < 0 ? identity : siblings[next].Identity, direction < 0 ? b.End : a.End, direction < 0 ? a.End : b.End);
    }
    public static SourceText Reparent(SourceText text, IReadOnlyList<SourceHierarchyRow> rows, string identity, string? parent, int newIndent, bool allowTabs = false)
    {
        var group = Subtree(rows, identity); if (parent is not null && group.Any(r => r.Identity == parent)) throw new ArgumentException("A subtree cannot become its own descendant.");
        var (start, end) = Bounds(text, rows, identity); var destination = parent is null ? text.Lines.Count : Bounds(text, rows, parent).End;
        var delta = newIndent - group[0].Indent; var changed = text;
        foreach (var row in group)
        {
            var line = changed.Line(row.Identity);
            if (!allowTabs && line.Indent.Contains('\t')) throw new ArgumentException("Tab indentation remains exact; this structural re-encoding is protected. Use Edit source.");
            if (row.Indent + delta < 0) throw new ArgumentException("The requested indentation is outside the hierarchy.");
            changed = changed.Replace(line.Replace(0, line.Indent.Length, new string(' ', row.Indent + delta)));
        }
        // Attached comments move with the group and receive the same proved indentation delta.
        foreach (var original in text.Lines.Skip(start).Take(end - start).Where(l => l.Comment))
        {
            if (original.Indent.Contains('\t') || original.Indent.Length + delta < 0) throw new ArgumentException("Comment indentation cannot be re-encoded safely. Inspect source.");
            changed = changed.Replace(original.Replace(0, original.Indent.Length, new string(' ', original.Indent.Length + delta)));
        }
        return destination >= start && destination <= end ? changed : SourceSections.MoveTo(changed, start, end, destination);
    }
    public static void ProveMembership(IReadOnlyList<SourceHierarchyRow> before, IReadOnlyList<SourceHierarchyRow> after, string? moved = null, string? parent = null)
    {
        foreach (var row in before)
        {
            var next = after.SingleOrDefault(r => r.Identity == row.Identity); if (next is null) continue;
            if (next.Parent != (row.Identity == moved ? parent : row.Parent)) throw new ArgumentException("The structural edit changes an unintended parent/child membership. Source is unchanged.");
        }
    }
}
