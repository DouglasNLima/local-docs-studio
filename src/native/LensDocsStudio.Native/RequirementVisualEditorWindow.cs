using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using LensDocsStudio.Core.RequirementDiagrams;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public sealed class RequirementVisualEditorWindow : ChartVisualEditorWindow
{
    private readonly RequirementVisualDraft requirement;
    private readonly TextBox kind, risk, verification, docRef;
    private readonly ComboBox sourceEndpoint, targetEndpoint, relationType;
    protected override string Values => base.Values + "\u001f" + kind?.Text + "\u001f" + risk?.Text + "\u001f" + verification?.Text + "\u001f" + docRef?.Text;
    public RequirementVisualEditorWindow(string source, DiagramService diagrams, Func<string, CancellationToken, Task<string?>> apply,
        bool dark = false, string diagramTheme = "Auto", Action? editSource = null, FontFamily? sourceFont = null, double sourceFontSize = 14)
        : base("Requirement", diagrams, apply, dark, diagramTheme, editSource, sourceFont, sourceFontSize)
    {
        requirement = new(source, diagrams); requirement.ValidatePresentation = (t, _, token) => ValidateNative(t, token);
        kind = Field("Requirement kind", "Kind"); risk = Field("Risk (low, medium, high or blank)", "Risk"); verification = Field("Verification (analysis, demonstration, inspection, test or blank)", "Verification"); docRef = Field("Element document reference", "DocRef");
        ActionButton("Add requirement", "AddRequirement", () => AddBlock("requirement")); ActionButton("Add element", "AddElement", () => AddBlock("element"));
        sourceEndpoint = Endpoint("New relation source", "SourceEndpoint"); targetEndpoint = Endpoint("New relation target", "TargetEndpoint"); relationType = Endpoint("New relation type", "RelationType"); relationType.ItemsSource = RequirementVisualDraft.RelationTypes; relationType.SelectedIndex = 0;
        ActionButton("Add relationship", "AddRelation", () => requirement.AddRelationAsync(sourceEndpoint.SelectedItem as string ?? "", targetEndpoint.SelectedItem as string ?? "", relationType.SelectedItem as string ?? "", Lifetime.Token), InspectorFields);
        Start(requirement);
    }
    private ComboBox Endpoint(string caption, string id)
    { var box = new ComboBox { Style = (System.Windows.Style)FindResource("Form.ComboBox") }; Identify(box, id, caption); InspectorFields.Children.Add(new Label { Content = caption, Target = box }); InspectorFields.Children.Add(box); return box; }
    private async Task<SourceAuthoringResult> AddBlock(string blockKind)
    { var n = 1; while (requirement.Model.Blocks.Any(b => b.Name == "node" + n)) n++; var r = await requirement.AddBlockAsync("node" + n, blockKind, Lifetime.Token); if (r.Success) requirement.Select(requirement.Model.Blocks[^1].Identity); return r; }
    private RequirementBlock? Block => requirement.Model.Blocks.FirstOrDefault(b => b.Identity == requirement.Selection);
    private RequirementRelation? Relation => requirement.Model.Relations.FirstOrDefault(r => r.Identity == requirement.Selection);
    protected override IEnumerable<Choice> Choices()
    {
        yield return new(requirement.Model.HeaderIdentity, "Diagram title and direction");
        foreach (var b in requirement.Model.Blocks) yield return new(b.Identity, (b.Element ? "Element · " : "Requirement · ") + b.Name + (b.Complete && requirement.Model.Unique(b.Name) ? "" : " · protected hidden/duplicate/malformed occurrence"));
        foreach (var r in requirement.Model.Relations) yield return new(r.Identity, "Relation · " + r.Source + " → " + r.Target + " · " + r.Type);
        foreach (var key in requirement.Model.Protected) yield return new(key, "Protected source · " + requirement.Text.Line(key).Content);
    }
    protected override void ShowProperties()
    {
        var b = Block; var r = Relation; var heading = requirement.Selection == requirement.Model.HeaderIdentity; var editable = b is not null && b.Complete && requirement.Model.Unique(b.Name);
        Captions(b is null ? heading ? "Effective title" : "Source symbolic name" : "Symbolic name (Update renames all proved references)", b is null ? heading ? "Direction (TB, BT, LR, RL)" : "Target symbolic name" : b.Element ? "Element type" : "Display ID (does not rename the node)", b is null ? "Relation type" : "Requirement text");
        Properties(b?.Name ?? r?.Source ?? (heading ? requirement.Model.Title?.Value ?? "" : ""), b is not null ? b.Value(b.Element ? "type" : "id") : r?.Target ?? (heading ? requirement.Model.Direction?.Value ?? "TB" : ""), b?.Value("text") ?? r?.Type ?? "",
            editLabel: editable || r is not null && requirement.Model.TopologySafe || heading, editIdentifier: editable || r is not null && requirement.Model.TopologySafe || heading, editValue: editable && !b!.Element || r is not null && requirement.Model.TopologySafe);
        kind.Text = b?.Kind ?? ""; risk.Text = b?.Value("risk") ?? ""; verification.Text = b?.Value("verifymethod") ?? ""; docRef.Text = b?.Value("docRef") ?? "";
        kind.IsEnabled = risk.IsEnabled = verification.IsEnabled = editable && !b!.Element; docRef.IsEnabled = editable && b!.Element;
        sourceEndpoint.ItemsSource = targetEndpoint.ItemsSource = requirement.Model.Visible.Select(n => n.Name).ToArray(); sourceEndpoint.SelectedIndex = 0; targetEndpoint.SelectedIndex = requirement.Model.Visible.Count > 1 ? 1 : 0;
        Describe(b is null ? r is null ? "Requirement · effective presentation" : "Relationship occurrence · " + (r.Reverse ? "reverse arrow source spelling" : "forward arrow source spelling") : b.Kind + " · " + b.Name,
            "Canonical native boxes and routes. Display ID is a property; symbolic names identify endpoints. Update of a symbolic name patches every proved reference atomically. Unknown/repeated properties stay exact; blank final properties override older values. Duplicate, hidden, malformed, parallel, self-referencing, cyclic or unresolved topology is protected. Remove incident relations before deleting a block. Six kinds: " + string.Join(", ", RequirementVisualDraft.Kinds) + ".");
    }
    protected override Task<SourceAuthoringResult> UpdateProperties() => Block is { } b ? requirement.UpdateBlockAsync(b.Identity, kind.Text,
        b.Element ? new Dictionary<string, string> { ["type"] = Identifier.Text, ["docRef"] = docRef.Text } : new Dictionary<string, string> { ["id"] = Identifier.Text, ["text"] = Value.Text, ["risk"] = risk.Text, ["verifymethod"] = verification.Text }, Lifetime.Token, Label.Text)
        : Relation is { } r ? requirement.UpdateRelationAsync(r.Identity, Label.Text, Identifier.Text, Value.Text, Lifetime.Token) : requirement.SetHeadingAsync(Label.Text, Identifier.Text, Lifetime.Token);
    protected override Task<SourceAuthoringResult> DeleteSelection() => requirement.DeleteAsync(requirement.Selection ?? "", Lifetime.Token);
    protected override Task<SourceAuthoringResult> MoveSelection(int direction) => requirement.MoveAsync(requirement.Selection ?? "", direction, Lifetime.Token);
    protected override IReadOnlyList<NativeSourceHit> Correspondence(string source, DiagramResult render)
    {
        if (source != requirement.Source) return []; var root = XDocument.Parse(render.Svg!).Root!; var result = RequirementVisualDraft.Correspondence(requirement.Model, render.Svg!).Select(p => Hit(p.Block.Identity, "Block · " + p.Block.Name, p.Shape, root)).OfType<NativeSourceHit>().ToList();
        foreach (var (r, path) in RequirementVisualDraft.RelationCorrespondence(requirement.Model, render.Svg!))
        { var shape = NativeSourceHit.ShapeGeometry(path, root)?.GetWidenedPathGeometry(new Pen(Brushes.Black, 8)); if (shape is not null) { shape.Freeze(); result.Add(new(r.Identity, "Relation · " + r.Source + " → " + r.Target, shape.Bounds, shape)); } }
        return result;
    }
}
