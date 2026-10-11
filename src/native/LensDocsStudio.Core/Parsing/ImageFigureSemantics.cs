using System.Net;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LensDocsStudio.Core.Parsing;

public sealed class FigureImageInline : LinkInline;

public sealed class DocumentImageFigureBlock(FigureImageInline image, string caption) : LeafBlock(null)
{
    public FigureImageInline Image { get; } = image;
    public string Caption { get; } = caption;
}

/// <summary>Reads the passive image-figure helper, retaining exact original attribute positions.</summary>
internal static class ImageFigureSemantics
{
    private sealed record Attribute(string Value, SourceSpan Span);
    private sealed record Tag(SourceSpan Span, Dictionary<string, Attribute> Attributes);

    public static DocumentImageFigureBlock? Read(string source, HtmlBlock block)
    {
        if (block.Span.Start < 0 || block.Span.Length <= 0 || block.Span.Length > 32 * 1024 * 1024 || block.Span.End >= source.Length) return null;
        var position = block.Span.Start; var end = block.Span.End + 1;
        var figure = ReadTag("figure", false, false);
        if (figure is null || figure.Attributes.Count != 1 || !figure.Attributes.ContainsKey("data-image-figure")) return null;
        var image = ReadTag("img", false, true);
        if (image is null || image.Attributes.Keys.Any(name => name is not ("src" or "alt" or "title"))
            || !image.Attributes.TryGetValue("src", out var url) || url.Value.Length == 0 || url.Value.Any(char.IsControl)) return null;
        var alt = image.Attributes.GetValueOrDefault("alt")?.Value ?? "";
        var title = image.Attributes.GetValueOrDefault("title")?.Value;
        if (alt.Length > 4096 || title?.Length > 4096) return null;
        var caption = "";
        WhiteSpace();
        if (source.AsSpan(position, end - position).StartsWith("<figcaption", StringComparison.OrdinalIgnoreCase))
        {
            var captionTag = ReadTag("figcaption", false, false);
            if (captionTag is null || captionTag.Attributes.Count != 0) return null;
            var captionEnd = source.IndexOf('<', position, end - position);
            if (captionEnd < 0 || captionEnd - position > 32768) return null;
            caption = WebUtility.HtmlDecode(source[position..captionEnd]); position = captionEnd;
            if (caption.Length > 4096 || ReadTag("figcaption", true, false) is null) return null;
        }
        if (ReadTag("figure", true, false) is null) return null;
        WhiteSpace(); if (position != end) return null;
        var link = new FigureImageInline { IsImage = true, Url = url.Value, UrlSpan = url.Span, Span = image.Span,
            Title = title, Line = block.Line + source.AsSpan(block.Span.Start, image.Span.Start - block.Span.Start).Count('\n') };
        link.AppendChild(new LiteralInline(alt) { Span = new(-1, -1) });
        var result = new DocumentImageFigureBlock(link, caption) { Span = block.Span, Line = block.Line, Column = block.Column, Inline = new ContainerInline() };
        result.Inline.AppendChild(link);
        return result;

        void WhiteSpace() { while (position < end && source[position] is ' ' or '\t' or '\r' or '\n') position++; }
        Tag? ReadTag(string expected, bool closing, bool selfClosing)
        {
            WhiteSpace(); var start = position;
            if (position >= end || source[position++] != '<') return null;
            if (closing && (position >= end || source[position++] != '/')) return null;
            if (position + expected.Length > end || !source.AsSpan(position, expected.Length).Equals(expected, StringComparison.OrdinalIgnoreCase)) return null;
            position += expected.Length;
            var attributes = new Dictionary<string, Attribute>(StringComparer.Ordinal);
            while (position < end)
            {
                var beforeSpace = position; WhiteSpace();
                if (position >= end) return null;
                if (source[position] == '>') { position++; return new(new(start, position - 1), attributes); }
                if (selfClosing && position + 1 < end && source[position] == '/' && source[position + 1] == '>')
                { position += 2; return new(new(start, position - 1), attributes); }
                if (closing || position == beforeSpace || attributes.Count >= 3) return null;
                var nameStart = position;
                while (position < end && (char.IsAsciiLetter(source[position]) || source[position] == '-')) position++;
                if (position == nameStart || position - nameStart > 32) return null;
                var name = source[nameStart..position].ToLowerInvariant(); WhiteSpace();
                if (position >= end) return null;
                Attribute attribute;
                if (source[position] == '=')
                {
                    position++; WhiteSpace();
                    if (position >= end || source[position] is not ('\'' or '"')) return null;
                    var quote = source[position++]; var valueStart = position;
                    while (position < end && source[position] != quote)
                    { if (source[position] is '<' or '\0') return null; position++; }
                    if (position >= end) return null;
                    attribute = new(WebUtility.HtmlDecode(source[valueStart..position]), new(valueStart, position - 1)); position++;
                }
                else if (name == "data-image-figure") attribute = new("", new(position, position - 1));
                else return null;
                if (!attributes.TryAdd(name, attribute)) return null;
            }
            return null;
        }
    }
}
