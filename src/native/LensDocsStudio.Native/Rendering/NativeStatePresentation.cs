using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.StateDiagrams;

namespace LensDocsStudio.Native.Rendering;

/// <summary>State identities remain separate. Only the pinned graph renderer's verified geometry/conversion is shared.</summary>
public sealed record NativeStatePresentation(NativeMermaidPresentation Canonical, IReadOnlyDictionary<string, string> Elements)
{
    public static NativeStatePresentation Create(StateDiagramModel model, DiagramResult render, NativeSvgService svg) => Create(model, render, svg, default);
    public static NativeStatePresentation Create(StateDiagramModel model, DiagramResult render, NativeSvgService svg, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StateSemanticValidation.ValidateRendering(model, render);
        // Mermaider 0.15.1 routes State through LayoutFlowchart and the same SVG graph renderer.
        // The shared adapter verifies every node, group and edge metadata item against that very positioned graph.
        var canonical = NativeMermaidPresentation.Create(render, svg, cancellationToken); var identities = new Dictionary<string, string>();
        foreach (var node in model.Nodes) identities.Add("node:" + node.Id, node.Identity);
        foreach (var pseudo in model.PseudoNodes) identities.Add("node:" + pseudo.RendererId, pseudo.Identity);
        foreach (var group in model.Groups) identities.Add("group:" + group.Id, group.Identity);
        return new(canonical, identities);
    }
}
