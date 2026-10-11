using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace LensDocsStudio.Core.Imports;

internal static class MarkdownSourceCarrier
{
    // This marker is only a format hint. It cannot grant permission to fetch or open an image.
    public static string? Read(string html, CancellationToken cancellationToken)
    {
        var document = new HtmlParser().ParseDocument(html);
        var carrier = document.QuerySelector("span[data-markdown-clipboard='source']");
        if (carrier is null) return null;
        if (carrier.ChildNodes.Length > 100000) throw new IOException("The Markdown clipboard carrier exceeds its node limit.");
        var source = new StringBuilder(); var index = 0;
        foreach (var child in carrier.ChildNodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (child is IText text) source.Append(text.Data);
            else if (child is IElement image && image.LocalName == "img" && image.GetAttribute("data-markdown-clipboard-image") == index.ToString(System.Globalization.CultureInfo.InvariantCulture))
            {
                var reference = image.GetAttribute("src") ?? "";
                if (!reference.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)) return null;
                var title = image.GetAttribute("title");
                if (image.GetAttribute("data-markdown-clipboard-html-image") == "true")
                {
                    source.Append("<img src=\"").Append(System.Net.WebUtility.HtmlEncode(reference)).Append("\" alt=\"")
                        .Append(System.Net.WebUtility.HtmlEncode(image.GetAttribute("alt") ?? "")).Append('"');
                    if (!string.IsNullOrEmpty(title)) source.Append(" title=\"").Append(System.Net.WebUtility.HtmlEncode(title)).Append('"');
                    source.Append('>'); index++; continue;
                }
                source.Append("![").Append(HtmlMarkdownConverter.Escape(image.GetAttribute("alt") ?? "Image")).Append("](").Append(reference);
                if (!string.IsNullOrEmpty(title)) source.Append(" \"").Append(title.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
                source.Append(')'); index++;
            }
            else return null;
        }
        return source.ToString();
    }
}
