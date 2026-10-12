using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Core.Imports;

internal sealed record WordParagraphFormat(int HeadingLevel = 0, bool IsCode = false, bool IsQuote = false);

internal sealed class WordParagraphStyles
{
    private readonly Dictionary<string, W.Style> styles;
    private readonly Dictionary<string, WordParagraphFormat> resolved = new(StringComparer.Ordinal);
    private readonly List<string> warnings;
    private readonly string? defaultId;
    private bool cycleReported;

    public WordParagraphStyles(W.Styles? source, List<string> warnings)
    {
        this.warnings = warnings;
        styles = (source?.Elements<W.Style>() ?? []).Where(s => s.StyleId?.Value is not null
            && (s.Type?.Value is null || s.Type.Value == W.StyleValues.Paragraph))
            .GroupBy(s => s.StyleId!.Value!, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        defaultId = styles.Values.FirstOrDefault(s => s.Default?.Value == true)?.StyleId?.Value;
    }

    public WordParagraphFormat Read(W.Paragraph paragraph)
    {
        var format = Resolve(paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? defaultId);
        return paragraph.ParagraphProperties?.OutlineLevel?.Val?.Value switch
        {
            >= 0 and <= 5 and var level => new(level + 1),
            9 => format with { HeadingLevel = 0 },
            _ => format
        };
    }

    private WordParagraphFormat Resolve(string? id) => Resolve(id, new HashSet<string>(StringComparer.Ordinal));

    private WordParagraphFormat Resolve(string? id, HashSet<string> visited)
    {
        if (string.IsNullOrEmpty(id)) return new();
        if (resolved.TryGetValue(id, out var cached)) return cached;
        if (!visited.Add(id))
        {
            if (!cycleReported) { warnings.Add("A cyclic Word paragraph style was imported as ordinary text."); cycleReported = true; }
            return new();
        }
        if (visited.Count > 64) throw new InvalidDataException("Word paragraph style inheritance exceeds the import limit.");
        styles.TryGetValue(id, out var style);
        var outline = style?.StyleParagraphProperties?.OutlineLevel?.Val?.Value;
        var format = outline is >= 0 and <= 5 ? new WordParagraphFormat(outline.Value + 1)
            : Named(style?.StyleName?.Val?.Value) ?? Named(id) ?? Resolve(style?.BasedOn?.Val?.Value, visited);
        if (outline == 9) format = format with { HeadingLevel = 0 };
        visited.Remove(id); resolved[id] = format;
        return format;
    }

    private static WordParagraphFormat? Named(string? value)
    {
        var name = string.Concat((value ?? "").Where(c => !char.IsWhiteSpace(c) && c is not ('_' or '-'))).ToLowerInvariant();
        if (name == "title") return new(1);
        if (name.Length == 8 && name.StartsWith("heading", StringComparison.Ordinal) && name[7] is >= '1' and <= '6') return new(name[7] - '0');
        return name switch { "lenscode" or "code" => new(IsCode: true), "quote" or "intensequote" => new(IsQuote: true), _ => null };
    }
}
