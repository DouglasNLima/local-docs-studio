using System.Text.RegularExpressions;
using Mermaider.Layout;
using Mermaider.Models;

namespace LensDocsStudio.Core.Rendering;

/// <summary>Decode portable Mermaid numeric escapes after parsing, before the existing layout and SVG renderer.
/// This avoids turning escaped delimiters into syntax, and keeps preview/export text and measurements consistent.</summary>
internal sealed partial class MermaidEntityLayoutProvider(IGraphLayoutProvider inner) : IGraphLayoutProvider
{
    public PositionedGraph LayoutFlowchart(MermaidGraph graph, RenderOptions? options = null, StrictStylingOptions? strict = null)
    {
        var decoded = graph with
        {
            Nodes = graph.Nodes.ToDictionary(pair => pair.Key, pair => pair.Value with { Label = Decode(pair.Value.Label) }),
            Edges = graph.Edges.Select(edge => edge with { Label = edge.Label is null ? null : Decode(edge.Label) }).ToArray(),
            Subgraphs = graph.Subgraphs.Select(DecodeGroup).ToArray()
        };
        return inner.LayoutFlowchart(decoded, options, strict);
    }
    public PositionedClassDiagram LayoutClass(ClassDiagram diagram) => inner.LayoutClass(diagram);
    public PositionedErDiagram LayoutEr(ErDiagram diagram) => inner.LayoutEr(diagram);
    private static MermaidSubgraph DecodeGroup(MermaidSubgraph group) => group with { Label = Decode(group.Label), Children = group.Children.Select(DecodeGroup).ToArray() };
    private static string Decode(string value) => NumericEntity().Replace(value, match => int.TryParse(match.Groups[1].Value, out var scalar)
        && System.Text.Rune.IsValid(scalar) && (scalar >= 32 || scalar is 9 or 10 or 13) ? char.ConvertFromUtf32(scalar) : match.Value);
    [GeneratedRegex(@"#(\d{1,7});", RegexOptions.CultureInvariant, 50)] private static partial Regex NumericEntity();
}
