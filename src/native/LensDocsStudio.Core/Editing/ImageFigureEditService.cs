using System.Net;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LensDocsStudio.Core.Editing;

public sealed record ImageFigureOptions(string AlternativeText, string Caption);
public sealed record EditableImageFigure(int Start, int Length, string Reference, string? Title, ImageFigureOptions Options);

public static class ImageFigureEditService
{
    public static EditableImageFigure? Find(ParsedDocument document, int start, int length)
    {
        if (start < 0 || length < 0 || (long)start + length > document.Source.Length) throw new ArgumentOutOfRangeException(nameof(start));
        foreach (var block in document.Syntax)
        {
            if (start < block.Span.Start || (long)start + length > block.Span.End + 1) continue;
            if (block is DocumentImageFigureBlock figure)
                return new(block.Span.Start, block.Span.Length, figure.Image.Url ?? "", figure.Image.Title, new(MarkdownService.InlineText(figure.Image), figure.Caption));
            if (block is ParagraphBlock { Inline: { } inline } && inline.FirstChild is LinkInline { IsImage: true, NextSibling: null } image
                && !string.IsNullOrWhiteSpace(image.Url))
                return new(block.Span.Start, block.Span.Length, image.Url, image.Title, new(MarkdownService.InlineText(image), ""));
        }
        return null;
    }

    public static MarkdownEdit Plan(ParsedDocument document, int start, int length, ImageFigureOptions options)
    {
        var image = Find(document, start, length) ?? throw new ArgumentException("Place the caret inside a standalone image or an existing image figure.");
        if (image.Reference.Any(char.IsControl) || image.Title?.Length > 4096)
            throw new ArgumentException("The existing image destination or title cannot be represented safely as a figure. Edit its Markdown first.");
        var alternative = Clean(options.AlternativeText); var caption = Clean(options.Caption);
        var newline = document.Source.IndexOf('\n') is var first && first > 0 && document.Source[first - 1] == '\r' ? "\r\n" : "\n";
        var value = "<figure data-image-figure>" + newline + "  <img src=\"" + WebUtility.HtmlEncode(image.Reference) + "\" alt=\"" + WebUtility.HtmlEncode(alternative) + "\""
            + (string.IsNullOrEmpty(image.Title) ? "" : " title=\"" + WebUtility.HtmlEncode(image.Title) + "\"") + ">" + newline
            + (caption.Length == 0 ? "" : "  <figcaption>" + WebUtility.HtmlEncode(caption) + "</figcaption>" + newline) + "</figure>";
        return new(image.Start, image.Length, value, image.Start, value.Length);
    }

    private static string Clean(string text)
    {
        if (text.Length > 4096 || text.Any(character => char.IsControl(character) && !char.IsWhiteSpace(character)))
            throw new ArgumentException("Use up to 4,096 characters of ordinary text for the image description and caption.");
        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
