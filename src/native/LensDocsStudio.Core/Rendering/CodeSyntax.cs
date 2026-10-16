using Markdig.Syntax;

namespace LensDocsStudio.Core.Rendering;

public enum CodeTokenRole { Plain, Comment, String, Keyword, Number, Name }
public sealed record CodeSyntaxSpan(int Start, int Length, CodeTokenRole Role);
public sealed record CodeSyntaxBlock(string Text, IReadOnlyList<CodeSyntaxSpan> Spans)
{
    public const int MaximumSpans = 1024;
}
public sealed record PreparedCodeSyntax(IReadOnlyDictionary<CodeBlock, CodeSyntaxBlock> Blocks, bool Limited);
public sealed record CodeSyntaxRun(string Text, CodeTokenRole Role);

public static class CodeSyntaxRuns
{
    public static IEnumerable<CodeSyntaxRun> Read(string text, CodeSyntaxBlock? syntax)
    {
        if (syntax is null || syntax.Text != text || !Valid(syntax.Spans, text.Length))
        {
            yield return new(text, CodeTokenRole.Plain);
            yield break;
        }
        var position = 0;
        foreach (var span in syntax.Spans)
        {
            if (span.Start > position) yield return new(text[position..span.Start], CodeTokenRole.Plain);
            yield return new(text.Substring(span.Start, span.Length), span.Role);
            position = span.Start + span.Length;
        }
        if (position < text.Length) yield return new(text[position..], CodeTokenRole.Plain);
    }

    private static bool Valid(IReadOnlyList<CodeSyntaxSpan> spans, int length)
    {
        if (spans.Count > CodeSyntaxBlock.MaximumSpans) return false;
        var position = 0;
        foreach (var span in spans)
        {
            if (span.Start < position || span.Start > length || span.Length <= 0 || span.Length > length - span.Start
                || !Enum.IsDefined(span.Role)) return false;
            position = span.Start + span.Length;
        }
        return true;
    }
}

public static class CodeSyntaxPalette
{
    public static string Hex(CodeTokenRole role, bool dark = false) => (role, dark) switch
    {
        (CodeTokenRole.Comment, false) => "466147", (CodeTokenRole.Comment, true) => "A3B89C",
        (CodeTokenRole.String, false) => "166534", (CodeTokenRole.String, true) => "A6D69E",
        (CodeTokenRole.Keyword, false) => "5B21B6", (CodeTokenRole.Keyword, true) => "C7B3FF",
        (CodeTokenRole.Number, false) => "92400E", (CodeTokenRole.Number, true) => "E8C78F",
        (CodeTokenRole.Name, false) => "075985", (CodeTokenRole.Name, true) => "91CDF2",
        (_, false) => "202124", (_, true) => "E8EAED"
    };
}
