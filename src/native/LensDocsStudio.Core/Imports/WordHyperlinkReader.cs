using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using LensDocsStudio.Core.Parsing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Core.Imports;

internal sealed class WordHyperlinkReader
{
    private readonly Dictionary<string, HyperlinkRelationship> relationships;
    private readonly Dictionary<string, string> targets = new(StringComparer.Ordinal);
    private readonly Dictionary<W.BookmarkStart, string> markers = [];
    private readonly HashSet<string> reported = [];
    private readonly List<string> warnings;
    private readonly CancellationToken cancellationToken;
    private readonly int maximumCharacters;

    public WordHyperlinkReader(MainDocumentPart main, IEnumerable<W.Paragraph> paragraphs, List<string> warnings, CancellationToken cancellationToken, int maximumCharacters)
    {
        this.warnings = warnings; this.cancellationToken = cancellationToken; this.maximumCharacters = maximumCharacters;
        relationships = main.HyperlinkRelationships.ToDictionary(r => r.Id, StringComparer.Ordinal);
        var names = new Dictionary<string, (W.BookmarkStart Start, int Order)> (StringComparer.Ordinal);
        var duplicateNames = new HashSet<string>(StringComparer.Ordinal);
        var duplicateIds = new HashSet<string>(StringComparer.Ordinal);
        var startIds = new HashSet<string>(StringComparer.Ordinal);
        var ends = new Dictionary<string, int>(StringComparer.Ordinal);
        var referenced = new HashSet<string>(StringComparer.Ordinal);
        var order = 0;
        foreach (var element in paragraphs.SelectMany(p => p.Descendants()))
        {
            cancellationToken.ThrowIfCancellationRequested(); order++;
            switch (element)
            {
                case W.Hyperlink link when string.IsNullOrEmpty(link.Id?.Value) && link.Anchor?.Value is { Length: > 0 } anchor:
                    referenced.Add(anchor); break;
                case W.BookmarkStart start when start.Name?.Value is { Length: > 0 } name && start.Id?.Value is { } id:
                    if (!names.TryAdd(name, (start, order))) duplicateNames.Add(name);
                    if (!startIds.Add(id)) duplicateIds.Add(id);
                    break;
                case W.BookmarkEnd end when end.Id?.Value is { } id:
                    if (!ends.TryAdd(id, order)) duplicateIds.Add(id);
                    break;
            }
        }
        // Use an import-specific namespace; source markup cannot become an anchor id.
        // Only paired, unambiguous, referenced bookmarks survive.
        var prefix = "word-bookmark-" + Guid.NewGuid().ToString("N") + "-";
        foreach (var (name, bookmark) in names)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = bookmark.Start.Id!.Value!;
            if (!referenced.Contains(name) || duplicateNames.Contains(name) || duplicateIds.Contains(id)
                || !ends.TryGetValue(id, out var endOrder) || endOrder <= bookmark.Order) continue;
            var target = prefix + targets.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            targets.Add(name, target); markers.Add(bookmark.Start, target);
        }
    }

    public string CodeAnchors(W.Paragraph paragraph)
    {
        var output = new WordMarkdownBuilder(maximumCharacters);
        foreach (var marker in paragraph.Descendants<W.BookmarkStart>().Where(markers.ContainsKey))
        {
            cancellationToken.ThrowIfCancellationRequested();
            output.Append("<span data-doc-anchor id=\"").Append(markers[marker]).Append("\"></span>");
        }
        return output.ToString();
    }

    public string Read(W.Paragraph paragraph, Func<W.Run, string> renderRun, bool literal = false)
    {
        var output = new WordMarkdownBuilder(maximumCharacters);
        foreach (var element in paragraph.ChildElements) Append(element, output, 0, false);
        return output.ToString();

        void Append(OpenXmlElement element, WordMarkdownBuilder target, int depth, bool insideLink)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (depth > 64) throw new InvalidDataException("Word inline content exceeds the nesting limit.");
            switch (element)
            {
                case W.ParagraphProperties: return;
                case W.Run run:
                    if (run.Elements<W.FieldCode>().Any() || run.Elements<W.FieldChar>().Any()) WarnField();
                    target.Append(renderRun(run)); return;
                case W.BookmarkStart bookmark:
                    if (!literal && markers.TryGetValue(bookmark, out var id)) target.Append("<span data-doc-anchor id=\"").Append(id).Append("\"></span>");
                    return;
                case W.Hyperlink link:
                    var label = new WordMarkdownBuilder(maximumCharacters);
                    foreach (var child in link.ChildElements) Append(child, label, depth + 1, true);
                    if (literal) { Warn("Hyperlinks in a Word code block were retained as literal text."); target.Append(label); return; }
                    if (insideLink) { Warn("A nested Word hyperlink was retained as text inside its parent link."); target.Append(label); return; }
                    var destination = Destination(link);
                    if (destination is null || label.Length == 0) target.Append(label);
                    else target.Append('[').Append(label).Append("](").Append(EscapeDestination(destination)).Append(')');
                    return;
                case W.SimpleField: WarnField(); break;
            }
            foreach (var child in element.ChildElements) Append(child, target, depth + 1, insideLink);
        }
    }

    private string? Destination(W.Hyperlink link)
    {
        if (link.Id?.Value is { Length: > 0 } id)
        {
            if (relationships.TryGetValue(id, out var relationship)
                && MarkdownLinks.ExternalUri(relationship.Uri.OriginalString) is { } uri)
            {
                if (!string.IsNullOrEmpty(link.DocLocation?.Value))
                    Warn("A Word link's document location was not imported; its external address was retained.");
                return uri.AbsoluteUri;
            }
            Warn("An unsupported or missing Word hyperlink destination was retained as text.");
            return null;
        }
        if (link.Anchor?.Value is { } anchor && targets.TryGetValue(anchor, out var target)) return "#" + target;
        Warn("A missing or ambiguous Word bookmark link was retained as text.");
        return null;
    }

    private void WarnField() => Warn("A Word field was retained as its stored result; it was not evaluated.");
    private void Warn(string message) { if (reported.Add(message)) warnings.Add(message); }
    private static string EscapeDestination(string value) => value.Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("\\", "%5C", StringComparison.Ordinal).Replace(" ", "%20", StringComparison.Ordinal)
        .Replace("(", "%28", StringComparison.Ordinal).Replace(")", "%29", StringComparison.Ordinal)
        .Replace("<", "%3C", StringComparison.Ordinal).Replace(">", "%3E", StringComparison.Ordinal).Replace("|", "%7C", StringComparison.Ordinal);
}
