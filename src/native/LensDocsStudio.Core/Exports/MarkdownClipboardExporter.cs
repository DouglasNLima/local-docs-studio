using System.Net;
using System.Text;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LensDocsStudio.Core.Exports;

public sealed class MarkdownClipboardExporter
{
    private sealed record Replacement(int Start, int Length, string Markdown, string Html);

    public ExportedClipboard Export(ExportContent content, CancellationToken cancellationToken = default)
    {
        var source = content.Document.Source;
        if (source.Length > 4 * 1024 * 1024 || content.Images.Values.Concat(content.Diagrams.Values).Sum(i => (long)i.PngBytes.Length) > 16 * 1024 * 1024)
            throw new IOException("Copy Markdown with images is limited to 4 million source characters and 16 MB of prepared images.");
        var replacements = new List<Replacement>();
        var dataUris = new Dictionary<ExportImage, string>();
        string Data(ExportImage image)
        {
            if (!dataUris.TryGetValue(image, out var uri)) dataUris[image] = uri = "data:image/png;base64," + Convert.ToBase64String(image.PngBytes);
            return uri;
        }
        string ImageHtml(ExportImage image, string alt, string? title) => "<img src=\"" + Data(image) + "\" alt=\"" + WebUtility.HtmlEncode(alt) + "\""
            + (string.IsNullOrEmpty(title) ? "" : " title=\"" + WebUtility.HtmlEncode(title) + "\"") + ">";
        long replacementCharacters = 0;
        void Add(Replacement replacement)
        {
            if ((replacementCharacters += Math.Max(replacement.Markdown.Length, replacement.Html.Length)) > HtmlClipboardFormat.MaximumBytes)
                throw new IOException("Repeated images exceed the clipboard size limit. Copy a smaller document.");
            replacements.Add(replacement);
        }
        foreach (var image in content.Document.Syntax.Descendants<LinkInline>().Where(i => i.IsImage))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!content.Images.TryGetValue(image.Url ?? "", out var prepared)) continue;
            Add(new(image.Span.Start, image.Span.Length, MarkdownImageSource.WithDestination(source, image, Data(prepared)),
                ImageHtml(prepared, MarkdownService.InlineText(image), image.Title)));
        }
        foreach (var code in content.Document.Syntax.Descendants<FencedCodeBlock>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (code.ClosingFencedCharCount == 0 || !content.Diagrams.TryGetValue(code.Span.Start, out var image)) continue;
            Add(new(code.Span.Start, code.Span.Length, "![Mermaid diagram](" + Data(image) + ")", ImageHtml(image, "Mermaid diagram", null)));
        }
        var plain = new StringBuilder();
        var html = new StringBuilder("<span data-markdown-clipboard=\"source\" style=\"white-space: pre-wrap\">");
        var position = 0; var index = 0;
        void Literal(string value) { plain.Append(value); html.Append(WebUtility.HtmlEncode(value)); }
        void CheckSize()
        {
            if (plain.Length > HtmlClipboardFormat.MaximumBytes || html.Length > HtmlClipboardFormat.MaximumBytes)
                throw new IOException("The clipboard payload exceeds 32 MB. Copy a smaller document.");
        }
        foreach (var replacement in replacements.OrderBy(r => r.Start))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (replacement.Start < position || replacement.Length <= 0 || (long)replacement.Start + replacement.Length > source.Length)
                throw new IOException("Markdown clipboard image positions overlap or are invalid.");
            Literal(source[position..replacement.Start]);
            plain.Append(replacement.Markdown);
            html.Append(replacement.Html.Insert(4, " data-markdown-clipboard-image=\"" + index++ + "\""));
            position = replacement.Start + replacement.Length;
            CheckSize();
        }
        Literal(source[position..]); html.Append("</span>"); CheckSize();
        if (Encoding.UTF8.GetByteCount(plain.ToString()) > HtmlClipboardFormat.MaximumBytes) throw new IOException("The Unicode clipboard text exceeds 32 MB.");
        return new(plain.ToString(), HtmlClipboardFormat.Encode(html.ToString()));
    }
}
