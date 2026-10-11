using System.Text.RegularExpressions;
using System.Text;
using AngleSharp.Html.Parser;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LensDocsStudio.Core.Parsing;

public enum DocumentInlineKind { StatusBadge, Shortcut, KeyboardKey }
public sealed class DocumentSemanticInline(DocumentInlineKind kind, string text, string variant = "") : LeafInline
{
    public DocumentInlineKind Kind { get; } = kind;
    public string Text { get; } = text;
    public string Variant { get; } = variant;
}

/// <summary>Maps recognised passive snippets to typed text; HTML attributes never reach native controls.</summary>
public static partial class SafeInlineSemantics
{
    public static void Apply(MarkdownDocument document, string source)
    {
        foreach (var block in document.Descendants<LeafBlock>()) if (block.Inline is { } inline) Convert(inline);
        void Convert(ContainerInline container)
        {
            var current = container.FirstChild;
            while (current is not null)
            {
                if (current is HtmlInline opening && TryRead(opening, source, out var semantic, out var last))
                {
                    var following = last!.NextSibling; var remove = opening.NextSibling;
                    opening.ReplaceBy(semantic!, false);
                    while (remove != following) { var next = remove!.NextSibling; remove.Remove(); remove = next; }
                    current = following; continue;
                }
                if (current is ContainerInline nested) Convert(nested);
                current = current.NextSibling;
            }
        }
    }

    private static bool TryRead(HtmlInline opening, string source, out DocumentSemanticInline? semantic, out Inline? last)
    {
        semantic = null; last = null;
        var first = Tag().Match(opening.Tag);
        if (!first.Success || first.Groups[1].Value.Length != 0) return false;
        var tag = first.Groups[2].Value.ToLowerInvariant();
        if (tag == "span" && !opening.Tag.Contains("data-status-badge", StringComparison.OrdinalIgnoreCase)
            && !opening.Tag.Contains("data-shortcut", StringComparison.OrdinalIgnoreCase)) return false;
        var depth = 1; var count = 0;
        for (var cursor = opening.NextSibling; cursor is not null; cursor = cursor.NextSibling)
        {
            if (++count > 256 || cursor.Span.End - opening.Span.Start > 16384) return false;
            if (cursor is not HtmlInline html) continue;
            var match = Tag().Match(html.Tag);
            if (!match.Success || !match.Groups[2].Value.Equals(tag, StringComparison.OrdinalIgnoreCase)) continue;
            depth += match.Groups[1].Value.Length == 0 ? 1 : -1;
            if (depth != 0) continue;
            var start = opening.Span.Start; var end = cursor.Span.End;
            if (start < 0 || end >= source.Length || end < start) return false;
            var parsed = new HtmlParser().ParseDocument(source.Substring(start, end - start + 1));
            var body = parsed.Body!;
            if (body.Children.Length != 1) return false;
            var element = body.Children[0];
            if (element.LocalName != tag || element.TextContent.Length is 0 or > 512 || element.TextContent.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t'))) return false;
            DocumentInlineKind kind; var variant = "";
            if (tag == "span" && element.HasAttribute("data-status-badge"))
            {
                if (element.Children.Length != 0) return false;
                kind = DocumentInlineKind.StatusBadge;
                variant = (element.GetAttribute("data-status-kind") ?? "custom").ToLowerInvariant();
                if (variant is not ("draft" or "ready" or "blocked" or "deprecated")) variant = "custom";
            }
            else if (tag == "span" && element.HasAttribute("data-shortcut"))
            {
                var keys = element.QuerySelectorAll("kbd");
                if (keys.Length is 0 or > 8 || element.QuerySelectorAll("*").Length != keys.Length || keys.Any(k => k.Children.Length != 0 || k.TextContent.Length is 0 or > 64)) return false;
                kind = DocumentInlineKind.Shortcut;
            }
            else if (tag == "kbd" && element.Children.Length == 0 && element.TextContent.Length <= 64) kind = DocumentInlineKind.KeyboardKey;
            else return false;
            // Keep Markdig's decoded visible text, including escaped punctuation and emphasis.
            // Links, images and unknown inline extensions must retain their original syntax tree.
            if (!TryVisibleText(opening.NextSibling, cursor, kind == DocumentInlineKind.Shortcut, out var text)) return false;
            semantic = new(kind, text, variant) { Span = new(start, end), Line = opening.Line, Column = opening.Column };
            last = cursor; return true;
        }
        return false;
    }
    private static bool TryVisibleText(Inline? first, Inline? end, bool allowKeys, out string text)
    {
        var result = new StringBuilder(); var nodes = 0;
        var success = Append(first, end, 0); text = result.ToString();
        return success && text.Length > 0 && !text.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t'));
        bool Append(Inline? start, Inline? stop, int depth)
        {
            if (depth > 32) return false;
            for (var current = start; current != stop; current = current?.NextSibling)
            {
                if (current is null || ++nodes > 256) return false;
                switch (current)
                {
                    case LiteralInline literal: result.Append(literal.Content.ToString()); break;
                    case HtmlEntityInline entity: result.Append(entity.Transcoded.ToString()); break;
                    case CodeInline code: result.Append(code.Content); break;
                    case LineBreakInline: result.Append(' '); break;
                    case EmphasisInline emphasis when Append(emphasis.FirstChild, null, depth + 1): break;
                    case HtmlInline html when allowKeys && Tag().Match(html.Tag) is { Success: true } match
                        && match.Groups[2].Value.Equals("kbd", StringComparison.OrdinalIgnoreCase): break;
                    default: return false;
                }
                if (result.Length > 512) return false;
            }
            return true;
        }
    }
    [GeneratedRegex(@"^<\s*(/?)\s*(span|kbd)(?=[\s/>])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex Tag();
}
