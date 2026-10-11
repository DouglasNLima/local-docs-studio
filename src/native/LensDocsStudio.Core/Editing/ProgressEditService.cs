using System.Net;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;

namespace LensDocsStudio.Core.Editing;

public sealed record ProgressOptions(string Label = "Progress", int Percentage = 50, string Colour = "accent");

public static class ProgressEditService
{
    public static DocumentProgressBlock? Find(ParsedDocument document, int start, int length)
        => document.Syntax.Descendants<DocumentProgressBlock>().FirstOrDefault(b => start >= b.Span.Start && (long)start + length <= b.Span.End + 1);

    public static MarkdownEdit Plan(ParsedDocument document, int start, int length, ProgressOptions options)
    {
        var source = document.Source;
        if (start < 0 || length < 0 || (long)start + length > source.Length) throw new ArgumentOutOfRangeException(nameof(start));
        var label = string.Join(' ', options.Label.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (label.Length is 0 or > 512 || label.Any(char.IsControl) || options.Percentage is < 0 or > 100
            || options.Colour is not ("accent" or "green" or "blue" or "amber" or "red" or "neutral")) throw new ArgumentException("Enter a label, a percentage from 0 to 100 and a supported colour.", nameof(options));
        var encoded = WebUtility.HtmlEncode(label);
        var value = $"<figure data-progress-bar data-progress-colour=\"{options.Colour}\" aria-label=\"{encoded} progress\">\n  <figcaption>\n    <span data-progress-label>{encoded}</span>\n    <span data-progress-value>{options.Percentage}%</span>\n  </figcaption>\n  <progress max=\"100\" value=\"{options.Percentage}\">{options.Percentage}%</progress>\n</figure>";
        if (Find(document, start, length) is { } existing)
        {
            var newline = source.IndexOf('\n') is var first && first > 0 && source[first - 1] == '\r' ? "\r\n" : "\n";
            value = value.Replace("\n", newline);
            return new(existing.Span.Start, existing.Span.Length, value, existing.Span.Start, value.Length);
        }
        return MarkdownEditService.PlanBlock(source, start, length, value, value);
    }
}
