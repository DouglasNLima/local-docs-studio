using System.Globalization;
using System.Xml.Linq;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;

namespace LensDocsStudio.Core.SankeyDiagrams;

public sealed record SankeyLinkSource(string Identity, string Source, string Target, string Weight, double Value, SourceVector Fields, bool Simple);
public sealed record SankeySourceModel(string HeaderIdentity, IReadOnlyList<SankeyLinkSource> Links, IReadOnlyList<string> Protected, IReadOnlyList<string> Errors)
{
    public IReadOnlyList<string> Nodes => Links.SelectMany(l => new[] { l.Source, l.Target }).Distinct(StringComparer.Ordinal).ToArray();
    public bool Cyclic
    {
        get
        {
            var edges = Links.GroupBy(l => l.Source, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Select(l => l.Target).ToArray(), StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal); var active = new HashSet<string>(StringComparer.Ordinal);
            bool Visit(string node)
            { if (active.Contains(node)) return true; if (!visited.Add(node)) return false; active.Add(node); foreach (var target in edges.GetValueOrDefault(node, [])) if (Visit(target)) return true; active.Remove(node); return false; }
            return Nodes.Any(Visit);
        }
    }
}

public sealed class SankeyVisualDraft : SourceDraft<SankeySourceModel>
{
    public const string Starter = "sankey-beta\n    Draft,Review,3.000\n    Draft,Review,2.000\n    Review,Published,5.000\n    Research,Published,2.000\n";
    public static bool IsSankey(string source) => ChartSource.Header(source).Equals("sankey-beta", StringComparison.OrdinalIgnoreCase);
    public SankeyVisualDraft(string source, DiagramService? diagrams = null) : base(source, diagrams) { }
    protected override SankeySourceModel Scan(SourceText text)
    {
        var rows = ChartSource.Body(text); var header = rows.FirstOrDefault(); var links = new List<SankeyLinkSource>(); var protect = new List<string>(); var errors = new List<string>();
        if (header is null || !IsSankey(header.Body)) errors.Add("A sankey-beta header is required. Other header options are preserved through Edit source.");
        protect.AddRange(text.Lines.Where(l => l.InitDirective || !rows.Contains(l) && l.Content.Length > 0 && !l.Comment).Select(l => l.Key));
        foreach (var line in rows.Skip(1))
        {
            var fields = SourceVector.Read(line, line.Indent.Length, line.Body.Length - line.Indent.Length, true);
            if (fields.Items.Count < 3) { protect.Add(line.Key); continue; }
            var source = fields.Items[0].Value; var target = fields.Items[1].Value; var weight = fields.Items[2].Value;
            if (source.Length == 0 || target.Length == 0 || !double.TryParse(weight, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number) || number <= 0)
            { protect.Add(line.Key); continue; }
            var simple = fields.Balanced && SourceVector.Finite(weight, out _);
            links.Add(new(line.Key, source, target, weight, number, fields, simple)); if (!simple) protect.Add(line.Key);
        }
        if (links.Count + links.SelectMany(l => new[] { l.Source, l.Target }).Distinct().Count() > 500) errors.Add("Sankey exceeds the 500-element budget.");
        if (links.GroupBy(l => l.Source).Any(g => !double.IsFinite(g.Sum(l => l.Value))) || links.GroupBy(l => l.Target).Any(g => !double.IsFinite(g.Sum(l => l.Value)))) errors.Add("Sankey endpoint totals exceed the finite range. Correct the original source.");
        return new(header?.Key ?? "", links, protect.Distinct().ToArray(), errors);
    }
    protected override bool Contains(SankeySourceModel m, string id) => id == m.HeaderIdentity || m.Links.Any(l => l.Identity == id) || m.Protected.Contains(id) || m.Nodes.Any(n => NodeIdentity(m, n) == id);
    protected override string? SourceProblem(SankeySourceModel model) => model.Errors.FirstOrDefault();
    public static string NodeIdentity(SankeySourceModel m, string node) => m.Links.First(l => l.Source == node || l.Target == node) is { } first ? first.Identity + (first.Source == node ? ":source-node" : ":target-node") : throw new ArgumentException("Select an existing node.");
    protected override void Validate(SourceText text, SankeySourceModel m, DiagramResult render)
    {
        if (m.Errors.Count > 0) throw new ArgumentException(m.Errors[0]);
        if (!render.Success || render.Source != text.Source) throw new ArgumentException(render.Error ?? "Sankey source did not render.");
        Correspondence(m, render.Svg!);
    }
    public static IReadOnlyList<(SankeyLinkSource Link, XElement Shape)> Correspondence(SankeySourceModel m, string svg)
    {
        var nodes = SourceDeclarations.Groups(svg, "sankey-node"); var ribbons = SourceDeclarations.Groups(svg, "sankey-link");
        if (!nodes.Select(n => (string?)n.Attribute("data-node")).SequenceEqual(m.Nodes)) throw new ArgumentException("Canonical Sankey node identities or first-appearance order differ from source. Apply is protected.");
        var rectangles = nodes.ToDictionary(n => (string)n.Attribute("data-node")!, n => n.Elements().First(), StringComparer.Ordinal);
        var rendered = m.Links.Where(l => l.Source != l.Target && ChartSource.Attribute(rectangles[l.Target], "x") > ChartSource.Attribute(rectangles[l.Source], "x"))
            .OrderBy(l => ChartSource.Attribute(rectangles[l.Source], "x")).ThenBy(l => l.Source).ThenBy(l => l.Target).ToArray();
        if (rendered.Length != ribbons.Length) throw new ArgumentException("Canonical Sankey ribbon occurrences differ from proved endpoint layers. Apply is protected.");
        for (var i = 0; i < ribbons.Length; i++)
        {
            var l = rendered[i]; var numbers = SourceText.Pattern(ChartSource.Numeric).Matches((string?)ribbons[i].Attribute("d") ?? "").Select(v => double.Parse(v.Value, CultureInfo.InvariantCulture)).ToArray();
            var s = rectangles[l.Source]; var t = rectangles[l.Target];
            var sourceHeight = ChartSource.Attribute(s, "height") * (l.Value / Math.Max(m.Links.Where(x => x.Source == l.Source).Sum(x => x.Value), 1e-9));
            var targetHeight = ChartSource.Attribute(t, "height") * (l.Value / Math.Max(m.Links.Where(x => x.Target == l.Target).Sum(x => x.Value), 1e-9));
            if (numbers.Length != 16 || Math.Abs(numbers[0] - ChartSource.Attribute(s, "x") - ChartSource.Attribute(s, "width")) > .025 ||
                Math.Abs(numbers[8] - ChartSource.Attribute(t, "x")) > .025 || Math.Abs(numbers[15] - numbers[1] - sourceHeight) > .035 || Math.Abs(numbers[9] - numbers[7] - targetHeight) > .035)
                throw new ArgumentException("Canonical Sankey ribbon endpoints, occurrence order or weight proportions differ from source. Apply is protected.");
        }
        return rendered.Select((l, i) => (l, ribbons[i])).ToArray();
    }
    public Task<SourceAuthoringResult> UpdateAsync(string id, string source, string target, string weight, CancellationToken token = default) => ChangeAsync((text, m) =>
    {
        Topology(m); var link = m.Links.Single(l => l.Identity == id); Simple(link); var line = text.Line(id);
        SourceText.Label(source); SourceText.Label(target); SourceVector.Number(weight, positive: true);
        if (weight != link.Weight) line = link.Fields.Patch(line, link.Fields.Items[2].Identity, weight);
        if (target != link.Target) line = link.Fields.Patch(line, link.Fields.Items[1].Identity, SourceVector.Quote(target));
        if (source != link.Source) line = link.Fields.Patch(line, link.Fields.Items[0].Identity, SourceVector.Quote(source));
        var candidate = text.Replace(line); Acyclic(Scan(candidate)); return candidate;
    }, token);
    public Task<SourceAuthoringResult> AddAsync(string source, string target, string weight, CancellationToken token = default) => ChangeAsync((text, m) =>
    {
        Topology(m); SourceText.Label(source); SourceText.Label(target); SourceVector.Number(weight, positive: true);
        var candidate = text.Insert(m.Links.Count == 0 ? text.Lines.Count : text.Index(m.Links[^1].Identity) + 1, "    " + SourceVector.Quote(source) + "," + SourceVector.Quote(target) + "," + weight);
        Acyclic(Scan(candidate)); return candidate;
    }, token);
    public Task<SourceAuthoringResult> DeleteAsync(string id, CancellationToken token = default) => ChangeAsync((text, m) =>
    { Topology(m); Simple(m.Links.Single(l => l.Identity == id)); return ChartSource.DeleteRow(text, id); }, token, Model.HeaderIdentity);
    public Task<SourceAuthoringResult> MoveAsync(string id, int direction, CancellationToken token = default) => ChangeAsync((text, m) =>
    { Topology(m); Simple(m.Links.Single(l => l.Identity == id)); return ChartSource.MoveRow(text, m.Links.Select(l => l.Identity).ToArray(), id, direction); }, token);
    public Task<SourceAuthoringResult> RenameNodeAsync(string old, string name, CancellationToken token = default) => ChangeAsync((text, m) =>
    {
        Topology(m); SourceText.Label(name); if (!m.Nodes.Contains(old)) throw new ArgumentException("Select an existing node."); if (name == old) return text;
        if (m.Nodes.Contains(name, StringComparer.Ordinal)) throw new ArgumentException("This rename would merge existing node identities. Choose a distinct name; pending input is retained.");
        if (m.Protected.Any(id => !text.Line(id).InitDirective)) throw new ArgumentException("Unproved or ignored rows protect a global rename. A local endpoint edit still changes only its selected row.");
        foreach (var link in m.Links.Where(l => l.Source == old || l.Target == old))
        {
            Simple(link); var line = text.Line(link.Identity);
            if (link.Target == old) line = link.Fields.Patch(line, link.Fields.Items[1].Identity, SourceVector.Quote(name));
            if (link.Source == old) line = link.Fields.Patch(line, link.Fields.Items[0].Identity, SourceVector.Quote(name)); text = text.Replace(line);
        }
        return text;
    }, token);
    private static void Simple(SankeyLinkSource link) { if (!link.Simple) throw new ArgumentException("This CSV row has unproved quoting or numeric boundaries. Visual field edits are protected; inspect the exact source."); }
    private static void Topology(SankeySourceModel m) { if (m.Errors.Count > 0) throw new ArgumentException(m.Errors[0]); Acyclic(m); }
    private static void Acyclic(SankeySourceModel m) { if (m.Cyclic) throw new ArgumentException("The pinned renderer may omit self-links and feedback ribbons. This cyclic topology is preserved; visual edits affecting it are protected. Use Edit source."); }
}
