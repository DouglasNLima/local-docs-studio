using System.Text.RegularExpressions;
using System.Xml.Linq;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;

namespace LensDocsStudio.Core.KanbanDiagrams;

public sealed record KanbanRow(string Identity, string? Column, string Id, string Label, string Form, SourceField IdToken, SourceField LabelToken,
    IReadOnlyList<SourceField> Metadata, bool MetadataSafe, int MetadataEnd)
{
    public string Meta(string name) => Metadata.LastOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value ?? "";
}
public sealed record KanbanSourceModel(string HeaderIdentity, SourceDeclaration? Title, IReadOnlyList<KanbanRow> Rows, IReadOnlyList<string> Protected)
{
    public IReadOnlyList<KanbanRow> Columns => Rows.Where(r => r.Column is null).ToArray();
    public IReadOnlyList<KanbanRow> Cards => Rows.Where(r => r.Column is not null).ToArray();
}

public sealed class KanbanVisualDraft : SourceDraft<KanbanSourceModel>
{
    public const string Starter = "kanban\n    title Documentation board\n    todo[To do]\n        guide[Write guide]@{ assigned: 'Author', priority: 'High' }\n        review[Review guide]\n    done[Done]\n        published[Publish guide]\n";
    private static readonly Regex HeaderPattern = SourceText.Pattern(@"^kanban$", RegexOptions.IgnoreCase);
    private static readonly Regex TitlePattern = SourceText.Pattern(@"^[ \t]*title\s+(?<label>.+?)[ \t]*$", RegexOptions.IgnoreCase);
    private static readonly Regex IdTitle = SourceText.Pattern(@"^(?<id>\S+)\[(?<label>[^\]]*)\]\s*$");
    private static readonly Regex MetaSuffix = SourceText.Pattern(@"^(?<body>.+?)\s*@\{(?<meta>[^}]*)\}\s*$");
    private static readonly Regex MetaPair = SourceText.Pattern("(?<key>\\w+)\\s*:\\s*(?:'(?<single>[^']*)'|\"(?<double>[^\"]*)\"|(?<plain>[^,}\\s][^,}]*)?)");
    public static bool IsKanban(string source) => HeaderPattern.IsMatch(ChartSource.Header(source));
    public KanbanVisualDraft(string source, DiagramService? diagrams = null) : base(source, diagrams) { }
    protected override KanbanSourceModel Scan(SourceText text)
    {
        var body = ChartSource.Body(text); var header = body.FirstOrDefault(); var rows = new List<KanbanRow>(); var protect = new List<string>(); SourceDeclaration? title = null; string? column = null; var indent = -1;
        foreach (var line in body.Skip(1))
        {
            var tm = TitlePattern.Match(line.Body); if (tm.Success) { title = new(line.Key, "title", tm.Groups["label"].Value.Trim(), tm); continue; }
            if (indent < 0) indent = line.Indent.Length;
            var isColumn = line.Indent.Length <= indent; if (isColumn) column = line.Key;
            var content = line.Content; var meta = isColumn ? Match.Empty : MetaSuffix.Match(content); var labelBody = meta.Success ? meta.Groups["body"].Value.Trim() : content;
            var explicitForm = IdTitle.Match(labelBody); var form = explicitForm.Success ? "explicit" : labelBody.StartsWith('[') && labelBody.EndsWith(']') ? "bracket" : "bare";
            var offset = line.Indent.Length; var id = explicitForm.Success ? explicitForm.Groups["id"].Value : form == "bracket" ? labelBody[1..^1] : labelBody;
            var label = explicitForm.Success ? explicitForm.Groups["label"].Value : id;
            var idToken = new SourceField(line.Key, "id", id, offset + (explicitForm.Success ? explicitForm.Groups["id"].Index : form == "bracket" ? 1 : 0), id.Length, id);
            var labelToken = new SourceField(line.Key, "label", label, offset + (explicitForm.Success ? explicitForm.Groups["label"].Index : form == "bracket" ? 1 : 0), label.Length, label);
            var fields = new List<SourceField>(); var safe = true;
            if (meta.Success)
            {
                var raw = meta.Groups["meta"].Value; var at = 0;
                foreach (Match pair in MetaPair.Matches(raw))
                {
                    if (raw[at..pair.Index].Any(c => !char.IsWhiteSpace(c) && c != ',')) safe = false;
                    var g = pair.Groups["single"].Success ? pair.Groups["single"] : pair.Groups["double"].Success ? pair.Groups["double"] : pair.Groups["plain"];
                    if (!g.Success || g == pair.Groups["plain"] && g.Value.IndexOfAny(['\'', '"']) >= 0) safe = false;
                    var tokenStart = offset + meta.Groups["meta"].Index + g.Index; var rawValue = g.Value; var value = rawValue.Trim();
                    if (pair.Groups["single"].Success || pair.Groups["double"].Success) { tokenStart--; rawValue = line.Body.Substring(tokenStart, g.Length + 2); value = g.Value; }
                    else tokenStart += rawValue.Length - rawValue.TrimStart().Length;
                    fields.Add(new(line.Key, pair.Groups["key"].Value, value, tokenStart, rawValue.Trim().Length, rawValue.Trim())); at = pair.Index + pair.Length;
                }
                if (raw[at..].Any(c => !char.IsWhiteSpace(c) && c != ',')) safe = false;
            }
            else if (!isColumn && content.Contains("@{", StringComparison.Ordinal)) safe = false;
            rows.Add(new(line.Key, isColumn ? null : column, id, label, form, idToken, labelToken, fields, safe, meta.Success ? offset + meta.Groups["meta"].Index + meta.Groups["meta"].Length : -1));
        }
        protect.AddRange(text.Lines.Where(l => !body.Contains(l) && l.Content.Length > 0 && !l.Comment).Select(l => l.Key));
        return new(header is not null && HeaderPattern.IsMatch(header.Content) ? header.Key : "", title, rows, protect);
    }
    protected override bool Contains(KanbanSourceModel m, string id) => id == m.HeaderIdentity || id == m.Title?.Identity || m.Rows.Any(r => r.Identity == id) || m.Protected.Contains(id);
    protected override string? SourceProblem(KanbanSourceModel m) => m.HeaderIdentity.Length == 0 ? "A simple kanban header is required." : m.Rows.Count > 500 ? "Kanban exceeds the 500-element budget." : null;
    protected override void Validate(SourceText text, KanbanSourceModel m, DiagramResult rendered) { WorkflowSource.Rendered(text, rendered); Correspondence(m, rendered.Svg!); }
    public static IReadOnlyList<(KanbanRow Row, XElement Shape)> Correspondence(KanbanSourceModel m, string svg)
    {
        var columns = SourceDeclarations.Groups(svg, "kanban-column"); var cards = SourceDeclarations.Groups(svg, "kanban-card");
        if (!columns.Select(e => (string?)e.Attribute("data-id")).SequenceEqual(m.Columns.Select(r => r.Id)) || !cards.Select(e => (string?)e.Attribute("data-id")).SequenceEqual(m.Cards.Select(r => r.Id))) throw new ArgumentException("Canonical Kanban membership or occurrence order differs from source. Apply is protected; inspect original source.");
        // Cards precede their containing columns for pointer hit testing. Duplicates retain occurrence order.
        return m.Cards.Zip(cards, (r, s) => (r, s)).Concat(m.Columns.Zip(columns, (r, s) => (r, s))).ToArray();
    }
    public Task<SourceAuthoringResult> SetTitleAsync(string title, CancellationToken token = default) => ChangeAsync((t, m) => { SourceText.Label(title); return SourceDeclarations.SetTitle(t, m.HeaderIdentity, m.Title, title); }, token);
    public Task<SourceAuthoringResult> UpdateAsync(string identity, string id, string label, string assigned, string ticket, string priority, CancellationToken token = default) => ChangeAsync((t, m) =>
    {
        var row = m.Rows.Single(r => r.Identity == identity); var changed = t;
        if (id != row.Id || label != row.Label)
        {
            SourceDeclarations.PlainLabel(label, "[]{}\""); SourceDeclarations.RejectDirectiveName(label, "title", true);
            if (row.Form != "explicit")
            { if (id != label) throw new ArgumentException("Bare text and bracket forms couple ID and title. Edit both to the same value; no syntax conversion is made."); changed = row.LabelToken.Set(changed, label); }
            else
            {
                SourceDeclarations.PlainLabel(id, "[]{}\"@"); if (id.Any(char.IsWhiteSpace)) throw new ArgumentException("An explicit ID cannot contain whitespace.");
                // Patch descending spans to retain every other byte.
                changed = row.LabelToken.Set(changed, label); changed = row.IdToken.Set(changed, id);
            }
        }
        foreach (var (key, value) in new[] { ("assigned", assigned), ("ticket", ticket), ("priority", priority) })
        {
            var current = Scan(changed).Rows.Single(r => r.Identity == identity); if (value == row.Meta(key)) continue;
            if (row.Column is null || !current.MetadataSafe) throw new ArgumentException("Partial or unsupported metadata protects this field. Label edits remain available; inspect original source.");
            if (key == "priority" && value.Length > 0 && !new[] { "very high", "high", "medium", "low", "very low" }.Contains(value.ToLowerInvariant())) throw new ArgumentException("Choose Very high, High, Medium, Low or Very low; other priorities have no native presentation.");
            if (value.IndexOfAny(['\'', '"', '{', '}', ',']) >= 0 || value.Any(char.IsControl)) throw new ArgumentException("Metadata quotes, commas, braces and line breaks have no proved escape here. Pending input is retained.");
            var field = current.Metadata.LastOrDefault(f => f.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (field is not null)
            { var quote = field.Raw.StartsWith('\'') ? "'" : field.Raw.StartsWith('"') ? "\"" : value.Length == 0 ? "'" : ""; changed = field.Set(changed, quote + value + quote); }
            else
            { var line = changed.Line(identity); changed = changed.Replace(current.MetadataEnd >= 0 ? line.Replace(current.MetadataEnd, 0, (current.Metadata.Count > 0 ? ", " : "") + key + ": '" + value + "'") : line.Replace(line.Body.TrimEnd().Length, 0, "@{ " + key + ": '" + value + "' }")); }
        }
        var after = Scan(changed).Rows.Single(r => r.Identity == identity);
        if (after.Id != id || after.Label != label || after.Column != row.Column) throw new ArgumentException("This label would change native ID, title or membership. Pending input is retained.");
        return changed;
    }, token);
    private static void Structure(SourceText t, KanbanSourceModel m)
    {
        if (m.Columns.Select(c => t.Line(c.Identity).Indent.Length).Distinct().Count() > 1) throw new ArgumentException("Mixed column indentation protects structural changes: moving the first column would change the fixed boundary. Safe field edits remain available.");
    }
    private static int ColumnEnd(SourceText t, KanbanSourceModel m, KanbanRow col) => m.Columns.SkipWhile(c => c.Identity != col.Identity).Skip(1).FirstOrDefault() is { } next ? t.AttachedStart(next.Identity) : t.Lines.Count;
    private static void Group(SourceText t, KanbanSourceModel m, int start, int end) => WorkflowSource.CompleteGroup(t, start, end, m.Rows.Select(r => r.Identity));
    public Task<SourceAuthoringResult> AddColumnAsync(string id, string label, CancellationToken token = default) => ChangeAsync((t, m) =>
    {
        Structure(t, m); SourceDeclarations.PlainLabel(id, "[]{}\"@"); SourceDeclarations.PlainLabel(label, "[]{}\""); if (id.Any(char.IsWhiteSpace)) throw new ArgumentException("Use an ID without whitespace.");
        if (m.Rows.Any(r => r.Id == id)) throw new ArgumentException("Choose an unused ID for a new column.");
        return t.Insert(t.Lines.Count, (m.Columns.FirstOrDefault() is { } c ? t.Line(c.Identity).Indent : "    ") + id + "[" + label + "]");
    }, token);
    public Task<SourceAuthoringResult> AddCardAsync(string column, string id, string label, CancellationToken token = default) => ChangeAsync((t, m) =>
    {
        Structure(t, m); var col = m.Columns.Single(c => c.Identity == column); SourceDeclarations.PlainLabel(id, "[]{}\"@"); SourceDeclarations.PlainLabel(label, "[]{}\""); if (id.Any(char.IsWhiteSpace)) throw new ArgumentException("Use an ID without whitespace.");
        if (m.Rows.Any(r => r.Id == id)) throw new ArgumentException("Choose an unused ID for a new card.");
        var end = ColumnEnd(t, m, col); Group(t, m, t.Index(column), end); return t.Insert(end, t.Line(column).Indent + "    " + id + "[" + label + "]");
    }, token);
    public Task<SourceAuthoringResult> DeleteAsync(string identity, bool completeColumn = false, CancellationToken token = default) => ChangeAsync((t, m) =>
    {
        Structure(t, m); var row = m.Rows.Single(r => r.Identity == identity); var start = t.AttachedStart(identity); var end = row.Column is null ? ColumnEnd(t, m, row) : t.Index(identity) + 1;
        if (row.Column is null && m.Cards.Any(c => c.Column == identity) && !completeColumn) throw new ArgumentException("This column contains cards. Move/remove them first, or explicitly choose Remove column and all cards.");
        Group(t, m, start, end); if (row.Column is not null) SourceRowMoves.CheckTrailingAttachment(t, identity); return t.Splice(start, end - start, []);
    }, token);
    public Task<SourceAuthoringResult> MoveAsync(string identity, int direction, CancellationToken token = default) => ChangeAsync((t, m) =>
    {
        Structure(t, m); var row = m.Rows.Single(r => r.Identity == identity); var peers = m.Rows.Where(r => r.Column == row.Column).ToArray(); var at = Array.IndexOf(peers, row); var next = at + Math.Sign(direction);
        if (next < 0 || next >= peers.Length) throw new ArgumentException("There is no occurrence in that direction.");
        if (row.Column is not null) return ChartSource.MoveRow(t, peers.Select(r => r.Identity).ToArray(), identity, direction);
        var a = peers[Math.Min(at, next)]; var b = peers[Math.Max(at, next)]; var ae = ColumnEnd(t, m, a); var be = ColumnEnd(t, m, b);
        Group(t, m, t.AttachedStart(a.Identity), ae); Group(t, m, t.AttachedStart(b.Identity), be); return SourceRowMoves.Swap(t, a.Identity, b.Identity, ae, be);
    }, token);
    public Task<SourceAuthoringResult> MoveCardAsync(string identity, string destination, CancellationToken token = default) => ChangeAsync((t, m) =>
    {
        Structure(t, m); var row = m.Cards.Single(r => r.Identity == identity); var column = m.Columns.Single(r => r.Identity == destination); if (row.Column == destination) return t;
        SourceRowMoves.CheckTrailingAttachment(t, identity); var start = t.AttachedStart(identity); var end = t.Index(identity) + 1; var to = ColumnEnd(t, m, column); Group(t, m, t.Index(destination), to);
        var indent = m.Cards.FirstOrDefault(c => c.Column == destination) is { } card ? t.Line(card.Identity).Indent : t.Line(destination).Indent + "    ";
        var changed = t.Splice(start, end - start, t.Lines.Skip(start).Take(end - start).Select(l => l with { Text = indent + l.Text[l.Indent.Length..] })); changed = SourceSections.MoveTo(changed, start, end, to);
        var scanned = Scan(changed);
        if (!scanned.Rows.Select(r => (r.Identity, r.Column)).OrderBy(r => r.Identity).SequenceEqual(m.Rows.Select(r => (r.Identity, r.Identity == identity ? destination : r.Column)).OrderBy(r => r.Identity))) throw new ArgumentException("This move could not prove unchanged column/card membership.");
        return changed;
    }, token);
}
