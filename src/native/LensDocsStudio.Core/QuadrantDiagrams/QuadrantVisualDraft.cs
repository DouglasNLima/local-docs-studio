using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;

namespace LensDocsStudio.Core.QuadrantDiagrams;

public sealed record QuadrantPointSource(string Identity, string Label, string X, string Y, double EffectiveX, double EffectiveY, Match Tokens);
public sealed record QuadrantSourceModel(string HeaderIdentity, SourceDeclaration? Title, IReadOnlyList<SourceDeclaration> Declarations,
    IReadOnlyList<QuadrantPointSource> Points, IReadOnlyList<string> Protected, IReadOnlyList<string> Errors)
{
    public SourceDeclaration? Effective(string name) => Declarations.LastOrDefault(item => item.Name == name);
}

public sealed class QuadrantVisualDraft : SourceDraft<QuadrantSourceModel>
{
    public const string Starter = "quadrantChart\n    title Documentation priorities\n    x-axis Low effort --> High effort\n    y-axis Low impact --> High impact\n    quadrant-1 Invest\n    quadrant-2 Quick wins\n    quadrant-3 Reconsider\n    quadrant-4 Defer\n    Guides: [.25, .75]\n    Reference: [.75, .25]\n";
    private static readonly Regex Declaration = SourceText.Pattern(@"^[ \t]*(?<name>title|x-axis|y-axis|quadrant-[1-4])[ \t]+(?<label>.+?)[ \t]*$");
    private static readonly Regex Point = SourceText.Pattern(@"^[ \t]*(?<label>.+?):[ \t]*\[[ \t]*(?<x>-?[0-9]*\.?[0-9]+)[ \t]*,[ \t]*(?<y>-?[0-9]*\.?[0-9]+)[ \t]*\][ \t]*$");
    public static bool IsQuadrant(string source) => SourceText.Pattern(@"^quadrantChart(?:\s|$)", RegexOptions.IgnoreCase).IsMatch(SourceText.Header(source));
    public QuadrantVisualDraft(string source, DiagramService? diagrams = null) : base(source, diagrams) { }
    protected override QuadrantSourceModel Scan(SourceText text)
    {
        var declarations = new List<SourceDeclaration>(); var points = new List<QuadrantPointSource>(); var protect = new List<string>(); var errors = new List<string>();
        string header = ""; SourceDeclaration? title = null;
        foreach (var line in text.Lines)
        {
            if (line.Content.Length == 0 || line.Comment) continue;
            if (line.InitDirective) { protect.Add(line.Key); continue; }
            if (header.Length == 0)
            {
                header = line.Key; var match = SourceDeclarations.Header(line, "quadrantChart");
                if (!match.Success) errors.Add("Unsupported Quadrant header. Inspect the original source.");
                else if (match.Groups["label"].Success) title = new(line.Key, "title", match.Groups["label"].Value, match);
                continue;
            }
            var directive = Declaration.Match(line.Body);
            if (directive.Success)
            {
                var item = new SourceDeclaration(line.Key, directive.Groups["name"].Value, directive.Groups["label"].Value, directive);
                declarations.Add(item); if (item.Name == "title") title = item; continue;
            }
            var point = Point.Match(line.Body);
            if (point.Success)
            {
                var x = point.Groups["x"].Value; var y = point.Groups["y"].Value;
                if (double.TryParse(x, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var nx) && double.IsFinite(nx) &&
                    double.TryParse(y, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var ny) && double.IsFinite(ny))
                    points.Add(new(line.Key, point.Groups["label"].Value.Trim(), x, y, Math.Clamp(nx, 0, 1), Math.Clamp(ny, 0, 1), point));
                else errors.Add("A Quadrant coordinate exceeds the finite numeric range. Correct it in source.");
            }
            else protect.Add(line.Key);
        }
        if (header.Length == 0) errors.Add("A Quadrant header is required.");
        if (points.Count > 500) errors.Add("Quadrant exceeds the 500-element budget.");
        return new(header, title, declarations, points, protect, errors);
    }
    protected override bool Contains(QuadrantSourceModel model, string identity) => identity == model.HeaderIdentity || identity.StartsWith(model.HeaderIdentity + ":", StringComparison.Ordinal) || model.Points.Any(p => p.Identity == identity) || model.Declarations.Any(d => d.Identity == identity) || model.Protected.Contains(identity);
    protected override void Validate(SourceText text, QuadrantSourceModel model, DiagramResult rendered)
    {
        if (model.Errors.Count > 0) throw new ArgumentException(model.Errors[0]);
        if (!rendered.Success || rendered.Source != text.Source) throw new ArgumentException(rendered.Error ?? "Quadrant source did not render.");
        VerifyCanonical(model, rendered.Svg!);
    }
    public static void VerifyCanonical(QuadrantSourceModel model, string svg)
    {
        var groups = SourceDeclarations.Groups(svg, "quadrant").ToDictionary(e => (string)e.Attribute("data-quadrant")!);
        var points = SourceDeclarations.Groups(svg, "quadrant-point");
        if (groups.Count != 4 || points.Length != model.Points.Count) throw new ArgumentException("Canonical Quadrant occurrence counts differ from source. Apply is protected.");
        double Number(XElement e, string name) => double.Parse((string)e.Attribute(name)!, CultureInfo.InvariantCulture);
        var tl = groups["2"].Elements().First(); var br = groups["4"].Elements().First();
        var left = Number(tl, "x"); var top = Number(tl, "y"); var width = Number(br, "x") + Number(br, "width") - left; var height = Number(br, "y") + Number(br, "height") - top;
        for (var i = 0; i < points.Length; i++)
        {
            var p = model.Points[i]; var circle = points[i].Elements().First();
            if (SourceDeclarations.VisibleText(points[i]) != SourceDeclarations.TextValue(p.Label) ||
                Math.Abs(Number(circle, "cx") - Math.Round(left + p.EffectiveX * width, 3)) > .0001 ||
                Math.Abs(Number(circle, "cy") - Math.Round(top + (1 - p.EffectiveY) * height, 3)) > .0001)
                throw new ArgumentException("Canonical point label, order or coordinate differs from its source occurrence. Apply is protected.");
        }
        foreach (var q in new[] { "1", "2", "3", "4" })
            if (((string?)groups[q].Attribute("data-label") ?? "") != (model.Effective("quadrant-" + q)?.Value ?? "")) throw new ArgumentException("Canonical quadrant labels differ from effective declarations.");
    }
    public Task<SourceAuthoringResult> SetTitleAsync(string value, CancellationToken token = default) => ChangeAsync((text, model) => SourceDeclarations.SetTitle(text, model.HeaderIdentity, model.Title, value), token);
    public Task<SourceAuthoringResult> SetDeclarationAsync(string name, string value, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        if (!new[] { "x-axis", "y-axis", "quadrant-1", "quadrant-2", "quadrant-3", "quadrant-4" }.Contains(name)) throw new ArgumentException("Select an axis or numbered quadrant.");
        SourceText.Label(value);
        if (name.EndsWith("axis", StringComparison.Ordinal))
        {
            var parts = value.Split("-->", StringSplitOptions.None); if (parts.Length > 2 || parts.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Enter one axis label or two labels separated by -->. Both labels must be complete.");
        }
        var old = model.Effective(name); return old is null ? text.Insert(text.Index(model.HeaderIdentity) + 1, "    " + name + " " + value) : text.Patch(old.Identity, old.Tokens.Groups["label"], value);
    }, token);
    public Task<SourceAuthoringResult> UpdateAsync(string identity, string label, string x, string y, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        var point = model.Points.Single(p => p.Identity == identity);
        if (label != point.Label) { SourceDeclarations.PlainLabel(label, ":"); SourceDeclarations.RejectDirectiveName(label, "title|x-axis|y-axis|quadrant-[1-4]"); }
        if (x != point.X) Coordinate(x); if (y != point.Y) Coordinate(y);
        var line = text.Line(identity);
        if (y != point.Y) line = line.Replace(point.Tokens.Groups["y"].Index, point.Tokens.Groups["y"].Length, y);
        if (x != point.X) line = line.Replace(point.Tokens.Groups["x"].Index, point.Tokens.Groups["x"].Length, x);
        if (label != point.Label) line = line.Replace(point.Tokens.Groups["label"].Index, point.Tokens.Groups["label"].Length, label);
        return text.Replace(line);
    }, token);
    public Task<SourceAuthoringResult> AddAsync(string label, string x, string y, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        SourceDeclarations.PlainLabel(label, ":"); SourceDeclarations.RejectDirectiveName(label, "title|x-axis|y-axis|quadrant-[1-4]"); Coordinate(x); Coordinate(y);
        var last = model.Points.LastOrDefault(); return text.Insert(last is null ? text.Lines.Count : text.Index(last.Identity) + 1, "    " + label + ": [" + x + ", " + y + "]");
    }, token);
    public Task<SourceAuthoringResult> DeleteAsync(string identity, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        _ = model.Points.Single(p => p.Identity == identity); SourceRowMoves.CheckTrailingAttachment(text, identity); var start = text.AttachedStart(identity); return text.Splice(start, text.Index(identity) + 1 - start, []);
    }, token);
    public Task<SourceAuthoringResult> MoveAsync(string identity, int direction, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        var index = model.Points.ToList().FindIndex(p => p.Identity == identity); var other = index + Math.Sign(direction);
        if (index < 0 || other < 0 || other >= model.Points.Count) throw new ArgumentException("There is no point in that direction.");
        return SourceRowMoves.Swap(text, model.Points[Math.Min(index, other)].Identity, model.Points[Math.Max(index, other)].Identity);
    }, token);
    public static void Coordinate(string value)
    {
        if (!SourceText.Pattern(@"^[0-9]*\.?[0-9]+$").IsMatch(value) || !double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number) || number < 0 || number > 1)
            throw new ArgumentException("Enter a complete finite coordinate from 0 to 1 using a decimal point, for example 0.5. Pending input is retained.");
        if (number == 0 && value.Any(c => c is >= '1' and <= '9')) throw new ArgumentException("This coordinate underflows to zero in the pinned renderer. Enter a representable coordinate.");
    }
}
