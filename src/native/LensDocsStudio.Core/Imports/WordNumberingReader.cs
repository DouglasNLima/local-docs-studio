using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Core.Imports;

internal sealed record WordListFormat(int? NumberId, int Level, long? WordIndent);

internal sealed class WordNumberingReader
{
    private sealed record ResolvedLevel(W.Level? Level, long Start, string? ParagraphStyle);
    private readonly Dictionary<int, W.NumberingInstance> instances;
    private readonly Dictionary<int, W.AbstractNum> definitions;
    private readonly Dictionary<string, W.Style> styles;
    private readonly Dictionary<(int Id, int Level), ResolvedLevel> resolved = [];
    private readonly Dictionary<(int Id, int Level), ResolvedLevel> abstractLevels = [];
    private readonly Dictionary<(string Id, bool Numbering), W.Style[]> chains = [];
    private readonly Dictionary<(int Id, string Style), int?> associations = [];
    private readonly List<string> warnings;
    private readonly string? defaultId;

    public WordNumberingReader(W.Numbering? numbering, W.Styles? sourceStyles, List<string> warnings)
    {
        this.warnings = warnings;
        instances = (numbering?.Elements<W.NumberingInstance>() ?? []).Where(n => n.NumberID?.Value is not null)
            .GroupBy(n => n.NumberID!.Value).ToDictionary(g => g.Key, g => g.First());
        definitions = (numbering?.Elements<W.AbstractNum>() ?? []).Where(n => n.AbstractNumberId?.Value is not null)
            .GroupBy(n => n.AbstractNumberId!.Value).ToDictionary(g => g.Key, g => g.First());
        styles = (sourceStyles?.Elements<W.Style>() ?? []).Where(s => s.StyleId?.Value is not null)
            .GroupBy(s => s.StyleId!.Value!, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        defaultId = styles.Values.FirstOrDefault(s => s.Default?.Value == true && s.Type?.Value == W.StyleValues.Paragraph)?.StyleId?.Value;
    }

    public WordListFormat Read(W.Paragraph paragraph)
    {
        var properties = paragraph.ParagraphProperties;
        var chain = Chain(properties?.ParagraphStyleId?.Val?.Value ?? defaultId);
        var inheritedId = chain.Select(s => s.StyleParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value).FirstOrDefault(id => id.HasValue);
        var direct = properties?.NumberingProperties;
        var numberId = direct?.NumberingId?.Val?.Value ?? inheritedId;
        if (numberId < 0) throw new InvalidDataException("The Word document contains an invalid numbering identifier.");
        var indent = WordListMarkdownWriter.Indent(properties?.Indentation)
            ?? chain.Select(s => WordListMarkdownWriter.Indent(s.StyleParagraphProperties?.Indentation)).FirstOrDefault(value => value.HasValue);
        if (numberId is not > 0) return new(numberId, 0, indent);
        var level = direct?.NumberingLevelReference?.Val?.Value;
        if (!level.HasValue)
        {
            // A style's ilvl is ignored. Its pStyle association in the numbering
            // definition selects the level, with the nearest derived style first.
            foreach (var style in chain)
            {
                var key = (numberId.Value, style.StyleId!.Value!);
                if (!associations.TryGetValue(key, out var associated))
                {
                    for (var candidate = 0; candidate < 9; candidate++)
                        if (ResolveLevel(numberId.Value, candidate, []).ParagraphStyle == key.Item2) { associated = candidate; break; }
                    associations[key] = associated;
                }
                if (associated.HasValue) { level = associated; break; }
            }
            if (!level.HasValue && direct?.NumberingId is null && inheritedId is > 0)
                Warn("A Word list style had no level association; level zero was used.");
        }
        return new(numberId, level ?? 0, indent);
    }

    public (W.Level? Level, long Start) Definition(int id, int level)
    {
        var result = ResolveLevel(id, level, []);
        return (result.Level, result.Start);
    }

    private ResolvedLevel ResolveLevel(int id, int level, HashSet<int> visited)
    {
        if (resolved.TryGetValue((id, level), out var cached)) return cached;
        if (!visited.Add(id)) throw new InvalidDataException("The Word document contains cyclic numbering style links.");
        if (visited.Count > 64) throw new InvalidDataException("Word numbering style links exceed the import limit.");
        instances.TryGetValue(id, out var instance);
        var abstractId = instance?.AbstractNumId?.Val?.Value ?? -1;
        if (!abstractLevels.TryGetValue((abstractId, level), out var inherited))
        {
            definitions.TryGetValue(abstractId, out var abstractNumber);
            if (abstractNumber?.NumberingStyleLink?.Val?.Value is { } link)
            {
                var linkedId = Chain(link, numbering: true).Select(s => s.StyleParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value)
                    .FirstOrDefault(value => value.HasValue);
                if (linkedId is > 0) inherited = ResolveLevel(linkedId.Value, level, visited);
                else { Warn("A Word numbering style link had no valid numbering target."); inherited = new(null, 1, null); }
            }
            else
            {
                var definition = abstractNumber?.Elements<W.Level>().FirstOrDefault(l => l.LevelIndex?.Value == level);
                inherited = new(definition, definition?.StartNumberingValue?.Val?.Value ?? 1, definition?.ParagraphStyleIdInLevel?.Val?.Value);
            }
            abstractLevels[(abstractId, level)] = inherited;
        }
        var levelOverride = instance?.Elements<W.LevelOverride>().FirstOrDefault(l => l.LevelIndex?.Value == level);
        var result = new ResolvedLevel(levelOverride?.Level ?? inherited.Level,
            (long)(levelOverride?.StartOverrideNumberingValue?.Val?.Value ?? (levelOverride?.Level is { } replacement
                ? replacement.StartNumberingValue?.Val?.Value ?? 1 : inherited.Start)),
            levelOverride?.Level?.ParagraphStyleIdInLevel?.Val?.Value ?? inherited.ParagraphStyle);
        visited.Remove(id); resolved[(id, level)] = result;
        return result;
    }

    private W.Style[] Chain(string? id, bool numbering = false)
    {
        if (string.IsNullOrEmpty(id)) return [];
        if (chains.TryGetValue((id, numbering), out var cached)) return cached;
        var result = new List<W.Style>(); var visited = new HashSet<string>(StringComparer.Ordinal); var current = id;
        while (!string.IsNullOrEmpty(current))
        {
            if (!visited.Add(current))
            {
                if (numbering) throw new InvalidDataException("The Word document contains cyclic numbering style inheritance.");
                Warn("A cyclic Word paragraph style was imported as ordinary text.");
                return chains[(id, numbering)] = [];
            }
            if (visited.Count > 64) throw new InvalidDataException("Word paragraph style inheritance exceeds the import limit.");
            if (!styles.TryGetValue(current, out var style)) break;
            if (numbering ? style.Type?.Value != W.StyleValues.Numbering : style.Type?.Value is { } type && type != W.StyleValues.Paragraph) break;
            result.Add(style); current = style.BasedOn?.Val?.Value;
        }
        return chains[(id, numbering)] = result.ToArray();
    }

    private void Warn(string message) { if (!warnings.Contains(message, StringComparer.Ordinal)) warnings.Add(message); }
}
