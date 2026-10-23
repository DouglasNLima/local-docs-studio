using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Core.VennDiagrams;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public sealed class VennVisualEditorWindow : TopologyVisualEditorWindow
{
    private readonly VennVisualDraft venn;
    private readonly ComboBox renameDisplay;
    private VennSourceSet? SetOccurrence => venn?.Model.Sets.FirstOrDefault(s => s.Identity == venn.Selection);
    private VennSourceUnion? Union => venn?.Model.Unions.FirstOrDefault(u => u.Identity == venn.Selection);
    protected override bool RejectOverlappingPointer => true;
    protected override string Values => base.Values + "\u001f" + renameDisplay?.SelectedItem;
    public VennVisualEditorWindow(string source, DiagramService diagrams, Func<string, CancellationToken, Task<string?>> apply,
        bool dark = false, string diagramTheme = "Auto", Action? editSource = null, FontFamily? sourceFont = null, double sourceFontSize = 14)
        : base("Venn", diagrams, apply, dark, diagramTheme, editSource, sourceFont, sourceFontSize)
    {
        venn = new(source, diagrams); venn.ValidatePresentation = (text, _, token) => ValidateNative(text, token);
        renameDisplay = Selector("Absent-label ID rename behaviour", "RenameDisplay", ["preserve display label", "rename default display with ID"]);
        ActionButton("Make set label explicit", "MakeLabelExplicit", () => venn.MakeLabelExplicitAsync(venn.Selection ?? "", Lifetime.Token), InspectorFields);
        ActionButton("Remove set and all proved unions", "DeleteConnected", () => venn.DeleteAsync(venn.Selection ?? "", true, Lifetime.Token), InspectorFields);
        ActionButton("Add set…", "AddSet", AddSet); ActionButton("Add union…", "AddUnion", AddUnion); Start(venn);
    }
    private async Task<SourceAuthoringResult> AddSet()
    {
        var id = PromptIdentifier("set", value => venn.Model.Sets.Any(s => s.Id == value)); if (id is null) return Protected("Addition cancelled; source is unchanged.");
        var result = await venn.AddSetAsync(id, token: Lifetime.Token); if (result.Success) venn.Select(venn.Model.Sets[^1].Identity); return result;
    }
    private async Task<SourceAuthoringResult> AddUnion()
    {
        var prompt = new TextPromptWindow("Add Venn union", "Enter at least two distinct existing safe set IDs, separated by commas. The union starts without a rendered label.", "", "Add union", "Union member IDs", "Example: A, B. No implicit sets are created.", value => value.Split(',').Length >= 2 ? null : "Enter at least two distinct IDs.", trimInput: false) { Owner = this };
        if (prompt.ShowDialog() != true) return Protected("Addition cancelled; source is unchanged.");
        var result = await venn.AddUnionAsync(prompt.Value, token: Lifetime.Token); if (result.Success) venn.Select(venn.Model.Unions[^1].Identity); return result;
    }
    protected override IEnumerable<Choice> Choices()
    {
        yield return new(venn.Model.HeaderIdentity, "Venn · fixed native circles");
        foreach (var (set, i) in venn.Model.Sets.Select((s, i) => (s, i))) yield return new(set.Identity, "Set " + (i + 1) + " · " + set.Id + " · " + set.Label + (!set.Proved || !venn.Model.Unique(set.Id) ? " · protected source" : set.LabelToken is null ? " · label defaults to ID" : ""));
        foreach (var (union, i) in venn.Model.Unions.Select((u, i) => (u, i))) yield return new(union.Identity, "Union " + (i + 1) + " · " + union.MembersToken.Raw + " · " + (union.LabelToken?.Value ?? "unrendered label") + (!union.Proved || !venn.Model.StructureSafe ? " · protected references" : " · keyboard occurrence"));
        foreach (var key in venn.Model.Protected) yield return new(key, "Protected source · " + venn.Text.Line(key).Content);
    }
    protected override void ShowProperties()
    {
        var set = SetOccurrence; var union = Union; var safeSet = set?.Proved == true && venn.Model.Unique(set.Id); var safeUnion = union?.Proved == true && venn.Model.StructureSafe;
        Captions("Separate display label (absent defaults to ID)", set is null ? "Proved union member source" : "Unique safe set ID (atomic references)", "Stored size metadata (does not change geometry)");
        Properties(set?.Label ?? union?.Label ?? "", set?.Id ?? union?.MembersToken.Raw ?? "", set?.SizeToken?.Raw ?? union?.SizeToken?.Raw ?? "", editLabel: safeSet && set!.LabelToken is not null || safeUnion && union!.LabelToken is not null,
            editIdentifier: safeSet || safeUnion, editValue: safeSet && set!.SizeToken is not null || safeUnion && union!.SizeToken is not null);
        renameDisplay.SelectedItem = "preserve display label"; renameDisplay.IsEnabled = safeSet && set!.LabelToken is null;
        Describe(set is null ? union is null ? "Venn · sets and source references" : "Union source occurrence · no separate native intersection region" : "Set source occurrence · " + set.Id,
            "The pinned native renderer uses a fixed radius and set-count arrangement. Size is stored source metadata; it never scales circles or computes proportional overlaps. Set labels remain separate from IDs; make an absent label explicit to retain its display text during rename, or explicitly choose to rename the default display with ID. Atomic rename patches every proved union reference with original quoting/spacing. Connected deletion rejects remaining unions unless the explicit atomic-removal action is chosen. Unknown, duplicate or missing IDs and unproved comma/quote/escape forms stay source-only. Circle overlap pointer ambiguity requires the keyboard list. Unions have no invented hit regions, including unrendered labels.");
    }
    protected override Task<SourceAuthoringResult> UpdateProperties()
    {
        if (SetOccurrence is { } set)
        {
            var rename = Selected(renameDisplay) == "rename default display with ID";
            return venn.UpdateSetAsync(set.Identity, Identifier.Text, set.LabelToken is null && rename && Identifier.Text != set.Id ? Identifier.Text : Label.Text, Value.Text, rename, Lifetime.Token);
        }
        return Union is { } union ? venn.UpdateUnionAsync(union.Identity, Identifier.Text, Label.Text, Value.Text, Lifetime.Token) : Task.FromResult(Protected("Select a proved source occurrence."));
    }
    protected override Task<SourceAuthoringResult> DeleteSelection() => venn.DeleteAsync(venn.Selection ?? "", token: Lifetime.Token);
    protected override Task<SourceAuthoringResult> MoveSelection(int direction) => venn.MoveAsync(venn.Selection ?? "", direction, Lifetime.Token);
    protected override IReadOnlyList<NativeSourceHit> Correspondence(string source, DiagramResult render)
    {
        if (source != venn.Source) return []; var root = XDocument.Parse(render.Svg!).Root!;
        return VennVisualDraft.Correspondence(venn.Model, render.Svg!).Select(p => Hit(p.Set.Identity, "Venn set · " + p.Set.Id, p.Shape, root)).OfType<NativeSourceHit>().ToArray();
    }
}
