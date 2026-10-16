using LensDocsStudio.Core.Editing;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;

namespace LensDocsStudio.Core.Tests;

public sealed class ImageFigureEditTests
{
    [Fact] public void Standalone_image_conversion_keeps_CRLF_neighbours_destination_and_title()
    {
        const string source = "# Before\r\n\r\n![Original](<assets/a&b.png> \"A title\")\r\n\r\nAfter.";
        var start = source.IndexOf("![", StringComparison.Ordinal);
        var edit = ImageFigureEditService.Plan(new MarkdownService().Parse(source), start + 3, 0, new("New & description", "Caption <one>"));
        var result = source.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.Replacement);
        Assert.StartsWith("# Before\r\n\r\n<figure", result); Assert.EndsWith("</figure>\r\n\r\nAfter.", result);
        Assert.DoesNotContain("\n", edit.Replacement.Replace("\r\n", ""));
        var figure = Assert.Single(new MarkdownService().Parse(result).Syntax.Descendants<DocumentImageFigureBlock>());
        Assert.Equal("assets/a&b.png", figure.Image.Url); Assert.Equal("A title", figure.Image.Title);
        Assert.Equal("New & description", MarkdownService.InlineText(figure.Image)); Assert.Equal("Caption <one>", figure.Caption);
        Assert.Equal(start, edit.SelectionStart); Assert.Equal(edit.Replacement.Length, edit.SelectionLength);
    }

    [Fact] public void Converting_a_reference_image_keeps_shared_definitions_and_other_links()
    {
        const string source = "![Original][asset]\n\n[Download][asset]\n\n[asset]: image.png \"Retained title\"\n";
        var edit = ImageFigureEditService.Plan(new MarkdownService().Parse(source), 3, 0, new("Description", "Caption"));
        var result = source.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.Replacement);
        Assert.EndsWith("\n\n[Download][asset]\n\n[asset]: image.png \"Retained title\"\n", result);
        var selected = ImageFigureEditService.Find(new MarkdownService().Parse(result), edit.Start, edit.Replacement.Length)!;
        Assert.Equal("image.png", selected.Reference); Assert.Equal("Retained title", selected.Title);
    }

    [Fact] public void Editing_a_figure_changes_only_its_own_block_and_can_remove_its_caption()
    {
        const string source = "Before\n\n<figure data-image-figure><img src='image.png' alt='Original'><figcaption>Old caption</figcaption></figure>\n\nAfter";
        var edit = ImageFigureEditService.Plan(new MarkdownService().Parse(source), source.IndexOf("Old", StringComparison.Ordinal), 3, new("  New\ttext  ", ""));
        var result = source.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.Replacement);
        Assert.StartsWith("Before\n\n", result); Assert.EndsWith("\n\nAfter", result); Assert.DoesNotContain("figcaption", result);
        var selected = ImageFigureEditService.Find(new MarkdownService().Parse(result), edit.Start, edit.SelectionLength)!;
        Assert.Equal("New text", selected.Options.AlternativeText); Assert.Equal("", selected.Options.Caption); Assert.Equal("image.png", selected.Reference);
    }

    [Theory]
    [InlineData("Text ![Inline](a.png)")]
    [InlineData("![Inline](a.png) text")]
    [InlineData("![One](a.png) ![Two](b.png)")]
    [InlineData("> ![Quoted](a.png)")]
    [InlineData("- ![Listed](a.png)")]
    [InlineData("<img src='a.png'>")]
    public void Images_without_a_supported_standalone_block_are_not_replaced(string source)
    {
        var document = new MarkdownService().Parse(source);
        Assert.Null(ImageFigureEditService.Find(document, 2, 0));
        Assert.Throws<ArgumentException>(() => ImageFigureEditService.Plan(document, 2, 0, new("Text", "Caption")));
        Assert.Equal(source, document.Source);
    }

    [Theory]
    [InlineData(-1, 0)] [InlineData(0, -1)] [InlineData(int.MaxValue, int.MaxValue)]
    public void Invalid_selections_are_rejected(int start, int length)
        => Assert.Throws<ArgumentOutOfRangeException>(() => ImageFigureEditService.Find(new MarkdownService().Parse("![Image](a.png)"), start, length));

    [Fact] public void Invalid_caption_text_is_rejected_without_a_partial_edit()
    {
        var document = new MarkdownService().Parse("![Image](a.png)");
        foreach (var options in new[] { new ImageFigureOptions(new string('a', 4097), "Caption"), new("Image", new string('c', 4097)), new("Image", "Control\0character") })
            Assert.Throws<ArgumentException>(() => ImageFigureEditService.Plan(document, 2, 0, options));
        Assert.Equal("![Image](a.png)", document.Source);
    }
}
