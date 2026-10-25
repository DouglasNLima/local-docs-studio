using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;

namespace LensDocsStudio.Core.Mermaid;

public enum MermaidCreationRoute { NewDiagram, ExistingDiagram, SourceOnly }

public sealed record MermaidCreationTarget(MermaidCreationRoute Route, MermaidBlockSource? Block, int InsertionOffset, string Explanation)
{
    /// <summary>Uses the real Markdown tree and exact mapped occurrences, never a source-body search.</summary>
    public static MermaidCreationTarget Resolve(ParsedDocument document, int selectionStart, int selectionLength)
    {
        var source = document.Source;
        if (selectionStart < 0 || selectionLength < 0 || selectionStart > source.Length - selectionLength)
            throw new ArgumentException("The captured source selection is no longer valid.");
        var end = selectionStart + selectionLength;
        if (document.StandaloneMermaid)
            return string.IsNullOrWhiteSpace(source)
                ? new(MermaidCreationRoute.NewDiagram, null, 0, "Apply creates raw Mermaid source. Save remains separate.")
                : new(MermaidCreationRoute.ExistingDiagram, MermaidBlockSource.Find(document, true).Single(), 0, "Edit the existing standalone diagram; its family is preserved.");

        var blocks = MermaidBlockSource.Find(document);
        var inside = blocks.Where(block => selectionStart >= block.Body.Start && selectionStart < block.Body.End && end <= block.Body.End).ToArray();
        if (inside.Length == 1) return new(MermaidCreationRoute.ExistingDiagram, inside[0], 0, "Edit this exact Mermaid source occurrence.");

        bool Touches(Block block)
        {
            // Markdig's unterminated fence span can end at its opening marker. It cannot authorise insertion after that marker.
            var last = block is FencedCodeBlock { ClosingFencedCharCount: < 3 } ? source.Length : block.Span.End;
            return selectionLength == 0 ? selectionStart >= block.Span.Start && selectionStart <= last
                : selectionStart <= last && end > block.Span.Start;
        }
        // Include ordinary/unterminated fences and containers which cannot be mapped without stripping prefixes.
        if (document.Syntax.Descendants<Block>().Any(block => block is CodeBlock && Touches(block)))
            return SourceOnly("The selection touches a code fence, multiple blocks or protected code. Move into one Mermaid body, or edit source.");
        if (document.Syntax.Descendants<Block>().Any(block => block is ListBlock or QuoteBlock or HtmlBlock && Touches(block)))
            return SourceOnly("This nested Markdown or HTML position is protected. Choose a blank line outside the structure, or edit source.");
        var offset = end;
        if (offset == 0 && source.StartsWith('\uFEFF')) offset = 1;
        if (offset > 0 && offset < source.Length && (source[offset - 1] == '\r' && source[offset] == '\n'
            || char.IsHighSurrogate(source[offset - 1]) && char.IsLowSurrogate(source[offset])))
            return SourceOnly("The insertion point divides a line ending or Unicode character. Choose a complete character boundary.");
        return new(MermaidCreationRoute.NewDiagram, null, offset, selectionLength > 0
            ? "Apply inserts after the selection, preserving all selected text. Save remains separate."
            : "Apply inserts at the captured caret. Save remains separate.");
    }

    private static MermaidCreationTarget SourceOnly(string reason) => new(MermaidCreationRoute.SourceOnly, null, 0, reason);

    public static string FormatInsertion(string draft, string current, MermaidSourceSpan span)
    {
        if (span.Length != 0) throw new ArgumentException("New Mermaid diagrams cannot replace selected text.");
        var newline = current.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var longest = 0; var run = 0;
        foreach (var character in draft) { run = character == '`' ? run + 1 : 0; longest = Math.Max(longest, run); }
        var fence = new string('`', Math.Max(3, longest + 1));
        var before = current[..span.Start]; var after = current[span.End..];
        var prefix = before.Length == 0 || before.EndsWith(newline + newline, StringComparison.Ordinal) ? ""
            : before.EndsWith(newline, StringComparison.Ordinal) ? newline : newline + newline;
        var suffix = after.Length == 0 || after.StartsWith(newline + newline, StringComparison.Ordinal) ? ""
            : after.StartsWith(newline, StringComparison.Ordinal) ? newline : newline + newline;
        return prefix + fence + "mermaid" + newline + draft.TrimEnd('\r', '\n') + newline + fence + suffix;
    }
}
