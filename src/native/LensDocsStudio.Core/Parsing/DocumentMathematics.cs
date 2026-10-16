using Markdig.Extensions.Mathematics;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Syntax;

namespace LensDocsStudio.Core.Parsing;

public sealed record DocumentFormula(int SourceStart, string Text, bool Display, bool Complete);

public static class DocumentMathematics
{
    public static IEnumerable<DocumentFormula> Read(ContainerBlock scope)
    {
        foreach (var node in scope.Descendants())
        {
            if (node is MathBlock block) yield return new(block.Span.Start, block.Lines.ToString(), true, block.ClosingFencedCharCount == 2);
            else if (node is MathInline inline) yield return new(inline.Span.Start, inline.Content.ToString(), inline.DelimiterCount == 2, true);
        }
    }
}

/// <summary>Recognises the reference application's dollar and parenthesised inline formulae before Markdown escapes.</summary>
internal sealed class DocumentMathInlineParser : MathInlineParser
{
    public DocumentMathInlineParser() { OpeningCharacters = ['$', '\\']; }
    public override bool Match(InlineProcessor processor, ref StringSlice slice)
    {
        var start = slice.Start;
        var parenthesised = slice.CurrentChar == '\\' && slice.PeekChar() == '(';
        if (slice.CurrentChar != '$' && !parenthesised) return false;
        var count = parenthesised || slice.PeekChar() == '$' ? 2 : 1;
        var contentStart = start + count;
        var close = parenthesised ? "\\)" : count == 2 ? "$$" : "$";
        var end = slice.Text.IndexOf(close, contentStart, Math.Min(8192, slice.End - contentStart + 1), StringComparison.Ordinal);
        if (end <= contentStart) return false;
        var content = slice.Text.AsSpan(contentStart, end - contentStart);
        if (!parenthesised && (content.Contains('\n') || content.Contains('\r') || char.IsWhiteSpace(content[0]) || char.IsWhiteSpace(content[^1]))) return false;
        if (count == 1 && end + 1 <= slice.End && char.IsDigit(slice.Text[end + 1])) return false;
        processor.Inline = new MathInline
        {
            Content = new StringSlice(slice.Text, contentStart, end - 1), Delimiter = parenthesised ? '\\' : '$', DelimiterCount = parenthesised ? 1 : count,
            Span = new(processor.GetSourcePosition(start, out var line, out var column), processor.GetSourcePosition(end + close.Length - 1)), Line = line, Column = column
        };
        slice.Start = end + close.Length; return true;
    }
}
