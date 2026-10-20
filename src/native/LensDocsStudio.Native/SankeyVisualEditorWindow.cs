using System.Windows.Media;
using System.Xml.Linq;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SankeyDiagrams;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public sealed class SankeyVisualEditorWindow : ChartVisualEditorWindow
{
    private readonly SankeyVisualDraft sankey;
    public SankeyVisualEditorWindow(string source, DiagramService diagrams, Func<string, CancellationToken, Task<string?>> apply,
        bool dark = false, string diagramTheme = "Auto", Action? editSource = null, FontFamily? sourceFont = null, double sourceFontSize = 14)
        : base("Sankey", diagrams, apply, dark, diagramTheme, editSource, sourceFont, sourceFontSize)
    {
        sankey = new(source, diagrams); sankey.ValidatePresentation = (text, _, token) => ValidateNative(text, token);
        ActionButton("Add link with weight 1", "AddLink", async () =>
        { var n = 1; while (sankey.Model.Nodes.Contains("New target " + n)) n++; var r = await sankey.AddAsync(sankey.Model.Nodes.FirstOrDefault() ?? "New source", "New target " + n, "1", Lifetime.Token); if (r.Success) sankey.Select(sankey.Model.Links[^1].Identity); return r; });
        Start(sankey);
    }
    private string? Node => sankey.Model.Nodes.FirstOrDefault(n => SankeyVisualDraft.NodeIdentity(sankey.Model, n) == sankey.Selection);
    protected override IEnumerable<Choice> Choices()
    {
        yield return new(sankey.Model.HeaderIdentity, "Sankey source · ordered positive weighted links");
        foreach (var (l, i) in sankey.Model.Links.Select((l, i) => (l, i))) yield return new(l.Identity, "Link " + (i + 1) + " · " + l.Source + " → " + l.Target + " · " + l.Weight);
        foreach (var node in sankey.Model.Nodes) yield return new(SankeyVisualDraft.NodeIdentity(sankey.Model, node), "Rename node across proved endpoints · " + node);
        foreach (var key in sankey.Model.Protected) yield return new(key, "Protected source · " + sankey.Text.Line(key).Content);
    }
    protected override string SourceIdentity(string id) => Node is { } node ? sankey.Model.Links.First(l => l.Source == node || l.Target == node).Identity : id;
    protected override void ShowProperties()
    {
        var l = sankey.Model.Links.FirstOrDefault(l => l.Identity == sankey.Selection); var node = Node;
        if (l is not null) Properties(l.Source, l.Target, l.Weight, editLabel: l.Simple && !sankey.Model.Cyclic, editIdentifier: l.Simple && !sankey.Model.Cyclic, editValue: l.Simple && !sankey.Model.Cyclic);
        else if (node is not null) Properties(node, editLabel: !sankey.Model.Cyclic);
        else Properties();
        Describe(node is null ? "Sankey · one row endpoint/weight edit" : "Explicit global node rename · " + node,
            sankey.Model.Errors.FirstOrDefault() ?? (sankey.Model.Cyclic ? "Protected cyclic topology: the pinned renderer can omit self-links and feedback ribbons. Source is preserved exactly; use Edit source."
            : node is not null ? "Update renames this node across every proved endpoint occurrence atomically. Existing node identity collisions and unproved rows protect global rename. This is distinct from editing one link."
            : "Each link is a separate source occurrence, including parallel equal links. Endpoint edits change only this row. Quotes, commas, doubled quotes, extra fields and untouched precision survive. Weights must be positive and finite. Ignored invalid rows remain protected in the source list. All ribbons and nodes use actual canonical output."));
    }
    protected override Task<SourceAuthoringResult> UpdateProperties() => Node is { } node ? sankey.RenameNodeAsync(node, Label.Text, Lifetime.Token) : sankey.UpdateAsync(sankey.Selection ?? "", Label.Text, Identifier.Text, Value.Text, Lifetime.Token);
    protected override Task<SourceAuthoringResult> DeleteSelection() => sankey.DeleteAsync(sankey.Selection ?? "", Lifetime.Token);
    protected override Task<SourceAuthoringResult> MoveSelection(int direction) => sankey.MoveAsync(sankey.Selection ?? "", direction, Lifetime.Token);
    protected override IReadOnlyList<NativeSourceHit> Correspondence(string source, DiagramResult render)
    {
        if (source != sankey.Source) return []; var pairs = SankeyVisualDraft.Correspondence(sankey.Model, render.Svg!); var root = XDocument.Parse(render.Svg!).Root!; var hits = new List<NativeSourceHit>();
        foreach (var node in SourceDeclarations.Groups(render.Svg!, "sankey-node"))
        { var name = (string)node.Attribute("data-node")!; if (Hit(SankeyVisualDraft.NodeIdentity(sankey.Model, name), "Node · " + name, node, root) is { } hit) hits.Add(hit); }
        foreach (var (link, shape) in pairs) if (Hit(link.Identity, "Link · " + link.Source + " → " + link.Target, shape, root) is { } hit) hits.Add(hit);
        return hits;
    }
}
