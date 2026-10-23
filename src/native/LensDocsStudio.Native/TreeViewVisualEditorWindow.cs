using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Core.TreeViewDiagrams;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public sealed class TreeViewVisualEditorWindow : TopologyVisualEditorWindow
{
    private readonly TreeViewVisualDraft tree;
    private readonly ComboBox directory, icon, parent;
    private TreeViewSourceNode? Node => tree?.Model.Nodes.FirstOrDefault(n => n.Identity == tree.Selection);
    protected override string Values => base.Values + "\u001f" + directory?.SelectedItem + "\u001f" + icon?.SelectedItem;
    public TreeViewVisualEditorWindow(string source, DiagramService diagrams, Func<string, CancellationToken, Task<string?>> apply,
        bool dark = false, string diagramTheme = "Auto", Action? editSource = null, FontFamily? sourceFont = null, double sourceFontSize = 14)
        : base("TreeView", diagrams, apply, dark, diagramTheme, editSource, sourceFont, sourceFontSize)
    {
        tree = new(source, diagrams); tree.ValidatePresentation = (text, _, token) => ValidateNative(text, token);
        directory = Selector("Directory state (trailing slash)", "Directory", ["file", "directory"]); icon = Selector("Native icon state", "Icon", ["default", "hidden", "protected"]);
        parent = Selector("Destination parent (complete subtree)", "Parent");
        ActionButton("Move to parent", "Reparent", () => tree.ReparentAsync(tree.Selection ?? "", (parent.SelectedItem as Choice)?.Identity is { Length: > 0 } key ? key : null, Lifetime.Token), InspectorFields);
        ActionButton("Add root…", "AddRoot", () => Add(null)); ActionButton("Add child…", "AddChild", () => Node is null ? Task.FromResult(Protected("Select a parent node.")) : Add(Node.Identity));
        ActionButton("Indent subtree", "Indent", () => Indent(false)); ActionButton("Deindent subtree", "Outdent", () => Indent(true)); Start(tree);
    }
    private async Task<SourceAuthoringResult> Add(string? parentIdentity)
    {
        var prompt = new TextPromptWindow("Add TreeView node", "Enter the new node label. Directory state and optional annotations can be changed in the inspector.", "", "Add node", "New node label", "Literal native file/folder label.", value => string.IsNullOrWhiteSpace(value) ? "Enter a label." : null, trimInput: false) { Owner = this };
        if (prompt.ShowDialog() != true) return Protected("Addition cancelled; source is unchanged.");
        var before = tree.Model.Nodes.Select(n => n.Identity).ToHashSet(); var result = await tree.AddAsync(parentIdentity, prompt.Value, false, Lifetime.Token);
        if (result.Success) tree.Select(tree.Model.Nodes.Single(n => !before.Contains(n.Identity)).Identity); return result;
    }
    private Task<SourceAuthoringResult> Indent(bool outdent)
    {
        var row = tree.Model.Hierarchy.FirstOrDefault(r => r.Identity == tree.Selection); if (row is null) return Task.FromResult(Protected("Select a subtree."));
        if (outdent) return row.Parent is null ? Task.FromResult(Protected("This node is already a root.")) : tree.ReparentAsync(row.Identity, tree.Model.Hierarchy.Single(r => r.Identity == row.Parent).Parent, Lifetime.Token);
        var siblings = tree.Model.Hierarchy.Where(r => r.Parent == row.Parent).ToArray(); var at = Array.IndexOf(siblings, row);
        return at == 0 ? Task.FromResult(Protected("There is no preceding sibling parent.")) : tree.ReparentAsync(row.Identity, siblings[at - 1].Identity, Lifetime.Token);
    }
    protected override IEnumerable<Choice> Choices()
    {
        yield return new(tree.Model.HeaderIdentity, "TreeView · " + (tree.Model.BoxMode ? "box-drawing mode" : "indentation mode"));
        foreach (var (node, i) in tree.Model.Nodes.Select((n, i) => (n, i))) yield return new(node.Identity, "Node " + (i + 1) + " · " + node.Label + (node.Directory ? "/" : "") + " · depth " + tree.Model.Hierarchy[i].Depth + (!node.Proved ? " · protected source" : ""));
    }
    protected override void ShowProperties()
    {
        var node = Node; Captions("Literal node label", "Class annotation (blank removes)", "Description (blank removes)");
        Properties(node?.Label ?? "", node?.ClassToken?.Value ?? "", node?.DescriptionToken?.Value ?? "", editLabel: node?.Proved == true, editIdentifier: node?.Proved == true, editValue: node?.Proved == true);
        directory.SelectedItem = node?.Directory == true ? "directory" : "file"; icon.SelectedItem = node?.Icon is null ? "default" : node.Icon == "" ? "hidden" : "protected";
        directory.IsEnabled = icon.IsEnabled = node?.Proved == true;
        parent.ItemsSource = new[] { new Choice("", "Root · no parent") }.Concat(tree.Model.Nodes.Select((n, i) => new Choice(n.Identity, "Node " + (i + 1) + " · " + n.Label))).ToArray();
        var current = tree.Model.Hierarchy.FirstOrDefault(r => r.Identity == node?.Identity)?.Parent; parent.SelectedItem = parent.Items.OfType<Choice>().First(c => c.Identity == (current ?? "")); parent.IsEnabled = tree.Model.StructureSafe;
        Describe("TreeView · " + (tree.Model.BoxMode ? "native box-drawing hierarchy" : "native indentation hierarchy"),
            "The first nonblank body line fixes the mode. Indentation tabs expand to four spaces. Basic box-mode field edits preserve every glyph; box structural re-encoding remains source-only. Complete indentation subtrees move only after every retained membership is proved. Ambiguous comments, blank attachments, mixed annotations and unproved tab re-encoding protect structure. Default folder/file glyphs and hidden icons are supported; explicit custom artwork remains source-only under existing SVG image sanitisation. Actual glyph/highlight shapes supply pointer selection; hidden icons and duplicate labels remain in the complete keyboard occurrence list.");
    }
    protected override Task<SourceAuthoringResult> UpdateProperties() => Node is { } node ? tree.UpdateAsync(node.Identity, Label.Text, Selected(directory) == "directory", Value.Text, Identifier.Text, Selected(icon), Lifetime.Token) : Task.FromResult(Protected("Select an editable node occurrence."));
    protected override Task<SourceAuthoringResult> DeleteSelection() => tree.DeleteAsync(tree.Selection ?? "", Lifetime.Token);
    protected override Task<SourceAuthoringResult> MoveSelection(int direction) => tree.MoveAsync(tree.Selection ?? "", direction, Lifetime.Token);
    protected override IReadOnlyList<NativeSourceHit> Correspondence(string source, DiagramResult render)
    {
        if (source != tree.Source) return []; var root = XDocument.Parse(render.Svg!).Root!;
        return TreeViewVisualDraft.Correspondence(tree.Model, render.Svg!).Select(p => Hit(p.Node.Identity, "TreeView node · " + p.Node.Label, p.Shape, root)).OfType<NativeSourceHit>().ToArray();
    }
}
