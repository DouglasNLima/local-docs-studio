using System.Text.RegularExpressions;
using System.Xml.Linq;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;

namespace LensDocsStudio.Core.ArchitectureDiagrams;

public sealed record ArchitectureSourceNode(string Identity, string Kind, SourceField IdToken, SourceField? LabelToken, SourceField? IconToken, SourceField? ParentToken, SourceField? ParentClause)
{
    public string Id => IdToken.Value;
    public string Label => LabelToken?.Value ?? "";
    public string Icon => IconToken?.Value ?? "";
    public string Parent => ParentToken?.Value ?? "";
}
public sealed record ArchitectureSourceEdge(string Identity, SourceField SourceToken, SourceField TargetToken, SourceField SourceSideToken, SourceField TargetSideToken, SourceField ArrowToken, bool Braced)
{
    public string Source => SourceToken.Value;
    public string Target => TargetToken.Value;
}
public sealed record ArchitectureSourceModel(string HeaderIdentity, IReadOnlyList<ArchitectureSourceNode> Nodes, IReadOnlyList<ArchitectureSourceEdge> Edges, IReadOnlyList<string> Protected)
{
    public bool Unique(string id) => Nodes.Count(n => n.Id == id) == 1;
    public bool TopologySafe => Protected.Count == 0 && Nodes.All(n => Unique(n.Id) && (n.Parent.Length == 0 || n.Kind != "group" && Nodes.Any(g => g.Kind == "group" && g.Id == n.Parent && g.Parent.Length == 0))) &&
        Edges.All(e => !e.Braced && e.Source != e.Target && Nodes.Any(n => n.Id == e.Source && n.Kind != "group") && Nodes.Any(n => n.Id == e.Target && n.Kind != "group"));
}

/// <summary>Lossless bounded architecture authoring, verified against actual native SVG geometry.</summary>
public sealed class ArchitectureVisualDraft : SourceDraft<ArchitectureSourceModel>
{
    public const string Starter = "architecture-beta\ngroup platform(cloud)[Platform]\nservice gateway(internet)[Gateway] in platform\nservice api(server)[API] in platform\nservice records(database)[Records]\njunction route in platform\ngateway:R --> L:api\napi:B -- T:route\nroute:R --> L:records\n";
    public static readonly string[] Icons = ["cloud", "database", "disk", "internet", "server", "generic"];
    public static bool IsArchitecture(string source) => ChartSource.Header(source).Equals("architecture-beta", StringComparison.OrdinalIgnoreCase);
    private static readonly Regex Node = SourceText.Pattern(@"^[ \t]*(?<kind>group|service)\s+(?<id>\S+?)(?:\((?<icon>[^)]*)\))?\s*\[(?<label>[^\]]*)\](?<parentClause>\s+in\s+(?<parent>\S+))?[ \t]*$");
    private static readonly Regex Junction = SourceText.Pattern(@"^[ \t]*(?<kind>junction)\s+(?<id>\S+?)(?<parentClause>\s+in\s+(?<parent>\S+))?[ \t]*$");
    private static readonly Regex Edge = SourceText.Pattern(@"^[ \t]*(?<source>\S+?)(?<sourcePort>:(?<sourceSide>L|R|T|B))?\s*(?<arrow>-->|<-->|<--|--)\s*(?<targetPort>(?<targetSide>L|R|T|B):)?(?<target>\S+)[ \t]*$");
    public ArchitectureVisualDraft(string source, DiagramService? diagrams = null) : base(source, diagrams) { }
    protected override ArchitectureSourceModel Scan(SourceText text) => Read(text);
    public static ArchitectureSourceModel Read(SourceText text)
    {
        var body = ChartSource.Body(text); if (body.Count == 0 || !body[0].Content.Equals("architecture-beta", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("This bounded editor requires architecture-beta. Original source is available.");
        var nodes = new List<ArchitectureSourceNode>(); var edges = new List<ArchitectureSourceEdge>(); var protectedRows = new List<string>();
        foreach (var line in body.Skip(1))
        {
            var match = Node.Match(line.Body); if (!match.Success) match = Junction.Match(line.Body);
            if (match.Success)
            {
                SourceField? Optional(string name) => match.Groups[name].Success ? WorkflowSource.Field(line, name, match.Groups[name]) : null;
                nodes.Add(new(line.Key, match.Groups["kind"].Value, WorkflowSource.Field(line, "id", match.Groups["id"]), Optional("label"), Optional("icon"), Optional("parent"), Optional("parentClause"))); continue;
            }
            match = Edge.Match(line.Body);
            if (match.Success)
            {
                var source = WorkflowSource.Field(line, "source", match.Groups["source"]); var target = WorkflowSource.Field(line, "target", match.Groups["target"]);
                var braced = source.Value.IndexOfAny(['{', '}']) >= 0 || target.Value.IndexOfAny(['{', '}']) >= 0;
                SourceField Port(string name, int absentAt) => match.Groups[name].Success ? WorkflowSource.Field(line, name, match.Groups[name]) : new(line.Key, name, "", absentAt, 0, "");
                edges.Add(new(line.Key, source, target, Port("sourcePort", source.Start + source.Length), Port("targetPort", target.Start), WorkflowSource.Field(line, "arrow", match.Groups["arrow"]), braced)); continue;
            }
            protectedRows.Add(line.Key);
        }
        return new(body[0].Key, nodes, edges, protectedRows);
    }
    protected override bool Contains(ArchitectureSourceModel model, string identity) => identity == model.HeaderIdentity || model.Nodes.Any(n => n.Identity == identity) || model.Edges.Any(e => e.Identity == identity) || model.Protected.Contains(identity);
    protected override void Validate(SourceText text, ArchitectureSourceModel model, DiagramResult rendered)
    {
        WorkflowSource.Rendered(text, rendered);
        foreach (var node in model.Nodes.Where(n => model.Unique(n.Id)))
        {
            var cls = node.Kind == "group" ? "architecture-group-title" : "architecture-" + node.Kind;
            var shapes = SourceDeclarations.Groups(rendered.Svg!, cls).Where(e => (string?)e.Attribute("data-id") == node.Id).ToArray();
            if (shapes.Length != 1 || node.LabelToken is not null && SourceDeclarations.VisibleText(shapes[0]) != SourceDeclarations.TextValue(SourceText.Pattern(@"<br\s*/?>", RegexOptions.IgnoreCase).Replace(node.Label, "\n")))
                throw new ArgumentException("The source declaration has no exact canonical native correspondence. Apply is protected.");
        }
        if (model.TopologySafe && EdgeCorrespondence(model, rendered.Svg!).Count != model.Edges.Count) throw new ArgumentException("The connection occurrences do not match the canonical native endpoints and arrowheads.");
    }
    public static IReadOnlyList<(ArchitectureSourceNode Node, XElement Shape)> Correspondence(ArchitectureSourceModel model, string svg)
    {
        var result = new List<(ArchitectureSourceNode, XElement)>();
        foreach (var node in model.Nodes.Where(n => model.Unique(n.Id)))
        {
            var shapes = SourceDeclarations.Groups(svg, "architecture-" + node.Kind).Where(e => (string?)e.Attribute("data-id") == node.Id).ToArray();
            if (shapes.Length == 1) result.Add((node, shapes[0]));
        }
        return result;
    }
    public static IReadOnlyList<(ArchitectureSourceEdge Edge, XElement Shape)> EdgeCorrespondence(ArchitectureSourceModel model, string svg)
    {
        var actual = SourceDeclarations.Groups(svg, "architecture-edge"); var expected = model.Edges.Where(e => !e.Braced && model.Unique(e.Source) && model.Unique(e.Target) && model.Nodes.Any(n => n.Id == e.Source && n.Kind != "group") && model.Nodes.Any(n => n.Id == e.Target && n.Kind != "group")).ToArray();
        if (expected.Length != actual.Length) return [];
        return expected.Zip(actual).Where(p => (string?)p.Second.Attribute("data-source") == p.First.Source && (string?)p.Second.Attribute("data-target") == p.First.Target && (p.Second.Attribute("marker-start") is not null) == p.First.ArrowToken.Value.StartsWith('<') && (p.Second.Attribute("marker-end") is not null) == p.First.ArrowToken.Value.EndsWith('>')).Select(p => (p.First, p.Second)).ToArray();
    }
    private static void Topology(ArchitectureSourceModel model)
    { if (!model.TopologySafe) throw new ArgumentException("Unknown, duplicate, nested, unresolved or brace endpoint source protects topology operations. Inspect the preserved source."); }
    private static ArchitectureSourceNode Editable(ArchitectureSourceModel model, string identity)
    { var node = model.Nodes.Single(n => n.Identity == identity); if (!model.Unique(node.Id)) throw new ArgumentException("Duplicate IDs share the group/service/junction namespace and protect this occurrence."); return node; }
    private static string Label(string value) { SourceDeclarations.PlainLabel(value, "[]<>"); return value; }
    public Task<SourceAuthoringResult> UpdateNodeAsync(string identity, string id, string label, string icon, string parent, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        var node = Editable(model, identity); var patches = new List<(SourceField, string)>();
        if (id != node.Id)
        {
            Topology(model); TopologySource.Identifier(id); if (model.Nodes.Any(n => n.Id == id)) throw new ArgumentException("That ID already exists in the shared namespace.");
            patches.Add((node.IdToken, id));
            patches.AddRange(model.Nodes.Where(n => n.Parent == node.Id).Select(n => (n.ParentToken!, id)));
            patches.AddRange(model.Edges.Where(e => e.Source == node.Id).Select(e => (e.SourceToken, id)));
            patches.AddRange(model.Edges.Where(e => e.Target == node.Id).Select(e => (e.TargetToken, id)));
        }
        if (node.LabelToken is not null && label != node.Label) patches.Add((node.LabelToken, Label(label)));
        if (node.Kind != "junction" && icon != node.Icon)
        {
            if (icon.Length > 0 && !Icons.Contains(icon)) throw new ArgumentException("Choose a verified bundled native pictogram or leave the icon omitted.");
            if (node.IconToken is { } field)
                patches.Add(icon.Length == 0 ? (new(field.Identity, "icon-clause", "", field.Start - 1, field.Length + 2, "(" + field.Raw + ")"), "") : (field, icon));
            else if (icon.Length > 0) patches.Add((new(node.Identity, "icon-clause", "", node.IdToken.Start + node.IdToken.Length, 0, ""), "(" + icon + ")"));
        }
        if (parent != node.Parent)
        {
            Topology(model);
            if (parent.Length > 0 && (node.Kind == "group" || !model.Nodes.Any(n => n.Kind == "group" && n.Id == parent && n.Parent.Length == 0))) throw new ArgumentException("Choose a root group for a service or junction. Deeper hierarchy remains protected.");
            if (node.ParentToken is not null) patches.Add(parent.Length == 0 ? (node.ParentClause!, "") : (node.ParentToken, parent));
            else if (parent.Length > 0) { var line = text.Line(node.Identity); var at = line.Body.TrimEnd(' ', '\t').Length; patches.Add((new(line.Key, "parent-clause", "", at, 0, ""), " in " + parent)); }
        }
        return TopologySource.Patch(text, patches);
    }, token);
    public Task<SourceAuthoringResult> AddNodeAsync(string kind, string id, string label, string icon = "", string parent = "", CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        Topology(model); TopologySource.Identifier(id); if (model.Nodes.Any(n => n.Id == id)) throw new ArgumentException("That ID already exists.");
        if (kind is not ("service" or "group" or "junction")) throw new ArgumentException("Choose service, junction or group.");
        if (parent.Length > 0 && (kind == "group" || !model.Nodes.Any(n => n.Id == parent && n.Kind == "group" && n.Parent.Length == 0))) throw new ArgumentException("Choose a root group for the new service or junction.");
        if (icon.Length > 0 && !Icons.Contains(icon)) throw new ArgumentException("Choose a verified bundled pictogram.");
        return TopologySource.Append(text, kind + " " + id + (kind == "junction" ? "" : (icon.Length > 0 ? "(" + icon + ")" : "") + "[" + Label(label) + "]") + (parent.Length > 0 ? " in " + parent : ""));
    }, token);
    private static void Endpoints(ArchitectureSourceModel model, string source, string target, string sourceSide, string targetSide, string arrow)
    {
        if (source == target || !model.Nodes.Any(n => n.Id == source && n.Kind != "group") || !model.Nodes.Any(n => n.Id == target && n.Kind != "group")) throw new ArgumentException("Choose two existing distinct services or junctions.");
        if (!new[] { "", "L", "R", "T", "B" }.Contains(sourceSide) || !new[] { "", "L", "R", "T", "B" }.Contains(targetSide) || !new[] { "--", "-->", "<--", "<-->" }.Contains(arrow)) throw new ArgumentException("Choose L/R/T/B or an omitted side, and none/source/target/both arrowheads.");
    }
    public Task<SourceAuthoringResult> AddEdgeAsync(string source, string target, string sourceSide = "R", string targetSide = "L", string arrow = "-->", CancellationToken token = default) => ChangeAsync((text, model) =>
    { Topology(model); Endpoints(model, source, target, sourceSide, targetSide, arrow); return TopologySource.Append(text, source + (sourceSide.Length > 0 ? ":" + sourceSide : "") + " " + arrow + " " + (targetSide.Length > 0 ? targetSide + ":" : "") + target); }, token);
    public Task<SourceAuthoringResult> UpdateEdgeAsync(string identity, string source, string target, string sourceSide, string targetSide, string arrow, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        Topology(model); Endpoints(model, source, target, sourceSide, targetSide, arrow); var edge = model.Edges.Single(e => e.Identity == identity);
        return TopologySource.Patch(text, [(edge.SourceToken, source), (edge.TargetToken, target), (edge.SourceSideToken, sourceSide.Length > 0 ? ":" + sourceSide : ""), (edge.TargetSideToken, targetSide.Length > 0 ? targetSide + ":" : ""), (edge.ArrowToken, arrow)]);
    }, token);
    public Task<SourceAuthoringResult> DeleteAsync(string identity, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        Topology(model); if (model.Edges.Any(e => e.Identity == identity)) return ChartSource.DeleteRow(text, identity);
        var node = Editable(model, identity);
        if (model.Nodes.Any(n => n.Parent == node.Id) || model.Edges.Any(e => e.Source == node.Id || e.Target == node.Id)) throw new ArgumentException("Remove incident connections and children first. Deletion never cascades or orphans source.");
        return ChartSource.DeleteRow(text, identity);
    }, token);
    public Task<SourceAuthoringResult> MoveAsync(string identity, int direction, CancellationToken token = default) => ChangeAsync((text, model) =>
    { Topology(model); var node = model.Nodes.Any(n => n.Identity == identity); var rows = node ? model.Nodes.Select(n => n.Identity).ToArray() : model.Edges.Select(e => e.Identity).ToArray(); return TopologySource.MoveRows(text, rows, identity, direction, node ? model.Edges.Select(e => e.Identity) : model.Nodes.Select(n => n.Identity)); }, token);
}
