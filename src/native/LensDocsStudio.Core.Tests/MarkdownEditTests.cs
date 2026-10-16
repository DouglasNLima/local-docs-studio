using LensDocsStudio.Core.Editing;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LensDocsStudio.Core.Tests;

public sealed class MarkdownEditTests
{
    private static string Apply(string source, MarkdownEdit edit) => source.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.Replacement);

    [Theory]
    [InlineData("bold", "**")]
    [InlineData("italic", "*")]
    [InlineData("strike", "~~")]
    public void Repeating_inline_format_toggles_only_the_selected_text_and_retains_outer_whitespace(string action, string marker)
    {
        const string source = "Before  selected  after";
        var service = new MarkdownEditService();
        var edit = service.Plan(source, 7, 10, action);
        var formatted = Apply(source, edit);
        Assert.Equal("Before  " + marker + "selected" + marker + "  after", formatted);
        Assert.Equal("selected", formatted.Substring(edit.SelectionStart, edit.SelectionLength));
        var toggled = service.Plan(formatted, edit.SelectionStart, edit.SelectionLength, action);
        Assert.Equal(source, Apply(formatted, toggled));
    }

    [Fact] public void Adding_italic_to_bold_source_does_not_strip_the_existing_bold_delimiters()
    {
        var service = new MarkdownEditService();
        Assert.Equal("***word***", Apply("**word**", service.Plan("**word**", 0, 8, "italic")));
        Assert.Equal("***word***", Apply("**word**", service.Plan("**word**", 2, 4, "italic")));
    }

    [Theory]
    [InlineData("italic", "**word**")]
    [InlineData("bold", "*word*")]
    public void Toggling_one_style_in_nested_emphasis_keeps_the_other_style(string action, string expected)
    {
        var service = new MarkdownEditService();
        Assert.Equal(expected, Apply("***word***", service.Plan("***word***", 3, 4, action)));
        Assert.Equal(expected, Apply("***word***", service.Plan("***word***", 0, 10, action)));
    }

    [Fact] public void Code_blocks_use_longer_fences_and_preserve_surrounding_source_and_CRLF()
    {
        const string selected = "```text\r\n# literal\r\n```";
        var source = "Before " + selected + " after\r\n";
        var edit = new MarkdownEditService().Plan(source, 7, selected.Length, "code");
        var formatted = Apply(source, edit);
        Assert.StartsWith("Before \r\n\r\n````text\r\n", formatted);
        Assert.EndsWith("\r\n````\r\n\r\n after\r\n", formatted);
        var code = Assert.Single(new MarkdownService().Parse(formatted).Syntax.Descendants<FencedCodeBlock>());
        Assert.Equal(selected, code.Lines.ToString());
    }

    [Fact] public void Inline_code_retains_literal_backticks_without_ending_the_span_early()
    {
        const string value = "`example`";
        var edit = new MarkdownEditService().Plan(value, 0, value.Length, "inline-code");
        var code = Assert.Single(new MarkdownService().Parse(Apply(value, edit)).Syntax.Descendants<CodeInline>());
        Assert.Equal(value, code.Content);
    }

    [Theory]
    [InlineData("heading", "## ")]
    [InlineData("task", "- [ ] ")]
    [InlineData("bullet", "- ")]
    [InlineData("quote", "> ")]
    public void Line_formatting_covers_selected_lines_and_toggles_without_changing_CRLF_or_adjacent_lines(string action, string marker)
    {
        const string source = "Outside\r\n  First\r\n  Second\r\nAfter";
        var service = new MarkdownEditService(); var start = source.IndexOf("First", StringComparison.Ordinal);
        var end = source.IndexOf("After", StringComparison.Ordinal);
        var edit = service.Plan(source, start, end - start, action); var result = Apply(source, edit);
        Assert.Equal("Outside\r\n  " + marker + "First\r\n  " + marker + "Second\r\nAfter", result);
        Assert.Equal(source, Apply(result, service.Plan(result, edit.SelectionStart, edit.SelectionLength, action)));
    }

    [Fact] public void Empty_line_commands_leave_the_caret_after_the_marker_instead_of_selecting_it()
    {
        var edit = new MarkdownEditService().Plan("Before\r\n", 8, 0, "heading");
        Assert.Equal("Before\r\n## ", Apply("Before\r\n", edit));
        Assert.Equal(11, edit.SelectionStart); Assert.Equal(0, edit.SelectionLength);
    }

    [Fact] public void Numbering_preserves_indentation_blank_lines_and_later_text()
    {
        const string source = "  One\n\n  Two\nAfter";
        var edit = new MarkdownEditService().Plan(source, 0, 13, "numbered");
        Assert.Equal("  1. One\n\n  2. Two\nAfter", Apply(source, edit));
    }

    [Fact] public void Converting_mixed_list_types_does_not_duplicate_markers_or_clear_existing_task_states()
    {
        const string source = "* First\n2. Second\n- [x] Checked";
        var edit = new MarkdownEditService().Plan(source, 0, source.Length, "task");
        Assert.Equal("- [ ] First\n- [ ] Second\n- [x] Checked", Apply(source, edit));
        const string headings = "# First\nPlain";
        Assert.Equal("## First\n## Plain", Apply(headings, new MarkdownEditService().Plan(headings, 0, headings.Length, "heading")));
    }

    [Theory]
    [InlineData("NOTE")]
    [InlineData("TIP")]
    [InlineData("IMPORTANT")]
    [InlineData("WARNING")]
    [InlineData("CAUTION")]
    public void Callout_insertion_wraps_selected_Markdown_and_retains_surrounding_paragraphs(string kind)
    {
        const string body = "**Selected**\n\nSupporting detail.";
        var source = "Before " + body + " after";
        var formatted = Apply(source, new MarkdownEditService().Plan(source, 7, body.Length, "callout:" + kind));
        var alert = Assert.Single(new MarkdownService().Parse(formatted).Syntax.Descendants<Markdig.Extensions.Alerts.AlertBlock>());
        Assert.Equal(kind, alert.Kind.ToString()); Assert.Contains(alert.Descendants<EmphasisInline>(), e => MarkdownService.InlineText(e) == "Selected");
        Assert.StartsWith("Before \n\n>", formatted); Assert.EndsWith("\n\n after", formatted);
    }

    [Fact] public void Template_insertion_retains_selected_text_and_selects_only_the_new_value_placeholder()
    {
        const string source = "Keep Value in this selection";
        var edit = new MarkdownEditService().Plan(source, 0, source.Length, "table");
        var formatted = Apply(source, edit);
        Assert.StartsWith(source + "\n\n", formatted);
        Assert.True(edit.SelectionStart > source.Length); Assert.Equal("Value", formatted.Substring(edit.SelectionStart, edit.SelectionLength));
    }
}
