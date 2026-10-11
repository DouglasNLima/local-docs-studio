using System.Globalization;
using System.Text;
using AngleSharp.Html.Parser;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;

namespace LensDocsStudio.Core.Tests;

public sealed class ClipboardExportTests
{
    [Fact] public void Windows_clipboard_offsets_use_UTF8_bytes_for_multilingual_text_and_emoji()
    {
        const string fragment = "<div><p>Café — 日本語 🛡️</p></div>";
        var encoded = HtmlClipboardFormat.Encode(fragment);
        var bytes = Encoding.UTF8.GetBytes(encoded);
        int Offset(string name) => int.Parse(encoded.Split('\n').Single(l => l.StartsWith(name + ":", StringComparison.Ordinal)).Split(':')[1], CultureInfo.InvariantCulture);
        var start = Offset("StartFragment"); var end = Offset("EndFragment");
        Assert.Equal(fragment, Encoding.UTF8.GetString(bytes, start, end - start));
        Assert.Equal(bytes.Length, Offset("EndHTML")); Assert.Equal("<html ", Encoding.UTF8.GetString(bytes, Offset("StartHTML"), 6));
        Assert.Equal(fragment, HtmlClipboardFormat.Decode(encoded));
        Assert.DoesNotContain("SourceURL", encoded);
    }

    [Fact] public void Fragment_markers_inside_copied_code_do_not_truncate_the_offset_bounded_content()
    {
        const string fragment = "<div><code>&lt;!--EndFragment--&gt;</code><!--EndFragment--><p>Still copied</p></div>";
        Assert.Equal(fragment, HtmlClipboardFormat.Decode(HtmlClipboardFormat.Encode(fragment)));
    }

    [Theory]
    [InlineData("StartFragment:0000000000")]
    [InlineData("StartFragment:9999999999")]
    [InlineData("EndFragment:0000000001")]
    public void Invalid_fragment_offsets_are_rejected_instead_of_reading_unselected_context(string replacement)
    {
        var name = replacement.Split(':')[0];
        var encoded = HtmlClipboardFormat.Encode("<div>Selected</div>");
        encoded = string.Join('\n', encoded.Split('\n').Select(l => l.StartsWith(name + ":", StringComparison.Ordinal) ? replacement + "\r" : l));
        Assert.Throws<IOException>(() => HtmlClipboardFormat.Decode(encoded));
    }

    [Fact] public void Raw_HTML_and_null_terminated_external_clipboard_payloads_are_supported()
    {
        Assert.Equal("<p>External HTML</p>", HtmlClipboardFormat.Decode("<p>External HTML</p>\0"));
        Assert.Equal("<p>External HTML</p>", HtmlClipboardFormat.Decode(HtmlClipboardFormat.Encode("<p>External HTML</p>") + "\0"));
    }

    [Fact] public void Rendered_clipboard_keeps_table_cells_and_embeds_images_without_native_controls_or_active_HTML()
    {
        const string source = "# Café\n\n| Feature | Status |\n|---|---|\n| Image | Ready |\n\n![Diagram](assets/image.png)\n\n<script>alert(1)</script>\n";
        var content = new ExportContent(new MarkdownService().Parse(source), new Dictionary<int, ExportImage>(),
            new Dictionary<string, ExportImage> { ["assets/image.png"] = new([1, 2, 3], 1, 1, "Diagram") });
        var result = new ClipboardExporter().Export(content);
        var html = new HtmlParser().ParseDocument(HtmlClipboardFormat.Decode(result.Html));
        Assert.Single(html.QuerySelectorAll("table")); Assert.Equal(2, html.QuerySelectorAll("tbody td").Length);
        Assert.Equal("data:image/png;base64,AQID", Assert.Single(html.QuerySelectorAll("img")).GetAttribute("src"));
        Assert.Empty(html.QuerySelectorAll("script,button,iframe"));
        Assert.Contains("Feature\tStatus", result.PlainText); Assert.Contains("Image\tReady", result.PlainText); Assert.Contains("Café", result.PlainText);
        Assert.DoesNotContain("Copy source", result.PlainText); Assert.DoesNotContain("assets/image.png", result.Html);
    }

    [Fact] public void Oversized_clipboard_source_is_rejected_before_HTML_generation()
    {
        var parsed = new ParsedDocument(new string('a', 4 * 1024 * 1024 + 1), new(), []);
        Assert.Throws<IOException>(() => new ClipboardExporter().Export(new(parsed, new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>())));
    }
    [Fact] public void Generic_Markdown_attributes_cannot_add_active_clipboard_HTML()
    {
        var parsed = new MarkdownService().Parse("# Heading {#chapter onclick=\"alert(1)\" style=\"background:url(https://example.invalid/beacon)\"}\n\n[Link](https://example.com){onmouseover=\"alert(2)\"}\n");
        var exported = new ClipboardExporter().Export(new(parsed, new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>()));
        var html = new HtmlParser().ParseDocument(HtmlClipboardFormat.Decode(exported.Html));
        Assert.Equal("chapter", html.QuerySelector("h1")!.Id);
        Assert.Empty(html.QuerySelectorAll("[onclick],[onmouseover],[style]"));
        Assert.DoesNotContain("beacon", exported.Html); Assert.DoesNotContain("alert(", exported.Html);
        Assert.Equal("https://example.com", html.QuerySelector("a")!.GetAttribute("href"));
    }
    [Fact] public void Rich_copy_keeps_checked_and_unchecked_task_states_in_both_clipboard_representations()
    {
        var parsed = new MarkdownService().Parse("- [x] Complete\n- [ ] Pending\n");
        var result = new ClipboardExporter().Export(new(parsed, new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>()));
        var html = new HtmlParser().ParseDocument(HtmlClipboardFormat.Decode(result.Html));
        Assert.Single(html.QuerySelectorAll("input[checked]")); Assert.Equal(2, html.QuerySelectorAll("input[disabled]").Length);
        Assert.Contains("[x]", result.PlainText); Assert.Contains("[ ]", result.PlainText);
    }

    [Fact] public void Clipboard_fragment_offsets_cannot_split_a_UTF8_character()
    {
        var encoded = HtmlClipboardFormat.Encode("<div>Café</div>");
        var partialEnd = Encoding.UTF8.GetByteCount(encoded[..encoded.IndexOf('é')]) + 1;
        var changed = string.Join('\n', encoded.Split('\n').Select(l => l.StartsWith("EndFragment:", StringComparison.Ordinal) ? "EndFragment:" + partialEnd.ToString("D10", CultureInfo.InvariantCulture) + "\r" : l));
        Assert.Throws<IOException>(() => HtmlClipboardFormat.Decode(changed));
    }

    [Fact] public void Clipboard_tables_identify_literal_cells_without_changing_visible_text_or_plain_fallback()
    {
        var parsed = new MarkdownService().Parse("| Name | Value |\n|---|---|\n| Formula | =1+1 |\n| Identifier | 000123 |");
        var result = new ClipboardExporter().Export(new(parsed, new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>()));
        var html = new HtmlParser().ParseDocument(HtmlClipboardFormat.Decode(result.Html));
        Assert.True(html.QuerySelector("table")!.HasAttribute("x:str"));
        Assert.All(html.QuerySelectorAll("td,th"), cell => Assert.Contains("mso-number-format:\"\\@\";", cell.GetAttribute("style")));
        Assert.Contains("Formula\t=1+1", result.PlainText); Assert.Contains("Identifier\t000123", result.PlainText);
        Assert.Contains("=1+1", html.Body!.TextContent); Assert.Contains("000123", html.Body.TextContent);
    }
}
