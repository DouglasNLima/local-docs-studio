using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LensDocsStudio.Core.Parsing;

public sealed class DocumentAnchorInline(string id, string text) : LeafInline
{
    public string Id { get; internal set; } = id;
    public string Text { get; } = text;
}

public sealed record DocumentTarget(string Id, string Title, int SourceStart, int SourceLine, int HeadingLevel = 0);

public static class DocumentAnchors
{
    public static bool IsValidId(string value) => value.Length is > 0 and <= 128
        && (char.IsLetterOrDigit(value[0]) || value[0] == '_')
        && value.All(c => char.IsLetterOrDigit(c) || c is '_' or '-' or '.' or ':');

    internal static IReadOnlyList<DocumentTarget> Collect(MarkdownDocument syntax)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        var nextSuffix = new Dictionary<string, int>(StringComparer.Ordinal);
        var targets = new List<DocumentTarget>();
        var nodes = syntax.Descendants().Where(n => n is HeadingBlock or DocumentAnchorInline).OrderBy(n => n.Span.Start).ToArray();
        string OriginalId(MarkdownObject node) => node is DocumentAnchorInline anchor ? anchor.Id : node.GetAttributes().Id is { Length: > 0 } id ? id : "section";
        var declared = nodes.Select(OriginalId).ToHashSet(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            var original = OriginalId(node); var id = original;
            if (!used.Add(id))
            {
                var suffix = nextSuffix.GetValueOrDefault(original, 2);
                do
                {
                    var ending = "-" + suffix++;
                    var stem = node is DocumentAnchorInline ? original[..Math.Min(original.Length, 128 - ending.Length)] : original;
                    id = stem + ending;
                } while (declared.Contains(id) || !used.Add(id));
                nextSuffix[original] = suffix;
            }
            if (node is DocumentAnchorInline named)
            {
                named.Id = id;
                targets.Add(new(id, named.Text.Length == 0 ? id : named.Text, named.Span.Start, named.Line));
            }
            else if (node is HeadingBlock heading)
            {
                heading.GetAttributes().Id = id;
                targets.Add(new(id, MarkdownService.InlineText(heading.Inline), heading.Span.Start, heading.Line, heading.Level));
            }
        }
        return targets;
    }

    public static DocumentTarget? Find(ParsedDocument document, string fragment) => document.Targets.FirstOrDefault(t => t.Id == fragment);
}
