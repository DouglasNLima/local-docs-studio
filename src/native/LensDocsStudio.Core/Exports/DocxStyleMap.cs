using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Core.Exports;

/// <summary>Maps the existing template contract to styles of the appropriate Word type.</summary>
internal sealed class DocxStyleMap(W.Styles styles)
{
    private readonly Dictionary<string, string> mapped = new(StringComparer.Ordinal);
    public string Body => Resolve("Normal", W.StyleValues.Paragraph, ["Normal", "BodyText"]);
    public string Code => Resolve("LensCode", W.StyleValues.Paragraph, ["Code", "NoSpacing"]);
    public string Quote => Resolve("Quote", W.StyleValues.Paragraph, ["Quote", "IntenseQuote"]);
    public string Caption => Resolve("Caption", W.StyleValues.Paragraph, ["Caption"]);
    public string Table => Resolve("TableGrid", W.StyleValues.Table, ["TableGrid"]);
    public string Heading(int level) => Resolve("Heading" + Math.Clamp(level, 1, 6), W.StyleValues.Paragraph, ["Heading" + Math.Clamp(level, 1, 6)], Math.Clamp(level, 1, 6));

    private string Resolve(string fallback, W.StyleValues type, string[] candidates, int headingLevel = 0)
    {
        if (mapped.TryGetValue(fallback, out var selected)) return selected;
        var compatible = styles.Elements<W.Style>().Where(style => style.Type?.Value == type && !string.IsNullOrEmpty(style.StyleId?.Value)).ToArray();
        foreach (var candidate in candidates)
        {
            var key = Normalise(candidate);
            var existing = compatible.FirstOrDefault(style => Normalise(style.StyleId?.Value) == key)
                ?? compatible.FirstOrDefault(style => Normalise(style.StyleName?.Val?.Value) == key);
            if (existing?.StyleId?.Value is { } id) return mapped[fallback] = id;
        }
        var baseStyle = fallback is "Normal" or "TableGrid" ? null : Body;
        var newId = fallback; var suffix = 1;
        while (styles.Elements<W.Style>().Any(style => string.Equals(style.StyleId?.Value, newId, StringComparison.Ordinal))) newId = "Lens" + fallback + suffix++;
        var created = new W.Style(new W.StyleName { Val = headingLevel > 0 ? "Heading " + headingLevel : fallback }) { StyleId = newId, Type = type };
        if (baseStyle is not null) created.BasedOn = new W.BasedOn { Val = baseStyle };
        if (type == W.StyleValues.Paragraph)
        {
            created.StyleParagraphProperties = new W.StyleParagraphProperties(new W.SpacingBetweenLines { After = "140" });
            created.StyleRunProperties = new W.StyleRunProperties();
            if (fallback == "Normal")
            {
                created.StyleRunProperties.RunFonts = new W.RunFonts { Ascii = "Segoe UI", HighAnsi = "Segoe UI" };
                created.StyleRunProperties.FontSize = new W.FontSize { Val = "22" };
            }
            if (headingLevel > 0)
            {
                created.StyleParagraphProperties.OutlineLevel = new W.OutlineLevel { Val = headingLevel - 1 };
                created.StyleParagraphProperties.KeepNext = new W.KeepNext();
                created.StyleParagraphProperties.SpacingBetweenLines = new W.SpacingBetweenLines { Before = "240", After = "120" };
                created.StyleRunProperties.Bold = new W.Bold();
                created.StyleRunProperties.FontSize = new W.FontSize { Val = (headingLevel switch { 1 => 44, 2 => 36, 3 => 28, 4 => 24, _ => 22 }).ToString(System.Globalization.CultureInfo.InvariantCulture) };
            }
            else if (fallback == "LensCode")
            {
                created.StyleRunProperties.RunFonts = new W.RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" };
                created.StyleRunProperties.FontSize = new W.FontSize { Val = "19" };
            }
            else if (fallback == "Quote")
            {
                created.StyleParagraphProperties.Indentation = new W.Indentation { Left = "360" };
                created.StyleRunProperties.Italic = new W.Italic();
            }
            else if (fallback == "Caption") created.StyleRunProperties.FontSize = new W.FontSize { Val = "18" };
        }
        styles.Append(created);
        return mapped[fallback] = newId;
    }

    private static string Normalise(string? value) => string.Concat((value ?? "").Where(character => !char.IsWhiteSpace(character) && character is not ('_' or '-'))).ToLowerInvariant();
}
