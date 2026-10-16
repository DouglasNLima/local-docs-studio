using DocumentFormat.OpenXml;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Core.Imports;

internal readonly record struct WordRunFormat(bool Bold = false, bool Italic = false, bool Strike = false,
    bool DoubleStrike = false, W.UnderlineValues? Underline = null);

/// <summary>Resolves passive run formatting without loading resources from document styles.</summary>
internal sealed class WordRunStyles
{
    private readonly Dictionary<string, W.Style> styles;
    private readonly Dictionary<(string Id, W.StyleValues Type), Overlay> resolved = [];
    private readonly HashSet<string> reported = [];
    private readonly List<string> warnings;
    private readonly CancellationToken cancellationToken;
    private readonly WordRunFormat defaults;
    private readonly string? defaultParagraph;
    private readonly record struct Overlay(bool Bold, bool Italic, bool Strike, bool? DoubleStrike, W.UnderlineValues? Underline);

    public WordRunStyles(W.Styles? source, List<string> warnings, CancellationToken cancellationToken)
    {
        this.warnings = warnings; this.cancellationToken = cancellationToken;
        styles = new(StringComparer.Ordinal);
        foreach (var style in source?.Elements<W.Style>() ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (style.StyleId?.Value is { } id) styles.TryAdd(id, style);
        }
        defaultParagraph = styles.Values.FirstOrDefault(style => Kind(style) == W.StyleValues.Paragraph
            && style.Default?.Value == true)?.StyleId?.Value;
        defaults = Direct(new(), source?.DocDefaults?.RunPropertiesDefault?.RunPropertiesBaseStyle);
    }

    public WordRunFormat Read(W.Paragraph paragraph, W.Run run, bool isHeading = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var format = Apply(defaults, Resolve(paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? defaultParagraph, W.StyleValues.Paragraph));
        var characterStyle = Resolve(run.RunProperties?.RunStyle?.Val?.Value, W.StyleValues.Character);
        format = Apply(format, characterStyle);
        format = Direct(format, run.RunProperties);
        // Markdown headings already express their paragraph style's intrinsic bold appearance.
        // Retain independently specified character/direct bold, rather than adding redundant markers.
        if (isHeading && run.RunProperties?.Bold is null && !characterStyle.Bold) format = format with { Bold = false };
        if (format.DoubleStrike) Warn("A Word double strikethrough was simplified to a single strikethrough.");
        if (format.Underline is { } underline && underline != W.UnderlineValues.None && underline != W.UnderlineValues.Single)
            Warn("A Word underline style was simplified to a single underline.");
        return format;
    }

    private Overlay Resolve(string? id, W.StyleValues type)
    {
        if (string.IsNullOrEmpty(id)) return default;
        if (resolved.TryGetValue((id, type), out var cached)) return cached;
        var chain = new List<W.Style>(); var visited = new HashSet<string>(StringComparer.Ordinal);
        for (var current = id; !string.IsNullOrEmpty(current);)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!visited.Add(current))
            {
                var message = type == W.StyleValues.Paragraph
                    ? "A cyclic Word paragraph style was imported as ordinary text."
                    : "A cyclic Word character style was imported without inherited formatting.";
                if (!warnings.Contains(message, StringComparer.Ordinal)) Warn(message);
                resolved.Add((id, type), default); return default;
            }
            if (visited.Count > 64) throw new InvalidDataException("Word run style inheritance exceeds the import limit.");
            if (!styles.TryGetValue(current, out var style) || Kind(style) != type)
            { Warn("A missing or incompatible Word run style was ignored."); break; }
            chain.Add(style); current = style.BasedOn?.Val?.Value;
        }
        var result = default(Overlay);
        for (var index = chain.Count - 1; index >= 0; index--)
        {
            var properties = chain[index].StyleRunProperties;
            // Bold, italic and single strike toggle in styles; explicit false leaves the inherited state.
            // Underline and double strike replace their prior value. Direct formatting is absolute.
            result = new(result.Bold ^ (Value<W.Bold>(properties) == true),
                result.Italic ^ (Value<W.Italic>(properties) == true), result.Strike ^ (Value<W.Strike>(properties) == true),
                Value<W.DoubleStrike>(properties) ?? result.DoubleStrike, Underline(properties) ?? result.Underline);
        }
        resolved.Add((id, type), result); return result;
    }

    private static W.StyleValues Kind(W.Style style) => style.Type?.Value ?? W.StyleValues.Paragraph;
    private static WordRunFormat Apply(WordRunFormat current, Overlay style) => new(current.Bold ^ style.Bold,
        current.Italic ^ style.Italic, current.Strike ^ style.Strike, style.DoubleStrike ?? current.DoubleStrike,
        style.Underline ?? current.Underline);
    private static WordRunFormat Direct(WordRunFormat current, OpenXmlElement? properties) => new(
        Value<W.Bold>(properties) ?? current.Bold, Value<W.Italic>(properties) ?? current.Italic,
        Value<W.Strike>(properties) ?? current.Strike, Value<W.DoubleStrike>(properties) ?? current.DoubleStrike,
        Underline(properties) ?? current.Underline);
    private static bool? Value<T>(OpenXmlElement? properties) where T : W.OnOffType =>
        properties?.GetFirstChild<T>() is { } value ? value.Val?.Value ?? true : null;
    private static W.UnderlineValues? Underline(OpenXmlElement? properties) =>
        properties?.GetFirstChild<W.Underline>() is { } value ? value.Val?.Value ?? W.UnderlineValues.Single : null;
    private void Warn(string message) { if (reported.Add(message)) warnings.Add(message); }
}
