using System.Text;
using System.Text.RegularExpressions;
using LensDocsStudio.Core.SourceAuthoring;
using Mermaider;

namespace LensDocsStudio.Core.Rendering;

/// <summary>Compatibility input for pinned Mermaider 0.15.1. Authoritative source is never changed.</summary>
public static partial class FlowchartCompatibility
{
    public static bool IsFlowchart(string source) => Header().IsMatch(SourceText.Header(source));
    public static string Prepare(string source) => Prepare(source, combineClasses: true);
    // Source-edit validation compares authored declarations, without private generated identities.
    internal static string Prepare(string source, bool combineClasses)
    {
        if (!IsFlowchart(source)) return source;
        if (source.Length > 100_000) throw new NotSupportedException("Flowchart compatibility supports up to 100,000 source characters.");
        var output = new StringBuilder(source.Length); var start = 0; var quote = '\0'; var depth = 0; var escaped = false; var comment = false;
        var assignments = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        for (var index = 0; index <= source.Length; index++)
        {
            if (index < source.Length)
            {
                var character = source[index];
                if (comment) { if (character != '\n') continue; comment = false; }
                else if (escaped) { escaped = false; continue; }
                else if (character == '\\') { escaped = true; continue; }
                else if (quote != '\0') { if (character == quote) quote = '\0'; continue; }
                else if (character == '"' || character == '\'' && IsDirective(source[start..index].Trim())) { quote = character; continue; }
                else if (character == '%' && index + 1 < source.Length && source[index + 1] == '%' && depth == 0)
                {
                    // Comments are opaque, including semicolons and directive-looking text.
                    Write(source[start..index], '\0'); start = index; comment = true; continue;
                }
                else if (character is '[' or '(' or '{') { depth++; continue; }
                else if (character is ']' or ')' or '}') { depth = Math.Max(0, depth - 1); continue; }
                if (depth != 0 || character is not ('\n' or ';')) continue;
                if (character == '\n' && source[start..index].TrimEnd().EndsWith(',') && IsDirective(source[start..index].Trim())) continue;
            }
            Write(source[start..index], index < source.Length ? source[index] : '\0'); start = index + 1;
        }
        // The pinned graph stores one class per node. Only multiple explicit
        // assignments need a renderer-private effective class; named definitions
        // and all authoritative source remain intact. Assignment order wins,
        // matching the independently tested pinned Mermaid.js renderer.
        if (combineClasses && assignments.Values.Any(classes => classes.Distinct().Count() > 1))
        {
            if (assignments.Count > 300 || assignments.Values.Any(classes => classes.Count > 16))
                throw new NotSupportedException("Multiple flowchart classes exceed the bounded 300-target / 16-class compatibility limit.");
            var graph = MermaidRenderer.Parse(output.ToString()); var used = graph.ClassDefs.Keys.ToHashSet(StringComparer.Ordinal); var number = 0;
            foreach (var (target, classes) in assignments.Where(pair => pair.Value.Distinct().Count() > 1))
            {
                var properties = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var name in classes)
                    if (graph.ClassDefs.TryGetValue(name, out var values))
                        foreach (var (key, value) in values) properties[key] = value;
                if (properties.Count == 0) continue;
                string effective; do { effective = "_lens_combined_" + number++; } while (!used.Add(effective));
                output.Append("\nclassDef ").Append(effective).Append(' ').AppendJoin(',', properties.Select(pair => pair.Key + ":" + pair.Value));
                output.Append("\nclass ").Append(target).Append(' ').Append(effective).Append('\n');
                if (output.Length > 100_000) throw new NotSupportedException("Effective flowchart styles exceed the 100,000-character rendering budget.");
            }
        }
        return output.ToString();

        void Write(string raw, char delimiter)
        {
            var text = raw.Trim();
            if (!IsDirective(text)) { output.Append(raw); if (delimiter != '\0') output.Append(delimiter); return; }
            var assignment = Assignment().Match(text);
            var definition = Definition().Match(text);
            var style = Style().Match(text);
            string canonical;
            if (CompoundClass().IsMatch(text))
            {
                // Mermaid.js treats this as one unresolved compound class name.
                // Preserve the established protected-inspection contract and its
                // default appearance, while preventing the pinned phantom node.
                output.Append("\n%% Protected compound class assignment; original source retained.\n");
                return;
            }
            if (assignment.Success)
            {
                var targets = Targets(assignment.Groups[1].Value); var classes = Targets(assignment.Groups[2].Value).Split(',');
                foreach (var target in targets.Split(','))
                {
                    if (!assignments.TryGetValue(target, out var names)) assignments[target] = names = [];
                    names.AddRange(classes);
                }
                canonical = "class " + targets + " " + classes[^1];
            }
            else if (definition.Success) canonical = "classDef " + definition.Groups[1].Value + " " + Properties(definition.Groups[2].Value);
            else if (style.Success) canonical = "style " + Targets(style.Groups[1].Value) + " " + Properties(style.Groups[2].Value);
            else throw new NotSupportedException("Malformed or unsupported flowchart styling directive. Complete class, classDef or style in the original source; no directive was rendered as a node.");
            if (output.Length > 0 && output[^1] is not ('\n' or '\r')) output.Append('\n');
            output.Append(canonical);
            // Only a recognised styling statement's terminator is replaced.
            // Semicolons in labels, escapes, comments and style values stay intact.
            output.Append('\n');
        }
        static string Targets(string value) => string.Join(',', value.Split(',').Select(item => item.Trim()));
        static string Properties(string value)
        {
            if (!PropertyStart().IsMatch(value)) throw new NotSupportedException("Complete the flowchart style properties before rendering.");
            var quote = '\0'; var escaped = false;
            foreach (var character in value)
            {
                if (escaped) { escaped = false; continue; }
                if (character == '\\') { escaped = true; continue; }
                if (quote != '\0')
                {
                    if (character is '\r' or '\n') throw new NotSupportedException("Multiline quoted style values remain source-only; no value was rewritten.");
                    if (character == quote) quote = '\0';
                }
                else if (character is '\'' or '"') quote = character;
            }
            return value.Replace("\r\n", " ", StringComparison.Ordinal).Replace('\n', ' ').TrimEnd();
        }
    }
    public static string? LabelWarning(string source)
    {
        if (!IsFlowchart(source)) return null;
        var messages = new List<string>();
        if (UnsupportedTypography().IsMatch(source)) messages.Add("Native Mermaid retains label text and line breaks, but small, subscript, superscript and highlight typography is not supported. Original source is unchanged; these labels remain protected in Visual Edit.");
        if (CompoundClassWarning().IsMatch(source)) messages.Add("Comma-separated class names remain protected source. Their unresolved style uses the default appearance; use separate class statements to apply multiple classes. Original source is unchanged.");
        return messages.Count == 0 ? null : string.Join(" ", messages);
    }
    [GeneratedRegex(@"^(?:flowchart|graph)\s+(?:TB|TD|BT|LR|RL)(?:\s|;|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 50)] private static partial Regex Header();
    private static bool IsDirective(string source)
    {
        var value = source.AsSpan().TrimStart();
        return Keyword(value, "class") || Keyword(value, "classDef") || Keyword(value, "style");
        static bool Keyword(ReadOnlySpan<char> value, ReadOnlySpan<char> keyword) => value.StartsWith(keyword, StringComparison.Ordinal)
            && (value.Length == keyword.Length || char.IsWhiteSpace(value[keyword.Length]));
    }
    [GeneratedRegex(@"^class\s+([\w-]+(?:\s*,\s*[\w-]+)*)\s+(\w+)$", RegexOptions.CultureInvariant, 50)] private static partial Regex Assignment();
    [GeneratedRegex(@"^class\s+[\w-]+(?:\s*,\s*[\w-]+)*\s+\w+(?:,\w+)+$", RegexOptions.CultureInvariant, 50)] private static partial Regex CompoundClass();
    [GeneratedRegex(@"(?:^|[;\n])\s*class\s+[\w-]+(?:\s*,\s*[\w-]+)*\s+\w+(?:,\w+)+(?:[;\r\n]|$)", RegexOptions.CultureInvariant, 50)] private static partial Regex CompoundClassWarning();
    [GeneratedRegex(@"^classDef\s+(\w+)\s+(.+)$", RegexOptions.Singleline | RegexOptions.CultureInvariant, 50)] private static partial Regex Definition();
    [GeneratedRegex(@"^style\s+([\w-]+(?:\s*,\s*[\w-]+)*)\s+(.+)$", RegexOptions.Singleline | RegexOptions.CultureInvariant, 50)] private static partial Regex Style();
    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z-]*\s*:", RegexOptions.CultureInvariant, 50)] private static partial Regex PropertyStart();
    [GeneratedRegex(@"</?(?:small|sub|sup|mark)\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 50)] private static partial Regex UnsupportedTypography();
}
