using System.Text.RegularExpressions;
using LensDocsStudio.Core.Mermaid;
using Mermaider.Models;

namespace LensDocsStudio.Core.ErDiagrams;

/// <summary>Complete bounded line recognition for the pinned 0.15.1 ER grammar. Unknown remainders stay protected.</summary>
public sealed class ErDiagramSourceParser
{
    private static Regex Pattern(string pattern) => new(pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Entity = Pattern("^(?<id>[^\\s\\[{}]+)(?:\\[(?:\"(?<label>[^\"]+)\"|(?<bare>[^\\]]+))\\])?\\s*\\{$");
    private static readonly Regex Attribute = Pattern("^(?<type>\\S+)\\s+(?<name>\\S+)(?:\\s+(?<rest>.+))?$");
    private static readonly Regex SafeAttribute = Pattern("^(?<type>[A-Za-z_][A-Za-z0-9_\\[\\]().-]*)[ \\t]+(?<name>[\\p{L}_][\\p{L}\\p{M}\\p{N}_.-]*)(?<keys>(?: +(?:[Pp][Kk]|[Ff][Kk]|[Uu][Kk]))*)(?: +\"(?<comment>[^\"\\r\\n]*)\")?$");
    private static readonly Regex Relationship = Pattern("^(?<from>\\S+)\\s+(?<left>[|o}{]+)(?<style>--|\\.\\.)(?<right>[|o}{]+)\\s+(?<to>[^\\s:]+)(?:\\s*:\\s*(?<label>.+))?$");
    private static readonly Regex Comment = Pattern("\"([^\"]*)\"");
    private static readonly Regex Break = new("<br\\s*/?>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex EscapedNewline = Pattern(@"\\n");
    private static readonly Regex UnsupportedFormatting = new(@"</?(?:sub|sup|small|mark)\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Bold = Pattern(@"\*\*(.+?)\*\*");
    private static readonly Regex Italic = Pattern(@"(?<!\*)\*([^\s*](?:[^*]*[^\s*])?)\*(?!\*)");
    private static readonly Regex Strike = Pattern(@"~~(.+?)~~");
    private static readonly Regex DirectionLine = new("^direction\\s+(TD|TB|LR|BT|RL)\\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    internal static string Normalise(string value)
    {
        // Match MultilineUtils.NormalizeBrTags at the pinned parser boundary, including
        // escaped line breaks and formatting. Original source tokens are kept separately.
        var result = value is ['"', .., '"'] ? value[1..^1] : value;
        result = Break.Replace(result, "\n"); result = EscapedNewline.Replace(result, "\n");
        result = UnsupportedFormatting.Replace(result, ""); result = Bold.Replace(result, "<b>$1</b>");
        result = Italic.Replace(result, "<i>$1</i>"); return Strike.Replace(result, "<s>$1</s>");
    }
    public static bool IsEr(string source) => source.Split('\n').Select(line => line.Trim()).FirstOrDefault(line => line.Length > 0 && !line.StartsWith("%%", StringComparison.Ordinal)) == "erDiagram";
    public ErDiagramModel Parse(string source, CancellationToken cancellationToken = default)
    {
        var statements = new List<ErStatement>(); var diagnostics = new List<string>(); var entities = new List<ErEntityModel>(); var relationships = new List<ErRelationshipModel>();
        string? owner = null; Direction? direction = null; var header = false;
        if (source.Length > 100_000) return new(source, [], [], [], null, ["The ER source exceeds the 100,000-character limit."]);
        ErEntityModel Ensure(string id)
        { var existing = entities.FirstOrDefault(item => item.Id == id); if (existing is not null) return existing; var entity = new ErEntityModel(id, id, [], []); entities.Add(entity); return entity; }
        void Set(ErEntityModel entity) => entities[entities.FindIndex(item => item.Id == entity.Id)] = entity;
        for (var start = 0; start < source.Length;)
        {
            cancellationToken.ThrowIfCancellationRequested(); var end = source.IndexOf('\n', start); var next = end < 0 ? source.Length : end + 1;
            var raw = source[start..(end < 0 ? source.Length : end)]; var text = raw.Trim(); var offset = start + raw.Length - raw.TrimStart().Length;
            var span = new MermaidSourceSpan(offset, text.Length); var line = new MermaidSourceSpan(start, next - start); start = next;
            if (text.Length == 0 || text.StartsWith("%%", StringComparison.Ordinal) && !text.StartsWith("%%{", StringComparison.Ordinal)) continue;
            var identity = Guid.NewGuid().ToString("N");
            var tokens = new Dictionary<string, MermaidSourceSpan>();
            ErStatement Statement(ErStatementKind kind, Match? match = null, bool protect = false)
            {
                if (match is not null) foreach (var name in match.Groups.Keys.Where(key => !int.TryParse(key, out _))) if (match.Groups[name].Success) tokens[name] = new(offset + match.Groups[name].Index, match.Groups[name].Length);
                var statement = new ErStatement(identity, kind, span, line, owner, tokens, protect); statements.Add(statement); return statement;
            }
            if (text.StartsWith("%%{", StringComparison.Ordinal)) { Statement(ErStatementKind.Unknown, protect: true); continue; }
            if (!header) { header = true; Statement(ErStatementKind.Header); if (text != "erDiagram") diagnostics.Add("Expected an exact erDiagram header."); continue; }
            if (owner is not null)
            {
                if (text == "}") { Statement(ErStatementKind.End); owner = null; continue; }
                var match = Attribute.Match(text);
                if (!match.Success) { Statement(ErStatementKind.Unknown, protect: true); continue; }
                var safe = SafeAttribute.Match(text); var statement = Statement(ErStatementKind.Attribute, safe.Success ? safe : match, !safe.Success);
                var rest = match.Groups["rest"].Value; var comment = Comment.Match(rest); var keys = Comment.Replace(rest, "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Where(part => part.ToUpperInvariant() is "PK" or "FK" or "UK").Select(part => Enum.Parse<ErKeyType>(part, true)).ToArray();
                var attribute = new ErAttributeModel(identity, owner, match.Groups["type"].Value, match.Groups["name"].Value, keys, comment.Success ? Normalise(comment.Groups[1].Value) : null, statement);
                var entity = Ensure(owner); Set(entity with { Attributes = entity.Attributes.Append(attribute).ToArray() }); continue;
            }
            var dir = DirectionLine.Match(text);
            if (dir.Success) { Statement(ErStatementKind.Direction); direction = Enum.Parse<Direction>(dir.Groups[1].Value.ToUpperInvariant()); continue; }
            var block = Entity.Match(text);
            if (block.Success)
            {
                var statement = Statement(ErStatementKind.Entity, block, !ValidId(block.Groups["id"].Value)); owner = block.Groups["id"].Value;
                var entity = Ensure(owner); var label = block.Groups["label"].Success ? block.Groups["label"].Value : block.Groups["bare"].Success ? block.Groups["bare"].Value : entity.Label;
                Set(entity with { Label = label, Declarations = entity.Declarations.Append(statement).ToArray() }); continue;
            }
            var relationship = Relationship.Match(text);
            if (relationship.Success && Cardinality(relationship.Groups["left"].Value) is { } first && Cardinality(relationship.Groups["right"].Value) is { } second)
            {
                var from = relationship.Groups["from"].Value; var to = relationship.Groups["to"].Value; var label = relationship.Groups["label"].Value.Trim().Trim('"', '\'');
                var quoted = relationship.Groups["label"].Value; var safeLabel = quoted.Length == 0 || quoted[0] is not ('"' or '\'') || quoted.Length >= 2 && quoted[^1] == quoted[0] && !quoted[1..^1].Contains(quoted[0]);
                var statement = Statement(ErStatementKind.Relationship, relationship, !ValidId(from) || !ValidId(to) || !safeLabel);
                Ensure(from); Ensure(to); relationships.Add(new(identity, from, to, first, second, Normalise(label), relationship.Groups["style"].Value == "--", statement)); continue;
            }
            Statement(ErStatementKind.Unknown, protect: true);
        }
        if (!header) diagnostics.Add("An ER header is required."); if (owner is not null) diagnostics.Add("An entity block is not closed.");
        return new(source, statements, entities, relationships, direction, diagnostics);
    }
    public static bool ValidId(string value) => value.Length is > 0 and <= 200 && Regex.IsMatch(value, @"^[\p{L}_][\p{L}\p{M}\p{N}_]*$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    public static ErCardinality? Cardinality(string token) => new string(token.Replace('}', '{').Order().ToArray()) switch { "||" => ErCardinality.One, "o|" => ErCardinality.ZeroOne, "{|" => ErCardinality.Many, "o{" => ErCardinality.ZeroMany, _ => null };
    public static string Notation(ErCardinality cardinality, bool first) => cardinality switch { ErCardinality.One => "||", ErCardinality.ZeroOne => first ? "|o" : "o|", ErCardinality.Many => first ? "}|" : "|{", ErCardinality.ZeroMany => first ? "}o" : "o{", _ => throw new ArgumentException("Choose a supported cardinality.") };
}
