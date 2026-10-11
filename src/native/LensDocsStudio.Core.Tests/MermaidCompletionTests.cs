using LensDocsStudio.Core.Editing;

namespace LensDocsStudio.Core.Tests;

public sealed class MermaidCompletionTests
{
    [Theory]
    [InlineData("```mermaid\nflo")]
    [InlineData("~~~mmd\nflo")]
    [InlineData("````mermaid\n```\nflo")]
    [InlineData("```Mermaid\nflo")]
    [InlineData("::: mermaid\nflo")]
    [InlineData(":::mermaid\r\n  flo")]
    [InlineData("Paragraph\n\n```text\n:::mermaid\n```\n\n~~~mermaid\nflo")]
    public void Completion_recognises_supported_fences_and_preserves_surrounding_source(string source)
    {
        var context = MermaidCompletionService.Query(source, source.Length, false)!;
        Assert.Equal("flo", context.Prefix); Assert.Equal("flowchart TD", Assert.Single(context.Items).Label);
        var edit = MermaidCompletionService.Plan(source + "\n```\nAfter", source.Length, false, "flowchart TD")!;
        Assert.Equal("flo", source.Substring(edit.Start, edit.Length));
        Assert.EndsWith("\n```\nAfter", Apply(source + "\n```\nAfter", edit));
    }

    [Theory]
    [InlineData("Ordinary flo")]
    [InlineData("```csharp\nflo")]
    [InlineData("```text\n```mermaid\nflo")]
    [InlineData("```mermaid\n```\nflo")]
    [InlineData("~~~mermaid\n~~~\nflo")]
    [InlineData(":::mermaid\n:::\nflo")]
    [InlineData("```mermaid")]
    [InlineData("```mermaid\n%% flo")]
    [InlineData("```mermaid\nA[\"flo")]
    public void Completion_stays_outside_prose_other_code_closed_fences_comments_and_quoted_labels(string source)
        => Assert.Null(MermaidCompletionService.Query(source, source.Length, false, true));

    [Fact] public void Completion_of_a_partial_token_replaces_its_suffix_without_duplicate_text()
    {
        const string source = "flowchart\nAfter";
        var edit = MermaidCompletionService.Plan(source, 3, true, "flowchart TD")!;
        Assert.Equal("flowchart TD\n  A[Start] --> B[Finish]\nAfter", Apply(source, edit));
        Assert.Equal(edit.Start + edit.Replacement.Length, edit.SelectionStart); Assert.Equal(0, edit.SelectionLength);
    }

    [Fact] public void Multiline_completion_preserves_CRLF_and_the_current_indentation()
    {
        const string source = "```mermaid\r\n  sub\r\n```";
        var edit = MermaidCompletionService.Plan(source, source.IndexOf("sub", StringComparison.Ordinal) + 3, false, "subgraph")!;
        Assert.Equal("```mermaid\r\n  subgraph Group\r\n    A[Item]\r\n  end\r\n```", Apply(source, edit));
    }

    [Fact] public void Explicit_completion_includes_the_catalogue_and_automatic_completion_requires_two_characters()
    {
        Assert.Null(MermaidCompletionService.Query("f", 1, true));
        Assert.Equal(13, MermaidCompletionService.Query("", 0, true, true)!.Items.Count);
        Assert.Contains(MermaidCompletionService.Query("graph", 5, true)!.Items, i => i.Label == "flowchart TD");
        Assert.Equal("arrow", Assert.Single(MermaidCompletionService.Query("--", 2, true)!.Items).Label);
        Assert.Null(MermaidCompletionService.Plan("flo", 3, true, "participant"));
    }

    [Fact] public void Typing_and_explicit_requests_have_separate_bounded_scan_limits()
    {
        var source = new string(' ', MermaidCompletionService.AutomaticLimit) + "\n```mermaid\nflo";
        Assert.Null(MermaidCompletionService.Query(source, source.Length, false));
        Assert.NotNull(MermaidCompletionService.Query(source, source.Length, false, true));
        source = new string(' ', MermaidCompletionService.ExplicitLimit + 1);
        Assert.Null(MermaidCompletionService.Query(source, source.Length, true, true));
    }
    private static string Apply(string source, MarkdownEdit edit) => source[..edit.Start] + edit.Replacement + source[(edit.Start + edit.Length)..];
}
