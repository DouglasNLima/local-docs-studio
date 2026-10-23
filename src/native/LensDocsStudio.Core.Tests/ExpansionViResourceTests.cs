using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;

namespace LensDocsStudio.Core.Tests;

public class ExpansionViResourceTests
{
    [Theory, InlineData("packet", "0: \"Bit\""), InlineData("packet-beta", "+1: \"Bit\""), InlineData("treeView", "file.txt"), InlineData("treeView-beta", "folder/"), InlineData("treemap-beta", "\"A\": 1"), InlineData("venn-beta", "set A")]
    public void All_aliases_adapt_only_leading_inert_trivia_and_retain_exact_authoritative_source(string header, string body)
    {
        var source = "%% exact Ω\r\n\r\n" + header + "\r\n" + body + "\r\n";
        var rendered = new DiagramService().Render(source);
        Assert.True(rendered.Success, rendered.Error); Assert.Equal(source, rendered.Source);
        Assert.Equal(header + "\r\n" + body + "\r\n", SourceText.PrepareRendererSource(source));
    }
    [Theory, InlineData("treeView"), InlineData("treemap-beta"), InlineData("TREEVIEW-BETA"), InlineData("TREEMAP-BETA")]
    public void Hierarchy_element_budget_is_checked_before_renderer_recursion(string header)
    {
        var source = header + "\n" + string.Concat(Enumerable.Repeat("\"A\": 1\n", 501));
        var result = new DiagramService().Render(source);
        Assert.False(result.Success); Assert.Contains("500-element", result.Error); Assert.Equal(source, result.Source);
    }
    [Theory, InlineData("treeView", "folder/"), InlineData("treemap-beta", "\"Section\"")]
    public void Hierarchy_depth_is_checked_before_recursive_ComputedValue(string header, string row)
    {
        var source = header + "\n" + string.Concat(Enumerable.Range(0, 66).Select(i => new string(' ', i) + row + "\n"));
        var result = new DiagramService().Render(source); Assert.False(result.Success); Assert.Contains("64-level", result.Error); Assert.Equal(source, result.Source);
    }
    [Fact]
    public void Packet_native_split_segments_share_the_existing_element_budget()
    {
        var source = "packet\n" + string.Concat(Enumerable.Repeat("0-4095: \"Wide\"\n", 4));
        var result = new DiagramService().Render(source); Assert.False(result.Success); Assert.Contains("500-element", result.Error); Assert.Equal(source, result.Source);
        Assert.True(new DiagramService().Render("packet\n0-4095: \"One bounded field\"\n").Success);
    }
    [Theory, InlineData("treemap-beta\n\"A\": "), InlineData("venn-beta\nset A[\"A\"]: "), InlineData("venn-beta\nunion A,B[\"AB\"]: ")]
    public void Decimal_overflow_fails_closed_instead_of_silently_storing_infinity(string prefix)
    {
        var source = prefix + new string('9', 400) + "\n"; var result = new DiagramService().Render(source);
        Assert.False(result.Success); Assert.Contains("finite", result.Error); Assert.Equal(source, result.Source);
    }
    [Fact]
    public void Treemap_aggregate_overflow_and_subnormal_scale_are_guarded_before_squarification()
    {
        var large = "1" + new string('0', 308);
        foreach (var source in new[] { "treemap-beta\n\"Group\"\n \"A\": " + large + "\n \"B\": " + large + "\n", "treemap-beta\n\"Tiny\": 0." + new string('0', 322) + "5\n" })
        { var result = new DiagramService().Render(source); Assert.False(result.Success); Assert.Contains("finite", result.Error); Assert.Equal(source, result.Source); }
    }
    [Fact]
    public void Ordinary_trivia_does_not_acquire_the_visual_editor_line_limit()
    {
        var source = string.Concat(Enumerable.Repeat("%% exact retained\r\n", 1600)) + "packet\r\n0: \"A\"\r\n";
        Assert.True(new DiagramService().Render(source).Success); Assert.Throws<ArgumentException>(() => SourceText.Read(source));
    }
    [Fact]
    public void Packet_skipped_overflow_does_not_advance_the_accepted_cursor()
    {
        var cursor = 8;
        foreach (var row in new[] { "+0: \"Skipped\"", "4096: \"Skipped\"", "+9999999999999999999: \"Skipped\"", "9-8: \"Skipped\"" })
            Assert.False(ExpansionViSource.PacketRange(ExpansionViSource.PacketField.Match(row), cursor, out _, out _));
        Assert.True(ExpansionViSource.PacketRange(ExpansionViSource.PacketField.Match("+04: \"Accepted\""), cursor, out var start, out var end)); Assert.Equal((8, 11), (start, end));
    }
}
