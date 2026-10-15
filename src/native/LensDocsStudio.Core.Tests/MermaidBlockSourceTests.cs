using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;

namespace LensDocsStudio.Core.Tests;

public sealed class MermaidBlockSourceTests
{
    [Theory]
    [InlineData("```mermaid", "```", "\n")]
    [InlineData("````mermaid extra", "````", "\r\n")]
    [InlineData("~~~~mmd", "~~~~", "\r\n")]
    [InlineData("  ````mmd", "  ````", "\r\n")]
    [InlineData("::: mermaid", ":::", "\r\n")]
    public void Replacing_the_second_identical_block_preserves_fences_and_every_other_byte(string open, string close, string newline)
    {
        var body = "flowchart LR" + newline + "  A[Same] --> B[Same]" + newline;
        var block = open + newline + body + close;
        var source = "Before" + newline + newline + block + newline + newline + "Between" + newline + newline + block + newline + "After";
        var parsed = new MarkdownService().Parse(source); var targets = MermaidBlockSource.Find(parsed);
        Assert.True(targets.Count == 2, string.Join("; ", parsed.Syntax.Select(block => $"{block.GetType().Name}: {block.Span}")));
        var target = targets[1]; Assert.Equal(body, target.Source);
        var replacement = body.Replace("B[Same]", "B[Changed]", StringComparison.Ordinal);
        var result = MermaidPatches.Apply(source, [new(target.Body, body, replacement)]);
        Assert.Equal(source[..target.Body.Start] + replacement + source[target.Body.End..], result);
        Assert.Equal(body, targets[0].Body.Read(result)); Assert.Contains(open, result); Assert.EndsWith(close + newline + "After", result);
    }
    [Theory] [InlineData(".mmd")] [InlineData(".mermaid")]
    public void Standalone_targets_never_capture_the_synthetic_markdown_fence(string extension)
    {
        Assert.Contains(extension, new[] { ".mmd", ".mermaid" });
        const string source = "flowchart TD\r\nA[Input] --> B[Output]";
        var target = Assert.Single(MermaidBlockSource.Find(new MarkdownService().Parse(source, true), true));
        Assert.True(target.Standalone); Assert.Equal(source, target.Source); Assert.Equal(new(0, source.Length), target.Body);
        Assert.Equal("graph LR\nC --> D", target.Replacement("graph LR\nC --> D"));
    }
    [Fact]
    public void Actual_edit_history_relocates_the_correct_identical_occurrence_and_rejects_touched_targets()
    {
        const string block = "```mermaid\nflowchart TD\nA --> B\n```\n";
        const string source = "Before\n" + block + "Middle\n" + block;
        var target = MermaidBlockSource.Find(new MarkdownService().Parse(source))[1];
        var changed = "Longer paragraph\n" + source[7..];
        var span = target.Relocate(changed, [new(0, 7, 17)]);
        Assert.Equal(target.Body.Start + 10, span.Start); Assert.Equal(target.Source, span.Read(changed));
        Assert.Throws<ArgumentException>(() => target.Relocate(source, [new(target.Body.Start + 1, 1, 1)]));
        Assert.Throws<ArgumentException>(() => target.Relocate(source, [new(target.Container.Start, target.Container.Length, target.Container.Length)]));
        Assert.Throws<ArgumentException>(() => target.Relocate(source[..target.Container.Start], [new(target.Container.Start, target.Container.Length, 0)]));
        Assert.Equal(target.Body, target.Relocate(source + "After", [new(source.Length, 0, 5)]));
    }
    [Fact]
    public void Unclosed_fences_and_block_quote_prefixes_are_not_claimed_as_editable_targets()
    {
        Assert.Empty(MermaidBlockSource.Find(new MarkdownService().Parse("```mermaid\nflowchart TD\nA --> B")));
        Assert.Empty(MermaidBlockSource.Find(new MarkdownService().Parse("> ```mermaid\n> flowchart TD\n> A --> B\n> ```")));
        Assert.Single(MermaidBlockSource.Find(new MarkdownService().Parse("  ~~~~mmd\nflowchart TD\nA --> B\n  ~~~~")));
        // In this surrounding-paragraph context the established parser produces a paragraph.
        // A visual action must use recognised syntax rather than inventing a text-match identity.
        Assert.Empty(MermaidBlockSource.Find(new MarkdownService().Parse("Before\r\n\r\n  ~~~~mmd\r\nflowchart TD\r\nA --> B\r\n  ~~~~\r\nAfter")));
    }
}
