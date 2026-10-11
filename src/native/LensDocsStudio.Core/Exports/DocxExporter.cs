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
    public byte[] Export(ExportContent content, byte[]? template = null)
    {
        using var stream = new MemoryStream();
        if (template is not null) stream.Write(template);
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
            string Style(string name, string? fallback = null)
            {
                var existing = styles.Styles.Elements<W.Style>().FirstOrDefault(s =>
                    string.Equals(s.StyleName?.Val?.Value, name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s.StyleId?.Value, name.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));
                if (existing?.StyleId?.Value is { } id) return id;
                var newId = fallback ?? name.Replace(" ", "");
                if (!styles.Styles.Elements<W.Style>().Any(s => s.StyleId == newId))
                    styles.Styles.Append(new W.Style(new W.StyleName { Val = name }) { StyleId = newId, Type = W.StyleValues.Paragraph });
                return newId;
            }
            var titleStyle = Style("Title");
            var bodyStyle = Style("Normal");
            var codeStyle = Style("Code", "LensCode");
            var numbering = main.NumberingDefinitionsPart ?? main.AddNewPart<NumberingDefinitionsPart>();
            numbering.Numbering ??= new W.Numbering();
            var nextNumber = numbering.Numbering.Elements<W.NumberingInstance>().Select(n => n.NumberID?.Value ?? 0).DefaultIfEmpty().Max() + 1;
            var nextAbstract = numbering.Numbering.Elements<W.AbstractNum>().Select(n => n.AbstractNumberId?.Value ?? 0).DefaultIfEmpty().Max() + 1;
            var drawingId = main.Document.Descendants<DW.DocProperties>().Select(p => p.Id?.Value ?? 0).DefaultIfEmpty().Max() + 1;
            var pageSize = section.GetFirstChild<W.PageSize>();
            var margins = section.GetFirstChild<W.PageMargin>();
            var widthEmu = ((long)(pageSize?.Width?.Value ?? 11906) - (margins?.Left?.Value ?? 1134) - (margins?.Right?.Value ?? 1134)) * 635;
            var heightEmu = ((long)(pageSize?.Height?.Value ?? 16838) - (margins?.Top?.Value ?? 1134) - (margins?.Bottom?.Value ?? 1134)) * 635;

            foreach (var block in content.Document.Syntax) AppendBlock(body, block);
            body.Append(section);
            package.PackageProperties.Title = content.Document.Headings.FirstOrDefault()?.Title ?? "Documentation";
            var settings = main.DocumentSettingsPart ?? main.AddNewPart<DocumentSettingsPart>();
            settings.Settings ??= new W.Settings();
            settings.Settings.RemoveAllChildren<W.UpdateFieldsOnOpen>();
            settings.Settings.Append(new W.UpdateFieldsOnOpen { Val = true });
            main.Document.Save(); styles.Styles.Save(); numbering.Numbering.Save(); settings.Settings.Save();

            void AppendBlock(OpenXmlCompositeElement parent, Block block, int? listId = null, int depth = 0)
            {
                switch (block)
                {
                    case HeadingBlock heading:
                        var hp = Paragraph(heading.Inline, heading.Level == 1 ? titleStyle : Style("Heading " + (heading.Level - 1)));
                        hp.ParagraphProperties!.KeepNext = new W.KeepNext();
                        parent.Append(hp); break;
                    case ParagraphBlock paragraph:
                        var p = Paragraph(paragraph.Inline, bodyStyle);
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
                        var id = nextNumber++; var abstractId = nextAbstract++;
                        var definition = new W.AbstractNum { AbstractNumberId = abstractId };
                        for (var level = 0; level < 9; level++) definition.Append(new W.Level(
                            new W.StartNumberingValue { Val = MarkdownFormatting.OrderedStart(list) },
                            new W.NumberingFormat { Val = list.IsOrdered ? W.NumberFormatValues.Decimal : W.NumberFormatValues.Bullet },
                            new W.LevelText { Val = list.IsOrdered ? "%" + (level + 1) + "." : "•" },
                            new W.LevelJustification { Val = W.LevelJustificationValues.Left },
                            new W.PreviousParagraphProperties(new W.Indentation { Left = ((level + 1) * 360).ToString(), Hanging = "240" })) { LevelIndex = level });
                        numbering.Numbering.InsertBefore(definition, numbering.Numbering.Elements<W.NumberingInstance>().FirstOrDefault());
                        numbering.Numbering.Append(new W.NumberingInstance(new W.AbstractNumId { Val = abstractId }) { NumberID = id });
                        foreach (var item in list.OfType<ListItemBlock>()) foreach (var child in item) AppendBlock(parent, child, id, listId.HasValue ? depth + 1 : depth);
                        break;
                    case Markdig.Extensions.Tables.Table table:
                        var wt = new W.Table();
                        wt.Append(tableProperties?.CloneNode(true) ?? new W.TableProperties(new W.TableWidth { Width = "5000", Type = W.TableWidthUnitValues.Pct },
                            new W.TableBorders(new W.TopBorder { Val = W.BorderValues.Single, Size = 4 }, new W.LeftBorder { Val = W.BorderValues.Single, Size = 4 },
                                new W.BottomBorder { Val = W.BorderValues.Single, Size = 4 }, new W.RightBorder { Val = W.BorderValues.Single, Size = 4 },
                                new W.InsideHorizontalBorder { Val = W.BorderValues.Single, Size = 4 }, new W.InsideVerticalBorder { Val = W.BorderValues.Single, Size = 4 })));
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
                    case HtmlBlock html:
                        parent.Append(new W.Paragraph(new W.Run(new W.Text(html.Lines.ToString()) { Space = SpaceProcessingModeValues.Preserve }))); break;
                    case AlertBlock alert:
                        parent.Append(new W.Paragraph(new W.ParagraphProperties { KeepNext = new W.KeepNext(), Shading = new W.Shading { Val = W.ShadingPatternValues.Clear, Fill = "F3F4F6" },
                            ParagraphBorders = new W.ParagraphBorders(new W.LeftBorder { Val = W.BorderValues.Single, Color = "FF883E", Size = 12 }) },
                            Run(MarkdownFormatting.CalloutLabel(alert.Kind.ToString()), MarkdownEmphasis.Bold)));
                        foreach (var child in alert) AppendBlock(parent, child, listId, depth);
                        break;
                    case ContainerBlock container:
                        foreach (var child in container) AppendBlock(parent, child, listId, depth); break;
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
                        case LiteralInline literal: target.Append(Run(literal.Content.ToString(), formatting)); break;
                        case CodeInline code: var run = Run(code.Content, formatting); run.RunProperties!.Append(new W.RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" }); target.Append(run); break;
                        case LineBreakInline line: target.Append(line.IsHard ? new W.Run(new W.Break()) : Run(" ", formatting)); break;
                        case Markdig.Extensions.TaskLists.TaskList task: target.Append(Run(task.Checked ? "☑ " : "☐ ", formatting)); break;
                        case LinkInline { IsImage: true } image:
                            if (!content.Images.TryGetValue(image.Url ?? "", out var asset)) throw new InvalidDataException("An image could not be exported: " + image.Url);
                            target.Append(Image(asset)); break;
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
                var id = drawingId++;
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

    private static void ValidateRelationships(OpenXmlPart part, HashSet<OpenXmlPart> seen)
    {
        if (!seen.Add(part)) return;
        if (part.ExternalRelationships.Any()) throw new InvalidDataException("Templates with external resources or attached remote templates are not supported.");
        foreach (var relationship in part.HyperlinkRelationships)
            if (relationship.Uri.Scheme is not ("https" or "http" or "mailto")) throw new InvalidDataException("The template contains an unsafe hyperlink.");
        foreach (var child in part.Parts) ValidateRelationships(child.OpenXmlPart, seen);
    }
}
