using System.Text.Json;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using Markdig.Syntax;
using Mermaider;
using Xunit.Abstractions;
using System.Xml.Linq;
using LensDocsStudio.Core.Mermaid;

namespace LensDocsStudio.Core.Tests;

public sealed class FlowchartCompatibilityTests(ITestOutputHelper output)
{
    private static string Markdown => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "native-flowchart", "spc-application-architecture.md"));
    private static string Source => new MarkdownService().Parse(Markdown).Syntax.Descendants<FencedCodeBlock>().Single().Lines.ToString();

    [Fact]
    public void Pinned_upstream_reproduction_retains_the_phantom_class_node_without_adaptation()
    {
        var graph = MermaidRenderer.Parse(Source);
        output.WriteLine(JsonSerializer.Serialize(graph));
        Assert.Equal(10, graph.Nodes.Count);
        Assert.True(graph.Nodes.ContainsKey("class"));
        Assert.Empty(graph.ClassAssignments);
        Assert.Equal(8, graph.ClassDefs.Count);
    }
    [Fact]
    public void Lens_rendering_preserves_the_exact_SPC_source_and_all_nine_graph_nodes()
    {
        var source = Source; var markdown = Markdown;
        var rendered = new DiagramService().Render(source, false);
        output.WriteLine(rendered.Error ?? rendered.Svg ?? "No output");
        Assert.True(rendered.Success, rendered.Error);
        Assert.Equal(source, rendered.Source); Assert.Equal(markdown, Markdown);
        var graph = rendered.FlowchartSemantics!;
        Assert.Equal(9, graph.Nodes.Count); Assert.False(graph.Nodes.ContainsKey("class"));
        Assert.Equal(9, graph.Edges.Count); Assert.Equal(8, graph.ClassDefs.Count); Assert.Equal(9, graph.ClassAssignments.Count);
        Assert.Equal(new[] { "App", "Automation", "DV", "Logic", "PCF" }, graph.Subgraphs.Single().NodeIds.Order());
        Assert.Equal("interface", graph.ClassAssignments["App"]); Assert.Equal("interface", graph.ClassAssignments["PCF"]);
        Assert.Equal("1px", graph.ClassDefs["users"]["stroke-width"]);
        Assert.Equal("#143A59", graph.NodeStyles["SPC"]["color"]);
        var svg = XDocument.Parse(rendered.Svg!); XNamespace ns = "http://www.w3.org/2000/svg";
        var group = svg.Descendants(ns + "g").Single(element => (string?)element.Attribute("data-id") == "SPC");
        Assert.Equal("6 4", (string?)group.Element(ns + "rect")!.Attribute("stroke-dasharray"));
        Assert.Equal("#143A59", (string?)svg.Descendants(ns + "text").Single(element => (string?)element.Attribute("data-subgraph-id") == "SPC").Attribute("fill"));
        Assert.Contains("small", rendered.FidelityWarning);
        var inspected = new MermaidSourceParser().Parse(source);
        Assert.Equal(9, inspected.Nodes.Count); Assert.Equal(MermaidCompatibility.Protected, inspected.Compatibility);
        Assert.All(inspected.Nodes, node => Assert.Equal(graph.Nodes[node.Id].Label, node.Label));
    }
    [Fact]
    public void SPC_semantics_are_identical_with_and_without_directive_terminators()
    {
        var without = string.Join('\n', Source.Split('\n').Select(line => line.TrimStart().StartsWith("class", StringComparison.Ordinal)
            || line.TrimStart().StartsWith("style ", StringComparison.Ordinal) ? line.TrimEnd(';') : line));
        var a = new DiagramService().Render(Source, false); var b = new DiagramService().Render(without, false);
        Assert.True(b.Success, b.Error); Assert.Equal(JsonSerializer.Serialize(a.FlowchartSemantics), JsonSerializer.Serialize(b.FlowchartSemantics));
    }
    [Theory]
    [InlineData("class A")] [InlineData("class A bad extra;")]
    [InlineData("classDef colour;")] [InlineData("classDef colour nonsense;")]
    [InlineData("style A;")] [InlineData("style A --> B;")]
    public void Malformed_styling_is_reported_instead_of_becoming_a_node(string directive)
    {
        var result = new DiagramService().Render("flowchart TB\nA[Known]\n" + directive);
        Assert.False(result.Success); Assert.Equal(DiagramFailureKind.UnsupportedSyntax, result.FailureKind);
        Assert.NotNull(result.Error);
    }
    [Fact]
    public void Compound_class_names_keep_protected_inspection_without_creating_a_phantom_node()
    {
        const string source = "flowchart LR\nA[Known]\nclass A red,blue;\n";
        var result = new DiagramService().Render(source);
        Assert.True(result.Success, result.Error); Assert.Single(result.FlowchartSemantics!.Nodes);
        Assert.False(result.FlowchartSemantics.Nodes.ContainsKey("class")); Assert.Contains("protected source", result.FidelityWarning);
        Assert.Equal(source, result.Source); Assert.Equal(MermaidCompatibility.Protected, new MermaidSourceParser().Parse(source).Compatibility);
    }
    [Fact]
    public void Multiple_statements_comma_targets_and_multiple_classes_retain_effective_colours_and_borders()
    {
        const string source = "flowchart TB\nA[One] --> B[Two]\nclassDef red fill:#AA0000;\nclassDef border stroke:#112233,stroke-width:2px;\nclass A, B red; class A border;\nclass B border;\nstyle Missing fill:#FFFFFF;\n";
        var result = new DiagramService().Render(source); Assert.True(result.Success, result.Error);
        Assert.Equal(2, result.FlowchartSemantics!.Nodes.Count); Assert.Single(result.FlowchartSemantics.Edges);
        Assert.True(result.FlowchartSemantics.NodeStyles.ContainsKey("Missing")); Assert.False(result.FlowchartSemantics.Nodes.ContainsKey("Missing"));
        Assert.All(result.FlowchartLayout!.Nodes, node =>
        {
            Assert.Equal("#AA0000", node.InlineStyle!["fill"]); Assert.Equal("#112233", node.InlineStyle["stroke"]); Assert.Equal("2px", node.InlineStyle["stroke-width"]);
        });
        Assert.Equal(source, result.Source);
    }
    [Fact]
    public void Later_class_assignments_win_overlapping_properties_independently_of_definition_order()
    {
        const string source = "flowchart TB\nA[One] --> B[Two]\nclassDef first fill:#123456,stroke:#112233;\nclassDef second fill:#654321,stroke-width:2px;\nclass A second; class A first;\nclass B first; class B second;\n";
        var result = new DiagramService().Render(source); Assert.True(result.Success, result.Error);
        var nodes = result.FlowchartLayout!.Nodes.ToDictionary(node => node.Id);
        Assert.Equal("#123456", nodes["A"].InlineStyle!["fill"]);
        Assert.Equal("#654321", nodes["B"].InlineStyle!["fill"]);
        Assert.All(nodes.Values, node => { Assert.Equal("#112233", node.InlineStyle!["stroke"]); Assert.Equal("2px", node.InlineStyle["stroke-width"]); });
        Assert.Equal(source, result.Source);
    }
    [Fact]
    public void Private_effective_classes_do_not_replace_author_declared_classes()
    {
        const string source = "flowchart LR\nA[One]\nclassDef _lens_combined_0 fill:#ABCDEF;\nclassDef border stroke:#112233;\nclass A _lens_combined_0; class A border;";
        var result = new DiagramService().Render(source); Assert.True(result.Success, result.Error);
        Assert.Equal("#ABCDEF", result.FlowchartSemantics!.ClassDefs["_lens_combined_0"]["fill"]);
        Assert.Equal("_lens_combined_1", result.FlowchartSemantics.ClassAssignments["A"]);
        Assert.Equal(source, result.Source);
    }
    [Fact]
    public void Quoted_style_semicolons_remain_intact_and_quoted_multiline_values_are_not_silently_changed()
    {
        const string source = "flowchart TB\nA[Known]\nclassDef c font-family:'One;Two',fill:#FFFFFF;\nclass A c;";
        Assert.Contains("font-family:'One;Two'", FlowchartCompatibility.Prepare(source));
        var unsupported = source.Replace("One;Two", "One\nTwo", StringComparison.Ordinal);
        var result = new DiagramService().Render(unsupported);
        Assert.False(result.Success); Assert.Equal(DiagramFailureKind.UnsupportedSyntax, result.FailureKind); Assert.Equal(unsupported, result.Source);
    }
    [Fact]
    public void Other_diagram_families_are_not_adapted()
    {
        const string source = "sequenceDiagram\r\nparticipant A\r\nA->>A: class A red;\r\n";
        Assert.Equal(source, FlowchartCompatibility.Prepare(source));
    }
    [Fact]
    public void Semicolons_escaped_characters_comments_and_multiline_labels_are_not_globally_rewritten()
    {
        const string source = "flowchart TB\n%% class Fake wrong; untouched\nA[\"First; #35; line\nclass label; text\"] --> B[\"Backslash \\ and & text;\"]\nclassDef c fill:#FFFFFF;\nclass A,\n B c; %% trailing comment; untouched\n";
        var prepared = FlowchartCompatibility.Prepare(source);
        Assert.Contains("%% class Fake wrong; untouched", prepared);
        Assert.Contains("First; #35; line\nclass label; text", prepared);
        Assert.Contains("Backslash \\ and & text;", prepared);
        Assert.Contains("%% trailing comment; untouched", prepared);
        var result = new DiagramService().Render(source); Assert.True(result.Success, result.Error);
        Assert.Equal(2, result.FlowchartSemantics!.Nodes.Count); Assert.Equal(2, result.FlowchartSemantics.ClassAssignments.Count);
    }
    [Fact]
    public async Task Supported_visual_edits_preserve_semicolon_styling_and_revalidate_the_complete_graph()
    {
        const string source = "flowchart LR\nA[One] --> B[Two]\nclassDef c fill:#123456,color:#FFFFFF;\nclass A,B c;\n";
        var draft = new MermaidVisualDraft(source);
        Assert.True(draft.Model.CanEdit, draft.Model.Diagnostic);
        var changed = await draft.ExecuteAsync(new UpdateMermaidNode("A", "Updated", FlowNodeShape.Process, "A"));
        Assert.True(changed.Success, changed.Error); Assert.Contains("class A,B c;", draft.Model.Source);
        var rendered = new DiagramService().Render(draft.Source);
        Assert.Equal("Updated", rendered.FlowchartSemantics!.Nodes["A"].Label);
        Assert.Equal(2, rendered.FlowchartSemantics.Nodes.Count);
    }
}
