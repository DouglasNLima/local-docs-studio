using System.Globalization;
using System.Text.RegularExpressions;

namespace LensDocsStudio.Core.SourceAuthoring;

/// <summary>Bounded preflight for the four pinned native paths, including recursive computed-value work.</summary>
public static class ExpansionViSource
{
    private static readonly Regex FamilyHeader = SourceText.Pattern(@"^(?:packet(?:-beta)?\b|treeView(?:-beta)?(?:\s|$)|treemap-beta$|venn-beta$)", RegexOptions.IgnoreCase);
    public static readonly Regex PacketField = SourceText.Pattern("^[ \\t]*(?:\\+(?<count>\\d+)|(?<start>\\d+)(?:-(?<end>\\d+))?)\\s*:\\s*\"(?<label>[^\"]*)\"[ \\t]*$");
    public static readonly Regex TreemapLeaf = SourceText.Pattern("^[ \\t]*\"(?<label>[^\"]+)\"\\s*:\\s*(?<value>\\d+(?:\\.\\d+)?)[ \\t]*$");
    public static readonly Regex VennSet = SourceText.Pattern("^[ \\t]*set\\s+(?:\"(?<quoted>[^\"]+)\"|(?<id>[^\\s\\[]+))(?:\\s*\\[\"(?<label>[^\"]+)\"\\])?\\s*(?::\\s*(?<value>\\d+(?:\\.\\d+)?))?[ \\t]*$");
    public static readonly Regex VennUnion = SourceText.Pattern("^[ \\t]*union\\s+(?<members>.+?)\\s*(?:\\[\"(?<label>[^\"]+)\"\\])?\\s*(?::\\s*(?<value>\\d+(?:\\.\\d+)?))?[ \\t]*$");
    public static bool IsFamily(string source) => FamilyHeader.IsMatch(ChartSource.Header(source));
    public static void PreserveProtected(SourceText before, IEnumerable<string> previous, SourceText after, IEnumerable<string> current)
    {
        var retained = previous.Distinct().Select(key => before.Line(key).Text).GroupBy(s => s, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        foreach (var row in current.Distinct().Select(key => after.Line(key).Text))
        {
            if (!retained.TryGetValue(row, out var count) || count == 0) throw new ArgumentException("Pending source introduces malformed, skipped or unproved grammar. Correct the highlighted source text or restore the last valid draft; Apply remains disabled.");
            retained[row] = count - 1;
        }
    }
    public static bool PacketRange(Match match, int cursor, out int start, out int end)
    {
        start = end = 0;
        if (!match.Success) return false;
        if (match.Groups["count"].Success)
        {
            if (!Integer(match.Groups["count"].Value, 1, 4096, out var count) || cursor > 4095) return false;
            start = cursor; var last = (long)cursor + count - 1; if (last > 4095) return false; end = (int)last;
        }
        else
        {
            if (!Integer(match.Groups["start"].Value, 0, 4095, out start)) return false;
            end = start; if (match.Groups["end"].Success && !Integer(match.Groups["end"].Value, start, 4095, out end)) return false;
        }
        return true;
    }
    public static bool Integer(string raw, int minimum, int maximum, out int value)
        => int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= minimum && value <= maximum;
    public static string? ResourceProblem(string source)
    {
        if (!IsFamily(source)) return null;
        try
        {
            var body = ChartSource.Body(SourceText.Read(source, int.MaxValue)); if (body.Count == 0) return null;
            var header = body[0].Content; var rows = body.Skip(1).ToArray();
            if (header.StartsWith("packet", StringComparison.OrdinalIgnoreCase))
            {
                var cursor = 0; var fields = 0; var segments = 0;
                foreach (var line in rows)
                {
                    if (!PacketRange(PacketField.Match(line.Body), cursor, out var start, out var end)) continue;
                    cursor = end + 1; fields++; segments += end / 32 - start / 32 + 1;
                    if (fields > 500 || segments > 500) return "Packet fields or split native segments exceed the existing 500-element rendering resource budget. Inspect the preserved source.";
                }
            }
            else if (header.StartsWith("treeView", StringComparison.OrdinalIgnoreCase) || header.Equals("treemap-beta", StringComparison.OrdinalIgnoreCase))
            {
                // The upstream Treemap computes totals recursively before its renderer depth guard.
                // Bound the actual source hierarchy with an iterative stack before calling the engine.
                if (rows.Length > 500) return "The hierarchy exceeds the existing 500-element rendering resource budget. Inspect the preserved source.";
                var tree = header.StartsWith("treeView", StringComparison.OrdinalIgnoreCase);
                var box = tree && rows.Length > 0 && SourceText.Pattern("[├└┣┗]").IsMatch(rows[0].Body);
                var stack = new List<(int Indent, double? Explicit, double Total)>(); var rootTotal = 0d;
                foreach (var line in rows)
                {
                    var raw = tree ? line.Body.Replace("\t", "    ", StringComparison.Ordinal) : line.Body;
                    var branch = box ? SourceText.Pattern("[├└┣┗]").Match(raw) : null;
                    var indent = box ? branch!.Success ? branch.Index / 4 : 0 : raw.TakeWhile(c => c == ' ' || !tree && c == '\t').Count();
                    while (stack.Count > 0 && stack[^1].Indent >= indent) if (!Pop()) return CalculationError;
                    if (stack.Count > Mermaider.Models.ResourceLimits.Default.MaxRecursionDepth) return "Hierarchy nesting exceeds the existing 64-level rendering resource budget. Inspect the preserved source.";
                    double? value = null;
                    if (!tree && TreemapLeaf.Match(line.Body) is { Success: true } leaf)
                    {
                        if (!double.TryParse(leaf.Groups["value"].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number)) return CalculationError;
                        value = number;
                    }
                    stack.Add((indent, value, 0));
                }
                while (stack.Count > 0) if (!Pop()) return CalculationError;
                bool Pop()
                {
                    var node = stack[^1]; stack.RemoveAt(stack.Count - 1); var value = node.Explicit ?? node.Total;
                    // Native squarification divides the chart area by each positive total. Fail closed before non-finite scale/aggregation.
                    if (!double.IsFinite(value) || value > 0 && !double.IsFinite(600d * 372 / value)) return false;
                    if (stack.Count == 0) { rootTotal += value; return double.IsFinite(rootTotal) && (rootTotal == 0 || double.IsFinite(600d * 372 / rootTotal)); }
                    var parent = stack[^1]; stack[^1] = parent with { Total = parent.Total + value }; return double.IsFinite(stack[^1].Total);
                }
            }
            else
            {
                // Venn bounds parsed sets + unions upstream, but its decimal parser can silently produce Infinity.
                foreach (var line in rows)
                {
                    var match = VennSet.Match(line.Body); if (!match.Success) match = VennUnion.Match(line.Body);
                    if (match.Success && match.Groups["value"].Success && (!double.TryParse(match.Groups["value"].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number)))
                        return "Venn stored size metadata exceeds finite native numeric limits. Inspect the preserved source.";
                }
            }
            return null;
        }
        catch (ArgumentException error) { return "The native source cannot be safely bounded: " + error.Message; }
        catch (RegexMatchTimeoutException) { return "The native source exceeded the existing bounded scanner timeout. Inspect the preserved source."; }
    }
    private const string CalculationError = "Treemap values exceed finite aggregation or native calculation limits. Inspect the preserved decimal source; no replacement geometry was drawn.";
}
