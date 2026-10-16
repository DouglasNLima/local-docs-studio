using LensDocsStudio.Core.Editing;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;

namespace LensDocsStudio.Core.Tests;

public sealed class DetailsEditTests
{
    private static string Apply(string source, MarkdownEdit edit) => source.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.Replacement);
    [Theory] [InlineData("\n")] [InlineData("\r\n")]
    public void Inserting_details_retains_surrounding_source_body_links_and_document_line_endings(string newline)
    {
        var source = "Before\n\nSelected **body** with [guide](guide.md).\n\nAfter".Replace("\n", newline);
        var start = source.IndexOf("Selected", StringComparison.Ordinal); var length = source.IndexOf(newline + newline + "After", StringComparison.Ordinal) - start;
        var document = new MarkdownService().Parse(source); var options = DetailsEditService.ReadOptions(document, start, length);
        Assert.Equal(source.Substring(start, length), options.Body);
        var edit = DetailsEditService.Plan(document, start, length, options with { Summary = "Café <review> & evidence", InitiallyExpanded = true });
        var changed = Apply(source, edit); var parsed = new MarkdownService().Parse(changed); var details = Assert.Single(parsed.Syntax.Descendants<DocumentDetailsBlock>());
        Assert.StartsWith("Before" + newline + newline, changed); Assert.EndsWith(newline + newline + "After", changed);
        Assert.Equal("Café <review> & evidence", details.Summary); Assert.True(details.InitiallyExpanded); Assert.Contains("&lt;review&gt; &amp; evidence", changed);
        Assert.Equal("guide.md", Assert.Single(MarkdownLinks.Read(changed)).Reference); Assert.Equal(edit.Replacement.Trim('\r', '\n'), changed.Substring(edit.SelectionStart, edit.SelectionLength));
        if (newline == "\r\n") Assert.DoesNotContain("\n", changed.Replace("\r\n", ""));
    }
    [Fact] public void Summary_and_expansion_edits_preserve_exact_existing_body_whitespace_and_surrounding_source()
    {
        const string body = "\r\n\r\nBody  \r\ncontinued.\r\n\r\n[guide]: docs/guide.md\r\n\r\n";
        const string source = "Before\r\n\r\n<details>\r\n<summary>Old label</summary>\r\n" + body + "</details>\r\n\r\nAfter";
        var document = new MarkdownService().Parse(source); var start = source.IndexOf("continued", StringComparison.Ordinal);
        var options = DetailsEditService.ReadOptions(document, start, 0);
        var edit = DetailsEditService.Plan(document, start, 0, options with { Summary = "New label", InitiallyExpanded = true });
        var changed = Apply(source, edit); Assert.Contains("</summary>\r\n" + body + "</details>", changed);
        Assert.StartsWith("Before\r\n\r\n", changed); Assert.EndsWith("\r\n\r\nAfter", changed);
        Assert.Equal("New label", Assert.Single(new MarkdownService().Parse(changed).Syntax.Descendants<DocumentDetailsBlock>()).Summary);
    }
    [Fact] public void The_innermost_containing_section_is_edited_without_changing_its_outer_body()
    {
        const string source = "<details>\n<summary>Outer</summary>\n\nBefore\n\n<details>\n<summary>Inner</summary>\n\nNeedle\n</details>\n\nAfter\n</details>";
        var document = new MarkdownService().Parse(source); var position = source.IndexOf("Needle", StringComparison.Ordinal);
        var selected = DetailsEditService.Find(document, position, 0)!; Assert.Equal("Inner", selected.Summary);
        var edit = DetailsEditService.Plan(document, position, 0, DetailsEditService.ReadOptions(document, position, 0) with { Summary = "Edited inner" });
        Assert.Equal(selected.Span.Start, edit.Start); var changed = Apply(source, edit);
        Assert.Equal(source[..selected.Span.Start], changed[..edit.Start]); Assert.EndsWith(source[(selected.Span.End + 1)..], changed);
        Assert.Equal(new[] { "Outer", "Edited inner" }, new MarkdownService().Parse(changed).Syntax.Descendants<DocumentDetailsBlock>().Select(block => block.Summary));
    }
    [Fact] public void Body_edits_preserve_nested_Markdown_and_allow_literal_closing_tags_in_complete_code_fences()
    {
        var document = new MarkdownService().Parse("");
        var edit = DetailsEditService.Plan(document, 0, 0, new("Examples", "```html\n</details>\n```\n\n**Retained**"));
        var details = Assert.Single(new MarkdownService().Parse(edit.Replacement).Syntax.Descendants<DocumentDetailsBlock>());
        Assert.Single(details.Descendants<FencedCodeBlock>()); Assert.Contains("**Retained**", edit.Replacement);
    }
    [Theory] [InlineData("Body\n</details>\nEscaped body")] [InlineData("```html\nUnclosed fence")]
    [InlineData("<details>\n<summary>Unclosed child</summary>\nBody")]
    public void Unbalanced_bodies_are_rejected_before_source_is_replaced(string body)
        => Assert.Throws<ArgumentException>(() => DetailsEditService.Plan(new MarkdownService().Parse("Retained"), 0, 8, new("Label", body)));
    [Theory] [InlineData("")] [InlineData("  ")] [InlineData("Line\nbreak")] [InlineData("Nul\0byte")]
    public void Invalid_summaries_are_rejected_without_an_edit(string summary)
        => Assert.Throws<ArgumentException>(() => DetailsEditService.Plan(new MarkdownService().Parse("Retained"), 0, 0, new(summary, "Body")));
    [Fact] public void Invalid_ranges_and_oversized_content_are_rejected_without_a_partial_plan()
    {
        var document = new MarkdownService().Parse("Retained");
        Assert.Throws<ArgumentOutOfRangeException>(() => DetailsEditService.ReadOptions(document, -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => DetailsEditService.Plan(document, 0, 20, new("Label", "Body")));
        Assert.Throws<ArgumentException>(() => DetailsEditService.Plan(document, 0, 0, new(new string('x', 513), "Body")));
        Assert.Throws<ArgumentException>(() => DetailsEditService.Plan(document, 0, 0, new("Label", new string('x', DetailsEditService.MaximumBodyCharacters + 1))));
        Assert.Throws<ArgumentException>(() => DetailsEditService.Plan(document, 0, 0, new("Label", "Body\0")));
        Assert.Equal("Retained", document.Source);
    }
}
