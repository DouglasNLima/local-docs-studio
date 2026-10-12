using LensDocsStudio.Core.Parsing;
using Markdig.Extensions.Mathematics;
using Markdig.Syntax;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Rendering;
using AngleSharp.Html.Parser;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using LensDocsStudio.Core.Imports;

namespace LensDocsStudio.Core.Tests;

public sealed class MathematicsTests
{
    [Theory] [InlineData("$E=mc^2$", "E=mc^2", false)] [InlineData(@"\(\frac{a_1}{b^2}\)", @"\frac{a_1}{b^2}", false)]
    [InlineData(@"$$\sqrt{x}$$", @"\sqrt{x}", true)] [InlineData("$$\n\\frac{a_1}{b^2}\n$$", @"\frac{a_1}{b^2}", true)]
    public void Formulae_retain_original_source_positions_and_display_intent(string markup, string expected, bool display)
    {
        var source = "# Before\r\n\r\n" + markup.Replace("\n", "\r\n") + "\r\n\r\nAfter";
        var parsed = new MarkdownService().Parse(source); var formula = Assert.Single(DocumentMathematics.Read(parsed.Syntax));
        Assert.Equal(12, formula.SourceStart); Assert.Equal(expected, formula.Text); Assert.Equal(display, formula.Display); Assert.True(formula.Complete);
        Assert.Equal(source, parsed.Source);
    }
    [Theory] [InlineData("$ incomplete")] [InlineData("$ spaced $")] [InlineData("$x$5")] [InlineData(@"\$x$")]
    [InlineData("`$x$`")] [InlineData("```tex\n$x$\n```")] [InlineData(@"\(unclosed")]
    public void Incomplete_currency_escaped_and_code_examples_remain_literal(string source)
        => Assert.Empty(DocumentMathematics.Read(new MarkdownService().Parse(source).Syntax));

    [Fact] public void Headings_links_and_nested_details_retain_formula_source_as_readable_text()
    {
        var parsed = new MarkdownService().Parse("# Formula $x^2$\n\n<details>\n<summary>Proof</summary>\n\n[Result $y$](guide.md) and \\(a+b\\).\n</details>");
        Assert.Equal("Formula x^2", parsed.Headings[0].Title); Assert.Equal(3, DocumentMathematics.Read(parsed.Syntax).Count());
        Assert.Contains(parsed.Syntax.Descendants<MathInline>(), inline => inline.Content.ToString() == "a+b");
    }
    [Fact] public void Unclosed_display_formula_is_identified_as_incomplete()
        => Assert.False(Assert.Single(DocumentMathematics.Read(new MarkdownService().Parse("$$\nx+y").Syntax)).Complete);

    [Theory] [InlineData("Literal $x$ and (parentheses)")] [InlineData(@"Literal \(x+y\) and &copy;")]
    [InlineData("[link](https://example.com) and $5 or $10")]
    public void Imported_literal_text_cannot_accidentally_become_formulae(string literal)
    {
        var imported = new HtmlMarkdownConverter().Convert("<p>" + System.Net.WebUtility.HtmlEncode(literal) + "</p>");
        var parsed = new MarkdownService().Parse(imported.Markdown); Assert.Empty(DocumentMathematics.Read(parsed.Syntax));
        Assert.Equal(literal, MarkdownService.InlineText(Assert.IsType<ParagraphBlock>(parsed.Syntax[0]).Inline));
    }

    [Theory] [InlineData(@"\begin{pmatrix}a & b \\ c & d\end{pmatrix}", @"\left(\matrix{a & b \\ c & d}\right)")]
    [InlineData(@"\begin{bmatrix}a & b\end{bmatrix}", @"\left[\matrix{a & b}\right]")]
    [InlineData(@"\begin{cases}x & x>0\end{cases}", @"\cases{x & x>0}")]
    [InlineData(@"\pmatrix{a & b \\ c & d}", @"\left(\matrix{a & b \\ c & d}\right)")]
    [InlineData(@"\begin{matrix}\pmatrix{a} & b\end{matrix}", @"\matrix{\left(\matrix{a}\right) & b}")]
    public void Standard_matrix_delimiters_are_adapted_without_changing_cells(string source, string expected)
        => Assert.Equal(expected, MathFormulaSource.Prepare(source));

    [Theory] [InlineData(@"\begin{pmatrix}a\end{bmatrix}")] [InlineData(@"\begin{bmatrix}a")]
    [InlineData(@"\pmatrix{unclosed")] [InlineData(@"\end{matrix}")]
    public void Mismatched_environments_are_rejected_before_native_rendering(string source)
        => Assert.Throws<ArgumentException>(() => MathFormulaSource.Prepare(source));

    [Fact] public void Exports_without_prepared_formulae_retain_readable_source_instead_of_losing_content()
    {
        const string source = "# Formula $x^2$\n\nInline \\(\\frac{a}{b}\\).\n\n$$\n\\notARealCommand{<script>}\n$$";
        var content = new ExportContent(new MarkdownService().Parse(source), new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>());
        var html = new HtmlParser().ParseDocument(new HtmlExporter().Export(content));
        Assert.Empty(html.QuerySelectorAll("script")); Assert.Contains(@"\notARealCommand{<script>}", html.Body!.TextContent);
        Assert.Contains(@"\frac{a}{b}", new ClipboardExporter().Export(content).PlainText);
        using var word = WordprocessingDocument.Open(new MemoryStream(new DocxExporter().Export(content)), false);
        Assert.Empty(new OpenXmlValidator().Validate(word)); Assert.Contains(@"\notARealCommand{<script>}", word.MainDocumentPart!.Document!.InnerText);
        Assert.Contains("x^2", word.MainDocumentPart.Document!.InnerText);
    }

    [Fact] public void Prepared_formulae_preserve_alt_text_display_size_and_site_heading_identity()
    {
        var document = new MarkdownService().Parse("# Formula $x^2$\n\nMaths \\(a+b\\).");
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg==");
        var content = new ExportContent(document, new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>())
        { Formulae = DocumentMathematics.Read(document.Syntax).ToDictionary(formula => formula.SourceStart, formula => new ExportImage(png, 180, 90, formula.Text, 60, 30)) };
        var html = new HtmlParser().ParseDocument(new HtmlExporter().Export(content)); var images = html.QuerySelectorAll(".document-math img");
        Assert.Equal(2, images.Length); Assert.Equal("x^2", images[0].GetAttribute("alt")); Assert.All(images, image => Assert.Equal("60", image.GetAttribute("width")));
        var site = new DocsSiteExporter().PreparePage("formula.md", content);
        Assert.Equal("Formula x^2", site.Title); Assert.Equal(document.Headings[0].Anchor, site.Headings[0].Id); Assert.Equal("Formula x^2", site.Headings[0].Text);
        Assert.Equal(document.Source, new MarkdownClipboardExporter().Export(content).PlainText);
        using var word = WordprocessingDocument.Open(new MemoryStream(new DocxExporter().Export(content)), false);
        Assert.Equal(2, word.MainDocumentPart!.ImageParts.Count()); Assert.Empty(new OpenXmlValidator().Validate(word));
        Assert.All(word.MainDocumentPart.Document!.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent>(), extent => Assert.Equal(60 * 9525, extent.Cx!.Value));
    }
}
