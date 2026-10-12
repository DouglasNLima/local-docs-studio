using System.Net;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;

namespace LensDocsStudio.Core.Editing;

public sealed record DetailsOptions(string Summary, string Body, bool InitiallyExpanded = false);

public static class DetailsEditService
{
    public const int MaximumBodyCharacters = 1_000_000;
    public static DocumentDetailsBlock? Find(ParsedDocument document, int start, int length)
        => document.Syntax.Descendants<DocumentDetailsBlock>().Where(block => start >= block.Span.Start && (long)start + length <= block.Span.End + 1)
            .OrderBy(block => block.Span.Length).FirstOrDefault();
    public static DetailsOptions ReadOptions(ParsedDocument document, int start, int length)
    {
        ValidateRange(document.Source, start, length);
        return Find(document, start, length) is { } details ? new(details.Summary, Body(document, details), details.InitiallyExpanded)
            : new("More details", length == 0 ? "Add supporting details here." : document.Source.Substring(start, length));
    }
    private static string Body(ParsedDocument document, DocumentDetailsBlock details)
        => document.Source[details.BodyStart..details.ClosingStart].Trim('\r', '\n');

    public static MarkdownEdit Plan(ParsedDocument document, int start, int length, DetailsOptions options)
    {
        var source = document.Source; ValidateRange(source, start, length);
        var summary = options.Summary.Trim();
        if (summary.Length is 0 or > 512 || summary.Any(char.IsControl)) throw new ArgumentException("Enter a summary of 1–512 characters on one line.", nameof(options));
        if (options.Body.Length > MaximumBodyCharacters || options.Body.Any(character => char.IsControl(character) && character is not ('\r' or '\n' or '\t')))
            throw new ArgumentException("Use up to one million characters of Markdown without control characters in the details body.", nameof(options));
        var newline = source.IndexOf('\n') is var first && first > 0 && source[first - 1] == '\r' ? "\r\n" : "\n";
        var existing = Find(document, start, length);
        var body = existing is not null && Normalise(options.Body).Trim('\n') == Normalise(Body(document, existing))
            ? source[existing.BodyStart..existing.ClosingStart]
            : newline + Normalise(options.Body).Trim('\n').Replace("\n", newline) + newline;
        var value = "<details data-details-block" + (options.InitiallyExpanded ? " open" : "") + ">" + newline
            + "  <summary>" + WebUtility.HtmlEncode(summary) + "</summary>" + newline + body + "</details>";
        var parsed = new MarkdownService().Parse(value);
        var visible = parsed.Syntax.Where(block => block is not LinkReferenceDefinitionGroup).ToArray();
        if (visible.Length != 1 || visible[0] is not DocumentDetailsBlock complete || complete.Span.End != value.Length - 1)
            throw new ArgumentException("Complete any code fences or nested details tags in the body before applying the section.", nameof(options));
        return existing is null ? MarkdownEditService.PlanBlock(source, start, length, value, value)
            : new(existing.Span.Start, existing.Span.Length, value, existing.Span.Start, value.Length);
    }
    private static string Normalise(string value) => value.Replace("\r\n", "\n").Replace('\r', '\n');
    private static void ValidateRange(string source, int start, int length)
    { if (start < 0 || length < 0 || (long)start + length > source.Length) throw new ArgumentOutOfRangeException(nameof(start)); }
}
