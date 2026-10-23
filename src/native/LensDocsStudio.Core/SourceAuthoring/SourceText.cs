using System.Text.RegularExpressions;
using LensDocsStudio.Core.Mermaid;

namespace LensDocsStudio.Core.SourceAuthoring;

/// <summary>Lossless source lines, including their original terminators. Keys belong to occurrences, never labels.</summary>
public sealed record SourceLine(string Key, string Text)
{
    // Token keys travel with list occurrences when a family edits an inline vector.
    // Empty initial maps derive keys from original slot positions, never token values.
    public IReadOnlyList<string> OccurrenceKeys { get; init; } = [];
    public string Occurrence(int index) => index < OccurrenceKeys.Count ? OccurrenceKeys[index] : Key + ":" + index;
    public string Body => Text.TrimEnd('\r', '\n');
    public string Ending => Text[Body.Length..];
    public string Indent => Body[..(Body.Length - Body.TrimStart(' ', '\t').Length)];
    public string Content => Body.Trim();
    public bool Comment => Content.StartsWith("%%", StringComparison.Ordinal) && !Content.StartsWith("%%{", StringComparison.Ordinal);
    public bool InitDirective => Content.StartsWith("%%{", StringComparison.Ordinal) && Content.EndsWith("}%%", StringComparison.Ordinal);
    public SourceLine Replace(int start, int length, string value) => this with { Text = Text[..start] + value + Text[(start + length)..] };
}

public sealed record SourceText(IReadOnlyList<SourceLine> Lines)
{
    public string Source => string.Concat(Lines.Select(line => line.Text));
    public string NewLine => Lines.Select(line => line.Ending).FirstOrDefault(value => value.Length > 0) ?? "\n";
    public static SourceText Read(string source, int lineLimit = 1500)
    {
        if (source.Length > 100_000) throw new ArgumentException("Visual source exceeds the 100,000-character rendering budget. Use Edit source.");
        var lines = new List<SourceLine>();
        for (var start = 0; start < source.Length;)
        {
            var end = source.IndexOf('\n', start); var next = end < 0 ? source.Length : end + 1;
            lines.Add(new(Guid.NewGuid().ToString("N"), source[start..next])); start = next;
        }
        if (lines.Count > lineLimit) throw new ArgumentException("Source exceeds the " + lineLimit.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + "-line source budget. Use Edit source.");
        return new(lines);
    }
    public int Index(string key) => Lines.ToList().FindIndex(line => line.Key == key) is var index && index >= 0 ? index : throw new ArgumentException("Select an existing source occurrence.");
    public SourceLine Line(string key) => Lines[Index(key)];
    public SourceText Replace(SourceLine line) => new(Lines.Select(item => item.Key == line.Key ? line : item).ToArray());
    public SourceText Patch(string key, Group token, string value) => Replace(Line(key).Replace(token.Index, token.Length, value));
    public SourceText Insert(int index, string text)
    {
        var lines = Lines.ToList();
        // Insertion after an unterminated final line owns only the necessary separator.
        if (index > 0 && lines[index - 1].Ending.Length == 0) lines[index - 1] = lines[index - 1] with { Text = lines[index - 1].Text + NewLine };
        lines.Insert(index, new(Guid.NewGuid().ToString("N"), text + NewLine)); return new(lines);
    }
    public SourceText Splice(int start, int count, IEnumerable<SourceLine> replacement)
    { var lines = Lines.ToList(); lines.RemoveRange(start, count); lines.InsertRange(start, replacement); return new(lines); }
    public MermaidSourceSpan Span(string key)
    { var index = Index(key); return new(Lines.Take(index).Sum(line => line.Text.Length), Lines[index].Body.Length); }
    /// <summary>Immediately preceding comments of exactly the same indentation attach to the following row.
    /// Blank lines and other indentation end attachment. No trailing comment is guessed to belong to a row.</summary>
    public int AttachedStart(string key)
    {
        var index = Index(key); var indent = Lines[index].Indent;
        while (index > 0 && Lines[index - 1].Comment && Lines[index - 1].Indent == indent) index--;
        return index;
    }
    public static Regex Pattern(string pattern, RegexOptions options = RegexOptions.None) => new(pattern, options | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    public static string Header(string source) => source.Split('\n').Select(line => line.Trim()).FirstOrDefault(line => line.Length > 0 && !line.StartsWith("%%", StringComparison.Ordinal)) ?? "";
    public static string PrepareRendererSource(string source)
    {
        // The renderer counts non-blank/non-comment lines. Its raw trivia must not acquire the editor's line limit.
        if (source.Length <= 100_000 && (TopologySource.IsTopology(source) || ExpansionViSource.IsFamily(source))) return ChartSource.Prepare(source, int.MaxValue);
        if (source.Length <= 100_000 && (ChartSource.IsChart(source) || WorkflowSource.IsWorkflow(source))) return ChartSource.Prepare(source);
        if (!Pattern(@"^(?:pie|gantt|timeline|journey|quadrantChart)(?:\s|$)|^mindmap$", RegexOptions.IgnoreCase).IsMatch(Header(source))) return source;
        // The pinned detector examines the first raw line, although its parsers skip comments.
        // Adapt only leading inert trivia for these families; DiagramResult retains authoritative source.
        var offset = 0;
        foreach (var line in source.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0 && (!trimmed.StartsWith("%%", StringComparison.Ordinal) || trimmed.StartsWith("%%{", StringComparison.Ordinal))) break;
            offset += line.Length + (offset + line.Length < source.Length ? 1 : 0);
        }
        return source[offset..];
    }
    public static void Label(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim() || value.Length > 2000 || value.Any(char.IsControl))
            throw new ArgumentException("Enter a non-empty label of at most 2,000 characters without leading/trailing spaces or line breaks.");
    }
}

public sealed record SourceAuthoringResult(bool Success, string? Error);
