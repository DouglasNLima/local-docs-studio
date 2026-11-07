using System.Text;
using System.Text.RegularExpressions;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LensDocsStudio.Core.Review;

public sealed record DocumentStatistics(int Words, int Characters)
{
    public int ReadingMinutes => (Words + 219) / 220;
    public string ReadingTime => ReadingMinutes < 60 ? $"{ReadingMinutes} min read"
        : $"{ReadingMinutes / 60} hr {ReadingMinutes % 60} min read";
    public string Summary => $"{ReadingTime} · {Words:N0} words · {Characters:N0} chars";
}

/// <summary>Preview-independent, bounded analysis of the current source. Never accesses linked assets.</summary>
public static partial class DocumentStatisticsService
{
    public static DocumentStatistics Measure(string source, bool standaloneMermaid = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (source.Length > DocumentReviewService.MaximumSourceCharacters)
            throw new IOException("Document statistics support up to 4 million source characters.");
        return MeasureParsed(new MarkdownService().Parse(source, standaloneMermaid), cancellationToken);
    }

    public static DocumentStatistics MeasureParsed(ParsedDocument parsed, CancellationToken cancellationToken = default)
    {
        var text = new StringBuilder();
        Walk(parsed.Syntax, 0);
        var readable = Whitespace().Replace(text.ToString(), " ").Trim();
        var words = CountWords(readable, cancellationToken);
        var characters = 0;
        foreach (var rune in readable.EnumerateRunes()) { cancellationToken.ThrowIfCancellationRequested(); characters++; }
        return new(words, characters);

        void Walk(ContainerBlock container, int depth)
        {
            Check(depth);
            foreach (var block in container)
            {
                Check(depth);
                if (block is CodeBlock) continue;
                if (block is DocumentImageFigureBlock figure) text.Append(figure.Caption).Append(' ');
                if (block is DocumentDetailsBlock details) text.Append(details.Summary).Append(' ');
                if (block is LeafBlock { Inline: { } inline }) { Inlines(inline, depth + 1); text.Append(' '); }
                if (block is ContainerBlock nested) Walk(nested, depth + 1);
            }
        }
        void Inlines(ContainerInline container, int depth)
        {
            Check(depth);
            foreach (var inline in container)
            {
                Check(depth);
                switch (inline)
                {
                    case WikiLinkInline wiki: text.Append(wiki.Label); break;
                    case AutolinkInline link: text.Append(link.Url); break;
                    case CodeInline: text.Append(' '); break;
                    case LiteralInline literal: text.Append(literal.Content.ToString()); break;
                    case HtmlEntityInline entity: text.Append(entity.Transcoded.ToString()); break;
                    case DocumentSemanticInline semantic: text.Append(semantic.Text); break;
                    case DocumentAnchorInline anchor: text.Append(anchor.Text); break;
                    case LineBreakInline: text.Append(' '); break;
                    case ContainerInline nested: Inlines(nested, depth + 1); break;
                }
            }
        }
        void Check(int depth)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (depth > 64) throw new IOException("The document contains excessively nested statistics content.");
        }
    }

    // Equivalent to the Review word expression, evaluated on Unicode scalars so
    // supplementary letters/numbers have the same meaning as JavaScript's /u mode.
    internal static int CountWords(string text, CancellationToken cancellationToken = default)
    {
        var words = 0; var inWord = false; var connector = false; var index = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if ((index++ & 4095) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (Rune.IsLetter(rune) || Rune.IsNumber(rune))
            { if (!inWord) words++; inWord = true; connector = false; }
            else if (rune.Value is '\'' or '-' && inWord && !connector) connector = true;
            else { inWord = false; connector = false; }
        }
        return words;
    }
    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, 100)] private static partial Regex Whitespace();
}
