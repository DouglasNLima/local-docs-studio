using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;

namespace LensDocsStudio.Core.BlockDiagrams;

public sealed record BlockSourceToken(string Identity, string LineIdentity, int Ordinal, int Start, int Length, SourceField IdToken, SourceField? LabelToken, SourceField? SpanToken, string Shape, string LabelSource, int Span, bool Space, bool Partial)
{
    public string Id => IdToken.Value;
    public string Label => LabelToken?.Value ?? Id;
    public string SpanText => SpanToken?.Raw ?? "1";
}
public sealed record BlockSourceEdge(string Identity, SourceField SourceToken, SourceField TargetToken, SourceField? LabelToken, string Form)
{
    public string Source => SourceToken.Value;
    public string Target => TargetToken.Value;
    public string Label => LabelToken?.Value ?? "";
}
public sealed record BlockSourceModel(string HeaderIdentity, SourceDeclaration? Title, SourceField? ColumnsToken, int Columns, IReadOnlyList<BlockSourceToken> Tokens, IReadOnlyList<BlockSourceEdge> Edges, IReadOnlyList<string> Protected)
{
    public IReadOnlyList<BlockSourceToken> Visible => Tokens.Where(t => !t.Space).GroupBy(t => t.Id, StringComparer.Ordinal).Select(g => g.First()).ToArray();
    public bool Unique(string id) => Tokens.Count(t => !t.Space && t.Id == id) == 1;
    public bool TopologySafe => Protected.Count == 0 && Tokens.All(t => !t.Partial && (t.Space || Unique(t.Id))) && Edges.All(e => e.Source != e.Target && Unique(e.Source) && Unique(e.Target)) && Edges.Select(e => (e.Source, e.Target)).Distinct().Count() == Edges.Count && TopologySource.Acyclic(Visible.Select(t => t.Id), Edges.Select(e => (e.Source, e.Target)));
}

/// <summary>Flat native grid authoring with stable token occurrences, spacers and first-wins source protection.</summary>
public sealed class BlockVisualDraft : SourceDraft<BlockSourceModel>
{
    public const string Starter = "block-beta title Documentation grid\ncolumns 3\nreader[\"Reader\"] space editor(\"Editor\")\nsource[\"Markdown\"]:2 preview[\"Preview\"]\nreader --> editor\neditor -- \"Saves\" --> source\neditor -->|Renders| preview\n";
    private static readonly Regex Header = SourceText.Pattern(@"^[ \t]*block(?:-beta)?(?:\s+title\s+(?<label>.+))?[ \t]*$", RegexOptions.IgnoreCase);
    private static readonly Regex Title = SourceText.Pattern(@"^[ \t]*title\s+(?<label>.+?)[ \t]*$");
    private static readonly Regex Columns = SourceText.Pattern(@"^[ \t]*columns\s+(?<count>\d+)[ \t]*$", RegexOptions.IgnoreCase);
    private static readonly Regex Edge = SourceText.Pattern("^[ \\t]*(?<source>[A-Za-z_][\\w-]*)\\s*(?:--\\s*(?:\"(?<quoted>[^\"]*)\"|(?<plain>[^>\"|]+?))\\s*)?-->\\s*(?:\\|(?<pipe>[^|]*)\\|\\s*)?(?<target>[A-Za-z_][\\w-]*)[ \\t]*$");
    public static bool IsBlock(string source) => Header.IsMatch(ChartSource.Header(source));
    public BlockVisualDraft(string source, DiagramService? diagrams = null) : base(source, diagrams) { }
    protected override BlockSourceModel Scan(SourceText text) => Read(text);
    public static BlockSourceModel Read(SourceText text)
    {
        var body = ChartSource.Body(text); if (body.Count == 0 || !Header.IsMatch(body[0].Body)) throw new ArgumentException("This editor requires flat block or block-beta source.");
        var match = Header.Match(body[0].Body); SourceDeclaration? title = match.Groups["label"].Success ? new(body[0].Key, "title", Unquote(match.Groups["label"].Value), match) : null;
        SourceField? columnsToken = null; var columns = 1; var tokens = new List<BlockSourceToken>(); var edges = new List<BlockSourceEdge>(); var protectedRows = new List<string>();
        foreach (var line in body.Skip(1))
        {
            match = Title.Match(line.Body); if (match.Success) { title = new(line.Key, "title", Unquote(match.Groups["label"].Value), match); continue; }
            match = Columns.Match(line.Body);
            if (match.Success) { if (int.TryParse(match.Groups["count"].Value, out var count) && count > 0) { columns = count; columnsToken = WorkflowSource.Field(line, "columns", match.Groups["count"]); } else protectedRows.Add(line.Key); continue; }
            match = Edge.Match(line.Body);
            if (match.Success)
            {
                var form = match.Groups["quoted"].Success ? "quoted" : match.Groups["plain"].Success ? "plain" : match.Groups["pipe"].Success ? "pipe" : "none";
                SourceField? label = form == "none" ? null : WorkflowSource.Field(line, "label", match.Groups[form]);
                if (label is not null && form != "quoted")
                {
                    var leading = label.Raw.Length - label.Raw.TrimStart().Length; var raw = label.Raw.Trim(); label = label with { Value = raw, Raw = raw, Start = label.Start + leading, Length = raw.Length };
                }
                edges.Add(new(line.Key, WorkflowSource.Field(line, "source", match.Groups["source"]), WorkflowSource.Field(line, "target", match.Groups["target"]), label, form)); continue;
            }
            var parsed = ParseLine(line); tokens.AddRange(parsed.Tokens); if (parsed.Partial) protectedRows.Add(line.Key);
        }
        return new(body[0].Key, title, columnsToken, columns, tokens, edges, protectedRows);
    }
    private static (IReadOnlyList<BlockSourceToken> Tokens, bool Partial) ParseLine(SourceLine line)
    {
        var body = line.Body; var tokens = new List<BlockSourceToken>(); var at = 0; var partial = false;
        while (at < body.Length)
        {
            while (at < body.Length && char.IsWhiteSpace(body[at])) at++;
            if (at == body.Length) break; var start = at;
            if (!(char.IsLetter(body[at]) || body[at] == '_')) { partial = true; break; }
            at++; while (at < body.Length && (char.IsLetterOrDigit(body[at]) || body[at] is '_' or '-')) at++;
            var id = new SourceField(line.Key, "id", body[start..at], start, at - start, body[start..at]); SourceField? label = null; SourceField? spanToken = null; var shape = "bare"; var labelSource = ""; var span = 1;
            if (at < body.Length && body[at] is '[' or '(')
            {
                var open = body[at++]; var close = open == '[' ? ']' : ')'; shape = open == '[' ? "rectangle" : "rounded"; var innerStart = at; var quoted = at < body.Length && body[at] == '"';
                if (quoted) at++; var labelStart = at; while (at < body.Length && body[at] != (quoted ? '"' : close)) at++;
                if (at == body.Length) { partial = true; break; }
                label = new(line.Key, "label", body[labelStart..at], labelStart, at - labelStart, body[labelStart..at]);
                if (quoted && (++at >= body.Length || body[at] != close)) { partial = true; break; }
                labelSource = body[innerStart..at]; at++;
            }
            if (at < body.Length && body[at] == ':')
            {
                var numberStart = at + 1; var numberEnd = numberStart; while (numberEnd < body.Length && char.IsAsciiDigit(body[numberEnd])) numberEnd++;
                if (numberEnd > numberStart && int.TryParse(body[numberStart..numberEnd], out var number) && number > 0)
                { span = number; spanToken = new(line.Key, "span", body[numberStart..numberEnd], numberStart, numberEnd - numberStart, body[numberStart..numberEnd]); at = numberEnd; }
            }
            tokens.Add(new(line.Occurrence(tokens.Count), line.Key, tokens.Count, start, at - start, id, label, spanToken, shape, labelSource, span, id.Value.Equals("space", StringComparison.OrdinalIgnoreCase), false));
        }
        return (tokens.Select(t => t with { Partial = partial }).ToArray(), partial);
    }
    private static string Unquote(string value) => value.Length >= 2 && (value[0] == '"' && value[^1] == '"' || value[0] == '\'' && value[^1] == '\'') ? value[1..^1] : value;
    protected override bool Contains(BlockSourceModel model, string identity) => identity == model.HeaderIdentity || model.Tokens.Any(t => t.Identity == identity) || model.Edges.Any(e => e.Identity == identity) || model.Protected.Contains(identity);
    protected override void Validate(SourceText text, BlockSourceModel model, DiagramResult rendered)
    {
        WorkflowSource.Rendered(text, rendered); var shapes = SourceDeclarations.Groups(rendered.Svg!, "node"); var visible = model.Visible;
        if (shapes.Length != visible.Count || shapes.Zip(visible).Any(p => (string?)p.First.Attribute("data-id") != p.Second.Id || (string?)p.First.Attribute("data-label") != p.Second.Label || (string?)p.First.Attribute("data-shape") != (p.Second.Shape == "rounded" ? "rounded" : "rectangle")))
            throw new ArgumentException("Flat block source tokens differ from the actual first-wins native grid. Apply is protected.");
        if (model.TopologySafe && EdgeCorrespondence(model, rendered.Svg!).Count != model.Edges.Count) throw new ArgumentException("The block link occurrences differ from their native endpoint geometry.");
    }
    public static IReadOnlyList<(BlockSourceToken Token, XElement Shape)> Correspondence(BlockSourceModel model, string svg)
    {
        var actual = SourceDeclarations.Groups(svg, "node"); if (actual.Length != model.Visible.Count) return [];
        // Source order proves the first declaration, even when a later declaration shares its renderer ID.
        return model.Visible.Zip(actual).Where(p => (string?)p.Second.Attribute("data-id") == p.First.Id && (string?)p.Second.Attribute("data-label") == p.First.Label && (string?)p.Second.Attribute("data-shape") == (p.First.Shape == "rounded" ? "rounded" : "rectangle")).Select(p => (p.First, p.Second)).ToArray();
    }
    public static IReadOnlyList<(BlockSourceEdge Edge, XElement Shape)> EdgeCorrespondence(BlockSourceModel model, string svg)
    {
        if (!model.TopologySafe) return []; var shapes = SourceDeclarations.Groups(svg, "block-edge"); if (shapes.Length != model.Edges.Count) return [];
        return model.Edges.Zip(shapes).Where(p => (string?)p.Second.Attribute("data-from") == p.First.Source && (string?)p.Second.Attribute("data-to") == p.First.Target).Select(p => (p.First, p.Second)).ToArray();
    }
    private static void Topology(BlockSourceModel model) { if (!model.TopologySafe) throw new ArgumentException("Partial lines, hidden/duplicate IDs, ignored syntax or unproved links protect grid topology. Original source remains available."); }
    private static int Integer(string value, int maximum = int.MaxValue)
    { if (!SourceText.Pattern(@"^[0-9]+$").IsMatch(value) || !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n < 1 || n > maximum) throw new ArgumentException("Enter a complete positive integer within the supported grid; no signs, decimals, overflow or coercion."); return n; }
    private static string Label(string value, string shape, bool quoted)
    {
        if (value.Length > 0) SourceText.Label(value);
        if (value.Contains(shape == "rounded" ? ')' : ']') || quoted && value.Contains('"') || !quoted && value.StartsWith('"')) throw new ArgumentException("This label has an unproved quote or delimiter for its existing Block source form. Pending input is retained.");
        return value;
    }
    public Task<SourceAuthoringResult> SetHeadingAsync(string title, string columns, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        if (title != (model.Title?.Value ?? ""))
        {
            SourceText.Label(title); if (title.IndexOfAny(['"', '\'']) >= 0) throw new ArgumentException("New Block titles with quote delimiters remain source-only.");
            text = SourceDeclarations.SetTitle(text, model.HeaderIdentity, model.Title, model.Title?.Tokens.Groups["label"].Value.StartsWith('"') == true ? "\"" + title + "\"" : title);
        }
        if (columns != (model.ColumnsToken?.Raw ?? "1"))
        {
            Integer(columns);
            text = model.ColumnsToken is null ? text.Insert(text.Index(model.HeaderIdentity) + 1, "columns " + columns) : model.ColumnsToken.Set(text, columns);
        }
        return text;
    }, token);
    public Task<SourceAuthoringResult> UpdateTokenAsync(string identity, string id, string label, string shape, string span, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        var item = model.Tokens.Single(t => t.Identity == identity); if (item.Partial || !item.Space && !model.Visible.Any(t => t.Identity == item.Identity)) throw new ArgumentException("A partial line or hidden declaration protects this token. The proved first declaration may retain independent fields.");
        var patches = new List<(SourceField, string)>();
        if (id != item.Id)
        {
            Topology(model); if (item.Space || id.Equals("space", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("space is reserved for spacer occurrences and cannot be renamed or targeted.");
            TopologySource.Identifier(id); if (model.Tokens.Any(t => !t.Space && t.Id == id)) throw new ArgumentException("That ID already exists."); patches.Add((item.IdToken, id));
            patches.AddRange(model.Edges.Where(e => e.Source == item.Id).Select(e => (e.SourceToken, id))); patches.AddRange(model.Edges.Where(e => e.Target == item.Id).Select(e => (e.TargetToken, id)));
        }
        if (span != item.SpanText) Integer(span, model.Columns);
        if (shape != item.Shape)
        {
            if (item.Space || !new[] { "bare", "rectangle", "rounded" }.Contains(shape)) throw new ArgumentException("Choose an explicit rectangle or rounded conversion for a real block.");
            if (shape == "bare" && label != id) throw new ArgumentException("A bare block couples its label to its ID. Set matching text before requesting that conversion.");
            var quoted = item.LabelSource.StartsWith('"'); var labelSource = label == item.Label && item.LabelToken is not null ? item.LabelSource : (quoted ? "\"" : "") + Label(label, shape, quoted) + (quoted ? "\"" : "");
            var raw = text.Line(item.LineIdentity).Body.Substring(item.Start, item.Length); var converted = id + (shape == "bare" ? "" : shape == "rounded" ? "(" + labelSource + ")" : "[" + labelSource + "]") + (span == item.SpanText ? item.SpanToken is null ? "" : ":" + item.SpanToken.Raw : ":" + span);
            patches.RemoveAll(p => p.Item1.Identity == item.LineIdentity); patches.Add((new(item.LineIdentity, "block", "", item.Start, item.Length, raw), converted));
        }
        else
        {
            if (!item.Space && label != item.Label)
            { if (item.LabelToken is null) throw new ArgumentException("A bare block couples label and ID. Choose an explicit shape conversion before changing only its label."); patches.Add((item.LabelToken, Label(label, item.Shape, item.LabelSource.StartsWith('"')))); }
            if (span != item.SpanText) patches.Add(item.SpanToken is not null ? (item.SpanToken, span) : (new(item.LineIdentity, "span-clause", "", item.Start + item.Length, 0, ""), ":" + span));
        }
        return TopologySource.Patch(text, patches);
    }, token);
    public Task<SourceAuthoringResult> AddBlockAsync(string id, string label, string shape = "rectangle", string span = "1", CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        Topology(model); Integer(span, model.Columns); TopologySource.Identifier(id); if (id.Equals("space", StringComparison.OrdinalIgnoreCase) || model.Tokens.Any(t => t.Id == id)) throw new ArgumentException("Choose a new real block ID; space is reserved.");
        if (shape is not ("rectangle" or "rounded")) throw new ArgumentException("Choose a rectangle or rounded block.");
        return text.Insert(text.Lines.Count, id + (shape == "rounded" ? "(" : "[") + "\"" + Label(label, shape, true) + "\"" + (shape == "rounded" ? ")" : "]") + (span == "1" ? "" : ":" + span));
    }, token);
    public Task<SourceAuthoringResult> AddSpaceAsync(string span = "1", CancellationToken token = default) => ChangeAsync((text, model) =>
    { Topology(model); Integer(span, model.Columns); return text.Insert(text.Lines.Count, "space" + (span == "1" ? "" : ":" + span)); }, token);
    private static void Endpoints(BlockSourceModel model, string source, string target)
    { if (source == target || !model.Unique(source) || !model.Unique(target)) throw new ArgumentException("Choose two existing distinct real block IDs. Spacers are never endpoints."); }
    public Task<SourceAuthoringResult> AddEdgeAsync(string source, string target, string label = "", CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        Topology(model); Endpoints(model, source, target); if (label.IndexOfAny(['|', '\r', '\n']) >= 0) throw new ArgumentException("A new pipe-label link cannot contain a pipe or line break.");
        var changed = text.Insert(text.Lines.Count, source + " -->" + (label.Length > 0 ? "|" + label + "|" : "") + " " + target); if (!Read(changed).TopologySafe) throw new ArgumentException("This link would introduce unproved parallel or cyclic topology."); return changed;
    }, token);
    public Task<SourceAuthoringResult> UpdateEdgeAsync(string identity, string source, string target, string label, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        Topology(model); Endpoints(model, source, target); var edge = model.Edges.Single(e => e.Identity == identity); var patches = new List<(SourceField, string)> { (edge.SourceToken, source), (edge.TargetToken, target) };
        if (label != edge.Label)
        {
            if (label.Length > 0) SourceText.Label(label);
            if (edge.Form == "quoted" && label.Contains('"') || edge.Form == "pipe" && label.Contains('|') || edge.Form == "plain" && (label.IndexOfAny(['>', '"', '|']) >= 0 || label.Length == 0)) throw new ArgumentException("The new label cannot preserve the selected link's existing delimiter form.");
            if (edge.LabelToken is not null) patches.Add((edge.LabelToken, label));
            else if (label.Length > 0) { if (label.Contains('|')) throw new ArgumentException("A new pipe label cannot contain a pipe."); patches.Add((new(edge.Identity, "label-clause", "", edge.TargetToken.Start, 0, ""), "|" + label + "| ")); }
        }
        var changed = TopologySource.Patch(text, patches); if (!Read(changed).TopologySafe) throw new ArgumentException("This link would introduce unproved parallel or cyclic topology."); return changed;
    }, token);
    public Task<SourceAuthoringResult> DeleteAsync(string identity, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        Topology(model); if (model.Edges.Any(e => e.Identity == identity)) return ChartSource.DeleteRow(text, identity);
        var item = model.Tokens.Single(t => t.Identity == identity); if (!item.Space && model.Edges.Any(e => e.Source == item.Id || e.Target == item.Id)) throw new ArgumentException("Remove incident links first. Block deletion never cascades or leaves dangling references.");
        var peers = model.Tokens.Where(t => t.LineIdentity == item.LineIdentity).ToArray(); if (peers.Length == 1) return ChartSource.DeleteRow(text, item.LineIdentity);
        var line = text.Line(item.LineIdentity).Replace(item.Start, item.Length, "") with { OccurrenceKeys = peers.Where(t => t.Identity != identity).Select(t => t.Identity).ToArray() }; return text.Replace(line);
    }, token);
    public Task<SourceAuthoringResult> MoveAsync(string identity, int direction, CancellationToken token = default) => ChangeAsync((text, model) =>
    {
        Topology(model); if (model.Edges.Any(e => e.Identity == identity)) return ChartSource.MoveRow(text, model.Edges.Select(e => e.Identity).ToArray(), identity, direction);
        var rows = model.Tokens.ToArray(); var at = Array.FindIndex(rows, t => t.Identity == identity); var next = at + Math.Sign(direction); if (at < 0 || next < 0 || next >= rows.Length) throw new ArgumentException("There is no grid occurrence in that direction.");
        var a = rows[Math.Min(at, next)]; var b = rows[Math.Max(at, next)]; var lineA = text.Line(a.LineIdentity); var lineB = text.Line(b.LineIdentity);
        var peersA = rows.Where(t => t.LineIdentity == a.LineIdentity).Select(t => t.Identity).ToArray(); var peersB = rows.Where(t => t.LineIdentity == b.LineIdentity).Select(t => t.Identity).ToArray();
        if (a.LineIdentity != b.LineIdentity && peersA.Length == 1 && peersB.Length == 1) return ChartSource.MoveRow(text, [a.LineIdentity, b.LineIdentity], direction < 0 ? b.LineIdentity : a.LineIdentity, direction);
        if (a.LineIdentity != b.LineIdentity && (text.AttachedStart(a.LineIdentity) != text.Index(a.LineIdentity) || text.AttachedStart(b.LineIdentity) != text.Index(b.LineIdentity))) throw new ArgumentException("Comments attached to a multi-token line protect cross-line token movement.");
        var rawA = lineA.Body.Substring(a.Start, a.Length); var rawB = lineB.Body.Substring(b.Start, b.Length);
        if (a.LineIdentity == b.LineIdentity)
        {
            (peersA[a.Ordinal], peersA[b.Ordinal]) = (peersA[b.Ordinal], peersA[a.Ordinal]);
            return text.Replace(lineA.Replace(b.Start, b.Length, rawA).Replace(a.Start, a.Length, rawB) with { OccurrenceKeys = peersA });
        }
        SourceRowMoves.CheckTrailingAttachment(text, a.LineIdentity); SourceRowMoves.CheckTrailingAttachment(text, b.LineIdentity); peersA[a.Ordinal] = b.Identity; peersB[b.Ordinal] = a.Identity;
        return text.Replace(lineA.Replace(a.Start, a.Length, rawB) with { OccurrenceKeys = peersA }).Replace(lineB.Replace(b.Start, b.Length, rawA) with { OccurrenceKeys = peersB });
    }, token);
}
