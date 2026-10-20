using System.Globalization;
using System.Windows.Media;
using System.Xml.Linq;
using LensDocsStudio.Core.PieDiagrams;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public sealed class PieVisualEditorWindow : SourceVisualEditorWindow
{
    private readonly PieVisualDraft pie;
    private readonly System.Windows.Controls.TextBox label, number;
    protected override string Values => label is null ? "" : label.Text + '\u001f' + number.Text;
    public PieVisualEditorWindow(string source, DiagramService diagrams, Func<string, CancellationToken, Task<string?>> apply,
        bool dark = false, string diagramTheme = "Auto", Action? editSource = null, FontFamily? sourceFont = null, double sourceFontSize = 14)
        : base("Pie", diagrams, apply, dark, diagramTheme, editSource, sourceFont, sourceFontSize)
    {
        label = Field("Label or title", "Label"); number = Field("Value (decimal)", "Value");
        pie = new(source, diagrams); pie.ValidatePresentation = (text, _, token) => ValidateNative(text, token);
        ActionButton("Add category", "AddCategory", async () => { var result = await pie.AddAsync("New category", "1", Lifetime.Token); if (result.Success) pie.Select(pie.Model.Entries[^1].Identity); return result; }); Start(pie);
    }
    protected override IEnumerable<Choice> Choices()
    {
        yield return new(pie.Model.HeaderIdentity, "Diagram title · " + pie.Model.Title);
        foreach (var (entry, index) in pie.Model.Entries.Select((entry, index) => (entry, index))) yield return new(entry.Identity, "Category " + (index + 1) + " · " + entry.Label + " · " + entry.Number + (entry.Value == 0 ? " · zero, source only" : ""));
        foreach (var key in pie.Model.Protected) yield return new(key, "Protected · " + pie.Text.Line(key).Content);
    }
    protected override void ShowProperties()
    {
        var entry = pie.Model.Entries.FirstOrDefault(item => item.Identity == pie.Selection); var title = pie.Selection == pie.Model.HeaderIdentity;
        label.Text = title ? pie.Model.Title : entry?.Label ?? "";
        // UI uses the current decimal separator. Original source precision is retained if this field is untouched.
        number.Text = entry?.Number.Replace(".", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator, StringComparison.Ordinal) ?? "";
        label.IsEnabled = title || entry is not null; number.IsEnabled = entry is not null;
        Describe(title ? "Pie title" : entry is null ? "Select a category or title" : entry.Label,
            pie.Model.Errors.FirstOrDefault() ?? (entry is null && !title ? "Select a source occurrence. Protected directives remain unchanged." :
            "Quoted labels and non-negative decimals. Duplicate labels retain separate source occurrences. Zero rows remain in source and have no slice. The chart needs a positive finite total. Embedded quotes and line breaks are unsupported by Mermaider 0.15.1."));
    }
    protected override Task<SourceAuthoringResult> UpdateProperties()
    {
        if (pie.Selection == pie.Model.HeaderIdentity) return pie.SetTitleAsync(label.Text, Lifetime.Token);
        var original = pie.Model.Entries.FirstOrDefault(item => item.Identity == pie.Selection);
        if (original is null) return Task.FromResult(new SourceAuthoringResult(false, "Select a category."));
        var displayed = original.Number.Replace(".", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator, StringComparison.Ordinal);
        var value = number.Text == displayed ? original.Number : number.Text.Replace(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator, ".", StringComparison.Ordinal);
        return pie.UpdateAsync(original.Identity, label.Text, value, Lifetime.Token);
    }
    protected override Task<SourceAuthoringResult> DeleteSelection() => pie.DeleteAsync(pie.Selection ?? "", Lifetime.Token);
    protected override Task<SourceAuthoringResult> MoveSelection(int direction) => pie.MoveAsync(pie.Selection ?? "", direction, Lifetime.Token);
    protected override IReadOnlyList<NativeSourceHit> Correspondence(string source, DiagramResult render)
    {
        var root = XDocument.Parse(render.Svg!).Root!; var slices = root.Descendants().Where(element => (string?)element.Attribute("class") == "pie-slice").ToArray();
        var rows = pie.Model.Entries.Where(entry => entry.Value > 0).ToArray(); var result = new List<NativeSourceHit>();
        if (source != pie.Source || slices.Length != rows.Length) return result;
        for (var index = 0; index < rows.Length; index++)
        {
            if ((string?)slices[index].Attribute("data-label") != rows[index].Label) throw new ArgumentException("Canonical slice occurrence does not match source order.");
            // Exact occurrence order is verified above, including duplicate labels. The actual SVG
            // wedge supplies pointer hit testing and selection outlines; no bounding-box guessing.
            if (NativeSourceHit.ShapeGeometry(slices[index], root) is { } shape) result.Add(new(rows[index].Identity, "Category " + (index + 1) + " · " + rows[index].Label, shape.Bounds, shape));
        }
        return result;
    }
}
