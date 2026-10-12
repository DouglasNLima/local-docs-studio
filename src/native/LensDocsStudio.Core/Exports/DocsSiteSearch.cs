using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace LensDocsStudio.Core.Exports;

public sealed record DocsSiteSearchEntry(string Id, string PageId, string Title, string Path, string HeadingId, string HeadingText, int Level, string Text);

internal static class DocsSiteSearch
{
    public static IReadOnlyList<DocsSiteSearchEntry> Build(IReadOnlyList<DocsSitePage> pages, long renderedBytes, CancellationToken cancellationToken)
    {
        var entries = new List<DocsSiteSearchEntry>(); var estimatedBytes = renderedBytes;
        foreach (var page in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var body = new HtmlParser().ParseDocument(page.Html).Body!;
            var sections = new List<(string Id, string Heading, int Level, string Text)>();
            var text = new StringBuilder(); var headingId = ""; var headingText = page.Title; var level = 0; var intro = "";
            var nodes = new Stack<(INode Node, bool End)>();
            foreach (var child in body.ChildNodes.Reverse()) nodes.Push((child, false));
            while (nodes.TryPop(out var item))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (item.End) { text.Append(' '); continue; }
                if (item.Node is IText literal) { text.Append(literal.TextContent); continue; }
                if (item.Node is not IElement element) continue;
                if (element.LocalName is "h1" or "h2" or "h3" or "h4" or "h5" or "h6")
                {
                    Flush(); headingId = element.Id ?? ""; headingText = element.TextContent.Trim(); level = element.LocalName[1] - '0';
                }
                var block = element.LocalName is "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "p" or "div" or "pre" or "li" or "blockquote"
                    or "table" or "tr" or "td" or "th" or "figure" or "figcaption" or "section" or "br" or "hr";
                if (block) { text.Append(' '); nodes.Push((element, true)); }
                if (element.LocalName == "img") text.Append(' ').Append(element.GetAttribute("alt")).Append(' ');
                foreach (var child in element.ChildNodes.Reverse()) nodes.Push((child, false));
            }
            Flush();
            Add(new(page.Id + "--page", page.Id, page.Title, page.Path, "", page.Title, 0,
                Normalise(intro + " " + page.Metadata.Description + " " + string.Join(' ', page.Metadata.Tags) + " " + page.Metadata.NavigationGroup + " " + page.ArtifactKind + " " + page.EvidenceLevel)));
            for (var index = 0; index < sections.Count; index++)
            {
                var section = sections[index];
                Add(new(page.Id + "--section-" + index, page.Id, page.Title, page.Path, section.Id, section.Heading, section.Level,
                    Normalise(section.Text + " " + page.ArtifactKind + " " + page.EvidenceLevel)));
            }
            void Flush()
            {
                var value = Normalise(text.ToString()); text.Clear();
                if (level == 0) intro = value;
                else if (value.Length > 0 || headingId.Length > 0) sections.Add((headingId, headingText, level, value));
                if (sections.Count > 50_000) throw new IOException("The Docs Site exceeds its 50,000-entry search limit.");
            }
        }
        return entries;

        void Add(DocsSiteSearchEntry entry)
        {
            cancellationToken.ThrowIfCancellationRequested();
            estimatedBytes += 512L + Encoding.UTF8.GetByteCount(entry.Id) + Encoding.UTF8.GetByteCount(entry.PageId) + Encoding.UTF8.GetByteCount(entry.Title)
                + Encoding.UTF8.GetByteCount(entry.Path) + Encoding.UTF8.GetByteCount(entry.HeadingId) + Encoding.UTF8.GetByteCount(entry.HeadingText) + Encoding.UTF8.GetByteCount(entry.Text);
            if (entries.Count >= 50_000 || estimatedBytes > DocsSiteExporter.MaximumOutputBytes / 3)
                throw new IOException("The Docs Site search index exceeds its entry or size budget.");
            entries.Add(entry);
        }
    }
    private static string Normalise(string value)
    {
        var result = new StringBuilder(value.Length); var space = false;
        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character)) { space = result.Length > 0; continue; }
            if (space) result.Append(' ');
            result.Append(character); space = false;
        }
        return result.ToString();
    }
}
