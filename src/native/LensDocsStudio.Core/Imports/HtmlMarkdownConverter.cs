using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace LensDocsStudio.Core.Imports;

/// <summary>Converts inert HTML into portable source; parsing never creates a browsing context.</summary>
public sealed partial class HtmlMarkdownConverter
{
    public ImportedDocument Convert(string html, CancellationToken cancellationToken = default)
    {
        var document = Parse(html, cancellationToken);
        var assets = new List<ImportedAsset>();
        var warnings = new HashSet<string>();
        var images = new Dictionary<string, string>(StringComparer.Ordinal);
        long imageBytes = 0;
        string Children(INode node, bool inTable = false) => string.Concat(node.ChildNodes.Select(n => Read(n, inTable)));
        string Read(INode node, bool inTable = false)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (node is IText text) return Escape(Whitespace().Replace(text.Data, " "));
            if (node is not IElement element) return Children(node, inTable);
            if (Excluded(element)) return "";
            var tag = element.LocalName;
            if (tag == "pre") return "\n\n" + Fence(element.TextContent, "text") + "\n\n";
            if (tag == "code") return InlineCode(element.TextContent);
            if (tag == "img") return Image(element);
            if (tag == "table") return Table(element, inTable);
            if (tag is "ul" or "ol") return List(element, inTable);
            var content = Children(element, inTable);
            return tag switch
            {
                "h1" or "h2" or "h3" or "h4" or "h5" or "h6" => "\n\n" + new string('#', tag[1] - '0') + " " + content.Trim() + "\n\n",
                "p" or "div" or "section" or "article" or "figure" or "figcaption" => "\n\n" + content.Trim() + "\n\n",
                "br" => "  \n",
                "hr" => "\n\n---\n\n",
                "strong" or "b" => Emphasis(content, "**"),
                "em" or "i" => Emphasis(content, "*"),
                "del" or "s" => Emphasis(content, "~~"),
                "blockquote" => "\n\n" + string.Join('\n', content.Trim().Split('\n').Select(line => "> " + line)) + "\n\n",
                "a" => Link(element, content),
                "input" when element.GetAttribute("type")?.Equals("checkbox", StringComparison.OrdinalIgnoreCase) == true => element.HasAttribute("checked") ? "[x] " : "[ ] ",
                _ => content
            };
        }
        string Link(IElement element, string content)
        {
            var href = element.GetAttribute("href") ?? "";
            if (!SafeReference(href)) { warnings.Add("An unsafe link was retained as text."); return content; }
            return "[" + content.Trim() + "](" + Destination(href) + ")";
        }
        string Image(IElement element)
        {
            var source = element.GetAttribute("src") ?? "";
            var alt = Escape(Whitespace().Replace(element.GetAttribute("alt") ?? "Image", " "));
            if (source.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                if (!images.TryGetValue(source, out var reference))
                {
                    var match = DataImage().Match(source);
                    if (!match.Success) { warnings.Add("An unsupported embedded image was retained as text."); return "Image: " + alt; }
                    if (assets.Count >= 128) throw new InvalidDataException("HTML import is limited to 128 distinct embedded images.");
                    byte[] bytes;
                    try { bytes = System.Convert.FromBase64String(match.Groups[2].Value); }
                    catch (FormatException) { warnings.Add("An invalid embedded image was retained as text."); return "Image: " + alt; }
                    if (bytes.Length == 0 || (imageBytes += bytes.Length) > 32 * 1024 * 1024) throw new InvalidDataException("Embedded images exceed the HTML import limit.");
                    reference = "assets/images/import-" + Guid.NewGuid().ToString("N") + ".png";
                    assets.Add(new(reference, bytes, "imported-image")); images[source] = reference;
                }
                source = reference;
            }
            else
            {
                if (!SafeReference(source)) { warnings.Add("An unsafe image reference was retained as text."); return "Image: " + alt; }
                warnings.Add("Linked image was retained as a reference; it was not fetched.");
            }
            return "![" + alt + "](" + Destination(source) + ")";
        }
        string List(IElement element, bool inTable)
        {
            var ordered = element.LocalName == "ol";
            var number = int.TryParse(element.GetAttribute("start"), out var start) && start is > 0 and < 1000000 ? start : 1;
            var items = element.Children.Where(e => e.LocalName == "li").Select(item =>
            {
                var prefix = ordered ? (number++).ToString(CultureInfo.InvariantCulture) + ". " : "- ";
                var content = Children(item, inTable).Trim();
                return prefix + content.Replace("\n", "\n" + new string(' ', prefix.Length));
            });
            return "\n\n" + string.Join('\n', items) + "\n\n";
        }
        string Table(IElement element, bool nested)
        {
            var rows = element.QuerySelectorAll("tr").Where(row => NearestTable(row) == element).ToArray();
            if (nested)
            {
                warnings.Add("A nested table was flattened within its parent cell.");
                return string.Join(" / ", rows.SelectMany(row => row.Children.Where(c => c.LocalName is "td" or "th")).Select(cell => Children(cell, true).Trim()));
            }
            if (rows.Length == 0) return "";
            if (rows.Length > 10000) throw new InvalidDataException("The HTML table exceeds 10,000 rows.");
            var cells = new Dictionary<(int Row, int Column), string>();
            var height = rows.Length; var width = 0;
            for (var row = 0; row < rows.Length; row++)
            {
                var column = 0;
                foreach (var cell in rows[row].Children.Where(c => c.LocalName is "td" or "th"))
                {
                    while (cells.ContainsKey((row, column))) column++;
                    var rowSpan = Span(cell, "rowspan", rows.Length - row); var colSpan = Span(cell, "colspan", 1);
                    if (rowSpan > 1 || colSpan > 1) warnings.Add("Merged table cells were flattened into a rectangular Markdown table.");
                    if (column + colSpan > 256 || row + rowSpan > 10000 || (long)cells.Count + rowSpan * colSpan > 100000)
                        throw new InvalidDataException("The HTML table exceeds its row, column or cell limits.");
                    var value = Children(cell, true).Trim().Replace("\r", "").Replace("\n", "<br>");
                    for (var r = row; r < row + rowSpan; r++)
                    for (var c = column; c < column + colSpan; c++)
                        if (!cells.TryAdd((r, c), r == row && c == column ? value : "")) throw new InvalidDataException("The HTML table has overlapping merged cells.");
                    column += colSpan; width = Math.Max(width, column); height = Math.Max(height, row + rowSpan);
                }
            }
            if (width == 0) return "";
            if ((long)width * height > 100000) throw new InvalidDataException("The expanded HTML table exceeds 100,000 cells.");
            string Row(int r) => "| " + string.Join(" | ", Enumerable.Range(0, width).Select(c => cells.GetValueOrDefault((r, c), ""))) + " |";
            return "\n\n" + Row(0) + "\n| " + string.Join(" | ", Enumerable.Repeat("---", width)) + " |\n" + string.Join('\n', Enumerable.Range(1, height - 1).Select(Row)) + "\n\n";
        }
        var markdown = Read(document.Body!).Trim();
        if (markdown.Length > 32 * 1024 * 1024) throw new InvalidDataException("The converted HTML exceeds 32 million characters.");
        return new(markdown + "\n", assets, warnings.ToArray());
    }

    public string PlainText(string html, CancellationToken cancellationToken = default)
    {
        var document = Parse(html, cancellationToken);
        string Read(INode node)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (node is IText text) return Whitespace().Replace(text.Data, " ");
            if (node is not IElement element) return string.Concat(node.ChildNodes.Select(Read));
            if (Excluded(element)) return "";
            if (element.LocalName == "pre") return "\n" + element.TextContent + "\n";
            if (element.LocalName == "table") return "\n" + string.Join('\n', element.QuerySelectorAll("tr").Where(row => NearestTable(row) == element)
                .Select(row => string.Join('\t', row.Children.Where(c => c.LocalName is "td" or "th").Select(cell => Read(cell).Trim())))) + "\n";
            var content = string.Concat(element.ChildNodes.Select(Read));
            return element.LocalName switch
            {
                "br" => "\n",
                "img" => element.GetAttribute("alt") ?? "Image",
                "p" or "div" or "section" or "article" or "blockquote" or "li" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6" => content.Trim() + "\n",
                _ => content
            };
        }
        return Read(document.Body!).Trim();
    }

    public static string Escape(string value) => MarkdownPunctuation().Replace(value.Replace("&", "&amp;", StringComparison.Ordinal),
        match => match.Value switch { "(" => "&#40;", ")" => "&#41;", _ => "\\" + match.Value });
    public static string Fence(string value, string language = "")
    {
        value = Normalise(value);
        var fence = new string('`', Math.Max(3, Backticks().Matches(value).Select(m => m.Length + 1).DefaultIfEmpty(3).Max()));
        return fence + language + "\n" + value + (value.EndsWith('\n') ? "" : "\n") + fence;
    }
    public static string Normalise(string value) => value.Replace("\r\n", "\n").Replace('\r', '\n');
    private static string InlineCode(string value)
    {
        value = Normalise(value).Replace('\n', ' ');
        var ticks = new string('`', Backticks().Matches(value).Select(m => m.Length + 1).DefaultIfEmpty(1).Max());
        var pad = value.StartsWith('`') || value.EndsWith('`') || value.StartsWith(' ') && value.EndsWith(' ') && value.Any(c => c != ' ') ? " " : "";
        return ticks + pad + value + pad + ticks;
    }
    private static string Emphasis(string content, string marker)
    {
        var trimmed = content.Trim();
        return trimmed.Length == 0 ? content : (char.IsWhiteSpace(content[0]) ? " " : "") + marker + trimmed + marker + (char.IsWhiteSpace(content[^1]) ? " " : "");
    }
    private static string Destination(string value) => value.Replace("\\", "%5C").Replace(" ", "%20").Replace("(", "%28").Replace(")", "%29").Replace("<", "%3C").Replace(">", "%3E");
    private static bool SafeReference(string value) => !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl) &&
        (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is "http" or "https" or "mailto");
    private static bool Excluded(IElement element) => element.LocalName is "script" or "style" or "iframe" or "object" or "embed" or "head" or "svg" or "template";
    private static IElement? NearestTable(IElement element)
    {
        for (var parent = element.ParentElement; parent is not null; parent = parent.ParentElement)
            if (parent.LocalName == "table") return parent;
        return null;
    }
    private static int Span(IElement element, string attribute, int remaining)
    {
        if (!int.TryParse(element.GetAttribute(attribute), out var span)) return 1;
        return span == 0 && attribute == "rowspan" ? remaining : Math.Clamp(span, 1, 10001);
    }
    private static IDocument Parse(string html, CancellationToken cancellationToken)
    {
        if (html.Length > 32 * 1024 * 1024) throw new InvalidDataException("HTML import is limited to 32 million characters.");
        cancellationToken.ThrowIfCancellationRequested();
        var document = new HtmlParser().ParseDocument(html);
        var pending = new Stack<(INode Node, int Depth)>(); pending.Push((document, 0)); var nodes = 0;
        while (pending.TryPop(out var item))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++nodes > 100000 || item.Depth > 128) throw new InvalidDataException("HTML import exceeds the document complexity limit.");
            foreach (var child in item.Node.ChildNodes) pending.Push((child, item.Depth + 1));
        }
        return document;
    }
    [GeneratedRegex(@"[\\`*_{}\[\]<>()!#+.\-|~>=$]")] private static partial Regex MarkdownPunctuation();
    [GeneratedRegex(@"\s+")] private static partial Regex Whitespace();
    [GeneratedRegex("`+")] private static partial Regex Backticks();
    [GeneratedRegex(@"^data:image/(png|jpeg|gif|webp|bmp);base64,([A-Za-z0-9+/=\s]+)$", RegexOptions.IgnoreCase)] private static partial Regex DataImage();
}
