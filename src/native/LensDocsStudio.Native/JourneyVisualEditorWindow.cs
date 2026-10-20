using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using LensDocsStudio.Core.JourneyDiagrams;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public sealed class JourneyVisualEditorWindow : SourceVisualEditorWindow
{
    private readonly JourneyVisualDraft journey;
    private readonly TextBox label, score;
    private readonly ComboBox destination;
    protected override string Values => label is null ? "" : label.Text + '\u001f' + score.Text;
    private JourneyTaskSource? TaskRow => journey.Model.Tasks.FirstOrDefault(t => t.Identity == journey.Selection || t.Actors.Any(a => a.Identity == journey.Selection));
    public JourneyVisualEditorWindow(string source, DiagramService diagrams, Func<string, CancellationToken, Task<string?>> apply,
        bool dark = false, string diagramTheme = "Auto", Action? editSource = null, FontFamily? sourceFont = null, double sourceFontSize = 14)
        : base("Journey", diagrams, apply, dark, diagramTheme, editSource, sourceFont, sourceFontSize)
    {
        Title = "Mermaid Visual Editor / User Journey";
        FamilyHeading.Text = "USER JOURNEY";
        label = Field("Title, section, task or selected actor name", "Label"); score = Field("Task score (integer 1–5; original raw value)", "Score");
        destination = new ComboBox { Style = (System.Windows.Style)FindResource("Form.ComboBox") }; Identify(destination, "Destination", "Destination section for the selected task");
        InspectorFields.Children.Add(new Label { Content = "Destination section", Target = destination, Style = (System.Windows.Style)FindResource("Form.Label") }); InspectorFields.Children.Add(destination);
        journey = new(source, diagrams); journey.ValidatePresentation = (text, _, token) => ValidateNative(text, token);
        ActionButton("Move task to section", "MoveToSection", () => destination.SelectedItem is Choice choice && TaskRow is { } t ? journey.MoveToSectionAsync(t.Identity, choice.Identity, Lifetime.Token) : Task.FromResult(new SourceAuthoringResult(false, "Select a task and its destination section.")), InspectorFields);
        ActionButton("Add actor to this task", "AddActor", async () =>
        {
            if (TaskRow is not { } t) return new(false, "Select a task or one of its actors. Actor authoring is task local."); var result = await journey.AddActorAsync(t.Identity, "New actor", Lifetime.Token);
            if (result.Success) journey.Select(journey.Model.Tasks.Single(x => x.Identity == t.Identity).Actors[^1].Identity); return result;
        }, InspectorFields);
        ActionButton("Add task", "AddTask", async () =>
        {
            var section = journey.Model.Sections.FirstOrDefault(s => s.Identity == journey.Selection)?.Identity ?? TaskRow?.Section ?? journey.Model.Sections.Last().Identity;
            var keys = journey.Model.Tasks.Select(t => t.Identity).ToHashSet(); var result = await journey.AddTaskAsync(section, Lifetime.Token); if (result.Success) journey.Select(journey.Model.Tasks.Single(t => !keys.Contains(t.Identity)).Identity); return result;
        });
        ActionButton("Add section", "AddSection", async () => { var result = await journey.AddSectionAsync(Lifetime.Token); if (result.Success) journey.Select(journey.Model.Sections[^1].Identity); return result; }); Start(journey);
    }
    protected override IEnumerable<Choice> Choices()
    {
        yield return new(journey.Model.HeaderIdentity, "Diagram title · " + journey.Model.Title?.Value);
        foreach (var section in journey.Model.Sections)
        {
            yield return new(section.Identity, "Section · " + section.Name + (journey.Model.Tasks.Any(t => t.Section == section.Identity) ? "" : " · empty, source only"));
            foreach (var (t, index) in journey.Model.Tasks.Where(t => t.Section == section.Identity).Select((t, i) => (t, i)))
            {
                yield return new(t.Identity, "  Task " + (index + 1) + " · " + t.Label + " · raw score " + t.Score + " · effective " + t.EffectiveScore);
                foreach (var (actor, i) in t.Actors.Select((a, i) => (a, i))) yield return new(actor.Identity, "    Actor " + (i + 1) + " · " + actor.Name + " · task " + t.Label);
            }
        }
        foreach (var key in journey.Model.Protected) yield return new(key, "Protected · " + journey.Text.Line(key).Content);
    }
    protected override string SourceIdentity(string identity) => identity.Split(':')[0];
    protected override void ShowProperties()
    {
        var title = journey.Selection == journey.Model.HeaderIdentity; var section = journey.Model.Sections.FirstOrDefault(s => s.Identity == journey.Selection); var t = TaskRow; var actor = t?.Actors.FirstOrDefault(a => a.Identity == journey.Selection);
        label.Text = title ? journey.Model.Title?.Value ?? "" : section?.Tokens is not null ? section.Name : actor?.Name ?? t?.Label ?? ""; label.IsEnabled = title || section?.Tokens is not null || t is not null;
        score.Text = t?.Score ?? ""; score.IsEnabled = t is not null && actor is null;
        var selected = (destination.SelectedItem as Choice)?.Identity; destination.ItemsSource = journey.Model.Sections.Select(s => new Choice(s.Identity, s.Name)).ToArray();
        destination.SelectedItem = destination.Items.OfType<Choice>().FirstOrDefault(c => c.Identity == selected) ?? destination.Items.OfType<Choice>().FirstOrDefault(c => c.Identity != t?.Section); destination.IsEnabled = t is not null;
        Describe(title ? "User Journey title" : actor is not null ? "Actor · " + actor.Name + " · selected task only" : t?.Label ?? section?.Name ?? "Select a source occurrence",
            journey.Model.Errors.FirstOrDefault() ?? (t is null ? "Empty sections and ignored source remain inspectable. Structural changes cannot cross unproved source. Select a task or one of its actor occurrences to author that task's actors." :
            "Raw score " + t.Score + "; effective renderer score " + t.EffectiveScore + ". Unchanged raw values and actor whitespace remain exact. Explicit score corrections require integers 1–5. Actor edits affect this task only; commas separate actors. Move up/down and Remove act on the selected task or actor occurrence."));
    }
    protected override Task<SourceAuthoringResult> UpdateProperties()
    {
        if (journey.Selection == journey.Model.HeaderIdentity) return journey.SetTitleAsync(label.Text, Lifetime.Token);
        if (journey.Model.Sections.Any(s => s.Identity == journey.Selection)) return journey.SetSectionAsync(journey.Selection!, label.Text, Lifetime.Token);
        if (TaskRow is { } t && t.Actors.Any(a => a.Identity == journey.Selection)) return journey.UpdateActorAsync(t.Identity, journey.Selection!, label.Text, Lifetime.Token);
        return journey.UpdateAsync(journey.Selection ?? "", label.Text, score.Text, Lifetime.Token);
    }
    protected override Task<SourceAuthoringResult> DeleteSelection() => journey.DeleteAsync(journey.Selection ?? "", Lifetime.Token);
    protected override Task<SourceAuthoringResult> MoveSelection(int direction) => journey.MoveAsync(journey.Selection ?? "", direction, Lifetime.Token);
    protected override IReadOnlyList<NativeSourceHit> Correspondence(string source, DiagramResult render)
    {
        if (source != journey.Source) return []; JourneyVisualDraft.VerifyCanonical(journey.Model, render.Svg!);
        var root = XDocument.Parse(render.Svg!).Root!; var groups = SourceDeclarations.Groups(render.Svg!, "journey-task"); var result = new List<NativeSourceHit>();
        for (var i = 0; i < groups.Length; i++) if (NativeSourceHit.ShapeBounds(groups[i], root) is { } bounds) result.Add(new(journey.Model.Tasks[i].Identity, "Task " + (i + 1) + " · " + journey.Model.Tasks[i].Label, bounds));
        return result;
    }
}
