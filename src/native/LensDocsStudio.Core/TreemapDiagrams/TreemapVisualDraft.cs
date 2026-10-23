using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;

namespace LensDocsStudio.Core.TreemapDiagrams;

public sealed record TreemapSourceNode(string Identity, SourceField LabelToken, SourceField? ValueToken, double? ExplicitValue, double ComputedValue, bool Proved)
{ public string Label => LabelToken.Value; }
public sealed record TreemapSourceModel(string HeaderIdentity, IReadOnlyList<TreemapSourceNode> Nodes, IReadOnlyList<SourceHierarchyRow> Hierarchy)
{ public bool StructureSafe => Nodes.All(n => n.Proved); }

/// <summary>Quoted source hierarchy and explicit decimal tokens; derived totals and native squarification stay read-only.</summary>
public sealed class TreemapVisualDraft : SourceDraft<TreemapSourceModel>
{
    public const string Starter = "treemap-beta\n\"Documentation\"\n  \"Guides\": 030.50\n  \"Reference\": 20\n  \"Planned\": 0\n\"Tests\"\n  \"Native\": 40\n  \"Browser\": 10\n";
    private static readonly Regex Section = SourceText.Pattern("^[ \\t]*\"(?<label>[^\"]+)\"[ \\t]*$");
    public static bool IsTreemap(string source) => ChartSource.Header(source).Equals("treemap-beta", StringComparison.OrdinalIgnoreCase);
    public TreemapVisualDraft(string source, DiagramService? diagrams = null) : base(source, diagrams) { }
    protected override TreemapSourceModel Scan(SourceText text) => Read(text);
    public static TreemapSourceModel Read(SourceText text)
    {
        var body = ChartSource.Body(text); if (body.Count == 0 || !IsTreemap(text.Source)) throw new ArgumentException("This editor requires treemap-beta source.");
        var nodes = new List<TreemapSourceNode>(); var rows = new List<(string, int)>();
        foreach (var line in body.Skip(1))
        {
            var match = ExpansionViSource.TreemapLeaf.Match(line.Body); var leaf = match.Success; if (!leaf) match = Section.Match(line.Body);
            var label = match.Success ? WorkflowSource.Field(line, "label", match.Groups["label"]) : new SourceField(line.Key, "label", line.Content.Trim('"'), line.Indent.Length, line.Content.Length, line.Content);
            var numeric = leaf ? WorkflowSource.Field(line, "value", match.Groups["value"]) : null; double? value = numeric is null ? null : double.TryParse(numeric.Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) ? number : double.PositiveInfinity;
            nodes.Add(new(line.Key, label, numeric, value, value ?? 0, match.Success && !label.Value.Contains('<') && !label.Value.Contains('>'))); rows.Add((line.Key, line.Indent.Length));
        }
        var hierarchy = SourceHierarchy.Link(rows); var computed = nodes.ToDictionary(n => n.Identity, n => n.ExplicitValue ?? 0);
        foreach (var row in hierarchy.Reverse()) if (row.Parent is not null && nodes.Single(n => n.Identity == row.Parent).ExplicitValue is null) computed[row.Parent] += computed[row.Identity];
        return new(body[0].Key, nodes.Select(n => n with { ComputedValue = computed[n.Identity] }).ToArray(), hierarchy);
    }
    protected override bool Contains(TreemapSourceModel model, string identity) => identity == model.HeaderIdentity || model.Nodes.Any(n => n.Identity == identity);
    protected override void Validate(SourceText text, TreemapSourceModel model, DiagramResult rendered)
    {
        WorkflowSource.Rendered(text, rendered);
        // Renderer may omit a positive node when its actual tile is too small. Require an ordered source subsequence,
        // never invent a tile or assign duplicate labels to an arbitrary visible rectangle.
        var at = 0; var positive = model.Nodes.Where(n => n.ComputedValue > 0).ToArray();
        foreach (var shape in Shapes(rendered.Svg!))
        {
            var label = (string?)shape.Attribute("data-label");
            while (at < positive.Length && positive[at].Label != label) at++;
            if (at == positive.Length) throw new ArgumentException("Native Treemap tile order/labels do not match the preserved parsed hierarchy. Apply is protected.");
            var node = positive[at++]; var section = model.Hierarchy.Any(r => r.Parent == node.Identity);
            if (HasClass(shape, "treemap-section") != section || !section && (string?)shape.Attribute("data-value") != node.ComputedValue.ToString("0.##", CultureInfo.InvariantCulture))
                throw new ArgumentException("Native Treemap explicit/derived values or section semantics differ. Inspect source; Apply is protected.");
        }
    }
    private static bool HasClass(XElement e, string value) => ((string?)e.Attribute("class") ?? "").Split(' ').Contains(value);
    private static XElement[] Shapes(string svg) => XDocument.Parse(svg).Descendants().Where(e => HasClass(e, "treemap-section") || HasClass(e, "treemap-tile")).ToArray();
    public static IReadOnlyList<(TreemapSourceNode Node, XElement Shape)> Correspondence(TreemapSourceModel model, string svg)
        => Shapes(svg).Select(s => (Shape: s, Nodes: model.Nodes.Where(n => n.Label == (string?)s.Attribute("data-label")).ToArray())).Where(p => p.Nodes.Length == 1 && p.Nodes[0].Proved && p.Nodes[0].ComputedValue > 0).Select(p => (p.Nodes[0], p.Shape)).ToArray();
    public Task<SourceAuthoringResult> UpdateAsync(string identity, string label, string value, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        var node = model.Nodes.Single(n => n.Identity == identity); if (!node.Proved) throw new ArgumentException("Raw fallback labels are preserved source-only; quote their intended grammar explicitly in source.");
        var patches = new List<(SourceField, string)>(); if (label != node.Label) { Label(label); patches.Add((node.LabelToken, label)); }
        if (node.ValueToken is null && value.Length > 0) throw new ArgumentException("A section's total is derived. Add explicit leaf syntax in source; derived values are never written back.");
        if (node.ValueToken is { } field && value != field.Raw) { Decimal(value); patches.Add((field, value)); }
        return TopologySource.Patch(text, patches);
    }, token);
    public Task<SourceAuthoringResult> AddAsync(string? parent, string label, string? value, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        Structure(text, model); Label(label); if (value is not null) Decimal(value);
        if (parent is not null && model.Nodes.Single(n => n.Identity == parent).ValueToken is not null) throw new ArgumentException("Choose a derived section parent; explicit leaf values must not hide child totals.");
        var indent = parent is null ? model.Hierarchy.FirstOrDefault()?.Indent ?? 0 : model.Hierarchy.Single(r => r.Identity == parent).Indent + 2;
        var at = parent is null ? text.Lines.Count : SourceHierarchy.Bounds(text, model.Hierarchy, parent).End; SourceSections.CheckTail(text, 0, at);
        var changed = text.Insert(at, new string(' ', indent) + "\"" + label + "\"" + (value is null ? "" : ": " + value)); var next = Read(changed); SourceHierarchy.ProveMembership(model.Hierarchy, next.Hierarchy);
        if (next.Hierarchy.Single(r => !model.Hierarchy.Any(old => old.Identity == r.Identity)).Parent != parent) throw new ArgumentException("The new node would have an unintended parent."); return changed;
    }, token);
    public Task<SourceAuthoringResult> DeleteAsync(string identity, CancellationToken token = default) => ChangeAsync((text, model) => { Structure(text, model); return SourceHierarchy.Delete(text, model.Hierarchy, identity); }, token);
    public Task<SourceAuthoringResult> MoveAsync(string identity, int direction, CancellationToken token = default) => ChangeAsync((text, model) =>
    { Structure(text, model); var changed = SourceHierarchy.Move(text, model.Hierarchy, identity, direction); SourceHierarchy.ProveMembership(model.Hierarchy, Read(changed).Hierarchy); return changed; }, token);
    public Task<SourceAuthoringResult> ReparentAsync(string identity, string? parent, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        Structure(text, model); if (parent is not null && model.Nodes.Single(n => n.Identity == parent).ValueToken is not null) throw new ArgumentException("An explicit-value leaf is not a derived section parent.");
        var indent = parent is null ? model.Hierarchy.Min(r => r.Indent) : model.Hierarchy.Single(r => r.Identity == parent).Indent + 2;
        var changed = SourceHierarchy.Reparent(text, model.Hierarchy, identity, parent, indent); SourceHierarchy.ProveMembership(model.Hierarchy, Read(changed).Hierarchy, identity, parent); return changed;
    }, token);
    private static void Structure(SourceText text, TreemapSourceModel model)
    {
        if (!model.StructureSafe) throw new ArgumentException("Raw fallback labels protect subtree structure. Inspect source.");
        if (model.Nodes.Any(n => text.Line(n.Identity).Indent.Contains('\t'))) throw new ArgumentException("Tab/mixed indentation remains exact. Structural re-encoding is protected; tabs count as one character here, not four.");
    }
    public static void Decimal(string raw)
    {
        var significant = raw.Replace(".", "", StringComparison.Ordinal).TrimStart('0'); var dot = raw.IndexOf('.');
        if (!SourceText.Pattern(@"^[0-9]+(?:\.[0-9]+)?$").IsMatch(raw) || significant.Length > 15 || dot >= 0 && raw.Length - dot - 1 > 15 || !double.TryParse(raw, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value) || value < 0)
            throw new ArgumentException("Enter a complete non-negative decimal with at most 15 significant digits and 15 decimal places. Signs, exponent notation, incomplete values and overflow/unproved precision are protected.");
    }
    private static void Label(string value)
    { SourceText.Label(value); if (value.IndexOfAny(['"', '<', '>']) >= 0) throw new ArgumentException("Quoted Treemap labels have no proved embedded quote or markup encoding. Pending input is retained."); }
}
