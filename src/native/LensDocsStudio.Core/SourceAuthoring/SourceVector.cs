using System.Globalization;
using System.Text;

namespace LensDocsStudio.Core.SourceAuthoring;

public sealed record SourceToken(string Identity, int Start, int Length, string Raw, string Value, string Segment);
public sealed record SourceVector(int Start, int Length, IReadOnlyList<SourceToken> Items, bool Balanced)
{
    private static readonly System.Text.RegularExpressions.Regex NumericPattern = SourceText.Pattern(@"^[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?$");
    private static readonly System.Text.RegularExpressions.Regex DirectivePattern = SourceText.Pattern(@"^[0-9]+(?:\.[0-9]+)?$");
    /// <summary>Exact comma spans, with only the pinned quote toggling/doubled-quote rules when requested.</summary>
    public static SourceVector Read(SourceLine line, int start, int length, bool quoted = false)
    {
        var items = new List<SourceToken>(); var decoded = new StringBuilder(); var inQuotes = false; var begin = start;
        var body = line.Body; var end = start + length;
        for (var i = start; i <= end; i++)
        {
            if (i == end || body[i] == ',' && !inQuotes)
            {
                var segment = body[begin..i]; var trim = segment.Trim(); var offset = segment.Length - segment.TrimStart().Length;
                items.Add(new(line.Occurrence(items.Count), begin + offset, trim.Length, trim, decoded.ToString().Trim(), segment));
                decoded.Clear(); begin = i + 1; continue;
            }
            if (quoted && body[i] == '"')
            {
                if (inQuotes && i + 1 < end && body[i + 1] == '"') { decoded.Append('"'); i++; }
                else inQuotes = !inQuotes;
            }
            else decoded.Append(body[i]);
        }
        return new(start, length, items, !inQuotes);
    }
    public SourceLine Patch(SourceLine line, string identity, string value)
    {
        if (!Balanced) throw new ArgumentException("Unclosed quotes protect this field. Correct the original source first.");
        var item = Items.Single(i => i.Identity == identity); return line.Replace(item.Start, item.Length, value);
    }
    public SourceLine Reorder(SourceLine line, IReadOnlyList<SourceToken> items)
    {
        if (!Balanced) throw new ArgumentException("Unclosed quotes protect this structural operation.");
        return line.Replace(Start, Length, string.Join(",", items.Select(i => i.Segment))) with { OccurrenceKeys = items.Select(i => i.Identity).ToArray() };
    }
    public SourceLine Add(SourceLine line, string value)
    {
        if (!Balanced) throw new ArgumentException("Unclosed quotes protect this structural operation.");
        return line.Replace(Start + Length, 0, ", " + value) with { OccurrenceKeys = Items.Select(i => i.Identity).Append(Guid.NewGuid().ToString("N")).ToArray() };
    }
    public static bool Finite(string value, out double number)
    {
        number = 0;
        return NumericPattern.IsMatch(value)
            && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number)
            && (number != 0 || !value.Split(['e', 'E'])[0].Any(c => c is >= '1' and <= '9'));
    }
    public static double Number(string value, bool positive = false, bool directive = false)
    {
        if (!Finite(value, out var number) || positive && number <= 0 || directive && !DirectivePattern.IsMatch(value))
            throw new ArgumentException(directive ? "Enter an unsigned finite decimal directive, for example 10.5. Signs and exponents are unsupported here; pending input is retained."
                : positive ? "Enter a positive finite weight with invariant decimal/exponent syntax. Pending input is retained."
                : "Enter a complete finite number using a decimal point and optional exponent. Pending input is retained; no zero substitution is made.");
        return number;
    }
    public static string Quote(string value)
    { SourceText.Label(value); return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""; }
}
