using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using LensDocsStudio.Core.BlockDiagrams;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public sealed class BlockVisualEditorWindow : TopologyVisualEditorWindow
{
    private readonly BlockVisualDraft block;
    private readonly ComboBox shape, sourceEndpoint, targetEndpoint;
    private BlockSourceToken? Token => block?.Model.Tokens.FirstOrDefault(t => t.Identity == block.Selection);
    private BlockSourceEdge? Edge => block?.Model.Edges.FirstOrDefault(e => e.Identity == block.Selection);
    protected override string Values => base.Values + (Token is not null ? "\u001f" + shape?.SelectedItem : Edge is not null ? "\u001f" + sourceEndpoint?.SelectedItem + "\u001f" + targetEndpoint?.SelectedItem : "");
    public BlockVisualEditorWindow(string source, DiagramService diagrams, Func<string, CancellationToken, Task<string?>> apply,
        bool dark = false, string diagramTheme = "Auto", Action? editSource = null, FontFamily? sourceFont = null, double sourceFontSize = 14)
        : base("Block", diagrams, apply, dark, diagramTheme, editSource, sourceFont, sourceFontSize)
    {
        block = new(source, diagrams); block.ValidatePresentation = (text, _, token) => ValidateNative(text, token);
        shape = Selector("Shape (explicit conversion)", "Shape", ["bare", "rectangle", "rounded"]); shape.SelectedItem = "rectangle";
        sourceEndpoint = Selector("Link source (real block ID)", "SourceEndpoint"); targetEndpoint = Selector("Link target (real block ID)", "TargetEndpoint");
        ActionButton("Add block", "AddBlock", Add); ActionButton("Add spacer", "AddSpace", async () => { var result = await block.AddSpaceAsync(token: Lifetime.Token); if (result.Success) block.Select(block.Model.Tokens[^1].Identity); return result; });
        ActionButton("Add directed link", "AddLink", () => block.AddEdgeAsync(Selected(sourceEndpoint), Selected(targetEndpoint), token: Lifetime.Token), InspectorFields);
        Start(block);
    }
    private async Task<SourceAuthoringResult> Add()
    {
        var id = PromptIdentifier("block", value => value.Equals("space", StringComparison.OrdinalIgnoreCase) || block.Model.Tokens.Any(t => !t.Space && t.Id == value)); if (id is null) return Protected("Addition cancelled; source is unchanged.");
        var result = await block.AddBlockAsync(id, id, Selected(shape) == "rounded" ? "rounded" : "rectangle", token: Lifetime.Token); if (result.Success) block.Select(block.Model.Tokens[^1].Identity); return result;
    }
    protected override IEnumerable<Choice> Choices()
    {
        yield return new(block.Model.HeaderIdentity, "Block · effective title and columns");
        foreach (var (item, index) in block.Model.Tokens.Select((t, i) => (t, i))) yield return new(item.Identity, "Grid " + (index + 1) + " · " + (item.Space ? "Spacer" : item.Id + " · " + item.Label) + " · span " + item.SpanText + (item.Partial ? " · protected partial line" : !item.Space && !block.Model.Unique(item.Id) ? " · protected hidden/duplicate ID" : ""));
        foreach (var (edge, index) in block.Model.Edges.Select((e, i) => (e, i))) yield return new(edge.Identity, "Link " + (index + 1) + " · " + edge.Source + " → " + edge.Target + " · " + edge.Label);
        foreach (var key in block.Model.Protected) yield return new(key, "Protected source · " + block.Text.Line(key).Content);
    }
    protected override void ShowProperties()
    {
        var item = Token; var edge = Edge; var heading = block.Selection == block.Model.HeaderIdentity; var editable = item is not null && !item.Partial && (item.Space || block.Model.Visible.Any(t => t.Identity == item.Identity));
        Captions(heading ? "Effective title" : item is { Shape: "bare", Space: false } ? "Label (coupled to ID)" : "Label", heading ? "Columns (positive integer)" : "ID (renames proved references)", "Span (positive integer)");
        Properties(item?.Space == true ? "" : item?.Label ?? edge?.Label ?? (heading ? block.Model.Title?.Value ?? "" : ""), item?.Space == true ? "" : item?.Id ?? (heading ? block.Model.ColumnsToken?.Raw ?? "1" : ""), item?.SpanText ?? "", editLabel: editable && !item!.Space && item.LabelToken is not null || edge is not null && block.Model.TopologySafe || heading,
            editIdentifier: editable && !item!.Space && block.Model.TopologySafe || heading, editValue: editable);
        shape.SelectedItem = item?.Shape ?? "rectangle"; shape.IsEnabled = editable && !item!.Space;
        var endpoints = block.Model.Visible.Where(t => block.Model.Unique(t.Id)).Select(t => t.Id).ToArray(); sourceEndpoint.ItemsSource = targetEndpoint.ItemsSource = endpoints;
        sourceEndpoint.SelectedItem = edge?.Source ?? endpoints.FirstOrDefault(); targetEndpoint.SelectedItem = edge?.Target ?? endpoints.Skip(1).FirstOrDefault(); sourceEndpoint.IsEnabled = targetEndpoint.IsEnabled = block.Model.TopologySafe;
        Describe(item is null ? edge is null ? "Flat Block · native grid" : "Directed link occurrence · " + edge.Source + " → " + edge.Target : item.Space ? "Spacer occurrence · span " + item.SpanText : "Block · " + item.Id + " · " + item.Shape,
            "Flat block/block-beta only. Several tokens on a line keep their exact spans, spacing and occurrence identities. Bare blocks couple label and ID; choose a shape explicitly to separate them. Spacers are repeatable grid occurrences, never endpoints. New spans must fit the current columns; changing columns preserves historical spans and numeric spelling. Native grid geometry is used. First-wins/hidden declarations, ignored suffixes, malformed lines and unproved links protect topology operations. Remove incident links before deleting a block. Nested groups, composite shapes, styling and free positioning remain source-only.");
    }
    protected override Task<SourceAuthoringResult> UpdateProperties() => Token is { } item ? block.UpdateTokenAsync(item.Identity, item.Space ? item.Id : Identifier.Text, item.Space ? item.Label : Label.Text, Selected(shape, item.Shape), Value.Text, Lifetime.Token)
        : Edge is { } edge ? block.UpdateEdgeAsync(edge.Identity, Selected(sourceEndpoint, edge.Source), Selected(targetEndpoint, edge.Target), Label.Text, Lifetime.Token)
        : block.Selection == block.Model.HeaderIdentity ? block.SetHeadingAsync(Label.Text, Identifier.Text, Lifetime.Token) : Task.FromResult(Protected("Select an editable Block occurrence."));
    protected override Task<SourceAuthoringResult> DeleteSelection() => block.DeleteAsync(block.Selection ?? "", Lifetime.Token);
    protected override Task<SourceAuthoringResult> MoveSelection(int direction) => block.MoveAsync(block.Selection ?? "", direction, Lifetime.Token);
    protected override string SourceIdentity(string identity) => block.Model.Tokens.FirstOrDefault(t => t.Identity == identity)?.LineIdentity ?? identity;
    protected override IReadOnlyList<NativeSourceHit> Correspondence(string source, DiagramResult render)
    {
        if (source != block.Source) return []; var root = XDocument.Parse(render.Svg!).Root!;
        return BlockVisualDraft.Correspondence(block.Model, render.Svg!).Select(p => Hit(p.Token.Identity, "Block · " + p.Token.Id, p.Shape, root))
            .Concat(BlockVisualDraft.EdgeCorrespondence(block.Model, render.Svg!).Select(p => PathHit(p.Edge.Identity, "Link · " + p.Edge.Source + " → " + p.Edge.Target, p.Shape, root))).OfType<NativeSourceHit>().ToArray();
    }
}
