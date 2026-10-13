using System.Text;
using LensDocsStudio.Core.Editing;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;

namespace LensDocsStudio.Core.Tests;

public sealed class LargeDocumentSourceFidelityTests
{
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Formatting_a_distant_Unicode_item_retains_surrounding_bytes_and_anchor_offsets(string newline)
    {
        const string literal = "Café 😀 é 漢字";
        var source = "# Start" + newline + newline + string.Join(newline,
            Enumerable.Range(1, 4000).Select(i => "- Item " + i + " " + literal))
            + newline + newline + "## Distant target" + newline + newline + "[Return](#start) ![Image](assets/evidence.png)" + newline;
        var original = Encoding.UTF8.GetBytes(source);
        var offset = source.LastIndexOf(literal, StringComparison.Ordinal);
        var edit = new MarkdownEditService().Plan(source, offset, literal.Length, "bold");
        var formatted = source.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.Replacement);
        Assert.Equal(source[..offset] + "**" + literal + "**" + source[(offset + literal.Length)..], formatted);
        Assert.Equal(literal, formatted.Substring(edit.SelectionStart, edit.SelectionLength));
        var parsed = new MarkdownService().Parse(formatted);
        Assert.Equal(formatted, parsed.Source);
        Assert.Equal(4000, parsed.Syntax.OfType<ListBlock>().Single().Count);
        var heading = parsed.Headings.Single(h => h.Title == "Distant target");
        Assert.Equal(formatted.IndexOf("## Distant target", StringComparison.Ordinal), heading.SourceStart);
        var undo = new MarkdownEditService().Plan(formatted, edit.SelectionStart, edit.SelectionLength, "bold");
        var restored = formatted.Remove(undo.Start, undo.Length).Insert(undo.Start, undo.Replacement);
        Assert.Equal(original, Encoding.UTF8.GetBytes(restored));
    }
}
