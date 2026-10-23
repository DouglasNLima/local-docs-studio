using System.Globalization;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Core.TreemapDiagrams;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public sealed class TreemapVisualEditorWindow : TopologyVisualEditorWindow
{
    private readonly TreemapVisualDraft map;
    private readonly ComboBox parent;
    private TreemapSourceNode? Node => map?.Model.Nodes.FirstOrDefault(n => n.Identity == map.Selection);
    public TreemapVisualEditorWindow(string source, DiagramService diagrams, Func<string, CancellationToken, Task<string?>> apply,
        bool dark = false, string diagramTheme = "Auto", Action? editSource = null, FontFamily? sourceFont = null, double sourceFontSize = 14)
        : base("Treemap", diagrams, apply, dark, diagramTheme, editSource, sourceFont, sourceFontSize)
    {
        map = new(source, diagrams); map.ValidatePresentation = (text, _, token) => ValidateNative(text, token);
        parent = Selector("Destination section (complete subtree)", "Parent");
        ActionButton("Move to section", "Reparent", () => map.ReparentAsync(map.Selection ?? "", (parent.SelectedItem as Choice)?.Identity is { Length: > 0 } key ? key : null, Lifetime.Token), InspectorFields);
        ActionButton("Add root section…", "AddSection", () => Add(false)); ActionButton("Add leaf…", "AddLeaf", () => Add(true)); Start(map);
    }
    private async Task<SourceAuthoringResult> Add(bool leaf)
    {
        var prompt = new TextPromptWindow("Add Treemap " + (leaf ? "leaf" : "section"), "Enter an explicit quoted-node label. A new leaf starts at zero and remains keyboard-selectable until its value is edited.", "", "Add", "New node label", "No embedded double quotes or markup.", value => string.IsNullOrWhiteSpace(value) ? "Enter a label." : null, trimInput: false) { Owner = this };
        if (prompt.ShowDialog() != true) return Protected("Addition cancelled; source is unchanged.");
        var before = map.Model.Nodes.Select(n => n.Identity).ToHashSet(); var result = await map.AddAsync(leaf && Node?.ValueToken is null ? Node?.Identity : null, prompt.Value, leaf ? "0" : null, Lifetime.Token);
        if (result.Success) map.Select(map.Model.Nodes.Single(n => !before.Contains(n.Identity)).Identity); return result;
    }
    protected override IEnumerable<Choice> Choices()
    {
        yield return new(map.Model.HeaderIdentity, "Treemap · native squarified hierarchy");
        foreach (var (node, i) in map.Model.Nodes.Select((n, i) => (n, i))) yield return new(node.Identity, "Node " + (i + 1) + " · " + node.Label + " · " + (node.ValueToken is null ? "derived " : "explicit ") + node.ComputedValue.ToString(CultureInfo.InvariantCulture) + (node.ComputedValue <= 0 ? " · no native tile" : "") + (!node.Proved ? " · protected fallback" : ""));
    }
    protected override void ShowProperties()
    {
        var node = Node; Captions("Quoted node label", value: "Explicit leaf value (decimal point)", maximum: "Computed value (read-only)");
        Properties(node?.Label ?? "", value: node?.ValueToken?.Raw ?? "", maximum: node?.ComputedValue.ToString(CultureInfo.InvariantCulture) ?? "", editLabel: node?.Proved == true, editValue: node?.Proved == true && node.ValueToken is not null);
        parent.ItemsSource = new[] { new Choice("", "Root · no section") }.Concat(map.Model.Nodes.Where(n => n.ValueToken is null).Select(n => new Choice(n.Identity, "Section · " + n.Label))).ToArray();
        var current = map.Model.Hierarchy.FirstOrDefault(r => r.Identity == node?.Identity)?.Parent; parent.SelectedItem = parent.Items.OfType<Choice>().FirstOrDefault(c => c.Identity == (current ?? "")); parent.IsEnabled = map.Model.StructureSafe;
        Describe("Treemap · " + (node?.ValueToken is null ? "derived section" : "explicit leaf decimal"),
            "Only quoted sections and quoted non-negative decimal leaves are editable. Explicit values retain their source spelling; parent totals stay derived. Tabs and spaces each count as one indentation character. Mixed/tab structural re-encoding and raw parser fallback labels remain protected source-only. Complete subtree operations prove all retained parent memberships. Zero values and tiles omitted for lack of space remain in the keyboard occurrence list. Pointer targets use actual native rectangles, with smaller tiles first; duplicate labels are never guessed. Native squarification owns area and position; no freeform geometry or substitute packing is offered. New decimals allow at most 15 significant digits and 15 decimal places.");
    }
    protected override Task<SourceAuthoringResult> UpdateProperties() => Node is { } node ? map.UpdateAsync(node.Identity, Label.Text, Value.Text, Lifetime.Token) : Task.FromResult(Protected("Select an editable quoted node."));
    protected override Task<SourceAuthoringResult> DeleteSelection() => map.DeleteAsync(map.Selection ?? "", Lifetime.Token);
    protected override Task<SourceAuthoringResult> MoveSelection(int direction) => map.MoveAsync(map.Selection ?? "", direction, Lifetime.Token);
    protected override IReadOnlyList<NativeSourceHit> Correspondence(string source, DiagramResult render)
    {
        if (source != map.Source) return []; var root = XDocument.Parse(render.Svg!).Root!;
        return TreemapVisualDraft.Correspondence(map.Model, render.Svg!).Select(p => Hit(p.Node.Identity, "Treemap node · " + p.Node.Label, p.Shape, root)).OfType<NativeSourceHit>().OrderBy(h => h.Bounds.Width * h.Bounds.Height).ToArray();
    }
}
