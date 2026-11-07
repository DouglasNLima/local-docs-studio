using LensDocsStudio.Core.Editing;

namespace LensDocsStudio.Core.Tests;

public sealed class SlashCommandTests
{
    [Theory]
    [InlineData("/")] [InlineData("# Heading\n\n/heading 6")]
    [InlineData("---\ntitle: Test\n---\n\n/table")]
    [InlineData("~~~md\n# Ignored\n~~~\n\n/code")]
    [InlineData(":::mermaid\nflowchart LR\n:::\n\n/mermaid")]
    [InlineData("   /check")]
    [InlineData("```md\n<!-- example\n```\n\n/")]
    public void A_short_query_on_an_empty_prose_line_is_a_context(string source)
    {
        var context = SlashCommandService.Query(source, source.Length);
        Assert.NotNull(context); Assert.StartsWith("/", source.Substring(context.Start, context.Length));
    }
    [Theory]
    [InlineData("https://")] [InlineData("C:/folder/")] [InlineData("Existing /")]
    [InlineData("    /")] [InlineData("\t/")] [InlineData("```md\n/")]
    [InlineData("~~~\n/")] [InlineData(":::mermaid\n/")]
    [InlineData("---\ntitle: test\n/")] [InlineData("<!--\n/")]
    [InlineData("`/")] [InlineData("/path/to/")] [InlineData("\"/")]
    [InlineData("<pre>\n/")] [InlineData("<script>\n/")]
    public void Literal_paths_quotes_indented_code_fences_and_metadata_are_not_commands(string source) =>
        Assert.Null(SlashCommandService.Query(source, source.Length));

    [Fact]
    public void A_trigger_before_existing_text_or_beyond_the_analysis_limit_is_rejected()
    {
        Assert.Null(SlashCommandService.Query("/existing", 1));
        Assert.Null(SlashCommandService.Query(new string('x', SlashCommandService.MaximumSourceCharacters) + "\n/", SlashCommandService.MaximumSourceCharacters + 2));
    }
    [Theory]
    [InlineData("heading:6", "###### ")] [InlineData("bullet", "- ")]
    [InlineData("numbered", "1. ")] [InlineData("task", "- [ ] ")]
    [InlineData("quote", "> ")] [InlineData("code", "```")]
    [InlineData("mermaid", "```mermaid")] [InlineData("table", "| Column |")]
    [InlineData("link", "[Link text]")] [InlineData("figure", "<figure data-image-figure>")]
    [InlineData("callout:NOTE", "> [!NOTE]")] [InlineData("details", "<details data-details-block>")]
    [InlineData("rule", "---")] [InlineData("paragraph", "")]
    public void Existing_insertion_services_replace_the_entire_query_and_preserve_CRLF(string command, string marker)
    {
        const string source = "Before\r\n\r\n/query";
        var edit = SlashCommandService.Plan(source, source.Length, command)!;
        var result = source.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.Replacement);
        Assert.StartsWith("Before\r\n\r\n", result); Assert.DoesNotContain("/query", result); Assert.Contains(marker, result);
        Assert.DoesNotContain("\n", result.Replace("\r\n", ""));
        Assert.InRange(edit.SelectionStart, 0, result.Length); Assert.InRange(edit.SelectionStart + edit.SelectionLength, 0, result.Length);
    }
    [Fact]
    public void Dialog_commands_are_not_reimplemented_as_core_insertions()
    {
        foreach (var command in new[] { "image", "document-link", "snippet", "unknown" }) Assert.Null(SlashCommandService.Plan("/", 1, command));
    }
    [Fact]
    public void Indented_commands_preserve_prose_and_block_indentation_without_retaining_the_query()
    {
        const string source = "Before\r\n\r\n  /code";
        var edit = SlashCommandService.Plan(source, source.Length, "code")!;
        var result = source.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.Replacement);
        Assert.StartsWith("Before\r\n\r\n  ```", result); Assert.EndsWith("\r\n  ```", result);
        Assert.DoesNotContain("/code", result);
    }
    [Fact]
    public void Cached_prose_validation_is_rejected_when_an_earlier_edit_opens_a_fence()
    {
        var previous = SlashCommandService.Query("Body\n\n/", 7)!;
        Assert.NotNull(SlashCommandService.Query("Body\n\n/head", 11, previous));
        Assert.Null(SlashCommandService.Query("```\n\n/head", 10, previous));
    }
}
