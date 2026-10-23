using System.Text.RegularExpressions;
using System.Xml.Linq;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Core.TreemapDiagrams;

namespace LensDocsStudio.Core.VennDiagrams;

public sealed record VennSourceSet(string Identity, SourceField IdToken, SourceField? LabelToken, SourceField? SizeToken, bool Proved)
{ public string Id => IdToken.Value; public string Label => LabelToken?.Value ?? Id; }
public sealed record VennSourceUnion(string Identity, SourceField MembersToken, IReadOnlyList<SourceField> References, SourceField? LabelToken, SourceField? SizeToken, bool Proved)
{ public string Label => LabelToken?.Value ?? ""; }
public sealed record VennSourceModel(string HeaderIdentity, IReadOnlyList<VennSourceSet> Sets, IReadOnlyList<VennSourceUnion> Unions, IReadOnlyList<string> Protected)
{
    public bool Unique(string id) => Sets.Count(s => s.Id == id) == 1;
    public bool StructureSafe => Protected.Count == 0 && Sets.All(s => s.Proved && Unique(s.Id)) && Unions.All(u => u.Proved && u.References.All(r => Unique(r.Value)));
}

/// <summary>Safe identifiers and exact union references. Stored sizes never claim proportional native geometry.</summary>
public sealed class VennVisualDraft : SourceDraft<VennSourceModel>
{
    public const string Starter = "venn-beta\nset A[\"Authors\"]: 100\nset \"B\"[\"Reviewers\"]: 50\nset C[\"Readers\"]\nunion A, \"B\"[\"Shared\"]: 20\nunion A, C\n";
    private static readonly Regex SafeId = SourceText.Pattern(@"^[A-Za-z_][A-Za-z0-9_-]*$");
    public static bool IsVenn(string source) => ChartSource.Header(source).Equals("venn-beta", StringComparison.OrdinalIgnoreCase);
    public VennVisualDraft(string source, DiagramService? diagrams = null) : base(source, diagrams) { }
    protected override VennSourceModel Scan(SourceText text) => Read(text);
    protected override void ValidateSourceChange(SourceText before, VennSourceModel previous, SourceText after, VennSourceModel current)
    {
        static IEnumerable<string> Protected(VennSourceModel model) => model.Protected.Concat(model.Sets.Where(s => !s.Proved || !model.Unique(s.Id)).Select(s => s.Identity)).Concat(model.Unions.Where(u => !u.Proved || u.References.Any(r => !model.Unique(r.Value))).Select(u => u.Identity));
        ExpansionViSource.PreserveProtected(before, Protected(previous), after, Protected(current));
    }
    public static VennSourceModel Read(SourceText text)
    {
        var body = ChartSource.Body(text); if (body.Count == 0 || !IsVenn(text.Source)) throw new ArgumentException("This editor requires venn-beta source.");
        var sets = new List<VennSourceSet>(); var unions = new List<VennSourceUnion>(); var protectedRows = new List<string>();
        foreach (var line in body.Skip(1))
        {
            var match = ExpansionViSource.VennSet.Match(line.Body);
            SourceField? Optional(string name) => match.Groups[name].Success ? WorkflowSource.Field(line, name, match.Groups[name]) : null;
            if (match.Success)
            {
                var id = WorkflowSource.Field(line, "id", match.Groups[match.Groups["quoted"].Success ? "quoted" : "id"]);
                sets.Add(new(line.Key, id, Optional("label"), Optional("value"), SafeId.IsMatch(id.Value) && id.Value.Length <= 200 && !(Optional("label")?.Value.IndexOfAny(['<', '>']) >= 0))); continue;
            }
            match = ExpansionViSource.VennUnion.Match(line.Body);
            if (!match.Success) { protectedRows.Add(line.Key); continue; }
            var members = WorkflowSource.Field(line, "members", match.Groups["members"]); var references = new List<SourceField>(); var at = 0; var proved = true;
            foreach (var raw in members.Raw.Split(','))
            {
                var token = raw.Trim(); var leading = raw.Length - raw.TrimStart().Length; var value = token.Trim('"');
                references.Add(new(line.Key, "reference", value, members.Start + at + leading, token.Length, token)); at += raw.Length + 1;
                proved &= SafeId.IsMatch(value) && value.Length <= 200 && (token == value || token == "\"" + value + "\"");
            }
            var nativeReferences = references.Where(r => r.Value.Length > 0).ToArray();
            if (nativeReferences.Length < 2) { protectedRows.Add(line.Key); continue; }
            proved &= references.Count >= 2 && references.Select(r => r.Value).Distinct(StringComparer.Ordinal).Count() == references.Count && !(Optional("label")?.Value.IndexOfAny(['<', '>']) >= 0);
            unions.Add(new(line.Key, members, references, Optional("label"), Optional("value"), proved));
        }
        return new(body[0].Key, sets, unions, protectedRows);
    }
    protected override bool Contains(VennSourceModel model, string identity) => identity == model.HeaderIdentity || model.Sets.Any(s => s.Identity == identity) || model.Unions.Any(u => u.Identity == identity) || model.Protected.Contains(identity);
    protected override void Validate(SourceText text, VennSourceModel model, DiagramResult rendered)
    {
        WorkflowSource.Rendered(text, rendered); var shapes = SourceDeclarations.Groups(rendered.Svg!, "venn-set");
        if (shapes.Length != model.Sets.Count || shapes.Zip(model.Sets).Any(p => (string?)p.First.Attribute("data-set") != p.Second.Id || (string?)p.First.Elements().Single(e => e.Name.LocalName == "circle").Attribute("r") != "130"))
            throw new ArgumentException("Venn ordered IDs or fixed native circles differ from source. Inspect source; Apply is protected.");
        if (model.StructureSafe)
        {
            var texts = XDocument.Parse(rendered.Svg!).Root!.Elements().Where(e => e.Name.LocalName == "text").Select(e => SourceDeclarations.TextValue(e.Value)).ToArray();
            var expected = model.Sets.Select(s => SourceDeclarations.TextValue(s.Label)).Concat(model.Unions.Where(u => u.LabelToken is not null).Select(u => SourceDeclarations.TextValue(u.Label))).ToArray();
            if (!texts.SequenceEqual(expected)) throw new ArgumentException("Venn native set/union label order does not match proved source occurrences. Inspect source.");
        }
    }
    public static IReadOnlyList<(VennSourceSet Set, XElement Shape)> Correspondence(VennSourceModel model, string svg)
    {
        var shapes = SourceDeclarations.Groups(svg, "venn-set"); if (shapes.Length != model.Sets.Count) return [];
        return model.Sets.Zip(shapes).Where(p => p.First.Proved && model.Unique(p.First.Id) && (string?)p.Second.Attribute("data-set") == p.First.Id).Select(p => (p.First, p.Second)).ToArray();
    }
    public Task<SourceAuthoringResult> UpdateSetAsync(string identity, string id, string label, string size, bool renameDefaultLabel = false, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        var set = model.Sets.Single(s => s.Identity == identity); if (!set.Proved || !model.Unique(set.Id)) throw new ArgumentException("Unsafe or duplicate set IDs remain protected source occurrences. Inspect source.");
        Identifier(id); var patches = new List<(SourceField, string)>();
        if (id != set.Id)
        {
            Structure(model); if (model.Sets.Any(s => s.Identity != identity && s.Id == id)) throw new ArgumentException("That set ID already exists.");
            if (set.LabelToken is null && !renameDefaultLabel) throw new ArgumentException("This set has an absent label that defaults to its ID. Explicitly choose rename default display label with ID, or make the display label explicit first.");
            patches.Add((set.IdToken, id));
            foreach (var reference in model.Unions.SelectMany(u => u.References).Where(r => r.Value == set.Id)) patches.Add((reference, reference.Raw.StartsWith('"') ? "\"" + id + "\"" : id));
        }
        if (set.LabelToken is not null && label != set.Label) { Label(label); patches.Add((set.LabelToken, label)); }
        else if (set.LabelToken is null && label != set.Label && !(renameDefaultLabel && id != set.Id && label == id)) throw new ArgumentException("The display label is absent. Make it explicit before editing a separate display value.");
        Metadata(set.SizeToken, size, patches); var changed = TopologySource.Patch(text, patches); var next = Read(changed);
        if (id != set.Id && next.Unions.SelectMany(u => u.References).Count(r => r.Value == id) != model.Unions.SelectMany(u => u.References).Count(r => r.Value == set.Id)) throw new ArgumentException("The atomic rename did not retain every proved source reference.");
        return changed;
    }, token);
    public Task<SourceAuthoringResult> MakeLabelExplicitAsync(string identity, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        var set = model.Sets.Single(s => s.Identity == identity); if (!set.Proved || !model.Unique(set.Id)) throw new ArgumentException("Choose a unique safe set."); if (set.LabelToken is not null) return text;
        var line = text.Line(identity); var end = set.IdToken.Start + set.IdToken.Length; if (end < line.Body.Length && line.Body[end] == '"') end++;
        return text.Replace(line.Replace(end, 0, "[\"" + set.Id + "\"]"));
    }, token);
    public Task<SourceAuthoringResult> UpdateUnionAsync(string identity, string members, string label, string size, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        var union = model.Unions.Single(u => u.Identity == identity); Structure(model); var patches = new List<(SourceField, string)>();
        if (members != union.MembersToken.Raw) { Members(members, model); patches.Add((union.MembersToken, members)); }
        if (union.LabelToken is not null && label != union.Label) { Label(label); patches.Add((union.LabelToken, label)); }
        else if (union.LabelToken is null && label.Length > 0) throw new ArgumentException("This union label is absent and unrendered. Add explicit label syntax in source first.");
        Metadata(union.SizeToken, size, patches); return TopologySource.Patch(text, patches);
    }, token);
    public Task<SourceAuthoringResult> AddSetAsync(string id, string? label = null, CancellationToken token = default) => ChangeAsync((text, model) =>
    { Structure(model); Identifier(id); if (model.Sets.Any(s => s.Id == id)) throw new ArgumentException("That set ID already exists."); if (label is not null) Label(label); return TopologySource.Append(text, "set " + id + (label is null ? "" : "[\"" + label + "\"]")); }, token);
    public Task<SourceAuthoringResult> AddUnionAsync(string members, string? label = null, CancellationToken token = default) => ChangeAsync((text, model) =>
    { Structure(model); Members(members, model); if (label is not null) Label(label); return TopologySource.Append(text, "union " + members + (label is null ? "" : "[\"" + label + "\"]")); }, token);
    public Task<SourceAuthoringResult> DeleteAsync(string identity, bool removeConnected = false, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        Structure(model); var set = model.Sets.FirstOrDefault(s => s.Identity == identity); var connected = set is null ? [] : model.Unions.Where(u => u.References.Any(r => r.Value == set.Id)).Select(u => u.Identity).ToArray();
        if (connected.Length > 0 && !removeConnected) throw new ArgumentException("This set is referenced by unions. Remove them first or explicitly choose atomic connected removal.");
        if (set is null && !model.Unions.Any(u => u.Identity == identity)) throw new ArgumentException("Select a set or union occurrence.");
        foreach (var key in connected.Append(identity).OrderByDescending(text.Index)) text = ChartSource.DeleteRow(text, key); return text;
    }, token);
    public Task<SourceAuthoringResult> MoveAsync(string identity, int direction, CancellationToken token = default) => ChangeAsync((text, model) =>
    { Structure(model); var set = model.Sets.Any(s => s.Identity == identity); return TopologySource.MoveRows(text, set ? model.Sets.Select(s => s.Identity).ToArray() : model.Unions.Select(u => u.Identity).ToArray(), identity, direction, set ? model.Unions.Select(u => u.Identity) : model.Sets.Select(s => s.Identity)); }, token);
    private static void Metadata(SourceField? field, string size, List<(SourceField, string)> patches)
    { if (field is null) { if (size.Length > 0) throw new ArgumentException("Size metadata is absent. Add its explicit source token first."); } else if (size != field.Raw) { TreemapVisualDraft.Decimal(size); patches.Add((field, size)); } }
    private static void Structure(VennSourceModel model)
    { if (!model.StructureSafe) throw new ArgumentException("Unknown/missing/duplicate IDs, unresolved or repeated union references and unproved quoting protect this operation. Inspect source."); }
    private static void Identifier(string value)
    { if (!SafeId.IsMatch(value) || value.Length > 200) throw new ArgumentException("Use a unique safe ID beginning with a letter or underscore, followed by letters, digits, underscores or hyphens. Commas, escapes and arbitrary quoted IDs remain source-only."); }
    private static void Label(string value)
    { SourceText.Label(value); if (value.IndexOfAny(['"', '<', '>']) >= 0) throw new ArgumentException("Display labels have no proved embedded quote or markup encoding. Pending input is retained."); }
    private static void Members(string raw, VennSourceModel model)
    {
        var ids = raw.Split(',').Select(s => s.Trim()).ToArray();
        if (ids.Length < 2 || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length) throw new ArgumentException("Choose at least two distinct set references.");
        foreach (var id in ids) { Identifier(id); if (!model.Unique(id)) throw new ArgumentException("Every new union reference must name one existing unique safe set ID."); }
    }
}
