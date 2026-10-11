using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LensDocsStudio.Core.Parsing;

public sealed record MarkdownLink(string Reference, bool IsImage, int Start, int Length, bool HtmlEncoded = false);

public static class MarkdownLinks
{
    public static IReadOnlyList<MarkdownLink> Read(string source) => new MarkdownService().Parse(source).Syntax.Descendants<LinkInline>()
        .Where(link => !string.IsNullOrEmpty(link.Url)).Select(link =>
        {
            var span = link.Reference?.UrlSpan ?? link.UrlSpan;
            return new MarkdownLink(link.Url!, link.IsImage, span.Start, span.Length, link is FigureImageInline);
        }).Where(link => link.Start >= 0 && link.Length > 0 && (long)link.Start + link.Length <= source.Length).ToArray();

    public static string Rewrite(string source, IEnumerable<(MarkdownLink Link, string Reference)> changes)
    {
        var replacements = changes.GroupBy(c => (c.Link.Start, c.Link.Length)).Select(group =>
        {
            if (group.Select(c => c.Reference).Distinct(StringComparer.Ordinal).Count() != 1)
                throw new IOException("A shared Markdown reference has conflicting changes.");
            return group.First();
        }).OrderByDescending(c => c.Link.Start).ToArray();
        var previousStart = source.Length;
        foreach (var (link, reference) in replacements)
        {
            if (link.Start < 0 || link.Length <= 0 || (long)link.Start + link.Length > previousStart)
                throw new IOException("Markdown link source positions overlap or are invalid.");
            source = source.Remove(link.Start, link.Length).Insert(link.Start, link.HtmlEncoded ? System.Net.WebUtility.HtmlEncode(reference) : reference);
            previousStart = link.Start;
        }
        return source;
    }

    public static string RelativeReference(string documentPath, string targetPath) =>
        string.Join('/', Path.GetRelativePath(Path.GetDirectoryName(documentPath)!, targetPath).Split(Path.DirectorySeparatorChar).Select(Uri.EscapeDataString));
}
