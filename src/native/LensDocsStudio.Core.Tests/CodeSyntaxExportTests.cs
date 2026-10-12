using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using Markdig.Syntax;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Core.Tests;

public sealed class CodeSyntaxExportTests
{
    [Fact]
    public void Word_code_retains_editable_colours_tabs_spaces_and_line_breaks_without_an_extra_final_line()
    {
        const string code = "public string Label()\r\n{\r\n\tvar label = \"Café <studio> 😀\";  // retained spaces\r\n\treturn label;\r\n}";
        var (content, block) = Prepare(code);
        var text = block.Lines.ToString();
        var spans = new[] { new CodeSyntaxSpan(0, 6, CodeTokenRole.Keyword),
            new CodeSyntaxSpan(text.IndexOf('"'), "\"Café <studio> 😀\"".Length, CodeTokenRole.String) };
        content = content with { CodeSyntax = new(new Dictionary<CodeBlock, CodeSyntaxBlock> { [block] = new(text, spans) }, false) };
        using var stream = new MemoryStream(new DocxExporter().Export(content));
        using var word = WordprocessingDocument.Open(stream, false);
        Assert.Empty(new OpenXmlValidator().Validate(word));
        var paragraph = Assert.Single(word.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>());
        Assert.Equal(code.Replace("\r\n", "\n"), ReadCode(paragraph));
        Assert.Equal(2, paragraph.Descendants<W.TabChar>().Count());
        Assert.All(paragraph.Descendants<W.Text>(), value => Assert.Equal(SpaceProcessingModeValues.Preserve, value.Space!.Value));
        Assert.All(paragraph.Elements<W.Run>(), run => Assert.Equal("Consolas", run.RunProperties!.RunFonts!.Ascii!.Value));
        Assert.Equal("5B21B6", paragraph.Elements<W.Run>().Single(run => run.InnerText == "public").RunProperties!.Color!.Val!.Value);
        Assert.Equal("166534", paragraph.Elements<W.Run>().Single(run => run.InnerText.Contains("Café")).RunProperties!.Color!.Val!.Value);
        Assert.Equal("```csharp\r\n" + code + "\r\n```", content.Document.Source);
    }

    [Theory]
    [InlineData("unprepared")]
    [InlineData("stale")]
    [InlineData("negative")]
    [InlineData("overlap")]
    [InlineData("past-end")]
    [InlineData("unknown-role")]
    [InlineData("empty")]
    [InlineData("over-budget")]
    public void Word_keeps_complete_plain_code_when_colouring_cannot_be_applied(string variant)
    {
        const string code = "    plain  Café <tag>\r\n\tend";
        var (content, block) = Prepare(code);
        var text = block.Lines.ToString();
        CodeSyntaxSpan[] spans = variant switch
        {
            "negative" => [new(-1, 2, CodeTokenRole.Keyword)],
            "overlap" => [new(0, 4, CodeTokenRole.Keyword), new(2, 3, CodeTokenRole.String)],
            "past-end" => [new(0, int.MaxValue, CodeTokenRole.Keyword)],
            "unknown-role" => [new(0, 2, (CodeTokenRole)999)],
            "empty" => [new(0, 0, CodeTokenRole.Keyword)],
            "over-budget" => Enumerable.Repeat(new CodeSyntaxSpan(0, 1, CodeTokenRole.Keyword), 1025).ToArray(),
            _ => [new(0, 4, CodeTokenRole.Keyword)]
        };
        if (variant != "unprepared") content = content with
        {
            CodeSyntax = new(new Dictionary<CodeBlock, CodeSyntaxBlock> { [block] = new(variant == "stale" ? "different text" : text, spans) }, false)
        };
        using var stream = new MemoryStream(new DocxExporter().Export(content));
        using var word = WordprocessingDocument.Open(stream, false);
        Assert.Empty(new OpenXmlValidator().Validate(word));
        var paragraph = Assert.Single(word.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>());
        Assert.Equal(code.Replace("\r\n", "\n"), ReadCode(paragraph));
        Assert.All(paragraph.Elements<W.Run>(), run => Assert.Null(run.RunProperties?.Color));
    }

    [Fact]
    public void Colour_run_boundaries_do_not_duplicate_a_split_CRLF_break()
    {
        var (content, block) = Prepare("before\r\nafter");
        var text = block.Lines.ToString();
        content = content with { CodeSyntax = new(new Dictionary<CodeBlock, CodeSyntaxBlock>
            { [block] = new(text, [new(0, 7, CodeTokenRole.Comment), new(7, 1, CodeTokenRole.String)]) }, false) };
        using var stream = new MemoryStream(new DocxExporter().Export(content));
        using var word = WordprocessingDocument.Open(stream, false);
        var paragraph = Assert.Single(word.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>());
        Assert.Equal("before\nafter", ReadCode(paragraph));
        Assert.Single(paragraph.Descendants<W.Break>());
    }

    private static (ExportContent, FencedCodeBlock) Prepare(string code)
    {
        var parsed = new MarkdownService().Parse("```csharp\r\n" + code + "\r\n```");
        return (new(parsed, new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>()), parsed.Syntax.Descendants<FencedCodeBlock>().Single());
    }

    private static string ReadCode(W.Paragraph paragraph) => string.Concat(paragraph.Elements<W.Run>().SelectMany(run => run.ChildElements)
        .Select(element => element switch { W.Text text => text.Text, W.TabChar => "\t", W.Break => "\n", _ => "" }));
}
