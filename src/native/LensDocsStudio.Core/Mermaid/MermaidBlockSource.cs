using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;

namespace LensDocsStudio.Core.Mermaid;

public sealed record MermaidBlockSource(MermaidSourceSpan Container, MermaidSourceSpan Body, string Source, bool Standalone)
{
    public bool ContainerEndsWithLineBreak { get; init; } = true;
    public bool IsInsertion { get; init; }
    public static IReadOnlyList<MermaidBlockSource> Find(ParsedDocument document, bool standalone = false)
    {
        var source = document.Source;
        if (standalone) return [new(new(0, source.Length), new(0, source.Length), source, true)];
        var found = new List<MermaidBlockSource>();
        foreach (var code in document.Syntax.Descendants<FencedCodeBlock>().Where(code => code.Info is "mermaid" or "mmd"))
        {
            if (code.ClosingFencedCharCount < 3) continue;
            var openingLine = LineStart(source, code.Span.Start);
            var bodyStart = source.IndexOf('\n', code.Span.Start);
            if (bodyStart < 0) continue; bodyStart++;
            var closingLine = LineStart(source, Math.Min(source.Length - 1, code.Span.End));
            if (closingLine < bodyStart) continue;
            // Block quotes/list prefixes need a per-line map. Protect them instead of stripping source.
            var opening = source[openingLine..(bodyStart - 1)].TrimEnd('\r');
            var trimmed = opening.TrimStart(' ', '\t');
            var closingEnd = source.IndexOf('\n', closingLine); if (closingEnd < 0) closingEnd = source.Length;
            var closing = source[closingLine..closingEnd].Trim();
            if (!(trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal)
                || trimmed.StartsWith(":::mermaid", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("::: mermaid", StringComparison.OrdinalIgnoreCase))) continue;
            if (!(closing.All(character => character == '`') || closing.All(character => character == '~') || closing == ":::")) continue;
            var body = new MermaidSourceSpan(bodyStart, closingLine - bodyStart);
            var hasNewline = closingEnd < source.Length;
            found.Add(new(new(openingLine, closingEnd + (hasNewline ? 1 : 0) - openingLine), body, body.Read(source), false)
            { ContainerEndsWithLineBreak = hasNewline });
        }
        return found;
    }
    private static int LineStart(string source, int offset) => offset <= 0 ? 0 : source.LastIndexOf('\n', offset - 1) + 1;

    /// <summary>Relocates only through an owning document's actual edit history; identical text is never searched.</summary>
    public MermaidSourceSpan Relocate(string currentSource, IEnumerable<MermaidSourceChange> history)
    {
        var container = Container; var body = Body;
        foreach (var change in history)
        {
            if (change.Offset < 0 || change.Removed < 0 || change.Inserted < 0) throw new ArgumentException("The document history is invalid.");
            if (change.Offset + change.Removed <= container.Start && (change.Removed > 0 || change.Offset < container.Start))
            {
                var delta = change.Inserted - change.Removed;
                container = container with { Start = container.Start + delta }; body = body with { Start = body.Start + delta };
            }
            else if (change.Offset < container.End || change.Offset == container.Start
                || change.Offset == container.End && (Standalone || !ContainerEndsWithLineBreak))
                throw new ArgumentException("This diagram or its fence changed while the visual editor was open. Copy the draft, reload the current diagram, or cancel.");
        }
        if (body.Start < 0 || body.End > currentSource.Length || body.Read(currentSource) != Source)
            throw new ArgumentException("The original diagram is no longer at its tracked source location. The draft was not applied.");
        if (!Standalone && !IsInsertion && container.Start > 0 && currentSource[container.Start - 1] is not ('\n' or '\r'))
            throw new ArgumentException("The opening Mermaid container is no longer at the start of a line. Reopen the current source.");
        return body;
    }
    public string Replacement(string draft)
    {
        if (Standalone || draft.Length == 0 || draft.EndsWith('\n')) return draft;
        return draft + (Source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n");
    }
}

public readonly record struct MermaidSourceChange(int Offset, int Removed, int Inserted);
