using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using LensDocsStudio.Core.TimelineDiagrams;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public sealed class TimelineVisualEditorWindow : SourceVisualEditorWindow
{
    private readonly TimelineVisualDraft timeline;
    private readonly TextBox label;
    private readonly ComboBox destination;
    protected override string Values => label?.Text ?? "";
    private TimelinePeriodSource? Period => timeline.Model.Periods.FirstOrDefault(p => p.Identity == timeline.Selection || p.Events.Any(e => e.Identity == timeline.Selection));
    public TimelineVisualEditorWindow(string source, DiagramService diagrams, Func<string, CancellationToken, Task<string?>> apply,
        bool dark = false, string diagramTheme = "Auto", Action? editSource = null, FontFamily? sourceFont = null, double sourceFontSize = 14)
        : base("Timeline", diagrams, apply, dark, diagramTheme, editSource, sourceFont, sourceFontSize)
    {
        label = Field("Title, section, period or event label", "Label");
        destination = new ComboBox { Style = (System.Windows.Style)FindResource("Form.ComboBox") }; Identify(destination, "Destination", "Destination section for the selected period");
        InspectorFields.Children.Add(new Label { Content = "Destination section", Target = destination, Style = (System.Windows.Style)FindResource("Form.Label") }); InspectorFields.Children.Add(destination);
        timeline = new(source, diagrams); timeline.ValidatePresentation = (text, _, token) => ValidateNative(text, token);
        ActionButton("Move period to section", "MoveToSection", () => destination.SelectedItem is Choice choice && Period is { } p ? timeline.MoveToSectionAsync(p.Identity, choice.Identity, Lifetime.Token) : Task.FromResult(new SourceAuthoringResult(false, "Select a period and its destination section.")), InspectorFields);
        ActionButton("Add event", "AddEvent", async () => { if (Period is not { } p) return new(false, "Select a period or one of its events."); var result = await timeline.AddEventAsync(p.Identity, Lifetime.Token); if (result.Success) timeline.Select(timeline.Model.Periods.Single(x => x.Identity == p.Identity).Events[^1].Identity); return result; }, InspectorFields);
        ActionButton("Add period", "AddPeriod", async () =>
        {
            var section = timeline.Model.Sections.FirstOrDefault(s => s.Identity == timeline.Selection)?.Identity ?? Period?.Section ?? timeline.Model.Sections.Last().Identity;
            var keys = timeline.Model.Periods.Select(p => p.Identity).ToHashSet(); var result = await timeline.AddPeriodAsync(section, Lifetime.Token); if (result.Success) timeline.Select(timeline.Model.Periods.Single(p => !keys.Contains(p.Identity)).Identity); return result;
        });
        ActionButton("Add section", "AddSection", async () => { var result = await timeline.AddSectionAsync(Lifetime.Token); if (result.Success) timeline.Select(timeline.Model.Sections[^1].Identity); return result; }); Start(timeline);
    }
    protected override IEnumerable<Choice> Choices()
    {
        yield return new(timeline.Model.HeaderIdentity, "Diagram title · " + timeline.Model.Title?.Value);
        foreach (var section in timeline.Model.Sections)
        {
            yield return new(section.Identity, "Section · " + section.Name + (timeline.Model.Periods.Any(p => p.Section == section.Identity) ? "" : " · empty, source only"));
            foreach (var (p, index) in timeline.Model.Periods.Where(p => p.Section == section.Identity).Select((p, i) => (p, i)))
            {
                yield return new(p.Identity, "  Period " + (index + 1) + " · " + p.Label);
                foreach (var (evt, i) in p.Events.Select((e, i) => (e, i))) yield return new(evt.Identity, "    Event " + (i + 1) + " · " + evt.Label);
            }
        }
        foreach (var key in timeline.Model.Protected) yield return new(key, "Protected · " + timeline.Text.Line(key).Content);
    }
    protected override string SourceIdentity(string identity) => identity.Split(':')[0];
    protected override void ShowProperties()
    {
        var title = timeline.Selection == timeline.Model.HeaderIdentity; var section = timeline.Model.Sections.FirstOrDefault(s => s.Identity == timeline.Selection); var p = Period; var evt = p?.Events.FirstOrDefault(e => e.Identity == timeline.Selection);
        label.Text = title ? timeline.Model.Title?.Value ?? "" : section?.Tokens is not null ? section.Name : evt?.Label ?? p?.Label ?? "";
        label.IsEnabled = title || section?.Tokens is not null || evt is not null || p is { Protected: false };
        var selected = (destination.SelectedItem as Choice)?.Identity; destination.ItemsSource = timeline.Model.Sections.Select(s => new Choice(s.Identity, s.Name)).ToArray();
        destination.SelectedItem = destination.Items.OfType<Choice>().FirstOrDefault(c => c.Identity == selected) ?? destination.Items.OfType<Choice>().FirstOrDefault(c => c.Identity != p?.Section); destination.IsEnabled = p is not null;
        Describe(title ? "Timeline title" : evt is not null ? "Event · " + evt.Label : p is not null ? "Period · " + p.Label : section?.Name ?? "Select a source occurrence",
            timeline.Model.Errors.FirstOrDefault() ?? "Period labels are authored text, without date sorting. Colons separate events and cannot be escaped in a new label. Moves carry complete continuation groups and proved leading comments. Empty sections and unknown source remain inspectable; ambiguous structure is protected.");
    }
    protected override Task<SourceAuthoringResult> UpdateProperties() => timeline.Selection == timeline.Model.HeaderIdentity ? timeline.SetTitleAsync(label.Text, Lifetime.Token) : timeline.UpdateAsync(timeline.Selection ?? "", label.Text, Lifetime.Token);
    protected override Task<SourceAuthoringResult> DeleteSelection() => timeline.DeleteAsync(timeline.Selection ?? "", Lifetime.Token);
    protected override Task<SourceAuthoringResult> MoveSelection(int direction) => timeline.MoveAsync(timeline.Selection ?? "", direction, Lifetime.Token);
    protected override IReadOnlyList<NativeSourceHit> Correspondence(string source, DiagramResult render)
    {
        if (source != timeline.Source) return []; TimelineVisualDraft.VerifyCanonical(timeline.Model, render.Svg!);
        var root = XDocument.Parse(render.Svg!).Root!; var periods = SourceDeclarations.Groups(render.Svg!, "timeline-period"); var events = SourceDeclarations.Groups(render.Svg!, "timeline-event"); var expected = timeline.Model.Periods.SelectMany(p => p.Events).ToArray(); var result = new List<NativeSourceHit>();
        for (var i = 0; i < periods.Length; i++) if (NativeSourceHit.ShapeBounds(periods[i], root) is { } bounds) result.Add(new(timeline.Model.Periods[i].Identity, "Period " + (i + 1) + " · " + timeline.Model.Periods[i].Label, bounds));
        for (var i = 0; i < events.Length; i++) if (NativeSourceHit.ShapeBounds(events[i], root) is { } bounds) result.Add(new(expected[i].Identity, "Event " + (i + 1) + " · " + expected[i].Label, bounds));
        return result;
    }
}
