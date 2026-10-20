using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;

namespace LensDocsStudio.Core.XyCharts;

public sealed record XyAxisSource(string Identity, string Name, Match Tokens, string? Title, SourceVector? Categories, Match? Range);
public sealed record XySeriesSource(string Identity, string Kind, string Name, Match Tokens, SourceVector Values, bool Simple);
public sealed record XySourceModel(string HeaderIdentity, Match Header, SourceDeclaration? Title, IReadOnlyList<XyAxisSource> Axes,
    IReadOnlyList<XySeriesSource> Series, IReadOnlyList<string> Protected, IReadOnlyList<string> Errors)
{
    public bool Horizontal => Header.Groups["orientation"].Value.Equals("horizontal", StringComparison.OrdinalIgnoreCase);
    public XyAxisSource? Categories => Axes.LastOrDefault(a => a.Categories is not null);
    public XyAxisSource? Range => Axes.LastOrDefault(a => a.Name == "y-axis" && a.Range is not null);
    public string AxisTitle(string name) => Axes.LastOrDefault(a => a.Name == name && a.Title is not null)?.Title ?? "";
    public bool Aligned => Categories?.Categories is { Balanced: true } c && c.Items.All(i => i.Value.Length > 0) && Series.Count > 0 && Series.All(s => s.Simple && s.Values.Items.Count == c.Items.Count);
}

public sealed class XyVisualDraft : SourceDraft<XySourceModel>
{
    public const string Starter = "xychart-beta\n    title Documentation comparisons\n    x-axis \"Category\" [Guides, Reference, Examples]\n    y-axis \"Coverage\" -10 --> 10\n    bar \"Current\" [-5, 0, 8]\n    line \"Target\" [2, 5, 10]\n";
    private static readonly Regex HeaderPattern = SourceText.Pattern(@"^[ \t]*(?<keyword>xychart(?:-beta)?)(?:[ \t]+(?<orientation>horizontal|vertical))?(?:[ \t]+title[ \t]+(?<label>.+?))?[ \t]*$", RegexOptions.IgnoreCase);
    private static readonly Regex SeriesPattern = SourceText.Pattern(@"^[ \t]*(?<kind>bar|line|box)(?:[ \t]+(?<name>.+?))?[ \t]*\[(?<values>.+)\][ \t]*$", RegexOptions.IgnoreCase);
    private static readonly Regex AxisPattern = SourceText.Pattern(@"^[ \t]*(?<name>x-axis|y-axis)[ \t]+(?<body>.+?)[ \t]*$");
    private static readonly Regex TitlePattern = SourceText.Pattern(@"^[ \t]*title[ \t]+(?<label>.+?)[ \t]*$");
    private static readonly Regex RangePattern = SourceText.Pattern(@"^(?:(?<title>""[^""]*""|.+?)[ \t]+)?(?<min>" + ChartSource.Numeric + @")[ \t]+-->[ \t]+(?<max>" + ChartSource.Numeric + @")$");
    public static bool IsXy(string source) => SourceText.Pattern(@"^xychart(?:-beta)?(?:\s|$)", RegexOptions.IgnoreCase).IsMatch(ChartSource.Header(source));
    public XyVisualDraft(string source, DiagramService? diagrams = null) : base(source, diagrams) { }
    protected override XySourceModel Scan(SourceText text)
    {
        var rows = ChartSource.Body(text); var axes = new List<XyAxisSource>(); var series = new List<XySeriesSource>(); var protect = new List<string>(); var errors = new List<string>();
        var first = rows.FirstOrDefault(); var header = HeaderPattern.Match(first?.Body ?? ""); SourceDeclaration? title = null;
        if (!header.Success) errors.Add("Unsupported XY Chart header. Inspect its original source.");
        if (header.Groups["label"].Success) title = new(first!.Key, "title", ChartSource.Unquote(header.Groups["label"].Value), header);
        protect.AddRange(text.Lines.Where(l => l.InitDirective || !rows.Contains(l) && l.Content.Length > 0 && !l.Comment).Select(l => l.Key));
        foreach (var line in rows.Skip(1))
        {
            var t = TitlePattern.Match(line.Body); if (t.Success) { title = new(line.Key, "title", ChartSource.Unquote(t.Groups["label"].Value), t); continue; }
            var a = AxisPattern.Match(line.Body);
            if (a.Success)
            {
                var name = a.Groups["name"].Value; var body = a.Groups["body"]; var bracket = body.Value.IndexOf('[');
                SourceVector? categories = null; Match? range = null; string? axisTitle = null;
                if (name == "x-axis" && bracket >= 0)
                {
                    var end = body.Value.LastIndexOf(']'); var stop = end > bracket ? end : body.Length;
                    categories = SourceVector.Read(line, body.Index + bracket + 1, stop - bracket - 1, true);
                    if (bracket > 0 && body.Value[..bracket].Trim().Length > 0) axisTitle = ChartSource.Unquote(body.Value[..bracket].Trim());
                    if (end != body.Length - 1 || !categories.Balanced || categories.Items.Any(i => i.Value.Length == 0)) protect.Add(line.Key);
                }
                else if (body.Value.Contains("-->", StringComparison.Ordinal))
                {
                    var r = RangePattern.Match(body.Value);
                    if (r.Success && SourceVector.Finite(r.Groups["min"].Value, out _) && SourceVector.Finite(r.Groups["max"].Value, out _))
                    { range = r; if (r.Groups["title"].Success) axisTitle = ChartSource.Unquote(r.Groups["title"].Value); }
                    else protect.Add(line.Key);
                    if (name == "x-axis") protect.Add(line.Key);
                }
                else axisTitle = ChartSource.Unquote(body.Value);
                axes.Add(new(line.Key, name, a, axisTitle, categories, range)); continue;
            }
            var s = SeriesPattern.Match(line.Body);
            if (s.Success)
            {
                var values = s.Groups["values"]; var vector = SourceVector.Read(line, values.Index, values.Length);
                var kind = s.Groups["kind"].Value.ToLowerInvariant(); var simple = kind != "box" && vector.Items.All(i => SourceVector.Finite(i.Raw, out _));
                series.Add(new(line.Key, kind, ChartSource.Unquote(s.Groups["name"].Value.Trim()), s, vector, simple)); if (!simple) protect.Add(line.Key); continue;
            }
            protect.Add(line.Key);
        }
        if (series.Sum(s => s.Values.Items.Count) > 500 || axes.Sum(a => a.Categories?.Items.Count ?? 0) > 500) errors.Add("XY Chart exceeds the 500-element budget.");
        return new(first?.Key ?? "", header, title, axes, series, protect.Distinct().ToArray(), errors);
    }
    protected override string? SourceProblem(XySourceModel model) => model.Errors.FirstOrDefault();
    protected override bool Contains(XySourceModel model, string id) => TextIdentity(model, id);
    private static bool TextIdentity(XySourceModel m, string id) => id == m.HeaderIdentity || id == m.HeaderIdentity + ":orientation" || id == m.HeaderIdentity + ":range" || id == m.HeaderIdentity + ":x-axis" || id == m.HeaderIdentity + ":y-axis" ||
        m.Series.Any(s => s.Identity == id || s.Values.Items.Any(v => v.Identity == id)) || m.Axes.Any(a => a.Identity == id || a.Categories?.Items.Any(c => c.Identity == id) == true) || m.Protected.Contains(id);
    protected override void Validate(SourceText text, XySourceModel model, DiagramResult rendered)
    {
        if (model.Errors.Count > 0) throw new ArgumentException(model.Errors[0]);
        if (!rendered.Success || rendered.Source != text.Source) throw new ArgumentException(rendered.Error ?? "XY Chart source did not render.");
        VerifyCanonical(model, rendered.Svg!);
    }
    public static void VerifyCanonical(XySourceModel model, string svg)
    {
        SourceDeclarations.VerifyTitle(svg, model.Title); var root = XDocument.Parse(svg).Root!;
        var groups = root.Descendants().Where(e => (string?)e.Attribute("class") is "xy-bars" or "xy-line" or "xy-box").ToArray();
        var categories = model.Categories?.Categories?.Items.Count(i => i.Value.Length > 0) ?? 0;
        if (categories == 0) categories = Math.Max(1, model.Series.Select(s => s.Values.Items.Count).DefaultIfEmpty(0).Max());
        var grid = SourceDeclarations.Groups(svg, "xy-grid").FirstOrDefault();
        var gridLines = grid?.Elements().Where(e => e.Name.LocalName == "line").ToArray() ?? [];
        double left = 0, top = 0, width = 0, height = 0;
        if (gridLines.Length > 0)
        {
            left = gridLines.Min(e => Math.Min(ChartSource.Attribute(e, "x1"), ChartSource.Attribute(e, "x2")));
            top = gridLines.Min(e => Math.Min(ChartSource.Attribute(e, "y1"), ChartSource.Attribute(e, "y2")));
            width = gridLines.Max(e => Math.Max(ChartSource.Attribute(e, "x1"), ChartSource.Attribute(e, "x2"))) - left;
            height = gridLines.Max(e => Math.Max(ChartSource.Attribute(e, "y1"), ChartSource.Attribute(e, "y2"))) - top;
        }
        double Effective(SourceToken token)
        {
            var raw = token.Raw; var cut = raw.IndexOfAny([' ', '"']); if (cut > 0) raw = raw[..cut].Trim();
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) ? n : 0;
        }
        var values = model.Series.SelectMany(s => s.Values.Items).Select(Effective).ToArray();
        var min = values.Length == 0 ? 0 : values.Min(); var max = values.Length == 0 ? 1 : values.Max();
        if (model.Series.Any(s => s.Kind == "bar")) { min = Math.Min(0, min); max = Math.Max(0, max); }
        if (model.Range?.Range is { } range) { min = double.Parse(range.Groups["min"].Value, CultureInfo.InvariantCulture); max = double.Parse(range.Groups["max"].Value, CultureInfo.InvariantCulture); }
        if (Math.Abs(max - min) < 1e-12) { min--; max++; }
        var proveValues = !model.Axes.Any(a => a.Name == "y-axis" && model.Protected.Contains(a.Identity));
        double Position(double value) => model.Horizontal ? left + width * Math.Clamp((value - min) / (max - min), 0, 1) : top + height * (1 - Math.Clamp((value - min) / (max - min), 0, 1));
        foreach (var (s, index) in model.Series.Select((s, i) => (s, i)))
        {
            if (s.Kind == "box") continue;
            var group = groups.SingleOrDefault(e => (string?)e.Attribute("data-series") == index.ToString(CultureInfo.InvariantCulture));
            var count = Math.Min(categories, s.Values.Items.Count);
            if (group is null || (string?)group.Attribute("class") != (s.Kind == "bar" ? "xy-bars" : "xy-line") ||
                (s.Kind == "bar" ? group.Elements().Count() : group.Elements().Count(e => e.Name.LocalName == "circle")) != count)
                throw new ArgumentException("Canonical XY series indices, types or value occurrences differ from source. Apply is protected.");
            if (!proveValues) continue;
            var marks = s.Kind == "line" ? group.Elements().Where(e => e.Name.LocalName == "circle").ToArray() : group.Elements().ToArray();
            for (var i = 0; i < marks.Length; i++)
            {
                var valuePosition = Position(Effective(s.Values.Items[i]));
                if (s.Kind == "line")
                {
                    var categoryPosition = model.Horizontal ? top + height * (i + .5) / categories : left + width * (i + .5) / categories;
                    if (Math.Abs(ChartSource.Attribute(marks[i], model.Horizontal ? "cx" : "cy") - valuePosition) > .02 ||
                        Math.Abs(ChartSource.Attribute(marks[i], model.Horizontal ? "cy" : "cx") - categoryPosition) > .02)
                        throw new ArgumentException("Canonical XY point positions differ from the source category/value occurrence. Apply is protected.");
                }
                else
                {
                    var baseline = Position(Math.Max(Math.Min(min, max), Math.Min(Math.Max(min, max), 0)));
                    if (Math.Abs(valuePosition - baseline) < .5) valuePosition = baseline + (valuePosition >= baseline ? .5 : -.5);
                    double low, high;
                    if (marks[i].Name.LocalName == "rect") { low = ChartSource.Attribute(marks[i], model.Horizontal ? "x" : "y"); high = low + ChartSource.Attribute(marks[i], model.Horizontal ? "width" : "height"); }
                    else
                    {
                        var coords = ChartSource.Numbers.Matches((string?)marks[i].Attribute("d") ?? "").Select(x => double.Parse(x.Value, CultureInfo.InvariantCulture)).Where((_, n) => n % 2 == (model.Horizontal ? 0 : 1)).ToArray();
                        low = coords.Min(); high = coords.Max();
                    }
                    if (Math.Abs(low - Math.Min(baseline, valuePosition)) > .02 || Math.Abs(high - Math.Max(baseline, valuePosition)) > .02)
                        throw new ArgumentException("Canonical XY bar value or baseline differs from source. Apply is protected.");
                }
            }
        }
        if (grid is not null && model.Categories?.Categories is { } cats)
        {
            var labels = grid.Elements().Where(e => e.Name.LocalName == "text").Skip(6).Select(e => SourceDeclarations.TextValue(e.Value));
            if (!labels.SequenceEqual(cats.Items.Where(c => c.Value.Length > 0).Select(c => SourceDeclarations.TextValue(c.Value)))) throw new ArgumentException("Canonical XY category labels or ordering differ from source.");
        }
    }
    public Task<SourceAuthoringResult> SetTitleAsync(string value, CancellationToken token = default) => ChangeAsync((text, m) => ChartSource.Title(text, m.HeaderIdentity, m.Title, value, true), token);
    public Task<SourceAuthoringResult> SetOrientationAsync(bool horizontal, CancellationToken token = default) => ChangeAsync((text, m) =>
    {
        if (horizontal == m.Horizontal) return text;
        var g = m.Header.Groups["orientation"]; var k = m.Header.Groups["keyword"];
        return text.Replace(text.Line(m.HeaderIdentity).Replace(g.Success ? g.Index : k.Index + k.Length, g.Length, (g.Success ? "" : " ") + (horizontal ? "horizontal" : "vertical")));
    }, token);
    public Task<SourceAuthoringResult> UpdateValueAsync(string identity, string value, CancellationToken token = default) => ChangeAsync((text, m) =>
    {
        var s = m.Series.Single(s => s.Values.Items.Any(v => v.Identity == identity)); Simple(s); var old = s.Values.Items.Single(v => v.Identity == identity);
        if (old.Raw == value) return text; SourceVector.Number(value); return text.Replace(s.Values.Patch(text.Line(s.Identity), identity, value));
    }, token);
    public Task<SourceAuthoringResult> UpdateSeriesAsync(string identity, string name, CancellationToken token = default) => ChangeAsync((text, m) =>
    {
        var s = m.Series.Single(s => s.Identity == identity); Simple(s); if (name == s.Name) return text;
        if (name.IndexOfAny(['[', ']']) >= 0) throw new ArgumentException("Brackets cannot be represented in a series name. Pending input is retained.");
        var g = s.Tokens.Groups["name"]; var k = s.Tokens.Groups["kind"];
        if (name.Length > 0) SourceText.Label(name);
        return text.Replace(text.Line(identity).Replace(g.Success ? g.Index : k.Index + k.Length, g.Length, name.Length == 0 ? "" : (g.Success ? "" : " ") + SourceVector.Quote(name)));
    }, token);
    public Task<SourceAuthoringResult> UpdateCategoryAsync(string identity, string label, CancellationToken token = default) => ChangeAsync((text, m) =>
    {
        var a = m.Categories ?? throw new ArgumentException("No explicit category list is present."); Category(a, m);
        var c = a.Categories!.Items.Single(c => c.Identity == identity); if (c.Value == label) return text;
        return text.Replace(a.Categories.Patch(text.Line(a.Identity), identity, SourceVector.Quote(label)));
    }, token);
    public Task<SourceAuthoringResult> SetAxisTitleAsync(string name, string value, CancellationToken token = default) => ChangeAsync((text, m) =>
    {
        if (name is not ("x-axis" or "y-axis")) throw new ArgumentException("Select an axis.");
        if (m.AxisTitle(name) == value) return text; SourceText.Label(value);
        if (value.IndexOfAny(['[', ']', '"']) >= 0 || value.Contains("-->", StringComparison.Ordinal)) throw new ArgumentException("This axis title contains an unsupported separator. Pending input is retained.");
        var last = m.Axes.LastOrDefault(a => a.Name == name);
        return text.Insert(last is null ? text.Index(m.HeaderIdentity) + 1 : text.Index(last.Identity) + 1, "    " + name + " " + SourceVector.Quote(value));
    }, token);
    public Task<SourceAuthoringResult> SetRangeAsync(string min, string max, CancellationToken token = default) => ChangeAsync((text, m) =>
    {
        var ranges = m.Axes.Where(a => a.Name == "y-axis" && a.Range is not null).ToArray();
        if (m.Axes.Any(a => a.Name == "y-axis" && m.Protected.Contains(a.Identity))) throw new ArgumentException("An unproved axis declaration protects range changes. Inspect source first.");
        if (max.Length == 0 && min.Length == 0)
        {
            foreach (var a in ranges.Reverse())
            {
                var r = a.Range!; var body = a.Tokens.Groups["body"];
                if (r.Groups["title"].Success) text = text.Replace(text.Line(a.Identity).Replace(body.Index + r.Groups["min"].Index, body.Length - r.Groups["min"].Index, ""));
                else text = ChartSource.DeleteRow(text, a.Identity);
            }
            return text;
        }
        var lo = SourceVector.Number(min); var hi = SourceVector.Number(max);
        if (hi <= lo || !double.IsFinite(hi - lo)) throw new ArgumentException("The Y maximum must exceed the minimum with a finite positive range. Pending input is retained.");
        var old = m.Range;
        if (old is null) return text.Insert(text.Index(m.HeaderIdentity) + 1, "    y-axis " + min + " --> " + max);
        var line = text.Line(old.Identity); var b = old.Tokens.Groups["body"]; var r2 = old.Range!;
        var amax = r2.Groups["max"]; var amin = r2.Groups["min"];
        if (amax.Value != max) line = line.Replace(b.Index + amax.Index, amax.Length, max);
        if (amin.Value != min) line = line.Replace(b.Index + amin.Index, amin.Length, min);
        return text.Replace(line);
    }, token);
    public Task<SourceAuthoringResult> AddSeriesAsync(string kind, CancellationToken token = default) => ChangeAsync((text, m) =>
    {
        if (kind is not ("bar" or "line")) throw new ArgumentException("Select bar or line.");
        var count = m.Categories?.Categories?.Items.Count ?? m.Series.FirstOrDefault()?.Values.Items.Count ?? 3;
        return text.Insert(m.Series.Count == 0 ? text.Lines.Count : text.Index(m.Series[^1].Identity) + 1, "    " + kind + " [" + string.Join(", ", Enumerable.Repeat("0", count)) + "]");
    }, token);
    public Task<SourceAuthoringResult> AddValueAsync(string series, string value, CancellationToken token = default) => ChangeAsync((text, m) =>
    { var s = m.Series.Single(s => s.Identity == series); Simple(s); SourceVector.Number(value); return text.Replace(s.Values.Add(text.Line(series), value)); }, token);
    public Task<SourceAuthoringResult> AddCategoryAsync(string label, string initial, CancellationToken token = default) => ChangeAsync((text, m) =>
    {
        Aligned(m); SourceVector.Number(initial); var a = m.Categories!;
        text = text.Replace(a.Categories!.Add(text.Line(a.Identity), SourceVector.Quote(label)));
        foreach (var s in m.Series) text = text.Replace(s.Values.Add(text.Line(s.Identity), initial)); return text;
    }, token);
    public Task<SourceAuthoringResult> DeleteAsync(string identity, CancellationToken token = default) => ChangeAsync((text, m) => Transform(text, m, identity, 0), token, Model.HeaderIdentity);
    public Task<SourceAuthoringResult> MoveAsync(string identity, int direction, CancellationToken token = default) => ChangeAsync((text, m) => Transform(text, m, identity, Math.Sign(direction)), token);
    private static SourceText Transform(SourceText text, XySourceModel m, string identity, int direction)
    {
        var row = m.Series.FirstOrDefault(s => s.Identity == identity);
        if (row is not null) { Simple(row); return direction == 0 ? ChartSource.DeleteRow(text, identity) : ChartSource.MoveRow(text, m.Series.Select(s => s.Identity).ToArray(), identity, direction); }
        if (m.Categories?.Categories?.Items.FirstOrDefault(c => c.Identity == identity) is { } category)
        {
            Aligned(m); var a = m.Categories; var at = a.Categories!.Items.ToList().IndexOf(category);
            text = text.Replace(ListChange(text.Line(a.Identity), a.Categories, at, direction));
            foreach (var s in m.Series) text = text.Replace(ListChange(text.Line(s.Identity), s.Values, at, direction)); return text;
        }
        var series = m.Series.SingleOrDefault(s => s.Values.Items.Any(v => v.Identity == identity)) ?? throw new ArgumentException("Select a simple series, category or value occurrence."); Simple(series);
        return text.Replace(ListChange(text.Line(series.Identity), series.Values, series.Values.Items.ToList().FindIndex(v => v.Identity == identity), direction));
    }
    public static SourceLine ListChange(SourceLine line, SourceVector vector, int at, int direction)
    {
        var items = vector.Items.ToList();
        if (direction == 0) { if (items.Count <= 1) throw new ArgumentException("Keep at least one value/category; removing the final token is protected."); items.RemoveAt(at); }
        else { var next = at + direction; if (next < 0 || next >= items.Count) throw new ArgumentException("There is no occurrence in that direction."); (items[at], items[next]) = (items[next], items[at]); }
        return vector.Reorder(line, items);
    }
    private static void Simple(XySeriesSource s) { if (!s.Simple) throw new ArgumentException("This series contains box syntax, coerced numbers or ignored suffixes. Preserve it exactly and use Edit source; visual edits are protected."); }
    private static void Category(XyAxisSource a, XySourceModel m) { if (m.Protected.Contains(a.Identity)) throw new ArgumentException("Unproved category boundaries protect this edit. Inspect source first."); }
    private static void Aligned(XySourceModel m) { if (!m.Aligned || m.Categories is null || m.Protected.Contains(m.Categories.Identity)) throw new ArgumentException("Aligned category changes require every affected series to be simple and exactly aligned. Ragged, box or ignored tokens are preserved; no vector is padded or truncated."); }
}
