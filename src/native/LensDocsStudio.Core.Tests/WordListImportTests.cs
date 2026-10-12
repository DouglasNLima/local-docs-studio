using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Core.Tests;

public sealed class WordListImportTests
{
    [Fact]
    public void Exported_lists_reimport_with_starts_hierarchy_and_continuation_item_boundaries()
    {
        const string source = "7. First **bold** Café\n\n   Continuation\n\n   - Child\n\n     ChildContinuation\n\n     3. Grandchild\n\n        GrandchildContinuation\n\n   - ChildTwo\n\n   AfterChildren\n\n8. Second\n\nAfterList\n\n0. Zero\n1. Next\n\nWide\n\n999999998. WideFirst\n999999999. WideNext";
        var result = RoundTrip(source);
        Assert.Equal(Shape(source), Shape(result.Markdown));
        Assert.Contains("7. First **bold** Café", result.Markdown);
        Assert.Contains("3. Grandchild", result.Markdown);
        Assert.Contains("0. Zero", result.Markdown);
        Assert.Contains("999999999. WideNext", result.Markdown);
        Assert.Empty(result.Warnings);
    }

    [Theory]
    [InlineData("- ### Heading\n\n  Continuation\n\n- Next")]
    [InlineData("- | Label |\n  | --- |\n  | Value |\n\n  Continuation\n\n- Next")]
    [InlineData("- - Nested\n\n  Continuation\n\n- Next")]
    [InlineData("-\n- First\n\n  Continuation")]
    public void First_blocks_and_empty_items_keep_their_parent_list_when_reimported(string source)
    {
        var result = RoundTrip(source);
        Assert.Equal(Shape(source), Shape(result.Markdown));
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Explicit_zero_numbering_does_not_turn_a_body_paragraph_into_a_list()
    {
        var result = new DocumentImportService().ImportDocx(Package(new W.Numbering(),
            new W.Paragraph(new W.ParagraphProperties(new W.NumberingProperties(new W.NumberingId { Val = 0 })), new W.Run(new W.Text("Ordinary body")))));
        Assert.Equal("Ordinary body", result.Markdown.Trim());
        Assert.Empty(new MarkdownService().Parse(result.Markdown).Syntax.Descendants<ListBlock>());
    }

    [Fact]
    public void Each_numbering_level_uses_its_own_format_and_start_override()
    {
        var numbering = new W.Numbering(new W.AbstractNum(
            Level(0, W.NumberFormatValues.Decimal, 2), Level(1, W.NumberFormatValues.Bullet, 1)) { AbstractNumberId = 1 },
            new W.NumberingInstance(new W.AbstractNumId { Val = 1 }, new W.LevelOverride(new W.StartOverrideNumberingValue { Val = 5 }) { LevelIndex = 0 }) { NumberID = 1 });
        var result = new DocumentImportService().ImportDocx(Package(numbering, Item(1, 0, "First"), Item(1, 1, "Child"), Item(1, 0, "Second")));
        Assert.Equal(Shape("5. First\n   - Child\n6. Second"), Shape(result.Markdown));
        Assert.Contains("5. First", result.Markdown);
        Assert.Contains("6. Second", result.Markdown);
        Assert.Empty(result.Warnings);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 2)]
    public void Nested_counters_respect_explicit_restart_and_never_restart(int restart, int expected)
    {
        var nested = Level(1, W.NumberFormatValues.Decimal, 2);
        nested.LevelRestart = new W.LevelRestart { Val = restart };
        var numbering = new W.Numbering(new W.AbstractNum(Level(0, W.NumberFormatValues.Decimal, 4), nested) { AbstractNumberId = 1 },
            new W.NumberingInstance(new W.AbstractNumId { Val = 1 }) { NumberID = 1 });
        var result = new DocumentImportService().ImportDocx(Package(numbering, Item(1, 0, "First"), Item(1, 1, "Child"), Item(1, 0, "Second"), Item(1, 1, "LaterChild")));
        Assert.Contains("4. First", result.Markdown);
        Assert.Contains("2. Child", result.Markdown);
        Assert.Contains("5. Second", result.Markdown);
        Assert.Contains(expected + ". LaterChild", result.Markdown);
        Assert.Equal(Shape($"4. First\n\n   2. Child\n\n5. Second\n\n   {expected}. LaterChild"), Shape(result.Markdown));
    }

    [Fact]
    public void Adjacent_numbering_instances_remain_separate_lists_in_markdown()
    {
        var numbering = new W.Numbering(new W.AbstractNum(Level(0, W.NumberFormatValues.Decimal, 1)) { AbstractNumberId = 1 },
            new W.NumberingInstance(new W.AbstractNumId { Val = 1 }) { NumberID = 1 },
            new W.NumberingInstance(new W.AbstractNumId { Val = 1 }, new W.LevelOverride(new W.StartOverrideNumberingValue { Val = 5 }) { LevelIndex = 0 }) { NumberID = 2 });
        var result = new DocumentImportService().ImportDocx(Package(numbering, Item(1, 0, "FirstList"), Item(1, 0, "FirstNext"), Item(2, 0, "SecondList"), Item(2, 0, "SecondNext")));
        var lists = new MarkdownService().Parse(result.Markdown).Syntax.OfType<ListBlock>().ToArray();
        Assert.Equal(new[] { "1", "5" }, lists.Select(l => l.OrderedStart));
        Assert.All(lists, l => Assert.Equal(2, l.Count));
        Assert.Contains("6) SecondNext", result.Markdown);
    }

    [Fact]
    public void Table_cell_lists_keep_visible_markers_and_do_not_change_body_continuations()
    {
        var numbering = new W.Numbering(new W.AbstractNum(Level(0, W.NumberFormatValues.Decimal, 1)) { AbstractNumberId = 1 },
            new W.NumberingInstance(new W.AbstractNumId { Val = 1 }) { NumberID = 1 },
            new W.NumberingInstance(new W.AbstractNumId { Val = 1 }) { NumberID = 2 });
        var table = new W.Table(new W.TableProperties(new W.TableIndentation { Type = W.TableWidthUnitValues.Dxa, Width = 360 }),
            new W.TableRow(new W.TableCell(new W.Paragraph(new W.Run(new W.Text("Header"))))),
            new W.TableRow(new W.TableCell(Item(2, 0, "CellFirst"), Item(2, 0, "CellNext"))));
        var continuation = new W.Paragraph(new W.ParagraphProperties(new W.NumberingProperties(new W.NumberingId { Val = 0 }),
            new W.Indentation { Left = "360" }), new W.Run(new W.Text("Continuation")));
        var result = new DocumentImportService().ImportDocx(Package(numbering, Item(1, 0, "BodyFirst"), table, continuation, Item(1, 0, "BodyNext")));
        Assert.Contains("| 1. CellFirst<br>2. CellNext |", result.Markdown);
        Assert.Contains("2. BodyNext", result.Markdown);
        var list = Assert.Single(new MarkdownService().Parse(result.Markdown).Syntax.OfType<ListBlock>());
        Assert.Equal(2, list.Count);
        Assert.Equal(3, Assert.IsType<ListItemBlock>(list[0]).Count);
        Assert.Single(result.Warnings, w => w.Contains("table cells", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(9)]
    [InlineData(int.MaxValue)]
    public void Invalid_word_levels_are_rejected_before_indentation_allocation(int level)
    {
        Assert.Throws<InvalidDataException>(() => new DocumentImportService().ImportDocx(Package(new W.Numbering(), Item(1, level, "Invalid"))));
    }

    [Fact]
    public void Numbering_outside_markdown_range_is_rejected_without_changing_input()
    {
        var numbering = new W.Numbering(new W.AbstractNum(Level(0, W.NumberFormatValues.Decimal, int.MaxValue)) { AbstractNumberId = 1 },
            new W.NumberingInstance(new W.AbstractNumId { Val = 1 }) { NumberID = 1 });
        var bytes = Package(numbering, Item(1, 0, "OutOfRange"));
        var original = bytes.ToArray();
        Assert.Throws<InvalidDataException>(() => new DocumentImportService().ImportDocx(bytes));
        Assert.Equal(original, bytes);
    }

    [Fact]
    public void Missing_definitions_are_reported_once_and_keep_separate_item_boundaries()
    {
        var result = new DocumentImportService().ImportDocx(Package(new W.Numbering(), Item(9, 0, "First"), Item(9, 0, "Next")));
        Assert.Single(result.Warnings);
        Assert.Contains("missing Word list definition", result.Warnings[0]);
        Assert.Equal(Shape("1. First\n2. Next"), Shape(result.Markdown));
    }

    [Fact]
    public void Unsupported_formats_and_composite_markers_are_explicitly_reported()
    {
        var level = Level(0, W.NumberFormatValues.UpperRoman, 4);
        level.LevelText = new W.LevelText { Val = "Chapter %1.%2" };
        var numbering = new W.Numbering(new W.AbstractNum(level) { AbstractNumberId = 1 }, new W.NumberingInstance(new W.AbstractNumId { Val = 1 }) { NumberID = 1 });
        var result = new DocumentImportService().ImportDocx(Package(numbering, Item(1, 0, "First"), Item(1, 0, "Next")));
        Assert.Equal(2, result.Warnings.Count);
        Assert.Contains("4. First", result.Markdown);
        Assert.Contains("5. Next", result.Markdown);
    }

    private static ImportedDocument RoundTrip(string source)
    {
        var parsed = new MarkdownService().Parse(source);
        var bytes = new DocxExporter().Export(new(parsed, new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>()));
        var original = bytes.ToArray();
        var result = new DocumentImportService().ImportDocx(bytes);
        Assert.Equal(original, bytes);
        Assert.Equal(source, parsed.Source);
        return result;
    }

    private static string Shape(string source)
    {
        string Read(Block block) => block switch
        {
            ListBlock list => $"{(list.IsOrdered ? list.OrderedStart : "bullet")}[{string.Join(",", list.Select(Read))}]",
            HeadingBlock heading => "Heading" + heading.Level,
            ContainerBlock container => $"{container.GetType().Name}[{string.Join(",", container.Select(Read))}]",
            LeafBlock leaf => leaf.GetType().Name,
            _ => block.GetType().Name
        };
        return Read(new MarkdownService().Parse(source).Syntax);
    }

    private static W.Level Level(int index, W.NumberFormatValues format, int start) => new(new W.StartNumberingValue { Val = start },
        new W.NumberingFormat { Val = format }, new W.LevelText { Val = format == W.NumberFormatValues.Bullet ? "•" : "%" + (index + 1) + "." },
        new W.PreviousParagraphProperties(new W.Indentation { Left = ((index + 1) * 360).ToString() })) { LevelIndex = index };

    private static W.Paragraph Item(int id, int level, string text) => new(new W.ParagraphProperties(new W.NumberingProperties(
        new W.NumberingLevelReference { Val = level }, new W.NumberingId { Val = id })), new W.Run(new W.Text(text)));

    private static byte[] Package(W.Numbering numbering, params OpenXmlElement[] blocks)
    {
        using var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(blocks));
            main.AddNewPart<NumberingDefinitionsPart>().Numbering = numbering;
        }
        return stream.ToArray();
    }
}
