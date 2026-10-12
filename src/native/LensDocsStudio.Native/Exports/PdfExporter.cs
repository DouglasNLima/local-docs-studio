using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markdig.Extensions.Alerts;
using Markdig.Extensions.Tables;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
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
        foreach (var block in content.Document.Syntax) Append(section.Elements, block);
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
        return fonts.HasSupplementaryCharacters
            ? PdfUnicodeText.PreserveSupplementaryCharacters(bytes, content.AllocationLimits.MaximumOutputBytes, cancellationToken)
            : bytes;

        void Append(DocumentElements elements, MdBlock block, string? marker = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (block)
            {
                case Markdig.Extensions.Mathematics.MathBlock formula:
                    var fp = elements.AddParagraph();
                    if (content.Formulae.TryGetValue(formula.Span.Start, out var formulaImage)) AddImage(fp.Elements, formulaImage);
                    else fonts.AddText(fp.Elements, formula.Lines.ToString()); break;
                case HeadingBlock heading:
                    var hp = elements.AddParagraph(); hp.Style = "Heading" + heading.Level;
                    if (headingTargets.TryGetValue(heading.Span.Start, out var headingTarget)) hp.AddBookmark(bookmarks[headingTarget.Id]);
                    Inlines(hp.Elements, heading.Inline); break;
                case ParagraphBlock paragraph:
                    var p = elements.AddParagraph(); if (marker is not null) fonts.AddText(p.Elements, marker);
                    Inlines(p.Elements, paragraph.Inline); break;
                case FencedCodeBlock code when code.Info is "mermaid" or "mmd":
                    if (!content.Diagrams.TryGetValue(code.Span.Start, out var diagram)) throw new InvalidDataException("A Mermaid diagram could not be exported.");
                    AddImage(elements.AddParagraph().Elements, diagram); break;
                case CodeBlock code:
                    var cp = elements.AddParagraph();
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
                    var index = MarkdownFormatting.OrderedStart(list);
                    foreach (var item in list.OfType<ListItemBlock>())
                    {
                        foreach (var child in item) Append(elements, child, list.IsOrdered ? $"{index}. " : "• ");
                        index++;
                    }
                    break;
                case Markdig.Extensions.Tables.Table table:
                    var wt = elements.AddTable();
                    wt.Borders.Width = Unit.FromPoint(.5); wt.Borders.Color = Colors.Gray;
                    var count = table.OfType<Markdig.Extensions.Tables.TableRow>().Select(r => r.Count).DefaultIfEmpty(1).Max();
                    for (var col = 0; col < count; col++) wt.AddColumn(Unit.FromCentimeter(17d / count));
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
                            foreach (var child in cell) Append(wr.Cells[col].Elements, child); col++;
                        }
                    }
                    break;
                case DocumentDetailsBlock details:
                    var detailsLabel = TextParagraph(elements, details.Summary); detailsLabel.Format.Font.Bold = true; detailsLabel.Format.KeepWithNext = true;
                    foreach (var child in details) Append(elements, child, marker);
                    break;
                case DocumentImageFigureBlock figure:
                    var figureImage = elements.AddParagraph(); Inlines(figureImage.Elements, figure.Inline);
                    figureImage.Format.KeepWithNext = figure.Caption.Length > 0;
                    if (figure.Caption.Length > 0) { var caption = TextParagraph(elements, figure.Caption); caption.Format.Font.Size = 9; }
                    break;
                case DocumentProgressBlock progress:
                    var progressLabel = TextParagraph(elements, progress.Description); progressLabel.Format.KeepWithNext = true;
                    var progressTable = elements.AddTable();
                    var ratio = Math.Round(Math.Clamp(progress.Percentage / 100, 0, 1), 4);
                    var portions = new[] { (ratio, Color.Parse("#" + progress.ColourHex)), (1 - ratio, Colors.WhiteSmoke) }.Where(p => p.Item1 > 0).ToArray();
                    foreach (var portion in portions) progressTable.AddColumn(Unit.FromCentimeter(17 * portion.Item1));
                    var progressRow = progressTable.AddRow(); progressRow.Height = Unit.FromPoint(6); progressRow.HeightRule = MigraDoc.DocumentObjectModel.Tables.RowHeightRule.Exactly;
                    for (var portionIndex = 0; portionIndex < portions.Length; portionIndex++) progressRow.Cells[portionIndex].Shading.Color = portions[portionIndex].Item2;
                    break;
                case HtmlBlock html: TextParagraph(elements, html.Lines.ToString()); break;
                case AlertBlock alert:
                    var label = TextParagraph(elements, MarkdownFormatting.CalloutLabel(alert.Kind.ToString()));
                    label.Format.Font.Bold = true; label.Format.KeepWithNext = true; label.Format.Shading.Color = Colors.WhiteSmoke;
                    foreach (var child in alert) Append(elements, child, marker);
                    break;
                case ContainerBlock container: foreach (var child in container) Append(elements, child, marker); break;
                case ThematicBreakBlock: TextParagraph(elements, "—"); break;
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
        void Inlines(ParagraphElements elements, ContainerInline? container)
        {
            if (container is null) return;
            foreach (var inline in container)
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (inline)
                {
                    case Markdig.Extensions.Mathematics.MathInline formula:
                        if (content.Formulae.TryGetValue(formula.Span.Start, out var formulaImage)) AddImage(elements, formulaImage);
                        else fonts.AddText(elements, formula.Content.ToString()); break;
                    case DocumentAnchorInline anchor:
                        elements.AddBookmark(bookmarks[anchor.Id]); fonts.AddText(elements, anchor.Text); break;
                    case DocumentSemanticInline semantic:
                        var semanticText = elements.AddFormattedText();
                        if (semantic.Kind == DocumentInlineKind.StatusBadge) semanticText.Bold = true;
                        else semanticText.Font.Name = "Consolas";
                        fonts.AddText(semanticText.Elements, semantic.Text, semantic.Kind == DocumentInlineKind.StatusBadge ? "Segoe UI" : "Consolas");
                        break;
                    case LiteralInline literal: fonts.AddText(elements, literal.Content.ToString()); break;
                    case AutolinkInline auto:
                        if (MarkdownLinks.ExternalUri(MarkdownLinks.AutolinkReference(auto)) is { } autoUri)
                            fonts.AddText(elements.AddHyperlink(autoUri.AbsoluteUri, HyperlinkType.Url).Elements, auto.Url);
                        else fonts.AddText(elements, auto.Url);
                        break;
                    case HtmlEntityInline entity: fonts.AddText(elements, entity.Transcoded.ToString()); break;
                    case CodeInline code:
                        var inlineCode = elements.AddFormattedText(); inlineCode.Font.Name = "Consolas";
                        fonts.AddText(inlineCode.Elements, code.Content, "Consolas"); break;
                    case LineBreakInline line: if (line.IsHard) elements.AddLineBreak(); else elements.AddText(" "); break;
                    case Markdig.Extensions.TaskLists.TaskList task: fonts.AddText(elements, task.Checked ? "☑ " : "☐ "); break;
                    case LinkInline { IsImage: true } link:
                        if (!content.Images.TryGetValue(link.Url ?? "", out var image)) throw new InvalidDataException("An image could not be exported.");
                        AddImage(elements, image); break;
                    case LinkInline link when link.Url?.StartsWith('#') == true && bookmarks.TryGetValue(Uri.UnescapeDataString(link.Url[1..]), out var bookmark):
                        Inlines(elements.AddHyperlink(bookmark).Elements, link); break;
                    case LinkInline link when MarkdownLinks.ExternalUri(link.Url) is { } externalUri:
                        Inlines(elements.AddHyperlink(externalUri.AbsoluteUri, HyperlinkType.Url).Elements, link); break;
                    case EmphasisInline emphasis:
                        var formatted = elements.AddFormattedText();
                        var formatting = MarkdownFormatting.Emphasis(emphasis);
                        if (formatting.HasFlag(MarkdownEmphasis.Bold)) formatted.Bold = true;
                        if (formatting.HasFlag(MarkdownEmphasis.Italic)) formatted.Italic = true;
                        if (formatting.HasFlag(MarkdownEmphasis.Underline)) formatted.Underline = Underline.Single;
                        if (formatting.HasFlag(MarkdownEmphasis.Subscript)) formatted.Subscript = true;
                        if (formatting.HasFlag(MarkdownEmphasis.Superscript)) formatted.Superscript = true;
                        // MigraDoc has no strikeout font property; keep explicit Markdown deletion markers in PDF output.
                        if (formatting.HasFlag(MarkdownEmphasis.Strike)) formatted.AddText("~~");
                        Inlines(formatted.Elements, emphasis);
                        if (formatting.HasFlag(MarkdownEmphasis.Strike)) formatted.AddText("~~");
                        break;
                    case HtmlInline html: fonts.AddText(elements, html.Tag); break;
                    case ContainerInline nested: Inlines(elements, nested); break;
                }
            }
        }
        PdfParagraph TextParagraph(DocumentElements elements, string text)
        {
            var paragraph = elements.AddParagraph();
            fonts.AddText(paragraph.Elements, text);
            return paragraph;
        }
        void AddImage(ParagraphElements elements, ExportImage asset)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!imageSources.TryGetValue(asset.PngBytes, out var source))
            {
                source = "base64:" + Convert.ToBase64String(asset.PngBytes);
                imageSources.Add(asset.PngBytes, source);
            }
            var image = elements.AddImage(source);
            image.LockAspectRatio = true;
            var width = Math.Min(482, asset.DisplayWidth > 0 ? asset.DisplayWidth * 72 / 96 : asset.PixelWidth * 72d / 144);
            var height = width * asset.PixelHeight / asset.PixelWidth;
            if (height > 700) width *= 700 / height;
            image.Width = Unit.FromPoint(width);
        }
    }
}
