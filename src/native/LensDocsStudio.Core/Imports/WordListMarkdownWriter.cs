using System.Globalization;
using System.Text;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Core.Imports;

// Word stores list items as sibling paragraphs. Keep their numbering and physical
// continuation indents separate from the indentation required by Markdown markers.
internal sealed class WordListMarkdownWriter(W.Numbering? numbering, List<string> warnings)
{
    private sealed record Frame(int NumberId, int Level, long WordIndent, int MarkerIndent, int ContentIndent, string Suffix);
    private readonly List<Frame> frames = [];
    private readonly Dictionary<(int Id, int Level), long> counters = [];
    private readonly Dictionary<(int Id, int Level), (W.Level? Level, long Start)> resolved = [];
    private readonly Dictionary<(int Id, int Level), W.Level?> abstractLevels = [];
    private readonly Dictionary<int, W.NumberingInstance> instances = numbering?.Elements<W.NumberingInstance>()
        .Where(n => n.NumberID?.Value is not null).GroupBy(n => n.NumberID!.Value).ToDictionary(g => g.Key, g => g.First()) ?? [];
    private readonly Dictionary<int, W.AbstractNum> definitions = numbering?.Elements<W.AbstractNum>()
        .Where(n => n.AbstractNumberId?.Value is not null).GroupBy(n => n.AbstractNumberId!.Value).ToDictionary(g => g.Key, g => g.First()) ?? [];
    private readonly HashSet<string> reported = [];
    private StringBuilder output = new();
    private int? previousQuoteIndent;

    public void Paragraph(W.Paragraph paragraph, string text, bool quote = false)
    {
        var properties = paragraph.ParagraphProperties;
        // numId=0 explicitly removes numbering, including inherited numbering.
        if (properties?.NumberingProperties?.NumberingId?.Val?.Value is not > 0)
        {
            Block(text, Indent(properties?.Indentation), quote);
            return;
        }
        var id = properties.NumberingProperties.NumberingId.Val.Value;
        var level = properties.NumberingProperties.NumberingLevelReference?.Val?.Value ?? 0;
        if (level is < 0 or > 8) throw new InvalidDataException("The Word document contains an invalid list level.");
        var (definition, start) = Definition(id, level);
        var bullet = definition?.NumberingFormat?.Val?.Value == W.NumberFormatValues.Bullet;
        if (definition is null) Warn("A missing Word list definition was converted to decimal numbering.");
        else if (!bullet && definition.NumberingFormat?.Val?.Value != W.NumberFormatValues.Decimal)
            Warn("A Word list number format was converted to decimal numbering.");
        var levelText = definition?.LevelText?.Val?.Value;
        var placeholder = "%" + (level + 1).ToString(CultureInfo.InvariantCulture);
        if (!bullet && levelText is not null && levelText != placeholder && levelText != placeholder + "." && levelText != placeholder + ")")
            Warn("A composite or decorated Word list marker was simplified for Markdown.");
        var key = (id, level);
        var value = counters.GetValueOrDefault(key, start);
        if (!bullet && value is < 0 or > 999999999)
            throw new InvalidDataException("Word list numbers must be between 0 and 999999999 for Markdown import.");
        counters[key] = value + 1;
        for (var childLevel = level + 1; childLevel < 9; childLevel++)
        {
            var child = (id, childLevel);
            if (!counters.ContainsKey(child)) continue;
            var restart = Definition(id, childLevel).Level?.LevelRestart?.Val?.Value ?? childLevel;
            if (restart < 0 || restart > childLevel) restart = childLevel;
            if (restart > 0 && restart - 1 == level) counters.Remove(child);
        }

        var wordIndent = Indent(properties.Indentation) ?? Indent(definition?.PreviousParagraphProperties?.Indentation);
        while (frames.Count > 0 && frames[^1].Level > level) frames.RemoveAt(frames.Count - 1);
        var previous = frames.Count > 0 && frames[^1].Level == level ? frames[^1] : null;
        var markerIndent = previous?.MarkerIndent ?? (frames.Count > 0 ? frames[^1].ContentIndent : 0);
        var suffix = bullet ? "-" : ".";
        if (previous is not null)
        {
            // Distinct Word numbering instances must not merge into one Markdown list.
            if (previous.NumberId == id) suffix = previous.Suffix;
            else if (bullet == (previous.Suffix is "-" or "+")) suffix = bullet ? previous.Suffix == "-" ? "+" : "-" : previous.Suffix == "." ? ")" : ".";
            frames.RemoveAt(frames.Count - 1);
        }
        var marker = bullet ? suffix : value.ToString(CultureInfo.InvariantCulture) + suffix;
        var contentIndent = markerIndent + marker.Length + 1;
        frames.Add(new(id, level, wordIndent ?? 0, markerIndent, contentIndent, suffix));
        Append(text, new string(' ', markerIndent) + marker + " ", contentIndent, separate: text.Length != 0, quote: quote);
    }

    public void Block(string text, long? wordIndent, bool quote = false)
    {
        var index = wordIndent is > 0 ? frames.FindLastIndex(f => f.WordIndent == wordIndent) : -1;
        if (index < 0) frames.Clear();
        else if (index < frames.Count - 1) frames.RemoveRange(index + 1, frames.Count - index - 1);
        var indent = index < 0 ? 0 : frames[index].ContentIndent;
        Append(text, new string(' ', indent), indent, separate: true, quote: quote, joinQuote: true);
    }

    // Pipe tables cannot contain Markdown block lists. Keep visible markers, but
    // isolate cell hierarchy so that it cannot become a continuation of a body item.
    public string Cell(IEnumerable<W.Paragraph> paragraphs, Func<W.Paragraph, string> render, Func<W.Paragraph, bool>? isQuote = null)
    {
        var savedFrames = frames.ToArray();
        var savedOutput = output;
        var savedQuote = previousQuoteIndent;
        frames.Clear(); output = new(); previousQuoteIndent = null;
        try
        {
            foreach (var paragraph in paragraphs)
            {
                if (paragraph.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value is > 0)
                    Warn("Word lists inside table cells were flattened to text with visible markers.");
                var quote = isQuote?.Invoke(paragraph) == true;
                if (quote) Warn("Word quotes inside table cells were flattened to text with visible markers.");
                Paragraph(paragraph, render(paragraph), quote);
            }
            return output.ToString().Trim().Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n\n", "\n", StringComparison.Ordinal).Replace("\n", "<br>", StringComparison.Ordinal);
        }
        finally { frames.Clear(); frames.AddRange(savedFrames); output = savedOutput; previousQuoteIndent = savedQuote; }
    }

    public override string ToString() => output.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);

    private (W.Level? Level, long Start) Definition(int id, int level)
    {
        if (resolved.TryGetValue((id, level), out var cached)) return cached;
        instances.TryGetValue(id, out var instance);
        var levelOverride = instance?.Elements<W.LevelOverride>().FirstOrDefault(l => l.LevelIndex?.Value == level);
        var abstractId = instance?.AbstractNumId?.Val?.Value ?? -1;
        if (!abstractLevels.TryGetValue((abstractId, level), out var abstractLevel))
        {
            definitions.TryGetValue(abstractId, out var abstractNumber);
            abstractLevel = abstractNumber?.Elements<W.Level>().FirstOrDefault(l => l.LevelIndex?.Value == level);
            abstractLevels[(abstractId, level)] = abstractLevel;
        }
        var definition = levelOverride?.Level ?? abstractLevel;
        var result = (definition, (long)(levelOverride?.StartOverrideNumberingValue?.Val?.Value ?? definition?.StartNumberingValue?.Val?.Value ?? 1));
        resolved[(id, level)] = result;
        return result;
    }

    private void Append(string text, string firstPrefix, int continuationIndent, bool separate, bool quote = false, bool joinQuote = false)
    {
        if (quote && joinQuote && previousQuoteIndent == continuationIndent && output.Length >= Environment.NewLine.Length * 2)
        {
            // Replace the last blank separator with a quoted blank line, keeping
            // consecutive Word quote paragraphs inside one Markdown quote block.
            output.Length -= Environment.NewLine.Length;
            output.Append(' ', continuationIndent).AppendLine(">");
        }
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        output.Append(firstPrefix).Append(quote ? "> " : "").AppendLine(lines[0]);
        foreach (var line in lines.Skip(1)) output.Append(' ', continuationIndent).Append(quote ? "> " : "").AppendLine(line);
        if (separate) output.AppendLine();
        previousQuoteIndent = quote && separate ? continuationIndent : null;
    }

    private void Warn(string message) { if (reported.Add(message)) warnings.Add(message); }

    internal static long? Indent(W.Indentation? indent)
    {
        if (indent is null) return null;
        var start = indent.GetAttributes().FirstOrDefault(a => a.LocalName == "start" && a.NamespaceUri == "http://schemas.openxmlformats.org/wordprocessingml/2006/main").Value;
        return long.TryParse(string.IsNullOrEmpty(start) ? indent.Left?.Value : start, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    }
}
