using LensDocsStudio.Core.Editing;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;

namespace LensDocsStudio.Core.Tests;

public sealed class InlineLabelEditTests
{
    private static string Apply(string source, MarkdownEdit edit) => source.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.Replacement);

    [Theory]
    [InlineData("draft", "Draft")]
    [InlineData("ready", "Ready")]
    [InlineData("blocked", "Blocked")]
    [InlineData("deprecated", "Deprecated")]
    [InlineData("beta", "Beta")]
    [InlineData("review", "In review")]
    public void Every_web_status_kind_inserts_as_a_typed_native_label_without_changing_surrounding_source(string kind, string label)
    {
        const string source = "Before selected after\r\n";
        Assert.Equal(label, InlineLabelEditService.StatusLabel(kind));
        var edit = InlineLabelEditService.Badge(new MarkdownService().Parse(source), 7, 8, kind, label);
        var result = Apply(source, edit);
        var semantic = Assert.Single(new MarkdownService().Parse(result).Syntax.Descendants<DocumentSemanticInline>());
        Assert.Equal(DocumentInlineKind.StatusBadge, semantic.Kind); Assert.Equal(label, semantic.Text); Assert.Equal(kind, semantic.Variant);
        Assert.StartsWith("Before ", result); Assert.EndsWith(" after\r\n", result);
        Assert.Equal(edit.Replacement, result.Substring(edit.SelectionStart, edit.SelectionLength));
    }

    [Fact]
    public void User_label_markup_remains_literal_in_native_semantics_and_HTML_export()
    {
        const string label = "**Café** & <script>alert('x')</script> [link](file.md) `code`";
        var edit = InlineLabelEditService.Badge(new MarkdownService().Parse(""), 0, 0, "ready", label);
        var document = new MarkdownService().Parse(edit.Replacement);
        Assert.Equal(label, Assert.Single(document.Syntax.Descendants<DocumentSemanticInline>()).Text);
        var html = new HtmlExporter().RenderFragment(new(document, new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>()));
        Assert.DoesNotContain("<script>", html); Assert.DoesNotContain("<a ", html); Assert.DoesNotContain("<code>", html);
        Assert.Contains("&lt;script&gt;", html); Assert.Contains("**Café**", html);
    }

    [Fact]
    public void A_caret_inside_an_existing_badge_updates_its_whole_span_without_nesting()
    {
        const string source = "Before <span data-status-badge data-status-kind=ready>Ready</span> after";
        var edit = InlineLabelEditService.Badge(new MarkdownService().Parse(source), source.IndexOf("Ready", StringComparison.Ordinal) + 1, 0, "review", "Needs review Ω");
        var result = Apply(source, edit);
        var semantic = Assert.Single(new MarkdownService().Parse(result).Syntax.Descendants<DocumentSemanticInline>());
        Assert.Equal("review", semantic.Variant); Assert.Equal("Needs review Ω", semantic.Text);
        Assert.Equal("Before " + edit.Replacement + " after", result);
    }

    [Theory]
    [InlineData('x')]
    [InlineData('*')]
    [InlineData('&')]
    [InlineData('\\')]
    public void Maximum_length_badges_retain_literal_text_and_remain_editable(char character)
    {
        var label = new string(character, 512);
        var edit = InlineLabelEditService.Badge(new MarkdownService().Parse(""), 0, 0, "ready", label);
        var document = new MarkdownService().Parse(edit.Replacement);
        var semantic = Assert.Single(document.Syntax.Descendants<DocumentSemanticInline>());
        Assert.Equal(label, semantic.Text);
        var changed = InlineLabelEditService.Badge(document, semantic.Span.Start + 2, 0, "review", "Revised");
        Assert.Equal(edit.Replacement.Length, changed.Length);
        Assert.Equal("Revised", Assert.Single(new MarkdownService().Parse(Apply(edit.Replacement, changed)).Syntax.Descendants<DocumentSemanticInline>()).Text);
    }

    [Theory]
    [InlineData('x')]
    [InlineData('*')]
    [InlineData('&')]
    [InlineData('\\')]
    public void Maximum_length_shortcut_keys_retain_literal_text(char character)
    {
        var key = new string(character, 64);
        var edit = InlineLabelEditService.Shortcut(new MarkdownService().Parse(""), 0, 0, "Ctrl + " + key);
        var semantic = Assert.Single(new MarkdownService().Parse(edit.Replacement).Syntax.Descendants<DocumentSemanticInline>());
        Assert.Equal(DocumentInlineKind.Shortcut, semantic.Kind);
        Assert.Equal("Ctrl + " + key, semantic.Text);
    }

    [Fact]
    public void Shortcut_keys_keep_literal_labels_and_reopen_as_one_editable_semantic_span()
    {
        const string source = "Use selection here.";
        var edit = InlineLabelEditService.Shortcut(new MarkdownService().Parse(source), 4, 9, "Ctrl, Shift + É & <key>");
        var result = Apply(source, edit);
        var document = new MarkdownService().Parse(result);
        var semantic = Assert.Single(document.Syntax.Descendants<DocumentSemanticInline>());
        Assert.Equal(DocumentInlineKind.Shortcut, semantic.Kind); Assert.Equal("Ctrl + Shift + É & <key>", semantic.Text);
        var changed = InlineLabelEditService.Shortcut(document, semantic.Span.Start + 2, 0, "Alt + Enter");
        Assert.Equal("Use " + changed.Replacement + " here.", Apply(result, changed));
        Assert.Equal("Alt + Enter", Assert.Single(new MarkdownService().Parse(Apply(result, changed)).Syntax.Descendants<DocumentSemanticInline>()).Text);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("line\nbreak")]
    [InlineData("line\rbreak")]
    [InlineData("tab\tkey")]
    [InlineData("null\0key")]
    public void Labels_and_keys_reject_empty_or_control_character_input(string value)
    {
        var document = new MarkdownService().Parse("Original");
        Assert.Throws<ArgumentException>(() => InlineLabelEditService.Badge(document, 0, 8, "ready", value));
        Assert.Throws<ArgumentException>(() => InlineLabelEditService.Shortcut(document, 0, 8, value));
        Assert.Equal("Original", document.Source);
    }

    [Fact]
    public void Unsupported_kinds_excessive_labels_and_excessive_key_counts_are_rejected_before_any_edit()
    {
        var document = new MarkdownService().Parse("Original");
        Assert.Throws<ArgumentException>(() => InlineLabelEditService.Badge(document, 0, 8, "unknown", "Label"));
        Assert.Throws<ArgumentException>(() => InlineLabelEditService.Badge(document, 0, 8, "ready", new string('x', 513)));
        Assert.Throws<ArgumentException>(() => InlineLabelEditService.Shortcut(document, 0, 8, new string('x', 65)));
        Assert.Throws<ArgumentException>(() => InlineLabelEditService.Shortcut(document, 0, 8, "1+2+3+4+5+6+7+8+9"));
        Assert.Throws<ArgumentException>(() => InlineLabelEditService.Shortcut(document, 0, 8, "++,,"));
        Assert.Equal("Original", document.Source);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Horizontal_rules_create_a_real_block_and_retain_the_documents_line_endings(string newline)
    {
        var source = "Before" + newline + "selected" + newline + "After";
        var edit = new MarkdownEditService().Plan(source, 6 + newline.Length, 8, "rule");
        var result = Apply(source, edit);
        Assert.Equal("Before" + newline + newline + "---" + newline + newline + "After", result);
        Assert.Single(new MarkdownService().Parse(result).Syntax.Descendants<ThematicBreakBlock>());
        Assert.Equal(0, edit.SelectionLength);
    }
}
