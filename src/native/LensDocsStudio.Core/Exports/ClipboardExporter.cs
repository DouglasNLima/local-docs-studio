using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using System.Globalization;

namespace LensDocsStudio.Core.Exports;

public sealed record ExportedClipboard(string PlainText, string Html);

public sealed class ClipboardExporter
{
    public ExportedClipboard Export(ExportContent content, ExportTableSelection? table = null)
    {
        if (content.Document.Source.Length > 4 * 1024 * 1024 || content.Images.Values.Concat(content.Diagrams.Values).Sum(i => (long)i.PngBytes.Length) > 16 * 1024 * 1024)
            throw new IOException("Copy rendered content is limited to 4 million source characters and 16 MB of prepared images. Export larger documents to a file.");
        var fragment = "<div>" + new HtmlExporter().RenderFragment(content, table) + "</div>";
        var html = new HtmlParser().ParseDocument(fragment);
        // Documentation cells are literal text, including identifiers and formula-like examples.
        foreach (var element in html.QuerySelectorAll("table")) element.SetAttribute("x:str", "");
        foreach (var cell in html.QuerySelectorAll("th,td"))
            cell.SetAttribute("style", (cell.GetAttribute("style") ?? "") + "mso-number-format:\"\\@\";");
        var encoded = HtmlClipboardFormat.Encode(html.Body!.InnerHtml);
        string ListMarker(IElement item)
        {
            if (item.ParentElement is not { LocalName: "ol" } list) return "• ";
            var start = int.TryParse(list.GetAttribute("start"), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 1;
            var index = list.Children.TakeWhile(child => child != item).Count(child => child.LocalName == "li");
            return (start + index).ToString(CultureInfo.InvariantCulture) + ". ";
        }
        string Read(INode node)
        {
            if (node is IText text) return text.TextContent;
            if (node is not IElement element) return string.Concat(node.ChildNodes.Select(Read));
            if (element.LocalName == "img") return "[" + (element.GetAttribute("alt") ?? "Image") + "]";
            if (element.LocalName == "input" && element.GetAttribute("type") == "checkbox") return element.HasAttribute("checked") ? "[x] " : "[ ] ";
            if (element.LocalName == "table") return "\n" + string.Join("\n", element.QuerySelectorAll("tr").Select(row =>
                string.Join("\t", row.Children.Where(c => c.LocalName is "td" or "th").Select(cell => Read(cell).Trim())))) + "\n";
            var children = string.Concat(element.ChildNodes.Select(Read));
            return element.LocalName switch
            {
                "br" => "\n",
                "hr" => "\n---\n",
                "li" => ListMarker(element) + children.Trim() + "\n",
                "figcaption" => "\n" + children.TrimEnd(),
                "p" or "div" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "pre" or "blockquote" or "figure" => children.TrimEnd() + "\n\n",
                _ => children
            };
        }
        return new(Read(html.Body!).Trim(), encoded);
    }
}
