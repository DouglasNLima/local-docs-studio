using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using LensDocsStudio.Core.ArchitectureDiagrams;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public sealed class ArchitectureVisualEditorWindow : TopologyVisualEditorWindow
{
    private readonly ArchitectureVisualDraft architecture;
    private readonly ComboBox icon, parent, sourceEndpoint, targetEndpoint, sourceSide, targetSide, arrow;
    private ArchitectureSourceNode? Node => architecture?.Model.Nodes.FirstOrDefault(n => n.Identity == architecture.Selection);
    private ArchitectureSourceEdge? Edge => architecture?.Model.Edges.FirstOrDefault(e => e.Identity == architecture.Selection);
    protected override string Values => base.Values + (Node is not null ? "\u001f" + icon?.SelectedItem + "\u001f" + parent?.SelectedItem : Edge is not null ? "\u001f" + sourceEndpoint?.SelectedItem + "\u001f" + targetEndpoint?.SelectedItem + "\u001f" + sourceSide?.SelectedItem + "\u001f" + targetSide?.SelectedItem + "\u001f" + arrow?.SelectedItem : "");
    public ArchitectureVisualEditorWindow(string source, DiagramService diagrams, Func<string, CancellationToken, Task<string?>> apply,
        bool dark = false, string diagramTheme = "Auto", Action? editSource = null, FontFamily? sourceFont = null, double sourceFontSize = 14)
        : base("Architecture", diagrams, apply, dark, diagramTheme, editSource, sourceFont, sourceFontSize)
    {
        architecture = new(source, diagrams); architecture.ValidatePresentation = (text, _, token) => ValidateNative(text, token);
        icon = Selector("Bundled icon (blank: omitted)", "Icon", new[] { "" }.Concat(ArchitectureVisualDraft.Icons)); parent = Selector("Parent group (blank: ungrouped)", "Parent");
        sourceEndpoint = Selector("Connection source", "SourceEndpoint"); targetEndpoint = Selector("Connection target", "TargetEndpoint");
        sourceSide = Selector("Source side (blank: omitted)", "SourceSide", ["", "L", "R", "T", "B"]); targetSide = Selector("Target side (blank: omitted)", "TargetSide", ["", "L", "R", "T", "B"]);
        arrow = Selector("Arrowheads (none / target / source / both)", "Arrow", ["--", "-->", "<--", "<-->"]);
        ActionButton("Add service", "AddService", () => Add("service")); ActionButton("Add junction", "AddJunction", () => Add("junction")); ActionButton("Add empty group", "AddGroup", () => Add("group"));
        ActionButton("Add connection", "AddConnection", () => architecture.AddEdgeAsync(Selected(sourceEndpoint), Selected(targetEndpoint), Selected(sourceSide), Selected(targetSide), Selected(arrow, "-->"), Lifetime.Token), InspectorFields);
        Start(architecture);
    }
    private async Task<SourceAuthoringResult> Add(string kind)
    {
        var id = PromptIdentifier(kind, value => architecture.Model.Nodes.Any(n => n.Id == value)); if (id is null) return Protected("Addition cancelled; source is unchanged.");
        var result = await architecture.AddNodeAsync(kind, id, id, token: Lifetime.Token); if (result.Success) architecture.Select(architecture.Model.Nodes[^1].Identity); return result;
    }
    protected override IEnumerable<Choice> Choices()
    {
        yield return new(architecture.Model.HeaderIdentity, "Architecture · service topology");
        foreach (var node in architecture.Model.Nodes) yield return new(node.Identity, node.Kind + " · " + node.Id + (node.Label.Length > 0 ? " · " + node.Label : "") + (architecture.Model.Unique(node.Id) ? "" : " · protected duplicate"));
        foreach (var (edge, index) in architecture.Model.Edges.Select((e, i) => (e, i))) yield return new(edge.Identity, "Connection " + (index + 1) + " · " + edge.Source + " " + edge.ArrowToken.Value + " " + edge.Target + (edge.Braced ? " · protected group endpoint" : ""));
        foreach (var key in architecture.Model.Protected) yield return new(key, "Protected source · " + architecture.Text.Line(key).Content);
    }
    protected override void ShowProperties()
    {
        var node = Node; var edge = Edge; var unique = node is not null && architecture.Model.Unique(node.Id); var safe = architecture.Model.TopologySafe;
        Captions("Label", "ID (renames proved references)"); Properties(node?.Label ?? "", node?.Id ?? "", editLabel: unique && node!.LabelToken is not null, editIdentifier: unique && safe);
        icon.SelectedItem = node?.Icon ?? ""; icon.IsEnabled = unique && node!.Kind != "junction";
        parent.ItemsSource = new[] { "" }.Concat(architecture.Model.Nodes.Where(n => n.Kind == "group" && n.Parent.Length == 0).Select(n => n.Id)).ToArray(); parent.SelectedItem = node?.Parent ?? ""; parent.IsEnabled = unique && safe && node!.Kind != "group";
        var endpoints = architecture.Model.Nodes.Where(n => n.Kind != "group" && architecture.Model.Unique(n.Id)).Select(n => n.Id).ToArray();
        sourceEndpoint.ItemsSource = targetEndpoint.ItemsSource = endpoints; sourceEndpoint.SelectedItem = edge?.Source ?? endpoints.FirstOrDefault(); targetEndpoint.SelectedItem = edge?.Target ?? endpoints.Skip(1).FirstOrDefault();
        sourceSide.SelectedItem = edge?.SourceSideToken.Value.Trim(':') ?? "R"; targetSide.SelectedItem = edge?.TargetSideToken.Value.Trim(':') ?? "L"; arrow.SelectedItem = edge?.ArrowToken.Value ?? "-->";
        sourceEndpoint.IsEnabled = targetEndpoint.IsEnabled = sourceSide.IsEnabled = targetSide.IsEnabled = arrow.IsEnabled = safe;
        Describe(node is not null ? node.Kind + " · " + node.Id + (node.Parent.Length > 0 ? " · in " + node.Parent : " · ungrouped") : edge is not null ? "Connection occurrence · " + edge.Source + " → " + edge.Target : "Architecture · native service topology",
            "Membership uses explicit in clauses. Labels and IDs are separate; omitted icons remain omitted. Verified bundled icons: cloud, database, disk, internet, server and generic. Junctions and empty groups remain in the keyboard occurrence list. Real native ports, arrowheads and geometry are used. Group endpoints, deeper hierarchy, duplicate IDs and unresolved source protect topology operations. Remove incident connections and children before deleting a node. Provider/custom icons retain original source and the existing native safety policy.");
    }
    protected override Task<SourceAuthoringResult> UpdateProperties() => Node is { } node ? architecture.UpdateNodeAsync(node.Identity, Identifier.Text, Label.Text, Selected(icon, node.Icon), Selected(parent, node.Parent), Lifetime.Token)
        : Edge is { } edge ? architecture.UpdateEdgeAsync(edge.Identity, Selected(sourceEndpoint), Selected(targetEndpoint), Selected(sourceSide), Selected(targetSide), Selected(arrow), Lifetime.Token) : Task.FromResult(Protected("Select an editable source occurrence."));
    protected override Task<SourceAuthoringResult> DeleteSelection() => architecture.DeleteAsync(architecture.Selection ?? "", Lifetime.Token);
    protected override Task<SourceAuthoringResult> MoveSelection(int direction) => architecture.MoveAsync(architecture.Selection ?? "", direction, Lifetime.Token);
    protected override IReadOnlyList<NativeSourceHit> Correspondence(string source, DiagramResult render)
    {
        if (source != architecture.Source) return []; var root = XDocument.Parse(render.Svg!).Root!;
        return ArchitectureVisualDraft.Correspondence(architecture.Model, render.Svg!).Select(p => Hit(p.Node.Identity, p.Node.Kind + " · " + p.Node.Id, p.Shape, root))
            .Concat(ArchitectureVisualDraft.EdgeCorrespondence(architecture.Model, render.Svg!).Select(p => PathHit(p.Edge.Identity, "Connection · " + p.Edge.Source + " → " + p.Edge.Target, p.Shape, root))).OfType<NativeSourceHit>().OrderBy(h => h.Bounds.Width * h.Bounds.Height).ToArray();
    }
}
