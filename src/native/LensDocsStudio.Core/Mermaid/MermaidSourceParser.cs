using Mermaider;
using System.Net;
using System.Text.RegularExpressions;

namespace LensDocsStudio.Core.Mermaid;

/// <summary>A bounded lexical adapter. Unknown tokens fail closed; Mermaider independently verifies the graph.</summary>
public sealed class MermaidSourceParser
{
    public const int MaximumCharacters = 100_000, MaximumNodes = 300, MaximumElements = 500, MaximumDepth = 8;
    public const int MaximumLabelCharacters = 4096;
    private static readonly (string Open, string Close, FlowNodeShape Shape)[] Shapes =
    [ ("(((", ")))", FlowNodeShape.DoubleCircle), ("([", "])", FlowNodeShape.Terminal),
      ("((", "))", FlowNodeShape.Circle), ("[[", "]]", FlowNodeShape.Subroutine), ("[(", ")]", FlowNodeShape.Cylinder),
      ("{{", "}}", FlowNodeShape.Hexagon), ("[", "]", FlowNodeShape.Process), ("(", ")", FlowNodeShape.Rounded), ("{", "}", FlowNodeShape.Decision) ];

    public MermaidDiagramModel Parse(string source, CancellationToken cancellationToken = default)
    {
        var occurrences = new List<MermaidOccurrence>(); var edges = new List<MermaidEdge>();
        var groups = new List<MermaidSubgraph>(); var styles = new List<MermaidStyle>();
        var protectedRegions = new List<MermaidProtectedRegion>(); var stack = new Stack<GroupDraft>();
        string direction = "TD"; MermaidSourceSpan directionSpan = default; string? error = null;
        var header = false;
        try
        {
            if (source.Length > MaximumCharacters) throw new ArgumentException("Visual editing supports up to 100,000 source characters.");
            CheckUnicode(source);
            foreach (var statement in Statements(source))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var text = statement.Read(source);
                if (text.StartsWith("%%", StringComparison.Ordinal))
                {
                    protectedRegions.Add(new(statement, "Comment or directive preserved verbatim."));
                    // Init/configuration directives can change parsing, security and layout. Keep them inspectable only.
                    if (text.StartsWith("%%{", StringComparison.Ordinal)) error ??= "Mermaid configuration directives are protected. Edit this diagram in source mode.";
                    continue;
                }
                if (!header)
                {
                    var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length != 2 || words[0] is not ("flowchart" or "graph") || !IsDirection(words[1]))
                        throw new ArgumentException("Visual editing supports flowchart and graph with TD, TB, BT, LR or RL direction.");
                    direction = words[1]; directionSpan = new(statement.End - direction.Length, direction.Length); header = true; continue;
                }
                if (text == "end")
                {
                    if (stack.Count == 0) throw new ArgumentException("A subgraph end has no matching opening.");
                    var group = stack.Pop(); groups.Add(new(group.Id, group.Label, group.ParentId, group.Opening,
                        group.Heading, statement, group.DirectionSpan, group.Direction)); continue;
                }
                if (text.StartsWith("subgraph ", StringComparison.Ordinal) || text.StartsWith("subgraph\t", StringComparison.Ordinal))
                {
                    if (stack.Count >= MaximumDepth) throw new ArgumentException("Visual editing supports at most eight nested subgraphs.");
                    var index = 8; SkipSpace(text, ref index); var idStart = index; var id = ReadId(text, ref index);
                    SkipSpace(text, ref index); var label = id;
                    var heading = new MermaidSourceSpan(statement.Start + idStart, text.Length - idStart);
                    if (index < text.Length && text[index] == '[' && text.EndsWith(']'))
                    { label = DecodeLabel(text[(index + 1)..^1]); index = text.Length; }
                    if (id.Length == 0 || index != text.Length) throw new ArgumentException("Use subgraph ID[Label] or subgraph ID for visual editing.");
                    if (groups.Any(group => group.Id == id) || stack.Any(group => group.Id == id)) throw new ArgumentException("Subgraph IDs must be unique.");
                    stack.Push(new(id, label, stack.TryPeek(out var parent) ? parent.Id : null, statement, heading)); continue;
                }
                if (text.StartsWith("direction ", StringComparison.Ordinal))
                {
                    var value = text[10..].Trim();
                    if (stack.Count == 0 || !IsDirection(value)) throw new ArgumentException("Use a supported direction inside a subgraph.");
                    var group = stack.Peek(); group.Direction = value; group.DirectionSpan = statement; continue;
                }
                if (TryStyle(text, statement, styles)) continue;
                var position = 0;
                var scope = stack.TryPeek(out var current) ? current.Id : null;
                try
                {
                    var first = ReadNode(text, ref position, statement, scope);
                    var local = new List<MermaidOccurrence> { first }; var localEdges = new List<MermaidEdge>();
                    while (true)
                    {
                        SkipSpace(text, ref position); if (position == text.Length) break;
                        var connectionStart = position;
                        var type = ReadConnection(text, ref position); var connectionEnd = position;
                        SkipSpace(text, ref position); MermaidSourceSpan? labelSpan = null; var label = "";
                        if (position < text.Length && text[position] == '|')
                        {
                            var start = ++position;
                            while (position < text.Length && text[position] != '|') position++;
                            if (position == text.Length) throw new ArgumentException("Close the connection label with a pipe.");
                            labelSpan = new(statement.Start + start, position - start); label = DecodeLabel(text[start..position]); position++;
                        }
                        var target = ReadNode(text, ref position, statement, scope);
                        localEdges.Add(new("edge-" + (edges.Count + localEdges.Count), local[^1], target, label, type,
                            new(statement.Start + connectionStart, connectionEnd - connectionStart), labelSpan, statement));
                        local.Add(target);
                    }
                    // Chained edges are mapped for inspection, but transforming a shared middle declaration is unsafe.
                    if (localEdges.Count > 1) { error ??= "Chained connections are protected. Split them into separate source statements to edit visually."; protectedRegions.Add(new(statement, error)); }
                    occurrences.AddRange(local); edges.AddRange(localEdges);
                }
                catch (ArgumentException ex)
                { error ??= "Protected syntax: " + ex.Message; protectedRegions.Add(new(statement, ex.Message)); }
                if (occurrences.Count + edges.Count + groups.Count > 2500) throw new ArgumentException("The diagram exceeds the bounded editing scan.");
            }
            if (!header) throw new ArgumentException("Start the diagram with flowchart TD or graph LR.");
            if (stack.Count != 0) throw new ArgumentException("Close every subgraph with end.");
            var nodes = occurrences.GroupBy(item => item.Id).Select(items =>
            {
                var declarations = items.Where(item => item.LabelSpan is not null).ToArray();
                if (declarations.Length > 1) error ??= "Repeated node declarations are protected; keep one declaration and use IDs for references.";
                var first = declarations.FirstOrDefault() ?? items.First();
                return new MermaidNode(items.Key, first.LabelSpan is { } span ? DecodeLabel(span.Read(source)) : items.Key, first.Shape, items.ToArray());
            }).ToArray();
            if (nodes.Length > MaximumNodes || nodes.Length + edges.Count + groups.Count > MaximumElements)
                error ??= "Visual editing supports at most 300 nodes and 500 total nodes, connections and subgraphs.";
            if (edges.Count > Math.Max(100, nodes.Length * 4))
                error ??= "This dense graph exceeds the visual editing budget (more than 100 connections and four connections per node). Keep it in protected source/preview mode.";
            if (styles.Count > MaximumElements) error ??= "The diagram exceeds the 500-statement styling budget for visual editing.";
            if (groups.Any(group => nodes.Any(node => node.Id == group.Id))) error ??= "Connections to subgraph IDs are protected.";
            if (nodes.Any(node => node.Occurrences.Select(item => item.SubgraphId).Where(id => id is not null).Distinct().Count() > 1))
                error ??= "A node belongs to more than one subgraph. Its membership is protected; resolve it in source mode.";
            if (styles.Where(style => style.Kind is "class" or "style").SelectMany(style => style.Targets).Any(target => nodes.All(node => node.Id != target.Id)))
                error ??= "A class or style references an unknown node. Correct it in source mode.";
            var compatibility = error is null ? MermaidCompatibility.Editable : MermaidCompatibility.Protected;
            if (error is null)
            {
                var graph = MermaidRenderer.Parse(source);
                if (graph.Nodes.Count != nodes.Length || graph.Edges.Count != edges.Count
                    || nodes.Any(node => !graph.Nodes.TryGetValue(node.Id, out var parsed) || parsed.Label != RendererLabel(node, source) || parsed.Shape.ToString() != RendererShape(node.Shape))
                    || edges.Where((edge, index) => graph.Edges[index].Source != edge.Source.Id || graph.Edges[index].Target != edge.Target.Id).Any())
                { error = "The source adapter and Mermaid parser disagree. This diagram is protected; use Edit source."; compatibility = MermaidCompatibility.Protected; }
            }
            return new(source, direction, directionSpan, nodes, edges, groups.OrderBy(group => group.Opening.Start).ToArray(), styles, protectedRegions, compatibility, error);
        }
        catch (Exception ex) when (ex is ArgumentException or MermaidParseException)
        { return new(source, direction, directionSpan, [], [], [], [], protectedRegions, MermaidCompatibility.Invalid, ex.Message); }
    }

    private static string RendererLabel(MermaidNode node, string source)
    {
        var raw = NormaliseRawLabelLines(node.Declaration?.LabelSpan?.Read(source) ?? node.Id);
        if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"') raw = raw[1..^1];
        return Regex.Replace(raw, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(50)).Replace("\\n", "\n");
    }
    private static string RendererShape(FlowNodeShape shape) => shape switch
    { FlowNodeShape.Process => "Rectangle", FlowNodeShape.Decision => "Diamond", FlowNodeShape.Terminal => "Stadium", _ => shape.ToString() };
    public static bool IsDirection(string text) => text is "TD" or "TB" or "BT" or "LR" or "RL";
    public static bool IsId(string text) => text.Length is > 0 and <= 128 && (char.IsLetter(text[0]) || text[0] == '_')
        && text.All(character => char.IsLetterOrDigit(character) || character is '_' or '-')
        && text is not ("end" or "subgraph" or "graph" or "flowchart" or "direction" or "class" or "classDef" or "style" or "click" or "default");
    public static void CheckUnicode(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i])) { if (++i >= text.Length || !char.IsLowSurrogate(text[i])) throw new ArgumentException("Correct the incomplete Unicode character in source mode."); }
            else if (char.IsLowSurrogate(text[i]) || text[i] is '\ufffe' or '\uffff' || char.IsControl(text[i]) && text[i] is not ('\r' or '\n' or '\t'))
                throw new ArgumentException("Remove unsupported control or XML text characters in source mode.");
        }
    }
    public static string DecodeLabel(string text)
    {
        text = NormaliseRawLabelLines(text);
        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"') text = text[1..^1];
        text = Regex.Replace(text, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(50));
        text = WebUtility.HtmlDecode(text).Replace("\\n", "\n");
        text = Regex.Replace(text, @"#(\d+);", match => int.TryParse(match.Groups[1].Value, out var value) && System.Text.Rune.IsValid(value)
            ? char.ConvertFromUtf32(value) : match.Value, RegexOptions.None, TimeSpan.FromMilliseconds(50));
        CheckUnicode(text); return text;
    }
    private static string NormaliseRawLabelLines(string text)
    {
        // Mermaider trims physical input lines before joining multiline labels. Match its
        // semantic view without normalising or rewriting a single byte of the saved source.
        var lines = text.Split('\n'); if (lines.Length == 1) return text;
        lines[0] = lines[0].TrimEnd(); lines[^1] = lines[^1].TrimStart();
        for (var index = 1; index < lines.Length - 1; index++) lines[index] = lines[index].Trim();
        return string.Join('\n', lines);
    }
    private static MermaidOccurrence ReadNode(string text, ref int position, MermaidSourceSpan statement, string? scope)
    {
        SkipSpace(text, ref position); var start = position; var id = ReadId(text, ref position);
        if (!IsId(id)) throw new ArgumentException("This node identifier is not supported by the visual editor.");
        var idSpan = new MermaidSourceSpan(statement.Start + start, id.Length); var shape = FlowNodeShape.Process; MermaidSourceSpan? label = null;
        foreach (var syntax in Shapes)
        {
            if (!text.AsSpan(position).StartsWith(syntax.Open, StringComparison.Ordinal)) continue;
            position += syntax.Open.Length; var content = position; var quoted = position < text.Length && text[position] == '"';
            if (quoted)
            {
                position++; while (position < text.Length && text[position] != '"') position++;
                if (position == text.Length) throw new ArgumentException("Close the quoted node label."); position++;
            }
            else while (position < text.Length && !text.AsSpan(position).StartsWith(syntax.Close, StringComparison.Ordinal)) position++;
            if (!text.AsSpan(position).StartsWith(syntax.Close, StringComparison.Ordinal)) throw new ArgumentException("This node shape or label is not supported.");
            if (position - content > MaximumLabelCharacters) throw new ArgumentException("Visual labels support at most 4,096 characters.");
            label = new(statement.Start + content, position - content); shape = syntax.Shape; position += syntax.Close.Length; break;
        }
        string? className = null;
        if (text.AsSpan(position).StartsWith(":::", StringComparison.Ordinal))
        { position += 3; className = ReadId(text, ref position); if (!IsId(className)) throw new ArgumentException("Use a supported class identifier."); }
        return new(id, idSpan, new(statement.Start + start, position - start), label, shape, statement, scope, className);
    }
    private static string ReadId(string text, ref int position)
    {
        var start = position;
        while (position < text.Length && (char.IsLetterOrDigit(text[position]) || text[position] == '_'
            || text[position] == '-' && position + 1 < text.Length && char.IsLetterOrDigit(text[position + 1]))) position++;
        return text[start..position];
    }
    private static FlowConnectionType ReadConnection(string text, ref int position)
    {
        foreach (var (token, type) in new[] { ("<-->", FlowConnectionType.Bidirectional), ("-.->", FlowConnectionType.Dotted),
            ("==>", FlowConnectionType.Thick), ("-->", FlowConnectionType.Directed), ("---", FlowConnectionType.Undirected) })
            if (text.AsSpan(position).StartsWith(token, StringComparison.Ordinal)) { position += token.Length; return type; }
        throw new ArgumentException("This connection syntax is protected. Use separate -->, ---, -.->, ==> or <--> connections with optional |label|.");
    }
    private static void SkipSpace(string text, ref int position) { while (position < text.Length && char.IsWhiteSpace(text[position])) position++; }
    private static bool TryStyle(string text, MermaidSourceSpan statement, List<MermaidStyle> styles)
    {
        var index = 0; var kind = ReadId(text, ref index);
        if (kind is not ("classDef" or "class" or "style")) return false;
        SkipSpace(text, ref index); var targets = new List<(string Id, MermaidSourceSpan Span)>();
        while (true)
        {
            var start = index; var id = ReadId(text, ref index);
            if (!IsId(id)) throw new ArgumentException("A style uses an unsupported identifier.");
            targets.Add((id, new(statement.Start + start, index - start)));
            if (index >= text.Length || text[index] != ',') break; index++;
        }
        SkipSpace(text, ref index); var value = text[index..];
        if (value.Length == 0 || kind == "class" && !IsId(value)) throw new ArgumentException("Complete the class or style in source mode.");
        styles.Add(new(kind, value, targets, statement)); return true;
    }
    private static IEnumerable<MermaidSourceSpan> Statements(string source)
    {
        var start = 0; var quote = false; var pipe = false; var depth = 0;
        for (var i = 0; i <= source.Length; i++)
        {
            if (i < source.Length && !quote && !pipe && depth == 0 && source.AsSpan(i).StartsWith("%%", StringComparison.Ordinal))
            {
                if (i > start && !string.IsNullOrWhiteSpace(source[start..i])) yield return Trim(source, start, i);
                start = i;
                if (source.AsSpan(i).StartsWith("%%{", StringComparison.Ordinal))
                { var end = source.IndexOf("}%%", i + 3, StringComparison.Ordinal); i = end < 0 ? source.Length : end + 3; }
                else { while (i < source.Length && source[i] != '\n') i++; }
                if (i > start) yield return Trim(source, start, i); start = i; if (i == source.Length) break;
            }
            if (i < source.Length)
            {
                var character = source[i];
                if (character == '"') quote = !quote;
                if (!quote && depth == 0 && character == '|') pipe = !pipe;
                if (!quote)
                {
                    if (character is '[' or '(' or '{') depth++;
                    if (character is ']' or ')' or '}') depth--;
                }
                if (quote || pipe || depth != 0 || character is not ('\n' or ';')) continue;
            }
            if (i > start && !string.IsNullOrWhiteSpace(source[start..i])) yield return Trim(source, start, i);
            start = i + 1;
        }
    }
    private static MermaidSourceSpan Trim(string source, int start, int end)
    { while (start < end && char.IsWhiteSpace(source[start])) start++; while (end > start && char.IsWhiteSpace(source[end - 1])) end--; return new(start, end - start); }
    private sealed class GroupDraft(string id, string label, string? parentId, MermaidSourceSpan opening, MermaidSourceSpan heading)
    {
        public string Id { get; } = id; public string Label { get; } = label; public string? ParentId { get; } = parentId;
        public MermaidSourceSpan Opening { get; } = opening; public MermaidSourceSpan Heading { get; } = heading;
        public MermaidSourceSpan? DirectionSpan { get; set; } public string? Direction { get; set; }
    }
}
