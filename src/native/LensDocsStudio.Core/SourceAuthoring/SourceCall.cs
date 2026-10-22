using System.Text;
using System.Text.RegularExpressions;

namespace LensDocsStudio.Core.SourceAuthoring;

/// <summary>Exact call arguments with the pinned C4 splitter's positional and quote semantics.</summary>
public sealed record SourceArgument(SourceField Token, string SplitValue, bool Named, bool Ignored);
public sealed record SourceCall(string Identity, SourceField Keyword, IReadOnlyList<SourceArgument> Arguments, int CloseIndex, bool Balanced)
{
    public IReadOnlyList<SourceArgument> Positional => Arguments.Where(a => !a.Named && !a.Ignored).ToArray();
    public string Value(int position, string fallback = "") => position < Positional.Count ? Positional[position].Token.Value : fallback;
    public static SourceCall Read(SourceLine line, Match match)
    {
        var group = match.Groups["args"]; var raw = group.Value; var arguments = new List<SourceArgument>(); var quoted = false; var start = 0;
        for (var at = 0; at < raw.Length; at++)
        {
            if (raw[at] == '"')
            {
                if (quoted && at + 1 < raw.Length && raw[at + 1] == '"') { at++; continue; }
                quoted = !quoted;
            }
            else if (raw[at] == ',' && !quoted) { Add(start, at); start = at + 1; }
        }
        Add(start, raw.Length);
        return new(line.Key, WorkflowSource.Field(line, "keyword", match.Groups["kind"]), arguments, group.Index + group.Length, !quoted);

        void Add(int from, int to)
        {
            while (from < to && char.IsWhiteSpace(raw[from])) from++;
            while (to > from && char.IsWhiteSpace(raw[to - 1])) to--;
            var original = raw[from..to]; var split = SplitValue(original);
            arguments.Add(new(new(line.Key, "argument", Decode(split), group.Index + from, to - from, original), split, split.StartsWith('$'), split.Length == 0));
        }
    }
    private static string SplitValue(string raw)
    {
        var value = new StringBuilder(); var quoted = false;
        for (var at = 0; at < raw.Length; at++)
        {
            if (raw[at] == '"')
            {
                if (quoted && at + 1 < raw.Length && raw[at + 1] == '"') { value.Append('"'); at++; continue; }
                quoted = !quoted;
            }
            value.Append(raw[at]);
        }
        return value.ToString().Trim();
    }
    private static string Decode(string split) => split.Length >= 2 && split[0] == '"' && split[^1] == '"' ? split[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal) : split;
    public static string Quote(string value)
    {
        if (value.Length > 0) SourceText.Label(value);
        var encoded = "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        if (Decode(SplitValue(encoded)) != value) throw new ArgumentException("Adjacent double quotes do not preserve their intended meaning in the pinned C4 splitter. Pending input is retained.");
        return encoded;
    }
    public SourceText Set(SourceText text, IReadOnlyDictionary<int, string> encoded)
    {
        var positional = Positional; var patches = encoded.Where(p => p.Key < positional.Count).Select(p => (positional[p.Key].Token, p.Value)).ToList();
        if (encoded.Keys.Any(i => i >= positional.Count))
        {
            var appended = new StringBuilder();
            for (var at = positional.Count; at <= encoded.Keys.Max(); at++) appended.Append(", ").Append(encoded.GetValueOrDefault(at, "\"\""));
            patches.Add((new(Identity, "append", "", CloseIndex, 0, ""), appended.ToString()));
        }
        return TopologySource.Patch(text, patches);
    }
}
