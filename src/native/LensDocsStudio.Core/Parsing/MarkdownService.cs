using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markdig.Renderers.Html;
using System.Text.RegularExpressions;

namespace LensDocsStudio.Core.Parsing;

public sealed record DocumentHeading(string Title, string Anchor, int Level, int SourceStart, int SourceLine);
public sealed record ParsedDocument(string Source, MarkdownDocument Syntax, IReadOnlyList<DocumentHeading> Headings);

public sealed partial class MarkdownService
{
    public MarkdownPipeline Pipeline { get; } = new MarkdownPipelineBuilder().UseAdvancedExtensions().UsePreciseSourceLocation().Build();

    public ParsedDocument Parse(string source, bool standaloneMermaid = false)
    {
        // Azure DevOps containers are replaced with equal-length fence lines to preserve source offsets.
        var normalised = NormaliseDevOps(source);
        var syntax = Markdown.Parse(standaloneMermaid ? "```mermaid\n" + source + "\n```" : normalised, Pipeline);
        var headings = syntax.Descendants<HeadingBlock>().Select(h =>
            new DocumentHeading(InlineText(h.Inline), h.GetAttributes().Id ?? "", h.Level, h.Span.Start, h.Line)).ToArray();
        return new(source, syntax, headings);
    }

    public string ToSafeHtml(string source) => Markdown.ToHtml(NormaliseDevOps(source),
        new MarkdownPipelineBuilder().UseAdvancedExtensions().DisableHtml().Build());

    public static string NormaliseDevOps(string source) => DevOpsFence().Replace(source, match =>
        match.Value.Contains("mermaid", StringComparison.OrdinalIgnoreCase)
            ? "```" + match.Value[3..]
            : "```" + match.Value[3..]);

    [GeneratedRegex(@"(?m)^:::(?:[ \t]+mermaid[ \t]*|[ \t]*)(?=\r?$)", RegexOptions.IgnoreCase)]
    private static partial Regex DevOpsFence();

    public static string InlineText(ContainerInline? inline) => inline is null ? "" :
        string.Concat(inline.Select(child => child switch
        {
            LiteralInline literal => literal.Content.ToString(),
            CodeInline code => code.Content,
            LineBreakInline => " ",
            ContainerInline container => InlineText(container),
            _ => ""
        }));
}
