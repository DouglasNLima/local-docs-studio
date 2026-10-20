using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using LensDocsStudio.Core.Mindmaps;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public sealed class MindmapVisualEditorWindow : SourceVisualEditorWindow
{
    private readonly MindmapVisualDraft mindmap;
    private readonly TextBox label;
    private readonly ComboBox parent;
    private readonly Button reparent;
    protected override string Values => label is null ? "" : label.Text;
    public MindmapVisualEditorWindow(string source, DiagramService diagrams, Func<string, CancellationToken, Task<string?>> apply,
        bool dark = false, string diagramTheme = "Auto", Action? editSource = null, FontFamily? sourceFont = null, double sourceFontSize = 14)
        : base("Mind map", diagrams, apply, dark, diagramTheme, editSource, sourceFont, sourceFontSize)
    {
        label = Field("Node text", "Label"); mindmap = new(source, diagrams); mindmap.ValidatePresentation = (text, _, token) => ValidateNative(text, token);
        ActionButton("Add child", "AddChild", () => Add(true)); ActionButton("Add sibling", "AddSibling", () => Add(false));
        ActionButton("Indent under previous sibling", "Indent", () => mindmap.IndentAsync(mindmap.Selection ?? "", false, Lifetime.Token));
        ActionButton("Outdent branch", "Outdent", () => mindmap.IndentAsync(mindmap.Selection ?? "", true, Lifetime.Token));
        parent = new ComboBox { Style = (Style)FindResource("Form.ComboBox"), DisplayMemberPath = "Description", SelectedValuePath = "Identity" }; Identify(parent, "Parent", "New parent node");
        InspectorFields.Children.Add(new Label { Content = "Move subtree to parent", Target = parent, Style = (Style)FindResource("Form.Label") }); InspectorFields.Children.Add(parent);
        reparent = ActionButton("Reparent subtree", "Reparent", () => mindmap.ReparentAsync(mindmap.Selection ?? "", parent.SelectedValue as string ?? "", Lifetime.Token), InspectorFields);
        Start(mindmap);
    }
    private async Task<SourceAuthoringResult> Add(bool child)
    {
        var keys = mindmap.Model.Nodes.Select(node => node.Identity).ToHashSet();
        var result = await mindmap.AddAsync(mindmap.Selection ?? mindmap.Model.Nodes.FirstOrDefault()?.Identity ?? "", child, token: Lifetime.Token);
        if (result.Success) mindmap.Select(mindmap.Model.Nodes.Single(node => !keys.Contains(node.Identity)).Identity); return result;
    }
    protected override IEnumerable<Choice> Choices() => mindmap.Model.Nodes.Select((node, index) => new Choice(node.Identity, new string(' ', node.Depth * 2) + (node.Depth == 0 ? "Root" : "Node " + (index + 1)) + " · " + node.Label + " · depth " + node.Depth + (node.Protection is null ? "" : " · protected")));
    protected override void ShowProperties()
    {
        var node = mindmap.Model.Nodes.FirstOrDefault(item => item.Identity == mindmap.Selection);
        label.Text = node?.Label ?? ""; label.IsEnabled = node is { Protection: null };
        var subtree = node is null ? [] : MindmapVisualDraft.Subtree(mindmap.Model, node).Select(item => item.Identity).ToHashSet();
        parent.ItemsSource = Choices().Where(item => !subtree.Contains(item.Identity)).ToArray(); parent.SelectedValue = node?.Parent;
        parent.IsEnabled = reparent.IsEnabled = node?.Parent is not null;
        Describe(node is null ? "Select a node" : (node.Parent is null ? "Root · " : "Node · ") + node.Label,
            mindmap.Model.Errors.FirstOrDefault() ?? node?.Protection ?? "Source occurrences identify nodes, including duplicate labels. Shape delimiters and IDs stay unchanged. Move, indent and reparent carry complete subtrees; the root is retained. Immediately preceding same-indent comments attach to their following node; ambiguous metadata/indentation protects structural operations.");
    }
    protected override Task<SourceAuthoringResult> UpdateProperties() => mindmap.UpdateAsync(mindmap.Selection ?? "", label.Text, Lifetime.Token);
    protected override Task<SourceAuthoringResult> DeleteSelection() => mindmap.DeleteAsync(mindmap.Selection ?? "", Lifetime.Token);
    protected override Task<SourceAuthoringResult> MoveSelection(int direction) => mindmap.MoveAsync(mindmap.Selection ?? "", direction, Lifetime.Token);
    protected override IReadOnlyList<NativeSourceHit> Correspondence(string source, DiagramResult render)
    {
        var root = XDocument.Parse(render.Svg!).Root!; var groups = root.Descendants().Where(element => ((string?)element.Attribute("class") ?? "").Split(' ').Contains("mindmap-node")).ToArray();
        var nodes = MindmapVisualDraft.CanonicalOrder(mindmap.Model); var result = new List<NativeSourceHit>();
        if (source != mindmap.Source || groups.Length != nodes.Count) return result;
        for (var index = 0; index < nodes.Count; index++)
        {
            if (!MindmapVisualDraft.DisplayLabelMatches(nodes[index].Label, (string?)groups[index].Attribute("data-label") ?? "") || (string?)groups[index].Attribute("data-depth") != nodes[index].Depth.ToString(System.Globalization.CultureInfo.InvariantCulture)) throw new ArgumentException("Canonical Mind map occurrence/depth does not match the source tree.");
            if (NativeSourceHit.ShapeBounds(groups[index], root) is { } bounds) result.Add(new(nodes[index].Identity, "Node · " + nodes[index].Label, bounds));
        }
        return result;
    }
}
