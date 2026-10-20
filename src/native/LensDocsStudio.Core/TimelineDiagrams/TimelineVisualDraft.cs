using System.Text.RegularExpressions;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;

namespace LensDocsStudio.Core.TimelineDiagrams;

public sealed record TimelineEventSource(string Identity, string LineIdentity, string Label, int Start, int Length, int SegmentStart, int SegmentLength);
public sealed record TimelinePeriodSource(string Identity, string Section, string Label, Match Tokens, int End, IReadOnlyList<TimelineEventSource> Events, bool Protected = false);
public sealed record TimelineSourceModel(string HeaderIdentity, SourceDeclaration? Title, IReadOnlyList<SourceSection> Sections,
    IReadOnlyList<TimelinePeriodSource> Periods, IReadOnlyList<string> Protected, IReadOnlyList<string> Errors);

public sealed class TimelineVisualDraft : SourceDraft<TimelineSourceModel>
{
    public const string Starter = "timeline\n    title Documentation milestones\n    section Draft\n    First review : Write guide : Review examples\n        : Check accessibility\n    section Publish\n    Ready : Export documentation\n";
    private static readonly Regex Title = SourceText.Pattern(@"^[ \t]*title[ \t]+(?<label>.+?)[ \t]*$");
    private static readonly Regex Section = SourceText.Pattern(@"^[ \t]*section[ \t]+(?<label>.+?)[ \t]*$");
    private static readonly Regex Period = SourceText.Pattern(@"^[ \t]*(?<label>.+?)[ \t]*:[ \t]*(?<events>.+?)[ \t]*$");
    private static readonly Regex Continuation = SourceText.Pattern(@"^[ \t]*:[ \t]*(?<events>.+?)[ \t]*$");
    public static bool IsTimeline(string source) => SourceText.Pattern(@"^timeline(?:\s|$)", RegexOptions.IgnoreCase).IsMatch(SourceText.Header(source));
    public TimelineVisualDraft(string source, DiagramService? diagrams = null) : base(source, diagrams) { }
    protected override TimelineSourceModel Scan(SourceText text)
    {
        var sections = new List<SourceSection>(); var periods = new List<TimelinePeriodSource>(); var protect = new List<string>(); var errors = new List<string>();
        string header = "", section = ""; SourceDeclaration? title = null; var last = -1;
        foreach (var (line, index) in text.Lines.Select((l, i) => (l, i)))
        {
            if (line.Content.Length == 0 || line.Comment) continue;
            if (line.InitDirective) { protect.Add(line.Key); continue; }
            if (header.Length == 0)
            {
                header = line.Key; var match = SourceDeclarations.Header(line, "timeline");
                if (!match.Success) errors.Add("Unsupported Timeline header. Inspect original source.");
                else if (match.Groups["label"].Success) title = new(line.Key, "title", match.Groups["label"].Value, match);
                section = header + ":unsectioned"; sections.Add(new(section, "Unsectioned", null)); continue;
            }
            var t = Title.Match(line.Body); if (t.Success) { title = new(line.Key, "title", t.Groups["label"].Value, t); continue; }
            var s = Section.Match(line.Body); if (s.Success) { section = line.Key; sections.Add(new(section, s.Groups["label"].Value, s)); last = -1; continue; }
            var continuation = Continuation.Match(line.Body);
            if (continuation.Success && last >= 0)
            {
                var old = periods[last]; periods[last] = old with { End = index + 1, Events = old.Events.Concat(Events(line, continuation.Groups["events"])).ToArray() }; continue;
            }
            var p = Period.Match(line.Body);
            if (p.Success && p.Groups["label"].Value.Trim().Length > 0)
            {
                var orphan = continuation.Success;
                periods.Add(new(line.Key, section, p.Groups["label"].Value.Trim(), p, index + 1, Events(line, p.Groups["events"]), orphan)); last = periods.Count - 1;
                if (orphan) protect.Add(line.Key);
            }
            else protect.Add(line.Key);
        }
        if (header.Length == 0) errors.Add("A Timeline header is required.");
        if (periods.Count + periods.Sum(p => p.Events.Count) > 500) errors.Add("Timeline exceeds the 500-element budget.");
        return new(header, title, sections, periods, protect, errors);
    }
    private static TimelineEventSource[] Events(SourceLine line, Group group)
    {
        var result = new List<TimelineEventSource>(); var start = group.Index;
        foreach (var (segment, index) in group.Value.Split(':').Select((s, i) => (s, i)))
        {
            var value = segment.Trim(); var pad = segment.Length - segment.TrimStart().Length;
            if (value.Length > 0) result.Add(new(line.Key + ":" + index, line.Key, value, start + pad, value.Length, start, segment.Length));
            start += segment.Length + 1;
        }
        return result.ToArray();
    }
    protected override bool Contains(TimelineSourceModel model, string identity) => identity == model.HeaderIdentity || model.Sections.Any(s => s.Identity == identity) || model.Periods.Any(p => p.Identity == identity || p.Events.Any(e => e.Identity == identity)) || model.Protected.Contains(identity);
    protected override void Validate(SourceText text, TimelineSourceModel model, DiagramResult rendered)
    {
        if (model.Errors.Count > 0) throw new ArgumentException(model.Errors[0]);
        if (!rendered.Success || rendered.Source != text.Source) throw new ArgumentException(rendered.Error ?? "Timeline did not render current source.");
        VerifyCanonical(model, rendered.Svg!);
    }
    public static void VerifyCanonical(TimelineSourceModel model, string svg)
    {
        SourceDeclarations.VerifyTitle(svg, model.Title);
        var periods = SourceDeclarations.Groups(svg, "timeline-period"); var events = SourceDeclarations.Groups(svg, "timeline-event"); var expected = model.Periods.SelectMany(p => p.Events).ToArray();
        if (periods.Length != model.Periods.Count || events.Length != expected.Length || periods.Where((p, i) => (string?)p.Attribute("data-label") != model.Periods[i].Label).Any() ||
            events.Where((e, i) => SourceDeclarations.VisibleText(e) != SourceDeclarations.TextValue(expected[i].Label)).Any())
            throw new ArgumentException("Canonical Timeline period/event counts, order or labels differ from source occurrences. Apply is protected.");
        var sections = SourceDeclarations.Groups(svg, "subgraph"); var named = model.Sections.Where(s => s.Tokens is not null && model.Periods.Any(p => p.Section == s.Identity)).ToArray();
        if (sections.Length != named.Length || sections.Where((s, i) => (string?)s.Attribute("data-label") != named[i].Name).Any()) throw new ArgumentException("Canonical Timeline sections differ from source groups.");
    }
    public Task<SourceAuthoringResult> SetTitleAsync(string value, CancellationToken token = default) => ChangeAsync((text, model) => SourceDeclarations.SetTitle(text, model.HeaderIdentity, model.Title, value), token);
    public Task<SourceAuthoringResult> UpdateAsync(string identity, string label, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        SourceText.Label(label);
        if (model.Sections.FirstOrDefault(s => s.Identity == identity && s.Tokens is not null) is { } section) return text.Patch(identity, section.Tokens!.Groups["label"], label);
        SourceDeclarations.PlainLabel(label, ":");
        if (model.Periods.FirstOrDefault(p => p.Identity == identity) is { } period)
        { if (period.Protected) throw new ArgumentException("An orphan continuation protects this period. Use Edit source."); SourceDeclarations.RejectDirectiveName(label, "title|section"); return text.Patch(identity, period.Tokens.Groups["label"], label); }
        var evt = model.Periods.SelectMany(p => p.Events).Single(e => e.Identity == identity); return text.Replace(text.Line(evt.LineIdentity).Replace(evt.Start, evt.Length, label));
    }, token);
    public Task<SourceAuthoringResult> AddSectionAsync(CancellationToken token = default) => ChangeAsync((text, _) => text.Insert(text.Lines.Count, "    section New section"), token);
    public Task<SourceAuthoringResult> AddPeriodAsync(string sectionId, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        var section = model.Sections.Single(s => s.Identity == sectionId); var end = SourceSections.End(text, model.Sections, section);
        if (model.Protected.Any(k => text.Index(k) >= (section.Tokens is null ? text.Index(model.HeaderIdentity) + 1 : text.Index(section.Identity)) && text.Index(k) < end)) throw new ArgumentException("Unknown source in this section protects period insertion. Choose a supported section or Edit source.");
        return text.Insert(end, "    New period : New event");
    }, token);
    public Task<SourceAuthoringResult> AddEventAsync(string periodId, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        var period = model.Periods.Single(p => p.Identity == periodId); SafePeriod(text, model, period);
        return text.Insert(period.End, text.Line(period.Identity).Indent + "    : New event");
    }, token);
    private static IEnumerable<string> Supported(TimelineSourceModel model) => model.Sections.Where(s => s.Tokens is not null).Select(s => s.Identity).Concat(model.Periods.Where(p => !p.Protected).Select(p => p.Identity)).Concat(model.Periods.Where(p => !p.Protected).SelectMany(p => p.Events).Select(e => e.LineIdentity));
    private static void SafePeriod(SourceText text, TimelineSourceModel model, TimelinePeriodSource period)
    {
        if (period.Protected) throw new ArgumentException("An orphan continuation protects this period group.");
        SourceSections.Safe(text, text.AttachedStart(period.Identity), period.End, Supported(model));
        SourceRowMoves.CheckTrailingAttachment(text, text.Lines[period.End - 1].Key);
    }
    public Task<SourceAuthoringResult> DeleteAsync(string identity, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        if (model.Sections.Any(s => s.Identity == identity)) return SourceSections.ChangeSection(text, model.Sections, identity, 0, true, Supported(model));
        if (model.Periods.FirstOrDefault(p => p.Identity == identity) is { } period) { SafePeriod(text, model, period); var start = text.AttachedStart(identity); return text.Splice(start, period.End - start, []); }
        var owner = model.Periods.Single(p => p.Events.Any(e => e.Identity == identity)); var evt = owner.Events.Single(e => e.Identity == identity);
        if (owner.Events.Count <= 1) throw new ArgumentException("A period needs at least one event. Remove the complete period instead.");
        var siblings = owner.Events.Where(e => e.LineIdentity == evt.LineIdentity).ToArray(); var index = Array.IndexOf(siblings, evt);
        if (siblings.Length == 1)
        {
            if (evt.LineIdentity == owner.Identity)
            {
                var next = owner.Events[1];
                if (text.Lines.Skip(text.Index(owner.Identity) + 1).Take(text.Index(next.LineIdentity) - text.Index(owner.Identity) - 1).Any(l => l.Content.Length > 0))
                    throw new ArgumentException("An intervening comment or directive protects promotion of the next continuation event.");
                var nextSiblings = owner.Events.Where(e => e.LineIdentity == next.LineIdentity).ToArray(); SourceText changed;
                if (nextSiblings.Length == 1) { SourceRowMoves.CheckTrailingAttachment(text, next.LineIdentity); changed = text.Splice(text.Index(next.LineIdentity), 1, []); }
                else changed = text.Replace(text.Line(next.LineIdentity).Replace(next.SegmentStart, next.SegmentLength + 1, ""));
                return changed.Replace(text.Line(evt.LineIdentity).Replace(evt.Start, evt.Length, next.Label));
            }
            SourceRowMoves.CheckTrailingAttachment(text, evt.LineIdentity); var start = text.AttachedStart(evt.LineIdentity); return text.Splice(start, text.Index(evt.LineIdentity) + 1 - start, []);
        }
        var begin = index == 0 ? evt.SegmentStart : evt.SegmentStart - 1; var length = evt.SegmentLength + 1;
        return text.Replace(text.Line(evt.LineIdentity).Replace(begin, length, ""));
    }, token, AfterRemoval(identity));
    private string AfterRemoval(string identity) => Model.Periods.FirstOrDefault(p => p.Events.Any(e => e.Identity == identity))?.Identity ?? Model.Periods.FirstOrDefault(p => p.Identity == identity)?.Section ?? Model.HeaderIdentity;
    public Task<SourceAuthoringResult> MoveAsync(string identity, int direction, CancellationToken token = default)
    {
        var eventOwner = Model.Periods.FirstOrDefault(p => p.Events.Any(e => e.Identity == identity)); var target = eventOwner is null ? -1 : eventOwner.Events.ToList().FindIndex(e => e.Identity == identity) + Math.Sign(direction);
        var selection = target >= 0 && target < (eventOwner?.Events.Count ?? 0) ? eventOwner!.Events[target].Identity : null;
        return ChangeAsync((text, model) =>
        {
            if (model.Sections.Any(s => s.Identity == identity)) return SourceSections.ChangeSection(text, model.Sections, identity, direction, false, Supported(model));
            if (model.Periods.FirstOrDefault(p => p.Identity == identity) is { } period)
            {
                var siblings = model.Periods.Where(p => p.Section == period.Section).ToArray(); var i = Array.IndexOf(siblings, period); var j = i + Math.Sign(direction);
                if (j < 0 || j >= siblings.Length) throw new ArgumentException("There is no period in that direction in this section. Use the destination selector to move between sections.");
                SafePeriod(text, model, period); SafePeriod(text, model, siblings[j]); return SourceRowMoves.Swap(text, siblings[Math.Min(i, j)].Identity, siblings[Math.Max(i, j)].Identity, siblings[Math.Min(i, j)].End, siblings[Math.Max(i, j)].End);
            }
            var owner = model.Periods.Single(p => p.Events.Any(e => e.Identity == identity)); var index = owner.Events.ToList().FindIndex(e => e.Identity == identity); var other = index + Math.Sign(direction);
            if (other < 0 || other >= owner.Events.Count) throw new ArgumentException("There is no event in that direction.");
            var a = owner.Events[Math.Min(index, other)]; var b = owner.Events[Math.Max(index, other)];
            if (a.LineIdentity == b.LineIdentity) return text.Replace(text.Line(a.LineIdentity).Replace(b.Start, b.Length, a.Label).Replace(a.Start, a.Length, b.Label));
            var between = text.Lines.Skip(text.Index(a.LineIdentity) + 1).Take(text.Index(b.LineIdentity) - text.Index(a.LineIdentity) - 1);
            if (between.Any(l => l.Content.Length > 0)) throw new ArgumentException("An intervening comment or directive protects this event move.");
            return text.Replace(text.Line(a.LineIdentity).Replace(a.Start, a.Length, b.Label)).Replace(text.Line(b.LineIdentity).Replace(b.Start, b.Length, a.Label));
        }, token, selection);
    }
    public Task<SourceAuthoringResult> MoveToSectionAsync(string periodId, string destinationId, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        var period = model.Periods.Single(p => p.Identity == periodId); var section = model.Sections.Single(s => s.Identity == destinationId); SafePeriod(text, model, period);
        if (period.Section == destinationId) throw new ArgumentException("Choose a different destination section.");
        var start = text.AttachedStart(periodId); var end = period.End; var destination = SourceSections.End(text, model.Sections, section);
        SourceSections.Safe(text, Math.Min(start, destination), Math.Max(end, destination), Supported(model));
        return SourceSections.MoveTo(text, start, end, destination);
    }, token);
}
