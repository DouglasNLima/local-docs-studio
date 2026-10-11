using System.Globalization;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Markdig.Syntax;

namespace LensDocsStudio.Core.Parsing;

public sealed class DocumentProgressBlock(string label, double value, double maximum, string colour) : LeafBlock(null)
{
    public string Label { get; } = label;
    public double Value { get; } = value;
    public double Maximum { get; } = maximum;
    public string Colour { get; } = colour;
    public double Percentage => Value / Maximum * 100;
    public string PercentageText => Percentage.ToString("0.##", CultureInfo.InvariantCulture) + "%";
    public string Description => Label + ": " + PercentageText;
    public string ColourHex => Colour switch
    {
        "green" => "2F855A", "blue" => "2563EB", "amber" => "B7791F", "red" => "C53030", "neutral" => "64748B", _ => "FF883E"
    };
}

/// <summary>Recognises bounded passive HTML components without executing or retaining imported attributes.</summary>
public static class SafeBlockSemantics
{
    public const int MaximumFragmentCharacters = 16 * 1024;
    public static void Apply(MarkdownDocument document)
    {
        foreach (var html in document.Descendants<HtmlBlock>().ToArray())
        {
            if (html.Span.Length > MaximumFragmentCharacters || html.Parent is not { } parent) continue;
            var progress = ReadProgress(html.Lines.ToString());
            if (progress is null) continue;
            progress.Span = html.Span; progress.Line = html.Line; progress.Column = html.Column;
            parent[parent.IndexOf(html)] = progress;
        }
    }

    private static DocumentProgressBlock? ReadProgress(string source)
    {
        source = source.Trim();
        if (source.Length > MaximumFragmentCharacters || !source.StartsWith("<figure", StringComparison.OrdinalIgnoreCase)
            || !source.EndsWith("</figure>", StringComparison.OrdinalIgnoreCase)) return null;
        var body = new HtmlParser().ParseDocument(source).Body!;
        if (body.Children.Length != 1 || !OnlyElementsAndWhitespace(body)) return null;
        var figure = body.Children[0];
        if (figure.LocalName != "figure" || !figure.HasAttribute("data-progress-bar") || figure.Children.Length != 2 || !OnlyElementsAndWhitespace(figure)) return null;
        var caption = figure.Children[0]; var progress = figure.Children[1];
        if (caption.LocalName != "figcaption" || progress.LocalName != "progress" || !OnlyText(progress)
            || caption.Children.Length != 2 || !OnlyElementsAndWhitespace(caption)) return null;
        var label = caption.Children[0]; var displayedValue = caption.Children[1];
        if (label.LocalName != "span" || !label.HasAttribute("data-progress-label") || !OnlyText(label)
            || displayedValue.LocalName != "span" || !displayedValue.HasAttribute("data-progress-value") || !OnlyText(displayedValue)) return null;
        var text = string.Join(' ', label.TextContent.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (text.Length is 0 or > 512 || text.Any(char.IsControl)) return null;
        if (!double.TryParse(progress.GetAttribute("value"), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value)
            || !double.TryParse(progress.GetAttribute("max"), NumberStyles.Float, CultureInfo.InvariantCulture, out var maximum) || !double.IsFinite(maximum) || maximum <= 0) return null;
        var colour = (figure.GetAttribute("data-progress-colour") ?? "accent").Trim().ToLowerInvariant();
        if (colour is not ("accent" or "green" or "blue" or "amber" or "red" or "neutral")) colour = "accent";
        return new(text, Math.Clamp(value, 0, maximum), maximum, colour);
    }
    private static bool OnlyElementsAndWhitespace(IElement element) => element.ChildNodes.All(n => n is IElement || n is IText text && string.IsNullOrWhiteSpace(text.TextContent));
    private static bool OnlyText(IElement element) => element.ChildNodes.All(n => n is IText);
}
