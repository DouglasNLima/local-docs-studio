using System.Text;
using LensDocsStudio.Core.Parsing;
using MigraDoc.DocumentObjectModel;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.Annotations;

namespace LensDocsStudio.Native.Exports;

internal sealed record PdfTextLink(string Target, bool Bookmark = false);

/// <summary>Uses generated link rectangles to decorate selectable, natively laid-out PDF text.</summary>
internal sealed class PdfTextDecorations(CancellationToken cancellationToken)
{
    private const int MaximumSpans = 65536;
    private const int MaximumRectangles = 131072;
    private readonly string prefix = "https://lens-docs.invalid/pdf-decoration/" + Guid.NewGuid().ToString("N") + "/";
    private readonly Dictionary<string, (MarkdownEmphasis Style, PdfTextLink? Link)> spans = new(StringComparer.Ordinal);
    public bool HasDecorations => spans.Count != 0;

    public ParagraphElements Target(ParagraphElements elements, MarkdownEmphasis style, PdfTextLink? destination)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var decorated = (style & (MarkdownEmphasis.Strike | MarkdownEmphasis.Highlight)) != 0;
        if (!decorated && destination is null) return elements;
        string address;
        if (decorated)
        {
            if (spans.Count >= MaximumSpans) throw new InvalidDataException("PDF text decoration exceeds the supported span limit.");
            address = prefix + spans.Count;
            spans.Add(address, (style, destination));
        }
        else address = destination!.Target;
        var link = elements.AddHyperlink(address, !decorated && destination!.Bookmark ? HyperlinkType.Local : HyperlinkType.Url);
        link.NoHyperlinkStyle = destination is null;
        // Link styles must not erase surrounding explicit Markdown emphasis.
        if (style.HasFlag(MarkdownEmphasis.Bold)) link.Font.Bold = true;
        if (style.HasFlag(MarkdownEmphasis.Italic)) link.Font.Italic = true;
        if (style.HasFlag(MarkdownEmphasis.Underline)) link.Font.Underline = Underline.Single;
        if (style.HasFlag(MarkdownEmphasis.Subscript)) link.Font.Subscript = true;
        if (style.HasFlag(MarkdownEmphasis.Superscript)) link.Font.Superscript = true;
        return link.Elements;
    }

    public void Apply(PdfDocument pdf)
    {
        var count = 0;
        foreach (PdfPage page in pdf.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rectangles = new List<(PdfAnnotation Annotation, PdfRectangle Rectangle, MarkdownEmphasis Style, PdfTextLink? Link)>();
            foreach (PdfAnnotation annotation in page.Annotations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var address = annotation.Elements.GetDictionary("/A")?.Elements.GetString("/URI");
                if (address is null || !address.StartsWith(prefix, StringComparison.Ordinal)) continue;
                if (!spans.TryGetValue(address, out var span)) throw new InvalidDataException("PDF text decoration lost its source mapping.");
                if (++count > MaximumRectangles) throw new InvalidDataException("PDF text decoration exceeds the supported layout limit.");
                var rectangle = annotation.Rectangle;
                if (new[] { rectangle.X1, rectangle.Y1, rectangle.X2, rectangle.Y2 }.Any(value => !double.IsFinite(value) || Math.Abs(value) > 10000000))
                    throw new InvalidDataException("PDF text decoration contains an invalid layout position.");
                rectangles.Add((annotation, rectangle, span.Style, span.Link));
            }
            if (rectangles.Count == 0) continue;
            if (rectangles.Any(item => item.Style.HasFlag(MarkdownEmphasis.Highlight)))
            {
                // Multiplication retains black text and remains visible over table/header shading.
                // Only generated numeric coordinates and a library-generated resource name enter this stream.
                var state = new PdfExtGState(pdf); state.Elements.SetName("/BM", "/Multiply");
                var resourceName = page.Resources.AddExtGState(state);
                var commands = new StringBuilder("q " + resourceName + " gs 0.929411765 0.929411765 0.929411765 rg\n");
                foreach (var item in rectangles.Where(item => item.Style.HasFlag(MarkdownEmphasis.Highlight)))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var rectangle = item.Rectangle;
                    if (rectangle.Width <= 0 || rectangle.Height <= 0) continue;
                    commands.Append(FormattableString.Invariant($"{rectangle.X1:0.###} {rectangle.Y1:0.###} {rectangle.Width:0.###} {rectangle.Height:0.###} re f\n"));
                }
                commands.Append("Q\n");
                page.Contents.AppendContent().CreateStream(Encoding.ASCII.GetBytes(commands.ToString()));
            }
            using (var graphics = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append))
            {
                foreach (var item in rectangles.Where(item => item.Style.HasFlag(MarkdownEmphasis.Strike)))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var rectangle = item.Rectangle;
                    if (rectangle.Width <= 0 || rectangle.Height <= 0) continue;
                    var y = page.Height.Point - rectangle.Y2 + rectangle.Height * .52;
                    graphics.DrawLine(new XPen(XColors.Black, Math.Clamp(rectangle.Height * .045, .4, 1.5)), rectangle.X1, y, rectangle.X2, y);
                }
            }
            foreach (var item in rectangles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                page.Annotations.Remove(item.Annotation);
                pdf.Internals.RemoveObject(item.Annotation);
                if (item.Link is { } link) page.Annotations.Add(link.Bookmark
                    ? PdfLinkAnnotation.CreateDocumentLink(item.Rectangle, link.Target)
                    : PdfLinkAnnotation.CreateWebLink(item.Rectangle, link.Target));
            }
        }
    }
}
