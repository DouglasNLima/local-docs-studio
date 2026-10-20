using System.Globalization;
using System.Text.RegularExpressions;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;

namespace LensDocsStudio.Core.JourneyDiagrams;

public sealed record JourneyActorSource(string Identity, string Name, int Start, int Length, int SegmentStart, int SegmentLength);
public sealed record JourneyTaskSource(string Identity, string Section, string Label, string Score, int EffectiveScore, Match Tokens, IReadOnlyList<JourneyActorSource> Actors);
public sealed record JourneySourceModel(string HeaderIdentity, SourceDeclaration? Title, IReadOnlyList<SourceSection> Sections,
    IReadOnlyList<JourneyTaskSource> Tasks, IReadOnlyList<string> Protected, IReadOnlyList<string> Errors);

public sealed class JourneyVisualDraft : SourceDraft<JourneySourceModel>
{
    public const string Starter = "journey\n    title Documentation experience\n    section Prepare\n    Write guide: 3: Author, Reviewer\n    Review examples: 4: Reviewer\n    section Publish\n    Read documentation: 5: Reader\n";
    private static readonly Regex Title = SourceText.Pattern(@"^[ \t]*title[ \t]+(?<label>.+?)[ \t]*$", RegexOptions.IgnoreCase);
    private static readonly Regex Section = SourceText.Pattern(@"^[ \t]*section[ \t]+(?<label>.+?)[ \t]*$", RegexOptions.IgnoreCase);
    private static readonly Regex TaskLine = SourceText.Pattern(@"^[ \t]*(?<label>.+?)[ \t]*:[ \t]*(?<score>\d+)[ \t]*(?::[ \t]*(?<actors>.*))?$" );
    public static bool IsJourney(string source) => SourceText.Pattern(@"^journey(?:\s|$)", RegexOptions.IgnoreCase).IsMatch(SourceText.Header(source));
    public JourneyVisualDraft(string source, DiagramService? diagrams = null) : base(source, diagrams) { }
    protected override JourneySourceModel Scan(SourceText text)
    {
        var sections = new List<SourceSection>(); var tasks = new List<JourneyTaskSource>(); var protect = new List<string>(); var errors = new List<string>();
        string header = "", section = ""; SourceDeclaration? title = null;
        foreach (var line in text.Lines)
        {
            if (line.Content.Length == 0 || line.Comment) continue;
            if (line.InitDirective) { protect.Add(line.Key); continue; }
            if (header.Length == 0)
            {
                header = line.Key; var match = SourceDeclarations.Header(line, "journey", false);
                if (!match.Success) errors.Add("Unsupported User Journey header. Inspect original source.");
                else if (match.Groups["label"].Success) title = new(line.Key, "title", match.Groups["label"].Value, match);
                section = header + ":unsectioned"; sections.Add(new(section, "Unsectioned", null)); continue;
            }
            var t = Title.Match(line.Body); if (t.Success) { title = new(line.Key, "title", t.Groups["label"].Value, t); continue; }
            var s = Section.Match(line.Body); if (s.Success) { section = line.Key; sections.Add(new(section, s.Groups["label"].Value, s)); continue; }
            var task = TaskLine.Match(line.Body);
            if (task.Success && task.Groups["label"].Value.Trim().Length > 0 && int.TryParse(task.Groups["score"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var score))
                tasks.Add(new(line.Key, section, task.Groups["label"].Value.Trim(), task.Groups["score"].Value, Math.Clamp(score, 1, 5), task, Actors(line.Key, task.Groups["actors"])));
            else protect.Add(line.Key);
        }
        if (header.Length == 0) errors.Add("A User Journey header is required.");
        if (tasks.Count > 500 || tasks.Sum(t => t.Actors.Count) > 1000) errors.Add("User Journey exceeds the bounded task/actor budget.");
        return new(header, title, sections, tasks, protect, errors);
    }
    private static JourneyActorSource[] Actors(string key, Group group)
    {
        var result = new List<JourneyActorSource>(); var start = group.Index;
        foreach (var (segment, index) in group.Value.Split(',').Select((s, i) => (s, i)))
        {
            var name = segment.Trim(); var pad = segment.Length - segment.TrimStart().Length;
            if (name.Length > 0) result.Add(new(key + ":actor:" + index, name, start + pad, name.Length, start, segment.Length));
            start += segment.Length + 1;
        }
        return result.ToArray();
    }
    protected override bool Contains(JourneySourceModel model, string identity) => identity == model.HeaderIdentity || model.Sections.Any(s => s.Identity == identity) || model.Tasks.Any(t => t.Identity == identity || t.Actors.Any(a => a.Identity == identity)) || model.Protected.Contains(identity);
    protected override void Validate(SourceText text, JourneySourceModel model, DiagramResult rendered)
    {
        if (model.Errors.Count > 0) throw new ArgumentException(model.Errors[0]);
        if (!rendered.Success || rendered.Source != text.Source) throw new ArgumentException(rendered.Error ?? "User Journey did not render current source.");
        VerifyCanonical(model, rendered.Svg!);
    }
    public static void VerifyCanonical(JourneySourceModel model, string svg)
    {
        var tasks = SourceDeclarations.Groups(svg, "journey-task");
        if (tasks.Length != model.Tasks.Count || tasks.Where((t, i) => (string?)t.Attribute("data-label") != model.Tasks[i].Label || (string?)t.Attribute("data-score") != model.Tasks[i].EffectiveScore.ToString(CultureInfo.InvariantCulture)).Any())
            throw new ArgumentException("Canonical Journey task counts, order, labels or effective scores differ from source occurrences. Apply is protected.");
        var actors = SourceDeclarations.Groups(svg, "actor-dot").Select(e => e.Elements().Single(t => t.Name.LocalName == "title").Value);
        if (!actors.SequenceEqual(model.Tasks.SelectMany(t => t.Actors).Select(a => a.Name))) throw new ArgumentException("Canonical actor order or task-local actor occurrences differ from source. Apply is protected.");
        var groups = SourceDeclarations.Groups(svg, "subgraph"); var expected = model.Sections.Where(s => model.Tasks.Any(t => t.Section == s.Identity)).ToArray();
        if (groups.Length != expected.Length || groups.Where((g, i) => (string?)g.Attribute("data-section") != i.ToString(CultureInfo.InvariantCulture)).Any()) throw new ArgumentException("Canonical Journey source sections differ.");
    }
    public Task<SourceAuthoringResult> SetTitleAsync(string value, CancellationToken token = default) => ChangeAsync((text, model) => SourceDeclarations.SetTitle(text, model.HeaderIdentity, model.Title, value), token);
    public Task<SourceAuthoringResult> SetSectionAsync(string identity, string label, CancellationToken token = default) => ChangeAsync((text, model) =>
    { SourceText.Label(label); var section = model.Sections.Single(s => s.Identity == identity && s.Tokens is not null); return text.Patch(identity, section.Tokens!.Groups["label"], label); }, token);
    public Task<SourceAuthoringResult> UpdateAsync(string identity, string label, string score, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        var task = model.Tasks.Single(t => t.Identity == identity);
        if (label != task.Label) { SourceDeclarations.PlainLabel(label, ":"); SourceDeclarations.RejectDirectiveName(label, "title|section"); }
        if (score != task.Score && (!SourceText.Pattern(@"^[1-5]$").IsMatch(score))) throw new ArgumentException("Enter a complete integer score from 1 to 5. The original raw score is preserved until explicitly corrected.");
        var line = text.Line(identity); if (score != task.Score) line = line.Replace(task.Tokens.Groups["score"].Index, task.Tokens.Groups["score"].Length, score);
        if (label != task.Label) line = line.Replace(task.Tokens.Groups["label"].Index, task.Tokens.Groups["label"].Length, label);
        return text.Replace(line);
    }, token);
    public Task<SourceAuthoringResult> AddSectionAsync(CancellationToken token = default) => ChangeAsync((text, _) => text.Insert(text.Lines.Count, "    section New section"), token);
    public Task<SourceAuthoringResult> AddTaskAsync(string sectionId, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        var section = model.Sections.Single(s => s.Identity == sectionId); var end = SourceSections.End(text, model.Sections, section);
        if (model.Protected.Any(k => text.Index(k) >= (section.Tokens is null ? text.Index(model.HeaderIdentity) + 1 : text.Index(section.Identity)) && text.Index(k) < end)) throw new ArgumentException("Unknown source in this section protects task insertion. Choose a supported section or Edit source.");
        return text.Insert(end, "    New task: 3");
    }, token);
    public Task<SourceAuthoringResult> AddActorAsync(string taskId, string name, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        SourceDeclarations.PlainLabel(name, ","); var task = model.Tasks.Single(t => t.Identity == taskId); var line = text.Line(taskId); var actors = task.Tokens.Groups["actors"];
        if (!actors.Success) return text.Replace(line.Replace(task.Tokens.Groups["score"].Index + task.Score.Length, 0, ": " + name));
        return text.Replace(line.Replace(actors.Index + actors.Length, 0, (task.Actors.Count > 0 ? ", " : "") + name));
    }, token);
    public Task<SourceAuthoringResult> UpdateActorAsync(string taskId, string actorId, string name, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        SourceDeclarations.PlainLabel(name, ","); var task = model.Tasks.Single(t => t.Identity == taskId); var actor = task.Actors.Single(a => a.Identity == actorId);
        return text.Replace(text.Line(taskId).Replace(actor.Start, actor.Length, name));
    }, token);
    private static IEnumerable<string> Supported(JourneySourceModel model) => model.Sections.Where(s => s.Tokens is not null).Select(s => s.Identity).Concat(model.Tasks.Select(t => t.Identity));
    public Task<SourceAuthoringResult> DeleteAsync(string identity, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        if (model.Sections.Any(s => s.Identity == identity)) return SourceSections.ChangeSection(text, model.Sections, identity, 0, true, Supported(model));
        if (model.Tasks.FirstOrDefault(t => t.Identity == identity) is { }) { SourceRowMoves.CheckTrailingAttachment(text, identity); var start = text.AttachedStart(identity); return text.Splice(start, text.Index(identity) + 1 - start, []); }
        var owner = model.Tasks.Single(t => t.Actors.Any(a => a.Identity == identity)); var actor = owner.Actors.Single(a => a.Identity == identity); var index = owner.Actors.ToList().IndexOf(actor);
        if (owner.Actors.Count == 1) return text.Replace(text.Line(owner.Identity).Replace(owner.Tokens.Groups["actors"].Index, owner.Tokens.Groups["actors"].Length, ""));
        var begin = index == 0 ? actor.SegmentStart : actor.SegmentStart - 1;
        return text.Replace(text.Line(owner.Identity).Replace(begin, actor.SegmentLength + 1, ""));
    }, token);
    public Task<SourceAuthoringResult> MoveAsync(string identity, int direction, CancellationToken token = default)
    {
        var owner = Model.Tasks.FirstOrDefault(t => t.Actors.Any(a => a.Identity == identity)); var actorIndex = owner is null ? -1 : owner.Actors.ToList().FindIndex(a => a.Identity == identity) + Math.Sign(direction);
        var selection = actorIndex >= 0 && actorIndex < (owner?.Actors.Count ?? 0) ? owner!.Actors[actorIndex].Identity : null;
        return ChangeAsync((text, model) =>
        {
            if (model.Sections.Any(s => s.Identity == identity)) return SourceSections.ChangeSection(text, model.Sections, identity, direction, false, Supported(model));
            if (model.Tasks.FirstOrDefault(t => t.Identity == identity) is { } task)
            {
                var tasks = model.Tasks.Where(t => t.Section == task.Section).ToArray(); var i = Array.IndexOf(tasks, task); var j = i + Math.Sign(direction);
                if (j < 0 || j >= tasks.Length) throw new ArgumentException("There is no task in that direction in this section. Use the destination selector to move between sections.");
                return SourceRowMoves.Swap(text, tasks[Math.Min(i, j)].Identity, tasks[Math.Max(i, j)].Identity);
            }
            var taskOwner = model.Tasks.Single(t => t.Actors.Any(a => a.Identity == identity)); var index = taskOwner.Actors.ToList().FindIndex(a => a.Identity == identity); var other = index + Math.Sign(direction);
            if (other < 0 || other >= taskOwner.Actors.Count) throw new ArgumentException("There is no actor in that direction for this task.");
            var a = taskOwner.Actors[Math.Min(index, other)]; var b = taskOwner.Actors[Math.Max(index, other)];
            return text.Replace(text.Line(taskOwner.Identity).Replace(b.Start, b.Length, a.Name).Replace(a.Start, a.Length, b.Name));
        }, token, selection);
    }
    public Task<SourceAuthoringResult> MoveToSectionAsync(string taskId, string destinationId, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        var task = model.Tasks.Single(t => t.Identity == taskId); var section = model.Sections.Single(s => s.Identity == destinationId);
        if (task.Section == destinationId) throw new ArgumentException("Choose a different destination section.");
        SourceRowMoves.CheckTrailingAttachment(text, taskId); var start = text.AttachedStart(taskId); var end = text.Index(taskId) + 1; var destination = SourceSections.End(text, model.Sections, section);
        SourceSections.Safe(text, Math.Min(start, destination), Math.Max(end, destination), Supported(model)); return SourceSections.MoveTo(text, start, end, destination);
    }, token);
}
