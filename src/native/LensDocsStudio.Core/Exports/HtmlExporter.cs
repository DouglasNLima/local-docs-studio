using System.Net;
using AngleSharp.Html.Parser;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using LensDocsStudio.Core.Parsing;
using Markdig.Extensions.Mathematics;

namespace LensDocsStudio.Core.Exports;

public sealed class HtmlExporter
{
    public string Export(ExportContent content, CancellationToken cancellationToken = default)
    {
        var fragment = RenderFragment(content, cancellationToken: cancellationToken);
        var title = WebUtility.HtmlEncode(content.Document.Headings.FirstOrDefault()?.Title ?? "Documentation");
        return "<!doctype html>\n<html lang=\"en-GB\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
            "<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; img-src data:; style-src 'unsafe-inline'\">" +
            "<title>" + title + "</title><style>body{font:16px/1.6 system-ui,sans-serif;max-width:960px;margin:2rem auto;padding:0 1.5rem;color:#202124;background:#fafafa}img{max-width:100%;height:auto}pre{white-space:pre-wrap;padding:1rem;background:#f3f4f6}table{border-collapse:collapse;width:100%}th,td{border:1px solid #bbb;padding:.5rem}blockquote{border-left:3px solid #ff883e;margin-left:0;padding-left:1rem}a{color:#174ea6}.document-progress{margin:1rem 0}.document-progress progress{width:100%;accent-color:#FF883E}.progress-green progress{accent-color:#2F855A}.progress-blue progress{accent-color:#2563EB}.progress-amber progress{accent-color:#B7791F}.progress-red progress{accent-color:#C53030}.progress-neutral progress{accent-color:#64748B}</style></head><body>" +
            fragment + "</body></html>";
    }
    public string RenderFragment(ExportContent content, ExportTableSelection? table = null, bool flattenDetails = false, bool preserveWikiLinks = false,
        CancellationToken cancellationToken = default)
    {
        content.AllocationLimits.Validate(content, table, cancellationToken);
        using var writer = new BoundedExportWriter(content.AllocationLimits.MaximumHtmlCharacters, cancellationToken);
        var renderer = new HtmlRenderer(writer);
        renderer.ObjectRenderers.Insert(0, new CodeRenderer(content));
        renderer.ObjectRenderers.Insert(0, new LinkRenderer(content));
        renderer.ObjectRenderers.Insert(0, new WikiRenderer(preserveWikiLinks));
        renderer.ObjectRenderers.Insert(0, new RawBlockRenderer());
        renderer.ObjectRenderers.Insert(0, new RawInlineRenderer());
        renderer.ObjectRenderers.Insert(0, new SemanticInlineRenderer());
        renderer.ObjectRenderers.Insert(0, new AnchorRenderer());
        renderer.ObjectRenderers.Insert(0, new ProgressRenderer());
        renderer.ObjectRenderers.Insert(0, new FigureRenderer());
        renderer.ObjectRenderers.Insert(0, new DetailsRenderer(flattenDetails));
        renderer.ObjectRenderers.Insert(0, new MathBlockRenderer(content));
        renderer.ObjectRenderers.Insert(0, new MathInlineRenderer(content));
        new MarkdownService().Pipeline.Setup(renderer);
        renderer.Render(table is null ? content.Document.Syntax : table.Resolve(content.Document));
        // Generic Markdown attributes can carry event handlers or network-bearing styles.
        // Clipboard HTML has no surrounding CSP, so exported fragments need the same explicit attribute boundary.
        var fragment = new HtmlParser().ParseDocument(writer.ToString());
        foreach (var element in fragment.Body!.QuerySelectorAll("*"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var attribute in element.Attributes.ToArray())
            {
                var allowed = attribute.Name switch
                {
                    "id" or "class" or "title" or "alt" or "colspan" or "rowspan" or "start" or "align" => true,
                    "disabled" or "checked" => element.LocalName == "input",
                    "type" => element.LocalName == "input" && attribute.Value == "checkbox",
                    "src" => element.LocalName == "img" && attribute.Value.StartsWith("data:image/png;base64,", StringComparison.Ordinal),
                    "href" => element.LocalName == "a" && SafeLink(attribute.Value),
                    "data-wikilink-target" => preserveWikiLinks && element.LocalName == "a",
                    "max" or "value" => element.LocalName == "progress",
                    "aria-label" => element.LocalName == "progress",
                    "open" => element.LocalName == "details",
                    "width" or "height" => element.LocalName == "img" && int.TryParse(attribute.Value, out var size) && size is > 0 and <= 8192,
                    "style" => attribute.Value is "text-align: left;" or "text-align: center;" or "text-align: right;",
                    _ => false
                };
                if (!allowed) element.RemoveAttribute(attribute.Name);
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return fragment.Body.InnerHtml;
    }
    private static bool SafeLink(string value) => !value.Any(char.IsControl) &&
        (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is "http" or "https" or "mailto");
    private sealed class WikiRenderer(bool preserve) : HtmlObjectRenderer<WikiLinkInline>
    {
        protected override void Write(HtmlRenderer renderer, WikiLinkInline obj)
        {
            if (preserve) renderer.Write("<a data-wikilink-target=\"").Write(WebUtility.HtmlEncode(obj.Target)).Write("\">");
            renderer.Write(WebUtility.HtmlEncode(obj.Label));
            if (preserve) renderer.Write("</a>");
        }
    }
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
    private sealed class DetailsRenderer(bool flatten) : HtmlObjectRenderer<DocumentDetailsBlock>
    {
        protected override void Write(HtmlRenderer renderer, DocumentDetailsBlock obj)
        {
            if (flatten) renderer.Write("<section class=\"document-details\"><p><strong>").Write(WebUtility.HtmlEncode(obj.Summary)).Write("</strong></p>");
            else renderer.Write(obj.InitiallyExpanded ? "<details class=\"document-details\" open><summary>" : "<details class=\"document-details\"><summary>")
                .Write(WebUtility.HtmlEncode(obj.Summary)).Write("</summary>");
            renderer.WriteChildren(obj); renderer.Write(flatten ? "</section>" : "</details>");
        }
    }
    private static void Formula(HtmlRenderer renderer, ExportContent content, int start, string source, bool display)
    {
        renderer.Write(display ? "<div class=\"document-math math-block\">" : "<span class=\"document-math math-inline\">");
        if (content.Formulae.TryGetValue(start, out var image))
        {
            renderer.Write("<img alt=\"").Write(WebUtility.HtmlEncode(source)).Write("\" src=\"data:image/png;base64,")
                .Write(Convert.ToBase64String(image.PngBytes)).Write("\" width=\"").Write(((int)Math.Ceiling(image.DisplayWidth)).ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Write("\" height=\"").Write(((int)Math.Ceiling(image.DisplayHeight)).ToString(System.Globalization.CultureInfo.InvariantCulture)).Write("\">");
        }
        else renderer.Write("<code>").Write(WebUtility.HtmlEncode(source)).Write("</code>");
        renderer.Write(display ? "</div>" : "</span>");
    }
    private sealed class MathBlockRenderer(ExportContent content) : HtmlMathBlockRenderer
    {
        protected override void Write(HtmlRenderer renderer, MathBlock block) => Formula(renderer, content, block.Span.Start, block.Lines.ToString(), true);
    }
    private sealed class MathInlineRenderer(ExportContent content) : HtmlMathInlineRenderer
    {
        protected override void Write(HtmlRenderer renderer, MathInline inline) => Formula(renderer, content, inline.Span.Start, inline.Content.ToString(), inline.DelimiterCount == 2);
    }
    private sealed class FigureRenderer : HtmlObjectRenderer<DocumentImageFigureBlock>
    {
        protected override void Write(HtmlRenderer renderer, DocumentImageFigureBlock obj)
        {
            renderer.Write("<figure>"); renderer.WriteLeafInline(obj);
            if (obj.Caption.Length > 0) renderer.Write("<figcaption>").Write(WebUtility.HtmlEncode(obj.Caption)).Write("</figcaption>");
            renderer.Write("</figure>");
        }
    }
    private sealed class ProgressRenderer : HtmlObjectRenderer<DocumentProgressBlock>
    {
        protected override void Write(HtmlRenderer renderer, DocumentProgressBlock obj)
        {
            renderer.Write("<figure class=\"document-progress progress-").Write(obj.Colour).Write("\"><figcaption>").Write(WebUtility.HtmlEncode(obj.Description))
                .Write("</figcaption><progress aria-label=\"").Write(WebUtility.HtmlEncode(obj.Description)).Write("\" max=\"100\" value=\"")
                .Write(obj.Percentage.ToString("0.########", System.Globalization.CultureInfo.InvariantCulture)).Write("\"></progress></figure>");
        }
    }
    private sealed class RawInlineRenderer : HtmlObjectRenderer<HtmlInline>
    {
        protected override void Write(HtmlRenderer renderer, HtmlInline obj) => renderer.Write(WebUtility.HtmlEncode(obj.Tag));
    }
    private sealed class SemanticInlineRenderer : HtmlObjectRenderer<DocumentSemanticInline>
    {
        protected override void Write(HtmlRenderer renderer, DocumentSemanticInline obj)
        {
            var tag = obj.Kind == DocumentInlineKind.StatusBadge ? "strong" : "kbd";
            renderer.Write("<").Write(tag).Write(">").Write(WebUtility.HtmlEncode(obj.Text)).Write("</").Write(tag).Write(">");
        }
    }
    private sealed class AnchorRenderer : HtmlObjectRenderer<DocumentAnchorInline>
    {
        protected override void Write(HtmlRenderer renderer, DocumentAnchorInline obj) => renderer.Write("<span id=\"")
            .Write(WebUtility.HtmlEncode(obj.Id)).Write("\">").Write(WebUtility.HtmlEncode(obj.Text)).Write("</span>");
    }
}
