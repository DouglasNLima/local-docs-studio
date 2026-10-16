using Markdig.Syntax.Inlines;

namespace LensDocsStudio.Core.Parsing;

public static class MarkdownImageSource
{
    // Change only the destination for inline images; expand a reference image at its own occurrence.
    public static string WithDestination(string source, LinkInline image, string destination)
    {
        var span = image.Span;
        if (span.Start < 0 || span.Length <= 0 || span.End >= source.Length)
            throw new IOException("An image has invalid Markdown source positions.");
        var url = image.UrlSpan;
        if (image is FigureImageInline)
        {
            if (url.Start < span.Start || url.Length <= 0 || url.End > span.End) throw new IOException("A figure image has invalid source positions.");
            return source[span.Start..url.Start] + System.Net.WebUtility.HtmlEncode(destination) + source[(url.End + 1)..(span.End + 1)];
        }
        if (image.Reference is null && url.Length > 0 && url.Start >= span.Start && url.End <= span.End)
        {
            if (source[url.Start] == '<' && source[url.End] == '>') destination = "<" + destination + ">";
            return source[span.Start..url.Start] + destination + source[(url.End + 1)..(span.End + 1)];
        }
        var label = image.LabelSpan;
        var altEnd = span.End;
        // LabelSpan identifies the reference label, which can differ from the visible image text.
        if (label.Start > span.Start + 2 && label.End <= span.End && source[label.Start - 1] == '[')
        {
            var candidate = label.Start - 2;
            while (candidate > span.Start && char.IsWhiteSpace(source[candidate])) candidate--;
            if (source[candidate] == ']') altEnd = candidate;
        }
        if (altEnd == span.End && span.Length >= 4 && source[(span.End - 1)..(span.End + 1)] == "[]") altEnd -= 2;
        var alt = source.AsSpan(span.Start, Math.Min(2, span.Length)).SequenceEqual("![") && source[altEnd] == ']'
            ? source[(span.Start + 2)..altEnd] : Escape(MarkdownService.InlineText(image));
        var title = string.IsNullOrEmpty(image.Title) ? "" : " \"" + image.Title.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        return "![" + alt + "](" + destination + title + ")";
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("[", "\\[").Replace("]", "\\]");
}
