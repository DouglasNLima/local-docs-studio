using System.Text.RegularExpressions;
using LensDocsStudio.Core.Mermaid;

namespace LensDocsStudio.Core.StateDiagrams;

/// <summary>Lossless lexical spans for the pinned 0.15.1 grammar. Unknown statements retain their bytes and protect dependencies.</summary>
public sealed class StateDiagramSourceParser
{
    private const string Id = @"[\w-]+";
    private static Regex Pattern(string value) => new(value, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex HeaderPattern = Pattern(@"^stateDiagram(?:-v2)?$");
    private static readonly Regex Alias = Pattern("^state\\s+\"(?<label>[^\"]+)\"\\s+as\\s+(?<id>\\w+)\\s*(?<open>\\{)?$");
    private static readonly Regex Composite = Pattern(@"^state\s+(?<id>\w+)\s*\{$");
    private static readonly Regex Stereotype = Pattern(@"(?i)^state\s+(?<id>\w+)\s+<<(?<kind>choice|fork|join)>>$");
    private static readonly Regex TransitionPattern = Pattern(@"^(?<from>\[\*\]|[\w-]+)(?::::(?<fromClass>[\w][\w-]*))?\s*-->\s*(?<to>\[\*\]|[\w-]+)(?::::(?<toClass>[\w][\w-]*))?(?:\s*:\s*(?<label>.+))?$");
    private static readonly Regex Description = Pattern(@"^(?<id>[\w-]+)\s*:\s*(?<label>.+)$");
    private static readonly Regex Direction = Pattern(@"(?i)^direction\s+(?<direction>TD|TB|LR|BT|RL)$");
    private static readonly Regex Note = Pattern(@"(?i)^note\s+(?:left|right)\s+of\s+(?<id>\w[\w-]*)(?:\s*:\s*(?<label>.+))?$");
    private static readonly Regex Style = Pattern(@"^(?:class|style)\s+(?<ids>[\w,-]+)\s+.+$");
    private static readonly Regex ClassDef = Pattern(@"^classDef\s+\w+\s+.+$");
    private static readonly Regex Shorthand = Pattern(@"^(?<id>[\w-]+):::[\w][\w-]*$");
    private static readonly Regex Identifier = Pattern("^" + Id + "$");
    private static readonly Regex Breaks = Pattern(@"(?i)<br\s*/?>");
    private static readonly Regex Entities = Pattern(@"#(\d{1,7});");
    public static bool IsState(string source) => source.ReplaceLineEndings("\n").Split('\n').Select(item => item.Trim())
        .FirstOrDefault(item => item.Length > 0 && !item.StartsWith("%%", StringComparison.Ordinal)) is { } header && HeaderPattern.IsMatch(header);
    public static bool IsIdentifier(string value) => value.Length is > 0 and <= 100 && Identifier.IsMatch(value) && !value.StartsWith('_') && !value.All(char.IsDigit);
    public static string NormaliseLabel(string value) => DecodeEntities(Breaks.Replace(value, "\n"));
    private static string DecodeEntities(string value) => Entities.Replace(value, match => int.TryParse(match.Groups[1].Value, out var scalar)
        && System.Text.Rune.IsValid(scalar) && (scalar >= 32 || scalar is 9 or 10 or 13) ? char.ConvertFromUtf32(scalar) : match.Value);

    public StateDiagramModel Parse(string source, CancellationToken cancellationToken = default)
    {
        try { return ParseCore(source, cancellationToken); }
        catch (RegexMatchTimeoutException) { return new(source, "", [], [], [], [], [], ["State source inspection exceeded its lexical budget. The complete source is retained; use Edit source."]); }
    }
    private static StateDiagramModel ParseCore(string source, CancellationToken cancellationToken)
    {
        var statements = new List<StateSourceStatement>(); var transitions = new List<StateTransition>(); var groups = new List<StateGroup>();
        var pseudo = new List<StatePseudoNode>(); var diagnostics = new List<string>(); var stack = new Stack<string>();
        var first = new Dictionary<string, (string Label, string Kind, StateSourceStatement Statement)>();
        var declarations = new Dictionary<string, List<StateSourceStatement>>(); var references = new Dictionary<string, List<StateReference>>();
        var header = ""; int offset = 0, startCount = 0, endCount = 0; StateSourceStatement? pendingNote = null;
        if (source.Length > 100_000) return new(source, "", [], [], [], [], [], ["The State diagram exceeds the existing 100,000-character budget."]);
        foreach (var raw in source.Split('\n'))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (statements.Count >= 1000) { diagnostics.Add("The State source model exceeds the 1,000-statement inspection budget."); break; }
            var lineLength = Math.Min(raw.Length + 1, source.Length - offset); var text = raw.Trim();
            var start = offset + raw.Length - raw.TrimStart().Length; var span = new MermaidSourceSpan(start, text.Length);
            var line = new MermaidSourceSpan(offset, lineLength); offset += lineLength;
            if (text.Length == 0 || text.StartsWith("%%", StringComparison.Ordinal) && !text.StartsWith("%%{", StringComparison.Ordinal)) continue;
            var scope = stack.TryPeek(out var current) ? current : null;
            StateSourceStatement Add(StateStatementKind kind, Match? match = null)
            {
                var tokens = new Dictionary<string, MermaidSourceSpan>();
                if (match is not null) foreach (var name in new[] { "id", "label", "from", "to", "direction", "ids", "kind", "fromClass", "toClass" })
                    if (match.Groups[name].Success) tokens[name] = new(start + match.Groups[name].Index, match.Groups[name].Length);
                var statement = new StateSourceStatement(Guid.NewGuid().ToString("N"), kind, span, line, scope, tokens); statements.Add(statement); return statement;
            }
            void Reference(string id, MermaidSourceSpan token, StateSourceStatement statement, string label, string kind, bool declaration)
            {
                if (!first.ContainsKey(id)) first.Add(id, (label, kind, statement));
                if (!references.TryGetValue(id, out var refs)) references[id] = refs = [];
                refs.Add(new(id, token, scope, statement.Identity));
                if (declaration) { if (!declarations.TryGetValue(id, out var items)) declarations[id] = items = []; items.Add(statement); }
            }
            if (pendingNote is not null)
            {
                var expanded = pendingNote with { Span = new(pendingNote.Span.Start, span.End - pendingNote.Span.Start), Line = new(pendingNote.Line.Start, line.End - pendingNote.Line.Start) };
                statements[statements.IndexOf(pendingNote)] = expanded; pendingNote = text.Equals("end note", StringComparison.OrdinalIgnoreCase) ? null : expanded; continue;
            }
            if (text.StartsWith("%%{", StringComparison.Ordinal)) { Add(StateStatementKind.Unknown); continue; }
            if (header.Length == 0)
            { if (!HeaderPattern.IsMatch(text)) diagnostics.Add("The first statement must be stateDiagram-v2 or stateDiagram."); header = text; Add(StateStatementKind.Header); continue; }
            if (text == "}")
            {
                var closing = Add(StateStatementKind.End);
                if (!stack.TryPop(out var id)) diagnostics.Add("An unmatched composite closing delimiter is present.");
                else { var index = groups.FindLastIndex(item => item.Id == id); groups[index] = groups[index] with { Closing = closing }; }
                continue;
            }
            var match = Alias.Match(text); var composite = Composite.Match(text);
            if (composite.Success || match.Success && match.Groups["open"].Success)
            {
                if (!match.Success) match = composite; var opening = Add(StateStatementKind.Composite, match); var id = match.Groups["id"].Value;
                if (groups.Any(item => item.Id == id)) diagnostics.Add("Composite IDs must be unique: " + id);
                groups.Add(new(id, match.Groups["label"].Success ? DecodeEntities(match.Groups["label"].Value) : id, scope, opening, null));
                stack.Push(id); continue;
            }
            if (match.Success)
            { var statement = Add(StateStatementKind.Declaration, match); var id = match.Groups["id"].Value; Reference(id, statement.Tokens["id"], statement, NormaliseLabel(match.Groups["label"].Value), "state", true); continue; }
            match = Stereotype.Match(text);
            if (match.Success)
            { var statement = Add(StateStatementKind.Stereotype, match); var id = match.Groups["id"].Value; Reference(id, statement.Tokens["id"], statement, "", match.Groups["kind"].Value.ToLowerInvariant(), true); continue; }
            match = Direction.Match(text); if (match.Success) { Add(StateStatementKind.Direction, match); continue; }
            match = Note.Match(text); if (match.Success) { var statement = Add(StateStatementKind.Note, match); if (!match.Groups["label"].Success) pendingNote = statement; continue; }
            match = Style.Match(text); if (match.Success) { Add(StateStatementKind.Style, match); continue; }
            if (ClassDef.IsMatch(text)) { Add(StateStatementKind.Style); continue; }
            match = TransitionPattern.Match(text);
            if (match.Success)
            {
                var statement = Add(StateStatementKind.Transition, match);
                string Endpoint(string token, bool initial)
                {
                    var id = match.Groups[token].Value;
                    if (id != "[*]") { Reference(id, statement.Tokens[token], statement, id, "state", false); return id; }
                    var identity = "pseudo:" + (scope ?? "") + (initial ? ":initial" : ":final");
                    if (!pseudo.Any(item => item.Identity == identity))
                    { var number = initial ? ++startCount : ++endCount; pseudo.Add(new(identity, (initial ? "_start" : "_end") + (number > 1 ? number.ToString(System.Globalization.CultureInfo.InvariantCulture) : ""), scope, initial)); }
                    return identity;
                }
                var from = Endpoint("from", true); var to = Endpoint("to", false);
                transitions.Add(new(statement.Identity, from, to, NormaliseLabel(match.Groups["label"].Value), statement)); continue;
            }
            match = Shorthand.Match(text);
            if (match.Success) { var statement = Add(StateStatementKind.Style, match); var id = match.Groups["id"].Value; Reference(id, statement.Tokens["id"], statement, id, "state", false); continue; }
            match = Description.Match(text);
            if (match.Success) { var statement = Add(StateStatementKind.Description, match); var id = match.Groups["id"].Value; Reference(id, statement.Tokens["id"], statement, NormaliseLabel(match.Groups["label"].Value.Trim()), "state", true); continue; }
            Add(StateStatementKind.Unknown);
        }
        if (header.Length == 0) diagnostics.Add("A State diagram header is required.");
        if (stack.Count > 0) diagnostics.Add("A composite state is missing its closing delimiter.");
        if (pendingNote is not null) diagnostics.Add("A note is missing end note.");
        if (statements.Count > 1000 || first.Count + groups.Count + pseudo.Count > 500) diagnostics.Add("The State source model exceeds the bounded element budget.");
        var nodes = first.Where(pair => groups.All(item => item.Id != pair.Key)).Select(pair => new StateNode(pair.Key, pair.Value.Label, pair.Value.Kind, pair.Value.Statement,
            declarations.GetValueOrDefault(pair.Key) ?? [], references[pair.Key], references[pair.Key].Select(item => item.Scope ?? "").Distinct().ToArray())).ToArray();
        return new(source, header, statements.AsReadOnly(), nodes, transitions.AsReadOnly(), groups.AsReadOnly(), pseudo.AsReadOnly(), diagnostics.AsReadOnly());
    }
}
