using System.Windows.Media;
using System.Xml.Linq;
using LensDocsStudio.Core.RadarCharts;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public sealed class RadarVisualEditorWindow : ChartVisualEditorWindow
{
    private readonly RadarVisualDraft radar;
    public RadarVisualEditorWindow(string source, DiagramService diagrams, Func<string, CancellationToken, Task<string?>> apply,
        bool dark = false, string diagramTheme = "Auto", Action? editSource = null, FontFamily? sourceFont = null, double sourceFontSize = 14)
        : base("Radar", diagrams, apply, dark, diagramTheme, editSource, sourceFont, sourceFontSize)
    {
        radar = new(source, diagrams); radar.ValidatePresentation = (text, _, token) => ValidateNative(text, token);
        ActionButton("Add axis with value 0 in every curve", "AddAxis", async () =>
        { var n = 1; while (radar.Model.Axes.Any(a => a.Id == "axis" + n)) n++; var r = await radar.AddAxisAsync("axis" + n, "New axis", "0", Lifetime.Token); if (r.Success) radar.Select(radar.Model.Axes[^1].Identity); return r; });
        ActionButton("Add curve with value 0 on every axis", "AddCurve", async () =>
        { var n = 1; while (radar.Model.Curves.Any(c => c.Id == "curve" + n)) n++; var r = await radar.AddCurveAsync("curve" + n, "New curve", "0", Lifetime.Token); if (r.Success) radar.Select(radar.Model.Curves[^1].Identity); return r; });
        Start(radar);
    }
    protected override IEnumerable<Choice> Choices()
    {
        var m = radar.Model; yield return new(m.HeaderIdentity, "Diagram title · " + m.Title?.Value); yield return new(m.HeaderIdentity + ":range", "Range · " + m.Minimum.ToString(System.Globalization.CultureInfo.InvariantCulture) + " to " + m.Maximum.ToString(System.Globalization.CultureInfo.InvariantCulture) + (m.Effective("max") is null ? " · automatic maximum" : ""));
        foreach (var (a, i) in m.Axes.Select((a, i) => (a, i))) yield return new(a.Identity, "Axis " + (i + 1) + " · " + a.Id + " · " + a.Label);
        foreach (var (c, i) in m.Curves.Select((c, i) => (c, i)))
        {
            yield return new(c.Identity, "Curve " + (i + 1) + " · " + c.Id + " · " + c.Label + (c.Simple ? "" : " · protected"));
            foreach (var (v, index) in c.Values.Items.Select((v, index) => (v, index))) yield return new(v.Identity, "Curve " + (i + 1) + " · axis " + (index + 1) + " · " + (index < m.Axes.Count ? m.Axes[index].Label : "extra position") + " · " + v.Raw);
        }
        foreach (var key in m.Protected.Where(k => m.Curves.All(c => c.Identity != k))) yield return new(key, "Protected source · " + radar.Text.Line(key).Content);
    }
    protected override string SourceIdentity(string id)
    { var m = radar.Model; return id == m.HeaderIdentity + ":range" ? m.Effective("max")?.Identity ?? m.HeaderIdentity : m.Axes.FirstOrDefault(a => a.Identity == id)?.Row ?? m.Curves.FirstOrDefault(c => c.Values.Items.Any(v => v.Identity == id))?.Identity ?? id; }
    protected override void ShowProperties()
    {
        var m = radar.Model; var id = radar.Selection; var a = m.Axes.FirstOrDefault(a => a.Identity == id); var c = m.Curves.FirstOrDefault(c => c.Identity == id || c.Values.Items.Any(v => v.Identity == id)); var v = c?.Values.Items.FirstOrDefault(v => v.Identity == id);
        if (id == m.HeaderIdentity) Properties(m.Title?.Value ?? "", editLabel: true);
        else if (id == m.HeaderIdentity + ":range") Properties(value: m.Effective("min")?.Value ?? "0", maximum: m.Effective("max")?.Value ?? "", editValue: true, editMaximum: true);
        else if (a is not null) Properties(a.Label, a.Id, editLabel: a.Simple, editIdentifier: a.Simple);
        else if (v is not null) Properties(value: v.Raw, editValue: c!.Simple);
        else if (c is not null) Properties(c.Label, c.Id, editLabel: c.Simple, editIdentifier: c.Simple);
        else Properties();
        Describe("Radar · positional curve/axis authoring", m.Errors.FirstOrDefault() ?? (c is { Simple: false } ? "Protected: keyed prefixes, dropped values, non-finite numbers or ignored suffixes stay exact. Inspect the original source."
            : "Axis add/remove/reorder is one atomic operation across every exactly aligned simple curve. Initial values are explicitly 0. Ragged/extra vectors and repeated IDs protect structural edits. Curve/axis list order distinguishes overlapping marks and duplicate labels. Blank maximum selects automatic maximum; min/max directives require unsigned decimals. Legend, graticule and ticks remain unchanged in source."));
    }
    protected override Task<SourceAuthoringResult> UpdateProperties()
    {
        var m = radar.Model; var id = radar.Selection ?? "";
        if (id == m.HeaderIdentity) return radar.SetTitleAsync(Label.Text, Lifetime.Token);
        if (id == m.HeaderIdentity + ":range") return radar.SetRangeAsync(Value.Text, Maximum.Text, Lifetime.Token);
        if (m.Axes.Any(a => a.Identity == id)) return radar.UpdateAxisAsync(id, Identifier.Text, Label.Text, Lifetime.Token);
        if (m.Curves.Any(c => c.Identity == id)) return radar.UpdateCurveAsync(id, Identifier.Text, Label.Text, Lifetime.Token);
        return radar.UpdateValueAsync(id, Value.Text, Lifetime.Token);
    }
    protected override Task<SourceAuthoringResult> DeleteSelection() => radar.DeleteAsync(radar.Selection ?? "", Lifetime.Token);
    protected override Task<SourceAuthoringResult> MoveSelection(int direction) => radar.MoveAsync(radar.Selection ?? "", direction, Lifetime.Token);
    protected override IReadOnlyList<NativeSourceHit> Correspondence(string source, DiagramResult render)
    {
        if (source != radar.Source) return []; RadarVisualDraft.VerifyCanonical(radar.Model, render.Svg!);
        var root = XDocument.Parse(render.Svg!).Root!; var groups = SourceDeclarations.Groups(render.Svg!, "radar-curve"); var hits = new List<NativeSourceHit>();
        if (radar.Model.Curves.Any(c => !c.Simple)) return hits;
        for (var i = 0; i < groups.Length; i++)
        {
            var c = radar.Model.Curves[i]; var marks = groups[i].Elements().Where(e => e.Name.LocalName == "circle").ToArray();
            for (var j = 0; j < Math.Min(marks.Length, c.Values.Items.Count); j++) if (Hit(c.Values.Items[j].Identity, "Curve " + (i + 1) + " · axis " + (j + 1), marks[j], root) is { } point) hits.Add(point);
        }
        for (var i = 0; i < groups.Length; i++) if (Hit(radar.Model.Curves[i].Identity, "Curve " + (i + 1) + " · " + radar.Model.Curves[i].Label, groups[i], root) is { } hit) hits.Add(hit);
        return hits;
    }
}
