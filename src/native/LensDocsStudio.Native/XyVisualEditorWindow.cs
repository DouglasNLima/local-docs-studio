using System.Windows.Media;
using System.Xml.Linq;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Core.XyCharts;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public sealed class XyVisualEditorWindow : ChartVisualEditorWindow
{
    private readonly XyVisualDraft xy;
    public XyVisualEditorWindow(string source, DiagramService diagrams, Func<string, CancellationToken, Task<string?>> apply,
        bool dark = false, string diagramTheme = "Auto", Action? editSource = null, FontFamily? sourceFont = null, double sourceFontSize = 14)
        : base("XY Chart", diagrams, apply, dark, diagramTheme, editSource, sourceFont, sourceFontSize)
    {
        xy = new(source, diagrams); xy.ValidatePresentation = (text, _, token) => ValidateNative(text, token);
        ActionButton("Add bar with zeros", "AddBar", async () => { var r = await xy.AddSeriesAsync("bar", Lifetime.Token); if (r.Success) xy.Select(xy.Model.Series[^1].Identity); return r; });
        ActionButton("Add line with zeros", "AddLine", async () => { var r = await xy.AddSeriesAsync("line", Lifetime.Token); if (r.Success) xy.Select(xy.Model.Series[^1].Identity); return r; });
        ActionButton("Add value 0 to selected series", "AddValue", async () =>
        {
            var s = xy.Model.Series.FirstOrDefault(s => s.Identity == xy.Selection || s.Values.Items.Any(v => v.Identity == xy.Selection));
            if (s is null) return Protected("Select a simple series or one of its values.");
            var r = await xy.AddValueAsync(s.Identity, "0", Lifetime.Token); if (r.Success) xy.Select(xy.Model.Series.Single(v => v.Identity == s.Identity).Values.Items[^1].Identity); return r;
        });
        ActionButton("Add category with value 0 in every series", "AddCategory", async () => { var r = await xy.AddCategoryAsync("New category", "0", Lifetime.Token); if (r.Success) xy.Select(xy.Model.Categories!.Categories!.Items[^1].Identity); return r; });
        Start(xy);
    }
    protected override IEnumerable<Choice> Choices()
    {
        var m = xy.Model; yield return new(m.HeaderIdentity, "Diagram title · " + m.Title?.Value);
        yield return new(m.HeaderIdentity + ":orientation", "Presentation · " + (m.Horizontal ? "horizontal" : "vertical"));
        yield return new(m.HeaderIdentity + ":x-axis", "X axis title · " + m.AxisTitle("x-axis")); yield return new(m.HeaderIdentity + ":y-axis", "Y axis title · " + m.AxisTitle("y-axis"));
        yield return new(m.HeaderIdentity + ":range", "Y range · " + (m.Range is null ? "automatic" : m.Range.Range!.Value));
        if (m.Categories?.Categories is { } cats) foreach (var (c, i) in cats.Items.Select((c, i) => (c, i))) yield return new(c.Identity, "Category " + (i + 1) + " · " + c.Value);
        foreach (var (s, index) in m.Series.Select((s, i) => (s, i)))
        {
            yield return new(s.Identity, "Series " + (index + 1) + " · " + s.Kind + " · " + s.Name + (s.Simple ? "" : " · protected"));
            foreach (var (v, i) in s.Values.Items.Select((v, i) => (v, i))) yield return new(v.Identity, "Series " + (index + 1) + " · value " + (i + 1) + " · " + v.Raw);
        }
        foreach (var key in m.Protected.Where(k => m.Series.All(s => s.Identity != k))) yield return new(key, "Protected source · " + xy.Text.Line(key).Content);
    }
    protected override string SourceIdentity(string id)
    {
        var m = xy.Model;
        if (id == m.HeaderIdentity + ":range") return m.Range?.Identity ?? m.HeaderIdentity;
        if (id == m.HeaderIdentity + ":orientation") return m.HeaderIdentity;
        if (id == m.HeaderIdentity + ":x-axis" || id == m.HeaderIdentity + ":y-axis") return m.Axes.LastOrDefault(a => a.Name == id[(id.IndexOf(':') + 1)..])?.Identity ?? m.HeaderIdentity;
        return m.Series.FirstOrDefault(s => s.Values.Items.Any(v => v.Identity == id))?.Identity ?? m.Axes.FirstOrDefault(a => a.Categories?.Items.Any(c => c.Identity == id) == true)?.Identity ?? id;
    }
    protected override void ShowProperties()
    {
        var m = xy.Model; var id = xy.Selection;
        var s = m.Series.FirstOrDefault(s => s.Identity == id || s.Values.Items.Any(v => v.Identity == id)); var v = s?.Values.Items.FirstOrDefault(v => v.Identity == id);
        var c = m.Categories?.Categories?.Items.FirstOrDefault(c => c.Identity == id);
        if (id == m.HeaderIdentity) Properties(m.Title?.Value ?? "", editLabel: true);
        else if (id == m.HeaderIdentity + ":orientation") Properties(m.Horizontal ? "horizontal" : "vertical", editLabel: true);
        else if (id == m.HeaderIdentity + ":range") Properties(value: m.Range?.Range?.Groups["min"].Value ?? "", maximum: m.Range?.Range?.Groups["max"].Value ?? "", editValue: true, editMaximum: true);
        else if (id == m.HeaderIdentity + ":x-axis" || id == m.HeaderIdentity + ":y-axis") Properties(m.AxisTitle(id[(id.IndexOf(':') + 1)..]), editLabel: true);
        else if (c is not null) Properties(c.Value, editLabel: true);
        else if (v is not null) Properties(value: v.Raw, editValue: s!.Simple);
        else if (s is not null) Properties(s.Name, editLabel: s.Simple);
        else Properties();
        Describe(s is null ? "XY Chart · category-based authoring" : "Series " + (m.Series.ToList().IndexOf(s) + 1) + " · " + s.Kind,
            m.Errors.FirstOrDefault() ?? (s is { Simple: false } ? "Protected: box syntax, invalid/coerced values or ignored point suffixes stay exact. Inspect the source."
            : "Every category, series and value is a separate source occurrence. List selection includes coincident, zero and clipped marks. Category add/remove/reorder affects every aligned simple series atomically; ragged vectors are never repaired. Blank minimum and maximum select automatic Y range. Presentation accepts horizontal or vertical. Numeric X ranges remain source-only."));
    }
    protected override Task<SourceAuthoringResult> UpdateProperties()
    {
        var m = xy.Model; var id = xy.Selection ?? "";
        if (id == m.HeaderIdentity) return xy.SetTitleAsync(Label.Text, Lifetime.Token);
        if (id == m.HeaderIdentity + ":orientation") return Label.Text is "horizontal" or "vertical" ? xy.SetOrientationAsync(Label.Text == "horizontal", Lifetime.Token) : Task.FromResult(Protected("Enter horizontal or vertical. Pending input is retained."));
        if (id == m.HeaderIdentity + ":range") return xy.SetRangeAsync(Value.Text, Maximum.Text, Lifetime.Token);
        if (id == m.HeaderIdentity + ":x-axis" || id == m.HeaderIdentity + ":y-axis") return xy.SetAxisTitleAsync(id[(id.IndexOf(':') + 1)..], Label.Text, Lifetime.Token);
        if (m.Categories?.Categories?.Items.Any(c => c.Identity == id) == true) return xy.UpdateCategoryAsync(id, Label.Text, Lifetime.Token);
        if (m.Series.Any(s => s.Identity == id)) return xy.UpdateSeriesAsync(id, Label.Text, Lifetime.Token);
        return xy.UpdateValueAsync(id, Value.Text, Lifetime.Token);
    }
    protected override Task<SourceAuthoringResult> DeleteSelection() => xy.DeleteAsync(xy.Selection ?? "", Lifetime.Token);
    protected override Task<SourceAuthoringResult> MoveSelection(int direction) => xy.MoveAsync(xy.Selection ?? "", direction, Lifetime.Token);
    protected override IReadOnlyList<NativeSourceHit> Correspondence(string source, DiagramResult render)
    {
        if (source != xy.Source) return []; XyVisualDraft.VerifyCanonical(xy.Model, render.Svg!);
        var root = XDocument.Parse(render.Svg!).Root!; var hits = new List<NativeSourceHit>();
        foreach (var (s, index) in xy.Model.Series.Select((s, i) => (s, i)))
        {
            if (s.Kind == "box") continue;
            var group = root.Descendants().Single(e => (string?)e.Attribute("data-series") == index.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var marks = s.Kind == "bar" ? group.Elements().ToArray() : group.Elements().Where(e => e.Name.LocalName == "circle").ToArray();
            for (var i = 0; i < marks.Length; i++) if (Hit(s.Values.Items[i].Identity, "Series " + (index + 1) + " · value " + (i + 1), marks[i], root) is { } hit) hits.Add(hit);
        }
        return hits;
    }
}
