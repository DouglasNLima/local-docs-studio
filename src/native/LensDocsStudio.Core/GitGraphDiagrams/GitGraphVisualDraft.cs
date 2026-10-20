using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;

namespace LensDocsStudio.Core.GitGraphDiagrams;

public sealed record GitSourceAttribute(SourceField Field, int Start, int Length);
public sealed record GitSourceCommand(string Identity, string Kind, string BranchBefore, string BranchAfter, string? Reference, string? Label, int Lane, IReadOnlyList<GitSourceAttribute> Attributes, bool Simple)
{
    public bool Mark => Kind is "commit" or "merge" or "cherry-pick";
    public string Value(string name) => name == "type" ? (Attributes.FirstOrDefault(a => a.Field.Name == name)?.Field.Value ?? "NORMAL").ToUpperInvariant() : Attributes.FirstOrDefault(a => a.Field.Name == name)?.Field.Value ?? "";
}
public sealed record GitSourceLink(string From, string To, string Kind);
public sealed record GitGraphSourceModel(string HeaderIdentity, IReadOnlyList<GitSourceCommand> Commands, IReadOnlyList<string> Branches, IReadOnlyDictionary<string, string> Heads, IReadOnlyList<GitSourceLink> Links, IReadOnlyList<string> Protected, string CurrentBranch)
{
    public bool Basic => Protected.Count == 0;
    public IReadOnlyList<GitSourceCommand> Marks => Commands.Where(c => c.Mark).ToArray();
}

/// <summary>Lossless command stream with conservative reference proof. All drawing and layout remain engine-owned.</summary>
public sealed class GitGraphVisualDraft : SourceDraft<GitGraphSourceModel>
{
    public const string Starter = "gitGraph LR:\n    commit id: \"Start\"\n    branch docs\n    commit tag: \"Draft\" type: HIGHLIGHT\n    checkout main\n    commit type: REVERSE\n    merge docs id: \"Reviewed\"\n    commit\n";
    private static readonly Regex Header = SourceText.Pattern(@"^gitGraph\s*(?:LR:|TB:|BT:)?\s*$", RegexOptions.IgnoreCase);
    private static readonly Regex Branch = SourceText.Pattern("^[ \\t]*branch\\s+\"?(?<name>[^\"\\s]+)\"?(?:\\s+order:\\s*(?<order>\\d+))?[ \\t]*$");
    private static readonly Regex Checkout = SourceText.Pattern("^[ \\t]*(?:checkout|switch)\\s+\"?(?<name>[^\"]+?)\"?[ \\t]*$");
    private static readonly Regex Merge = SourceText.Pattern("^[ \\t]*merge\\s+\"?(?<name>[^\"]+?)\"?(?:\\s+(?<rest>.+))?$");
    private static readonly Regex Commit = SourceText.Pattern(@"^[ \t]*commit(?:\s+(?<rest>.+))?$"), Cherry = SourceText.Pattern(@"^[ \t]*cherry-pick\s+(?<rest>.+)$");
    private static readonly Regex Attribute = SourceText.Pattern("(?<name>id|tag|parent):\\s*(?<value>\"[^\"]+\")|(?<name>(?i:type)):\\s*(?<value>(?i:NORMAL|HIGHLIGHT|REVERSE))");
    private static readonly Regex NewBranch = SourceText.Pattern(@"^[A-Za-z_][A-Za-z0-9_./-]*$");
    public static bool IsGitGraph(string source) => Header.IsMatch(ChartSource.Header(source));
    public GitGraphVisualDraft(string source, DiagramService? diagrams = null) : base(source, diagrams) { }
    protected override GitGraphSourceModel Scan(SourceText t)
    {
        var rows = ChartSource.Body(t); var header = rows.FirstOrDefault(); var commands = new List<GitSourceCommand>(); var branches = new List<string> { "main" }; var heads = new Dictionary<string, string>(StringComparer.Ordinal); var forks = new Dictionary<string, string>(StringComparer.Ordinal); var links = new List<GitSourceLink>(); var protect = new List<string>(); var current = "main"; var counter = 0;
        if (header is not null && (header.Content.Contains("TB:", StringComparison.OrdinalIgnoreCase) || header.Content.Contains("BT:", StringComparison.OrdinalIgnoreCase))) protect.Add(header.Key);
        foreach (var line in rows.Skip(1))
        {
            var before = current; var kind = "unknown"; string? reference = null, label = null; var simple = true; var fields = new List<GitSourceAttribute>(); var branch = Branch.Match(line.Body.TrimEnd()); var checkout = Checkout.Match(line.Body.TrimEnd()); var merge = Merge.Match(line.Body.TrimEnd()); var commit = Commit.Match(line.Body.TrimEnd()); var cherry = Cherry.Match(line.Body.TrimEnd());
            if (branch.Success)
            {
                kind = "branch"; reference = branch.Groups["name"].Value;
                if (!branches.Contains(reference)) { branches.Add(reference); if (heads.TryGetValue(current, out var fork)) forks[reference] = fork; } else simple = false;
                current = reference; if (branch.Groups["order"].Success) simple = false;
            }
            else if (checkout.Success) { kind = line.Content.StartsWith("switch", StringComparison.Ordinal) ? "switch" : "checkout"; reference = checkout.Groups["name"].Value; current = reference; if (!branches.Contains(reference)) simple = false; }
            else if (merge.Success || commit.Success || cherry.Success)
            {
                kind = merge.Success ? "merge" : commit.Success ? "commit" : "cherry-pick"; reference = merge.Success ? merge.Groups["name"].Value : null;
                var match = merge.Success ? merge : commit.Success ? commit : cherry; var rest = match.Groups["rest"]; var at = rest.Index;
                foreach (Match attr in Attribute.Matches(line.Body, rest.Success ? rest.Index : line.Body.Length))
                {
                    if (line.Body[at..attr.Index].Any(c => !char.IsWhiteSpace(c))) simple = false;
                    fields.Add(new(WorkflowSource.Field(line, attr.Groups["name"].Value.ToLowerInvariant(), attr.Groups["value"], true), attr.Index, attr.Length)); at = attr.Index + attr.Length;
                }
                if (rest.Success && line.Body[at..].Any(c => !char.IsWhiteSpace(c))) simple = false;
                if (fields.GroupBy(f => f.Field.Name).Any(g => g.Count() > 1) || fields.Any(f => f.Field.Name == "parent")) simple = false;
                var id = fields.FirstOrDefault(f => f.Field.Name == "id")?.Field.Value; label = kind == "commit" ? id ?? counter.ToString(CultureInfo.InvariantCulture) : id;
                if (kind == "cherry-pick") simple = false;
                if (!branches.Contains(current)) simple = false;
                if (heads.TryGetValue(current, out var parent)) links.Add(new(parent, line.Key, "lane"));
                else if (forks.TryGetValue(current, out var fork)) links.Add(new(fork, line.Key, commands.Single(c => c.Identity == fork).Lane == branches.IndexOf(current) ? "lane" : "fork"));
                if (kind == "merge")
                { if (reference == current || reference is null || !heads.TryGetValue(reference, out var mergeHead) || !heads.ContainsKey(current)) simple = false; if (reference is not null && heads.TryGetValue(reference, out mergeHead)) links.Add(new(mergeHead, line.Key, "merge")); }
                heads[current] = line.Key; counter++;
            }
            else simple = false;
            if (!simple) protect.Add(line.Key);
            commands.Add(new(line.Key, kind, before, current, reference, label, Math.Max(0, branches.IndexOf(current)), fields, simple));
        }
        return new(header is not null && Header.IsMatch(header.Content) ? header.Key : "", commands, branches, heads, links, protect, current);
    }
    protected override bool Contains(GitGraphSourceModel m, string id) => id == m.HeaderIdentity || m.Commands.Any(c => c.Identity == id);
    protected override string? SourceProblem(GitGraphSourceModel m) => m.HeaderIdentity.Length == 0 ? "A gitGraph header is required." : m.Commands.Count > 500 ? "GitGraph exceeds the 500-element budget." : null;
    protected override void Validate(SourceText t, GitGraphSourceModel m, DiagramResult r) { WorkflowSource.Rendered(t, r); Correspondence(m, r.Svg!); }
    public static IReadOnlyList<(GitSourceCommand Command, XElement Shape)> Correspondence(GitGraphSourceModel m, string svg)
    {
        var shapes = SourceDeclarations.Groups(svg, "commit"); var marks = m.Marks;
        if (shapes.Length != marks.Count) throw new ArgumentException("Canonical GitGraph command marks differ from source occurrences. Apply is protected.");
        for (var i = 0; i < marks.Count; i++)
        {
            var c = marks[i]; var texts = shapes[i].Elements().Where(e => e.Name.LocalName == "text").Select(e => e.Value).ToArray();
            var expected = new[] { c.Label, c.Kind == "cherry-pick" ? null : c.Value("tag") }.Where(v => !string.IsNullOrEmpty(v));
            if (!texts.SequenceEqual(expected) || (string?)shapes[i].Attribute("data-type") != c.Value("type").ToLowerInvariant() || ((string?)shapes[i].Attribute("data-merge") == "true") != (c.Kind == "merge")) throw new ArgumentException("Canonical GitGraph labels, tag/type or merge occurrences differ from source. Apply is protected.");
        }
        if (m.Basic) VerifyLinks(m, svg, shapes);
        return marks.Zip(shapes, (c, s) => (c, s)).ToArray();
    }
    private static void VerifyLinks(GitGraphSourceModel m, string svg, XElement[] shapes)
    {
        var branches = SourceDeclarations.Groups(svg, "gitgraph-branch");
        if (m.Marks.Count > 0 && !branches.Select(b => (string?)b.Attribute("data-branch")).SequenceEqual(m.Branches)) throw new ArgumentException("Canonical GitGraph branch declaration order differs from source.");
        var laneCentres = branches.ToDictionary(b => (string)b.Attribute("data-branch")!, b => Centre(b).Y, StringComparer.Ordinal);
        foreach (var (command, shape) in m.Marks.Zip(shapes)) if (Math.Abs(Centre(shape).Y - laneCentres[command.BranchAfter]) > .1) throw new ArgumentException("Canonical GitGraph commit lane differs from its proved current branch.");
        var centres = m.Marks.Zip(shapes).ToDictionary(p => p.First.Identity, p => Centre(p.Second)); var actual = SourceDeclarations.Groups(svg, "gitgraph-link").ToList();
        foreach (var expected in m.Links)
        {
            var from = centres[expected.From]; var to = centres[expected.To]; var candidates = actual.Where(p => (string?)p.Attribute("data-kind") == expected.Kind && Endpoints(p, from, to)).ToArray();
            if (candidates.Length != 1) throw new ArgumentException("Canonical GitGraph parent/branch-head links are ambiguous or incomplete. Stream operations are protected."); actual.Remove(candidates[0]);
        }
        if (actual.Count > 0) throw new ArgumentException("Canonical GitGraph contains an unproved parent link.");
        var head = m.Heads.GetValueOrDefault(m.CurrentBranch); var marked = shapes.Select((s, i) => (s, i)).Where(p => (string?)p.s.Attribute("data-head") == "true").ToArray();
        if (head is null ? marked.Length != 0 : marked.Length != 1 || m.Marks[marked[0].i].Identity != head) throw new ArgumentException("Canonical GitGraph current branch HEAD differs from source context.");
        static bool Endpoints(XElement p, (double X, double Y) from, (double X, double Y) to)
        { var n = ChartSource.Numbers.Matches((string?)p.Attribute("d") ?? "").Select(v => double.Parse(v.Value, CultureInfo.InvariantCulture)).ToArray(); return n.Length >= 4 && Math.Abs(n[0] - from.X) < .1 && Math.Abs(n[1] - from.Y) < .1 && Math.Abs(n[^2] - to.X) < .1 && Math.Abs(n[^1] - to.Y) < .1; }
    }
    private static (double X, double Y) Centre(XElement shape)
    { var mark = shape.Elements().First(e => e.Name.LocalName is "circle" or "rect"); return mark.Name.LocalName == "circle" ? (ChartSource.Attribute(mark, "cx"), ChartSource.Attribute(mark, "cy")) : (ChartSource.Attribute(mark, "x") + ChartSource.Attribute(mark, "width") / 2, ChartSource.Attribute(mark, "y") + ChartSource.Attribute(mark, "height") / 2); }
    private static void Basic(GitGraphSourceModel m) { if (!m.Basic) throw new ArgumentException("Orientation/order, repeated branches, unknown commands, unresolved heads or cherry-pick protect stateful stream operations. Original source remains available."); }
    private static void ValidateText(string value) { if (value.Length > 0) SourceText.Label(value); if (value.IndexOfAny(['"', '\r', '\n']) >= 0) throw new ArgumentException("Double quotes and line breaks have no proved escape here. Pending input is retained."); }
    public Task<SourceAuthoringResult> UpdateAsync(string identity, string id, string tag, string type, CancellationToken token = default) => ChangeAsync((t, m) =>
    {
        var c = m.Commands.Single(c => c.Identity == identity); if (!c.Simple || !c.Mark || c.Kind == "cherry-pick") throw new ArgumentException("This command has unproved attributes or ancestry. Preserve its source; edit an independent simple command."); ValidateText(id); ValidateText(tag); if (!new[] { "NORMAL", "HIGHLIGHT", "REVERSE" }.Contains(type)) throw new ArgumentException("Choose NORMAL, HIGHLIGHT or REVERSE.");
        if (id != c.Value("id") && (m.Commands.Any(c => c.Kind == "cherry-pick") || id.Length > 0 && m.Commands.Any(x => x.Identity != identity && x.Value("id") == id))) throw new ArgumentException("An existing ID or cherry-pick reference protects this ID change.");
        var changed = t;
        foreach (var (name, value) in new[] { ("id", id), ("tag", tag), ("type", type) })
        {
            var current = Scan(changed).Commands.Single(x => x.Identity == identity); if (value == current.Value(name)) continue; var attribute = current.Attributes.FirstOrDefault(a => a.Field.Name == name);
            if (attribute is not null) changed = value.Length == 0 ? changed.Replace(changed.Line(identity).Replace(attribute.Start, attribute.Length, "")) : attribute.Field.Set(changed, name == "type" ? value : "\"" + value + "\"");
            else { var line = changed.Line(identity); changed = changed.Replace(line.Replace(line.Body.TrimEnd().Length, 0, " " + name + ": " + (name == "type" ? value : "\"" + value + "\""))); }
        }
        return changed;
    }, token);
    public Task<SourceAuthoringResult> AppendAsync(string kind, string reference = "", string id = "", string tag = "", string type = "NORMAL", CancellationToken token = default) => ChangeAsync((t, m) =>
    {
        Basic(m); ValidateText(id); ValidateText(tag); if (!new[] { "NORMAL", "HIGHLIGHT", "REVERSE" }.Contains(type)) throw new ArgumentException("Choose NORMAL, HIGHLIGHT or REVERSE.");
        if (id.Length > 0 && m.Commands.Any(c => c.Value("id") == id)) throw new ArgumentException("Choose an unused explicit commit ID.");
        if (kind == "branch") { if (!NewBranch.IsMatch(reference) || m.Branches.Contains(reference)) throw new ArgumentException("Use a new branch name with letters, digits, underscore, dot, slash or hyphen, beginning with a letter or underscore."); }
        else if (kind is "checkout" or "switch" or "merge")
        { if (!m.Branches.Contains(reference)) throw new ArgumentException("Choose an existing branch."); if (kind == "merge" && (reference == m.CurrentBranch || !m.Heads.ContainsKey(reference) || !m.Heads.ContainsKey(m.CurrentBranch))) throw new ArgumentException("A merge requires two distinct existing branches with non-empty heads."); }
        else if (kind != "commit") throw new ArgumentException("Append commit, branch, checkout/switch or merge. Cherry-pick authoring is protected.");
        var command = kind + (kind == "commit" ? "" : " " + reference);
        if (kind is "commit" or "merge") command += (id.Length > 0 ? " id: \"" + id + "\"" : "") + (tag.Length > 0 ? " tag: \"" + tag + "\"" : "") + (type != "NORMAL" ? " type: " + type : "");
        var changed = t.Insert(t.Lines.Count, "    " + command); Basic(Scan(changed)); return changed;
    }, token);
    private static void Tail(GitGraphSourceModel m, string identity)
    { Basic(m); var index = m.Commands.ToList().FindIndex(c => c.Identity == identity); if (index < 0 || m.Commands.Skip(index).Any(c => c.Kind != "commit")) throw new ArgumentException("Only simple independent commits in the final uninterrupted commit run can be removed or reordered. Branch/head dependencies protect earlier history."); }
    public Task<SourceAuthoringResult> DeleteAsync(string identity, CancellationToken token = default) => ChangeAsync((t, m) => { Tail(m, identity); return ChartSource.DeleteRow(t, identity); }, token);
    public Task<SourceAuthoringResult> MoveAsync(string identity, int direction, CancellationToken token = default) => ChangeAsync((t, m) =>
    { Tail(m, identity); var at = m.Commands.ToList().FindIndex(c => c.Identity == identity); var next = at + Math.Sign(direction); if (next < 0 || next >= m.Commands.Count) throw new ArgumentException("There is no command in that direction."); Tail(m, m.Commands[next].Identity); return ChartSource.MoveRow(t, m.Commands.Select(c => c.Identity).ToArray(), identity, direction); }, token);
}
