using System.Text.RegularExpressions;
using LensDocsStudio.Core.Mermaid;
using Mermaider.Models;

namespace LensDocsStudio.Core.ClassDiagrams;

/// <summary>Independent source scan matching the pinned parser, with stricter, complete authoring recognition.
/// Unknown bytes are retained even when Mermaider ignores them or treats them as broad member text.</summary>
public sealed class ClassDiagramSourceParser
{
    public const string Starter = "classDiagram\n    class Document {\n        +string Title\n        +Open(path) Document\n    }\n    class MarkdownDocument {\n        +Render() string\n    }\n    Document <|-- MarkdownDocument\n";
    private static Regex Pattern(string value, RegexOptions flags = RegexOptions.None) => new(value, flags | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Declaration = Pattern(@"^class\s+(?<id>\S+?)(?:\s*~(?<generic>\w+)~)?(?<block>\s*\{)?\s*$");
    private static readonly Regex InlineAnnotation = Pattern(@"^class\s+(?<id>\S+?)\s*\{\s*<<(?<annotation>\w+)>>\s*\}$");
    private static readonly Regex Annotation = Pattern(@"^<<(?<annotation>\w+)>>$");
    private static readonly Regex Namespace = Pattern(@"^namespace\s+(?<name>\S+)\s*\{$");
    private static readonly Regex DirectionLine = Pattern(@"^direction\s+(?<direction>TD|TB|LR|BT|RL)\s*$", RegexOptions.IgnoreCase);
    private static readonly Regex InlineMember = Pattern(@"^(?<id>\S+?)\s*:\s*(?<member>.+)$");
    private static readonly Regex ArrowCheck = Pattern(@"<\|--|--|\*--|o--|-->|\.\.>|\.\.\|>");
    private static readonly Regex Relation = Pattern("^(?<from>\\S+?)\\s+(?:\"(?<first>[^\"]*?)\"\\s+)?(?<arrow><\\|--|<\\|\\.\\.|\\*--|o--|-->|--\\*|--o|--\\|>|\\.\\.>|\\.\\.\\|>|<--|<\\.\\.?|--)\\s+(?:\"(?<second>[^\"]*?)\"\\s+)?(?<to>\\S+?)(?:\\s*:\\s*(?<label>.+))?$");
    private static readonly Regex Lollipop = Pattern(@"^(?<from>\S+?)\s+(?<arrow>--\(\)|\.\.?\(\)|\(\)--|\.?\(\)\.\.)\s+(?<to>\S+?)(?:\s*:\s*(?<label>.+))?$");
    private static readonly Regex Note = Pattern("^note\\s+(?:for\\s+(?<id>\\S+)\\s+)?\"(?<text>[^\"]+)\"$");
    private static readonly Regex Method = Pattern(@"^(?<name>.+?)\((?<parameters>[^)]*)\)(?:\s*(?<type>.+))?$");
    private static readonly Regex Break = Pattern(@"<br\s*/?>", RegexOptions.IgnoreCase);
    private static readonly Regex EscapedNewline = Pattern(@"\\n");
    private static readonly Regex UnsupportedTags = Pattern(@"</?(?:sub|sup|small|mark)\s*>", RegexOptions.IgnoreCase);
    private static readonly Regex Bold = Pattern(@"\*\*(.+?)\*\*");
    private static readonly Regex Italic = Pattern(@"(?<!\*)\*([^\s*](?:[^*]*[^\s*])?)\*(?!\*)");
    private static readonly Regex Strike = Pattern(@"~~(.+?)~~");
    private static readonly Regex SafeAttribute = Pattern(@"^(?<visibility>[+\-#~]?)(?<afterVisibility>[ \t]*)(?:(?<type>[\p{L}_][\p{L}\p{M}\p{N}_~\[\],.?]*) +)?(?<name>[\p{L}_][\p{L}\p{M}\p{N}_]*)(?<modifier>[$*]?);?$");
    private static readonly Regex SafeMethod = Pattern(@"^(?<visibility>[+\-#~]?)(?<afterVisibility>[ \t]*)(?<name>[\p{L}_][\p{L}\p{M}\p{N}_]*)(?<modifier>[$*]?)\((?<parameters>[\p{L}\p{M}\p{N}_ ,.:?~\[\]=+\-]*)\)(?:[ \t]+(?<type>[\p{L}_][\p{L}\p{M}\p{N}_~\[\],.?]*))?;?$");
    public static bool IsClass(string source) => string.Equals(source.Split('\n').Select(item => item.Trim()).FirstOrDefault(item => item.Length > 0 && !item.StartsWith("%%", StringComparison.Ordinal)), "classDiagram", StringComparison.OrdinalIgnoreCase);
    public static bool ValidId(string value) => value.Length is > 0 and <= 200 && Pattern(@"^[\p{L}_][\p{L}\p{M}\p{N}_]*$").IsMatch(value);
    internal static string? Empty(string? text) => string.IsNullOrEmpty(text) ? null : text;
    internal static string Normalise(string text)
    {
        // Independently match the pinned label boundary, including its double-quote
        // stripping. Original tokens remain separate from these renderer semantics.
        var value = text is ['"', .., '"'] ? text[1..^1] : text;
        value = Break.Replace(value, "\n"); value = EscapedNewline.Replace(value, "\n");
        value = UnsupportedTags.Replace(value, ""); value = Bold.Replace(value, "<b>$1</b>");
        value = Italic.Replace(value, "<i>$1</i>"); return Strike.Replace(value, "<s>$1</s>");
    }
    internal static (ClassRelationType Type, ClassMarkerAt Marker)? Arrow(string text) => text switch
    {
        "<|--" => (ClassRelationType.Inheritance, ClassMarkerAt.From), "--|>" => (ClassRelationType.Inheritance, ClassMarkerAt.To),
        "*--" => (ClassRelationType.Composition, ClassMarkerAt.From), "--*" => (ClassRelationType.Composition, ClassMarkerAt.To),
        "o--" => (ClassRelationType.Aggregation, ClassMarkerAt.From), "--o" => (ClassRelationType.Aggregation, ClassMarkerAt.To),
        "<--" => (ClassRelationType.Association, ClassMarkerAt.From), "-->" or "--" => (ClassRelationType.Association, ClassMarkerAt.To),
        "<.." => (ClassRelationType.Dependency, ClassMarkerAt.From), "..>" => (ClassRelationType.Dependency, ClassMarkerAt.To),
        "<|.." => (ClassRelationType.Realization, ClassMarkerAt.From), "..|>" => (ClassRelationType.Realization, ClassMarkerAt.To), _ => null
    };
    internal static ClassMember? ParseMember(string text)
    {
        var rest = text.Trim().TrimEnd(';'); if (rest.Length == 0) return null;
        var visibility = rest[0] switch { '+' => ClassVisibility.Public, '-' => ClassVisibility.Private, '#' => ClassVisibility.Protected, '~' => ClassVisibility.Package, _ => ClassVisibility.None };
        if (visibility != ClassVisibility.None) rest = rest[1..].TrimStart();
        var method = Method.Match(rest);
        if (method.Success) return new(visibility, method.Groups["name"].Value.Trim().TrimEnd('$', '*'), Empty(method.Groups["type"].Value.Trim()), rest.Contains('$'), rest.Contains('*'), true, Empty(method.Groups["parameters"].Value.Trim()));
        var parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries); var name = parts.Length >= 2 ? string.Join(' ', parts[1..]) : parts.Length > 0 ? parts[0] : rest;
        return new(visibility, name.TrimEnd('$', '*'), parts.Length >= 2 ? parts[0] : null, name.EndsWith('$'), name.EndsWith('*'));
    }
    internal static bool PortableType(string? type) => type is null || type.Length <= 2000 && Pattern(@"^[\p{L}_][\p{L}\p{M}\p{N}_]*(?:\.[\p{L}_][\p{L}\p{M}\p{N}_]*)*(?:~[\p{L}_][\p{L}\p{M}\p{N}_]*~)?(?:\[\])*\??$").IsMatch(type);
    public ClassDiagramModel Parse(string source, CancellationToken cancellationToken = default)
    {
        using var diagnosticPhase = LensDocsStudio.Core.Rendering.MermaidDiagnostics.Measure("source.scan.Class");
        cancellationToken.ThrowIfCancellationRequested();
        try { return ParseCore(source, cancellationToken); }
        catch (RegexMatchTimeoutException) { return Invalid(source, "The Class source scan exceeded its safe time budget. The source is unchanged; use Edit source."); }
    }
    private static ClassDiagramModel Invalid(string source, string diagnostic) => new(source, [], [], [], [], [], null, [diagnostic]);
    private static ClassDiagramModel ParseCore(string source, CancellationToken cancellationToken)
    {
        var statements = new List<ClassDiagramStatement>(); var classes = new List<ClassDiagramClass>(); var relationships = new List<ClassDiagramRelationship>();
        var namespaces = new List<ClassDiagramNamespace>(); var notes = new List<ClassDiagramNote>(); var errors = new List<string>();
        string? owner = null; ClassDiagramStatement? ns = null; var nsClasses = new List<string>(); Direction? direction = null; var header = false; var memberCount = 0;
        if (source.Length > 100_000) return new(source, [], [], [], [], [], null, ["The Class source exceeds the 100,000-character limit."]);
        ClassDiagramClass Ensure(string id)
        { var found = classes.FirstOrDefault(item => item.Id == id); if (found is not null) return found; var added = new ClassDiagramClass(Guid.NewGuid().ToString("N"), id, id, null, [], []); classes.Add(added); return added; }
        void Set(ClassDiagramClass item) => classes[classes.FindIndex(value => value.Id == item.Id)] = item;
        for (var start = 0; start < source.Length;)
        {
            // Bound source-model work before immutable occurrence arrays can grow
            // quadratically. These limits do not raise any existing renderer budget.
            if (classes.Count + relationships.Count + memberCount + namespaces.Count + notes.Count > 500 || statements.Count > 1500)
                return Invalid(source, "The Class source exceeds the 500-element or 1,500-statement visual source budget. The source is unchanged; use Edit source.");
            cancellationToken.ThrowIfCancellationRequested(); var end = source.IndexOf('\n', start); var next = end < 0 ? source.Length : end + 1;
            var raw = source[start..(end < 0 ? source.Length : end)]; var text = raw.Trim(); var offset = start + raw.Length - raw.TrimStart().Length;
            var line = new MermaidSourceSpan(start, next - start); var span = new MermaidSourceSpan(offset, text.Length); start = next;
            if (text.Length == 0 || text.StartsWith("%%", StringComparison.Ordinal) && !text.StartsWith("%%{", StringComparison.Ordinal)) continue;
            var identity = Guid.NewGuid().ToString("N");
            ClassDiagramStatement Statement(ClassStatementKind kind, Match? match = null, bool protect = false, int tokenOffset = 0)
            {
                var tokens = new Dictionary<string, MermaidSourceSpan>();
                if (match is not null) foreach (var name in match.Groups.Keys.Where(key => !int.TryParse(key, out _))) if (match.Groups[name].Success) tokens[name] = new(offset + tokenOffset + match.Groups[name].Index, match.Groups[name].Length);
                var statement = new ClassDiagramStatement(identity, kind, span, line, owner, ns?.Identity, tokens, protect); statements.Add(statement); return statement;
            }
            void Member(string id, string value, int memberOffset, Match? inline = null)
            {
                var semantic = ParseMember(value); var safe = (semantic?.IsMethod == true ? SafeMethod : SafeAttribute).Match(value);
                var supported = semantic is not null && !semantic.IsAbstract && !(semantic.IsMethod && semantic.IsStatic) && PortableType(semantic.Type) && (semantic.Params?.Length ?? 0) <= 2000;
                var statement = Statement(ClassStatementKind.Member, safe.Success ? safe : null, !safe.Success || !ValidId(id) || !supported, memberOffset);
                ((Dictionary<string, MermaidSourceSpan>)statement.Tokens)["member"] = new(offset + memberOffset, value.Length);
                if (inline is not null) ((Dictionary<string, MermaidSourceSpan>)statement.Tokens)["id"] = new(offset + inline.Groups["id"].Index, inline.Groups["id"].Length);
                if (semantic is not null) { memberCount++; var cls = Ensure(id); Set(cls with { Members = cls.Members.Append(new ClassDiagramMember(identity, id, semantic, statement)).ToArray() }); }
            }
            if (text.StartsWith("%%{", StringComparison.Ordinal)) { Statement(ClassStatementKind.Unknown, protect: true); continue; }
            if (!header) { header = true; Statement(ClassStatementKind.Header); if (!string.Equals(text, "classDiagram", StringComparison.OrdinalIgnoreCase)) errors.Add("Expected a classDiagram header. Front matter requires Edit source."); continue; }
            if (owner is not null)
            {
                if (text == "}") { Statement(ClassStatementKind.EndClass); owner = null; continue; }
                var annotation = Annotation.Match(text);
                if (annotation.Success) { var statement = Statement(ClassStatementKind.Annotation, annotation); var cls = Ensure(owner); Set(cls with { Annotation = annotation.Groups["annotation"].Value }); continue; }
                Member(owner, text, 0); continue;
            }
            var dir = DirectionLine.Match(text); if (dir.Success) { Statement(ClassStatementKind.Direction, dir); direction = Enum.Parse<Direction>(dir.Groups["direction"].Value.ToUpperInvariant()); continue; }
            var namespaceMatch = Namespace.Match(text);
            if (namespaceMatch.Success)
            {
                if (ns is not null) errors.Add("Nested namespace membership is ambiguous; use Edit source.");
                ns = Statement(ClassStatementKind.Namespace, namespaceMatch, !ValidId(namespaceMatch.Groups["name"].Value)); nsClasses = []; continue;
            }
            if (text == "}" && ns is not null)
            { Statement(ClassStatementKind.EndNamespace); namespaces.Add(new(ns.Identity, ns.Read(source, "name"), nsClasses.ToArray(), ns)); ns = null; nsClasses = []; continue; }
            var declaration = Declaration.Match(text);
            if (declaration.Success)
            {
                var id = declaration.Groups["id"].Value; var statement = Statement(ClassStatementKind.Class, declaration, !ValidId(id)); var cls = Ensure(id);
                var generic = declaration.Groups["generic"];
                Set(cls with { Label = generic.Success ? id + "<" + generic.Value + ">" : cls.Label, Declarations = cls.Declarations.Append(statement).ToArray() });
                if (ns is not null) nsClasses.Add(id); if (declaration.Groups["block"].Success) owner = id; continue;
            }
            var annot = InlineAnnotation.Match(text);
            if (annot.Success)
            {
                var id = annot.Groups["id"].Value; var statement = Statement(ClassStatementKind.Class, annot, !ValidId(id)); var cls = Ensure(id);
                Set(cls with { Annotation = annot.Groups["annotation"].Value, Declarations = cls.Declarations.Append(statement).ToArray() }); continue;
            }
            var inline = InlineMember.Match(text);
            if (inline.Success && !ArrowCheck.IsMatch(inline.Groups["member"].Value)) { Member(inline.Groups["id"].Value, inline.Groups["member"].Value, inline.Groups["member"].Index, inline); continue; }
            var note = Note.Match(text);
            if (note.Success) { var statement = Statement(ClassStatementKind.Note, note, note.Groups["id"].Success && !ValidId(note.Groups["id"].Value)); notes.Add(new(identity, new(Empty(note.Groups["id"].Value), note.Groups["text"].Value), statement)); continue; }
            var rel = Relation.Match(text); var lollipop = false;
            if (!rel.Success) { rel = Lollipop.Match(text); lollipop = rel.Success; }
            var arrow = rel.Success ? lollipop ? (ClassRelationType.Lollipop, ClassMarkerAt.To) : Arrow(rel.Groups["arrow"].Value) : null;
            if (rel.Success && arrow is { } parsed)
            {
                var from = rel.Groups["from"].Value; var to = rel.Groups["to"].Value; Ensure(from); Ensure(to);
                var label = rel.Groups["label"].Success ? (lollipop ? rel.Groups["label"].Value.Trim() : Normalise(rel.Groups["label"].Value.Trim())) : null;
                var first = rel.Groups["first"].Success ? Normalise(rel.Groups["first"].Value) : null; var second = rel.Groups["second"].Success ? Normalise(rel.Groups["second"].Value) : null;
                var rawLabel = rel.Groups["label"].Success ? rel.Groups["label"].Value.Trim() : null;
                var content = rawLabel is ['"', .., '"'] ? rawLabel[1..^1] : rawLabel;
                var statement = Statement(ClassStatementKind.Relationship, rel, lollipop || !ValidId(from) || !ValidId(to)
                    || !SafeText(content) || content != label || label is "" || !SafeText(first) || !SafeText(second)
                    || rel.Groups["first"].Success && rel.Groups["first"].Value != first || rel.Groups["second"].Success && rel.Groups["second"].Value != second);
                relationships.Add(new(identity, new(from, to, parsed.Item1, parsed.Item2, label, first, second), statement)); continue;
            }
            Statement(ClassStatementKind.Unknown, protect: true);
        }
        if (classes.Count + relationships.Count + memberCount + namespaces.Count + notes.Count > 500 || statements.Count > 1500)
            return Invalid(source, "The Class source exceeds the 500-element or 1,500-statement visual source budget. The source is unchanged; use Edit source.");
        if (!header) errors.Add("A classDiagram header is required."); if (owner is not null) errors.Add("A class block is not closed."); if (ns is not null) errors.Add("A namespace is not closed.");
        return new(source, statements, classes, relationships, namespaces, notes, direction, errors);
    }
    internal static bool SafeText(string? value) => value is null || value.Length <= 2000 && !value.Any(c => char.IsControl(c) || c is '"' or '\'' or '<' or '>' or '{' or '}' or ';') && !value.Contains("%%", StringComparison.Ordinal) && !value.Contains('\\');
}
