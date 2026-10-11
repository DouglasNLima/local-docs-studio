using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using LensDocsStudio.Core.Parsing;
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
        using var stream = new MemoryStream();
        if (template is not null) stream.Write(new DocxTemplateService().Prepare(template, cancellationToken).Bytes);
        using (var package = template is null
            ? WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true)
            : WordprocessingDocument.Open(stream, true))
        {
            var main = package.MainDocumentPart ?? package.AddMainDocumentPart();
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
            settings.Settings.Append(new W.UpdateFieldsOnOpen { Val = true });
            main.Document.Save(); styles.Styles.Save(); numbering.Numbering.Save(); settings.Settings.Save();

            void AppendBlock(OpenXmlCompositeElement parent, Block block, int? listId = null, int depth = 0, string? paragraphStyle = null)
            {
                switch (block)
                {
                    case HeadingBlock heading:
                        var hp = Paragraph(heading.Inline, styleMap.Heading(heading.Level));
                        hp.ParagraphProperties!.KeepNext = new W.KeepNext();
                        if (headingTargets.TryGetValue(heading.Span.Start, out var headingTarget))
                        {
                            var bookmark = bookmarks[headingTarget.Id];
                            hp.InsertAfter(new W.BookmarkStart { Id = bookmark.Id, Name = bookmark.Name }, hp.ParagraphProperties);
                            hp.Append(new W.BookmarkEnd { Id = bookmark.Id });
                        }
                        parent.Append(hp); break;
                    case ParagraphBlock paragraph:
                        var p = Paragraph(paragraph.Inline, paragraphStyle ?? bodyStyle);
                        if (listId.HasValue) p.ParagraphProperties!.NumberingProperties = new W.NumberingProperties(new W.NumberingLevelReference { Val = Math.Min(depth, 8) }, new W.NumberingId { Val = listId.Value });
                        parent.Append(p); break;
                    case FencedCodeBlock code when code.Info is "mermaid" or "mmd":
                        if (!content.Diagrams.TryGetValue(code.Span.Start, out var diagram)) throw new InvalidDataException("A Mermaid diagram could not be exported.");
                        parent.Append(new W.Paragraph(Image(diagram))); break;
                    case CodeBlock code:
                        var cp = new W.Paragraph(new W.ParagraphProperties(new W.ParagraphStyleId { Val = codeStyle }));
                        foreach (var line in code.Lines.ToString().Split('\n')) cp.Append(new W.Run(new W.RunProperties(new W.RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" }), new W.Text(line.TrimEnd('\r')) { Space = SpaceProcessingModeValues.Preserve }, new W.Break()));
                        parent.Append(cp); break;
                    case ListBlock list:
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
                        numbering.Numbering.InsertBefore(definition, numbering.Numbering.Elements<W.NumberingInstance>().FirstOrDefault());
                        numbering.Numbering.Append(new W.NumberingInstance(new W.AbstractNumId { Val = abstractId }) { NumberID = id });
                        foreach (var item in list.OfType<ListItemBlock>()) foreach (var child in item) AppendBlock(parent, child, id, listId.HasValue ? depth + 1 : depth, paragraphStyle);
                        break;
                    case Markdig.Extensions.Tables.Table table:
                        var wt = new W.Table();
                        wt.Append(tableProperties?.CloneNode(true) ?? new W.TableProperties(new W.TableWidth { Width = "5000", Type = W.TableWidthUnitValues.Pct },
                            new W.TableBorders(new W.TopBorder { Val = W.BorderValues.Single, Size = 4 }, new W.LeftBorder { Val = W.BorderValues.Single, Size = 4 },
                                new W.BottomBorder { Val = W.BorderValues.Single, Size = 4 }, new W.RightBorder { Val = W.BorderValues.Single, Size = 4 },
                                new W.InsideHorizontalBorder { Val = W.BorderValues.Single, Size = 4 }, new W.InsideVerticalBorder { Val = W.BorderValues.Single, Size = 4 })));
                        wt.TableProperties!.TableStyle ??= new W.TableStyle { Val = styleMap.Table };
                        var columns = table.OfType<Markdig.Extensions.Tables.TableRow>().Select(r => r.Count).DefaultIfEmpty(1).Max();
                        wt.Append(new W.TableGrid(Enumerable.Range(0, columns).Select(_ => new W.GridColumn { Width = (widthEmu / 635 / columns).ToString() })));
                        foreach (var row in table.OfType<Markdig.Extensions.Tables.TableRow>())
                        {
                            var wr = new W.TableRow();
                            if (row.IsHeader) wr.Append(new W.TableRowProperties(new W.TableHeader()));
                            var column = 0;
                            foreach (var cell in row.OfType<Markdig.Extensions.Tables.TableCell>())
                            {
                                var wc = new W.TableCell();
                                foreach (var child in cell) AppendBlock(wc, child);
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
                    case DocumentImageFigureBlock figure:
                        var picture = Paragraph(figure.Inline, bodyStyle);
                        if (figure.Caption.Length > 0) picture.ParagraphProperties!.KeepNext = new W.KeepNext();
                        parent.Append(picture);
                        if (figure.Caption.Length > 0) parent.Append(new W.Paragraph(new W.ParagraphProperties(new W.ParagraphStyleId { Val = styleMap.Caption }), Run(figure.Caption, MarkdownEmphasis.None)));
                        break;
                    case DocumentProgressBlock progress:
                        parent.Append(new W.Paragraph(new W.ParagraphProperties(new W.ParagraphStyleId { Val = bodyStyle }) { KeepNext = new W.KeepNext() }, Run(progress.Description, MarkdownEmphasis.None)));
                        var progressTable = new W.Table(new W.TableProperties(new W.TableWidth { Type = W.TableWidthUnitValues.Pct, Width = "5000" },
                            new W.TableLayout { Type = W.TableLayoutValues.Fixed }, new W.TableCellMarginDefault(new W.TopMargin { Type = W.TableWidthUnitValues.Dxa, Width = "0" }, new W.BottomMargin { Type = W.TableWidthUnitValues.Dxa, Width = "0" })));
                        var progressGrid = new W.TableGrid(); var progressRow = new W.TableRow(new W.TableRowProperties(new W.TableRowHeight { Val = 120, HeightType = W.HeightRuleValues.Exact }));
                        var fill = Math.Clamp((int)Math.Round(progress.Percentage * 50), 0, 5000);
                        foreach (var (amount, colour) in new[] { (fill, progress.ColourHex), (5000 - fill, "EDEDED") })
                        {
                            if (amount == 0) continue;
                            progressGrid.Append(new W.GridColumn { Width = Math.Max(1, widthEmu / 635 * amount / 5000).ToString(System.Globalization.CultureInfo.InvariantCulture) });
                            progressRow.Append(new W.TableCell(new W.TableCellProperties(new W.TableCellWidth { Type = W.TableWidthUnitValues.Pct, Width = amount.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                                new W.Shading { Val = W.ShadingPatternValues.Clear, Fill = colour }), new W.Paragraph(new W.ParagraphProperties(new W.SpacingBetweenLines { Before = "0", After = "0", Line = "20", LineRule = W.LineSpacingRuleValues.Exact }))));
                        }
                        progressTable.Append(progressGrid, progressRow); parent.Append(progressTable); break;
                    case HtmlBlock html:
                        parent.Append(new W.Paragraph(new W.Run(new W.Text(html.Lines.ToString()) { Space = SpaceProcessingModeValues.Preserve }))); break;
                    case AlertBlock alert:
                        parent.Append(new W.Paragraph(new W.ParagraphProperties { KeepNext = new W.KeepNext(), Shading = new W.Shading { Val = W.ShadingPatternValues.Clear, Fill = "F3F4F6" },
                            ParagraphBorders = new W.ParagraphBorders(new W.LeftBorder { Val = W.BorderValues.Single, Color = "FF883E", Size = 12 }) },
                            Run(MarkdownFormatting.CalloutLabel(alert.Kind.ToString()), MarkdownEmphasis.Bold)));
                        foreach (var child in alert) AppendBlock(parent, child, listId, depth);
                        break;
                    case QuoteBlock quote:
                        foreach (var child in quote) AppendBlock(parent, child, listId, depth, styleMap.Quote);
                        break;
                    case ContainerBlock container:
                        foreach (var child in container) AppendBlock(parent, child, listId, depth, paragraphStyle); break;
                    case ThematicBreakBlock:
                        parent.Append(new W.Paragraph(new W.ParagraphProperties(new W.ParagraphBorders(new W.BottomBorder { Val = W.BorderValues.Single, Size = 4 })))); break;
                }
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
                    switch (inline)
                    {
                        case DocumentAnchorInline anchor:
                            var anchorBookmark = bookmarks[anchor.Id];
                            target.Append(new W.BookmarkStart { Id = anchorBookmark.Id, Name = anchorBookmark.Name }, Run(anchor.Text, formatting), new W.BookmarkEnd { Id = anchorBookmark.Id });
                            break;
                        case DocumentSemanticInline semantic:
                            var semanticRun = Run(semantic.Text, formatting | (semantic.Kind == DocumentInlineKind.StatusBadge ? MarkdownEmphasis.Bold : MarkdownEmphasis.None));
                            if (semantic.Kind != DocumentInlineKind.StatusBadge) semanticRun.RunProperties!.RunFonts = new W.RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" };
                            target.Append(semanticRun); break;
                        case LiteralInline literal: target.Append(Run(literal.Content.ToString(), formatting)); break;
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
                        case LinkInline link when Uri.TryCreate(link.Url, UriKind.Absolute, out var url) && url.Scheme is "https" or "http" or "mailto":
                            var relationship = main.AddHyperlinkRelationship(url, true);
                            var hyperlink = new W.Hyperlink { Id = relationship.Id };
                            Inlines(hyperlink, link, formatting); target.Append(hyperlink); break;
                        case EmphasisInline emphasis: Inlines(target, emphasis, formatting | MarkdownFormatting.Emphasis(emphasis)); break;
                        case HtmlInline html: target.Append(Run(html.Tag, formatting)); break;
                        case ContainerInline nested: Inlines(target, nested, formatting); break;
                    }
                }
            }
            static W.Run Run(string text, MarkdownEmphasis formatting)
            {
                var properties = new W.RunProperties();
                if (formatting.HasFlag(MarkdownEmphasis.Bold)) properties.Bold = new W.Bold();
                if (formatting.HasFlag(MarkdownEmphasis.Italic)) properties.Italic = new W.Italic();
                if (formatting.HasFlag(MarkdownEmphasis.Strike)) properties.Strike = new W.Strike();
                if (formatting.HasFlag(MarkdownEmphasis.Underline)) properties.Underline = new W.Underline { Val = W.UnderlineValues.Single };
                if (formatting.HasFlag(MarkdownEmphasis.Subscript)) properties.VerticalTextAlignment = new W.VerticalTextAlignment { Val = W.VerticalPositionValues.Subscript };
                if (formatting.HasFlag(MarkdownEmphasis.Superscript)) properties.VerticalTextAlignment = new W.VerticalTextAlignment { Val = W.VerticalPositionValues.Superscript };
                if (formatting.HasFlag(MarkdownEmphasis.Highlight)) properties.Shading = new W.Shading { Val = W.ShadingPatternValues.Clear, Fill = "EDEDED" };
                return new W.Run(properties, new W.Text(text) { Space = SpaceProcessingModeValues.Preserve });
            }
            W.Run Image(ExportImage image)
            {
                var part = main.AddImagePart(ImagePartType.Png);
                using var data = new MemoryStream(image.PngBytes); part.FeedData(data);
                var cx = image.PixelWidth * 9525L; var cy = image.PixelHeight * 9525L;
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
