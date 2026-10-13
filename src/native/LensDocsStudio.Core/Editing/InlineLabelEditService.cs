using System.Net;
using System.Text;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;

namespace LensDocsStudio.Core.Editing;

/// <summary>Builds passive, portable labels without interpreting their user-entered text as markup.</summary>
public static class InlineLabelEditService
{
    public static string StatusLabel(string kind) => kind switch
    {
        "draft" => "Draft", "ready" => "Ready", "blocked" => "Blocked", "deprecated" => "Deprecated",
        "beta" => "Beta", "review" => "In review", _ => throw new ArgumentException("Choose a supported status kind.")
    };

    public static DocumentSemanticInline? Find(ParsedDocument document, int start, int length, DocumentInlineKind kind)
        => document.Syntax.Descendants<DocumentSemanticInline>().FirstOrDefault(item => item.Kind == kind
            && start >= item.Span.Start && (long)start + length <= item.Span.End + 1);

    public static MarkdownEdit Badge(ParsedDocument document, int start, int length, string kind, string label)
    {
        _ = StatusLabel(kind); ValidateLabel(label, 512);
        var snippet = "<span data-status-badge data-status-kind=\"" + kind + "\" aria-label=\"Status: "
            + WebUtility.HtmlEncode(label) + "\">" + Literal(label) + "</span>";
        return Replace(document, start, length, DocumentInlineKind.StatusBadge, snippet);
    }

    public static MarkdownEdit Shortcut(ParsedDocument document, int start, int length, string keys)
    {
        ValidateLabel(keys, 512);
        var parts = keys.Split(['+', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is 0 or > 8) throw new ArgumentException("Enter between one and eight keys, separated by + or commas.");
        foreach (var part in parts) ValidateLabel(part, 64);
        var text = string.Join(" + ", parts);
        ValidateLabel(text, 512);
        var snippet = "<span data-shortcut aria-label=\"Keyboard shortcut " + WebUtility.HtmlEncode(string.Join(' ', parts)) + "\">"
            + string.Join(" + ", parts.Select(part => "<kbd>" + Literal(part) + "</kbd>")) + "</span>";
        return Replace(document, start, length, DocumentInlineKind.Shortcut, snippet);
    }

    private static MarkdownEdit Replace(ParsedDocument document, int start, int length, DocumentInlineKind kind, string snippet)
    {
        if (start < 0 || length < 0 || (long)start + length > document.Source.Length) throw new ArgumentOutOfRangeException(nameof(start));
        var existing = Find(document, start, length, kind);
        start = existing?.Span.Start ?? start; length = existing?.Span.Length ?? length;
        // A non-table line immediately after a pipe table can invalidate that paragraph's table.
        // At this boundary start a separate paragraph, keeping in-cell edits inline.
        if (existing is null && length == 0 && document.Syntax.Descendants<Markdig.Extensions.Tables.Table>().Any(table =>
            start - table.Span.End is 2 or 3 && document.Source[(table.Span.End + 1)..start] is "\n" or "\r\n"))
            return MarkdownEditService.PlanBlock(document.Source, start, length, snippet, snippet);
        return new(start, length, snippet, start, snippet.Length);
    }

    private static void ValidateLabel(string label, int limit)
    {
        if (string.IsNullOrWhiteSpace(label) || label.Length > limit || label.Any(char.IsControl))
            throw new ArgumentException($"Enter a single line of between 1 and {limit} characters.");
    }

    private static string Literal(string value)
    {
        var result = new StringBuilder();
        foreach (var c in WebUtility.HtmlEncode(value))
        {
            // Character references keep both Markdown and HTML readers on the same literal length.
            if ("\\`*_[]!~^=+$|".Contains(c)) result.Append("&#").Append((int)c).Append(';');
            else result.Append(c);
        }
        return result.ToString();
    }
}
