using System.Net;
using AngleSharp.Html.Parser;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using LensDocsStudio.Core.Parsing;

namespace LensDocsStudio.Core.Exports;

public sealed class HtmlExporter
{
    public string Export(ExportContent content)
    {
        var fragment = RenderFragment(content);
        var title = WebUtility.HtmlEncode(content.Document.Headings.FirstOrDefault()?.Title ?? "Documentation");
        return "<!doctype html>\n<html lang=\"en-GB\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
            "<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; img-src data:; style-src 'unsafe-inline'\">" +
            "<title>" + title + "</title><style>body{font:16px/1.6 system-ui,sans-serif;max-width:960px;margin:2rem auto;padding:0 1.5rem;color:#202124;background:#fafafa}img{max-width:100%;height:auto}pre{white-space:pre-wrap;padding:1rem;background:#f3f4f6}table{border-collapse:collapse;width:100%}th,td{border:1px solid #bbb;padding:.5rem}blockquote{border-left:3px solid #ff883e;margin-left:0;padding-left:1rem}a{color:#174ea6}</style></head><body>" +
            fragment + "</body></html>";
    }
    public string RenderFragment(ExportContent content)
    {
        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        renderer.ObjectRenderers.Insert(0, new CodeRenderer(content));
        renderer.ObjectRenderers.Insert(0, new LinkRenderer(content));
        renderer.ObjectRenderers.Insert(0, new RawBlockRenderer());
        renderer.ObjectRenderers.Insert(0, new RawInlineRenderer());
        new MarkdownService().Pipeline.Setup(renderer);
        renderer.Render(content.Document.Syntax);
        // Generic Markdown attributes can carry event handlers or network-bearing styles.
        // Clipboard HTML has no surrounding CSP, so exported fragments need the same explicit attribute boundary.
        var fragment = new HtmlParser().ParseDocument(writer.ToString());
        foreach (var element in fragment.Body!.QuerySelectorAll("*"))
        {
            foreach (var attribute in element.Attributes.ToArray())
            {
                var allowed = attribute.Name switch
                {
                    "id" or "class" or "title" or "alt" or "colspan" or "rowspan" or "start" or "align" => true,
                    "disabled" or "checked" => element.LocalName == "input",
                    "type" => element.LocalName == "input" && attribute.Value == "checkbox",
                    "src" => element.LocalName == "img" && attribute.Value.StartsWith("data:image/png;base64,", StringComparison.Ordinal),
                    "href" => element.LocalName == "a" && SafeLink(attribute.Value),
                    "style" => attribute.Value is "text-align: left;" or "text-align: center;" or "text-align: right;",
                    _ => false
                };
                if (!allowed) element.RemoveAttribute(attribute.Name);
            }
        }
        return fragment.Body.InnerHtml;
    }
    private static bool SafeLink(string value) => !value.Any(char.IsControl) &&
        (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is "http" or "https" or "mailto");
    private sealed class CodeRenderer(ExportContent content) : HtmlObjectRenderer<FencedCodeBlock>
    {
        protected override void Write(HtmlRenderer renderer, FencedCodeBlock obj)
        {
            if (obj.Info is "mermaid" or "mmd")
            {
                if (!content.Diagrams.TryGetValue(obj.Span.Start, out var image)) throw new InvalidDataException("A Mermaid diagram could not be exported.");
                renderer.Write("<figure><img alt=\"Mermaid diagram\" src=\"data:image/png;base64,").Write(Convert.ToBase64String(image.PngBytes)).Write("\"></figure>");
            }
            else renderer.Write("<pre><code>").Write(WebUtility.HtmlEncode(obj.Lines.ToString())).Write("</code></pre>");
        }
    }
    private sealed class LinkRenderer(ExportContent content) : HtmlObjectRenderer<LinkInline>
    {
        protected override void Write(HtmlRenderer renderer, LinkInline obj)
        {
            if (obj.IsImage)
            {
                if (!content.Images.TryGetValue(obj.Url ?? "", out var image)) throw new InvalidDataException("An image could not be exported.");
                renderer.Write("<img alt=\"").Write(WebUtility.HtmlEncode(MarkdownService.InlineText(obj))).Write("\" src=\"data:image/png;base64,")
                    .Write(Convert.ToBase64String(image.PngBytes)).Write("\">");
                return;
            }
            var url = obj.Url ?? "";
            if (Uri.TryCreate(url, UriKind.Absolute, out var absolute) && absolute.Scheme is not ("https" or "http" or "mailto"))
            { renderer.WriteChildren(obj); return; }
            renderer.Write("<a href=\"").Write(WebUtility.HtmlEncode(url)).Write("\">");
            renderer.WriteChildren(obj);
            renderer.Write("</a>");
        }
    }
    private sealed class RawBlockRenderer : HtmlObjectRenderer<HtmlBlock>
    {
        protected override void Write(HtmlRenderer renderer, HtmlBlock obj) => renderer.Write("<pre>").Write(WebUtility.HtmlEncode(obj.Lines.ToString())).Write("</pre>");
    }
    private sealed class RawInlineRenderer : HtmlObjectRenderer<HtmlInline>
    {
        protected override void Write(HtmlRenderer renderer, HtmlInline obj) => renderer.Write(WebUtility.HtmlEncode(obj.Tag));
    }
}
