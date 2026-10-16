using System.Net;
using System.Text;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;

namespace LensDocsStudio.Core.Editing;

public sealed record AnchorOptions(string Id = "section-anchor", string Label = "");

public static class AnchorEditService
{
    public static DocumentAnchorInline? Find(ParsedDocument document, int start, int length) => document.Syntax.Descendants<DocumentAnchorInline>()
        .FirstOrDefault(a => start >= a.Span.Start && (long)start + length <= a.Span.End + 1);

    public static MarkdownEdit Plan(ParsedDocument document, int start, int length, AnchorOptions options)
    {
        if (start < 0 || length < 0 || (long)start + length > document.Source.Length) throw new ArgumentOutOfRangeException(nameof(start));
        if (!DocumentAnchors.IsValidId(options.Id)) throw new ArgumentException("Use up to 128 letters, numbers, underscores, hyphens, dots or colons; start with a letter, number or underscore.", nameof(options));
        if (options.Label.Length > 512 || options.Label.Any(char.IsControl)) throw new ArgumentException("Use a single-line label of up to 512 characters, or leave it empty.", nameof(options));
        var existing = Find(document, start, length);
        if (existing is not null && existing.Id != options.Id) throw new ArgumentException("Keep the existing identifier so links to this anchor remain valid.", nameof(options));
        if (document.Targets.Any(t => t.Id == options.Id && (existing is null || t.SourceStart != existing.Span.Start)))
            throw new ArgumentException("This identifier is already used by a heading or anchor. Choose another identifier.", nameof(options));
        var label = new StringBuilder();
        foreach (var c in WebUtility.HtmlEncode(options.Label))
        {
            if ("\\`*_[]!~^=+$|".Contains(c)) label.Append('\\');
            label.Append(c);
        }
        var snippet = "<span id=\"" + WebUtility.HtmlEncode(options.Id) + "\" data-doc-anchor>" + label + "</span>";
        start = existing?.Span.Start ?? start; length = existing?.Span.Length ?? length;
        return new(start, length, snippet, start, snippet.Length);
    }
}
