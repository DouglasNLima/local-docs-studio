using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using LensDocsStudio.Core.QuadrantDiagrams;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public sealed class QuadrantVisualEditorWindow : SourceVisualEditorWindow
{
    private readonly QuadrantVisualDraft quadrant;
    private readonly TextBox label, opposite, x, y;
    private static readonly string[] Names = ["x-axis", "y-axis", "quadrant-1", "quadrant-2", "quadrant-3", "quadrant-4"];
    protected override string Values => label is null ? "" : string.Join('\u001f', label.Text, opposite.Text, x.Text, y.Text);
    private string? DeclarationName => quadrant.Selection is { } key && key.StartsWith(quadrant.Model.HeaderIdentity + ":", StringComparison.Ordinal) ? key[(key.IndexOf(':') + 1)..] : null;
    public QuadrantVisualEditorWindow(string source, DiagramService diagrams, Func<string, CancellationToken, Task<string?>> apply,
        bool dark = false, string diagramTheme = "Auto", Action? editSource = null, FontFamily? sourceFont = null, double sourceFontSize = 14)
        : base("Quadrant", diagrams, apply, dark, diagramTheme, editSource, sourceFont, sourceFontSize)
    {
        label = Field("Label, title or left/bottom axis label", "Label"); opposite = Field("Right/top axis label (optional)", "OppositeLabel");
        x = Field("X coordinate (0–1, decimal point)", "X"); y = Field("Y coordinate (0–1, decimal point)", "Y");
        quadrant = new(source, diagrams); quadrant.ValidatePresentation = (text, _, token) => ValidateNative(text, token);
        ActionButton("Add point", "AddPoint", async () => { var result = await quadrant.AddAsync("New point", "0.5", "0.5", Lifetime.Token); if (result.Success) quadrant.Select(quadrant.Model.Points[^1].Identity); return result; }); Start(quadrant);
    }
    protected override IEnumerable<Choice> Choices()
    {
        yield return new(quadrant.Model.HeaderIdentity, "Diagram title · " + quadrant.Model.Title?.Value);
        foreach (var name in Names) yield return new(quadrant.Model.HeaderIdentity + ":" + name, name switch { "x-axis" => "X axis · left → right", "y-axis" => "Y axis · bottom → top", "quadrant-1" => "Quadrant 1 · top right", "quadrant-2" => "Quadrant 2 · top left", "quadrant-3" => "Quadrant 3 · bottom left", _ => "Quadrant 4 · bottom right" });
        foreach (var (point, index) in quadrant.Model.Points.Select((p, i) => (p, i))) yield return new(point.Identity, "Point " + (index + 1) + " · " + point.Label + " · [" + point.X + ", " + point.Y + "]");
        foreach (var d in quadrant.Model.Declarations) yield return new(d.Identity, "Source declaration · " + d.Name + " · " + d.Value);
        foreach (var key in quadrant.Model.Protected) yield return new(key, "Protected · " + quadrant.Text.Line(key).Content);
    }
    protected override string SourceIdentity(string identity) => DeclarationName is { } name ? quadrant.Model.Effective(name)?.Identity ?? quadrant.Model.HeaderIdentity : identity;
    protected override void ShowProperties()
    {
        var p = quadrant.Model.Points.FirstOrDefault(p => p.Identity == quadrant.Selection); var title = quadrant.Selection == quadrant.Model.HeaderIdentity; var name = DeclarationName; var axis = name is "x-axis" or "y-axis";
        var value = name is null ? "" : quadrant.Model.Effective(name)?.Value ?? "";
        var parts = axis ? SourceText.Pattern(@"^(?<first>.+?)(?:\s*-->\s*(?<second>.+))?$").Match(value) : null;
        label.Text = title ? quadrant.Model.Title?.Value ?? "" : p?.Label ?? (axis ? parts!.Groups["first"].Value.Trim() : value);
        opposite.Text = axis ? parts!.Groups["second"].Value.Trim() : ""; x.Text = p?.X ?? ""; y.Text = p?.Y ?? "";
        label.IsEnabled = title || p is not null || name is not null; opposite.IsEnabled = axis; x.IsEnabled = y.IsEnabled = p is not null;
        Describe(title ? "Quadrant title" : name ?? p?.Label ?? "Select a point or effective declaration",
            quadrant.Model.Errors.FirstOrDefault() ?? (p is null ? "Edit effective declarations above. Earlier source declarations remain unchanged and inspectable. Axis labels cannot contain -->. Point list order distinguishes coincident and repeated labels." :
            "Raw coordinates [" + p.X + ", " + p.Y + "]; effective renderer values [" + p.EffectiveX.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", " + p.EffectiveY.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]. Unchanged raw lexemes are preserved. Changed coordinates require 0–1; Y increases upwards."));
    }
    protected override Task<SourceAuthoringResult> UpdateProperties()
    {
        if (quadrant.Selection == quadrant.Model.HeaderIdentity) return quadrant.SetTitleAsync(label.Text, Lifetime.Token);
        if (DeclarationName is { } name)
        {
            return name is "x-axis" or "y-axis" ? quadrant.SetAxisAsync(name, label.Text, opposite.Text, Lifetime.Token) : quadrant.SetDeclarationAsync(name, label.Text, Lifetime.Token);
        }
        return quadrant.UpdateAsync(quadrant.Selection ?? "", label.Text, x.Text, y.Text, Lifetime.Token);
    }
    protected override Task<SourceAuthoringResult> DeleteSelection() => quadrant.DeleteAsync(quadrant.Selection ?? "", Lifetime.Token);
    protected override Task<SourceAuthoringResult> MoveSelection(int direction) => quadrant.MoveAsync(quadrant.Selection ?? "", direction, Lifetime.Token);
    protected override IReadOnlyList<NativeSourceHit> Correspondence(string source, DiagramResult render)
    {
        if (source != quadrant.Source) return [];
        QuadrantVisualDraft.VerifyCanonical(quadrant.Model, render.Svg!);
        var root = XDocument.Parse(render.Svg!).Root!; var groups = SourceDeclarations.Groups(render.Svg!, "quadrant-point"); var hits = new List<NativeSourceHit>();
        for (var i = 0; i < groups.Length; i++)
            if (NativeSourceHit.ShapeGeometry(groups[i], root) is { } geometry) hits.Add(new(quadrant.Model.Points[i].Identity, "Point " + (i + 1) + " · " + quadrant.Model.Points[i].Label, geometry.Bounds, geometry));
        return hits;
    }
}
