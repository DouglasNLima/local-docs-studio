using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using LensDocsStudio.Core.C4Diagrams;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public sealed class C4VisualEditorWindow : TopologyVisualEditorWindow
{
    private readonly C4VisualDraft c4;
    private readonly ComboBox elementKind, boundaryKind, destination, sourceEndpoint, targetEndpoint, relationKind;
    private C4SourceNode? Node => c4?.Model.Nodes.FirstOrDefault(n => n.Identity == c4.Selection);
    private C4SourceRelation? Relation => c4?.Model.Relations.FirstOrDefault(r => r.Identity == c4.Selection);
    protected override string Values => base.Values + (Relation is not null ? "\u001f" + sourceEndpoint?.SelectedItem + "\u001f" + targetEndpoint?.SelectedItem : "");
    public C4VisualEditorWindow(string source, DiagramService diagrams, Func<string, CancellationToken, Task<string?>> apply,
        bool dark = false, string diagramTheme = "Auto", Action? editSource = null, FontFamily? sourceFont = null, double sourceFontSize = 14)
        : base("C4", diagrams, apply, dark, diagramTheme, editSource, sourceFont, sourceFontSize)
    {
        c4 = new(source, diagrams); c4.ValidatePresentation = (text, _, token) => ValidateNative(text, token);
        elementKind = Selector("New element kind (keeps this diagram level)", "ElementKind", C4VisualDraft.ElementKinds); elementKind.SelectedItem = c4.Model.Variant.Equals("C4Component", StringComparison.OrdinalIgnoreCase) ? "Component" : c4.Model.Variant.Equals("C4Container", StringComparison.OrdinalIgnoreCase) ? "Container" : "System";
        boundaryKind = Selector("New boundary kind", "BoundaryKind", C4VisualDraft.BoundaryKinds); boundaryKind.SelectedIndex = 0;
        destination = Selector("Membership destination (blank: root)", "Destination");
        sourceEndpoint = Selector("Relationship source", "SourceEndpoint"); targetEndpoint = Selector("Relationship target", "TargetEndpoint"); relationKind = Selector("New relationship kind", "RelationKind", ["Rel", "BiRel", "Rel_Back"]); relationKind.SelectedIndex = 0;
        ActionButton("Add element", "AddElement", () => Add(Selected(elementKind, "System"))); ActionButton("Add empty boundary", "AddBoundary", () => Add(Selected(boundaryKind, "Boundary")));
        ActionButton("Move to destination", "MoveMembership", () =>
        {
            var boundary = c4.Model.Nodes.FirstOrDefault(n => n.Boundary && n.Alias == Selected(destination));
            return c4.MoveMembershipAsync(c4.Selection ?? "", boundary?.Identity ?? "", Lifetime.Token);
        }, InspectorFields);
        ActionButton("Add relationship", "AddRelation", () => c4.AddRelationAsync(Selected(relationKind, "Rel"), Selected(sourceEndpoint), Selected(targetEndpoint), token: Lifetime.Token), InspectorFields);
        Start(c4);
    }
    private async Task<SourceAuthoringResult> Add(string kind)
    {
        var alias = PromptIdentifier("C4 alias", value => c4.Model.Nodes.Any(n => n.Alias == value)); if (alias is null) return Protected("Addition cancelled; source is unchanged.");
        var result = await c4.AddNodeAsync(kind, alias, alias, token: Lifetime.Token); if (result.Success) c4.Select(c4.Model.Nodes[^1].Identity); return result;
    }
    protected override IEnumerable<Choice> Choices()
    {
        yield return new(c4.Model.HeaderIdentity, c4.Model.Variant + " · effective title");
        foreach (var node in c4.Model.Nodes) yield return new(node.Identity, node.Kind + " · " + node.Alias + " · " + node.Label + (!node.Complete ? " · protected incomplete boundary" : !c4.Model.Unique(node.Alias) ? " · protected hidden/duplicate alias" : ""));
        foreach (var (relation, index) in c4.Model.Relations.Select((r, i) => (r, i))) yield return new(relation.Identity, "Relationship " + (index + 1) + " · " + relation.Call.Keyword.Value + " · " + relation.Source + " → " + relation.Target + (relation.Simple ? "" : " · protected variant/arguments"));
        foreach (var key in c4.Model.Protected) yield return new(key, "Protected source · " + c4.Text.Line(key).Content);
    }
    protected override void ShowProperties()
    {
        var node = Node; var relation = Relation; var heading = c4.Selection == c4.Model.HeaderIdentity; var editable = node is not null && c4.Model.EffectiveNodes.Any(n => n.Identity == node.Identity) && node.Call.Balanced;
        Captions(heading ? "Effective title" : "Label", "Alias (renames proved references)", node?.Boundary == true ? "Boundary type label" : "Technology", "Description");
        Properties(node?.Label ?? relation?.Label ?? (heading ? c4.Model.Title?.Value ?? "" : ""), node?.Alias ?? "", node?.Technology ?? relation?.Technology ?? "", node?.Description ?? "", editLabel: editable || relation is { Simple: true } || heading,
            editIdentifier: editable && c4.Model.TopologySafe, editValue: editable && node!.TechnologyIndex >= 0 || relation is { Simple: true }, editMaximum: editable && node!.DescriptionIndex >= 0);
        destination.ItemsSource = new[] { "" }.Concat(c4.Model.Nodes.Where(n => n.Boundary && n.Complete && n.Depth == 0).Select(n => n.Alias)).ToArray(); destination.SelectedItem = c4.Model.Nodes.FirstOrDefault(n => n.Identity == node?.ParentIdentity)?.Alias ?? ""; destination.IsEnabled = node is not null && c4.Model.TopologySafe;
        var endpoints = c4.Model.Nodes.Where(n => !n.Boundary && c4.Model.Unique(n.Alias)).Select(n => n.Alias).ToArray(); sourceEndpoint.ItemsSource = targetEndpoint.ItemsSource = endpoints;
        sourceEndpoint.SelectedItem = relation?.Source ?? endpoints.FirstOrDefault(); targetEndpoint.SelectedItem = relation?.Target ?? endpoints.Skip(1).FirstOrDefault(); sourceEndpoint.IsEnabled = targetEndpoint.IsEnabled = c4.Model.TopologySafe;
        Describe(c4.Model.Variant + (node is null ? relation is null ? " · effective presentation" : " · " + relation.Call.Keyword.Value + " occurrence" : " · " + node.Kind + " · alias " + node.Alias),
            "Supported levels: C4Context, C4Container and C4Component. The diagram kind and existing element kinds stay exact. Person/System description slots differ from Container/Component technology and description. Blank edited fields use explicit empty quoted arguments; named, extra and ignored arguments remain in source. Alias rename patches every proved reference. Rel_Back retains its keyword and original reversed argument order. Complete simple boundaries retain braces, comments and children through moves. Unknown, incomplete, nested, hidden/duplicate, parallel, self or cyclic topology protects structural commands. Dynamic, Deployment, style/layout and direction/index variants remain protected source.");
    }
    protected override Task<SourceAuthoringResult> UpdateProperties() => Node is { } node ? c4.UpdateNodeAsync(node.Identity, Identifier.Text, Label.Text, Value.Text, Maximum.Text, Lifetime.Token)
        : Relation is { } relation ? c4.UpdateRelationAsync(relation.Identity, Selected(sourceEndpoint, relation.Source), Selected(targetEndpoint, relation.Target), Label.Text, Value.Text, Lifetime.Token)
        : c4.Selection == c4.Model.HeaderIdentity ? c4.SetTitleAsync(Label.Text, Lifetime.Token) : Task.FromResult(Protected("Select an editable C4 occurrence."));
    protected override Task<SourceAuthoringResult> DeleteSelection() => c4.DeleteAsync(c4.Selection ?? "", Lifetime.Token);
    protected override Task<SourceAuthoringResult> MoveSelection(int direction) => c4.MoveAsync(c4.Selection ?? "", direction, Lifetime.Token);
    protected override IReadOnlyList<NativeSourceHit> Correspondence(string source, DiagramResult render)
    {
        if (source != c4.Source) return []; var root = XDocument.Parse(render.Svg!).Root!;
        return C4VisualDraft.Correspondence(c4.Model, render.Svg!).Select(p => Hit(p.Node.Identity, p.Node.Kind + " · " + p.Node.Alias, p.Shape, root))
            .Concat(C4VisualDraft.RelationCorrespondence(c4.Model, render.Svg!).Select(p => PathHit(p.Relation.Identity, "Relationship · " + p.Relation.Source + " → " + p.Relation.Target, p.Shape, root))).OfType<NativeSourceHit>().OrderBy(h => h.Bounds.Width * h.Bounds.Height).ToArray();
    }
}
