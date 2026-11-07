using System.Text.RegularExpressions;

namespace LensDocsStudio.Core.Editing;

public sealed record SlashCommand(string Id, string Label);
public sealed record SlashContext(int Start, int Length, string Query, string Prefix);

/// <summary>Bounded prose-only trigger detection; formatting remains owned by MarkdownEditService.</summary>
public static partial class SlashCommandService
{
    public const int MaximumSourceCharacters = 4 * 1024 * 1024;
    public static IReadOnlyList<SlashCommand> Catalogue { get; } =
    [
        .. Enumerable.Range(1, 6).Select(level => new SlashCommand($"heading:{level}", $"Heading {level}")),
        new("paragraph", "Paragraph"), new("bullet", "Bullet list"), new("numbered", "Numbered list"), new("task", "Checklist"),
        new("quote", "Quote"), new("code", "Code block"), new("mermaid", "Mermaid diagram"), new("table", "Table"),
        new("link", "Link"), new("document-link", "Document link"), new("image", "Image"), new("figure", "Image figure"),
        new("callout:NOTE", "Callout"), new("details", "Collapsible section"), new("rule", "Horizontal rule"),
        new("date-time", "Date/time"), new("snippet", "Built-in snippet")
    ];

    public static SlashContext? Query(string source, int caret, SlashContext? previous = null)
    {
        if (source.Length > MaximumSourceCharacters || caret < 0 || caret > source.Length) return null;
        var lineStart = caret == 0 ? 0 : source.LastIndexOf('\n', caret - 1) + 1;
        var lineEnd = source.IndexOf('\n', caret); if (lineEnd < 0) lineEnd = source.Length;
        if (source.AsSpan(caret, lineEnd - caret).TrimEnd('\r').Length != 0) return null;
        var match = Trigger().Match(source[lineStart..caret]); if (!match.Success) return null;
        var start = lineStart + match.Value.IndexOf('/');
        if (previous is not null && source.AsSpan(0, lineStart).SequenceEqual(previous.Prefix))
            return new(start, caret - start, match.Groups[1].Value, previous.Prefix);
        var prefix = source[..lineStart];
        char marker = '\0'; var width = 0; var frontMatter = false; var comment = false; string? rawTag = null;
        var rows = prefix.Split('\n');
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index].TrimEnd('\r');
            if (index == 0 && row == "---") { frontMatter = true; continue; }
            if (frontMatter) { if (row is "---" or "...") frontMatter = false; continue; }
            if (rawTag is not null) { if (row.Contains($"</{rawTag}>", StringComparison.OrdinalIgnoreCase)) rawTag = null; continue; }
            if (comment) { if (row.Contains("-->", StringComparison.Ordinal)) comment = false; continue; }
            var fence = Fence().Match(row);
            if (fence.Success)
            {
                if (marker == '\0') { marker = fence.Groups[1].Value[0]; width = fence.Groups[1].Length; }
                else if (fence.Groups[1].Value[0] == marker && fence.Groups[1].Length >= width && string.IsNullOrWhiteSpace(fence.Groups[2].Value)) marker = '\0';
                continue;
            }
            if (marker != '\0') continue;
            if (row.TrimStart().StartsWith("<!--", StringComparison.Ordinal)) comment = true;
            if (row.Contains("-->", StringComparison.Ordinal)) comment = false;
            var tag = RawTag().Match(row);
            if (tag.Success && !row.Contains($"</{tag.Groups[1].Value}>", StringComparison.OrdinalIgnoreCase)) rawTag = tag.Groups[1].Value;
        }
        return marker != '\0' || frontMatter || comment || rawTag is not null ? null : new(start, caret - start, match.Groups[1].Value, prefix);
    }

    public static MarkdownEdit? Plan(string source, int caret, string command)
    {
        var context = Query(source, caret);
        if (context is null || !Catalogue.Any(item => item.Id == command) || command is "document-link" or "image" or "snippet") return null;
        var virtualSource = source.Remove(context.Start, context.Length);
        var edit = new MarkdownEditService().Plan(virtualSource, context.Start, 0, command);
        var lineStart = context.Start == 0 ? 0 : source.LastIndexOf('\n', context.Start - 1) + 1;
        var indentation = source[lineStart..context.Start];
        if (indentation.Length > 0 && edit.Start == context.Start)
        {
            var newline = source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var removed = edit.Replacement.Length - edit.Replacement.TrimStart('\r', '\n').Length;
            string Adjust(string text) => text[Math.Min(removed, text.Length)..].Replace(newline, newline + indentation, StringComparison.Ordinal);
            var selectionStart = Adjust(edit.Replacement[..Math.Clamp(edit.SelectionStart - edit.Start, 0, edit.Replacement.Length)]).Length;
            var selectionEnd = Adjust(edit.Replacement[..Math.Clamp(edit.SelectionStart + edit.SelectionLength - edit.Start, 0, edit.Replacement.Length)]).Length;
            edit = edit with { Replacement = Adjust(edit.Replacement), SelectionStart = edit.Start + selectionStart, SelectionLength = selectionEnd - selectionStart };
        }
        return edit with { Length = edit.Length + context.Length };
    }
    [GeneratedRegex(@"^ {0,3}/([a-z0-9 -]{0,64})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)] private static partial Regex Trigger();
    [GeneratedRegex(@"^ {0,3}(`{3,}|~{3,}|:::)(.*)$", RegexOptions.CultureInvariant, 100)] private static partial Regex Fence();
    [GeneratedRegex(@"^\s*<(pre|code|script|style|textarea)(?:\s|>)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)] private static partial Regex RawTag();
}
