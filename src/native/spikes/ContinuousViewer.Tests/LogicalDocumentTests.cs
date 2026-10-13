using ContinuousViewer.Model;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;

namespace ContinuousViewer.Tests;

public sealed class LogicalDocumentTests
{
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void SelectionRetainsDecodedTextSourceAndGraphemes(string newline)
    {
        var source = $"# Heading{newline}{newline}**Café 😀 é** &amp; review.{newline}{newline}- First{newline}- Last";
        var model = LogicalDocument.Build(new MarkdownService().Parse(source));
        Assert.Equal(source, model.Parsed.Source);
        Assert.Equal("Heading\nCafé 😀 é & review.\n• First\n• Last\n", model.Text);
        var selection = new LogicalSelection(model); var emoji = model.Text.IndexOf("😀", StringComparison.Ordinal);
        selection.Set(emoji + 1, emoji + 1); Assert.Equal("😀", selection.Copy().UnicodeText);
        selection.Set(0, model.Text.Length); var copy = selection.Copy();
        Assert.Equal(model.Text, copy.UnicodeText);
        Assert.Contains("<strong>Café 😀 é</strong>", System.Net.WebUtility.HtmlDecode(HtmlClipboardFormat.Decode(copy.CfHtml)), StringComparison.Ordinal);
        Assert.Contains("\\u-10179?\\u-8704?", copy.Rtf, StringComparison.Ordinal);
        Assert.Contains("**Café 😀 é**", copy.Markdown, StringComparison.Ordinal);
    }
    [Fact]
    public void LargeSingleBlockIsSplitWithoutDroppingCharactersOrSource()
    {
        var fixture = FixtureCatalogue.All.Single(f => f.Name == "oversized");
        var model = LogicalDocument.Build(new MarkdownService().Parse(fixture.Source));
        Assert.Equal(fixture.Source + "\n", model.Text);
        Assert.True(model.Pieces.Length > 600);
        Assert.All(model.Pieces, p => Assert.InRange(p.Length, 1, LogicalDocument.PieceCharacters));
        var last = model.AtSource(fixture.Source.Length - 5); Assert.Equal(model.Pieces[^1], last);
        var selection = new LogicalSelection(model); selection.Set(250, model.Text.Length - 3);
        Assert.Equal(model.Text[250..^3], selection.Copy().UnicodeText);
    }
    [Fact]
    public void CompleteLargeDocumentAndDistantMappingDoNotRequireVisuals()
    {
        var fixture = FixtureCatalogue.All.Single(f => f.Name == "large-4mb");
        Assert.Equal(4140025, System.Text.Encoding.UTF8.GetByteCount(fixture.Source));
        var model = LogicalDocument.Build(new MarkdownService().Parse(fixture.Source));
        Assert.Equal(20001, model.Pieces.Length); Assert.Contains(fixture.Last, model.Text, StringComparison.Ordinal);
        Assert.Equal(model.Pieces[^1], model.AtSource(fixture.Source.LastIndexOf("Paragraph 19999", StringComparison.Ordinal)));
    }
    [Fact]
    public void NestedDetailsAndTablesRemainLogicalWhenCollapsed()
    {
        var fixture = FixtureCatalogue.All.Single(f => f.Name == "rich"); var parsed = new MarkdownService().Parse(fixture.Source);
        var model = LogicalDocument.Build(parsed);
        Assert.Equal(2, model.Groups.Length);
        Assert.Contains(model.Pieces, p => p.Details.Length == 2 && p.Kind == PieceKind.Heading);
        Assert.Contains("Café\tFirst\nSecond\t😀", model.Text, StringComparison.Ordinal);
        Assert.Contains("1. First item", model.Text, StringComparison.Ordinal);
        Assert.Contains("☑", model.Text, StringComparison.Ordinal); Assert.Contains("Wiki label", model.Text, StringComparison.Ordinal);
        var target = parsed.Targets.Single(t => t.Id == "distant"); Assert.Equal(PieceKind.Heading, model.AtSource(target.SourceStart).Kind);
        var selection = new LogicalSelection(model); selection.Set(0, model.Text.Length);
        Assert.Contains(fixture.Last, selection.Copy().UnicodeText, StringComparison.Ordinal);
        Assert.Equal(fixture.Source, parsed.Source);
    }
    [Fact]
    public void CancelledProjectionCannotReturnACompleteLogicalDocument()
    {
        var parsed = new MarkdownService().Parse("# Cancelled\n\nRetained source.");
        Assert.Throws<OperationCanceledException>(() => LogicalDocument.Build(parsed, new CancellationToken(true)));
        Assert.Equal("# Cancelled\n\nRetained source.", parsed.Source);
    }
    [Fact]
    public void ClipboardRetainsSupportedDecorationsAndCancellation()
    {
        var model = LogicalDocument.Build(new MarkdownService().Parse("**Bold** *italic* ~~strike~~ ++underlined++ H~2~ x^2^ ==marked=="));
        var selection = new LogicalSelection(model); selection.Set(0, model.Text.Length); var copy = selection.Copy();
        var html = HtmlClipboardFormat.Decode(copy.CfHtml);
        Assert.Contains("<s>strike</s>", html, StringComparison.Ordinal);
        Assert.Contains("<u>underlined</u>", html, StringComparison.Ordinal);
        Assert.Contains("<sub>2</sub>", html, StringComparison.Ordinal);
        Assert.Contains("<sup>2</sup>", html, StringComparison.Ordinal);
        Assert.Contains("<mark>marked</mark>", html, StringComparison.Ordinal);
        Assert.Throws<OperationCanceledException>(() => selection.Copy(new CancellationToken(true)));
    }
    [Theory]
    [InlineData("cell")]
    [InlineData("code")]
    [InlineData("quote")]
    [InlineData("list")]
    public void DistantOffsetsInsideOversizedSemanticBlocksResolveTheFinalPiece(string kind)
    {
        var text = new string('a', 350000) + "Final retained target.";
        var source = kind switch { "cell" => "| Content |\n|---|\n| " + text + " |", "code" => "```text\n" + text + "\n```", "quote" => "> " + text, _ => "- " + text };
        var model = LogicalDocument.Build(new MarkdownService().Parse(source));
        Assert.Equal(model.Pieces[^1], model.AtSource(source.LastIndexOf("Final retained target.", StringComparison.Ordinal)));
        Assert.Contains("Final retained target.", string.Concat(model.Pieces[^1].Runs.Select(r => r.Text)), StringComparison.Ordinal);
    }
}
