using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Core.XyCharts;

namespace LensDocsStudio.Core.RadarCharts;

public sealed record RadarAxisSource(string Identity, string Row, string Id, string Label, SourceToken Token, bool Simple);
public sealed record RadarAxisRow(string Identity, SourceVector Axes);
public sealed record RadarCurveSource(string Identity, string Id, string Label, Match Tokens, SourceVector Values, bool Simple);
public sealed record RadarSourceModel(string HeaderIdentity, SourceDeclaration? Title, IReadOnlyList<RadarAxisSource> Axes, IReadOnlyList<RadarAxisRow> AxisRows,
    IReadOnlyList<RadarCurveSource> Curves, IReadOnlyList<SourceDeclaration> Declarations, IReadOnlyList<string> Protected, IReadOnlyList<string> Errors)
{
    public SourceDeclaration? Effective(string name) => Declarations.LastOrDefault(d => d.Name == name);
    public double Minimum => double.Parse(Effective("min")?.Value ?? "0", CultureInfo.InvariantCulture);
    public double Maximum => Effective("max") is { } max ? double.Parse(max.Value, CultureInfo.InvariantCulture) : Math.Max(5, Curves.SelectMany(c => c.Values.Items).Select(v => SourceVector.Finite(v.Raw, out var n) ? n : 0).DefaultIfEmpty(5).Max());
    public bool Aligned => Axes.Count > 0 && Axes.All(a => a.Simple) && Curves.All(c => c.Simple && c.Values.Items.Count == Axes.Count) &&
        Axes.Select(a => a.Id).Distinct(StringComparer.Ordinal).Count() == Axes.Count && Curves.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() == Curves.Count;
}

public sealed class RadarVisualDraft : SourceDraft<RadarSourceModel>
{
    public const string Starter = "radar-beta\n    title Documentation profiles\n    axis guides[\"Guides\"], reference[\"Reference\"], examples[\"Examples\"], review[\"Review\"]\n    min 0\n    max 10\n    curve current[\"Current\"]{2, 4, 6, 8}\n    curve target[\"Target\"]{8, 6, 4, 2}\n";
    private static readonly Regex Axis = SourceText.Pattern(@"^[ \t]*axis[ \t]+(?<axes>.+?)[ \t]*$");
    private static readonly Regex AxisToken = SourceText.Pattern(@"^(?<id>[^\s,""{}\[\]]+)(?:\[""(?<label>[^""]+)""\])?$");
    private static readonly Regex Curve = SourceText.Pattern(@"^[ \t]*curve[ \t]+(?<id>""[^""]+""|[^\s""{}\[\]]+)(?:\[""(?<label>[^""]+)""\])?[ \t]*\{(?<values>[^{}]+)\}(?<suffix>.*)$");
    private static readonly Regex Declaration = SourceText.Pattern(@"^[ \t]*(?<name>title|min|max|ticks|graticule|showLegend)[ \t]+(?<label>.+?)[ \t]*$");
    public static bool IsRadar(string source) => ChartSource.Header(source).Equals("radar-beta", StringComparison.OrdinalIgnoreCase);
    public RadarVisualDraft(string source, DiagramService? diagrams = null) : base(source, diagrams) { }
    protected override RadarSourceModel Scan(SourceText text)
    {
        var rows = ChartSource.Body(text); var axes = new List<RadarAxisSource>(); var axisRows = new List<RadarAxisRow>(); var curves = new List<RadarCurveSource>();
        var declarations = new List<SourceDeclaration>(); var protect = new List<string>(); var errors = new List<string>(); SourceDeclaration? title = null;
        var header = rows.FirstOrDefault(); if (header is null || !IsRadar(header.Body)) errors.Add("A radar-beta header is required.");
        protect.AddRange(text.Lines.Where(l => l.InitDirective || !rows.Contains(l) && l.Content.Length > 0 && !l.Comment).Select(l => l.Key));
        foreach (var line in rows.Skip(1))
        {
            var a = Axis.Match(line.Body);
            if (a.Success)
            {
                var g = a.Groups["axes"]; var vector = SourceVector.Read(line, g.Index, g.Length); axisRows.Add(new(line.Key, vector));
                foreach (var item in vector.Items)
                {
                    if (item.Raw.Length == 0) { protect.Add(line.Key); continue; }
                    var match = AxisToken.Match(item.Raw); var bracket = item.Raw.IndexOf('[');
                    var id = bracket < 0 ? item.Raw : item.Raw[..bracket].Trim();
                    var label = bracket < 0 ? item.Raw : item.Raw[(bracket + 1)..].TrimEnd(']').Trim().Trim('"');
                    axes.Add(new(item.Identity, line.Key, id, label, item, match.Success)); if (!match.Success) protect.Add(line.Key);
                }
                continue;
            }
            var c = Curve.Match(line.Body);
            if (c.Success)
            {
                var v = c.Groups["values"]; var vector = SourceVector.Read(line, v.Index, v.Length);
                var simple = c.Groups["suffix"].Value.Trim().Length == 0 && vector.Items.All(i => SourceVector.Finite(i.Raw, out _));
                var id = c.Groups["id"].Value.Trim('"'); curves.Add(new(line.Key, id, c.Groups["label"].Success ? c.Groups["label"].Value : id, c, vector, simple));
                if (!simple) protect.Add(line.Key); continue;
            }
            var d = Declaration.Match(line.Body);
            if (d.Success)
            {
                var name = d.Groups["name"].Value; var value = d.Groups["label"].Value;
                var supported = name switch {
                    "min" or "max" => SourceText.Pattern(@"^[0-9]+(?:\.[0-9]+)?$").IsMatch(value) && SourceVector.Finite(value, out _),
                    "ticks" => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) && ticks is >= 1 and <= 100,
                    "graticule" => value.Equals("circle", StringComparison.OrdinalIgnoreCase) || value.Equals("polygon", StringComparison.OrdinalIgnoreCase),
                    "showLegend" => value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("false", StringComparison.OrdinalIgnoreCase), _ => true };
                if (supported) { var declaration = new SourceDeclaration(line.Key, name, value, d); declarations.Add(declaration); if (name == "title") title = declaration; }
                else { protect.Add(line.Key); if (name == "ticks") errors.Add("Radar tick controls exceed the supported 1–100 resource budget. Correct the original source."); }
                continue;
            }
            protect.Add(line.Key);
        }
        if (axes.Count + curves.Sum(c => c.Values.Items.Count) > 500) errors.Add("Radar exceeds the 500-element budget.");
        return new(header?.Key ?? "", title, axes, axisRows, curves, declarations, protect.Distinct().ToArray(), errors);
    }
    protected override bool Contains(RadarSourceModel m, string id) => id == m.HeaderIdentity || id == m.HeaderIdentity + ":range" || m.Axes.Any(a => a.Identity == id) || m.Curves.Any(c => c.Identity == id || c.Values.Items.Any(v => v.Identity == id)) || m.Declarations.Any(d => d.Identity == id) || m.Protected.Contains(id);
    protected override string? SourceProblem(RadarSourceModel model) => model.Errors.FirstOrDefault();
    protected override void Validate(SourceText text, RadarSourceModel m, DiagramResult render)
    {
        if (m.Errors.Count > 0) throw new ArgumentException(m.Errors[0]);
        if (!render.Success || render.Source != text.Source) throw new ArgumentException(render.Error ?? "Radar source did not render.");
        VerifyCanonical(m, render.Svg!);
    }
    public static void VerifyCanonical(RadarSourceModel m, string svg)
    {
        if (m.Axes.Count > 0) SourceDeclarations.VerifyTitle(svg, m.Title);
        var curves = SourceDeclarations.Groups(svg, "radar-curve");
        // Unsupported historical vectors remain protected, with exact original source accessible.
        if (!double.IsFinite(m.Maximum - m.Minimum)) throw new ArgumentException("The Radar display range must be finite. Correct the original source.");
        if (m.Curves.All(c => c.Simple))
        {
            var expected = m.Maximum > m.Minimum && m.Axes.Count > 0 ? m.Curves.Count : 0;
            if (curves.Length != expected) throw new ArgumentException("Canonical Radar curve occurrences differ from source. Apply is protected.");
            for (var i = 0; i < curves.Length; i++)
            {
                if ((string?)curves[i].Attribute("data-curve") != m.Curves[i].Id || curves[i].Elements().Count(e => e.Name.LocalName == "circle") != m.Axes.Count)
                    throw new ArgumentException("Canonical Radar positional marks or curve order differ from source. Apply is protected.");
                var grid = SourceDeclarations.Groups(svg, "radar-grid").Single(); var spoke = grid.Elements().First(e => e.Name.LocalName == "line");
                var cx = ChartSource.Attribute(spoke, "x1"); var cy = ChartSource.Attribute(spoke, "y1");
                var radius = Math.Abs(cy - ChartSource.Attribute(spoke, "y2"));
                var marks = curves[i].Elements().Where(e => e.Name.LocalName == "circle").ToArray();
                for (var axis = 0; axis < marks.Length; axis++)
                {
                    var value = axis < m.Curves[i].Values.Items.Count ? double.Parse(m.Curves[i].Values.Items[axis].Raw, CultureInfo.InvariantCulture) : 0;
                    var r = radius * Math.Clamp((value - m.Minimum) / (m.Maximum - m.Minimum), 0, 1); var angle = 2 * Math.PI * axis / marks.Length - Math.PI / 2;
                    if (Math.Abs(ChartSource.Attribute(marks[axis], "cx") - cx - r * Math.Cos(angle)) > .02 || Math.Abs(ChartSource.Attribute(marks[axis], "cy") - cy - r * Math.Sin(angle)) > .02)
                        throw new ArgumentException("Canonical Radar radius or positional axis correspondence differs from source. Apply is protected.");
                }
            }
            if (m.Axes.Count > 0)
            {
                var labels = SourceDeclarations.Groups(svg, "radar-grid").Single().Elements().Where(e => e.Name.LocalName == "text").Select(e => SourceDeclarations.TextValue(e.Value));
                if (!labels.SequenceEqual(m.Axes.Select(a => SourceDeclarations.TextValue(a.Label)))) throw new ArgumentException("Canonical Radar axis labels or ordering differ from source.");
            }
        }
    }
    public Task<SourceAuthoringResult> SetTitleAsync(string value, CancellationToken token = default) => ChangeAsync((t, m) => ChartSource.Title(t, m.HeaderIdentity, m.Title, value), token);
    public Task<SourceAuthoringResult> UpdateAxisAsync(string identity, string id, string label, CancellationToken token = default) => ChangeAsync((t, m) =>
    {
        var a = m.Axes.Single(a => a.Identity == identity); if (!a.Simple) throw new ArgumentException("This axis has unproved boundaries; inspect its original source.");
        if (id == a.Id && label == a.Label) return t; Name(id, false); Name(label, true);
        if (id != a.Id && m.Axes.Any(other => other.Identity != identity && other.Id == id)) throw new ArgumentException("An existing axis has this ID. Choose a distinct ID; pending input is retained.");
        var row = m.AxisRows.Single(r => r.Identity == a.Row); return t.Replace(row.Axes.Patch(t.Line(a.Row), identity, AxisText(id, label)));
    }, token);
    public Task<SourceAuthoringResult> UpdateCurveAsync(string identity, string id, string label, CancellationToken token = default) => ChangeAsync((t, m) =>
    {
        var c = m.Curves.Single(c => c.Identity == identity); Simple(c); if (id == c.Id && label == c.Label) return t; Name(id, true); Name(label, true);
        if (id != c.Id && m.Curves.Any(other => other.Identity != identity && other.Id == id)) throw new ArgumentException("An existing curve has this ID. Choose a distinct ID; pending input is retained.");
        var line = t.Line(identity); var name = c.Tokens.Groups["id"]; var oldLabel = c.Tokens.Groups["label"];
        if (label != c.Label)
        { if (oldLabel.Success) line = line.Replace(oldLabel.Index, oldLabel.Length, label); else line = line.Replace(name.Index + name.Length, 0, "[\"" + label + "\"]"); }
        if (id != c.Id) line = line.Replace(name.Index, name.Length, "\"" + id + "\""); return t.Replace(line);
    }, token);
    public Task<SourceAuthoringResult> UpdateValueAsync(string identity, string value, CancellationToken token = default) => ChangeAsync((t, m) =>
    {
        var c = m.Curves.Single(c => c.Values.Items.Any(v => v.Identity == identity)); Simple(c); SourceVector.Number(value);
        return t.Replace(c.Values.Patch(t.Line(c.Identity), identity, value));
    }, token);
    public Task<SourceAuthoringResult> SetRangeAsync(string min, string max, CancellationToken token = default) => ChangeAsync((t, m) =>
    {
        var lo = SourceVector.Number(min, directive: true); var hi = max.Length > 0 ? SourceVector.Number(max, directive: true) : Math.Max(5, m.Curves.SelectMany(c => c.Values.Items).Select(v => SourceVector.Finite(v.Raw, out var n) ? n : 0).DefaultIfEmpty(5).Max());
        if (hi <= lo || !double.IsFinite(hi - lo)) throw new ArgumentException("The Radar maximum must exceed its minimum with a finite positive range. Pending input is retained.");
        foreach (var name in new[] { "min", "max" })
        {
            var value = name == "min" ? min : max; var old = m.Effective(name);
            if (name == "max" && value.Length == 0)
            { foreach (var d in m.Declarations.Where(d => d.Name == "max").Reverse()) t = ChartSource.DeleteRow(t, d.Identity); }
            else if (old is null) t = t.Insert(t.Index(m.HeaderIdentity) + 1, "    " + name + " " + value);
            else if (old.Value != value) t = t.Patch(old.Identity, old.Tokens.Groups["label"], value);
        }
        return t;
    }, token);
    public Task<SourceAuthoringResult> AddCurveAsync(string id, string label, string initial, CancellationToken token = default) => ChangeAsync((t, m) =>
    {
        Name(id, true); Name(label, true); SourceVector.Number(initial);
        if (m.Axes.Count == 0 || m.Curves.Any(c => c.Id == id)) throw new ArgumentException("Add axes first and choose a distinct curve ID.");
        return t.Insert(m.Curves.Count == 0 ? t.Lines.Count : t.Index(m.Curves[^1].Identity) + 1, "    curve \"" + id + "\"[\"" + label + "\"]{" + string.Join(", ", Enumerable.Repeat(initial, m.Axes.Count)) + "}");
    }, token);
    public Task<SourceAuthoringResult> AddAxisAsync(string id, string label, string initial, CancellationToken token = default) => ChangeAsync((t, m) =>
    {
        Aligned(m); Name(id, false); Name(label, true); SourceVector.Number(initial);
        if (m.Axes.Any(a => a.Id == id)) throw new ArgumentException("An existing axis has this ID. Choose a distinct ID.");
        var last = m.AxisRows[^1]; t = t.Replace(last.Axes.Add(t.Line(last.Identity), AxisText(id, label)));
        foreach (var c in m.Curves) t = t.Replace(c.Values.Add(t.Line(c.Identity), initial)); return t;
    }, token);
    public Task<SourceAuthoringResult> DeleteAsync(string id, CancellationToken token = default) => ChangeAsync((t, m) => Transform(t, m, id, 0), token, Model.HeaderIdentity);
    public Task<SourceAuthoringResult> MoveAsync(string id, int direction, CancellationToken token = default) => ChangeAsync((t, m) => Transform(t, m, id, Math.Sign(direction)), token);
    private static SourceText Transform(SourceText t, RadarSourceModel m, string id, int direction)
    {
        var c = m.Curves.FirstOrDefault(c => c.Identity == id);
        if (c is not null) { Simple(c); return direction == 0 ? ChartSource.DeleteRow(t, id) : ChartSource.MoveRow(t, m.Curves.Select(c => c.Identity).ToArray(), id, direction); }
        var axis = m.Axes.FirstOrDefault(a => a.Identity == id) ?? throw new ArgumentException("Select an axis or curve. Individual value moves would break axis correspondence and are protected.");
        Aligned(m); var at = m.Axes.ToList().IndexOf(axis); var next = at + direction;
        if (direction == 0 && m.Axes.Count <= 3) throw new ArgumentException("Keep at least three axes for this editable slice.");
        if (direction != 0 && (next < 0 || next >= m.Axes.Count)) throw new ArgumentException("There is no axis in that direction.");
        var row = m.AxisRows.Single(r => r.Identity == axis.Row); var local = row.Axes.Items.ToList().FindIndex(i => i.Identity == id);
        if (direction == 0 && row.Axes.Items.Count == 1) t = ChartSource.DeleteRow(t, row.Identity);
        else if (direction == 0 || m.Axes[next].Row == axis.Row) t = t.Replace(XyVisualDraft.ListChange(t.Line(row.Identity), row.Axes, local, direction));
        else throw new ArgumentException("Moving an axis across declarations has unproved comment attachment. Use Edit source; every curve is unchanged.");
        foreach (var curve in m.Curves) t = t.Replace(XyVisualDraft.ListChange(t.Line(curve.Identity), curve.Values, at, direction)); return t;
    }
    private static string AxisText(string id, string label) => id + "[\"" + label + "\"]";
    private static void Name(string value, bool spaces)
    {
        SourceText.Label(value);
        if (value.IndexOfAny([',', '{', '}', '[', ']', '"']) >= 0 || !spaces && value.Any(char.IsWhiteSpace)) throw new ArgumentException("This Radar name contains an unsupported separator or ID whitespace. The pinned grammar has no escape here; pending input is retained.");
    }
    private static void Simple(RadarCurveSource curve) { if (!curve.Simple) throw new ArgumentException("This curve contains dropped, keyed, non-finite or ignored tokens. Visual edits are protected; inspect the exact source."); }
    private static void Aligned(RadarSourceModel m) { if (!m.Aligned || m.AxisRows.Any(r => m.Protected.Contains(r.Identity))) throw new ArgumentException("Axis changes require simple axes, distinct IDs and an exact value for every axis in every curve. Ragged, extra or ignored tokens are preserved without repair."); }
}
