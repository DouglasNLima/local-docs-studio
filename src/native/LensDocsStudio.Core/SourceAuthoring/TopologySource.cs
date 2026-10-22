using System.Text.RegularExpressions;

namespace LensDocsStudio.Core.SourceAuthoring;

/// <summary>Shared dispatch for architecture views; authoritative source remains untouched.</summary>
public static class TopologySource
{
    private static readonly Regex Header = SourceText.Pattern(@"^(?:architecture(?:-beta)?(?:\s|$)|C4(?:Context|Container|Component|Dynamic|Deployment)\b|block(?:-beta)?\b)", RegexOptions.IgnoreCase);
    public static bool IsTopology(string source) => Header.IsMatch(ChartSource.Header(source));
    public static string? ResourceProblem(string source)
    {
        if (!IsTopology(source)) return null;
        try
        {
            var text = SourceText.Read(source, int.MaxValue); var header = ChartSource.Header(source); var count = 0;
            if (header.StartsWith("C4", StringComparison.OrdinalIgnoreCase))
            {
                var model = C4Diagrams.C4VisualDraft.Read(text); count = model.Nodes.Count + model.Relations.Count(r => r.Call.Positional.Count >= (r.Call.Keyword.Value.Equals("RelIndex", StringComparison.OrdinalIgnoreCase) ? 3 : 2));
                if (model.MaximumDepth > Mermaider.Models.ResourceLimits.Default.MaxRecursionDepth) return "C4 boundary nesting exceeds the existing 64-level rendering resource budget. Correct the preserved original source.";
            }
            else if (header.StartsWith("block", StringComparison.OrdinalIgnoreCase))
            { var model = BlockDiagrams.BlockVisualDraft.Read(text); count = model.Visible.Count + model.Tokens.Count(t => t.Space) + model.Edges.Count; }
            // The pinned Architecture path already enforces this same element budget. C4 and Block bypass it upstream.
            return count > 500 ? "The diagram exceeds the existing 500-element rendering resource budget. Correct the preserved original source." : null;
        }
        catch (ArgumentException error) { return "The native source cannot be safely bounded: " + error.Message; }
        catch (RegexMatchTimeoutException) { return "The native source exceeded the existing bounded scanner timeout. Correct the preserved original source."; }
    }

    public static void Identifier(string value)
    {
        if (!SourceText.Pattern(@"^[A-Za-z_][A-Za-z0-9_-]*$").IsMatch(value) || value.Length > 200)
            throw new ArgumentException("Enter an identifier beginning with a letter or underscore, followed by letters, digits, underscores or hyphens (at most 200 characters).");
    }
    public static SourceText Patch(SourceText text, IEnumerable<(SourceField Field, string Encoded)> patches)
    {
        // Later tokens are replaced first; every untouched character and source occurrence stays exact.
        foreach (var (field, encoded) in patches.OrderByDescending(p => text.Index(p.Field.Identity)).ThenByDescending(p => p.Field.Start))
        {
            if (text.Line(field.Identity).Body.Substring(field.Start, field.Length) != field.Raw)
                throw new ArgumentException("The source token changed. Select the current occurrence again.");
            if (field.Raw != encoded) text = field.Set(text, encoded);
        }
        return text;
    }
    public static SourceText Append(SourceText text, string declaration)
    {
        SourceSections.CheckTail(text, 0, text.Lines.Count);
        return text.Insert(text.Lines.Count, declaration);
    }
    public static bool Acyclic(IEnumerable<string> ids, IEnumerable<(string Source, string Target)> connections)
    {
        var edges = connections.ToArray(); var done = new HashSet<string>(); var active = new HashSet<string>();
        bool Visit(string id)
        {
            if (active.Contains(id)) return false; if (!done.Add(id)) return true; active.Add(id);
            foreach (var edge in edges.Where(e => e.Source == id)) if (!Visit(edge.Target)) return false;
            active.Remove(id); return true;
        }
        return ids.All(Visit);
    }
    public static IReadOnlySet<string> ProvedRows(SourceText text, IEnumerable<string> identities)
    {
        var keys = new HashSet<string>();
        foreach (var identity in identities)
            foreach (var line in text.Lines.Skip(text.AttachedStart(identity)).Take(text.Index(identity) - text.AttachedStart(identity) + 1)) keys.Add(line.Key);
        return keys;
    }
    public static SourceText MoveRows(SourceText text, IReadOnlyList<string> rows, string identity, int direction, IEnumerable<string> independent)
    {
        var index = rows.ToList().IndexOf(identity); var next = index + Math.Sign(direction);
        if (index < 0 || next < 0 || next >= rows.Count) throw new ArgumentException("There is no occurrence in that direction.");
        return MoveGroup(text, rows[index], rows[index], rows[next], rows[next], direction, independent);
    }
    public static SourceText MoveGroup(SourceText text, string identity, string endIdentity, string peer, string peerEnd, int direction, IEnumerable<string> independent)
    {
        var start = text.AttachedStart(identity); var end = text.Index(endIdentity) + 1; var otherStart = text.AttachedStart(peer); var otherEnd = text.Index(peerEnd) + 1;
        if (start < otherEnd && otherStart < end) throw new ArgumentException("Source groups overlap; this move is protected.");
        SourceRowMoves.CheckTrailingAttachment(text, endIdentity); SourceRowMoves.CheckTrailingAttachment(text, peerEnd);
        var gapStart = Math.Min(end, otherEnd); var gapEnd = Math.Max(start, otherStart); var proved = ProvedRows(text, independent);
        if (text.Lines.Skip(gapStart).Take(gapEnd - gapStart).Any(l => l.Content.Length > 0 && !proved.Contains(l.Key))) throw new ArgumentException("An unproved intervening directive or comment protects this move.");
        if (text.Lines[end - 1].Ending.Length == 0 || text.Lines[otherEnd - 1].Ending.Length == 0) throw new ArgumentException("An unterminated source group protects this move.");
        // Move only the chosen complete group. Independent middle rows retain their bytes and relative order.
        return SourceSections.MoveTo(text, start, end, direction < 0 ? otherStart : otherEnd);
    }
}
