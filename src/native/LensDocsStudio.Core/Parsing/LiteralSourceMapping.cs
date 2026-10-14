namespace LensDocsStudio.Core.Parsing;

public sealed record LiteralSourceSpan(int Start, int Length)
{
    public int End => Start + Length;
}

/// <summary>Find literal text only within its own parser span, never elsewhere in the document.</summary>
public static class LiteralSourceMapping
{
    public static LiteralSourceSpan? Locate(string source, int start, int endInclusive, string renderedText)
    {
        if (start < 0 || endInclusive < start || endInclusive >= source.Length || renderedText.Length == 0) return null;
        var region = source.AsSpan(start, endInclusive - start + 1);
        var offset = region.IndexOf(renderedText.AsSpan(), StringComparison.Ordinal);
        if (offset < 0) return null;
        // Delimiters may surround a literal; ambiguous transformed spans get semantic treatment.
        if (region[(offset + 1)..].IndexOf(renderedText.AsSpan(), StringComparison.Ordinal) >= 0) return null;
        return new(start + offset, renderedText.Length);
    }
}
