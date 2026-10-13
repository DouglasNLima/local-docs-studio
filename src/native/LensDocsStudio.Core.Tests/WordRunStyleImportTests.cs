using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Core.Tests;

public sealed class WordRunStyleImportTests
{
    [Fact]
    public void Paragraph_and_character_style_chains_preserve_combined_formatting()
    {
        var styles = new W.Styles(Style("Base", W.StyleValues.Paragraph, null, new W.Bold()),
            Style("Body", W.StyleValues.Paragraph, "Base", new W.Italic()),
            Style("Removed", W.StyleValues.Character, null, new W.Strike()),
            Style("Marked", W.StyleValues.Character, "Removed", new W.Underline { Val = W.UnderlineValues.Single }));
        var imported = Import(styles, Paragraph("Body", Run("Marked")));
        AssertFormatting(imported, MarkdownEmphasis.Bold | MarkdownEmphasis.Italic | MarkdownEmphasis.Strike | MarkdownEmphasis.Underline);
        Assert.Empty(imported.Warnings);
    }

    [Theory]
    [InlineData(true, MarkdownEmphasis.None)]
    [InlineData(false, MarkdownEmphasis.Bold | MarkdownEmphasis.Italic | MarkdownEmphasis.Strike)]
    public void Style_toggle_values_follow_the_inheritance_chain(bool childValue, MarkdownEmphasis expected)
    {
        var styles = new W.Styles(Style("Base", W.StyleValues.Paragraph, null, new W.Bold(), new W.Italic(), new W.Strike()),
            Style("Child", W.StyleValues.Paragraph, "Base", new W.Bold { Val = childValue },
                new W.Italic { Val = childValue }, new W.Strike { Val = childValue }));
        AssertFormatting(Import(styles, Paragraph("Child", Run(null))), expected);
    }

    [Fact]
    public void Direct_off_values_override_inherited_formatting()
    {
        var styles = new W.Styles(Style("Body", W.StyleValues.Paragraph, null, new W.Bold(), new W.Italic(), new W.Strike(),
            new W.Underline { Val = W.UnderlineValues.Single }));
        AssertFormatting(Import(styles, Paragraph("Body", Run(null, new W.Bold { Val = false }, new W.Italic { Val = false },
            new W.Strike { Val = false }, new W.Underline { Val = W.UnderlineValues.None }))), MarkdownEmphasis.None);
    }

    [Fact]
    public void Direct_on_values_are_absolute_after_styles_toggle_off()
    {
        var styles = new W.Styles(Style("Base", W.StyleValues.Paragraph, null, new W.Bold(), new W.Strike()),
            Style("Child", W.StyleValues.Paragraph, "Base", new W.Bold(), new W.Strike()));
        AssertFormatting(Import(styles, Paragraph("Child", Run(null, new W.Bold(), new W.Strike()))), MarkdownEmphasis.Bold | MarkdownEmphasis.Strike);
    }

    [Fact]
    public void Document_defaults_and_default_paragraph_style_apply_to_unstyled_runs()
    {
        var normal = Style("Normal", W.StyleValues.Paragraph, null, new W.Bold()); normal.Default = true;
        var styles = new W.Styles(new W.DocDefaults(new W.RunPropertiesDefault(new W.RunPropertiesBaseStyle(
            new W.Italic(), new W.Underline { Val = W.UnderlineValues.Single }))), normal);
        AssertFormatting(Import(styles, Paragraph(null, Run(null))), MarkdownEmphasis.Bold | MarkdownEmphasis.Italic | MarkdownEmphasis.Underline);
    }

    [Fact]
    public void Character_style_cache_retains_each_paragraphs_effective_context()
    {
        var styles = new W.Styles(Style("BoldBody", W.StyleValues.Paragraph, null, new W.Bold()),
            Style("PlainBody", W.StyleValues.Paragraph, null), Style("Toggle", W.StyleValues.Character, null, new W.Bold()));
        var imported = Import(styles, Paragraph("BoldBody", Run("Toggle")), Paragraph("PlainBody", Run("Toggle")));
        var paragraphs = new MarkdownService().Parse(imported.Markdown).Syntax.OfType<ParagraphBlock>().ToArray();
        Assert.Equal(2, paragraphs.Length);
        Assert.Empty(paragraphs[0].Inline!.Descendants<EmphasisInline>());
        Assert.Equal(MarkdownEmphasis.Bold, MarkdownFormatting.Emphasis(Assert.Single(paragraphs[1].Inline!.Descendants<EmphasisInline>())));
    }

    [Fact]
    public void Unreferenced_default_character_style_is_not_applied_implicitly()
    {
        var style = Style("DefaultCharacter", W.StyleValues.Character, null, new W.Strike()); style.Default = true;
        AssertFormatting(Import(new W.Styles(style), Paragraph(null, Run(null))), MarkdownEmphasis.None);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("wrong-type")]
    public void Missing_or_incompatible_character_styles_preserve_text_with_one_note(string reference)
    {
        var styles = new W.Styles(Style("wrong-type", W.StyleValues.Paragraph, null, new W.Bold()));
        var imported = Import(styles, Paragraph(null, Run(reference), Run(reference)));
        AssertFormatting(imported, MarkdownEmphasis.None, "Café ΩCafé Ω");
        Assert.Equal("A missing or incompatible Word run style was ignored.", Assert.Single(imported.Warnings));
    }

    [Theory]
    [InlineData("character")]
    [InlineData("paragraph")]
    public void Cyclic_run_styles_keep_readable_text_and_one_note_without_changing_input(string kind)
    {
        var type = kind == "character" ? W.StyleValues.Character : W.StyleValues.Paragraph;
        var styles = new W.Styles(Style("A", type, "B", new W.Strike()), Style("B", type, "A", new W.Italic()));
        var bytes = Package(styles, Paragraph(kind == "paragraph" ? "A" : null, Run(kind == "character" ? "A" : null)));
        var original = bytes.ToArray();
        var imported = new DocumentImportService().ImportDocx(bytes);
        AssertFormatting(imported, MarkdownEmphasis.None);
        Assert.Contains("cyclic", Assert.Single(imported.Warnings));
        Assert.Equal(original, bytes);
    }

    [Fact]
    public void Excessive_run_style_inheritance_is_rejected_before_rendering()
    {
        var styles = new W.Styles(Enumerable.Range(0, 65).Select(index => Style("S" + index, W.StyleValues.Character,
            index == 64 ? null : "S" + (index + 1), new W.Italic { Val = false })));
        Assert.Contains("inheritance exceeds", Assert.Throws<InvalidDataException>(() => Import(styles, Paragraph(null, Run("S0")))).Message);
    }

    [Fact]
    public void Sixty_four_style_levels_remain_supported_and_cache_reuse_preserves_text()
    {
        var styles = new W.Styles(Enumerable.Range(0, 64).Select(index => Style("S" + index, W.StyleValues.Character,
            index == 63 ? null : "S" + (index + 1), new W.Italic { Val = index == 63 })));
        AssertFormatting(Import(styles, Paragraph(null, Run("S0")), Paragraph(null, Run("S0"))), MarkdownEmphasis.Italic, "Café ΩCafé Ω");
    }

    [Fact]
    public void A_missing_base_style_keeps_the_valid_childs_own_formatting()
    {
        var styles = new W.Styles(Style("Marked", W.StyleValues.Character, "Unavailable", new W.Strike()));
        var imported = Import(styles, Paragraph(null, Run("Marked")));
        AssertFormatting(imported, MarkdownEmphasis.Strike);
        Assert.Equal("A missing or incompatible Word run style was ignored.", Assert.Single(imported.Warnings));
    }

    [Fact]
    public void Inherited_formatting_survives_hyperlinks_and_table_cells()
    {
        var styles = new W.Styles(Style("Marked", W.StyleValues.Character, null, new W.Strike(),
            new W.Underline { Val = W.UnderlineValues.Single }));
        var paragraph = Paragraph(null, new W.Hyperlink(Run("Marked")) { Anchor = "unresolved" });
        var table = new W.Table(new W.TableProperties(), new W.TableGrid(new W.GridColumn { Width = "1440" }),
            new W.TableRow(new W.TableCell(Paragraph(null, Run("Marked")))));
        var imported = Import(styles, paragraph, table);
        AssertFormatting(imported, MarkdownEmphasis.Strike | MarkdownEmphasis.Underline, "Café ΩCafé Ω");
        Assert.Contains("missing or ambiguous Word bookmark", Assert.Single(imported.Warnings));
    }

    [Fact]
    public void Code_styles_keep_literal_text_despite_inherited_run_formatting()
    {
        var styles = new W.Styles(Style("Code", W.StyleValues.Paragraph, null, new W.Bold(), new W.Strike()));
        var imported = Import(styles, Paragraph("Code", Run(null)));
        Assert.Equal("Café Ω", Assert.Single(new MarkdownService().Parse(imported.Markdown).Syntax.Descendants<CodeBlock>()).Lines.ToString());
        Assert.DoesNotContain("~~", imported.Markdown);
    }

    [Theory]
    [InlineData(false, "# Café Ω\n\n")]
    [InlineData(true, "# **Café Ω**\n\n")]
    public void Heading_style_bold_is_structural_while_direct_bold_remains_explicit(bool direct, string expected)
    {
        var styles = new W.Styles(Style("Heading1", W.StyleValues.Paragraph, null, new W.Bold()));
        var imported = Import(styles, Paragraph("Heading1", direct ? Run(null, new W.Bold()) : Run(null)));
        Assert.Equal(expected, imported.Markdown);
        Assert.Empty(imported.Warnings);
    }

    [Theory]
    [InlineData("double")]
    [InlineData("dotted")]
    [InlineData("words")]
    public void Other_underline_styles_retain_underlining_with_an_explicit_simplification_note(string kind)
    {
        var underline = kind switch { "double" => W.UnderlineValues.Double, "dotted" => W.UnderlineValues.Dotted, _ => W.UnderlineValues.Words };
        var styles = new W.Styles(Style("Marked", W.StyleValues.Character, null, new W.Underline { Val = underline }));
        var imported = Import(styles, Paragraph(null, Run("Marked")));
        AssertFormatting(imported, MarkdownEmphasis.Underline);
        Assert.Equal("A Word underline style was simplified to a single underline.", Assert.Single(imported.Warnings));
        var direct = Import(new W.Styles(), Paragraph(null, Run(null, new W.Underline { Val = underline })));
        AssertFormatting(direct, MarkdownEmphasis.Underline);
        Assert.Equal("A Word underline style was simplified to a single underline.", Assert.Single(direct.Warnings));
    }

    [Fact]
    public void Inherited_double_strike_preserves_meaning_and_direct_off_overrides_it()
    {
        var styles = new W.Styles(Style("Removed", W.StyleValues.Character, null, new W.DoubleStrike()));
        var imported = Import(styles, Paragraph(null, Run("Removed")));
        AssertFormatting(imported, MarkdownEmphasis.Strike);
        Assert.Equal("A Word double strikethrough was simplified to a single strikethrough.", Assert.Single(imported.Warnings));
        AssertFormatting(Import(styles, Paragraph(null, Run("Removed", new W.DoubleStrike { Val = false }))), MarkdownEmphasis.None);
    }

    [Fact]
    public void Inherited_markup_is_counted_at_the_exact_generated_text_boundary()
    {
        var bytes = Package(new W.Styles(Style("Marked", W.StyleValues.Character, null, new W.Strike(), new W.Underline { Val = W.UnderlineValues.Single })),
            Paragraph(null, Run("Marked")));
        var importer = new DocumentImportService(); var imported = importer.ImportDocx(bytes);
        Assert.Contains("++~~Café Ω~~++", imported.Markdown);
        Assert.Equal(imported.Markdown, importer.ImportDocx(bytes, limits: new(MaximumTextCharacters: imported.Markdown.Length)).Markdown);
        Assert.Throws<InvalidDataException>(() => importer.ImportDocx(bytes, limits: new(MaximumTextCharacters: imported.Markdown.Length - 1)));
    }

    private static W.Style Style(string id, W.StyleValues type, string? parent, params OpenXmlElement[] properties)
    {
        var style = new W.Style(new W.StyleName { Val = id }) { StyleId = id, Type = type };
        if (parent is not null) style.Append(new W.BasedOn { Val = parent });
        style.Append(new W.StyleRunProperties(properties)); return style;
    }
    private static W.Run Run(string? style, params OpenXmlElement[] direct)
    {
        var properties = new W.RunProperties(); if (style is not null) properties.Append(new W.RunStyle { Val = style });
        foreach (var item in direct) properties.Append(item);
        return new(properties, new W.Text("Café Ω"));
    }
    private static W.Paragraph Paragraph(string? style, params OpenXmlElement[] children)
    {
        var paragraph = new W.Paragraph(); if (style is not null) paragraph.Append(new W.ParagraphProperties(new W.ParagraphStyleId { Val = style }));
        foreach (var child in children) paragraph.Append(child); return paragraph;
    }
    private static ImportedDocument Import(W.Styles styles, params OpenXmlElement[] children)
    {
        var bytes = Package(styles, children); var original = bytes.ToArray();
        var result = new DocumentImportService().ImportDocx(bytes); Assert.Equal(original, bytes); return result;
    }
    private static byte[] Package(W.Styles styles, params OpenXmlElement[] children)
    {
        using var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = package.AddMainDocumentPart(); main.Document = new W.Document(new W.Body(children.Select(child => child.CloneNode(true))));
            main.AddNewPart<StyleDefinitionsPart>().Styles = (W.Styles)styles.CloneNode(true);
            var errors = new OpenXmlValidator().Validate(package).ToArray();
            Assert.True(errors.Length == 0, string.Join("\n", errors.Select(error => error.Description)));
        }
        return stream.ToArray();
    }
    private static void AssertFormatting(ImportedDocument imported, MarkdownEmphasis expected, string text = "Café Ω")
    {
        var syntax = new MarkdownService().Parse(imported.Markdown).Syntax;
        var format = syntax.Descendants<EmphasisInline>().Aggregate(MarkdownEmphasis.None, (value, emphasis) => value | MarkdownFormatting.Emphasis(emphasis));
        Assert.Equal(expected, format);
        Assert.Equal(text, string.Concat(syntax.Descendants<LiteralInline>().Select(literal => literal.Content.ToString())));
    }
}
