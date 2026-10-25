using Markdig.Syntax;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Parsing;

namespace LensDocsStudio.Core.Tests;

public sealed class MermaidCreationTargetTests
{
    private static readonly MarkdownService Markdown = new();
    private static readonly string Diagram = "flowchart TD\n  A[Start] --> B[End]\n";
    private static string Fence(string body) => "```mermaid\n" + body + "```\n";

    [Theory]
    [MemberData(nameof(MermaidCreationCatalogueTests.Starters), MemberType = typeof(MermaidCreationCatalogueTests))]
    public void Every_family_maps_the_exact_second_duplicate_body(MermaidDiagramFamily family, string variant)
    {
        var source = MermaidDiagramCatalogue.Get(family).Variants.Single(item => item.Id == variant).Source;
        var document = Markdown.Parse("# Before\n\n" + Fence(source) + "\nBetween Ω😀\n\n" + Fence(source) + "\nAfter");
        var blocks = MermaidBlockSource.Find(document);
        var target = MermaidCreationTarget.Resolve(document, blocks[1].Body.Start + 1, 2);
        Assert.Equal(MermaidCreationRoute.ExistingDiagram, target.Route);
        Assert.Equal(blocks[1], target.Block);
        Assert.NotEqual(blocks[0].Body.Start, target.Block!.Body.Start);
    }

    [Fact]
    public void Fences_and_selections_over_multiple_containers_fail_closed()
    {
        var doc = Markdown.Parse(Fence(Diagram) + "\n" + Fence(Diagram));
        var blocks = MermaidBlockSource.Find(doc);
        foreach (var at in new[] { 0, 3, blocks[0].Body.Start - 1, blocks[0].Body.End, blocks[0].Container.End - 2 })
            Assert.Equal(MermaidCreationRoute.SourceOnly, MermaidCreationTarget.Resolve(doc, at, 0).Route);
        Assert.Equal(MermaidCreationRoute.SourceOnly, MermaidCreationTarget.Resolve(doc, blocks[0].Body.Start, blocks[1].Body.End - blocks[0].Body.Start).Route);
        Assert.Equal(MermaidCreationRoute.SourceOnly, MermaidCreationTarget.Resolve(doc, 0, blocks[0].Container.Length).Route);
    }

    [Theory]
    [InlineData("```csharp\nvar x = 1;\n```\n", 12)]
    [InlineData("```mermaid\nflowchart TD\n", 14)]
    [InlineData("    ordinary code\n", 6)]
    [InlineData("> ```mermaid\n> flowchart TD\n> ```\n", 20)]
    [InlineData("- List item\n    ```mermaid\n    flowchart TD\n    ```\n", 3)]
    [InlineData("<div>protected</div>\n", 7)]
    public void Ordinary_unclosed_and_nested_structures_do_not_become_new_diagrams(string source, int at)
        {
        var parsed = Markdown.Parse(source);
        Assert.True(MermaidCreationTarget.Resolve(parsed, at, 0).Route == MermaidCreationRoute.SourceOnly,
            string.Join("; ", parsed.Syntax.Descendants<Block>().Select(b => b.GetType().Name + " " + b.Span + (b is FencedCodeBlock c ? " closing=" + c.ClosingFencedCharCount + " lines=" + c.Lines.ToString() : ""))));
    }

    [Theory]
    [InlineData("", 0, 0)]
    [InlineData("Before Ω😀 after", 0, 0)]
    [InlineData("Before Ω😀 after", 7, 0)]
    [InlineData("Before Ω😀 after", 16, 0)]
    [InlineData("Before Ω😀 after", 0, 6)]
    [InlineData("\uFEFFBefore\r\nAfter\r\n", 0, 0)]
    [InlineData("Before\r\n\r\nAfter", 10, 0)]
    public void Insertion_preserves_every_outer_character_and_nonempty_selection(string source, int start, int length)
    {
        var target = MermaidCreationTarget.Resolve(Markdown.Parse(source), start, length);
        Assert.Equal(MermaidCreationRoute.NewDiagram, target.Route);
        var newline = source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var body = Diagram.Replace("\n", newline, StringComparison.Ordinal);
        var inserted = MermaidCreationTarget.FormatInsertion(body, source, new(target.InsertionOffset, 0));
        var candidate = source.Insert(target.InsertionOffset, inserted);
        var block = Assert.Single(MermaidBlockSource.Find(Markdown.Parse(candidate)));
        Assert.Equal(body, block.Source);
        Assert.Equal(source, candidate.Remove(target.InsertionOffset, inserted.Length));
        Assert.True(target.InsertionOffset >= start + length);
        if (source.StartsWith('\uFEFF')) Assert.StartsWith("\uFEFF", candidate);
    }

    [Fact]
    public void Adjacent_fences_and_long_backtick_runs_roundtrip_through_the_actual_parser()
    {
        var source = Fence(Diagram) + "\n" + Fence(Diagram);
        var offset = Fence(Diagram).Length;
        var target = MermaidCreationTarget.Resolve(Markdown.Parse(source), offset, 0);
        Assert.Equal(MermaidCreationRoute.NewDiagram, target.Route);
        var draft = "flowchart TD\n  %% literal ``````` in this source\n  A --> B\n";
        var formatted = MermaidCreationTarget.FormatInsertion(draft, source, new(offset, 0));
        Assert.Contains("````````mermaid", formatted);
        var blocks = MermaidBlockSource.Find(Markdown.Parse(source.Insert(offset, formatted)));
        Assert.Equal(new[] { Diagram, draft, Diagram }, blocks.Select(block => block.Source));
    }

    [Theory]
    [InlineData("", MermaidCreationRoute.NewDiagram)]
    [InlineData(" \r\n", MermaidCreationRoute.NewDiagram)]
    [InlineData("flowchart TD\n", MermaidCreationRoute.ExistingDiagram)]
    [InlineData("unsupported source", MermaidCreationRoute.ExistingDiagram)]
    public void Standalone_nonempty_source_always_routes_to_its_existing_occurrence(string source, MermaidCreationRoute expected)
        => Assert.Equal(expected, MermaidCreationTarget.Resolve(Markdown.Parse(source, true), 0, 0).Route);

    [Fact]
    public void Split_unicode_and_line_endings_are_refused_and_invalid_selections_throw()
    {
        Assert.Equal(MermaidCreationRoute.SourceOnly, MermaidCreationTarget.Resolve(Markdown.Parse("a😀b"), 2, 0).Route);
        Assert.Equal(MermaidCreationRoute.SourceOnly, MermaidCreationTarget.Resolve(Markdown.Parse("a\r\nb"), 2, 0).Route);
        Assert.Throws<ArgumentException>(() => MermaidCreationTarget.Resolve(Markdown.Parse("a"), 1, 1));
        Assert.Throws<ArgumentException>(() => MermaidCreationTarget.FormatInsertion(Diagram, "selection", new(0, 9)));
    }
}
