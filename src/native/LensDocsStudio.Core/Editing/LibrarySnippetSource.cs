using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LensDocsStudio.Core.Editing;

public static class LibrarySnippetSource
{
    public static string Select(string source, int start, int length)
    {
        if (start < 0 || length < 1 || (long)start + length > source.Length) throw new ArgumentOutOfRangeException(nameof(start));
        if (source.Length > 4 * 1024 * 1024) throw new IOException("Saving snippets supports source documents up to 4 million characters.");
        var selected = source.Substring(start, length);
        var definitions = new MarkdownService().Parse(source).Syntax.Descendants<LinkInline>()
            .Where(link => link.Span.Start >= start && link.Span.End < start + length && link.Reference is not null)
            .Select(link => link.Reference!).Distinct().Where(reference => reference.Span.Start < start || reference.Span.End >= start + length).ToArray();
        var newline = source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        foreach (var reference in definitions)
        {
            if (reference.Span.Start < 0 || reference.Span.Length <= 0 || reference.Span.End >= source.Length)
                throw new IOException("A snippet reference definition has unavailable source positions. Include its definition in the selection.");
            selected += newline + newline + source.Substring(reference.Span.Start, reference.Span.Length);
        }
        return selected;
    }
}
