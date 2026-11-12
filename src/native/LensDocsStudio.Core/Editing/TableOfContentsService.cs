using System.Text;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Review;

namespace LensDocsStudio.Core.Editing;

public enum TableOfContentsStyle { Bullets, Numbered, Plain }
public sealed record TableOfContentsOptions(int StartLevel = 1, int EndLevel = 6,
    TableOfContentsStyle Style = TableOfContentsStyle.Bullets, bool Normalise = true, bool Details = false);

/// <summary>Formats parser-owned targets in source order without reading assets or changing source.</summary>
public static class TableOfContentsService
{
    public const int MaximumOutputCharacters = 4 * DocumentReviewService.MaximumSourceCharacters;

    public static string Create(IReadOnlyList<DocumentHeading> headings, TableOfContentsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new();
        if (options.StartLevel is < 1 or > 6 || options.EndLevel is < 1 or > 6
            || options.StartLevel > options.EndLevel || !Enum.IsDefined(options.Style))
            throw new ArgumentException("Choose heading levels H1–H6, with the start level no greater than the end level.");
        var levels = new Stack<int>();
        var result = new StringBuilder();
        foreach (var heading in headings)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (heading.Level < options.StartLevel || heading.Level > options.EndLevel) continue;
            if (heading.Title.Length > DocumentReviewService.MaximumSourceCharacters
                || heading.Anchor.Length > DocumentReviewService.MaximumSourceCharacters)
                throw new IOException("The table of contents exceeds the supported document size.");
            while (levels.TryPeek(out var level) && level >= heading.Level) levels.Pop();
            var depth = options.Normalise ? levels.Count : heading.Level - 1;
            levels.Push(heading.Level);
            if (result.Length > 0) Append("\n");
            else if (options.Details) Append("<details>\n<summary>Table of Contents</summary>\n\n");
            Append(new string(' ', depth * (options.Style == TableOfContentsStyle.Numbered ? 3 : 2)));
            if (options.Style != TableOfContentsStyle.Plain) Append(options.Style == TableOfContentsStyle.Numbered ? "1. [" : "- [");
            var pendingSpace = false; var hasText = false;
            foreach (var character in heading.Title)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (char.IsWhiteSpace(character)) { pendingSpace = hasText; continue; }
                if (pendingSpace) { Append(" "); pendingSpace = false; }
                // All ASCII punctuation can be escaped in a Markdown link label. This also
                // prevents entities, images and extension syntax from altering its visible text.
                if (options.Style != TableOfContentsStyle.Plain && character is '(' or ')')
                    Append(character == '(' ? "&#40;" : "&#41;"); // Avoid the supported \(...\) maths delimiter.
                else
                {
                    if (options.Style != TableOfContentsStyle.Plain && char.IsAscii(character)
                        && (char.IsPunctuation(character) || char.IsSymbol(character))) Append("\\");
                    if (result.Length == MaximumOutputCharacters) throw new IOException("The generated table of contents is too large to copy. Select fewer heading levels.");
                    result.Append(character);
                }
                hasText = true;
            }
            if (options.Style != TableOfContentsStyle.Plain) Append("](#" + Uri.EscapeDataString(heading.Anchor) + ")");
        }
        if (result.Length > 0 && options.Details) Append("\n\n</details>");
        cancellationToken.ThrowIfCancellationRequested();
        return result.ToString();

        void Append(string value)
        {
            if (value.Length > MaximumOutputCharacters - result.Length)
                throw new IOException("The generated table of contents is too large to copy. Select fewer heading levels.");
            result.Append(value);
        }
    }
}
