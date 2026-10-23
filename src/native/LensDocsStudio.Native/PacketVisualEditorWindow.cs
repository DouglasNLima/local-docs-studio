using System.Windows.Media;
using System.Xml.Linq;
using LensDocsStudio.Core.PacketDiagrams;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public sealed class PacketVisualEditorWindow : ChartVisualEditorWindow
{
    private readonly PacketVisualDraft packet;
    private PacketSourceField? FieldOccurrence => packet?.Model.Fields.FirstOrDefault(f => f.Identity == packet.Selection);
    public PacketVisualEditorWindow(string source, DiagramService diagrams, Func<string, CancellationToken, Task<string?>> apply,
        bool dark = false, string diagramTheme = "Auto", Action? editSource = null, FontFamily? sourceFont = null, double sourceFontSize = 14)
        : base("Packet", diagrams, apply, dark, diagramTheme, editSource, sourceFont, sourceFontSize)
    {
        packet = new(source, diagrams); packet.ValidatePresentation = (text, _, token) => ValidateNative(text, token);
        ActionButton("Add field…", "AddField", Add); Start(packet);
    }
    private async Task<SourceAuthoringResult> Add()
    {
        var prompt = new TextPromptWindow("Add Packet field", "Enter an explicit inclusive range, one bit or relative count, with its quoted label.", "", "Add field", "New field declaration",
            "Examples: 48-63: \"Options\", 64: \"Flag\", +8: \"Payload\". Existing ranges are never packed or renumbered.", value => ExpansionViSource.PacketField.IsMatch(value) ? null : "Use start-end: \"Label\", bit: \"Label\" or +count: \"Label\".", trimInput: false) { Owner = this };
        if (prompt.ShowDialog() != true) return Protected("Addition cancelled; source is unchanged.");
        var match = ExpansionViSource.PacketField.Match(prompt.Value); var relative = match.Groups["count"].Success; var range = match.Groups["end"].Success;
        var result = await packet.AddFieldAsync(relative ? "relative" : range ? "range" : "bit", match.Groups[relative ? "count" : "start"].Value, match.Groups["end"].Value, match.Groups["label"].Value, Lifetime.Token);
        if (result.Success) packet.Select(packet.Model.Fields[^1].Identity); return result;
    }
    protected override IEnumerable<Choice> Choices()
    {
        yield return new(packet.Model.HeaderIdentity, "Packet · effective title");
        foreach (var title in packet.Model.Titles.Where(t => t.Identity != packet.Model.HeaderIdentity)) yield return new(title.Identity, "Title · " + title.Value + (title == packet.Model.Title ? " · effective" : " · overridden source"));
        foreach (var (field, i) in packet.Model.Fields.Select((f, i) => (f, i))) yield return new(field.Identity, "Field " + (i + 1) + " · " + field.Label + " · " + (field.Accepted ? field.Start + ".." + field.End + " inclusive · " + field.Form : "skipped source") + (!packet.Model.Unambiguous(field) ? " · pointer protected" : ""));
        foreach (var key in packet.Model.Protected) yield return new(key, "Protected source · " + packet.Text.Line(key).Content);
    }
    protected override void ShowProperties()
    {
        var field = FieldOccurrence; var title = packet.Selection == packet.Model.HeaderIdentity || packet.Selection == packet.Model.Title?.Identity;
        Captions(title ? "Effective title" : "Literal field label", field?.Form == "relative" ? "Relative count (+count form)" : "Absolute inclusive start / one bit", "Absolute inclusive end (range form)", "Effective inclusive range (read-only)");
        Properties(field?.Label ?? (title ? packet.Model.Title?.Value ?? "" : ""), field?.FirstToken.Raw ?? "", field?.EndToken?.Raw ?? "", field is { Accepted: true } ? field.Start + ".." + field.End : "", editLabel: field?.Accepted == true || title, editIdentifier: field is not null && packet.Model.Unambiguous(field), editValue: field?.EndToken is not null && packet.Model.Unambiguous(field));
        var accepted = packet.Model.Fields.Where(f => f.Accepted).ToArray(); var gaps = accepted.Zip(accepted.Skip(1)).Any(p => p.Second.Start > p.First.End + 1); var overlaps = accepted.Any(f => !packet.Model.Unambiguous(f));
        Describe(field is null ? "Packet · ordered bit fields" : "Exact " + field.Form + " source occurrence · " + (field.Accepted ? field.Start + ".." + field.End + " inclusive" : "skipped by the native parser"),
            "Native 32-bit rows and all actual segments select one exact field. " + (gaps ? "Gaps are present. " : "") + (overlaps ? "Overlapping/duplicate ranges have keyboard-only source access. " : "") + "Range, one-bit and +count forms retain original spelling. Only accepted fields advance the relative cursor. An edit/remove/move that changes any other accepted or skipped field is rejected; inspect the complete source to make that impact explicit. No packing, renumbering, quote escapes or replacement grid. Bit range: 0..4095. Unknown and skipped lines remain available through Locate source.");
    }
    protected override Task<SourceAuthoringResult> UpdateProperties() => FieldOccurrence is { } field ? packet.UpdateFieldAsync(field.Identity, Label.Text, Identifier.Text, Value.Text, Lifetime.Token)
        : packet.Selection == packet.Model.HeaderIdentity || packet.Selection == packet.Model.Title?.Identity ? packet.SetTitleAsync(Label.Text, Lifetime.Token) : Task.FromResult(Protected("This occurrence is source-only. Use Locate source or Edit source."));
    protected override Task<SourceAuthoringResult> DeleteSelection() => packet.DeleteAsync(packet.Selection ?? "", Lifetime.Token);
    protected override Task<SourceAuthoringResult> MoveSelection(int direction) => packet.MoveAsync(packet.Selection ?? "", direction, Lifetime.Token);
    protected override IReadOnlyList<NativeSourceHit> Correspondence(string source, DiagramResult render)
    {
        if (source != packet.Source) return []; var root = XDocument.Parse(render.Svg!).Root!;
        // The shared shell keeps one hit per occurrence. Combine actual native segments, retaining gaps and row positions.
        return PacketVisualDraft.Correspondence(packet.Model, render.Svg!).GroupBy(p => p.Field.Identity).Select(group =>
        {
            var geometry = new GeometryGroup { FillRule = FillRule.Nonzero };
            foreach (var pair in group) if (NativeSourceHit.ShapeGeometry(pair.Shape, root) is { } shape) geometry.Children.Add(shape);
            geometry.Freeze(); return geometry.Children.Count == 0 ? null : new NativeSourceHit(group.Key, "Packet field · " + group.First().Field.Label, geometry.Bounds, geometry);
        }).OfType<NativeSourceHit>().ToArray();
    }
}
