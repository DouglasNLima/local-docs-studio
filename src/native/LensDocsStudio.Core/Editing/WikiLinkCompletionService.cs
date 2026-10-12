using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LensDocsStudio.Core.Editing;

public sealed record WikiLinkCompletionContext(int Start, int Length, string Prefix, int ClosingPosition, IReadOnlyList<string> Paths);
public static class WikiLinkCompletionService
{
    public static bool HasOpening(string source, int caret)
    {
        if (caret < 2 || caret > source.Length) return false;
        var lineStart = source.LastIndexOf('\n', caret - 1) + 1;
        return source.LastIndexOf("[[", caret - 1, caret - lineStart, StringComparison.Ordinal) >= lineStart;
    }
    public static WikiLinkCompletionContext? Query(string source, int caret, WikiLinkIndex catalogue, bool force = false)
    {
        if (caret < 0 || caret > source.Length) throw new ArgumentOutOfRangeException(nameof(caret));
        if (source.Length > (force ? MermaidCompletionService.ExplicitLimit : MermaidCompletionService.AutomaticLimit)) return null;
        var lineStart = caret == 0 ? 0 : source.LastIndexOf('\n', caret - 1) + 1;
        var opening = source.LastIndexOf("[[", Math.Max(0, caret - 1), Math.Max(0, caret - lineStart), StringComparison.Ordinal);
        if (opening < lineStart || opening + 2 > caret) return null;
        var before = source[(opening + 2)..caret];
        if (before.IndexOfAny(['[', ']', '|', '#', '\r']) >= 0 || before.Length > 4096 || !force && before.Length < 1) return null;
        var parsed = new MarkdownService().Parse(source);
        if (parsed.Syntax.Descendants().Any(node => node is FencedCodeBlock or CodeBlock or HtmlBlock or CodeInline or HtmlInline or LinkInline
            && node.Span.Start <= opening && SourceEnd(node) >= caret - 1)) return null;
        // An escaped opening bracket remains literal even while its closing brackets are incomplete.
        var escapes = 0; for (var index = opening - 1; index >= 0 && source[index] == '\\'; index--) escapes++;
        if (escapes % 2 != 0) return null;
        var end = caret;
        while (end < source.Length && source[end] is not ('\r' or '\n' or '|' or ']' or '#')) end++;
        var lineEnd = source.IndexOf('\n', end); if (lineEnd < 0) lineEnd = source.Length;
        var closing = source.IndexOf("]]", end, lineEnd - end, StringComparison.Ordinal);
        var prefix = before.Trim().Replace('\\', '/');
        var paths = catalogue.Paths.Where(path => prefix.Length == 0 || path.Contains(prefix, StringComparison.OrdinalIgnoreCase)).Take(80).ToArray();
        var start = opening + 2;
        while (start < caret && char.IsWhiteSpace(source[start])) start++;
        while (end > caret && char.IsWhiteSpace(source[end - 1])) end--;
        return paths.Length == 0 ? null : new(start, end - start, prefix, closing, paths);
    }
    public static MarkdownEdit? Plan(string source, int caret, WikiLinkIndex catalogue, string path)
    {
        var context = Query(source, caret, catalogue, true);
        if (context is null || !context.Paths.Contains(path, StringComparer.Ordinal)) return null;
        var value = EscapeTarget(path); var end = context.Start + context.Length;
        var addClose = context.ClosingPosition < 0 && (end == source.Length || source[end] is '\r' or '\n');
        if (addClose) value += "]]";
        var nextCaret = context.ClosingPosition >= 0 ? context.ClosingPosition + 2 + value.Length - context.Length : context.Start + value.Length;
        return new(context.Start, context.Length, value, nextCaret, 0);
    }
    public static string EscapeTarget(string path) => path.Replace("%", "%25", StringComparison.Ordinal)
        .Replace("[", "%5B", StringComparison.Ordinal).Replace("]", "%5D", StringComparison.Ordinal).Replace("#", "%23", StringComparison.Ordinal);
    private static int SourceEnd(MarkdownObject node)
    {
        // An unfinished fenced block can retain only its opening-line span.
        if (node is LeafBlock block && block.Lines.Count > 0)
        {
            var last = block.Lines.Lines[block.Lines.Count - 1];
            return Math.Max(node.Span.End, last.Position + last.Slice.Length - 1);
        }
        return node.Span.End;
    }
}
