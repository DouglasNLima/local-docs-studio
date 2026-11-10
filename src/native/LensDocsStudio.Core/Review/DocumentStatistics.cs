using System.Globalization;
using System.Text;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LensDocsStudio.Core.Review;

public sealed record DocumentStatistics(int Words, int Characters)
{
    public int ReadingMinutes => Words / 220 + (Words % 220 == 0 ? 0 : 1);
    public string ReadingTime => ReadingMinutes < 60 ? $"{ReadingMinutes} min read"
        : $"{ReadingMinutes / 60} hr {ReadingMinutes % 60} min read";
    public string Summary => $"{Words:N0} words · {Characters:N0} chars · {ReadingTime}";
}

/// <summary>Readable Markdown text only; no rendering, linked asset access or source changes.</summary>
public static class DocumentStatisticsService
{
    public static DocumentStatistics Measure(string source, bool standaloneMermaid = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (source.Length > DocumentReviewService.MaximumSourceCharacters)
            throw new IOException("Document statistics support up to 4 million source characters.");
        // Parsing is synchronous and bounded by source size. Callers run it off the UI thread;
        // cancellation is observed before parsing and throughout the subsequent traversal.
        return MeasureParsed(new MarkdownService().Parse(source, standaloneMermaid), cancellationToken);
    }

    internal static DocumentStatistics MeasureParsed(ParsedDocument parsed, CancellationToken cancellationToken)
    {
        var words = 0; var characters = 0; var inWord = false; var connector = false; var space = false;
        Walk(parsed.Syntax, 0);
        cancellationToken.ThrowIfCancellationRequested();
        return new(words, characters);

        void Append(string text)
        {
            var index = 0;
            foreach (var rune in text.EnumerateRunes())
            {
                if ((index++ & 4095) == 0) cancellationToken.ThrowIfCancellationRequested();
                if (Rune.IsWhiteSpace(rune))
                { space = characters != 0; inWord = connector = false; continue; }
                if (space) { characters++; space = false; }
                characters++;
                if (Rune.IsLetterOrDigit(rune) || Rune.IsNumber(rune))
                { if (!inWord) words++; inWord = true; connector = false; }
                else if (inWord && !connector && Rune.GetUnicodeCategory(rune) is
                    UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark) { }
                else if (rune.Value is '\'' or '-' or 0x2019 && inWord && !connector) connector = true;
                else { inWord = connector = false; }
            }
        }
        void Walk(ContainerBlock container, int depth)
        {
            Check(depth);
            foreach (var block in container)
            {
                Check(depth);
                if (block is CodeBlock) continue;
                if (block is DocumentImageFigureBlock figure) { Append(figure.Caption); Append(" "); }
                if (block is DocumentDetailsBlock details) { Append(details.Summary); Append(" "); }
                if (block is LeafBlock { Inline: { } inline }) { Inlines(inline, depth + 1); Append(" "); }
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
                    case WikiLinkInline wiki: Append(wiki.Label); break;
                    case AutolinkInline link: Append(link.Url); break;
                    case CodeInline: Append(" "); break;
                    case LiteralInline literal: Append(literal.Content.ToString()); break;
                    case HtmlEntityInline entity: Append(entity.Transcoded.ToString()); break;
                    case DocumentSemanticInline semantic: Append(semantic.Text); break;
                    case DocumentAnchorInline anchor: Append(anchor.Text); break;
                    case LineBreakInline: Append(" "); break;
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
}
