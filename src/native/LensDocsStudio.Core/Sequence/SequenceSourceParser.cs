using System.Text.RegularExpressions;
using LensDocsStudio.Core.Mermaid;

namespace LensDocsStudio.Core.Sequence;

/// <summary>Bounded lexical source model. Unknown statements are retained and fail dependency analysis closed.</summary>
public sealed class SequenceSourceParser
{
    public static readonly IReadOnlyList<string> Arrows = Array.AsReadOnly(new[] { "->>", "-->>", "->", "-->", "-)", "--)", "-x", "--x", "<<->>", "<<-->>" });
    private const string Id = "[A-Za-z_][A-Za-z0-9_]*";
    private static Regex Pattern(string value) => new(value, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly (SequenceStatementKind Kind, Regex Pattern)[] Patterns =
    [
        (SequenceStatementKind.Participant, Pattern($@"^(?<type>participant|actor)\s+(?<id>{Id})(?:\s+as\s+(?<label>.+))?$")),
        (SequenceStatementKind.Note, Pattern($@"^(?i:Note)\s+(?<placement>(?i:left of|right of|over))\s+(?<first>{Id})(?:\s*,\s*(?<second>{Id}))?\s*:\s*(?<label>.+)$")),
        (SequenceStatementKind.Message, Pattern($@"^(?<from>{Id})\s*(?<arrow><<-->>|<<->>|-->>|->>|--\)|-\)|--x|-x|-->|->)\s*(?<marker>[+-]?)(?<to>{Id})\s*:\s*(?<label>.+)$")),
        (SequenceStatementKind.Activation, Pattern($@"^(?<verb>activate|deactivate)\s+(?<id>{Id})$")),
        (SequenceStatementKind.Block, Pattern(@"^(?<type>loop|alt|opt|par|critical|break|rect)(?:\s+(?<label>.*))?$")),
        (SequenceStatementKind.Divider, Pattern(@"^(?<type>else|and)(?:\s+(?<label>.*))?$")),
        (SequenceStatementKind.Autonumber, Pattern(@"^autonumber(?:\s+(?<start>\d+))?(?:\s+(?<step>\d+))?$"))
    ];
    public static bool IsSequence(string source) => source.ReplaceLineEndings("\n").Split('\n')
        .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("%%", StringComparison.Ordinal))?.Trim() == "sequenceDiagram";
    public static bool ValidId(string value) => Pattern("^" + Id + "$").IsMatch(value);
    public static string NormaliseLabel(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"') value = value[1..^1];
        return Regex.Replace(value, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))
            .Replace("\\n", "\n", StringComparison.Ordinal);
    }
    public static string EncodeLabel(string text)
    {
        var encoded = text.ReplaceLineEndings("<br/>");
        var plain = Regex.Replace(encoded, @"<br\s*/?>", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (encoded.Length is 0 or > 4096 || encoded.Trim() != encoded || plain.Any(char.IsControl)
            || plain.IndexOfAny(['<', '>', '"', '*', '~']) >= 0 || plain.Contains("%%", StringComparison.Ordinal))
            throw new ArgumentException("Use a non-empty label of up to 4,096 characters. Plain Unicode text and line breaks are supported; markup and control characters require Edit source.");
        return encoded;
    }
    public SequenceDiagramModel Parse(string source, CancellationToken cancellationToken = default)
    {
        var statements = new List<SequenceStatement>(); var diagnostics = new List<string>();
        var participants = new List<SequenceParticipant>(); var messages = new List<SequenceMessage>();
        var notes = new List<SequenceNote>(); var activations = new List<SequenceActivation>();
        if (source.Length > 100_000) return new(source, [], [], [], [], [], ["The Sequence source exceeds the 100,000-character limit."]);
        var offset = 0; var depth = 0; var header = false;
        foreach (var line in source.Split('\n'))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var raw = line.TrimEnd('\r'); var text = raw.Trim(); var start = offset + raw.Length - raw.TrimStart().Length;
            var lineSpan = new MermaidSourceSpan(offset, line.Length + (offset + line.Length < source.Length ? 1 : 0)); offset += lineSpan.Length;
            if (text.Length == 0 || text.StartsWith("%%", StringComparison.Ordinal) && !text.StartsWith("%%{", StringComparison.Ordinal)) continue;
            if (statements.Count >= 1000) { diagnostics.Add("The Sequence statement limit is 1,000."); break; }
            var kind = SequenceStatementKind.Protected; Match? match = null;
            if (!header && text == "sequenceDiagram") { header = true; kind = SequenceStatementKind.Header; }
            else if (text == "end") { kind = SequenceStatementKind.End; if (--depth < 0) diagnostics.Add("An end statement has no matching block."); }
            else foreach (var candidate in Patterns) if ((match = candidate.Pattern.Match(text)).Success) { kind = candidate.Kind; break; }
            var tokens = new Dictionary<string, MermaidSourceSpan>(StringComparer.Ordinal);
            if (match?.Success == true) foreach (var name in match.Groups.Keys.Where(name => name != "0"))
                if (match.Groups[name].Success) tokens[name] = new(start + match.Groups[name].Index, match.Groups[name].Length);
            var statement = new SequenceStatement(Guid.NewGuid().ToString("N"), kind, new(start, text.Length), lineSpan, tokens, depth);
            statements.Add(statement);
            string Get(string name) => statement.Read(source, name);
            if (kind == SequenceStatementKind.Protected) diagnostics.Add("Unrecognised or protected syntax: " + text[..Math.Min(90, text.Length)] + ". Use Edit source.");
            if (kind == SequenceStatementKind.Block) depth++;
            if (kind == SequenceStatementKind.Divider && depth == 0) diagnostics.Add("A block divider has no enclosing block.");
            if (kind == SequenceStatementKind.Participant)
            {
                if (participants.Any(item => item.Id == Get("id"))) diagnostics.Add("Repeated or late participant declarations require Edit source.");
                else participants.Add(new(Get("id"), tokens.ContainsKey("label") ? Get("label") : Get("id"), Get("type") == "actor", statement));
            }
            if (kind == SequenceStatementKind.Message)
            {
                Ensure(Get("from")); Ensure(Get("to"));
                messages.Add(new(statement.Identity, Get("from"), Get("to"), Get("arrow"), Get("label"), Get("marker"), statement));
                if (Get("marker").Length > 0) activations.Add(new(statement.Identity + ":activation", Get("marker") == "+" ? Get("to") : Get("from"), Get("marker") == "+", statement.Identity, statement, false));
                if (Get("arrow").StartsWith("<<", StringComparison.Ordinal) && Get("marker").Length > 0) diagnostics.Add("Bidirectional activation shorthand is protected.");
            }
            if (kind == SequenceStatementKind.Note)
            {
                Ensure(Get("first")); if (Get("second").Length > 0) Ensure(Get("second"));
                var placement = Get("placement").ToLowerInvariant();
                if (placement != "over" && Get("second").Length > 0) diagnostics.Add("Side notes with multiple participants require Edit source.");
                notes.Add(new(statement.Identity, placement, Get("second").Length == 0 ? [Get("first")] : [Get("first"), Get("second")], Get("label"), statement));
            }
            if (kind == SequenceStatementKind.Activation)
            {
                var previous = statements.Count > 1 ? statements[^2] : null;
                var message = messages.LastOrDefault(); var activate = Get("verb") == "activate";
                if (previous?.Kind != SequenceStatementKind.Message || message is null || message.Marker.Length > 0
                    || (activate ? message.Recipient : message.Sender) != Get("id")) diagnostics.Add("Standalone activation must follow its matching message without another marker.");
                else activations.Add(new(statement.Identity, Get("id"), activate, message.Identity, statement, true));
            }
            if (tokens.ContainsKey("label"))
            {
                try { EncodeLabel(NormaliseLabel(Get("label"))); }
                catch (ArgumentException) { diagnostics.Add("Rich label syntax is preserved for source editing."); }
            }
            void Ensure(string id) { if (participants.All(item => item.Id != id)) participants.Add(new(id, id, false, null)); }
        }
        if (!header || statements.FirstOrDefault()?.Kind != SequenceStatementKind.Header) diagnostics.Add("A sequenceDiagram header is required.");
        if (depth != 0) diagnostics.Add("Sequence block boundaries are unbalanced.");
        var active = new HashSet<string>(StringComparer.Ordinal);
        foreach (var activation in activations)
        {
            if (activation.Activate ? !active.Add(activation.Participant) : !active.Remove(activation.Participant))
                diagnostics.Add("Nested or unmatched activations require Edit source.");
        }
        // Open activations are renderer-supported; nested or unmatched lifecycles remain protected.
        return new(source, statements.AsReadOnly(), participants.AsReadOnly(), messages.AsReadOnly(), notes.AsReadOnly(), activations.AsReadOnly(), diagnostics.Distinct().ToArray());
    }
}
