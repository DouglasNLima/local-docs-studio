using System.Text.RegularExpressions;

namespace LensDocsStudio.Core.Editing;

public sealed record MermaidCompletionItem(string Label, string Description, string Insert, params string[] Triggers);
public sealed record MermaidCompletionContext(int Start, int Length, string Prefix, string Indentation, string Newline, IReadOnlyList<MermaidCompletionItem> Items);

public static partial class MermaidCompletionService
{
    // Automatic requests must stay cheap while typing. Explicit completion permits a larger scan.
    public const int AutomaticLimit = 256 * 1024;
    public const int ExplicitLimit = 4 * 1024 * 1024;
    private static readonly MermaidCompletionItem[] Catalogue =
    [
        new("flowchart TD", "Flowchart starter", "flowchart TD\n  A[Start] --> B[Finish]", "flow", "flowchart", "graph"),
        new("sequenceDiagram", "Sequence diagram starter", "sequenceDiagram\n  participant User\n  participant System\n  User->>System: Request\n  System-->>User: Response", "seq", "sequence", "participant"),
        new("participant", "Sequence participant", "participant User", "part", "participant"),
        new("subgraph", "Flowchart grouping", "subgraph Group\n  A[Item]\nend", "sub", "subgraph"),
        new("arrow", "Flowchart arrow", "-->", "arrow", "--", "->"),
        new("note over", "Sequence note", "Note over User,System: Important detail", "note", "over"),
        new("alt / else", "Sequence branch", "alt condition\n  User->>System: Primary path\nelse fallback\n  System-->>User: Alternate path\nend", "alt", "else"),
        new("loop", "Sequence loop", "loop each item\n  User->>System: Repeat action\nend", "loop"),
        new("classDef", "Flowchart style class", "classDef primary fill:#F3F4F6,stroke:#FF883E,color:#111827", "class", "classdef"),
        new("style node", "Style a node", "style A fill:#F3F4F6,stroke:#FF883E,color:#111827", "style"),
        new("stateDiagram-v2", "State diagram starter", "stateDiagram-v2\n  [*] --> Draft\n  Draft --> Review\n  Review --> [*]", "state"),
        new("erDiagram", "Entity relationship starter", "erDiagram\n  USER ||--o{ ORDER : places\n  ORDER ||--|{ LINE_ITEM : contains", "er", "entity"),
        new("gantt task", "Gantt task lines", "section Delivery\n  Design :done, des1, 2026-01-01, 3d\n  Build :active, build1, after des1, 5d", "gantt", "task")
    ];

    public static MermaidCompletionContext? Query(string source, int caret, bool standalone, bool force = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (caret < 0 || caret > source.Length) throw new ArgumentOutOfRangeException(nameof(caret));
        if (source.Length > (force ? ExplicitLimit : AutomaticLimit)) return null;
        var lineStart = caret == 0 ? 0 : source.LastIndexOf('\n', caret - 1) + 1;
        var before = source[lineStart..caret];
        if (before.Contains("%%", StringComparison.Ordinal) || InsideQuote(before) || StandardFence().IsMatch(before) || DevOpsFence().IsMatch(before)) return null;
        if (!standalone && !InsideMermaid(source, lineStart)) return null;
        var prefix = Prefix().Match(before).Value;
        if (!force && prefix.Length < 2) return null;
        var items = Catalogue.Where(item => prefix.Length == 0 || item.Label.Contains(prefix, StringComparison.OrdinalIgnoreCase)
            || item.Triggers.Any(t => t.Contains(prefix, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (items.Length == 0) return null;
        var start = caret - prefix.Length; var end = caret;
        if (prefix.Length > 0)
            while (end < source.Length && (char.IsLetter(prefix[0]) ? char.IsLetterOrDigit(source[end]) || source[end] is '_' or '-' : source[end] is '-' or '.' or '=' or '>')) end++;
        var indentation = new string(before.TakeWhile(c => c is ' ' or '\t').ToArray());
        var firstBreak = source.IndexOf('\n'); var newline = firstBreak > 0 && source[firstBreak - 1] == '\r' ? "\r\n" : "\n";
        return new(start, end - start, prefix, indentation, newline, items);
    }

    public static MarkdownEdit? Plan(string source, int caret, bool standalone, string label)
    {
        var context = Query(source, caret, standalone, true);
        var item = context?.Items.FirstOrDefault(i => i.Label == label);
        if (context is null || item is null) return null;
        var value = item.Insert.Replace("\n", context.Newline + context.Indentation, StringComparison.Ordinal);
        return new(context.Start, context.Length, value, context.Start + value.Length, 0);
    }

    private static bool InsideMermaid(string source, int lineStart)
    {
        char marker = '\0'; var size = 0; var mermaid = false; var devOps = false;
        for (var start = 0; start < lineStart;)
        {
            var end = source.IndexOf('\n', start); if (end < 0) end = source.Length;
            var line = source[start..end].TrimEnd('\r'); start = end + 1;
            var standard = StandardFence().Match(line);
            if (marker != '\0')
            {
                if (standard.Success && standard.Groups[1].Value[0] == marker && standard.Groups[1].Length >= size && string.IsNullOrWhiteSpace(standard.Groups[2].Value))
                { marker = '\0'; mermaid = false; }
                continue;
            }
            var special = DevOpsFence().Match(line);
            if (devOps) { if (special.Success && special.Groups[1].Value.Length == 0) devOps = false; continue; }
            if (standard.Success)
            {
                var fence = standard.Groups[1].Value; var info = standard.Groups[2].Value.Trim();
                if (fence[0] == '`' && info.Contains('`')) continue;
                marker = fence[0]; size = fence.Length;
                var language = info.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                mermaid = string.Equals(language, "mermaid", StringComparison.OrdinalIgnoreCase) || string.Equals(language, "mmd", StringComparison.OrdinalIgnoreCase);
            }
            else if (special.Success && special.Groups[1].Value.Length > 0) devOps = true;
        }
        return devOps || marker != '\0' && mermaid;
    }

    private static bool InsideQuote(string line)
    {
        var quoted = false; var escaped = false;
        foreach (var c in line) { if (escaped) { escaped = false; continue; } if (c == '\\') escaped = true; else if (c == '"') quoted = !quoted; }
        return quoted;
    }
    [GeneratedRegex(@"([A-Za-z][\w-]*|[-.=]+>?)$", RegexOptions.CultureInvariant)] private static partial Regex Prefix();
    [GeneratedRegex(@"^ {0,3}(`{3,}|~{3,})(.*)$", RegexOptions.CultureInvariant)] private static partial Regex StandardFence();
    [GeneratedRegex(@"^[ \t]*:::[ \t]*(mermaid)?[ \t]*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex DevOpsFence();
}
