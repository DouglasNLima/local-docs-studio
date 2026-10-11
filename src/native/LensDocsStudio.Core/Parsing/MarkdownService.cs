using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markdig.Renderers.Html;
using System.Text.RegularExpressions;

namespace LensDocsStudio.Core.Parsing;

public sealed record DocumentHeading(string Title, string Anchor, int Level, int SourceStart, int SourceLine);
public sealed record ParsedDocument(string Source, MarkdownDocument Syntax, IReadOnlyList<DocumentHeading> Headings)
{
    public DocumentMetadata Metadata { get; init; } = new();
    public IReadOnlyList<DocumentTarget> Targets { get; init; } = [];
}

public sealed partial class MarkdownService
{
    public MarkdownPipeline Pipeline { get; } = new MarkdownPipelineBuilder().UseAdvancedExtensions().UsePreciseSourceLocation().Build();

    public ParsedDocument Parse(string source, bool standaloneMermaid = false)
    {
        // Azure DevOps containers are replaced with equal-length fence lines to preserve source offsets.
        var frontMatter = standaloneMermaid ? new FrontMatterResult(new(), 0) : FrontMatterService.Read(source);
        var normalised = NormaliseDevOps(FrontMatterService.HideHeader(source, frontMatter.BodyStart));
        var syntax = Markdown.Parse(standaloneMermaid ? "```mermaid\n" + source + "\n```" : normalised, Pipeline);
        if (standaloneMermaid && syntax.FirstOrDefault() is FencedCodeBlock standalone)
        {
            standalone.Span = new(0, Math.Max(0, source.Length - 1));
            standalone.Line = 0;
        }
        if (!standaloneMermaid) { SafeInlineSemantics.Apply(syntax, source); SafeBlockSemantics.Apply(syntax); }
        var targets = DocumentAnchors.Collect(syntax);
        var headings = syntax.Descendants<HeadingBlock>().Select(h =>
            new DocumentHeading(InlineText(h.Inline), h.GetAttributes().Id ?? "", h.Level, h.Span.Start, h.Line)).ToArray();
        return new(source, syntax, headings) { Metadata = frontMatter.Metadata, Targets = targets };
    }

    public string ToSafeHtml(string source) => Markdown.ToHtml(NormaliseDevOps(FrontMatterService.HideHeader(source, FrontMatterService.Read(source).BodyStart)),
        new MarkdownPipelineBuilder().UseAdvancedExtensions().DisableHtml().Build());

    public static string NormaliseDevOps(string source)
    {
        var lines = source.Split('\n');
        var devOps = false; char fence = '\0'; var fenceLength = 0;
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].TrimEnd('\r');
            var standard = StandardFence().Match(line);
            if (!devOps && standard.Success)
            {
                var marker = standard.Groups[1].Value;
                if (fence == '\0') { fence = marker[0]; fenceLength = marker.Length; }
                else if (fence == marker[0] && marker.Length >= fenceLength && line[standard.Length..].Trim().Length == 0) fence = '\0';
                continue;
            }
            if (fence != '\0') continue;
            var match = DevOpsFence().Match(line);
            if (!match.Success) continue;
            var opening = match.Groups[1].Success;
            if ((!devOps && opening) || (devOps && !opening))
            {
                var offset = lines[index].IndexOf(":::", StringComparison.Ordinal);
                lines[index] = lines[index][..offset] + "```" + lines[index][(offset + 3)..];
                devOps = opening;
            }
        }
        return string.Join('\n', lines);
    }

    [GeneratedRegex(@"^[ \t]*:::(?:[ \t]*(mermaid)[ \t]*|[ \t]*)$", RegexOptions.IgnoreCase)]
    private static partial Regex DevOpsFence();
    [GeneratedRegex(@"^ {0,3}(`{3,}|~{3,})")]
    private static partial Regex StandardFence();

    public static string InlineText(ContainerInline? inline) => inline is null ? "" :
        string.Concat(inline.Select(child => child switch
        {
            DocumentSemanticInline semantic => semantic.Text,
            DocumentAnchorInline anchor => anchor.Text,
            LiteralInline literal => literal.Content.ToString(),
            HtmlEntityInline entity => entity.Transcoded.ToString(),
            CodeInline code => code.Content,
            LineBreakInline => " ",
            ContainerInline container => InlineText(container),
            _ => ""
        }));
}
