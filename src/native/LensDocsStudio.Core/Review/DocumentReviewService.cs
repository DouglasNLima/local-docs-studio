using System.Text.RegularExpressions;
using LensDocsStudio.Core.Parsing;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LensDocsStudio.Core.Review;

public enum ReviewSeverity { Warning, Note }
public sealed record DocumentReviewIssue(string Rule, ReviewSeverity Severity, string Message, string Suggestion, int Start, int Length, int Line, int Column)
{
    public string Location => $"Line {Line}, column {Column}";
    public string AccessibleName => $"{Severity}: {Message} {Location}";
}
public sealed record DocumentReviewMetrics(int Words, int Headings, int Links, int Images, int Tables, int CodeBlocks, int Diagrams)
{
    public int ReadingMinutes => Words == 0 ? 0 : Math.Max(1, (Words + 219) / 220);
}
public enum WorkspaceLinkReviewState { NotChecked, Complete, Incomplete }
public sealed record DocumentReviewResult(DocumentReviewMetrics Metrics, IReadOnlyList<DocumentReviewIssue> Issues, bool Limited)
{
    public WorkspaceLinkReviewState WorkspaceLinks { get; init; }
}

/// <summary>Advisory checks of the current Markdown buffer; never reads links or changes source.</summary>
public sealed partial class DocumentReviewService
{
    public const int MaximumSourceCharacters = 4 * 1024 * 1024;
    public const int MaximumIssues = 2000;
    private static readonly IReadOnlyDictionary<string, string> BritishTerms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["analyze"] = "analyse", ["analyzed"] = "analysed", ["analyzing"] = "analysing", ["behavior"] = "behaviour", ["behaviors"] = "behaviours",
        ["catalog"] = "catalogue", ["center"] = "centre", ["centered"] = "centred", ["color"] = "colour", ["colors"] = "colours",
        ["customize"] = "customise", ["customized"] = "customised", ["customizing"] = "customising", ["favorite"] = "favourite", ["favorites"] = "favourites",
        ["gray"] = "grey", ["honor"] = "honour", ["honors"] = "honours", ["localization"] = "localisation", ["neighbor"] = "neighbour", ["neighbors"] = "neighbours",
        ["organize"] = "organise", ["organized"] = "organised", ["organizing"] = "organising", ["prioritize"] = "prioritise", ["prioritized"] = "prioritised",
        ["prioritizing"] = "prioritising", ["specialize"] = "specialise", ["specialized"] = "specialised", ["specializing"] = "specialising"
    };

    public DocumentReviewResult Analyse(string source, bool standaloneMermaid = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (source.Length > MaximumSourceCharacters) throw new IOException("Document review supports up to 4 million source characters.");
        var parsed = new MarkdownService().Parse(source, standaloneMermaid);
        return AnalyseParsed(parsed, standaloneMermaid, cancellationToken);
    }

    internal DocumentReviewResult AnalyseParsed(ParsedDocument parsed, bool standaloneMermaid, CancellationToken cancellationToken)
    {
        var source = parsed.Source;
        cancellationToken.ThrowIfCancellationRequested();
        var issues = new List<DocumentReviewIssue>(); var limited = false;
        var words = 0; var headings = 0; var links = 0; var images = 0; var tables = 0; var code = 0; var diagrams = 0; var previousLevel = 0;
        var lineStarts = SourceLines(source, cancellationToken);
        var seenHeadings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var targets = parsed.Targets.Select(target => target.Id).ToHashSet(StringComparer.Ordinal);
        WalkBlocks(parsed.Syntax, 0);
        if (!standaloneMermaid) MarkdownTableReview.Scan(source, parsed, lineStarts,
            (start, length, message, suggestion) => Add("Tables", ReviewSeverity.Warning, message, suggestion, start, length), cancellationToken);
        if (parsed.Syntax.Count == 0)
            Add("Document", ReviewSeverity.Warning, "Document is empty.", "Add document content.", 0, 0);
        else if (!standaloneMermaid && !parsed.Headings.Any(heading => heading.Level == 1))
            Add("Headings", ReviewSeverity.Warning, "Missing H1 title.", "Add a level-one heading to identify this document.", 0, 0);
        cancellationToken.ThrowIfCancellationRequested();
        return new(new(words, headings, links, images, tables, code, diagrams), issues.OrderBy(issue => issue.Severity).ThenBy(issue => issue.Start).ToArray(), limited);

        void WalkBlocks(ContainerBlock container, int depth)
        {
            Check(depth);
            foreach (var block in container)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (block is CodeBlock)
                {
                    if (block is FencedCodeBlock fence && string.Equals(fence.Info, "mermaid", StringComparison.OrdinalIgnoreCase)) diagrams++;
                    else code++;
                    continue;
                }
                if (block is HeadingBlock heading)
                {
                    headings++;
                    if (previousLevel != 0 && heading.Level > previousLevel + 1)
                        Add("Heading hierarchy", ReviewSeverity.Warning, $"Heading level jumps from H{previousLevel} to H{heading.Level}.", $"Use H{previousLevel + 1} for this section or add the missing heading level.", heading.Span.Start, heading.Span.Length);
                    previousLevel = heading.Level;
                    var title = MarkdownService.InlineText(heading.Inline).Trim();
                    if (!seenHeadings.Add(title)) Add("Headings", ReviewSeverity.Note, $"Duplicate heading \"{Short(title)}\".", "Consider a distinct heading if these sections have different purposes.", heading.Span.Start, heading.Span.Length);
                }
                if (block is Table) tables++;
                if (block is DocumentImageFigureBlock figure) CountWords(figure.Caption);
                if (block is DocumentDetailsBlock details) CountWords(details.Summary);
                if (block is LeafBlock { Inline: { } inline })
                {
                    var text = new System.Text.StringBuilder(); WalkInlines(inline, depth + 1, false, text); CountWords(text.ToString());
                }
                if (block is ContainerBlock nested) WalkBlocks(nested, depth + 1);
            }
        }
        void WalkInlines(ContainerInline container, int depth, bool skipProse, System.Text.StringBuilder text)
        {
            Check(depth);
            foreach (var inline in container)
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (inline)
                {
                    case WikiLinkInline wiki: links++; text.Append(wiki.Label); break;
                    case LinkInline link:
                        if (link.IsImage)
                        {
                            images++;
                            if (string.IsNullOrWhiteSpace(MarkdownService.InlineText(link)))
                                Add("Alt text", ReviewSeverity.Warning, "Image is missing alt text.", link is FigureImageInline ? "Add a concise description in the image's alt attribute." : "Add a concise description inside ![...].", link.Span.Start, link.Span.Length);
                        }
                        else
                        {
                            links++;
                            if (link.Url is { Length: > 1 } url && url[0] == '#' && !targets.Contains(Uri.UnescapeDataString(url[1..])))
                                Add("Internal links", ReviewSeverity.Warning, "Link target is missing from this document.", "Choose an existing heading or named anchor.", link.Span.Start, link.Span.Length);
                        }
                        WalkInlines(link, depth + 1, skipProse || link.IsAutoLink, text); break;
                    case AutolinkInline autolink: links++; text.Append(autolink.Url); break;
                    case CodeInline: text.Append(' '); break;
                    case LiteralInline literal:
                        text.Append(literal.Content.ToString());
                        if (!skipProse) ScanProse(literal.Span); break;
                    case HtmlEntityInline entity: text.Append(entity.Transcoded.ToString()); break;
                    case DocumentSemanticInline semantic: text.Append(semantic.Text); break;
                    case DocumentAnchorInline anchor: text.Append(anchor.Text); break;
                    case LineBreakInline: text.Append(' '); break;
                    case ContainerInline nested: WalkInlines(nested, depth + 1, skipProse, text); break;
                }
            }
        }
        void CountWords(string text)
        {
            foreach (var match in Words().EnumerateMatches(text)) { cancellationToken.ThrowIfCancellationRequested(); words++; }
        }
        void ScanProse(SourceSpan span)
        {
            if (span.Start < 0 || span.Length <= 0 || span.End >= source.Length) return;
            foreach (var match in Terms().EnumerateMatches(source.AsSpan(span.Start, span.Length)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var start = span.Start + match.Index; var value = source.Substring(start, match.Length);
                if (value.Equals("TODO", StringComparison.OrdinalIgnoreCase) || value.Equals("FIXME", StringComparison.OrdinalIgnoreCase))
                    Add("TODO/FIXME", ReviewSeverity.Warning, $"{value.ToUpperInvariant()} marker found.", "Resolve the marker or move the follow-up into tracked work before release.", start, match.Length);
                else if (BritishTerms.TryGetValue(value, out var replacement))
                {
                    if (value.All(char.IsUpper)) replacement = replacement.ToUpperInvariant();
                    else if (char.IsUpper(value[0])) replacement = char.ToUpperInvariant(replacement[0]) + replacement[1..];
                    Add("British English", ReviewSeverity.Note, $"Review the spelling \"{value}\".", $"Use \"{replacement}\" unless this is an immutable name or quotation.", start, match.Length);
                }
            }
        }
        void Add(string rule, ReviewSeverity severity, string message, string suggestion, int start, int length)
        {
            if (issues.Count == MaximumIssues) { limited = true; return; }
            start = Math.Clamp(start, 0, source.Length); length = Math.Clamp(length, 0, source.Length - start);
            var line = lineStarts.BinarySearch(start); if (line < 0) line = ~line - 1;
            issues.Add(new(rule, severity, message, suggestion, start, length, line + 1, start - lineStarts[line] + 1));
        }
        void Check(int depth)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (depth > 64) throw new IOException("The document contains excessively nested review content.");
        }
    }
    internal static List<int> SourceLines(string source, CancellationToken cancellationToken)
    {
        var starts = new List<int> { 0 };
        for (var index = 0; index < source.Length; index++)
        {
            if ((index & 4095) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (source[index] == '\r') { if (index + 1 < source.Length && source[index + 1] == '\n') index++; starts.Add(index + 1); }
            else if (source[index] == '\n') starts.Add(index + 1);
        }
        return starts;
    }
    private static string Short(string text) => text.Length > 160 ? text[..157] + "…" : text;
    [GeneratedRegex(@"[\p{L}\p{N}]+(?:['-][\p{L}\p{N}]+)*", RegexOptions.CultureInvariant, 100)] private static partial Regex Words();
    [GeneratedRegex(@"\b[A-Za-z]+\b", RegexOptions.CultureInvariant, 100)] private static partial Regex Terms();
}
