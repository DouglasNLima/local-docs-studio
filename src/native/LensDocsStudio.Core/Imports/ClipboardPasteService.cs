using System.Text;
using System.Text.RegularExpressions;
using LensDocsStudio.Core.Exports;

namespace LensDocsStudio.Core.Imports;

public enum PasteMode { Auto, Text, Table, Code, Quote, Html, List, Checklist, NumberedList, Mermaid }
public sealed record ClipboardPayload(string Text, string Html);
public sealed record PasteResult(ImportedDocument Document, bool Block, string Status);

public sealed partial class ClipboardPasteService
{
    public PasteResult Convert(ClipboardPayload payload, PasteMode mode, CancellationToken cancellationToken = default)
    {
        if (payload.Text.Length > 4 * 1024 * 1024 || payload.Html.Length > HtmlClipboardFormat.MaximumBytes)
            throw new IOException("Paste is limited to 4 million text characters and 32 MB of HTML. Import larger documents from a file.");
        cancellationToken.ThrowIfCancellationRequested();
        var text = HtmlMarkdownConverter.Normalise(payload.Text);
        var converter = new HtmlMarkdownConverter();
        string Html() => HtmlClipboardFormat.Decode(payload.Html);
        string Plain() => text.Length > 0 ? text : converter.PlainText(Html(), cancellationToken);
        PasteResult Result(string source, bool block, string status) => new(new(source, [], []), block, status);
        PasteResult Rich(string html)
        {
            var result = converter.Convert(html, cancellationToken);
            return new(result, true, "HTML pasted as Markdown.");
        }
        switch (mode)
        {
            case PasteMode.Auto:
                if (MarkdownLike().IsMatch(text)) return Result(text, false, "Markdown pasted.");
                if (!string.IsNullOrWhiteSpace(payload.Html)) return Rich(Html());
                if (text.Contains('\t') && TryTable(text, '\t', out var table)) return Result(table, true, "Table pasted as Markdown.");
                return Result(text, false, "Text pasted.");
            case PasteMode.Text: return Result(Plain(), false, "Plain text pasted.");
            case PasteMode.Html:
                if (!string.IsNullOrWhiteSpace(payload.Html)) return Rich(Html());
                if (text.TrimStart().StartsWith('<')) return Rich(text);
                throw new IOException("The clipboard does not contain HTML. Choose another paste format.");
            case PasteMode.Code: return Result(HtmlMarkdownConverter.Fence(Plain()), true, "Code block pasted.");
            case PasteMode.Mermaid: return Result(HtmlMarkdownConverter.Fence(MermaidSource(Plain()), "mermaid"), true, "Mermaid block pasted.");
            case PasteMode.Quote: return Result(string.Join('\n', Plain().TrimEnd('\n').Split('\n').Select(l => "> " + l)), true, "Quote pasted.");
            case PasteMode.List: case PasteMode.Checklist: case PasteMode.NumberedList:
                var lines = Plain().Split('\n').Select(l => ListPrefix().Replace(l.Trim(), "")).Where(l => l.Length > 0).ToArray();
                return Result(string.Join('\n', lines.Select((line, i) => (mode == PasteMode.NumberedList ? (i + 1) + ". " : mode == PasteMode.Checklist ? "- [ ] " : "- ") + line)), true,
                    mode == PasteMode.Checklist ? "Checklist pasted." : mode == PasteMode.NumberedList ? "Numbered list pasted." : "List pasted.");
            case PasteMode.Table:
                if (!string.IsNullOrWhiteSpace(payload.Html))
                {
                    var html = Html();
                    if (HtmlTable().IsMatch(html)) return Rich(html);
                }
                if (TryTable(text, text.Contains('\t') ? '\t' : ',', out var result)) return Result(result, true, "Table pasted as Markdown.");
                throw new IOException("The clipboard does not contain a table with at least two rows and two columns.");
            default: throw new ArgumentOutOfRangeException(nameof(mode));
        }
    }

    private static bool TryTable(string source, char delimiter, out string markdown)
    {
        markdown = "";
        var rows = new List<string[]>(); var row = new List<string>(); var cell = new StringBuilder();
        var quoted = false; var closed = false; var cells = 0;
        void Cell()
        {
            if (++cells > 100000 || row.Count >= 256) throw new IOException("The pasted table exceeds its cell or column limit.");
            row.Add(HtmlMarkdownConverter.Escape(cell.ToString()).Replace("\n", "<br>")); cell.Clear(); closed = false;
        }
        void Row()
        {
            Cell(); rows.Add(row.ToArray()); row.Clear();
            if (rows.Count > 10000) throw new IOException("The pasted table exceeds 10,000 rows.");
        }
        source = source.TrimEnd('\n');
        for (var i = 0; i < source.Length; i++)
        {
            var ch = source[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < source.Length && source[i + 1] == '"') { cell.Append('"'); i++; }
                else if (ch == '"') { quoted = false; closed = true; }
                else cell.Append(ch);
            }
            else if (ch == delimiter) Cell();
            else if (ch == '\n') Row();
            else if (closed) throw new IOException("A quoted table cell has unexpected trailing text.");
            else if (ch == '"' && cell.Length == 0) quoted = true;
            else cell.Append(ch);
        }
        if (quoted) throw new IOException("A quoted table cell is unfinished. Paste as text to retain it literally.");
        Row();
        var width = rows.Max(r => r.Length);
        if (rows.Count < 2 || width < 2) return false;
        if ((long)width * rows.Count > 100000) throw new IOException("The expanded table exceeds 100,000 cells.");
        string Format(string[] values) => "| " + string.Join(" | ", Enumerable.Range(0, width).Select(i => i < values.Length ? values[i] : "")) + " |";
        markdown = Format(rows[0]) + "\n" + Format(Enumerable.Repeat("---", width).ToArray()) + "\n" + string.Join('\n', rows.Skip(1).Select(Format));
        return true;
    }
    private static string MermaidSource(string source)
    {
        source = source.Trim();
        var devops = DevOpsMermaid().Match(source);
        if (devops.Success) return devops.Groups[1].Value.Trim();
        var lines = source.Split('\n');
        if (lines.Length >= 3)
        {
            var open = MermaidFence().Match(lines[0]);
            if (open.Success && lines[^1].Trim() == open.Groups[1].Value) return string.Join('\n', lines[1..^1]);
        }
        return source;
    }
    [GeneratedRegex(@"(?m)^(?:`{3,}|~{3,}|::: *mermaid|#{1,6}\s|[-*+]\s|\d+[.)]\s|\|.+\|\s*$)|!?\[[^\]\n]*\]\([^\n]+\)", RegexOptions.IgnoreCase)] private static partial Regex MarkdownLike();
    [GeneratedRegex(@"^(?:(?:[-*+]|\d+[.)])\s+)?(?:\[[ xX]\]\s+)?")] private static partial Regex ListPrefix();
    [GeneratedRegex(@"<table(?:\s|>)", RegexOptions.IgnoreCase)] private static partial Regex HtmlTable();
    [GeneratedRegex(@"^:::\s*mermaid\s*\n([\s\S]*?)\n:::$", RegexOptions.IgnoreCase)] private static partial Regex DevOpsMermaid();
    [GeneratedRegex(@"^(`{3,}|~{3,})(?:mermaid|mmd)?\s*$", RegexOptions.IgnoreCase)] private static partial Regex MermaidFence();
}
