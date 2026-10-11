using System.Globalization;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LensDocsStudio.Core.Parsing;

[Flags]
public enum MarkdownEmphasis { None = 0, Bold = 1, Italic = 2, Strike = 4, Subscript = 8, Superscript = 16, Underline = 32, Highlight = 64 }

public static class MarkdownFormatting
{
    public static int OrderedStart(ListBlock list) => int.TryParse(list.OrderedStart, NumberStyles.None, CultureInfo.InvariantCulture, out var start) ? start : 1;
    public static string CalloutLabel(string kind) => kind.ToUpperInvariant() switch
    {
        "NOTE" => "Note", "TIP" => "Tip", "IMPORTANT" => "Important", "WARNING" => "Warning", "CAUTION" => "Caution", _ => kind
    };
    public static MarkdownEmphasis Emphasis(EmphasisInline inline) => inline.DelimiterChar switch
    {
        '*' or '_' => (inline.DelimiterCount >= 2 ? MarkdownEmphasis.Bold : MarkdownEmphasis.None) | (inline.DelimiterCount % 2 == 1 ? MarkdownEmphasis.Italic : MarkdownEmphasis.None),
        '~' => inline.DelimiterCount == 2 ? MarkdownEmphasis.Strike : MarkdownEmphasis.Subscript,
        '^' => MarkdownEmphasis.Superscript,
        '+' => MarkdownEmphasis.Underline,
        '=' => MarkdownEmphasis.Highlight,
        _ => MarkdownEmphasis.None
    };
}
