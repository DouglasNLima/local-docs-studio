using System.Text.RegularExpressions;
using LensDocsStudio.Core.Imports;

namespace LensDocsStudio.Core.Editing;

public sealed record MarkdownEdit(int Start, int Length, string Replacement, int SelectionStart, int SelectionLength);

public sealed partial class MarkdownEditService
{
    public MarkdownEdit Plan(string source, int start, int length, string action)
    {
        if (start < 0 || length < 0 || (long)start + length > source.Length) throw new ArgumentOutOfRangeException(nameof(start));
        var selected = source.Substring(start, length);
        MarkdownEdit Edit(int offset, int count, string value, int selectionOffset = 0, int? selectionLength = null)
            => new(offset, count, value, offset + selectionOffset, selectionLength ?? value.Length);
        MarkdownEdit Wrap(string marker, string placeholder)
        {
            bool HasStyle(int run) => marker == "*" ? run % 2 == 1 : run >= marker.Length;
            if (selected.Length > 2 * marker.Length && selected.StartsWith(marker, StringComparison.Ordinal) && selected.EndsWith(marker, StringComparison.Ordinal)
                && HasStyle(selected.TakeWhile(c => c == marker[0]).Count()) && HasStyle(selected.Reverse().TakeWhile(c => c == marker[0]).Count()))
                return Edit(start, length, selected[marker.Length..^marker.Length]);
            var beforeRun = 0; var afterRun = 0;
            while (start > beforeRun && source[start - beforeRun - 1] == marker[0]) beforeRun++;
            while (start + length + afterRun < source.Length && source[start + length + afterRun] == marker[0]) afterRun++;
            if (start >= marker.Length && start + length + marker.Length <= source.Length
                && source.AsSpan(start - marker.Length, marker.Length).SequenceEqual(marker)
                && source.AsSpan(start + length, marker.Length).SequenceEqual(marker)
                && HasStyle(beforeRun) && HasStyle(afterRun))
                return Edit(start - marker.Length, length + marker.Length * 2, selected);
            if (length > 0 && string.IsNullOrWhiteSpace(selected)) return Edit(start, length, selected);
            var leading = selected.Length - selected.TrimStart().Length;
            var trailing = selected.Length - selected.TrimEnd().Length;
            var body = length == 0 ? placeholder : selected.Trim();
            return Edit(start, length, selected[..leading] + marker + body + marker + (trailing > 0 ? selected[^trailing..] : ""), leading + marker.Length, body.Length);
        }
        MarkdownEdit Block(string value, string? selectText = null)
            => PlanBlock(source, start, length, value, selectText);
        if (action is "bold" or "italic" or "strike") return Wrap(action == "bold" ? "**" : action == "italic" ? "*" : "~~", action == "strike" ? "deleted text" : action + " text");
        if (action == "inline-code")
        {
            var body = selected.Length == 0 ? "code" : HtmlMarkdownConverter.Normalise(selected).Replace('\n', ' ');
            var fence = new string('`', Backticks().Matches(body).Select(m => m.Length + 1).DefaultIfEmpty(1).Max());
            var pad = body.StartsWith('`') || body.EndsWith('`') || body.StartsWith(' ') && body.EndsWith(' ') && body.Any(c => c != ' ') ? " " : "";
            return Edit(start, length, fence + pad + body + pad + fence, fence.Length + pad.Length, body.Length);
        }
        if (action is "heading" or "task" or "bullet" or "numbered" or "quote")
        {
            var lineStart = start == 0 ? 0 : source.LastIndexOf('\n', start - 1) + 1;
            var last = length > 0 && source[start + length - 1] == '\n' ? start + length - 1 : start + length;
            var lineEnd = source.IndexOf('\n', last); if (lineEnd < 0) lineEnd = source.Length;
            if (lineEnd > lineStart && source[lineEnd - 1] == '\r') lineEnd--;
            var parts = LineSeparators().Split(source[lineStart..lineEnd]);
            var pattern = action switch { "heading" => @"^([ \t]*)##[ \t]+", "task" => @"^([ \t]*)[-*+] \[[ xX]\][ \t]+", "bullet" => @"^([ \t]*)[-*+][ \t]+", "numbered" => @"^([ \t]*)\d+[.)][ \t]+", _ => @"^([ \t]*)>[ \t]?" };
            var matcher = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            var nonEmpty = parts.Where((_, index) => index % 2 == 0).Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
            var remove = nonEmpty.Length > 0 && nonEmpty.All(matcher.IsMatch); var number = 1;
            for (var index = 0; index < parts.Length; index += 2)
            {
                var line = parts[index];
                if (remove) { parts[index] = matcher.Replace(line, "$1", 1); continue; }
                if (string.IsNullOrWhiteSpace(line) && parts.Length > 1) continue;
                var indent = line.Length - line.TrimStart(' ', '\t').Length;
                var body = line[indent..];
                if (action == "heading") body = HeadingPrefix().Replace(body, "", 1);
                if (action is "task" or "bullet" or "numbered")
                {
                    if (action == "task" && matcher.IsMatch(line)) continue;
                    body = ExistingListPrefix().Replace(body, "", 1);
                }
                var marker = action switch { "heading" => "## ", "task" => "- [ ] ", "bullet" => "- ", "numbered" => number++ + ". ", _ => "> " };
                parts[index] = line[..indent] + marker + body;
            }
            var replacement = string.Concat(parts);
            return length == 0 ? Edit(lineStart, lineEnd - lineStart, replacement,
                Math.Clamp(start - lineStart + replacement.Length - (lineEnd - lineStart), 0, replacement.Length), 0)
                : Edit(lineStart, lineEnd - lineStart, replacement);
        }
        if (action.StartsWith("callout:", StringComparison.Ordinal))
        {
            var kind = action[8..];
            if (kind is not ("NOTE" or "TIP" or "IMPORTANT" or "WARNING" or "CAUTION")) throw new ArgumentException("Unknown callout kind.", nameof(action));
            var body = selected.Length == 0 ? "Write the note here." : HtmlMarkdownConverter.Normalise(selected);
            return Block("> [!" + kind + "]\n" + string.Join('\n', body.Split('\n').Select(l => l.Length == 0 ? ">" : "> " + l)), selected.Length == 0 ? body : null);
        }
        return action switch
        {
            "link" => Edit(start, length, "[" + (length == 0 ? "Link text" : selected.Replace("[", "\\[").Replace("]", "\\]")) + "](document.md)"),
            "code" => Block(HtmlMarkdownConverter.Fence(selected, "text")),
            "mermaid" => Block(HtmlMarkdownConverter.Fence(length == 0 ? "flowchart LR\n  A[Draft] --> B[Review]\n  B --> C[Publish]" : selected, "mermaid")),
            "table" => Block((length == 0 ? "" : selected + "\n\n") + "| Column | Description |\n|---|---|\n| Value | Details |", "Value"),
            _ => throw new ArgumentException("Unknown Markdown editing action.", nameof(action))
        };
    }
    public static MarkdownEdit PlanBlock(string source, int start, int length, string value, string? selectText = null)
    {
        if (start < 0 || length < 0 || (long)start + length > source.Length) throw new ArgumentOutOfRangeException(nameof(start));
        var newline = source.IndexOf('\n') is var first && first > 0 && source[first - 1] == '\r' ? "\r\n" : "\n";
        value = HtmlMarkdownConverter.Normalise(value).Replace("\n", newline).Trim('\r', '\n');
        if (selectText is not null) selectText = HtmlMarkdownConverter.Normalise(selectText).Replace("\n", newline);
        var before = source.AsSpan(Math.Max(0, start - 4), Math.Min(4, start));
        var after = source.AsSpan(start + length, Math.Min(4, source.Length - start - length));
        var prefix = before.Length == 0 || before.EndsWith(newline + newline, StringComparison.Ordinal) ? "" : before.EndsWith(newline, StringComparison.Ordinal) ? newline : newline + newline;
        var suffix = after.Length == 0 || after.StartsWith(newline + newline, StringComparison.Ordinal) ? "" : after.StartsWith(newline, StringComparison.Ordinal) ? newline : newline + newline;
        var selectionOffset = selectText is null ? -1 : value.LastIndexOf(selectText, StringComparison.Ordinal);
        return new(start, length, prefix + value + suffix, start + prefix.Length + (selectionOffset < 0 ? value.Length : selectionOffset), selectionOffset < 0 ? 0 : selectText!.Length);
    }
    [GeneratedRegex("`+")] private static partial Regex Backticks();
    [GeneratedRegex("(\\r\\n|\\n|\\r)")] private static partial Regex LineSeparators();
    [GeneratedRegex(@"^#{1,6}[ \t]+")] private static partial Regex HeadingPrefix();
    [GeneratedRegex(@"^(?:[-*+]|\d+[.)])[ \t]+(?:\[[ xX]\][ \t]+)?")] private static partial Regex ExistingListPrefix();
}
