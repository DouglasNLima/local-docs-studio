using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using LensDocsStudio.Core.GanttDiagrams;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public sealed class GanttVisualEditorWindow : SourceVisualEditorWindow
{
    private readonly GanttVisualDraft gantt;
    private readonly TextBox label, duration;
    private readonly DatePicker date;
    private string originalDateText = "";
    protected override string Values => label is null ? "" : label.Text + '\u001f' + date.Text + '\u001f' + duration.Text;
    public GanttVisualEditorWindow(string source, DiagramService diagrams, Func<string, CancellationToken, Task<string?>> apply,
        bool dark = false, string diagramTheme = "Auto", Action? editSource = null, FontFamily? sourceFont = null, double sourceFontSize = 14)
        : base("Gantt", diagrams, apply, dark, diagramTheme, editSource, sourceFont, sourceFontSize)
    {
        label = Field("Label or title", "Label");
        date = new DatePicker { SelectedDateFormat = DatePickerFormat.Short, FirstDayOfWeek = DayOfWeek.Monday }; Identify(date, "StartDate", "Explicit task start date");
        InspectorFields.Children.Add(new Label { Content = "Start date", Target = date, Style = (Style)FindResource("Form.Label") }); InspectorFields.Children.Add(date);
        date.SelectedDateChanged += (_, _) => FieldsChanged(); date.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, _) => FieldsChanged()));
        // Keep incomplete native DatePicker text for correction instead of throwing or replacing it.
        date.DateValidationError += (_, args) => { args.ThrowException = false; };
        duration = Field("Duration (whole days)", "Duration"); gantt = new(source, diagrams); gantt.ValidatePresentation = (text, _, token) => ValidateNative(text, token);
        ActionButton("Add task", "AddTask", AddTask); ActionButton("Add section", "AddSection", async () =>
        { var result = await gantt.AddSectionAsync(Lifetime.Token); if (result.Success) gantt.Select(gantt.Model.Sections[^1].Identity); return result; }); Start(gantt);
    }
    private async Task<SourceAuthoringResult> AddTask()
    {
        var section = gantt.Model.Sections.FirstOrDefault(item => item.Identity == gantt.Selection)?.Identity ?? gantt.Model.Tasks.FirstOrDefault(item => item.Identity == gantt.Selection)?.Section ?? gantt.Model.Sections.LastOrDefault()?.Identity;
        var keys = gantt.Model.Tasks.Select(task => task.Identity).ToHashSet(); var result = await gantt.AddTaskAsync(section, Lifetime.Token);
        if (result.Success) gantt.Select(gantt.Model.Tasks.Single(task => !keys.Contains(task.Identity)).Identity); return result;
    }
    protected override IEnumerable<Choice> Choices()
    {
        yield return new(gantt.Model.HeaderIdentity, "Diagram title · " + gantt.Model.Title);
        foreach (var line in gantt.Text.Lines)
        {
            if (gantt.Model.Sections.FirstOrDefault(item => item.Identity == line.Key) is { } section) yield return new(section.Identity, "Section · " + section.Name);
            if (gantt.Model.Tasks.FirstOrDefault(item => item.Identity == line.Key) is { } task) yield return new(task.Identity, "  Task · " + task.Label + (task.Simple ? " · " + task.Start + " · " + task.Duration + " days" : " · protected schedule"));
            if (gantt.Model.Directives.Contains(line.Key)) yield return new(line.Key, "Preserved directive · " + line.Content);
        }
    }
    protected override void ShowProperties()
    {
        var task = gantt.Model.Tasks.FirstOrDefault(item => item.Identity == gantt.Selection); var section = gantt.Model.Sections.FirstOrDefault(item => item.Identity == gantt.Selection); var title = gantt.Selection == gantt.Model.HeaderIdentity;
        label.Text = title ? gantt.Model.Title : section?.Name ?? task?.Label ?? ""; label.IsEnabled = title || section is not null || task is not null;
        date.SelectedDate = task?.Start is { } start && DateTime.TryParseExact(start, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed : null;
        if (task?.Start is { } invalid && !GanttVisualDraft.ValidDate(invalid)) date.Text = invalid;
        originalDateText = date.Text; duration.Text = task?.Duration ?? "";
        var protection = task is null ? null : GanttVisualDraft.TemporalReason(gantt.Model, task);
        date.IsEnabled = duration.IsEnabled = task is not null && protection is null;
        date.ToolTip = duration.ToolTip = protection ?? "Explicit ISO date and positive whole-day duration; source uses invariant yyyy-MM-dd and Nd.";
        System.Windows.Automation.AutomationProperties.SetHelpText(date, date.ToolTip.ToString()!); System.Windows.Automation.AutomationProperties.SetHelpText(duration, duration.ToolTip.ToString()!);
        Describe(title ? "Gantt title" : section is not null ? "Section · " + section.Name : task is not null ? "Task · " + task.Label : "Select a task, section or title",
            gantt.Model.Errors.FirstOrDefault() ?? protection ?? (task?.Protection ?? "Title/section names and task labels are source-span edits. IDs stay unchanged. Structural changes require explicit independent tasks and cannot cross directives or ambiguous comments. Empty sections remain in source until a task is added; the pinned renderer omits them."));
    }
    protected override Task<SourceAuthoringResult> UpdateProperties()
    {
        if (gantt.Selection == gantt.Model.HeaderIdentity) return gantt.SetTitleAsync(label.Text, Lifetime.Token);
        if (gantt.Model.Sections.Any(item => item.Identity == gantt.Selection)) return gantt.SetSectionAsync(gantt.Selection!, label.Text, Lifetime.Token);
        var task = gantt.Model.Tasks.FirstOrDefault(item => item.Identity == gantt.Selection);
        if (task is null) return Task.FromResult(new SourceAuthoringResult(false, "Select an editable task, section or title."));
        string? invariant = task.Start;
        if (date.IsEnabled && date.Text != originalDateText)
        {
            if (!DateTime.TryParse(date.Text, CultureInfo.CurrentCulture, DateTimeStyles.None, out var parsed)) return Task.FromResult(new SourceAuthoringResult(false, "Enter a complete real calendar date or select one from the calendar. The pending date is retained."));
            invariant = parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        return gantt.UpdateAsync(task.Identity, label.Text, invariant, task.Simple ? duration.Text : task.Duration, Lifetime.Token);
    }
    protected override Task<SourceAuthoringResult> DeleteSelection() => gantt.DeleteAsync(gantt.Selection ?? "", Lifetime.Token);
    protected override Task<SourceAuthoringResult> MoveSelection(int direction) => gantt.MoveAsync(gantt.Selection ?? "", direction, Lifetime.Token);
    protected override IReadOnlyList<NativeSourceHit> Correspondence(string source, DiagramResult render)
    {
        var root = XDocument.Parse(render.Svg!).Root!; var groups = root.Descendants().Where(element => (string?)element.Attribute("class") == "gantt-task").ToArray(); var result = new List<NativeSourceHit>();
        if (source != gantt.Source || groups.Length != gantt.Model.Tasks.Count) return result;
        for (var index = 0; index < groups.Length; index++)
        {
            var task = gantt.Model.Tasks[index];
            if (string.Concat(groups[index].Descendants().Where(element => element.Name.LocalName == "text").Select(element => element.Value)) != task.Label) throw new ArgumentException("Canonical Gantt task occurrence does not match source order.");
            if (NativeSourceHit.ShapeBounds(groups[index], root) is { } bounds) result.Add(new(task.Identity, "Task · " + task.Label, bounds));
        }
        return result;
    }
}
