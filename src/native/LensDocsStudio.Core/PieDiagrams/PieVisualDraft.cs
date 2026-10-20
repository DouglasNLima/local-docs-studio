using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;

namespace LensDocsStudio.Core.PieDiagrams;

public sealed record PieEntry(string Identity, string Label, string Number, double Value, Match Tokens);
public sealed record PieSourceModel(IReadOnlyList<PieEntry> Entries, string Title, string? TitleIdentity, Match? TitleTokens,
    string HeaderIdentity, IReadOnlyList<string> Protected, IReadOnlyList<string> Errors);

public sealed class PieVisualDraft : SourceDraft<PieSourceModel>
{
    public const string Starter = "pie showData\n    title Documentation mix\n    \"Guides\" : 6\n    \"Reference\" : 3\n    \"Examples\" : 1\n";
    private static readonly Regex Entry = SourceText.Pattern("^[ \\t]*\"(?<label>[^\"\\r\\n]+)\"[ \\t]*:[ \\t]*(?<value>[0-9]+(?:\\.[0-9]+)?)[ \\t]*$");
    private static readonly Regex Title = SourceText.Pattern(@"^[ \t]*title[ \t]+(?<label>.+?)[ \t]*$");
    private static readonly Regex Header = SourceText.Pattern(@"^pie(?:[ \t]+showData)?(?:[ \t]+title[ \t]+(?<label>.+))?$", RegexOptions.IgnoreCase);
    public static bool IsPie(string source) => SourceText.Pattern(@"^pie(?:\s|$)", RegexOptions.IgnoreCase).IsMatch(SourceText.Header(source));
    public PieVisualDraft(string source, DiagramService? diagrams = null) : base(source, diagrams) { }
    protected override PieSourceModel Scan(SourceText text)
    {
        var entries = new List<PieEntry>(); var errors = new List<string>(); var protect = new List<string>();
        string title = "", header = ""; string? titleKey = null; Match? titleTokens = null;
        foreach (var line in text.Lines)
        {
            if (line.Content.Length == 0 || line.Comment) continue;
            if (line.InitDirective) { protect.Add(line.Key); continue; }
            if (header.Length == 0)
            {
                header = line.Key; var match = Header.Match(line.Content);
                if (!match.Success) errors.Add("Unsupported Pie header. Use pie, optional showData and optional title.");
                // Match against the full line so token offsets always address original bytes.
                match = SourceText.Pattern(@"^[ \t]*pie(?:[ \t]+showData)?(?:[ \t]+title[ \t]+(?<label>.+?))?[ \t]*$", RegexOptions.IgnoreCase).Match(line.Body);
                if (match.Success && match.Groups["label"].Success) { title = match.Groups["label"].Value; titleKey = line.Key; titleTokens = match; }
                continue;
            }
            var named = Title.Match(line.Body);
            if (named.Success) { title = named.Groups["label"].Value; titleKey = line.Key; titleTokens = named; continue; }
            var entry = Entry.Match(line.Body);
            if (entry.Success)
            {
                var number = entry.Groups["value"].Value;
                if (!double.TryParse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value) || value == 0 && number.Any(character => character is >= '1' and <= '9')) errors.Add("Pie values must be finite non-negative decimals within the pinned renderer's numeric range. A non-zero value must not underflow into an omitted zero row.");
                else entries.Add(new(line.Key, entry.Groups["label"].Value, number, value, entry));
                continue;
            }
            if (line.Content.StartsWith('"')) errors.Add("A Pie category is malformed. Use a quoted non-empty label, colon and non-negative decimal; the pinned parser does not support embedded quotes or line breaks.");
            else protect.Add(line.Key);
        }
        if (header.Length == 0) errors.Add("A Pie header is required.");
        if (entries.Count > 500) errors.Add("Pie exceeds the 500-element rendering budget.");
        return new(entries, title, titleKey, titleTokens, header, protect, errors);
    }
    protected override bool Contains(PieSourceModel model, string identity) => identity == model.HeaderIdentity || model.Entries.Any(entry => entry.Identity == identity) || model.Protected.Contains(identity);
    protected override void Validate(SourceText text, PieSourceModel model, DiagramResult rendered)
    {
        if (model.Errors.Count > 0) throw new ArgumentException(model.Errors[0]);
        if (!rendered.Success || rendered.Source != text.Source) throw new ArgumentException(rendered.Error ?? "Pie rendering did not match the current source.");
        var positive = model.Entries.Where(entry => entry.Value > 0).ToArray();
        if (positive.Length == 0 || !double.IsFinite(positive.Sum(entry => entry.Value))) throw new ArgumentException("Pie needs at least one positive value and a finite total. Zero rows remain in source; the pinned renderer does not draw them.");
        var slices = XDocument.Parse(rendered.Svg!).Descendants().Where(element => ((string?)element.Attribute("class")) == "pie-slice").ToArray();
        if (slices.Length != positive.Length || slices.Where((slice, index) => (string?)slice.Attribute("data-label") != positive[index].Label).Any())
            throw new ArgumentException("Canonical Pie slice occurrences do not match the complete positive source rows. Apply is protected.");
    }
    public Task<SourceAuthoringResult> SetTitleAsync(string value, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        SourceText.Label(value);
        return model.TitleIdentity is { } key ? text.Patch(key, model.TitleTokens!.Groups["label"], value) : text.Insert(text.Index(model.HeaderIdentity) + 1, "    title " + value);
    }, token);
    public Task<SourceAuthoringResult> UpdateAsync(string identity, string label, string number, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        var entry = model.Entries.Single(item => item.Identity == identity); ValidateLabel(label); ValidateNumber(number);
        var encoded = label == DisplayLabel(entry.Label) ? entry.Label : EncodeLabel(label);
        var line = text.Line(identity);
        // Patch right-to-left and retain untouched decimal spelling/precision.
        if (number != entry.Number) line = line.Replace(entry.Tokens.Groups["value"].Index, entry.Tokens.Groups["value"].Length, number);
        if (encoded != entry.Label) line = line.Replace(entry.Tokens.Groups["label"].Index, entry.Tokens.Groups["label"].Length, encoded);
        return text.Replace(line);
    }, token);
    public Task<SourceAuthoringResult> AddAsync(string label, string number, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        ValidateLabel(label); ValidateNumber(number);
        var last = model.Entries.LastOrDefault(); var index = last is null ? text.Index(model.HeaderIdentity) + 1 : text.Index(last.Identity) + 1;
        return text.Insert(index, (last is null ? "    " : text.Line(last.Identity).Indent) + "\"" + EncodeLabel(label) + "\" : " + number);
    }, token);
    public Task<SourceAuthoringResult> DeleteAsync(string identity, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        _ = model.Entries.SingleOrDefault(item => item.Identity == identity) ?? throw new ArgumentException("Only category rows can be removed. The title and preserved directives use source inspection.");
        SourceRowMoves.CheckTrailingAttachment(text, identity); var start = text.AttachedStart(identity);
        return text.Splice(start, text.Index(identity) + 1 - start, []);
    }, token);
    public Task<SourceAuthoringResult> MoveAsync(string identity, int direction, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        var index = model.Entries.ToList().FindIndex(entry => entry.Identity == identity); var other = index + Math.Sign(direction);
        if (index < 0 || other < 0 || other >= model.Entries.Count) throw new ArgumentException("There is no category in that direction.");
        return SourceRowMoves.Swap(text, model.Entries[Math.Min(index, other)].Identity, model.Entries[Math.Max(index, other)].Identity);
    }, token);
    public static void ValidateLabel(string value)
    { SourceText.Label(EncodeLabel(value)); if (value.Contains('"')) throw new ArgumentException("Mermaider 0.15.1 cannot encode a quote inside a Pie label. Remove the quote; the pending label is retained."); }
    public static string EncodeLabel(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
    public static string DisplayLabel(string value) => SourceText.Pattern(@"<br\s*/?>", RegexOptions.IgnoreCase).Replace(value, "\n").Replace("\\n", "\n", StringComparison.Ordinal);
    public static void ValidateNumber(string value)
    {
        if (!SourceText.Pattern(@"^[0-9]+(?:\.[0-9]+)?$").IsMatch(value) || !double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
                throw new ArgumentException("Enter a complete non-negative decimal, for example 12.50. Exponents, signs, NaN and infinity are unsupported.");
        if (number == 0 && value.Any(character => character is >= '1' and <= '9')) throw new ArgumentException("This non-zero decimal is too small for the pinned renderer and would be omitted. Enter a representable value; the pending text is retained.");
    }
}
