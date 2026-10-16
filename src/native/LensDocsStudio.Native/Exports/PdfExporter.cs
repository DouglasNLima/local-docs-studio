using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markdig.Extensions.Alerts;
using Markdig.Extensions.Tables;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using PdfSharp.Drawing;
using MdBlock = Markdig.Syntax.Block;
using PdfParagraph = MigraDoc.DocumentObjectModel.Paragraph;

namespace LensDocsStudio.Native.Exports;

public sealed class PdfExporter
{
    public byte[] Export(ExportContent content, CancellationToken cancellationToken = default)
    {
        content.AllocationLimits.Validate(content, cancellationToken: cancellationToken);
        var codeSyntax = content.CodeSyntax ?? CodeSyntaxService.Prepare(content.Document, cancellationToken);
        var fonts = new PdfFontFallback(cancellationToken);
        var decorations = new PdfTextDecorations(cancellationToken);
        var imageSources = new Dictionary<byte[], string>(ReferenceEqualityComparer.Instance);
        var document = new Document();
        document.Info.Title = content.Document.Headings.FirstOrDefault()?.Title ?? "Documentation";
        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = "Segoe UI"; normal.Font.Size = 10;
        normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(7);
        for (var level = 1; level <= 6; level++)
        {
            var style = document.Styles["Heading" + level]!;
            style.Font.Name = "Segoe UI"; style.Font.Bold = true; style.Font.Size = Math.Max(11, 25 - level * 3);
            style.ParagraphFormat.KeepWithNext = true; style.ParagraphFormat.SpaceBefore = Unit.FromPoint(12);
        }
        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.TopMargin = section.PageSetup.BottomMargin = Unit.FromCentimeter(2);
        section.PageSetup.LeftMargin = section.PageSetup.RightMargin = Unit.FromCentimeter(2);
        section.Footers.Primary.AddParagraph().AddPageField();
        var bookmarks = content.Document.Targets.ToDictionary(t => t.Id, t => "LensTarget" + t.SourceStart, StringComparer.Ordinal);
        var headingTargets = content.Document.Targets.Where(t => t.HeadingLevel > 0).ToDictionary(t => t.SourceStart);
        foreach (var block in content.Document.Syntax) Append(section.Elements, block, availableWidth: Unit.FromCentimeter(17).Point);
        using var pdf = new PdfSharp.Pdf.PdfDocument();
        // Inspect the actual PDF glyph mapping too, including inherited bold/italic faces.
        pdf.RenderEvents.RenderTextEvent += (_, args) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var pair in args.CodePointGlyphIndexPairs)
                if (pair.GlyphIndex == 0) throw PdfFontFallback.MissingGlyph(pair.CodePoint);
        };
        var renderer = new PdfDocumentRenderer { Document = document, PdfDocument = pdf };
        cancellationToken.ThrowIfCancellationRequested();
        renderer.RenderDocument();
        cancellationToken.ThrowIfCancellationRequested();
        using var output = new BoundedExportStream(content.AllocationLimits.MaximumOutputBytes, cancellationToken);
        renderer.PdfDocument.Save(output, false);
        var bytes = output.ToArray();
        return fonts.HasSupplementaryCharacters || decorations.HasDecorations
            ? PdfExportPostProcessing.Complete(bytes, content.AllocationLimits.MaximumOutputBytes, cancellationToken,
                fonts.HasSupplementaryCharacters, decorations.HasDecorations ? decorations : null)
            : bytes;

        void Append(DocumentElements elements, MdBlock block, ListItemLayout? listItem = null, double availableWidth = 482)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var textWidth = availableWidth - (listItem?.Indent ?? 0);
            switch (block)
            {
                case Markdig.Extensions.Mathematics.MathBlock formula:
                    var fp = Paragraph(elements, listItem);
                    if (content.Formulae.TryGetValue(formula.Span.Start, out var formulaImage)) AddImage(fp.Elements, formulaImage, textWidth);
                    else fonts.AddText(fp.Elements, formula.Lines.ToString()); break;
                case HeadingBlock heading:
                    var hp = Paragraph(elements, listItem); hp.Style = "Heading" + heading.Level;
                    if (headingTargets.TryGetValue(heading.Span.Start, out var headingTarget)) hp.AddBookmark(bookmarks[headingTarget.Id]);
                    Inlines(hp.Elements, heading.Inline, availableWidth: textWidth); break;
                case ParagraphBlock paragraph:
                    var p = Paragraph(elements, listItem);
                    Inlines(p.Elements, paragraph.Inline, availableWidth: textWidth); break;
                case FencedCodeBlock code when code.Info is "mermaid" or "mmd":
                    if (!content.Diagrams.TryGetValue(code.Span.Start, out var diagram)) throw new InvalidDataException("A Mermaid diagram could not be exported.");
                    AddImage(Paragraph(elements, listItem).Elements, diagram, textWidth); break;
                case CodeBlock code:
                    var cp = Paragraph(elements, listItem);
                    cp.Format.Font.Name = "Consolas"; cp.Format.Font.Size = 8; cp.Format.Shading.Color = Colors.WhiteSmoke;
                    var codeText = code.Lines.ToString();
                    if (codeSyntax.Blocks.TryGetValue(code, out var syntax) && syntax.Text == codeText)
                    {
                        var position = 0;
                        foreach (var span in syntax.Spans)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (span.Start > position) CodeText(cp.Elements, codeText[position..span.Start]);
                            var colour = ((System.Windows.Media.SolidColorBrush)CodeSyntaxColours.For(span.Role, false)).Color;
                            CodeText(cp.Elements, codeText.Substring(span.Start, span.Length), Color.FromRgb(colour.R, colour.G, colour.B));
                            position = span.Start + span.Length;
                        }
                        if (position < codeText.Length) CodeText(cp.Elements, codeText[position..]);
                    }
                    else CodeText(cp.Elements, codeText);
                    cp.Format.KeepTogether = false; break;
                case ListBlock list:
                {
                    // Each item owns one pending marker, shared by its continuation blocks.
                    // A nested list receives a fresh scope without consuming later parent text.
                    EnsureListAnchor(elements, listItem);
                    var index = MarkdownFormatting.OrderedStart(list);
                    var widestMarker = list.IsOrdered ? ((long)index + Math.Max(0, list.Count - 1)).ToString(System.Globalization.CultureInfo.InvariantCulture) + "." : "•";
                    using var measure = XGraphics.CreateMeasureContext(new XSize(1, 1), XGraphicsUnit.Point, XPageDirection.Downwards);
                    var markerWidth = Math.Max(18, measure.MeasureString(widestMarker, new XFont("Segoe UI", 10)).Width + 6);
                    var indent = (listItem?.Indent ?? 0) + markerWidth;
                    if (availableWidth - indent < 36) throw new IOException("The nested PDF list leaves too little space for readable content.");
                    foreach (var item in list.OfType<ListItemBlock>())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var layout = new ListItemLayout(indent, markerWidth, list.IsOrdered ? index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "." : "•");
                        foreach (var child in item) Append(elements, child, layout, availableWidth);
                        if (layout.Marker is not null) Paragraph(elements, layout);
                        index++;
                    }
                    break;
                }
                case Markdig.Extensions.Tables.Table table:
                    EnsureListAnchor(elements, listItem);
                    var wt = elements.AddTable();
                    if (listItem is not null) wt.Rows.LeftIndent = Unit.FromPoint(listItem.Indent);
                    wt.Borders.Width = Unit.FromPoint(.5); wt.Borders.Color = Colors.Gray;
                    var count = table.OfType<Markdig.Extensions.Tables.TableRow>().Select(r => r.Count).DefaultIfEmpty(1).Max();
                    var columnWidth = textWidth / count;
                    for (var col = 0; col < count; col++) wt.AddColumn(Unit.FromPoint(columnWidth));
                    foreach (var row in table.OfType<Markdig.Extensions.Tables.TableRow>())
                    {
                        var wr = wt.AddRow(); wr.HeadingFormat = row.IsHeader;
                        if (row.IsHeader) { wr.Shading.Color = Colors.WhiteSmoke; wr.Format.Font.Bold = true; }
                        var col = 0;
                        foreach (var cell in row.OfType<Markdig.Extensions.Tables.TableCell>())
                        {
                            var columnIndex = cell.ColumnIndex >= 0 ? cell.ColumnIndex : col;
                            wr.Cells[col].Format.Alignment = columnIndex < table.ColumnDefinitions.Count ? table.ColumnDefinitions[columnIndex].Alignment switch
                            { TableColumnAlign.Center => ParagraphAlignment.Center, TableColumnAlign.Right => ParagraphAlignment.Right, _ => ParagraphAlignment.Left } : ParagraphAlignment.Left;
                            foreach (var child in cell) Append(wr.Cells[col].Elements, child, availableWidth: Math.Max(1, columnWidth - 7)); col++;
                        }
                    }
                    break;
                case DocumentDetailsBlock details:
                    var detailsLabel = TextParagraph(elements, details.Summary, listItem); detailsLabel.Format.Font.Bold = true; detailsLabel.Format.KeepWithNext = true;
                    foreach (var child in details) Append(elements, child, listItem, availableWidth);
                    break;
                case DocumentImageFigureBlock figure:
                    var figureImage = Paragraph(elements, listItem); Inlines(figureImage.Elements, figure.Inline, availableWidth: textWidth);
                    figureImage.Format.KeepWithNext = figure.Caption.Length > 0;
                    if (figure.Caption.Length > 0) { var caption = TextParagraph(elements, figure.Caption, listItem); caption.Format.Font.Size = 9; }
                    break;
                case DocumentProgressBlock progress:
                    var progressLabel = TextParagraph(elements, progress.Description, listItem); progressLabel.Format.KeepWithNext = true;
                    var progressTable = elements.AddTable();
                    if (listItem is not null) progressTable.Rows.LeftIndent = Unit.FromPoint(listItem.Indent);
                    var ratio = Math.Round(Math.Clamp(progress.Percentage / 100, 0, 1), 4);
                    var portions = new[] { (ratio, Color.Parse("#" + progress.ColourHex)), (1 - ratio, Colors.WhiteSmoke) }.Where(p => p.Item1 > 0).ToArray();
                    foreach (var portion in portions) progressTable.AddColumn(Unit.FromPoint(textWidth * portion.Item1));
                    var progressRow = progressTable.AddRow(); progressRow.Height = Unit.FromPoint(6); progressRow.HeightRule = MigraDoc.DocumentObjectModel.Tables.RowHeightRule.Exactly;
                    for (var portionIndex = 0; portionIndex < portions.Length; portionIndex++) progressRow.Cells[portionIndex].Shading.Color = portions[portionIndex].Item2;
                    break;
                case HtmlBlock html: TextParagraph(elements, html.Lines.ToString(), listItem); break;
                case AlertBlock alert:
                    var label = TextParagraph(elements, MarkdownFormatting.CalloutLabel(alert.Kind.ToString()), listItem);
                    label.Format.Font.Bold = true; label.Format.KeepWithNext = true; label.Format.Shading.Color = Colors.WhiteSmoke;
                    foreach (var child in alert) Append(elements, child, listItem, availableWidth);
                    break;
                case ContainerBlock container: foreach (var child in container) Append(elements, child, listItem, availableWidth); break;
                case ThematicBreakBlock: TextParagraph(elements, "—", listItem); break;
            }
        }
        void CodeText(ParagraphElements elements, string text, Color? colour = null)
        {
            if (colour is { } foreground)
            {
                var formatted = elements.AddFormattedText(); formatted.Font.Color = foreground;
                elements = formatted.Elements;
            }
            for (var start = 0; start < text.Length;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var spaces = text[start] == ' '; var end = start + 1;
                while (end < text.Length && (text[end] == ' ') == spaces) end++;
                // Explicit space characters preserve code indentation and alignment without
                // replacing source spaces with non-breaking Unicode characters in the export.
                if (spaces) elements.AddSpace(end - start);
                else fonts.AddText(elements, text[start..end], "Consolas");
                start = end;
            }
        }
        void Inlines(ParagraphElements elements, ContainerInline? container, MarkdownEmphasis inherited = MarkdownEmphasis.None, PdfTextLink? destination = null, double availableWidth = 482)
        {
            if (container is null) return;
            foreach (var inline in container)
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (inline)
                {
                    case Markdig.Extensions.Mathematics.MathInline formula:
                        if (content.Formulae.TryGetValue(formula.Span.Start, out var formulaImage)) AddImage(Target(), formulaImage, availableWidth);
                        else fonts.AddText(Target(), formula.Content.ToString()); break;
                    case DocumentAnchorInline anchor:
                        elements.AddBookmark(bookmarks[anchor.Id]); fonts.AddText(Target(), anchor.Text); break;
                    case DocumentSemanticInline semantic:
                        var semanticText = Target().AddFormattedText();
                        if (semantic.Kind == DocumentInlineKind.StatusBadge) semanticText.Bold = true;
                        else semanticText.Font.Name = "Consolas";
                        fonts.AddText(semanticText.Elements, semantic.Text, semantic.Kind == DocumentInlineKind.StatusBadge ? "Segoe UI" : "Consolas");
                        break;
                    case LiteralInline literal: fonts.AddText(Target(), literal.Content.ToString()); break;
                    case AutolinkInline auto:
                        if (MarkdownLinks.ExternalUri(MarkdownLinks.AutolinkReference(auto)) is { } autoUri)
                            fonts.AddText(decorations.Target(elements, inherited, new(autoUri.AbsoluteUri)), auto.Url);
                        else fonts.AddText(Target(), auto.Url);
                        break;
                    case HtmlEntityInline entity: fonts.AddText(Target(), entity.Transcoded.ToString()); break;
                    case CodeInline code:
                        var inlineCode = Target().AddFormattedText(); inlineCode.Font.Name = "Consolas";
                        fonts.AddText(inlineCode.Elements, code.Content, "Consolas"); break;
                    case LineBreakInline line: if (line.IsHard) elements.AddLineBreak(); else Target().AddText(" "); break;
                    case Markdig.Extensions.TaskLists.TaskList task: fonts.AddText(Target(), task.Checked ? "☑ " : "☐ "); break;
                    case LinkInline { IsImage: true } link:
                        if (!content.Images.TryGetValue(link.Url ?? "", out var image)) throw new InvalidDataException("An image could not be exported.");
                        AddImage(Target(), image, availableWidth); break;
                    case LinkInline link when link.Url?.StartsWith('#') == true && bookmarks.TryGetValue(Uri.UnescapeDataString(link.Url[1..]), out var bookmark):
                        Inlines(elements, link, inherited, new(bookmark, Bookmark: true), availableWidth); break;
                    case LinkInline link when MarkdownLinks.ExternalUri(link.Url) is { } externalUri:
                        Inlines(elements, link, inherited, new(externalUri.AbsoluteUri), availableWidth); break;
                    case EmphasisInline emphasis:
                        var formatted = elements.AddFormattedText();
                        var formatting = MarkdownFormatting.Emphasis(emphasis);
                        if (formatting.HasFlag(MarkdownEmphasis.Bold)) formatted.Bold = true;
                        if (formatting.HasFlag(MarkdownEmphasis.Italic)) formatted.Italic = true;
                        if (formatting.HasFlag(MarkdownEmphasis.Underline)) formatted.Underline = Underline.Single;
                        if (formatting.HasFlag(MarkdownEmphasis.Subscript)) formatted.Subscript = true;
                        if (formatting.HasFlag(MarkdownEmphasis.Superscript)) formatted.Superscript = true;
                        Inlines(formatted.Elements, emphasis, inherited | formatting, destination, availableWidth);
                        break;
                    case HtmlInline html: fonts.AddText(Target(), html.Tag); break;
                    case ContainerInline nested: Inlines(elements, nested, inherited, destination, availableWidth); break;
                }
            }
            ParagraphElements Target() => decorations.Target(elements, inherited, destination);
        }
        PdfParagraph Paragraph(DocumentElements elements, ListItemLayout? listItem)
        {
            var paragraph = elements.AddParagraph();
            if (listItem is null) return paragraph;
            paragraph.Format.LeftIndent = Unit.FromPoint(listItem.Indent);
            if (listItem.Marker is { } marker)
            {
                paragraph.Format.FirstLineIndent = Unit.FromPoint(-listItem.MarkerWidth);
                paragraph.Format.TabStops.AddTabStop(Unit.FromPoint(listItem.Indent));
                var label = paragraph.AddFormattedText();
                label.Font.Name = "Segoe UI"; label.Font.Size = 10; label.Bold = false; label.Italic = false;
                fonts.AddText(label.Elements, marker); paragraph.AddTab(); listItem.Marker = null;
            }
            return paragraph;
        }
        void EnsureListAnchor(DocumentElements elements, ListItemLayout? listItem)
        {
            if (listItem?.Marker is null) return;
            var anchor = Paragraph(elements, listItem);
            anchor.Format.SpaceAfter = 0; anchor.Format.KeepWithNext = true;
        }
        PdfParagraph TextParagraph(DocumentElements elements, string text, ListItemLayout? listItem = null)
        {
            var paragraph = Paragraph(elements, listItem);
            fonts.AddText(paragraph.Elements, text);
            return paragraph;
        }
        void AddImage(ParagraphElements elements, ExportImage asset, double availableWidth = 482)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!imageSources.TryGetValue(asset.PngBytes, out var source))
            {
                source = "base64:" + Convert.ToBase64String(asset.PngBytes);
                imageSources.Add(asset.PngBytes, source);
            }
            var image = elements.AddImage(source);
            image.LockAspectRatio = true;
            var width = Math.Min(availableWidth, asset.DisplayWidth > 0 ? asset.DisplayWidth * 72 / 96 : asset.PixelWidth * 72d / 144);
            var height = width * asset.PixelHeight / asset.PixelWidth;
            if (height > 700) width *= 700 / height;
            image.Width = Unit.FromPoint(width);
        }
    }

    private sealed class ListItemLayout(double indent, double markerWidth, string marker)
    {
        public double Indent { get; } = indent;
        public double MarkerWidth { get; } = markerWidth;
        public string? Marker { get; set; } = marker;
    }
}
