using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;

namespace LensDocsStudio.Core.GanttDiagrams;

public sealed record GanttSourceTask(string Identity, string Label, string? Section, string? Id, string? Start, string? Duration, Match Tokens, bool Simple, string Protection);
public sealed record GanttSourceSection(string Identity, string Name, Match Tokens);
public sealed record GanttSourceModel(string HeaderIdentity, string Title, string? TitleIdentity, Match? TitleTokens,
    IReadOnlyList<GanttSourceSection> Sections, IReadOnlyList<GanttSourceTask> Tasks, IReadOnlyList<string> Directives, IReadOnlyList<string> Errors, bool TemporalProtection);

public sealed class GanttVisualDraft : SourceDraft<GanttSourceModel>
{
    public const string Starter = "gantt\n    title Documentation plan\n    dateFormat YYYY-MM-DD\n    section Draft\n    Write guide : guide, 2026-10-20, 3d\n    Review guide : review, 2026-10-23, 2d\n    section Publish\n    Prepare pack : pack, 2026-10-26, 1d\n";
    private static readonly Regex TaskLine = SourceText.Pattern(@"^[ \t]*(?<label>.+?)[ \t]*:[ \t]*(?<meta>.*?)[ \t]*$");
    private static readonly Regex SimpleTask = SourceText.Pattern(@"^[ \t]*(?<label>.+?)[ \t]*:[ \t]*(?:(?<id>[^,\s]+)[ \t]*,[ \t]*)?(?<date>\d{4}-\d{2}-\d{2})[ \t]*,[ \t]*(?<duration>[0-9]+)d[ \t]*$");
    private static readonly Regex Section = SourceText.Pattern(@"^[ \t]*section[ \t]+(?<label>.+?)[ \t]*$", RegexOptions.IgnoreCase);
    private static readonly Regex Title = SourceText.Pattern(@"^[ \t]*title[ \t]+(?<label>.+?)[ \t]*$", RegexOptions.IgnoreCase);
    private static readonly Regex Header = SourceText.Pattern(@"^[ \t]*gantt(?:[ \t]+title[ \t]+(?<label>.+?))?[ \t]*$", RegexOptions.IgnoreCase);
    private static readonly Regex Format = SourceText.Pattern(@"^dateFormat\s+(?<format>.+)$", RegexOptions.IgnoreCase);
    public static bool IsGantt(string source) => SourceText.Pattern(@"^gantt(?:\s|$)", RegexOptions.IgnoreCase).IsMatch(SourceText.Header(source));
    public GanttVisualDraft(string source, DiagramService? diagrams = null) : base(source, diagrams) { }
    protected override GanttSourceModel Scan(SourceText text)
    {
        string header = "", title = "", format = "YYYY-MM-DD"; string? titleKey = null, section = null; Match? titleTokens = null;
        var sections = new List<GanttSourceSection>(); var tasks = new List<GanttSourceTask>(); var directives = new List<string>(); var errors = new List<string>(); var temporal = false;
        foreach (var line in text.Lines)
        {
            if (line.Content.Length == 0 || line.Comment) continue;
            if (header.Length == 0)
            {
                header = line.Key; var match = Header.Match(line.Body); if (!match.Success) errors.Add("Unsupported Gantt header. Use gantt and an optional title.");
                if (match.Groups["label"].Success) { title = match.Groups["label"].Value; titleKey = line.Key; titleTokens = match; } continue;
            }
            var named = Title.Match(line.Body); if (named.Success) { title = named.Groups["label"].Value; titleKey = line.Key; titleTokens = named; continue; }
            var group = Section.Match(line.Body); if (group.Success) { section = line.Key; sections.Add(new(section, group.Groups["label"].Value, group)); continue; }
            var dateFormat = Format.Match(line.Content);
            if (dateFormat.Success) { format = dateFormat.Groups["format"].Value.Trim(); directives.Add(line.Key); if (!CompatibleFormat(format)) temporal = true; continue; }
            if (SourceText.Pattern(@"^(?:excludes|includes|weekend|weekday|axisFormat|tickInterval|todayMarker|click|accTitle|accDescr)\b", RegexOptions.IgnoreCase).IsMatch(line.Content))
            { temporal = true; directives.Add(line.Key); continue; }
            var broad = TaskLine.Match(line.Body);
            if (!broad.Success) { directives.Add(line.Key); temporal = true; continue; }
            var simple = SimpleTask.Match(line.Body); var supported = simple.Success && CompatibleFormat(format);
            var meta = broad.Groups["meta"].Value; var tokens = meta.Split(',').Select(value => value.Trim()).ToArray();
            // The pinned parser consumes status tags before its optional ID. Keep those tokens read-only.
            var first = tokens.SkipWhile(value => value.ToLowerInvariant() is "done" or "active" or "crit" or "milestone").FirstOrDefault();
            var id = simple.Success ? simple.Groups["id"].Success ? simple.Groups["id"].Value : null
                : first is not null && !SourceText.Pattern(@"^(?:after\b|[0-9]+(?:\.[0-9]+)?(?:ms|[smhdwyM])$|\d{4}[-/])", RegexOptions.IgnoreCase).IsMatch(first) ? first : null;
            if (simple.Success && simple.Groups["id"].Value.ToLowerInvariant() is "done" or "active" or "crit" or "milestone") supported = false;
            var date = simple.Success ? simple.Groups["date"].Value : null; var duration = simple.Success ? simple.Groups["duration"].Value : null;
            if (supported && (!ValidDate(date!) || !ValidDuration(duration!))) errors.Add("A Gantt task contains an impossible ISO date or invalid whole-day duration. Repair the task fields or source; no implicit scheduling is accepted as a substitute.");
            var reason = supported ? "Explicit ISO start and positive whole-day duration." : !CompatibleFormat(format) ? "The active dateFormat is incompatible with ISO fields. Existing dates and scheduling tokens remain protected." : "Dependencies, implicit starts, statuses, milestones or alternate scheduling tokens are preserved; only the task label is editable.";
            tasks.Add(new(line.Key, broad.Groups["label"].Value.TrimEnd(), section, id, date, duration, simple.Success ? simple : broad, supported, reason));
        }
        if (header.Length == 0) errors.Add("A Gantt header is required.");
        if (tasks.Count + sections.Count > 500) errors.Add("Gantt exceeds the 500-element rendering budget.");
        if (tasks.Where(task => task.Id is not null).GroupBy(task => task.Id!, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1)) temporal = true;
        return new(header, title, titleKey, titleTokens, sections, tasks, directives, errors, temporal);
    }
    public static bool CompatibleFormat(string format) => format is "YYYY-MM-DD" or "yyyy-MM-dd";
    public static bool ValidDate(string value) => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    public static bool ValidDuration(string value) => SourceText.Pattern(@"^[0-9]+$").IsMatch(value) && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var duration) && duration is > 0 and <= 73000;
    protected override bool Contains(GanttSourceModel model, string identity) => identity == model.HeaderIdentity || model.Tasks.Any(task => task.Identity == identity) || model.Sections.Any(section => section.Identity == identity) || model.Directives.Contains(identity);
    protected override void Validate(SourceText text, GanttSourceModel model, DiagramResult rendered)
    {
        if (model.Errors.Count > 0) throw new ArgumentException(model.Errors[0]);
        if (!rendered.Success || rendered.Source != text.Source) throw new ArgumentException(rendered.Error ?? "Gantt rendering does not match current source.");
        var root = XDocument.Parse(rendered.Svg!); var groups = root.Descendants().Where(element => (string?)element.Attribute("class") == "gantt-section").ToArray();
        var expected = model.Tasks.GroupBy(task => task.Section).ToArray();
        if (groups.Length != expected.Length && model.Tasks.Count > 0) throw new ArgumentException("Canonical Gantt section count does not match all source tasks.");
        for (var section = 0; section < expected.Length; section++)
        {
            var group = groups[section]; var name = expected[section].Key is { } key ? model.Sections.Single(item => item.Identity == key).Name : "";
            if (((string?)group.Attribute("data-label") ?? "") != name) throw new ArgumentException("Canonical Gantt section names do not match source occurrences.");
            var rows = group.Elements().Where(element => (string?)element.Attribute("class") == "gantt-task").ToArray(); var tasks = expected[section].ToArray();
            if (rows.Length != tasks.Length) throw new ArgumentException("Canonical Gantt omitted a source task. Apply is protected.");
            for (var index = 0; index < rows.Length; index++)
                if (string.Concat(rows[index].Descendants().Where(element => element.Name.LocalName == "text").Select(element => element.Value)) != tasks[index].Label)
                    throw new ArgumentException("Canonical Gantt task labels/order do not match source occurrences. Apply is protected.");
        }
    }
    public Task<SourceAuthoringResult> SetTitleAsync(string value, CancellationToken token = default) => ChangeAsync((text, model) =>
    { SourceText.Label(value); return model.TitleIdentity is { } key ? text.Patch(key, model.TitleTokens!.Groups["label"], value) : text.Insert(text.Index(model.HeaderIdentity) + 1, "    title " + value); }, token);
    public Task<SourceAuthoringResult> SetSectionAsync(string identity, string value, CancellationToken token = default) => ChangeAsync((text, model) =>
    { SourceText.Label(value); var section = model.Sections.Single(item => item.Identity == identity); return text.Patch(identity, section.Tokens.Groups["label"], value); }, token);
    public Task<SourceAuthoringResult> UpdateAsync(string identity, string label, string? date = null, string? duration = null, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        TaskLabel(label); var task = Task(model, identity); var line = text.Line(identity);
        if (date != task.Start || duration != task.Duration)
        {
            var reason = TemporalReason(model, task); if (reason is not null) throw new ArgumentException(reason);
            if (!ValidDate(date ?? "")) throw new ArgumentException("Enter a complete valid ISO date (yyyy-MM-dd), including a real calendar day.");
            if (!ValidDuration(duration ?? "")) throw new ArgumentException("Enter a positive whole-day duration from 1 to 73000. Incomplete, fractional and negative durations are unsupported.");
            if (duration != task.Duration) line = line.Replace(task.Tokens.Groups["duration"].Index, task.Tokens.Groups["duration"].Length, duration!);
            if (date != task.Start) line = line.Replace(task.Tokens.Groups["date"].Index, task.Tokens.Groups["date"].Length, date!);
        }
        if (label != task.Label) line = line.Replace(task.Tokens.Groups["label"].Index, task.Tokens.Groups["label"].Length, label);
        var changed = text.Replace(line); if (Scan(changed).Tasks.SingleOrDefault(item => item.Identity == identity)?.Label != label) throw new ArgumentException("This label would become a directive instead of a task. Choose a task label without directive syntax."); return changed;
    }, token);
    public static string? TemporalReason(GanttSourceModel model, GanttSourceTask task)
    {
        if (!task.Simple) return task.Protection;
        if (model.TemporalProtection) return "Calendar rules, incompatible dateFormat, duplicate IDs or unknown directives protect temporal and structural operations. Task/section labels remain editable.";
        if (model.Tasks.Any(item => !item.Simple)) return "Dependencies or implicit scheduling are present. Temporal and structural operations are protected to avoid changing referenced or default scheduling; local labels remain editable.";
        return null;
    }
    private static void Structure(GanttSourceModel model)
    { if (model.TemporalProtection || model.Tasks.Any(task => !task.Simple)) throw new ArgumentException("Dependencies, implicit starts, calendar rules, incompatible formats or unknown directives protect this structural operation. Local labels remain editable."); }
    public Task<SourceAuthoringResult> AddTaskAsync(string? section, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        Structure(model); if (section is not null && !model.Sections.Any(item => item.Identity == section)) throw new ArgumentException("Select a section.");
        var existing = model.Tasks.Where(task => task.Section == section).ToArray(); var last = existing.LastOrDefault();
        var index = last is not null ? text.Index(last.Identity) + 1 : text.Index(section ?? model.HeaderIdentity) + 1;
        // Explicit dates make insertion independent of preceding scheduling. IDs are unnecessary for this form.
        return text.Insert(index, (last is not null ? text.Line(last.Identity).Indent : "    ") + "New task : 2026-10-20, 1d");
    }, token);
    public Task<SourceAuthoringResult> AddSectionAsync(CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        Structure(model); var index = model.Tasks.LastOrDefault() is { } task ? text.Index(task.Identity) + 1 : text.Lines.Count;
        // Empty sections are retained in source, but omitted canonically by the pinned renderer.
        return text.Insert(index, "    section New section");
    }, token);
    public Task<SourceAuthoringResult> DeleteAsync(string identity, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        Structure(model); var start = text.AttachedStart(identity);
        if (model.Tasks.Any(task => task.Identity == identity)) { SourceRowMoves.CheckTrailingAttachment(text, identity); return text.Splice(start, text.Index(identity) + 1 - start, []); }
        var section = model.Sections.SingleOrDefault(item => item.Identity == identity) ?? throw new ArgumentException("Select a task or section to remove.");
        var rows = model.Tasks.Where(task => task.Section == section.Identity).ToArray(); var end = rows.Length == 0 ? text.Index(identity) + 1 : text.Index(rows[^1].Identity) + 1;
        SourceRowMoves.CheckTrailingAttachment(text, text.Lines[end - 1].Key); ProtectGroup(text, model, start, end); return text.Splice(start, end - start, []);
    }, token);
    public Task<SourceAuthoringResult> MoveAsync(string identity, int direction, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        Structure(model);
        if (model.Tasks.FirstOrDefault(task => task.Identity == identity) is { } task)
        {
            var tasks = model.Tasks.Where(item => item.Section == task.Section).ToArray(); var index = Array.IndexOf(tasks, task); var other = index + Math.Sign(direction);
            if (other < 0 || other >= tasks.Length) throw new ArgumentException("Move tasks within their own section; there is no task in that direction.");
            return SourceRowMoves.Swap(text, tasks[Math.Min(index, other)].Identity, tasks[Math.Max(index, other)].Identity);
        }
        var section = model.Sections.SingleOrDefault(item => item.Identity == identity) ?? throw new ArgumentException("Select a task or section.");
        var sections = model.Sections.ToArray(); var at = Array.IndexOf(sections, section); var next = at + Math.Sign(direction);
        if (next < 0 || next >= sections.Length) throw new ArgumentException("There is no section in that direction.");
        var first = sections[Math.Min(at, next)]; var second = sections[Math.Max(at, next)];
        int End(string key) => model.Tasks.LastOrDefault(task => task.Section == key) is { } last ? text.Index(last.Identity) + 1 : text.Index(key) + 1;
        ProtectGroup(text, model, text.AttachedStart(first.Identity), End(second.Identity));
        return SourceRowMoves.Swap(text, first.Identity, second.Identity, End(first.Identity), End(second.Identity));
    }, token);
    private static void ProtectGroup(SourceText text, GanttSourceModel model, int start, int end)
    {
        var known = model.Tasks.Select(task => task.Identity).Concat(model.Sections.Select(section => section.Identity)).ToHashSet();
        for (var index = start; index < end; index++)
        {
            var line = text.Lines[index]; if (known.Contains(line.Key) || line.Content.Length == 0) continue;
            if (!line.Comment || index < text.AttachedStart(text.Lines.Skip(index + 1).FirstOrDefault(item => !item.Comment && item.Content.Length > 0)?.Key ?? line.Key))
                throw new ArgumentException("An intervening directive or ambiguously attached comment protects this section group.");
        }
    }
    private static GanttSourceTask Task(GanttSourceModel model, string identity) => model.Tasks.SingleOrDefault(task => task.Identity == identity) ?? throw new ArgumentException("Select an existing task.");
    private static void TaskLabel(string value) { SourceText.Label(value); if (value.Contains(':')) throw new ArgumentException("Gantt task labels cannot contain a colon in the pinned grammar. The pending label is retained."); }
}
