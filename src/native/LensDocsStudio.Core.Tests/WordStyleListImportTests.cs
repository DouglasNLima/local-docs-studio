using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Core.Tests;

public sealed class WordStyleListImportTests
{
    [Fact]
    public void Paragraph_styles_use_associated_levels_and_inherited_numbering()
    {
        var parent = Style("Parent", 1); parent.StyleParagraphProperties!.NumberingProperties!.NumberingLevelReference = new() { Val = 8 };
        var child = Style("Child", null, "Parent");
        var result = Import(new W.Styles(parent, child), Numbering(Level(0, "Parent", 4), Level(1, "Child", 2)),
            Paragraph("Parent", "First"), Paragraph("Child", "Child"), Paragraph("Parent", "Next"));
        var list = Assert.Single(Parse(result).OfType<ListBlock>());
        Assert.Equal("4", list.OrderedStart); Assert.Equal(2, list.Count);
        Assert.Equal("2", Assert.Single(Assert.IsType<ListItemBlock>(list[0]).OfType<ListBlock>()).OrderedStart);
        Assert.Contains("5. Next", result.Markdown); Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Default_numbering_and_explicit_zero_overrides_keep_body_text_outside_lists()
    {
        var style = Style("DefaultList", 1); style.Default = true;
        var suppressed = Style("Body", 0, "DefaultList");
        var result = Import(new W.Styles(style, suppressed), Numbering(Level(0, "DefaultList", 7)),
            Paragraph(null, "First"), Paragraph(null, "Next"), Paragraph(null, "Direct body", 0), Paragraph("Body", "Style body"));
        var parsed = Parse(result);
        var list = Assert.Single(parsed.OfType<ListBlock>());
        Assert.Equal("7", list.OrderedStart); Assert.Equal(2, list.Count);
        Assert.Equal(2, parsed.OfType<ParagraphBlock>().Count()); Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Inherited_indentation_keeps_unnumbered_continuations_inside_their_item()
    {
        var style = Style("List", 1); style.StyleParagraphProperties!.Indentation = new() { Left = "720" };
        var continuation = Style("Continuation", 0, "List");
        var outside = Paragraph("Continuation", "Outside"); outside.ParagraphProperties!.Indentation = new() { Left = "0" };
        var result = Import(new W.Styles(style, continuation), Numbering(Level(0, "List", 1)),
            Paragraph("List", "First"), Paragraph("Continuation", "More"), Paragraph("List", "Next"), outside);
        var parsed = Parse(result); var list = Assert.Single(parsed.OfType<ListBlock>());
        Assert.Equal(2, list.Count); Assert.Equal(2, Assert.IsType<ListItemBlock>(list[0]).Count);
        Assert.Equal("Outside", MarkdownService.InlineText(Assert.Single(parsed.OfType<ParagraphBlock>()).Inline));
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Direct_level_and_instance_override_the_inherited_numbering_properties()
    {
        var numbering = Numbering(Level(0, "Parent", 3), Level(1, "Child", 2));
        numbering.Append(new W.NumberingInstance(new W.AbstractNumId { Val = 1 },
            new W.LevelOverride(new W.StartOverrideNumberingValue { Val = 9 }) { LevelIndex = 1 }) { NumberID = 2 });
        var result = Import(new W.Styles(Style("Parent", 1), Style("Child", null, "Parent")), numbering,
            Paragraph("Parent", "First"), Paragraph("Parent", "Explicit child", level: 1), Paragraph("Child", "New instance", 2));
        Assert.Contains("3. First", result.Markdown); Assert.Contains("2. Explicit child", result.Markdown); Assert.Contains("9) New instance", result.Markdown);
        Assert.Equal(3, Parse(result).Descendants<ListBlock>().Count()); Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Linked_numbering_styles_keep_real_levels_and_outer_start_overrides()
    {
        var linked = Style("NumberingStyle", 2); linked.Type = W.StyleValues.Numbering;
        var numbering = new W.Numbering(new W.AbstractNum(new W.NumberingStyleLink { Val = "NumberingStyle" }) { AbstractNumberId = 1 },
            new W.AbstractNum(Level(0, "List", 4)) { AbstractNumberId = 2 },
            new W.NumberingInstance(new W.AbstractNumId { Val = 1 }, new W.LevelOverride(new W.StartOverrideNumberingValue { Val = 8 }) { LevelIndex = 0 }) { NumberID = 1 },
            new W.NumberingInstance(new W.AbstractNumId { Val = 2 }) { NumberID = 2 });
        var result = Import(new W.Styles(Style("List", 1), linked), numbering, Paragraph("List", "First"), Paragraph("List", "Next"));
        var list = Assert.Single(Parse(result).OfType<ListBlock>());
        Assert.Equal("8", list.OrderedStart); Assert.Equal(2, list.Count); Assert.Contains("9. Next", result.Markdown); Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Cyclic_numbering_style_links_are_rejected_without_changing_input()
    {
        var style = Style("Loop", 1); style.Type = W.StyleValues.Numbering;
        var numbering = new W.Numbering(new W.AbstractNum(new W.NumberingStyleLink { Val = "Loop" }) { AbstractNumberId = 1 },
            new W.NumberingInstance(new W.AbstractNumId { Val = 1 }) { NumberID = 1 });
        var bytes = Package(new W.Styles(style), numbering, Paragraph(null, "Body", 1)); var original = bytes.ToArray();
        Assert.Throws<InvalidDataException>(() => new DocumentImportService().ImportDocx(bytes)); Assert.Equal(original, bytes);
    }

    [Fact]
    public void Inherited_table_lists_keep_markers_and_report_the_flattened_structure()
    {
        var table = new W.Table(new W.TableRow(new W.TableCell(new W.Paragraph(new W.Run(new W.Text("Header"))))),
            new W.TableRow(new W.TableCell(Paragraph("List", "First"), Paragraph("List", "Next"))));
        var result = Import(new W.Styles(Style("List", 1)), Numbering(Level(0, "List", 4)), table);
        Assert.Contains("| 4. First<br>5. Next |", result.Markdown);
        Assert.Single(result.Warnings, w => w.Contains("table cells", StringComparison.Ordinal));
    }

    [Fact]
    public void Unassociated_style_numbering_has_an_explicit_level_zero_fallback()
    {
        var result = Import(new W.Styles(Style("List", 1)), Numbering(Level(0, null, 3)), Paragraph("List", "First"));
        Assert.Equal("3", Assert.Single(Parse(result).OfType<ListBlock>()).OrderedStart);
        Assert.Single(result.Warnings, w => w.Contains("level association", StringComparison.Ordinal));
    }

    [Fact]
    public void A_level_override_keeps_the_abstract_style_association_and_changes_the_marker()
    {
        var replacement = Level(1, null, 1); replacement.NumberingFormat = new() { Val = W.NumberFormatValues.Bullet }; replacement.LevelText = new() { Val = "•" };
        var numbering = Numbering(Level(0, "Parent", 1), Level(1, "Child", 2));
        numbering.Elements<W.NumberingInstance>().Single().Append(new W.LevelOverride(replacement) { LevelIndex = 1 });
        var result = Import(new W.Styles(Style("Parent", 1), Style("Child", null, "Parent")), numbering,
            Paragraph("Parent", "First"), Paragraph("Child", "Bullet"), Paragraph("Parent", "Next"));
        var list = Assert.Single(Parse(result).OfType<ListBlock>());
        Assert.False(Assert.Single(Assert.IsType<ListItemBlock>(list[0]).OfType<ListBlock>()).IsOrdered);
        Assert.Contains("2. Next", result.Markdown); Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Numbered_heading_styles_keep_both_heading_semantics_and_list_placement()
    {
        var result = Import(new W.Styles(Style("Heading2", 1)), Numbering(Level(0, "Heading2", 3)),
            Paragraph("Heading2", "First"), Paragraph("Heading2", "Next"));
        var list = Assert.Single(Parse(result).OfType<ListBlock>());
        Assert.Equal("3", list.OrderedStart); Assert.Equal(2, list.Count);
        Assert.All(list.Descendants<HeadingBlock>(), h => Assert.Equal(2, h.Level));
        Assert.Equal(2, list.Descendants<HeadingBlock>().Count()); Assert.Empty(result.Warnings);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Missing_or_wrong_type_numbering_styles_keep_visible_items_with_notes(bool wrongType)
    {
        var styles = wrongType ? new W.Styles(Style("Missing", 1)) : new W.Styles();
        var numbering = new W.Numbering(new W.AbstractNum(new W.NumberingStyleLink { Val = "Missing" }) { AbstractNumberId = 1 },
            new W.NumberingInstance(new W.AbstractNumId { Val = 1 }) { NumberID = 1 });
        var result = Import(styles, numbering, Paragraph(null, "First", 1), Paragraph(null, "Next", 1));
        Assert.Equal(2, Assert.Single(Parse(result).OfType<ListBlock>()).Count);
        Assert.Single(result.Warnings, w => w.Contains("no valid numbering target", StringComparison.Ordinal));
    }

    [Fact]
    public void Long_numbering_link_chains_are_rejected_before_creating_output()
    {
        var styles = new W.Styles(); var numbering = new W.Numbering();
        for (var i = 1; i <= 66; i++)
        {
            var style = Style("Link" + i, i + 1); style.Type = W.StyleValues.Numbering; styles.Append(style);
            numbering.Append(new W.AbstractNum(new W.NumberingStyleLink { Val = "Link" + i }) { AbstractNumberId = i },
                new W.NumberingInstance(new W.AbstractNumId { Val = i }) { NumberID = i });
        }
        var bytes = Package(styles, numbering, Paragraph(null, "Body", 1)); var original = bytes.ToArray();
        Assert.Throws<InvalidDataException>(() => new DocumentImportService().ImportDocx(bytes)); Assert.Equal(original, bytes);
    }

    [Fact]
    public void Numbering_style_inheritance_can_resolve_a_base_style_without_a_local_number_id()
    {
        var parent = Style("NumberingParent", 2); parent.Type = W.StyleValues.Numbering;
        var child = Style("NumberingChild", null, "NumberingParent"); child.Type = W.StyleValues.Numbering;
        var numbering = new W.Numbering(new W.AbstractNum(new W.NumberingStyleLink { Val = "NumberingChild" }) { AbstractNumberId = 1 },
            new W.AbstractNum(Level(0, null, 5)) { AbstractNumberId = 2 },
            new W.NumberingInstance(new W.AbstractNumId { Val = 1 }) { NumberID = 1 }, new W.NumberingInstance(new W.AbstractNumId { Val = 2 }) { NumberID = 2 });
        var result = Import(new W.Styles(parent, child), numbering, Paragraph(null, "First", 1));
        Assert.Equal("5", Assert.Single(Parse(result).OfType<ListBlock>()).OrderedStart); Assert.Empty(result.Warnings);
    }

    private static MarkdownDocument Parse(ImportedDocument result) => new MarkdownService().Parse(result.Markdown).Syntax;
    private static ImportedDocument Import(W.Styles styles, W.Numbering numbering, params OpenXmlElement[] blocks)
    {
        var bytes = Package(styles, numbering, blocks); var original = bytes.ToArray();
        var result = new DocumentImportService().ImportDocx(bytes); Assert.Equal(original, bytes); return result;
    }
    private static W.Style Style(string id, int? numberId, string? basedOn = null)
    {
        var style = new W.Style(new W.StyleName { Val = id }, new W.StyleParagraphProperties()) { StyleId = id, Type = W.StyleValues.Paragraph };
        if (numberId.HasValue) style.StyleParagraphProperties!.NumberingProperties = new(new W.NumberingId { Val = numberId.Value });
        if (basedOn is not null) style.BasedOn = new() { Val = basedOn };
        return style;
    }
    private static W.Paragraph Paragraph(string? style, string text, int? numberId = null, int? level = null)
    {
        var properties = new W.ParagraphProperties();
        if (style is not null) properties.ParagraphStyleId = new() { Val = style };
        if (numberId.HasValue || level.HasValue)
        {
            properties.NumberingProperties = new();
            if (numberId.HasValue) properties.NumberingProperties.NumberingId = new() { Val = numberId.Value };
            if (level.HasValue) properties.NumberingProperties.NumberingLevelReference = new() { Val = level.Value };
        }
        return new(properties, new W.Run(new W.Text(text)));
    }
    private static W.Level Level(int index, string? style, int start)
    {
        var level = new W.Level(new W.StartNumberingValue { Val = start }, new W.NumberingFormat { Val = W.NumberFormatValues.Decimal },
            new W.LevelText { Val = "%" + (index + 1) + "." }, new W.PreviousParagraphProperties(new W.Indentation { Left = ((index + 1) * 720).ToString() })) { LevelIndex = index };
        if (style is not null) level.ParagraphStyleIdInLevel = new() { Val = style };
        return level;
    }
    private static W.Numbering Numbering(params W.Level[] levels) => new(new W.AbstractNum(levels) { AbstractNumberId = 1 },
        new W.NumberingInstance(new W.AbstractNumId { Val = 1 }) { NumberID = 1 });
    private static byte[] Package(W.Styles styles, W.Numbering numbering, params OpenXmlElement[] blocks)
    {
        using var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = package.AddMainDocumentPart(); main.Document = new W.Document(new W.Body(blocks));
            main.AddNewPart<StyleDefinitionsPart>().Styles = styles;
            main.AddNewPart<NumberingDefinitionsPart>().Numbering = numbering;
        }
        return stream.ToArray();
    }
}
