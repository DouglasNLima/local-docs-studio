using System.Globalization;
using System.Text.RegularExpressions;

namespace LensDocsStudio.Core.Parsing;

public sealed record DocumentMetadata
{
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string NavigationGroup { get; init; } = "";
    public double? Order { get; init; }
    public bool Draft { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
}
public sealed record FrontMatterResult(DocumentMetadata Metadata, int BodyStart);

/// <summary>Reads the existing flat document metadata contract without evaluating YAML or object tags.</summary>
public static partial class FrontMatterService
{
    public const int MaximumHeaderCharacters = 64 * 1024;
    public static FrontMatterResult Read(string source)
    {
        var empty = new FrontMatterResult(new(), 0);
        if (source.Length == 0) return empty;
        var offset = source[0] == '\uFEFF' ? 1 : 0;
        var firstEnd = source.IndexOf('\n', offset);
        if (firstEnd < 0 || source[offset..firstEnd].TrimEnd(' ', '\t', '\r') != "---") return empty;
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var start = firstEnd + 1; start <= Math.Min(source.Length, MaximumHeaderCharacters);)
        {
            var end = source.IndexOf('\n', start); if (end < 0) end = source.Length;
            if (end > MaximumHeaderCharacters) return empty;
            var line = source[start..end].TrimEnd('\r');
            if (line.TrimEnd(' ', '\t') == "---")
            {
                string Value(string key) => values.GetValueOrDefault(key, "");
                var orderText = Value("order");
                double? order = values.ContainsKey("order") && double.TryParse(orderText.Length == 0 ? "0" : orderText,
                    NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number) ? number : null;
                var draft = Value("draft");
                return new(new()
                {
                    Title = Text(Value("title")), Description = Text(Value("description")), NavigationGroup = Text(Value("navgroup")), Order = order,
                    Draft = draft.Length > 0 && !draft.Equals("false", StringComparison.OrdinalIgnoreCase)
                        && !(double.TryParse(draft, NumberStyles.Float, CultureInfo.InvariantCulture, out var numericDraft) && numericDraft == 0),
                    Tags = values.TryGetValue("tags", out var tags) ? Tags(tags) : []
                }, end < source.Length ? end + 1 : end);
            }
            var match = Entry().Match(line);
            if (match.Success)
            {
                var key = match.Groups[1].Value.ToLowerInvariant();
                if (key is "nav_group" or "nav-group") key = "navgroup";
                if (key is "title" or "description" or "navgroup" or "order" or "draft" or "tags") values[key] = Unquote(match.Groups[2].Value);
            }
            if (end == source.Length) break;
            start = end + 1;
        }
        return empty;
    }
    public static string HideHeader(string source, int bodyStart)
    {
        if (bodyStart == 0) return source;
        var blank = source[..bodyStart].Select(c => c is '\r' or '\n' ? c : ' ').ToArray();
        return new string(blank) + source[bodyStart..];
    }
    private static string Unquote(string value)
    {
        value = value.Trim();
        return value.Length >= 2 && value[0] is '\'' or '"' && value[^1] == value[0] ? value[1..^1].Trim() : value;
    }
    private static string Text(string value) => value.Equals("false", StringComparison.OrdinalIgnoreCase) || value == "0" ? "" : value;
    private static IReadOnlyList<string> Tags(string value) => (value.StartsWith('[') && value.EndsWith(']') ? value[1..^1].Split(',') : [value])
        .Select(v => Text(Unquote(v))).Where(v => v.Length > 0).ToArray();
    [GeneratedRegex(@"^([A-Za-z][A-Za-z0-9_-]*)\s*:\s*(.*?)\s*$", RegexOptions.CultureInvariant)] private static partial Regex Entry();
}
