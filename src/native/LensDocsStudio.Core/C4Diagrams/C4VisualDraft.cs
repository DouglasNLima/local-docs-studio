using System.Text.RegularExpressions;
using System.Xml.Linq;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;

namespace LensDocsStudio.Core.C4Diagrams;

public sealed record C4SourceNode(string Identity, string EndIdentity, SourceCall Call, string? ParentIdentity, int Depth, bool Boundary, bool Complete)
{
    public string Kind => Call.Keyword.Value;
    public string Alias => Call.Positional.Count > 0 ? Call.Positional[0].SplitValue : "";
    public string Label => Call.Value(1, Alias);
    public int TechnologyIndex => Boundary ? Kind.Equals("Boundary", StringComparison.OrdinalIgnoreCase) ? 2 : -1 : Kind.StartsWith("Person", StringComparison.OrdinalIgnoreCase) || Kind.StartsWith("System", StringComparison.OrdinalIgnoreCase) ? -1 : 2;
    public int DescriptionIndex => Boundary ? -1 : TechnologyIndex < 0 ? 2 : 3;
    public string Technology => TechnologyIndex < 0 ? "" : Call.Value(TechnologyIndex);
    public string Description => DescriptionIndex < 0 ? "" : Call.Value(DescriptionIndex);
    public bool Simple => Complete && Call.Balanced && !Call.Arguments.Any(a => a.Named || a.Ignored) && !Kind.StartsWith("Node", StringComparison.OrdinalIgnoreCase) && !Kind.Equals("Deployment_Node", StringComparison.OrdinalIgnoreCase) && Call.Positional.Count <= (Boundary ? TechnologyIndex < 0 ? 2 : 3 : DescriptionIndex + 1);
}
public sealed record C4SourceRelation(string Identity, SourceCall Call)
{
    public bool Back => Call.Keyword.Value.Equals("Rel_Back", StringComparison.OrdinalIgnoreCase);
    public string Source => Call.Positional.Count > 1 ? Call.Positional[Back ? 1 : 0].SplitValue : "";
    public string Target => Call.Positional.Count > 1 ? Call.Positional[Back ? 0 : 1].SplitValue : "";
    public string Label => Call.Value(2);
    public string Technology => Call.Value(3);
    public bool Simple => Call.Balanced && new[] { "Rel", "BiRel", "Rel_Back" }.Contains(Call.Keyword.Value, StringComparer.OrdinalIgnoreCase) && Call.Positional.Count is >= 2 and <= 4 && !Call.Arguments.Any(a => a.Named || a.Ignored);
}
public sealed record C4SourceModel(string HeaderIdentity, string Variant, SourceDeclaration? Title, IReadOnlyList<C4SourceNode> Nodes, IReadOnlyList<C4SourceRelation> Relations, IReadOnlyList<string> Protected, int MaximumDepth)
{
    public bool SupportedVariant => new[] { "C4Context", "C4Container", "C4Component" }.Contains(Variant, StringComparer.OrdinalIgnoreCase);
    public bool Unique(string alias) => Nodes.Count(n => n.Alias == alias) == 1;
    public IReadOnlyList<C4SourceNode> EffectiveNodes => Nodes.GroupBy(n => n.Alias, StringComparer.Ordinal).SelectMany(g => g.Count() == 1 ? g.ToArray() : g.All(n => !n.Boundary) ? new[] { g.Last() } : Array.Empty<C4SourceNode>()).ToArray();
    public bool TopologySafe => SupportedVariant && Protected.Count == 0 && Nodes.All(n => n.Simple && Unique(n.Alias) && n.Depth <= 1 && (!n.Boundary || n.Depth == 0)) &&
        Relations.All(r => r.Simple && r.Source != r.Target && Nodes.Any(n => n.Alias == r.Source && !n.Boundary) && Nodes.Any(n => n.Alias == r.Target && !n.Boundary)) && Relations.Select(r => (r.Source, r.Target)).Distinct().Count() == Relations.Count && TopologySource.Acyclic(Nodes.Select(n => n.Alias), Relations.Select(r => (r.Source, r.Target)));
}

/// <summary>Three separately proved C4 levels; exact call tokens and complete brace groups remain authoritative.</summary>
public sealed class C4VisualDraft : SourceDraft<C4SourceModel>
{
    public const string Starter = "C4Context\ntitle Documentation context\nPerson(reader, \"Reader\", \"Reads local documentation\")\nSystem_Boundary(studio, \"Studio\") {\n  System(docs, \"Documents\", \"Local Markdown and Mermaid\")\n}\nSystem_Ext(archive, \"Archive\", \"Existing files\")\nRel(reader, docs, \"Reads\", \"Local\")\nRel_Back(archive, docs, \"Exports\", \"Files\")\n";
    public static readonly string[] ElementKinds = ["Person", "Person_Ext", "System", "System_Ext", "SystemDb", "SystemDb_Ext", "SystemQueue", "SystemQueue_Ext", "Container", "Container_Ext", "ContainerDb", "ContainerDb_Ext", "ContainerQueue", "ContainerQueue_Ext", "Component", "Component_Ext", "ComponentDb", "ComponentDb_Ext", "ComponentQueue", "ComponentQueue_Ext"];
    public static readonly string[] BoundaryKinds = ["Boundary", "Enterprise_Boundary", "System_Boundary", "Container_Boundary"];
    private static readonly Regex Header = SourceText.Pattern(@"^(?<kind>C4(?:Context|Container|Component|Dynamic|Deployment))(?:\s+title\s+(?<label>.+))?$", RegexOptions.IgnoreCase);
    private static readonly Regex Node = SourceText.Pattern(@"^[ \t]*(?<kind>Person(?:_Ext)?|System(?:Db|Queue)?(?:_Ext)?|Container(?:Db|Queue)?(?:_Ext)?|Component(?:Db|Queue)?(?:_Ext)?|Deployment_Node|Node(?:_[LR])?|Boundary|Enterprise_Boundary|System_Boundary|Container_Boundary)\s*\((?<args>.+)\)\s*(?<open>\{)?[ \t]*$", RegexOptions.IgnoreCase);
    private static readonly Regex Relation = SourceText.Pattern(@"^[ \t]*(?<kind>BiRel|Rel_Back|Rel_U|Rel_Up|Rel_D|Rel_Down|Rel_L|Rel_Left|Rel_R|Rel_Right|RelIndex|Rel)\s*\((?<args>.+)\)[ \t]*$", RegexOptions.IgnoreCase);
    private static readonly Regex Title = SourceText.Pattern(@"^[ \t]*title\s+(?<label>.+?)[ \t]*$");
    public static bool IsC4(string source) => SourceText.Pattern(@"^C4(?:Context|Container|Component)(?:\s+title\s+.+)?$", RegexOptions.IgnoreCase).IsMatch(ChartSource.Header(source));
    public C4VisualDraft(string source, DiagramService? diagrams = null) : base(source, diagrams) { }
    protected override C4SourceModel Scan(SourceText text) => Read(text);
    public static C4SourceModel Read(SourceText text)
    {
        var body = ChartSource.Body(text); if (body.Count == 0 || !Header.IsMatch(body[0].Content)) throw new ArgumentException("Select a C4 Context, Container or Component source diagram.");
        var header = Header.Match(body[0].Body.Trim()); var title = header.Groups["label"].Success ? new SourceDeclaration(body[0].Key, "title", ChartSource.Unquote(header.Groups["label"].Value), SourceDeclarations.Header(body[0], @"C4(?:Context|Container|Component|Dynamic|Deployment)", false)) : null;
        var nodes = new List<C4SourceNode>(); var relations = new List<C4SourceRelation>(); var protectedRows = new List<string>(); var stack = new Stack<int>(); var maximumDepth = 0;
        for (var index = 1; index < body.Count; index++)
        {
            var line = body[index];
            if (line.Content == "}")
            {
                if (stack.Count == 0) protectedRows.Add(line.Key); else { var at = stack.Pop(); nodes[at] = nodes[at] with { EndIdentity = line.Key, Complete = true }; }
                continue;
            }
            var match = Title.Match(line.Body);
            if (match.Success) { title = new(line.Key, "title", ChartSource.Unquote(match.Groups["label"].Value), match); if (stack.Count > 0) protectedRows.Add(line.Key); continue; }
            match = Relation.Match(line.Body);
            if (match.Success) { relations.Add(new(line.Key, SourceCall.Read(line, match))); continue; }
            match = Node.Match(line.Body);
            if (!match.Success) { protectedRows.Add(line.Key); continue; }
            var call = SourceCall.Read(line, match); var boundary = BoundaryKinds.Contains(call.Keyword.Value, StringComparer.OrdinalIgnoreCase) || call.Keyword.Value.Equals("Deployment_Node", StringComparison.OrdinalIgnoreCase) || call.Keyword.Value.StartsWith("Node", StringComparison.OrdinalIgnoreCase);
            if (call.Positional.Count == 0) { protectedRows.Add(line.Key); continue; }
            var opens = boundary && (match.Groups["open"].Success || index + 1 < body.Count && body[index + 1].Content == "{");
            nodes.Add(new(line.Key, line.Key, call, stack.Count > 0 ? nodes[stack.Peek()].Identity : null, stack.Count, boundary, !opens));
            maximumDepth = Math.Max(maximumDepth, stack.Count + (opens ? 1 : 0));
            if (opens) { stack.Push(nodes.Count - 1); if (!match.Groups["open"].Success) index++; }
        }
        return new(body[0].Key, header.Groups["kind"].Value, title, nodes, relations, protectedRows, maximumDepth);
    }
    protected override string? SourceProblem(C4SourceModel model) => !model.SupportedVariant ? "C4Dynamic and C4Deployment remain protected source-only variants. Use the original source editor." : null;
    protected override bool Contains(C4SourceModel model, string identity) => identity == model.HeaderIdentity || model.Nodes.Any(n => n.Identity == identity) || model.Relations.Any(r => r.Identity == identity) || model.Protected.Contains(identity);
    protected override void Validate(SourceText text, C4SourceModel model, DiagramResult rendered)
    {
        WorkflowSource.Rendered(text, rendered);
        foreach (var node in model.EffectiveNodes)
        {
            var cls = node.Boundary ? "c4-boundary" : "c4-element"; var actual = SourceDeclarations.Groups(rendered.Svg!, cls).Where(e => (string?)e.Attribute("data-id") == node.Alias).ToArray();
            if (actual.Length != 1 || !Matches(node, actual[0]))
                throw new ArgumentException("The C4 source occurrence differs from its canonical native element. Apply is protected.");
        }
        if (model.TopologySafe && RelationCorrespondence(model, rendered.Svg!).Count != model.Relations.Count) throw new ArgumentException("The C4 relationship endpoints have no complete canonical correspondence.");
    }
    private static bool Matches(C4SourceNode node, XElement shape) => node.Boundary ||
        (string?)shape.Attribute("data-kind") == SourceText.Pattern("_Ext$", RegexOptions.IgnoreCase).Replace(node.Kind, "").ToLowerInvariant() &&
        (string?)shape.Attribute("data-external") == (node.Kind.EndsWith("_Ext", StringComparison.OrdinalIgnoreCase) ? "true" : "false") &&
        new[] { node.Label, node.Technology, node.Description }.All(value => SourceDeclarations.VisibleText(shape).Contains(SourceDeclarations.TextValue(value), StringComparison.Ordinal));
    public static IReadOnlyList<(C4SourceNode Node, XElement Shape)> Correspondence(C4SourceModel model, string svg) => model.EffectiveNodes.SelectMany(n => SourceDeclarations.Groups(svg, n.Boundary ? "c4-boundary" : "c4-element").Where(e => (string?)e.Attribute("data-id") == n.Alias && Matches(n, e)).Select(e => (n, e))).ToArray();
    public static IReadOnlyList<(C4SourceRelation Relation, XElement Shape)> RelationCorrespondence(C4SourceModel model, string svg)
    {
        if (!model.TopologySafe) return []; var shapes = SourceDeclarations.Groups(svg, "c4-relation"); if (shapes.Length != model.Relations.Count) return [];
        return model.Relations.Zip(shapes).Where(p => (string?)p.Second.Attribute("data-from") == p.First.Source && (string?)p.Second.Attribute("data-to") == p.First.Target && (p.Second.Attribute("marker-start") is not null) == p.First.Call.Keyword.Value.Equals("BiRel", StringComparison.OrdinalIgnoreCase)).Select(p => (p.First, p.Second)).ToArray();
    }
    private static void Topology(C4SourceModel model) { if (!model.TopologySafe) throw new ArgumentException("Incomplete/nested boundaries, unknown/named/extra arguments, duplicate aliases or unproved relationships protect topology operations. Original source is retained."); }
    public Task<SourceAuthoringResult> SetTitleAsync(string value, CancellationToken token = default) => ChangeAsync((text, model) => model.Title?.Value == value ? text : SourceDeclarations.SetTitle(text, model.HeaderIdentity, model.Title, SourceCall.Quote(value)), token);
    public Task<SourceAuthoringResult> UpdateNodeAsync(string identity, string alias, string label, string technology, string description, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        var node = model.Nodes.Single(n => n.Identity == identity); if (!model.EffectiveNodes.Any(n => n.Identity == identity) || !node.Call.Balanced) throw new ArgumentException("Hidden aliases or ambiguous argument quoting protect this occurrence. Proved effective fields remain independent.");
        var values = new Dictionary<int, string>();
        if (alias != node.Alias)
        {
            Topology(model); TopologySource.Identifier(alias); if (model.Nodes.Any(n => n.Alias == alias)) throw new ArgumentException("That alias already exists.");
            if (node.Call.Positional.Count < 2) throw new ArgumentException("This source couples the label to the alias. Set an explicit label before renaming it.");
            values[0] = alias;
            foreach (var relation in model.Relations)
            {
                var renamed = new Dictionary<int, string>();
                if (relation.Source == node.Alias) renamed[relation.Back ? 1 : 0] = alias;
                if (relation.Target == node.Alias) renamed[relation.Back ? 0 : 1] = alias;
                if (renamed.Count > 0) text = relation.Call.Set(text, renamed);
            }
        }
        if (label != node.Label) values[1] = SourceCall.Quote(label);
        if (node.TechnologyIndex >= 0 && technology != node.Technology) values[node.TechnologyIndex] = SourceCall.Quote(technology);
        if (node.DescriptionIndex >= 0 && description != node.Description) values[node.DescriptionIndex] = SourceCall.Quote(description);
        return node.Call.Set(text, values);
    }, token);
    public Task<SourceAuthoringResult> AddNodeAsync(string kind, string alias, string label, string technology = "", string description = "", CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        Topology(model); TopologySource.Identifier(alias); if (model.Nodes.Any(n => n.Alias == alias)) throw new ArgumentException("That alias already exists.");
        var boundary = BoundaryKinds.Contains(kind); if (!boundary && !ElementKinds.Contains(kind)) throw new ArgumentException("Choose a verified C4 element or boundary kind; levels are never converted.");
        var declaration = kind + "(" + alias + ", " + SourceCall.Quote(label);
        if (!boundary) declaration += kind.StartsWith("Person", StringComparison.Ordinal) || kind.StartsWith("System", StringComparison.Ordinal) ? ", " + SourceCall.Quote(description) : ", " + SourceCall.Quote(technology) + ", " + SourceCall.Quote(description);
        text = text.Insert(text.Lines.Count, declaration + ")" + (boundary ? " {" : "")); return boundary ? text.Insert(text.Lines.Count, "}") : text;
    }, token);
    private static void Endpoints(C4SourceModel model, string source, string target)
    { if (source == target || !model.Nodes.Any(n => n.Alias == source && !n.Boundary) || !model.Nodes.Any(n => n.Alias == target && !n.Boundary)) throw new ArgumentException("Choose two existing distinct C4 elements, keeping boundary aliases separate."); }
    public Task<SourceAuthoringResult> AddRelationAsync(string kind, string source, string target, string label = "", string technology = "", CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        Topology(model); Endpoints(model, source, target); if (!new[] { "Rel", "BiRel", "Rel_Back" }.Contains(kind)) throw new ArgumentException("Choose Rel, BiRel or Rel_Back.");
        var back = kind == "Rel_Back"; var changed = text.Insert(text.Lines.Count, kind + "(" + (back ? target : source) + ", " + (back ? source : target) + ", " + SourceCall.Quote(label) + ", " + SourceCall.Quote(technology) + ")");
        if (!Read(changed).TopologySafe) throw new ArgumentException("This relationship would introduce an unproved parallel or cyclic topology."); return changed;
    }, token);
    public Task<SourceAuthoringResult> UpdateRelationAsync(string identity, string source, string target, string label, string technology, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        var relation = model.Relations.Single(r => r.Identity == identity); if (!relation.Simple) throw new ArgumentException("This relationship variant or its arguments remain protected source.");
        var values = new Dictionary<int, string>();
        if (source != relation.Source || target != relation.Target) { Topology(model); Endpoints(model, source, target); values[relation.Back ? 1 : 0] = source; values[relation.Back ? 0 : 1] = target; }
        if (label != relation.Label) values[2] = SourceCall.Quote(label); if (technology != relation.Technology) values[3] = SourceCall.Quote(technology);
        var changed = relation.Call.Set(text, values); if ((source != relation.Source || target != relation.Target) && !Read(changed).TopologySafe) throw new ArgumentException("This edit would introduce an unproved parallel or cyclic topology."); return changed;
    }, token);
    public Task<SourceAuthoringResult> DeleteAsync(string identity, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        Topology(model); if (model.Relations.Any(r => r.Identity == identity)) return ChartSource.DeleteRow(text, identity);
        var node = model.Nodes.Single(n => n.Identity == identity);
        if (model.Nodes.Any(n => n.ParentIdentity == identity) || model.Relations.Any(r => r.Source == node.Alias || r.Target == node.Alias)) throw new ArgumentException("Remove the boundary children and incident relationships first. Deletion never cascades.");
        var start = text.AttachedStart(identity); var end = text.Index(node.EndIdentity) + 1; SourceRowMoves.CheckTrailingAttachment(text, node.EndIdentity); return text.Splice(start, end - start, []);
    }, token);
    public Task<SourceAuthoringResult> MoveAsync(string identity, int direction, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        Topology(model); if (model.Relations.Any(r => r.Identity == identity)) return ChartSource.MoveRow(text, model.Relations.Select(r => r.Identity).ToArray(), identity, direction);
        var node = model.Nodes.Single(n => n.Identity == identity); var peers = model.Nodes.Where(n => n.ParentIdentity == node.ParentIdentity).ToArray(); var at = Array.IndexOf(peers, node); var next = at + Math.Sign(direction);
        if (next < 0 || next >= peers.Length) throw new ArgumentException("There is no peer in that direction."); var a = peers[Math.Min(at, next)]; var b = peers[Math.Max(at, next)];
        SourceRowMoves.CheckTrailingAttachment(text, a.EndIdentity); SourceRowMoves.CheckTrailingAttachment(text, b.EndIdentity); return SourceRowMoves.Swap(text, a.Identity, b.Identity, text.Index(a.EndIdentity) + 1, text.Index(b.EndIdentity) + 1);
    }, token);
    public Task<SourceAuthoringResult> MoveMembershipAsync(string identity, string boundaryIdentity, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        Topology(model); var node = model.Nodes.Single(n => n.Identity == identity);
        if (node.ParentIdentity == (boundaryIdentity.Length == 0 ? null : boundaryIdentity)) return text;
        if (node.Boundary && model.Nodes.Any(n => n.ParentIdentity == node.Identity)) throw new ArgumentException("Only an empty boundary can change membership in this slice.");
        var target = boundaryIdentity.Length == 0 ? null : model.Nodes.SingleOrDefault(n => n.Identity == boundaryIdentity && n.Boundary && n.Complete && n.Depth == 0);
        if (boundaryIdentity.Length > 0 && (target is null || node.Boundary)) throw new ArgumentException("Choose a complete root boundary for an element. Nested boundary construction remains protected.");
        SourceRowMoves.CheckTrailingAttachment(text, node.EndIdentity); var start = text.AttachedStart(identity); var end = text.Index(node.EndIdentity) + 1; var group = text.Lines.Skip(start).Take(end - start).ToArray();
        if (group.Any(l => l.Ending.Length == 0)) throw new ArgumentException("An unterminated source group protects this move; add a source line terminator explicitly first.");
        var changed = text.Splice(start, end - start, []); var insertion = target is null ? changed.Lines.Count : changed.Index(target.EndIdentity);
        if (insertion > 0 && (changed.Lines[insertion - 1].Comment || changed.Lines[insertion - 1].Ending.Length == 0)) throw new ArgumentException("The destination has ambiguous trailing comment or line attachment. Source is unchanged.");
        return changed.Splice(insertion, 0, group);
    }, token);
}
