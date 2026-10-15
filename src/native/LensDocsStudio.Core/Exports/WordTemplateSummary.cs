using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LensDocsStudio.Core.Exports;

public sealed record WordTemplateTextSample(string FontFamily, double Points, bool Bold, bool Italic);

/// <summary>Describes only validated retained formatting; never reads document body content or external fonts.</summary>
public sealed record WordTemplateSummary(int StyleCount, IReadOnlyList<string> Styles, IReadOnlyList<string> HeadingStyles,
    int NumberingCount, string PageSize, string Margins, int Headers, int Footers, int EmbeddedImages,
    WordTemplateTextSample BodySample, WordTemplateTextSample HeadingSample)
{
    internal static WordTemplateSummary Inspect(MainDocumentPart main)
    {
        var styles = main.StyleDefinitionsPart?.Styles?.Elements<W.Style>().ToArray() ?? [];
        var headings = styles.Where(style => style.Type?.Value == W.StyleValues.Paragraph &&
            (style.StyleParagraphProperties?.OutlineLevel is not null || Key(style).StartsWith("heading", StringComparison.OrdinalIgnoreCase))).ToArray();
        var body = styles.FirstOrDefault(style => style.Type?.Value == W.StyleValues.Paragraph && Key(style) is "normal" or "bodytext");
        var section = main.Document?.Body?.Elements<W.SectionProperties>().LastOrDefault();
        var page = section?.GetFirstChild<W.PageSize>(); var margins = section?.GetFirstChild<W.PageMargin>();
        var headers = main.HeaderParts.ToArray(); var footers = main.FooterParts.ToArray();
        return new(styles.Length, styles.Take(80).Select(Name).ToArray(), headings.Take(12).Select(Name).ToArray(),
            main.NumberingDefinitionsPart?.Numbering?.Elements<W.NumberingInstance>().Count() ?? 0,
            page is null ? "Default Word page size" : $"{Cm(page.Width?.Value)} × {Cm(page.Height?.Value)} cm",
            margins is null ? "Default Word margins" : $"Top {Cm(margins.Top?.Value)} · bottom {Cm(margins.Bottom?.Value)} · left {Cm(margins.Left?.Value)} · right {Cm(margins.Right?.Value)} cm",
            headers.Length, footers.Length, headers.SelectMany(part => part.ImageParts).Concat(footers.SelectMany(part => part.ImageParts)).Select(part => part.Uri).Distinct().Count(),
            Sample(body, 11, false), Sample(headings.FirstOrDefault(), 22, true));

        WordTemplateTextSample Sample(W.Style? style, double fallbackSize, bool fallbackBold)
        {
            string? font = null, size = null; bool? bold = null, italic = null;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (style is not null && visited.Count < 32 && visited.Add(style.StyleId?.Value ?? ""))
            {
                var run = style.StyleRunProperties;
                font ??= run?.RunFonts?.Ascii?.Value ?? run?.RunFonts?.HighAnsi?.Value;
                size ??= run?.FontSize?.Val?.Value;
                if (run?.Bold is { } b) bold ??= b.Val?.Value ?? true;
                if (run?.Italic is { } i) italic ??= i.Val?.Value ?? true;
                style = styles.FirstOrDefault(candidate => candidate.StyleId?.Value == style.BasedOn?.Val?.Value);
            }
            // Font names from imported XML cannot become WPF resource URIs or trigger font retrieval.
            string[] installedFamilies = ["Segoe UI", "Arial", "Calibri", "Cambria", "Consolas", "Courier New", "Georgia", "Tahoma", "Times New Roman", "Verdana"];
            var family = installedFamilies.FirstOrDefault(candidate => string.Equals(candidate, font, StringComparison.OrdinalIgnoreCase)) ?? "Segoe UI";
            var points = double.TryParse(size, NumberStyles.None, CultureInfo.InvariantCulture, out var halfPoints) ? Math.Clamp(halfPoints / 2, 6, 36) : fallbackSize;
            return new(family, points, bold ?? fallbackBold, italic ?? false);
        }
        static string Key(W.Style style) => string.Concat((style.StyleName?.Val?.Value ?? style.StyleId?.Value ?? "").Where(char.IsLetterOrDigit)).ToLowerInvariant();
        static string Name(W.Style style) => new((style.StyleName?.Val?.Value ?? style.StyleId?.Value ?? "Unnamed style").Where(character => !char.IsControl(character)).Take(120).ToArray());
        static string Cm(double? twips) => twips is null ? "default" : (twips.Value * 2.54 / 1440).ToString("0.##", CultureInfo.InvariantCulture);
    }
}
