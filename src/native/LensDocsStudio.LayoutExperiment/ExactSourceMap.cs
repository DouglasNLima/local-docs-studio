using System.Net;
using System.Text;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;

namespace LensDocsStudio.LayoutExperiment;

internal static class ExactSourceMap
{
    public static (int Start, int End)[] Inline(string source, int start, int end, string rendered, bool code, bool atomic = false)
    {
        if (rendered.Length == 0) return [];
        if (atomic) return Enumerable.Repeat((start, end), rendered.Length).ToArray();
        if (LiteralSourceMapping.Locate(source, start, end - 1, rendered) is { } exact)
            return Enumerable.Range(0, rendered.Length).Select(i => (exact.Start + i, exact.Start + i + 1)).ToArray();
        var text = new StringBuilder(); var map = new List<(int, int)>();
        var left = start; var right = end;
        if (code)
        {
            var fence = 0; while (left + fence < right && source[left + fence] == '`') fence++;
            if (fence > 0 && right - fence >= left + fence && source.AsSpan(right - fence, fence).IndexOfAnyExcept('`') < 0) { left += fence; right -= fence; }
        }
        for (var at = left; at < right;)
        {
            var begin = at; var value = source[at++].ToString();
            if (!code && value == "\\" && at < right && IsPunctuation(source[at])) value = source[at++].ToString();
            else if (!code && value == "&")
            {
                var semicolon = source.IndexOf(';', at, Math.Min(32, right - at));
                if (semicolon >= 0)
                {
                    var entity = source[begin..(semicolon + 1)]; var decoded = WebUtility.HtmlDecode(entity);
                    if (decoded != entity) { value = decoded; at = semicolon + 1; }
                }
            }
            else if (value is "\r" or "\n")
            {
                if (value == "\r" && at < right && source[at] == '\n') at++;
                value = " ";
            }
            text.Append(value); for (var i = 0; i < value.Length; i++) map.Add((begin, at));
        }
        if (code && text.Length > 1 && text[0] == ' ' && text[^1] == ' ' && text.ToString().Any(character => character != ' '))
        { text.Remove(text.Length - 1, 1); text.Remove(0, 1); map.RemoveAt(map.Count - 1); map.RemoveAt(0); }
        if (text.ToString() != rendered) throw new NotSupportedException($"No exact transformed-source mapping for [{start},{end}).");
        return map.ToArray();
    }
    private static bool IsPunctuation(char value) => value is >= '!' and <= '/' or >= ':' and <= '@' or >= '[' and <= '`' or >= '{' and <= '~';
    public static (int Start, int End)[] Code(CodeBlock code, string content)
    {
        var text = new StringBuilder(); var map = new List<(int, int)>();
        for (var line = 0; line < code.Lines.Count; line++)
        {
            var slice = code.Lines.Lines[line].Slice; var value = slice.ToString();
            if (line > 0)
            { var previous = code.Lines.Lines[line - 1].Slice; text.Append('\n'); map.Add((previous.End + 1, slice.Start)); }
            text.Append(value); for (var index = 0; index < value.Length; index++) map.Add((slice.Start + index, slice.Start + index + 1));
        }
        if (text.ToString() != content) throw new NotSupportedException("Code-line/source mapping differs from parser content.");
        return map.ToArray();
    }
}
