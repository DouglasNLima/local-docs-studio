using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using Markdig.Syntax;

namespace LensDocsStudio.Core.Tests;

public sealed class HostMermaidFixtureTests
{
    [Theory]
    [InlineData("flowchart LR\nA[unfinished\n", true)]
    [InlineData("flowchart LR\nA[Write] --> B[Review]\n", true)]
    [InlineData("flowchart TD\n  A -->\n", false)]
    public void Host_fixture_expectations_use_the_pinned_renderer_and_preserve_exact_source(string source, bool accepted)
    {
        var result = new DiagramService().Render(source);
        Assert.Equal(accepted, result.Success); Assert.Equal(source, result.Source);
        var parsed = new MarkdownService().Parse(source, standaloneMermaid: true);
        Assert.Equal(source, parsed.Source); Assert.Equal(0, Assert.Single(parsed.Syntax.OfType<FencedCodeBlock>()).Span.Start);
        if (accepted) { Assert.Null(result.Error); Assert.Contains("<svg", result.Svg); }
        else { Assert.Null(result.Svg); Assert.Equal("An arrow is missing its destination. Complete the connection and try again.", result.Error); }
    }
}
