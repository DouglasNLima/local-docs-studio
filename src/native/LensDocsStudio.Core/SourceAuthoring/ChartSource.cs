using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace LensDocsStudio.Core.SourceAuthoring;

public static class ChartSource
{
    public const string Numeric = @"[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?";
    public static readonly Regex Numbers = SourceText.Pattern(Numeric);
    private static readonly Regex ChartHeader = SourceText.Pattern(@"^(?:xychart(?:-beta)?(?:\s|$)|radar-beta$|sankey-beta$)", RegexOptions.IgnoreCase);
    public static IReadOnlyList<SourceLine> Body(SourceText text)
    {
        var lines = text.Lines; var first = 0;
        while (first < lines.Count && lines[first].Content.Length == 0) first++;
        if (first < lines.Count && lines[first].Content == "---")
        {
            var end = first + 1; while (end < lines.Count && lines[end].Content != "---") end++;
            if (end < lines.Count) first = end + 1;
        }
        return lines.Skip(first).Where(l => l.Content.Length > 0 && !l.Comment && !l.InitDirective).ToArray();
    }
    public static string Header(string source)
    {
        var lines = source.Split('\n').Select(l => l.Trim()).ToArray(); var at = 0;
        while (at < lines.Length && lines[at].Length == 0) at++;
        if (at < lines.Length && lines[at] == "---")
        { var close = Array.IndexOf(lines, "---", at + 1); if (close >= 0) at = close + 1; }
        return lines.Skip(at).FirstOrDefault(l => l.Length > 0 && !l.StartsWith("%%", StringComparison.Ordinal)) ?? "";
    }
    public static bool IsChart(string source) => ChartHeader.IsMatch(Header(source));
    public static string Prepare(string source, int lineLimit = 1500)
    {
        // Keep configuration for the public preprocessor. Remove only inert trivia before detection.
        var text = SourceText.Read(source, lineLimit); var header = Body(text).FirstOrDefault(); if (header is null) return source;
        var at = text.Index(header.Key); var leading = text.Lines.Take(at).ToList();
        return string.Concat(leading.Where(l => l.Content.Length > 0 && !l.Comment).Select(l => l.Text)) + string.Concat(text.Lines.Skip(at).Select(l => l.Text));
    }
    public static string Unquote(string value) => value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal) : value;
    public static SourceText DeleteRow(SourceText text, string identity)
    { SourceRowMoves.CheckTrailingAttachment(text, identity); var start = text.AttachedStart(identity); return text.Splice(start, text.Index(identity) + 1 - start, []); }
    public static SourceText MoveRow(SourceText text, IReadOnlyList<string> rows, string identity, int direction)
    {
        var at = rows.ToList().IndexOf(identity); var next = at + Math.Sign(direction);
        if (at < 0 || next < 0 || next >= rows.Count) throw new ArgumentException("There is no occurrence in that direction.");
        SourceRowMoves.CheckTrailingAttachment(text, rows[at]); SourceRowMoves.CheckTrailingAttachment(text, rows[next]);
        return SourceRowMoves.Swap(text, rows[Math.Min(at, next)], rows[Math.Max(at, next)]);
    }
    public static double Attribute(XElement element, string name) => double.Parse((string)element.Attribute(name)!, System.Globalization.CultureInfo.InvariantCulture);
    public static double SvgNumber(double value) => double.Parse(Math.Round(value, 3).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture);
    public static SourceText Title(SourceText text, string header, SourceDeclaration? title, string value, bool quote = false)
    {
        SourceText.Label(value); if (title?.Value == value) return text;
        return SourceDeclarations.SetTitle(text, header, title, quote ? SourceVector.Quote(value) : value);
    }
}
