using System.Text;
using System.Text.RegularExpressions;

namespace LensDocsStudio.Core.Rendering;

/// <summary>Adapts standard LaTeX environments to the native engine without altering stored document source.</summary>
public static partial class MathFormulaSource
{
    private static readonly IReadOnlyDictionary<string, (string Open, string Close)> Environments = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
    {
        ["matrix"] = (@"\matrix{", "}"), ["pmatrix"] = (@"\left(\matrix{", @"}\right)"),
        ["bmatrix"] = (@"\left[\matrix{", @"}\right]"), ["Bmatrix"] = (@"\left\{\matrix{", @"}\right\}"),
        ["vmatrix"] = (@"\left|\matrix{", @"}\right|"), ["Vmatrix"] = (@"\left\Vert\matrix{", @"}\right\Vert"),
        ["cases"] = (@"\cases{", "}"), ["aligned"] = (@"\begin{align}", @"\end{align}"),
        ["align*"] = (@"\begin{align}", @"\end{align}"), ["equation"] = ("{", "}"), ["equation*"] = ("{", "}")
    };
    public static string Prepare(string source)
    {
        if (source.Length > 4096) throw new ArgumentException("The formula exceeds the native character limit.", nameof(source));
        var result = new StringBuilder(); var stack = new Stack<string>(); var suffixes = new Dictionary<int, string>();
        for (var position = 0; position < source.Length; position++)
        {
            if (source[position] != '\\')
            { result.Append(source[position]); if (suffixes.TryGetValue(position, out var suffix)) result.Append(suffix); continue; }
            if (position + 1 < source.Length && source[position + 1] == '\\') { result.Append(@"\\"); position++; continue; }
            if (source.AsSpan(position).StartsWith(@"\pmatrix", StringComparison.Ordinal))
            {
                var opening = position + 8; while (opening < source.Length && char.IsWhiteSpace(source[opening])) opening++;
                if (opening < source.Length && source[opening] == '{')
                {
                    var depth = 1; var closing = opening + 1;
                    for (; closing < source.Length; closing++)
                    {
                        if (source[closing] == '\\' && closing + 1 < source.Length) { closing++; continue; }
                        if (source[closing] == '{') depth++;
                        if (source[closing] == '}' && --depth == 0) break;
                    }
                    if (closing == source.Length) throw new ArgumentException("The formula has an unclosed matrix.", nameof(source));
                    suffixes.Add(closing, @"\right)"); result.Append(@"\left(\matrix"); position += 7; continue;
                }
            }
            var match = EnvironmentToken().Match(source, position);
            if (!match.Success || match.Index != position || !Environments.TryGetValue(match.Groups[2].Value, out var replacement))
            { result.Append(source[position]); continue; }
            if (match.Groups[1].Value == "begin") { stack.Push(match.Groups[2].Value); result.Append(replacement.Open); }
            else
            {
                if (!stack.TryPop(out var opening) || opening != match.Groups[2].Value) throw new ArgumentException("The formula has mismatched environment delimiters.", nameof(source));
                result.Append(replacement.Close);
            }
            position += match.Length - 1;
        }
        if (stack.Count != 0) throw new ArgumentException("The formula has an unclosed environment.", nameof(source));
        return result.ToString();
    }
    [GeneratedRegex(@"\\(begin|end)\{([A-Za-z*]+)\}", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex EnvironmentToken();
}
