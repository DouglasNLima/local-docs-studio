using System.Xml.Linq;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using Markdig.Syntax;

namespace LensDocsStudio.Core.Tests;

public sealed class RenderingTests
{
    [Fact]
    public void Headings_retain_exact_source_positions()
    {
        const string source = "# Title\r\n\r\nParagraph.\r\n\r\n## Café section\r\n";
        var parsed = new MarkdownService().Parse(source);
        Assert.Equal(source.IndexOf("##", StringComparison.Ordinal), parsed.Headings[1].SourceStart);
        Assert.Equal("Café section", parsed.Headings[1].Title);
        Assert.NotEmpty(parsed.Headings[1].Anchor);
    }

    [Fact]
    public void Devops_fences_preserve_following_source_offsets()
    {
        const string source = "::: mermaid\r\nflowchart LR\r\nA --> B\r\n:::\r\n\r\n## After\r\n";
        var parsed = new MarkdownService().Parse(source);
        Assert.IsType<FencedCodeBlock>(parsed.Syntax[0]);
        Assert.Equal(source.IndexOf("##", StringComparison.Ordinal), Assert.Single(parsed.Headings).SourceStart);
    }

    [Theory]
    [InlineData("::: note\nText\n:::")]
    [InlineData("```text\n::: mermaid\nA --> B\n:::\n```")]
    [InlineData("~~~text\n::: mermaid\nA --> B\n:::\n~~~")]
    public void Unrelated_containers_and_code_examples_are_preserved(string source) => Assert.Equal(source, MarkdownService.NormaliseDevOps(source));

    [Fact]
    public void Standalone_diagram_source_reference_does_not_include_a_synthetic_fence()
    {
        const string source = "flowchart LR\nA --> B";
        var block = Assert.IsType<FencedCodeBlock>(new MarkdownService().Parse(source, true).Syntax[0]);
        Assert.Equal(0, block.Span.Start);
        Assert.Equal(source.Length - 1, block.Span.End);
    }

    [Theory]
    [InlineData("flowchart LR\nA[Write] --> B{Review}\nB -->|Yes| C[Export]")]
    [InlineData("sequenceDiagram\nAlice->>Bob: Hello\nBob-->>Alice: Welcome")]
    [InlineData("flowchart TD\nsubgraph Workspace\nA[Draft] --> B[Review]\nend\nB --> C[Publish]")]
    [InlineData("classDiagram\nDocument <|-- Markdown\nDocument : +string Text")]
    [InlineData("stateDiagram-v2\n[*] --> Draft\nDraft --> Published")]
    [InlineData("erDiagram\nDOCUMENT ||--o{ ASSET : contains")]
    [InlineData("pie title Coverage\n\"Tested\" : 70\n\"Pending\" : 30")]
    public void Representative_diagrams_produce_sanitised_svg(string source)
    {
        var result = new DiagramService().Render(source);
        Assert.True(result.Success, result.Error);
        var xml = XDocument.Parse(result.Svg!);
        Assert.Equal("svg", xml.Root!.Name.LocalName);
        Assert.DoesNotContain(xml.Descendants(), element => element.Name.LocalName is "script" or "foreignObject");
        Assert.Contains(xml.Descendants(), element => element.Name.LocalName == "text");
    }

    [Fact]
    public void Invalid_diagram_is_visible_as_an_error()
    {
        var result = new DiagramService().Render("flowchart TD\n A -->");
        Assert.False(result.Success);
        Assert.NotEmpty(result.Error!);
    }
}
