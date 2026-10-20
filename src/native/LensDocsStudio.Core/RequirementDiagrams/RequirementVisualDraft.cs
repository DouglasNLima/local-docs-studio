using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;

namespace LensDocsStudio.Core.RequirementDiagrams;

public sealed record RequirementBlock(string Identity, string EndIdentity, string Name, string Kind, SourceField NameToken, SourceField KindToken, IReadOnlyList<SourceField> Fields, bool Complete)
{
    public bool Element => Kind.Equals("element", StringComparison.OrdinalIgnoreCase);
    public string Value(string key) => Fields.LastOrDefault(f => f.Name.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value ?? "";
}
public sealed record RequirementRelation(string Identity, string Source, string Target, string Type, SourceField SourceToken, SourceField TargetToken, SourceField TypeToken, bool Reverse);
public sealed record RequirementSourceModel(string HeaderIdentity, SourceDeclaration? Title, SourceField? Direction, IReadOnlyList<RequirementBlock> Blocks, IReadOnlyList<RequirementRelation> Relations, IReadOnlyList<string> Protected)
{
    public IReadOnlyList<RequirementBlock> Visible => Blocks.Where(b => !b.Element).Concat(Blocks.Where(b => b.Element)).GroupBy(b => b.Name, StringComparer.Ordinal).Select(g => g.First()).ToArray();
    public bool Unique(string name) => Blocks.Count(b => b.Name == name) == 1;
    public bool TopologySafe => Blocks.All(b => b.Complete && Unique(b.Name)) && Protected.Count == 0 && Relations.All(r => r.Source != r.Target && Unique(r.Source) && Unique(r.Target)) && Relations.Select(r => (r.Source, r.Target)).Distinct().Count() == Relations.Count && !Cyclic;
    private bool Cyclic
    {
        get
        {
            var done = new HashSet<string>(); var active = new HashSet<string>();
            bool Visit(string n) { if (active.Contains(n)) return true; if (!done.Add(n)) return false; active.Add(n); foreach (var r in Relations.Where(r => r.Source == n)) if (Visit(r.Target)) return true; active.Remove(n); return false; }
            return Blocks.Any(b => Visit(b.Name));
        }
    }
}

public sealed class RequirementVisualDraft : SourceDraft<RequirementSourceModel>
{
    public const string Starter = "requirementDiagram\n    title Documentation traceability\n    direction LR\n    requirement guide {\n        id: \"DOC-17\"\n        text: \"Publish a local guide\"\n        risk: low\n        verifymethod: inspection\n    }\n    element studio {\n        type: \"Documentation studio\"\n        docRef: \"guide.md\"\n    }\n    studio - satisfies -> guide\n";
    public static readonly string[] Kinds = ["requirement", "functionalRequirement", "interfaceRequirement", "performanceRequirement", "physicalRequirement", "designConstraint"];
    public static readonly string[] RelationTypes = ["contains", "copies", "derives", "satisfies", "verifies", "refines", "traces"];
    private static readonly Regex Header = SourceText.Pattern(@"^requirement(?:Diagram)?$", RegexOptions.IgnoreCase);
    private static readonly Regex Open = SourceText.Pattern(@"^[ \t]*(?<kind>requirement|functionalRequirement|interfaceRequirement|performanceRequirement|physicalRequirement|designConstraint|element)\s+(?<name>.+?)\s*\{\s*$", RegexOptions.IgnoreCase);
    private static readonly Regex Property = SourceText.Pattern(@"^[ \t]*(?<key>\w+)\s*:\s*(?<value>.+?)[ \t]*$");
    private static readonly Regex Title = SourceText.Pattern(@"^[ \t]*title\s+(?<label>.+?)[ \t]*$");
    private static readonly Regex Direction = SourceText.Pattern(@"^[ \t]*direction\s+(?<value>TB|BT|LR|RL)[ \t]*$", RegexOptions.IgnoreCase);
    private static readonly Regex Forward = SourceText.Pattern(@"^[ \t]*(?<left>.+?)\s+-\s+(?<type>contains|copies|derives|satisfies|verifies|refines|traces)\s+->\s+(?<right>.+?)[ \t]*$", RegexOptions.IgnoreCase);
    private static readonly Regex Reverse = SourceText.Pattern(@"^[ \t]*(?<left>.+?)\s+<-\s+(?<type>contains|copies|derives|satisfies|verifies|refines|traces)\s+-\s+(?<right>.+?)[ \t]*$", RegexOptions.IgnoreCase);
    public static bool IsRequirement(string source) => Header.IsMatch(ChartSource.Header(source));
    public RequirementVisualDraft(string source, DiagramService? diagrams = null) : base(source, diagrams) { }
    protected override RequirementSourceModel Scan(SourceText t)
    {
        var rows = ChartSource.Body(t); var header = rows.FirstOrDefault(); var blocks = new List<RequirementBlock>(); var relations = new List<RequirementRelation>(); var protect = new List<string>(); SourceDeclaration? title = null; SourceField? direction = null;
        for (var i = 1; i < rows.Count; i++)
        {
            var line = rows[i]; var tm = Title.Match(line.Body); if (tm.Success) { title = new(line.Key, "title", WorkflowSource.Unquote(tm.Groups["label"].Value), tm); continue; }
            var dm = Direction.Match(line.Body); if (dm.Success) { direction = WorkflowSource.Field(line, "direction", dm.Groups["value"]); continue; }
            var open = Open.Match(line.Body);
            if (open.Success)
            {
                var fields = new List<SourceField>(); var complete = true; var end = line.Key; var closed = false;
                for (i++; i < rows.Count; i++)
                {
                    var body = rows[i]; end = body.Key; if (body.Content == "}") { closed = true; break; }
                    var p = Property.Match(body.Body); if (p.Success) fields.Add(WorkflowSource.Field(body, p.Groups["key"].Value, p.Groups["value"], true));
                    else { complete = false; protect.Add(body.Key); }
                }
                blocks.Add(new(line.Key, end, WorkflowSource.Unquote(open.Groups["name"].Value), open.Groups["kind"].Value, WorkflowSource.Field(line, "name", open.Groups["name"], true), WorkflowSource.Field(line, "kind", open.Groups["kind"]), fields, complete && closed)); continue;
            }
            var rel = Forward.Match(line.Body); var reverse = false; if (!rel.Success) { rel = Reverse.Match(line.Body); reverse = true; }
            if (rel.Success)
            {
                var src = WorkflowSource.Field(line, "source", rel.Groups[reverse ? "right" : "left"], true); var dst = WorkflowSource.Field(line, "target", rel.Groups[reverse ? "left" : "right"], true);
                relations.Add(new(line.Key, src.Value, dst.Value, rel.Groups["type"].Value.ToLowerInvariant(), src, dst, WorkflowSource.Field(line, "type", rel.Groups["type"]), reverse)); continue;
            }
            protect.Add(line.Key);
        }
        // Configuration and comments are passive, never symbolic references.
        return new(header is not null && Header.IsMatch(header.Content) ? header.Key : "", title, direction, blocks, relations, protect);
    }
    protected override bool Contains(RequirementSourceModel m, string id) => id == m.HeaderIdentity || id == m.Title?.Identity || id == m.Direction?.Identity || m.Blocks.Any(b => b.Identity == id) || m.Relations.Any(r => r.Identity == id) || m.Protected.Contains(id);
    protected override string? SourceProblem(RequirementSourceModel m) => m.HeaderIdentity.Length == 0 ? "A requirementDiagram header is required." : m.Blocks.Count + m.Relations.Count > 500 ? "Requirement exceeds the 500-element budget." : null;
    protected override void Validate(SourceText t, RequirementSourceModel m, DiagramResult r) { WorkflowSource.Rendered(t, r); Correspondence(m, r.Svg!); }
    public static IReadOnlyList<(RequirementBlock Block, XElement Shape)> Correspondence(RequirementSourceModel m, string svg)
    {
        var shapes = SourceDeclarations.Groups(svg, "req-node");
        if (!shapes.Select(s => (string?)s.Attribute("data-id")).SequenceEqual(m.Visible.Select(b => b.Name))) throw new ArgumentException("Canonical Requirement box identity/order differs from pinned first-wins semantics. Apply is protected.");
        return m.Visible.Zip(shapes, (b, s) => (b, s)).ToArray();
    }
    public static IReadOnlyList<(RequirementRelation Relation, XElement Shape)> RelationCorrespondence(RequirementSourceModel m, string svg)
    {
        if (!m.TopologySafe) return [];
        var boxes = Correspondence(m, svg).ToDictionary(p => p.Block.Name, p => p.Shape.Elements().First(e => e.Name.LocalName == "rect"), StringComparer.Ordinal); var paths = SourceDeclarations.Groups(svg, "req-relation"); var result = new List<(RequirementRelation, XElement)>();
        foreach (var path in paths)
        {
            var numbers = ChartSource.Numbers.Matches((string?)path.Attribute("d") ?? "").Select(n => double.Parse(n.Value, CultureInfo.InvariantCulture)).ToArray(); if (numbers.Length < 4) throw new ArgumentException("Canonical relationship path has no proved endpoints.");
            var matches = m.Relations.Where(r => r.Type == (string?)path.Attribute("data-type") && On(boxes[r.Source], numbers[0], numbers[1]) && On(boxes[r.Target], numbers[^2], numbers[^1])).ToArray();
            if (matches.Length != 1 || result.Any(p => p.Item1.Identity == matches[0].Identity)) throw new ArgumentException("Canonical relationship endpoints are ambiguous. Use source for affected topology."); result.Add((matches[0], path));
        }
        if (result.Count != m.Relations.Count) throw new ArgumentException("Not every simple source relationship has a canonical path. Topology is protected."); return result;
        static bool On(XElement rect, double x, double y)
        { var left = ChartSource.Attribute(rect, "x"); var top = ChartSource.Attribute(rect, "y"); var right = left + ChartSource.Attribute(rect, "width"); var bottom = top + ChartSource.Attribute(rect, "height"); return x >= left - .1 && x <= right + .1 && y >= top - .1 && y <= bottom + .1 && Math.Min(Math.Min(Math.Abs(x - left), Math.Abs(x - right)), Math.Min(Math.Abs(y - top), Math.Abs(y - bottom))) < .1; }
    }
    private static void Topology(RequirementSourceModel m) { if (!m.TopologySafe) throw new ArgumentException("Hidden/duplicate nodes, unknown text, missing references, malformed blocks, parallel/self/cyclic relations protect affected topology. Safe independent fields remain available."); }
    private static void Name(string name) { SourceDeclarations.PlainLabel(name, "\"{}\r\n"); if (name.Contains(" ->", StringComparison.Ordinal) || name.Contains(" <-", StringComparison.Ordinal) || name.StartsWith("%%", StringComparison.Ordinal)) throw new ArgumentException("This symbolic name contains an unrepresentable relation/comment delimiter."); }
    private static RequirementBlock Editable(RequirementSourceModel m, string id)
    { var b = m.Blocks.SingleOrDefault(b => b.Identity == id) ?? throw new ArgumentException("Select a requirement or element block occurrence."); if (!b.Complete || !m.Unique(b.Name)) throw new ArgumentException("This hidden/duplicate or malformed block is protected. Inspect its original source."); return b; }
    public Task<SourceAuthoringResult> SetHeadingAsync(string title, string direction, CancellationToken token = default) => ChangeAsync((t, m) =>
    {
        var changed = t; if (title != (m.Title?.Value ?? "")) { var value = WorkflowSource.Quoted(title, m.Title?.Tokens.Groups["label"].Value); changed = SourceDeclarations.SetTitle(changed, m.HeaderIdentity, m.Title, value); }
        if (direction != (m.Direction?.Value ?? "TB")) { if (!new[] { "TB", "BT", "LR", "RL" }.Contains(direction)) throw new ArgumentException("Choose TB, BT, LR or RL."); changed = m.Direction is null ? changed.Insert(changed.Index(m.HeaderIdentity) + 1, "    direction " + direction) : m.Direction.Set(changed, direction); } return changed;
    }, token);
    public Task<SourceAuthoringResult> UpdateBlockAsync(string identity, string kind, IReadOnlyDictionary<string, string> values, CancellationToken token = default, string? symbolicName = null) => ChangeAsync((t, m) =>
    {
        var b = Editable(m, identity); var changed = t;
        if (kind != b.Kind) { if (b.Element || !Kinds.Contains(kind)) throw new ArgumentException("Choose one of the six requirement kinds; element conversion is protected."); changed = b.KindToken.Set(changed, kind); }
        foreach (var (key, value) in values)
        {
            if (!(b.Element ? new[] { "type", "docRef" } : new[] { "id", "text", "risk", "verifymethod" }).Contains(key, StringComparer.OrdinalIgnoreCase)) throw new ArgumentException("This property is outside the editable slice.");
            if (value == b.Value(key)) continue;
            if (key.Equals("risk", StringComparison.OrdinalIgnoreCase) && value.Length > 0 && !new[] { "low", "medium", "high" }.Contains(value.ToLowerInvariant())) throw new ArgumentException("Choose low, medium, high or blank risk; unsupported values have no native presentation.");
            if (key.Equals("verifymethod", StringComparison.OrdinalIgnoreCase) && value.Length > 0 && !new[] { "analysis", "demonstration", "inspection", "test" }.Contains(value.ToLowerInvariant())) throw new ArgumentException("Choose analysis, demonstration, inspection, test or blank verification.");
            var current = Scan(changed).Blocks.Single(x => x.Identity == identity); changed = WorkflowSource.SetField(changed, current.Fields, key, value, changed.Index(current.EndIdentity), t.Line(identity).Indent + "    ");
        }
        return symbolicName is null || symbolicName == b.Name ? changed : Rename(changed, Scan(changed), identity, symbolicName);
    }, token);
    public Task<SourceAuthoringResult> RenameAsync(string identity, string name, CancellationToken token = default) => ChangeAsync((t, m) => Rename(t, m, identity, name), token);
    private static SourceText Rename(SourceText t, RequirementSourceModel m, string identity, string name)
    {
        var b = Editable(m, identity); Name(name); if (name == b.Name) return t; Topology(m); if (m.Blocks.Any(x => x.Name == name)) throw new ArgumentException("The symbolic name already exists.");
        var changed = b.NameToken.Set(t, WorkflowSource.Quoted(name, b.NameToken.Raw));
        foreach (var relation in m.Relations)
        {
            var edits = new[] { relation.SourceToken, relation.TargetToken }.Where(f => f.Value == b.Name).OrderByDescending(f => f.Start);
            foreach (var f in edits) changed = f.Set(changed, WorkflowSource.Quoted(name, f.Raw));
        }
        return changed;
    }
    public Task<SourceAuthoringResult> AddBlockAsync(string name, string kind, CancellationToken token = default) => ChangeAsync((t, m) =>
    { Topology(m); Name(name); if (m.Blocks.Any(b => b.Name == name)) throw new ArgumentException("The symbolic name already exists."); if (!Kinds.Append("element").Contains(kind)) throw new ArgumentException("Choose a supported block kind."); var at = m.Relations.FirstOrDefault() is { } relation ? t.AttachedStart(relation.Identity) : t.Lines.Count; var changed = t.Insert(at, "    " + kind + " " + WorkflowSource.Quoted(name) + " {"); return changed.Insert(at + 1, "    }"); }, token);
    public Task<SourceAuthoringResult> AddRelationAsync(string source, string target, string type, CancellationToken token = default) => ChangeAsync((t, m) =>
    { Topology(m); Endpoints(m, source, target, type); var changed = t.Insert(t.Lines.Count, "    " + WorkflowSource.Quoted(source) + " - " + type + " -> " + WorkflowSource.Quoted(target)); if (!Scan(changed).TopologySafe) throw new ArgumentException("This relationship would create an unproved cycle."); return changed; }, token);
    private static void Endpoints(RequirementSourceModel m, string source, string target, string type)
    { if (source == target || !m.Unique(source) || !m.Unique(target) || !RelationTypes.Contains(type)) throw new ArgumentException("Choose two existing distinct symbolic names and a supported relation type."); if (m.Relations.Any(r => r.Source == source && r.Target == target)) throw new ArgumentException("A parallel relationship would have ambiguous correspondence."); }
    public Task<SourceAuthoringResult> UpdateRelationAsync(string identity, string source, string target, string type, CancellationToken token = default) => ChangeAsync((t, m) =>
    {
        Topology(m); Endpoints(m with { Relations = m.Relations.Where(r => r.Identity != identity).ToArray() }, source, target, type); var r = m.Relations.Single(r => r.Identity == identity); var changed = t;
        foreach (var (field, value) in new[] { (r.SourceToken, source), (r.TargetToken, target), (r.TypeToken, type) }.OrderByDescending(p => p.Item1.Start)) if ((field.Name == "type" ? r.Type : field.Value) != value) changed = field.Set(changed, field.Name == "type" ? value : WorkflowSource.Quoted(value, field.Raw));
        if (!Scan(changed).TopologySafe) throw new ArgumentException("This relationship would create an unproved cycle or dependency."); return changed;
    }, token);
    public Task<SourceAuthoringResult> DeleteAsync(string identity, CancellationToken token = default) => ChangeAsync((t, m) =>
    {
        Topology(m); if (m.Relations.Any(r => r.Identity == identity)) return ChartSource.DeleteRow(t, identity);
        var b = Editable(m, identity); if (m.Relations.Any(r => r.Source == b.Name || r.Target == b.Name)) throw new ArgumentException("Remove the incident relationships first. Connected block deletion never silently cascades or leaves dangling references.");
        var start = t.AttachedStart(identity); var end = t.Index(b.EndIdentity) + 1; SourceRowMoves.CheckTrailingAttachment(t, b.EndIdentity); return t.Splice(start, end - start, []);
    }, token);
    public Task<SourceAuthoringResult> MoveAsync(string identity, int direction, CancellationToken token = default) => ChangeAsync((t, m) =>
    {
        Topology(m); var b = Editable(m, identity); var peers = m.Blocks.ToArray(); var at = Array.IndexOf(peers, b); var next = at + Math.Sign(direction); if (next < 0 || next >= peers.Length) throw new ArgumentException("There is no block in that direction."); var a = peers[Math.Min(at, next)]; var c = peers[Math.Max(at, next)]; SourceRowMoves.CheckTrailingAttachment(t, a.EndIdentity); SourceRowMoves.CheckTrailingAttachment(t, c.EndIdentity); return SourceRowMoves.Swap(t, a.Identity, c.Identity, t.Index(a.EndIdentity) + 1, t.Index(c.EndIdentity) + 1);
    }, token);
}
