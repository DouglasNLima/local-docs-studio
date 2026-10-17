using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Core.Tests;

public sealed class MermaidRenderingCorrespondenceTests
{
    [Theory] [InlineData("complex")] [InlineData("advanced-flow-error-log-lab")]
    public void Complete_canonical_metadata_matches_source_properties(string fixture)
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "native-mermaid-visual", fixture + ".mmd"));
        Compare(source);
    }
    [Fact]
    public void Literal_escaped_syntax_retains_its_canonical_metadata()
        => Compare("flowchart TD\n" + MermaidTransformationService.NodeText("A", "Café 😀 e\u0301 \"[x](y)|{z}\\ <script> #34; &amp;\nNext", FlowNodeShape.Process) + "\n");
    private static void Compare(string source)
    {
        var model = new MermaidSourceParser().Parse(source); var result = new DiagramService().Render(source);
        Assert.True(result.Success, result.Error); var graph = result.FlowchartSemantics!;
        Assert.NotNull(result.FlowchartLayout);
        var mismatches = model.Nodes.Select(node => new { node.Id, ExpectedLabel = node.Label, ActualLabel = graph.Nodes[node.Id].Label,
            ExpectedShape = node.Shape.ToString(), ActualShape = graph.Nodes[node.Id].Shape.ToString() })
            .Where(item => item.ExpectedLabel != item.ActualLabel).ToArray();
        Assert.True(mismatches.Length == 0, System.Text.Json.JsonSerializer.Serialize(mismatches));
        Assert.Equal(model.Edges.Select(edge => (edge.Source.Id, edge.Target.Id, edge.Label)), graph.Edges.Select(edge => (edge.Source, edge.Target, edge.Label ?? "")));
        foreach (var group in model.Subgraphs)
        {
            var rendered = result.FlowchartGroups.Single(identity => identity.Id == group.Id);
            Assert.Equal(group.ParentId, rendered.ParentId);
            Assert.Equal(model.Nodes.Where(node => node.Occurrences.Any(item => item.SubgraphId == group.Id)).OrderBy(node => node.Occurrences.First(item => item.SubgraphId == group.Id).Span.Start).Select(node => node.Id), rendered.NodeIds);
        }
    }
}
