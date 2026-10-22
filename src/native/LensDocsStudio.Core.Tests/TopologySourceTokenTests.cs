using LensDocsStudio.Core.SourceAuthoring;

namespace LensDocsStudio.Core.Tests;

public class TopologySourceTokenTests
{
    private static SourceCall Call(SourceText text) => SourceCall.Read(text.Lines[0], SourceText.Pattern(@"^\s*(?<kind>\w+)\((?<args>.+)\)\s*$").Match(text.Lines[0].Body));
    [Fact]
    public void C4_tokens_preserve_ignored_empty_named_quoted_unicode_and_CRLF_bytes()
    {
        const string source = "  Container(a, \"Label, Ω\", , $tags=\"kept\", \"\", \"Description\")  \r\n";
        var text = SourceText.Read(source); var call = Call(text);
        Assert.True(call.Balanced); Assert.Equal(4, call.Positional.Count); Assert.Single(call.Arguments, a => a.Named); Assert.Single(call.Arguments, a => a.Ignored);
        Assert.Equal("Label, Ω", call.Value(1)); Assert.Equal("", call.Value(2)); Assert.Equal("Description", call.Value(3));
        var changed = call.Set(text, new Dictionary<int, string> { [2] = SourceCall.Quote("WPF"), [3] = SourceCall.Quote("") });
        Assert.Equal(source.Replace("\"\", \"Description\"", "\"WPF\", \"\"", StringComparison.Ordinal), changed.Source);
        Assert.Equal(text.Lines[0].Key, changed.Lines[0].Key);
    }
    [Fact]
    public void C4_missing_slots_are_added_explicitly_without_shifting_named_arguments()
    {
        var text = SourceText.Read("Container(a, \"A\", $tags=\"kept\")\n"); var changed = Call(text).Set(text, new Dictionary<int, string> { [3] = SourceCall.Quote("Details") });
        Assert.Equal("Container(a, \"A\", $tags=\"kept\", \"\", \"Details\")\n", changed.Source);
        Assert.Equal("", Call(changed).Value(2)); Assert.Equal("Details", Call(changed).Value(3));
    }
    [Theory, InlineData("A, B"), InlineData("A \"quoted\" value"), InlineData("Unicode Ω")]
    public void C4_new_quoted_values_round_trip_through_both_pinned_quote_stages(string value)
    { var text = SourceText.Read("Person(a," + SourceCall.Quote(value) + ")\n"); Assert.Equal(value, Call(text).Value(1)); }
    [Fact]
    public void C4_adjacent_quote_input_is_rejected_instead_of_silently_collapsing_it()
    { Assert.Throws<ArgumentException>(() => SourceCall.Quote("A\"\"B")); }
    [Fact]
    public void Topology_ordinary_preprocessing_keeps_the_existing_renderer_line_budget()
    {
        var source = string.Concat(Enumerable.Repeat("%% unchanged\r\n", 1600)) + "block\r\na[A]\r\n";
        Assert.Equal("block\r\na[A]\r\n", SourceText.PrepareRendererSource(source));
        Assert.Throws<ArgumentException>(() => SourceText.Read(source));
    }
}
