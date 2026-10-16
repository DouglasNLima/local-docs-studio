using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markdig.Extensions.Alerts;
using Markdig.Extensions.Tables;
using W = DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace LensDocsStudio.Core.Exports;

public sealed class DocxExporter
{
    public byte[] Export(ExportContent content, byte[]? template = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        content.AllocationLimits.Validate(content, cancellationToken: cancellationToken);
        ValidateCodeAllocation(content, cancellationToken);
        using var stream = new BoundedExportStream(content.AllocationLimits.MaximumOutputBytes, cancellationToken);
        if (template is not null) stream.Write(new DocxTemplateService().Prepare(template, cancellationToken).Bytes);
        using (var package = template is null
            ? WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true)
            : WordprocessingDocument.Open(stream, true))
        {
            var main = package.MainDocumentPart ?? package.AddMainDocumentPart();
            var imageParts = new Dictionary<byte[], ImagePart>(ReferenceEqualityComparer.Instance);
            if (main.VbaProjectPart is not null) throw new InvalidDataException("Macro-enabled templates are not supported.");
            ValidateRelationships(main, new HashSet<OpenXmlPart>());
            main.Document ??= new W.Document(new W.Body());
            var body = main.Document.Body ?? main.Document.AppendChild(new W.Body());
            var section = body.Descendants<W.SectionProperties>().LastOrDefault()?.CloneNode(true) as W.SectionProperties
                ?? new W.SectionProperties(new W.PageSize { Width = 11906, Height = 16838 }, new W.PageMargin { Top = 1134, Bottom = 1134, Left = 1134, Right = 1134, Header = 567, Footer = 567, Gutter = 0 });
            var tablePrototype = body.Descendants<W.Table>().FirstOrDefault();
            var tableProperties = tablePrototype?.TableProperties?.CloneNode(true) as W.TableProperties;
            body.RemoveAllChildren();
            var styles = main.StyleDefinitionsPart ?? main.AddNewPart<StyleDefinitionsPart>();
            styles.Styles ??= new W.Styles();
            var styleMap = new DocxStyleMap(styles.Styles);
            var bodyStyle = styleMap.Body;
            var codeStyle = styleMap.Code;
            var numbering = main.NumberingDefinitionsPart ?? main.AddNewPart<NumberingDefinitionsPart>();
            numbering.Numbering ??= new W.Numbering();
            var usedNumbers = numbering.Numbering.Elements<W.NumberingInstance>().Select(n => n.NumberID?.Value ?? 0).ToHashSet();
            var usedAbstracts = numbering.Numbering.Elements<W.AbstractNum>().Select(n => n.AbstractNumberId?.Value ?? 0).ToHashSet();
            var usedDrawings = PartElements<DW.DocProperties>(main, new HashSet<OpenXmlPart>()).Select(p => p.Id?.Value ?? 0).ToHashSet();
            var nextNumber = 1; var nextAbstract = 1; uint drawingId = 1;
            var pageSize = section.GetFirstChild<W.PageSize>();
            var margins = section.GetFirstChild<W.PageMargin>();
            var widthEmu = ((long)(pageSize?.Width?.Value ?? 11906) - (margins?.Left?.Value ?? 1134) - (margins?.Right?.Value ?? 1134)) * 635;
            var heightEmu = ((long)(pageSize?.Height?.Value ?? 16838) - (margins?.Top?.Value ?? 1134) - (margins?.Bottom?.Value ?? 1134)) * 635;
            var retainedBookmarks = PartElements<W.BookmarkStart>(main, new HashSet<OpenXmlPart>()).ToArray();
            var bookmarkNames = new HashSet<string>(retainedBookmarks.Select(b => b.Name?.Value ?? ""), StringComparer.Ordinal);
            var bookmarkIds = new HashSet<string>(retainedBookmarks.Select(b => b.Id?.Value ?? ""), StringComparer.Ordinal);
            var bookmarks = new Dictionary<string, (string Name, string Id)>(StringComparer.Ordinal);
            var headingTargets = content.Document.Targets.Where(t => t.HeadingLevel > 0).ToDictionary(t => t.SourceStart);
            var nextBookmark = 0;
            foreach (var target in content.Document.Targets)
            {
                string id, name;
                do { id = (++nextBookmark).ToString(System.Globalization.CultureInfo.InvariantCulture); name = "LensTarget" + id; }
                while (bookmarkIds.Contains(id) || bookmarkNames.Contains(name));
                bookmarks.Add(target.Id, (name, id)); bookmarkIds.Add(id); bookmarkNames.Add(name);
            }

            foreach (var block in content.Document.Syntax) { cancellationToken.ThrowIfCancellationRequested(); AppendBlock(body, block); }
            body.Append(section);
            package.PackageProperties.Title = content.Document.Headings.FirstOrDefault()?.Title ?? "Documentation";
            var settings = main.DocumentSettingsPart ?? main.AddNewPart<DocumentSettingsPart>();
            settings.Settings ??= new W.Settings();
            settings.Settings.RemoveAllChildren<W.UpdateFieldsOnOpen>();
            // Word warns about external files even when this flag is set on a field-free document.
            if (HasFields(main.Document) || main.HeaderParts.Any(part => HasFields(part.Header)) || main.FooterParts.Any(part => HasFields(part.Footer)))
                settings.Settings.Append(new W.UpdateFieldsOnOpen { Val = true });
            main.Document.Save(); styles.Styles.Save(); numbering.Numbering.Save(); settings.Settings.Save();

            static bool HasFields(OpenXmlElement? element) => element is not null
                && (element.Descendants<W.SimpleField>().Any() || element.Descendants<W.FieldChar>().Any());

            void AppendBlock(OpenXmlCompositeElement parent, Block block, ListItemContext? listItem = null, string? paragraphStyle = null, long? containerWidth = null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var availableWidth = containerWidth ?? widthEmu;
                var textWidth = availableWidth - (listItem?.IndentTwips ?? 0) * 635L;
                switch (block)
                {
                    case Markdig.Extensions.Mathematics.MathBlock formula:
                        AppendParagraph(parent, new W.Paragraph(content.Formulae.TryGetValue(formula.Span.Start, out var formulaImage)
                            ? Image(formulaImage) : Run(formula.Lines.ToString(), MarkdownEmphasis.None)), listItem, availableWidth); break;
                    case HeadingBlock heading:
                        var hp = Paragraph(heading.Inline, styleMap.Heading(heading.Level));
                        hp.ParagraphProperties!.KeepNext = new W.KeepNext();
                        if (headingTargets.TryGetValue(heading.Span.Start, out var headingTarget))
                        {
                            var bookmark = bookmarks[headingTarget.Id];
                            hp.InsertAfter(new W.BookmarkStart { Id = bookmark.Id, Name = bookmark.Name }, hp.ParagraphProperties);
                            hp.Append(new W.BookmarkEnd { Id = bookmark.Id });
                        }
                        AppendParagraph(parent, hp, listItem, availableWidth); break;
                    case ParagraphBlock paragraph:
                        var p = Paragraph(paragraph.Inline, paragraphStyle ?? bodyStyle);
                        AppendParagraph(parent, p, listItem, availableWidth); break;
                    case FencedCodeBlock code when code.Info is "mermaid" or "mmd":
                        if (!content.Diagrams.TryGetValue(code.Span.Start, out var diagram)) throw new InvalidDataException("A Mermaid diagram could not be exported.");
                        AppendParagraph(parent, new W.Paragraph(Image(diagram)), listItem, availableWidth); break;
                    case CodeBlock code:
                        var cp = new W.Paragraph(new W.ParagraphProperties(new W.ParagraphStyleId { Val = codeStyle }));
                        CodeText(cp, code);
                        AppendParagraph(parent, cp, listItem, availableWidth); break;
                    case ListBlock list:
                        EnsureListAnchor(parent, listItem, availableWidth);
                        var depth = listItem is null ? 0 : listItem.Depth + 1;
                        if (availableWidth - (depth + 1) * 360L * 635 < 720 * 635L)
                            throw new IOException("The nested Word list leaves too little space for readable content.");
                        while (usedNumbers.Contains(nextNumber)) nextNumber++;
                        while (usedAbstracts.Contains(nextAbstract)) nextAbstract++;
                        var id = nextNumber++; var abstractId = nextAbstract++;
                        usedNumbers.Add(id); usedAbstracts.Add(abstractId);
                        var definition = new W.AbstractNum { AbstractNumberId = abstractId };
                        for (var level = 0; level < 9; level++) definition.Append(new W.Level(
                            new W.StartNumberingValue { Val = MarkdownFormatting.OrderedStart(list) },
                            new W.NumberingFormat { Val = list.IsOrdered ? W.NumberFormatValues.Decimal : W.NumberFormatValues.Bullet },
                            new W.LevelText { Val = list.IsOrdered ? "%" + (level + 1) + "." : "•" },
                            new W.LevelJustification { Val = W.LevelJustificationValues.Left },
                            new W.PreviousParagraphProperties(new W.Indentation { Left = ((level + 1) * 360).ToString(), Hanging = "240" })) { LevelIndex = level });
                        // Word's optional cleanup marker must remain after every numbering definition/instance.
                        numbering.Numbering.InsertBefore(definition, numbering.Numbering.ChildElements.FirstOrDefault(element => element is W.NumberingInstance or W.NumberingIdMacAtCleanup));
                        numbering.Numbering.InsertBefore(new W.NumberingInstance(new W.AbstractNumId { Val = abstractId }) { NumberID = id }, numbering.Numbering.GetFirstChild<W.NumberingIdMacAtCleanup>());
                        foreach (var item in list.OfType<ListItemBlock>())
                        {
                            var context = new ListItemContext(id, depth);
                            foreach (var child in item) AppendBlock(parent, child, context, paragraphStyle, availableWidth);
                            if (context.MarkerPending) AppendParagraph(parent, Paragraph(null, paragraphStyle ?? bodyStyle), context, availableWidth);
                        }
                        break;
                    case Markdig.Extensions.Tables.Table table:
                        EnsureListAnchor(parent, listItem, availableWidth);
                        var wt = new W.Table();
                        wt.Append(tableProperties?.CloneNode(true) ?? new W.TableProperties(new W.TableWidth { Width = "5000", Type = W.TableWidthUnitValues.Pct },
                            new W.TableBorders(new W.TopBorder { Val = W.BorderValues.Single, Size = 4 }, new W.LeftBorder { Val = W.BorderValues.Single, Size = 4 },
                                new W.BottomBorder { Val = W.BorderValues.Single, Size = 4 }, new W.RightBorder { Val = W.BorderValues.Single, Size = 4 },
                                new W.InsideHorizontalBorder { Val = W.BorderValues.Single, Size = 4 }, new W.InsideVerticalBorder { Val = W.BorderValues.Single, Size = 4 })));
                        wt.TableProperties!.TableStyle ??= new W.TableStyle { Val = styleMap.Table };
                        var tableWidth = Math.Max(1, textWidth / 635);
                        var requestedWidth = wt.TableProperties.TableWidth;
                        if (long.TryParse(requestedWidth?.Width?.Value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var requested) && requested > 0)
                            tableWidth = requestedWidth?.Type?.Value == W.TableWidthUnitValues.Pct
                                ? Math.Max(1, tableWidth * Math.Min(5000, requested) / 5000) : Math.Min(tableWidth, requested);
                        if (listItem is not null || availableWidth != widthEmu)
                        {
                            wt.TableProperties.TableWidth = new W.TableWidth { Width = tableWidth.ToString(System.Globalization.CultureInfo.InvariantCulture), Type = W.TableWidthUnitValues.Dxa };
                            if (listItem is not null) wt.TableProperties.TableIndentation = new W.TableIndentation { Width = listItem.IndentTwips, Type = W.TableWidthUnitValues.Dxa };
                        }
                        var columns = table.OfType<Markdig.Extensions.Tables.TableRow>().Select(r => r.Count).DefaultIfEmpty(1).Max();
                        var cellMargins = wt.TableProperties.TableCellMarginDefault;
                        var horizontalCellMargins = CellMargin(cellMargins?.StartMargin ?? (OpenXmlElement?)cellMargins?.TableCellLeftMargin)
                            + CellMargin(cellMargins?.EndMargin ?? (OpenXmlElement?)cellMargins?.TableCellRightMargin);
                        wt.Append(new W.TableGrid(Enumerable.Range(0, columns).Select(_ => new W.GridColumn { Width = (tableWidth / columns).ToString(System.Globalization.CultureInfo.InvariantCulture) })));
                        foreach (var row in table.OfType<Markdig.Extensions.Tables.TableRow>())
                        {
                            var wr = new W.TableRow();
                            if (row.IsHeader) wr.Append(new W.TableRowProperties(new W.TableHeader()));
                            var column = 0;
                            foreach (var cell in row.OfType<Markdig.Extensions.Tables.TableCell>())
                            {
                                var wc = new W.TableCell();
                                foreach (var child in cell) AppendBlock(wc, child, containerWidth: Math.Max(1, tableWidth / columns - horizontalCellMargins) * 635);
                                if (!wc.Elements<W.Paragraph>().Any()) wc.Append(new W.Paragraph());
                                var columnIndex = cell.ColumnIndex >= 0 ? cell.ColumnIndex : column;
                                var alignment = columnIndex < table.ColumnDefinitions.Count ? table.ColumnDefinitions[columnIndex].Alignment : null;
                                column += Math.Max(1, cell.ColumnSpan);
                                foreach (var paragraph in wc.Elements<W.Paragraph>())
                                {
                                    paragraph.ParagraphProperties ??= new W.ParagraphProperties();
                                    paragraph.ParagraphProperties.Justification = new W.Justification { Val = alignment switch
                                    { TableColumnAlign.Center => W.JustificationValues.Center, TableColumnAlign.Right => W.JustificationValues.Right, _ => W.JustificationValues.Left } };
                                }
                                wr.Append(wc);
                            }
                            wt.Append(wr);
                        }
                        parent.Append(wt); break;
                    case DocumentDetailsBlock details:
                        AppendParagraph(parent, new W.Paragraph(new W.ParagraphProperties(new W.ParagraphStyleId { Val = bodyStyle }) { KeepNext = new W.KeepNext() },
                            Run(details.Summary, MarkdownEmphasis.Bold)), listItem, availableWidth);
                        foreach (var child in details) AppendBlock(parent, child, listItem, paragraphStyle, availableWidth);
                        break;
                    case DocumentImageFigureBlock figure:
                        var picture = Paragraph(figure.Inline, bodyStyle);
                        if (figure.Caption.Length > 0) picture.ParagraphProperties!.KeepNext = new W.KeepNext();
                        AppendParagraph(parent, picture, listItem, availableWidth);
                        if (figure.Caption.Length > 0) AppendParagraph(parent, new W.Paragraph(new W.ParagraphProperties(new W.ParagraphStyleId { Val = styleMap.Caption }), Run(figure.Caption, MarkdownEmphasis.None)), listItem, availableWidth);
                        break;
                    case DocumentProgressBlock progress:
                        AppendParagraph(parent, new W.Paragraph(new W.ParagraphProperties(new W.ParagraphStyleId { Val = bodyStyle }) { KeepNext = new W.KeepNext() }, Run(progress.Description, MarkdownEmphasis.None)), listItem, availableWidth);
                        var progressTable = new W.Table(new W.TableProperties(new W.TableWidth { Type = W.TableWidthUnitValues.Pct, Width = "5000" },
                            new W.TableLayout { Type = W.TableLayoutValues.Fixed }, new W.TableCellMarginDefault(new W.TopMargin { Type = W.TableWidthUnitValues.Dxa, Width = "0" }, new W.BottomMargin { Type = W.TableWidthUnitValues.Dxa, Width = "0" })));
                        if (listItem is not null)
                        {
                            progressTable.TableProperties!.TableWidth = new W.TableWidth { Type = W.TableWidthUnitValues.Dxa, Width = (textWidth / 635).ToString(System.Globalization.CultureInfo.InvariantCulture) };
                            progressTable.TableProperties.TableIndentation = new W.TableIndentation { Width = listItem.IndentTwips, Type = W.TableWidthUnitValues.Dxa };
                        }
                        var progressGrid = new W.TableGrid(); var progressRow = new W.TableRow(new W.TableRowProperties(new W.TableRowHeight { Val = 120, HeightType = W.HeightRuleValues.Exact }));
                        var fill = Math.Clamp((int)Math.Round(progress.Percentage * 50), 0, 5000);
                        foreach (var (amount, colour) in new[] { (fill, progress.ColourHex), (5000 - fill, "EDEDED") })
                        {
                            if (amount == 0) continue;
                            progressGrid.Append(new W.GridColumn { Width = Math.Max(1, textWidth / 635 * amount / 5000).ToString(System.Globalization.CultureInfo.InvariantCulture) });
                            progressRow.Append(new W.TableCell(new W.TableCellProperties(new W.TableCellWidth { Type = W.TableWidthUnitValues.Pct, Width = amount.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                                new W.Shading { Val = W.ShadingPatternValues.Clear, Fill = colour }), new W.Paragraph(new W.ParagraphProperties(new W.SpacingBetweenLines { Before = "0", After = "0", Line = "20", LineRule = W.LineSpacingRuleValues.Exact }))));
                        }
                        progressTable.Append(progressGrid, progressRow); parent.Append(progressTable); break;
                    case HtmlBlock html:
                        AppendParagraph(parent, new W.Paragraph(new W.Run(new W.Text(html.Lines.ToString()) { Space = SpaceProcessingModeValues.Preserve })), listItem, availableWidth); break;
                    case AlertBlock alert:
                        AppendParagraph(parent, new W.Paragraph(new W.ParagraphProperties { KeepNext = new W.KeepNext(), Shading = new W.Shading { Val = W.ShadingPatternValues.Clear, Fill = "F3F4F6" },
                            ParagraphBorders = new W.ParagraphBorders(new W.LeftBorder { Val = W.BorderValues.Single, Color = "FF883E", Size = 12 }) },
                            Run(MarkdownFormatting.CalloutLabel(alert.Kind.ToString()), MarkdownEmphasis.Bold)), listItem, availableWidth);
                        foreach (var child in alert) AppendBlock(parent, child, listItem, containerWidth: availableWidth);
                        break;
                    case QuoteBlock quote:
                        foreach (var child in quote) AppendBlock(parent, child, listItem, styleMap.Quote, availableWidth);
                        break;
                    case ContainerBlock container:
                        foreach (var child in container) AppendBlock(parent, child, listItem, paragraphStyle, availableWidth); break;
                    case ThematicBreakBlock:
                        AppendParagraph(parent, new W.Paragraph(new W.ParagraphProperties(new W.ParagraphBorders(new W.BottomBorder { Val = W.BorderValues.Single, Size = 4 }))), listItem, availableWidth); break;
                }
            }
            void AppendParagraph(OpenXmlCompositeElement parent, W.Paragraph paragraph, ListItemContext? listItem, long availableWidth)
            {
                if (listItem is not null)
                {
                    paragraph.ParagraphProperties ??= new W.ParagraphProperties(new W.ParagraphStyleId { Val = bodyStyle });
                    paragraph.ParagraphProperties.NumberingProperties = listItem.MarkerPending
                        ? new W.NumberingProperties(new W.NumberingLevelReference { Val = Math.Min(listItem.Depth, 8) }, new W.NumberingId { Val = listItem.NumberingId })
                        : new W.NumberingProperties(new W.NumberingId { Val = 0 }); // Explicitly suppress numbering inherited from a template style.
                    paragraph.ParagraphProperties.Indentation = new W.Indentation { Left = listItem.IndentTwips.ToString(System.Globalization.CultureInfo.InvariantCulture), Hanging = listItem.MarkerPending ? "240" : "0" };
                    listItem.MarkerPending = false;
                    availableWidth -= listItem.IndentTwips * 635L;
                }
                foreach (var inline in paragraph.Descendants<DW.Inline>())
                {
                    if (inline.Extent is not { Cx.Value: > 0, Cy.Value: > 0 } extent || extent.Cx.Value <= availableWidth) continue;
                    var height = Math.Max(1, (long)(extent.Cy.Value * ((double)availableWidth / extent.Cx.Value)));
                    extent.Cx = availableWidth; extent.Cy = height;
                    foreach (var shape in inline.Descendants<A.Extents>()) { shape.Cx = availableWidth; shape.Cy = height; }
                }
                parent.Append(paragraph);
            }
            void EnsureListAnchor(OpenXmlCompositeElement parent, ListItemContext? listItem, long availableWidth)
            {
                if (listItem?.MarkerPending != true) return;
                var anchor = Paragraph(null, bodyStyle); anchor.ParagraphProperties!.KeepNext = new W.KeepNext();
                AppendParagraph(parent, anchor, listItem, availableWidth);
            }
            long CellMargin(OpenXmlElement? margin)
            {
                if (margin is null) return 108; // Word's default horizontal cell margin, in twentieths of a point.
                var type = margin.GetAttribute("type", margin.NamespaceUri).Value;
                if (type == "nil") return 0;
                return type == "dxa" && long.TryParse(margin.GetAttribute("w", margin.NamespaceUri).Value,
                    System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value)
                    ? Math.Min(value, widthEmu / 635) : 108;
            }
            W.Paragraph Paragraph(ContainerInline? inline, string style)
            {
                var paragraph = new W.Paragraph(new W.ParagraphProperties(new W.ParagraphStyleId { Val = style }));
                if (inline is not null) Inlines(paragraph, inline, MarkdownEmphasis.None);
                return paragraph;
            }
            void Inlines(OpenXmlCompositeElement target, ContainerInline container, MarkdownEmphasis formatting)
            {
                foreach (var inline in container)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    switch (inline)
                    {
                        case Markdig.Extensions.Mathematics.MathInline formula:
                            target.Append(content.Formulae.TryGetValue(formula.Span.Start, out var formulaImage)
                                ? Image(formulaImage) : Run(formula.Content.ToString(), formatting)); break;
                        case DocumentAnchorInline anchor:
                            var anchorBookmark = bookmarks[anchor.Id];
                            target.Append(new W.BookmarkStart { Id = anchorBookmark.Id, Name = anchorBookmark.Name }, Run(anchor.Text, formatting), new W.BookmarkEnd { Id = anchorBookmark.Id });
                            break;
                        case DocumentSemanticInline semantic:
                            var semanticRun = Run(semantic.Text, formatting | (semantic.Kind == DocumentInlineKind.StatusBadge ? MarkdownEmphasis.Bold : MarkdownEmphasis.None));
                            if (semantic.Kind != DocumentInlineKind.StatusBadge) semanticRun.RunProperties!.RunFonts = new W.RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" };
                            target.Append(semanticRun); break;
                        case LiteralInline literal: target.Append(Run(literal.Content.ToString(), formatting)); break;
                        case AutolinkInline auto:
                            var autoRun = Run(auto.Url, formatting);
                            if (MarkdownLinks.ExternalUri(MarkdownLinks.AutolinkReference(auto)) is { } autoUri)
                            {
                                var autoRelationship = main.AddHyperlinkRelationship(autoUri, true);
                                target.Append(new W.Hyperlink(autoRun) { Id = autoRelationship.Id });
                            }
                            else target.Append(autoRun);
                            break;
                        case HtmlEntityInline entity: target.Append(Run(entity.Transcoded.ToString(), formatting)); break;
                        case CodeInline code: var run = Run(code.Content, formatting); run.RunProperties!.Append(new W.RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" }); target.Append(run); break;
                        case LineBreakInline line: target.Append(line.IsHard ? new W.Run(new W.Break()) : Run(" ", formatting)); break;
                        case Markdig.Extensions.TaskLists.TaskList task: target.Append(Run(task.Checked ? "☑ " : "☐ ", formatting)); break;
                        case LinkInline { IsImage: true } image:
                            if (!content.Images.TryGetValue(image.Url ?? "", out var asset)) throw new InvalidDataException("An image could not be exported: " + image.Url);
                            target.Append(Image(asset)); break;
                        case LinkInline link when link.Url?.StartsWith('#') == true && bookmarks.TryGetValue(Uri.UnescapeDataString(link.Url[1..]), out var localBookmark):
                            var localLink = new W.Hyperlink { Anchor = localBookmark.Name, History = true };
                            Inlines(localLink, link, formatting); target.Append(localLink); break;
                        case LinkInline link when MarkdownLinks.ExternalUri(link.Url) is { } url:
                            var relationship = main.AddHyperlinkRelationship(url, true);
                            var hyperlink = new W.Hyperlink { Id = relationship.Id };
                            Inlines(hyperlink, link, formatting); target.Append(hyperlink); break;
                        case EmphasisInline emphasis: Inlines(target, emphasis, formatting | MarkdownFormatting.Emphasis(emphasis)); break;
                        case HtmlInline html: target.Append(Run(html.Tag, formatting)); break;
                        case ContainerInline nested: Inlines(target, nested, formatting); break;
                    }
                }
            }
            W.Run Run(string text, MarkdownEmphasis formatting)
            {
                var properties = new W.RunProperties();
                if (formatting.HasFlag(MarkdownEmphasis.Bold)) properties.Bold = new W.Bold();
                if (formatting.HasFlag(MarkdownEmphasis.Italic)) properties.Italic = new W.Italic();
                if (formatting.HasFlag(MarkdownEmphasis.Strike)) properties.Strike = new W.Strike();
                if (formatting.HasFlag(MarkdownEmphasis.Underline)) properties.Underline = new W.Underline { Val = W.UnderlineValues.Single };
                if (formatting.HasFlag(MarkdownEmphasis.Subscript)) properties.VerticalTextAlignment = new W.VerticalTextAlignment { Val = W.VerticalPositionValues.Subscript };
                if (formatting.HasFlag(MarkdownEmphasis.Superscript)) properties.VerticalTextAlignment = new W.VerticalTextAlignment { Val = W.VerticalPositionValues.Superscript };
                if (formatting.HasFlag(MarkdownEmphasis.Highlight)) properties.Shading = new W.Shading { Val = W.ShadingPatternValues.Clear, Fill = "EDEDED" };
                if (text.IndexOfAny(['\r', '\n', '\t']) < 0)
                    return new W.Run(properties, new W.Text(text) { Space = SpaceProcessingModeValues.Preserve });
                // Decoded entities can carry controls too; Word ignores raw controls inside w:t.
                var result = new W.Run(properties);
                foreach (var element in ReadCodeElements(text, '\0', cancellationToken))
                    switch (element.Kind)
                    {
                        case CodeElementKind.Tab: result.Append(new W.TabChar()); break;
                        case CodeElementKind.Break: result.Append(new W.Break()); break;
                        default: result.Append(new W.Text(text.Substring(element.Start, element.Length)) { Space = SpaceProcessingModeValues.Preserve }); break;
                    }
                return result;
            }
            void CodeText(W.Paragraph paragraph, CodeBlock code)
            {
                CodeSyntaxBlock? syntax = null;
                content.CodeSyntax?.Blocks.TryGetValue(code, out syntax);
                var previous = '\0';
                foreach (var segment in CodeSyntaxRuns.Read(code.Lines.ToString(), syntax))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var properties = new W.RunProperties(new W.RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" });
                    if (segment.Role != CodeTokenRole.Plain) properties.Color = new W.Color { Val = CodeSyntaxPalette.Hex(segment.Role) };
                    var run = new W.Run(properties);
                    foreach (var element in ReadCodeElements(segment.Text, previous, cancellationToken))
                    {
                        switch (element.Kind)
                        {
                            case CodeElementKind.Tab: run.Append(new W.TabChar()); break;
                            case CodeElementKind.Break: run.Append(new W.Break()); break;
                            default: run.Append(new W.Text(segment.Text.Substring(element.Start, element.Length)) { Space = SpaceProcessingModeValues.Preserve }); break;
                        }
                    }
                    if (segment.Text.Length > 0) previous = segment.Text[^1];
                    paragraph.Append(run);
                }
            }

            W.Run Image(ExportImage image)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!imageParts.TryGetValue(image.PngBytes, out var part))
                {
                    part = main.AddImagePart(ImagePartType.Png);
                    using var data = new MemoryStream(image.PngBytes, false); part.FeedData(data);
                    imageParts.Add(image.PngBytes, part);
                }
                var cx = (long)((image.DisplayWidth > 0 ? image.DisplayWidth : image.PixelWidth) * 9525);
                var cy = (long)((image.DisplayHeight > 0 ? image.DisplayHeight : image.PixelHeight) * 9525);
                var scale = Math.Min(1, Math.Min((double)widthEmu / cx, (double)heightEmu / cy));
                cx = (long)(cx * scale); cy = (long)(cy * scale);
                while (usedDrawings.Contains(drawingId)) drawingId++;
                var id = drawingId++; usedDrawings.Add(id);
                return new W.Run(new W.Drawing(new DW.Inline(
                    new DW.Extent { Cx = cx, Cy = cy },
                    new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
                    new DW.DocProperties { Id = id, Name = "Image " + id, Description = image.AlternativeText },
                    new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                    new A.Graphic(new A.GraphicData(new PIC.Picture(
                        new PIC.NonVisualPictureProperties(new PIC.NonVisualDrawingProperties { Id = 0, Name = "Image " + id }, new PIC.NonVisualPictureDrawingProperties()),
                        new PIC.BlipFill(new A.Blip { Embed = main.GetIdOfPart(part) }, new A.Stretch(new A.FillRectangle())),
                        new PIC.ShapeProperties(new A.Transform2D(new A.Offset { X = 0, Y = 0 }, new A.Extents { Cx = cx, Cy = cy }),
                            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }))) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))));
            }
        }
        return stream.ToArray();
    }

    private sealed class ListItemContext(int numberingId, int depth)
    {
        public int NumberingId { get; } = numberingId;
        public int Depth { get; } = depth;
        public int IndentTwips => (Depth + 1) * 360;
        public bool MarkerPending { get; set; } = true;
    }

    private static void ValidateCodeAllocation(ExportContent content, CancellationToken cancellationToken)
    {
        var remaining = content.AllocationLimits.MaximumWordCodeElements;
        void Reserve(int count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (count > remaining) throw new IOException("Code formatting exceeds the Word export size limit. Export fewer or smaller code blocks.");
            remaining -= count;
        }
        foreach (var code in content.Document.Syntax.Descendants<CodeBlock>())
        {
            if (code is Markdig.Extensions.Mathematics.MathBlock or FencedCodeBlock { Info: "mermaid" or "mmd" }) continue;
            Reserve(3); // Paragraph, properties and style; charged across the whole document.
            for (var ancestor = code.Parent; ancestor is not null; ancestor = ancestor.Parent)
                if (ancestor is ListItemBlock) { Reserve(4); break; } // Numbering, level, id and indentation; continuation blocks use at most this many.
            CodeSyntaxBlock? syntax = null;
            content.CodeSyntax?.Blocks.TryGetValue(code, out syntax);
            var previous = '\0';
            foreach (var segment in CodeSyntaxRuns.Read(code.Lines.ToString(), syntax))
            {
                Reserve(segment.Role == CodeTokenRole.Plain ? 3 : 4); // Run, properties, font and optional colour.
                foreach (var element in ReadCodeElements(segment.Text, previous, cancellationToken)) Reserve(1);
                if (segment.Text.Length > 0) previous = segment.Text[^1];
            }
        }
    }

    private enum CodeElementKind { Text, Tab, Break }
    private readonly record struct CodeElement(CodeElementKind Kind, int Start, int Length);

    // The preflight and writer share delimiter handling, including CRLF split across colour runs.
    private static IEnumerable<CodeElement> ReadCodeElements(string text, char previous, CancellationToken cancellationToken)
    {
        for (var start = 0; start < text.Length;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var character = text[start];
            if (character is '\r' or '\n' or '\t')
            {
                if (character == '\t') yield return new(CodeElementKind.Tab, start, 1);
                else if (character != '\n' || previous != '\r') yield return new(CodeElementKind.Break, start, 1);
                previous = character; start++; continue;
            }
            var end = start + 1;
            while (end < text.Length && text[end] is not ('\r' or '\n' or '\t'))
            {
                if ((end & 4095) == 0) cancellationToken.ThrowIfCancellationRequested();
                end++;
            }
            yield return new(CodeElementKind.Text, start, end - start);
            previous = text[end - 1]; start = end;
        }
    }

    private static IEnumerable<T> PartElements<T>(OpenXmlPart part, HashSet<OpenXmlPart> seen) where T : OpenXmlElement
    {
        if (!seen.Add(part)) yield break;
        if (part.RootElement is { } root) foreach (var element in root.Descendants<T>()) yield return element;
        foreach (var child in part.Parts) foreach (var element in PartElements<T>(child.OpenXmlPart, seen)) yield return element;
    }

    private static void ValidateRelationships(OpenXmlPart part, HashSet<OpenXmlPart> seen)
    {
        if (!seen.Add(part)) return;
        if (part.ExternalRelationships.Any()) throw new InvalidDataException("Templates with external resources or attached remote templates are not supported.");
        foreach (var relationship in part.HyperlinkRelationships)
            if (relationship.Uri.Scheme is not ("https" or "http" or "mailto")) throw new InvalidDataException("The template contains an unsafe hyperlink.");
        foreach (var child in part.Parts) ValidateRelationships(child.OpenXmlPart, seen);
    }
}
