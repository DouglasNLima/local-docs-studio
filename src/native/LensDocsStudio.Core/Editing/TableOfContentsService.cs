using System.Text.RegularExpressions;
using LensDocsStudio.Core.Parsing;

namespace LensDocsStudio.Core.Editing;

public enum TableOfContentsStyle { Bullets, Numbered, Plain }
public sealed record TableOfContentsOptions(int StartLevel = 1, int EndLevel = 6,
    TableOfContentsStyle Style = TableOfContentsStyle.Bullets, bool Normalise = true, bool Details = false);

/// <summary>Formats existing parser targets; never re-slugs headings or edits Markdown.</summary>
public static partial class TableOfContentsService
{
    public static string Create(IReadOnlyList<DocumentHeading> headings, TableOfContentsOptions? options = null)
    {
        options ??= new();
        if (options.StartLevel < 1 || options.EndLevel > 6 || options.StartLevel > options.EndLevel || !Enum.IsDefined(options.Style))
            throw new ArgumentException("Choose heading levels from 1 to 6, with the start no greater than the end, and a supported style.");
        var levels = new Stack<int>(); var rows = new List<string>();
        foreach (var heading in headings.Where(h => h.Level >= options.StartLevel && h.Level <= options.EndLevel))
        {
            while (levels.TryPeek(out var level) && level >= heading.Level) levels.Pop();
            var depth = options.Normalise ? levels.Count : heading.Level - 1;
            levels.Push(heading.Level);
            var label = Whitespace().Replace(heading.Title, " ").Trim();
            var escaped = Escape().Replace(label, @"\$0");
            var entry = options.Style == TableOfContentsStyle.Plain ? label
                : $"{(options.Style == TableOfContentsStyle.Numbered ? "1." : "-")} [{escaped}](#{Uri.EscapeDataString(heading.Anchor)})";
            rows.Add(new string(' ', depth * (options.Style == TableOfContentsStyle.Numbered ? 3 : 2)) + entry);
        }
        if (rows.Count == 0) return "";
        var body = string.Join('\n', rows);
        return options.Details ? $"<details>\n<summary>Table of Contents</summary>\n\n{body}\n\n</details>" : body;
    }
    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, 100)] private static partial Regex Whitespace();
    [GeneratedRegex(@"[\\\[\]`*_<>]", RegexOptions.CultureInvariant, 100)] private static partial Regex Escape();
}
