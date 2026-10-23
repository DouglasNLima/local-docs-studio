using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;

namespace LensDocsStudio.Core.PacketDiagrams;

public sealed record PacketSourceField(string Identity, SourceField LabelToken, SourceField FirstToken, SourceField? EndToken, string Form, bool Accepted, int Start, int End)
{
    public string Label => LabelToken.Value;
}
public sealed record PacketSourceModel(string HeaderIdentity, IReadOnlyList<SourceDeclaration> Titles, IReadOnlyList<PacketSourceField> Fields, IReadOnlyList<string> Protected)
{
    public SourceDeclaration? Title => Titles.LastOrDefault();
    public bool Unambiguous(PacketSourceField field) => field.Accepted && !Fields.Any(other => other.Identity != field.Identity && other.Accepted && other.Start <= field.End && field.Start <= other.End);
    public bool StructureSafe => Protected.Count == 0 && Fields.All(Unambiguous);
}

/// <summary>Exact inclusive bit-token edits; downstream accepted/skipped cursor semantics are proved atomically.</summary>
public sealed class PacketVisualDraft : SourceDraft<PacketSourceModel>
{
    public const string Starter = "packet-beta title Protocol fields\n0-15: \"Source\"\n+20: \"Destination\"\n36: \"Flag\"\n40-47: \"Options\"\n";
    private static readonly Regex Header = SourceText.Pattern(@"^[ \t]*packet(?:-beta)?(?:\s+title\s+(?<label>.+?))?[ \t]*$", RegexOptions.IgnoreCase);
    private static readonly Regex Title = SourceText.Pattern(@"^[ \t]*title\s+(?<label>.+?)[ \t]*$", RegexOptions.IgnoreCase);
    public static bool IsPacket(string source) => Header.IsMatch(ChartSource.Header(source));
    public PacketVisualDraft(string source, DiagramService? diagrams = null) : base(source, diagrams) { }
    protected override PacketSourceModel Scan(SourceText text) => Read(text);
    public static PacketSourceModel Read(SourceText text)
    {
        var body = ChartSource.Body(text); if (body.Count == 0 || !Header.IsMatch(body[0].Body)) throw new ArgumentException("This editor requires packet or packet-beta source.");
        var titles = new List<SourceDeclaration>(); var fields = new List<PacketSourceField>(); var protectedRows = new List<string>(); var cursor = 0;
        var match = Header.Match(body[0].Body); if (match.Groups["label"].Success) titles.Add(new(body[0].Key, "title", match.Groups["label"].Value, match));
        foreach (var line in body.Skip(1))
        {
            match = Title.Match(line.Body); if (match.Success) { titles.Add(new(line.Key, "title", match.Groups["label"].Value, match)); continue; }
            match = ExpansionViSource.PacketField.Match(line.Body); if (!match.Success) { protectedRows.Add(line.Key); continue; }
            var accepted = ExpansionViSource.PacketRange(match, cursor, out var start, out var end); if (accepted) cursor = end + 1;
            var relative = match.Groups["count"].Success; var range = match.Groups["end"].Success;
            fields.Add(new(line.Key, WorkflowSource.Field(line, "label", match.Groups["label"]), WorkflowSource.Field(line, relative ? "count" : "start", match.Groups[relative ? "count" : "start"]), range ? WorkflowSource.Field(line, "end", match.Groups["end"]) : null, relative ? "relative" : range ? "range" : "bit", accepted, start, end));
        }
        return new(body[0].Key, titles, fields, protectedRows);
    }
    protected override bool Contains(PacketSourceModel model, string identity) => identity == model.HeaderIdentity || model.Fields.Any(f => f.Identity == identity) || model.Titles.Any(t => t.Identity == identity) || model.Protected.Contains(identity);
    protected override void Validate(SourceText text, PacketSourceModel model, DiagramResult rendered)
    {
        WorkflowSource.Rendered(text, rendered); var actual = SourceDeclarations.Groups(rendered.Svg!, "packet-field"); var expected = Segments(model).ToArray();
        if (actual.Length != expected.Length || actual.Zip(expected).Any(p => !Matches(p.First, p.Second.Field, p.Second.Start, p.Second.End)))
            throw new ArgumentException("Packet source ranges, ordered segments or labels differ from actual canonical native output. Inspect source; Apply is protected.");
        SourceDeclarations.VerifyTitle(rendered.Svg!, model.Title);
    }
    public static IEnumerable<(PacketSourceField Field, int Start, int End)> Segments(PacketSourceModel model)
        => model.Fields.Where(f => f.Accepted).SelectMany(f => Enumerable.Range(f.Start / 32, f.End / 32 - f.Start / 32 + 1).Select(row => (Field: f, Start: Math.Max(f.Start, row * 32), End: Math.Min(f.End, row * 32 + 31))))
            .OrderBy(s => s.Start / 32); // Stable ordering within a native row is declaration order, not the bit number.
    private static bool Matches(XElement shape, PacketSourceField field, int start, int end)
        => (string?)shape.Attribute("data-start") == start.ToString(CultureInfo.InvariantCulture) && (string?)shape.Attribute("data-end") == end.ToString(CultureInfo.InvariantCulture)
            && SourceDeclarations.TextValue(shape.Elements().FirstOrDefault(e => e.Name.LocalName == "text")?.Value ?? "") == SourceDeclarations.TextValue(field.Label);
    public static IReadOnlyList<(PacketSourceField Field, XElement Shape)> Correspondence(PacketSourceModel model, string svg)
    {
        var actual = SourceDeclarations.Groups(svg, "packet-field"); var expected = Segments(model).ToArray(); if (actual.Length != expected.Length) return [];
        return actual.Zip(expected).Where(p => model.Unambiguous(p.Second.Field) && Matches(p.First, p.Second.Field, p.Second.Start, p.Second.End)).Select(p => (p.Second.Field, p.First)).ToArray();
    }
    public Task<SourceAuthoringResult> SetTitleAsync(string value, CancellationToken token = default) => ChangeAsync((text, model) =>
    { SourceText.Label(value); if (value.Contains('<') || value.Contains('>')) throw new ArgumentException("Title markup has no proved literal encoding here. Inspect source."); return ChartSource.Title(text, model.HeaderIdentity, model.Title, value); }, token);
    public Task<SourceAuthoringResult> UpdateFieldAsync(string identity, string label, string first, string end = "", CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        var field = model.Fields.SingleOrDefault(f => f.Identity == identity) ?? throw new ArgumentException("Select a field occurrence.");
        if (!field.Accepted) throw new ArgumentException("The pinned parser skips this field. Inspect or repair the preserved source.");
        LiteralLabel(label); var patches = new List<(SourceField, string)>(); if (label != field.Label) patches.Add((field.LabelToken, label));
        if (first != field.FirstToken.Raw || field.EndToken is not null && end != field.EndToken.Raw)
        {
            if (!model.Unambiguous(field)) throw new ArgumentException("Overlapping bit ranges protect range edits. Inspect source; no automatic packing or renumbering occurs.");
            RequireInteger(first, field.Form == "relative" ? 1 : 0, field.Form == "relative" ? 4096 : 4095);
            if (field.EndToken is not null) RequireInteger(end, 0, 4095);
            if (first != field.FirstToken.Raw) patches.Add((field.FirstToken, first)); if (field.EndToken is not null && end != field.EndToken.Raw) patches.Add((field.EndToken, end));
        }
        var changed = TopologySource.Patch(text, patches); var next = Read(changed); var selected = next.Fields.Single(f => f.Identity == identity);
        if (!selected.Accepted) throw new ArgumentException("The inclusive range/count is outside 0..4095 or inverted. Pending input is retained.");
        if ((first != field.FirstToken.Raw || field.EndToken is not null && end != field.EndToken.Raw) && !next.Unambiguous(selected)) throw new ArgumentException("The edited inclusive range overlaps another field. Inspect source; no fields were packed or renumbered.");
        StableNeighbours(model, next, [identity]); return changed;
    }, token);
    public Task<SourceAuthoringResult> AddFieldAsync(string form, string first, string end, string label, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        if (!model.StructureSafe) throw new ArgumentException("Skipped, overlapping or unknown source protects field addition. Inspect source first.");
        LiteralLabel(label); RequireInteger(first, form == "relative" ? 1 : 0, form == "relative" ? 4096 : 4095); if (form == "range") RequireInteger(end, 0, 4095);
        if (form is not ("relative" or "range" or "bit")) throw new ArgumentException("Choose explicit range, one bit or relative count.");
        var changed = TopologySource.Append(text, (form == "relative" ? "+" : "") + first + (form == "range" ? "-" + end : "") + ": \"" + label + "\""); var next = Read(changed);
        if (next.Fields.Count != model.Fields.Count + 1 || !next.Fields[^1].Accepted || !next.Unambiguous(next.Fields[^1])) throw new ArgumentException("The new field is skipped or overlaps an existing inclusive range. Choose an explicit non-overlapping range/count.");
        StableNeighbours(model, next, []); return changed;
    }, token);
    public Task<SourceAuthoringResult> DeleteAsync(string identity, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        if (!model.StructureSafe || !model.Fields.Any(f => f.Identity == identity)) throw new ArgumentException("Only a complete proved field can be removed. Unknown, skipped or overlapping source remains protected.");
        var changed = ChartSource.DeleteRow(text, identity); StableNeighbours(model, Read(changed), [identity]); return changed;
    }, token);
    public Task<SourceAuthoringResult> MoveAsync(string identity, int direction, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        if (!model.StructureSafe) throw new ArgumentException("Unknown, skipped or overlapping source protects movement.");
        var changed = ChartSource.MoveRow(text, model.Fields.Select(f => f.Identity).ToArray(), identity, direction); StableNeighbours(model, Read(changed), []); return changed;
    }, token);
    private static void StableNeighbours(PacketSourceModel before, PacketSourceModel after, IEnumerable<string> permitted)
    {
        var allowed = permitted.ToHashSet();
        foreach (var field in before.Fields.Where(f => !allowed.Contains(f.Identity)))
        {
            var next = after.Fields.SingleOrDefault(f => f.Identity == field.Identity);
            if (next is null || next.Accepted != field.Accepted || next.Accepted && (next.Start != field.Start || next.End != field.End))
                throw new ArgumentException("This operation changes a later relative field's accepted cursor/range. Its downstream impact is protected; inspect and edit the complete source explicitly.");
        }
    }
    private static void RequireInteger(string raw, int minimum, int maximum)
    { if (!SourceText.Pattern("^[0-9]+$").IsMatch(raw) || !ExpansionViSource.Integer(raw, minimum, maximum, out _)) throw new ArgumentException("Enter a complete bounded integer (" + minimum + ".." + maximum + "), without signs, spaces, decimals or overflow."); }
    private static void LiteralLabel(string value)
    { if (value.Length > 2000 || value.IndexOfAny(['"', '<', '>']) >= 0 || value.Any(char.IsControl)) throw new ArgumentException("Packet labels allow at most 2,000 literal characters. Double quotes, markup and control characters have no proved encoding. Pending input is retained."); }
}
