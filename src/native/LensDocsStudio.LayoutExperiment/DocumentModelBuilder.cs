using System.Collections.Immutable;
using System.Text;
using System.Windows;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markdig.Extensions.Tables;

namespace LensDocsStudio.LayoutExperiment;

/// <summary>Extracts transferable semantics; never creates a WPF document.</summary>
internal sealed class DocumentModelBuilder
{
    private readonly string source;
    private readonly CancellationToken cancellation;
    private readonly StringBuilder text = new();
    private readonly List<int> starts = [], ends = [];
    private readonly List<Item> paragraphs = [];
    private readonly IReadOnlyDictionary<string, PreviewImage> images;
    private readonly PreparedCodeSyntax syntax;
    private readonly bool dark;
    private int id;
    private string separator = "\r\n";

    private DocumentModelBuilder(ParsedDocument parsed, CancellationToken cancellation, string? path, string? root, string appearance)
    {
        source = parsed.Source; this.cancellation = cancellation; dark = appearance == "Dark";
        images = new PreviewImageService().Prepare(parsed, path, root, cancellation);
        syntax = CodeSyntaxService.Prepare(parsed, cancellation);
    }
    public static ListModel Parse(string source, CancellationToken cancellation, string? path, string? workspaceRoot, string appearance)
    {
        cancellation.ThrowIfCancellationRequested();
        var parsed = new MarkdownService().Parse(source);
        var builder = new DocumentModelBuilder(parsed, cancellation, path, workspaceRoot, appearance);
        var root = builder.Container(parsed.Syntax, "Document");
        foreach (var node in root.Descendants().Where(node => node.Paragraph >= 0))
        {
            var item = builder.paragraphs[node.Paragraph];
            var size = node.Kind == "Code" ? 13 : node.Kind == "Heading" ? node.Level switch { 1 => 30, 2 => 24, 3 => 20, _ => 17 } : 15;
            var header = node.Kind is "Heading" or "DetailsHeader";
            builder.paragraphs[node.Paragraph] = item with { Runs = item.Runs.Select(run => run with
            { FontSize = run.Emphasis.HasFlag(MarkdownEmphasis.Subscript) || run.Emphasis.HasFlag(MarkdownEmphasis.Superscript) ? 11 : size,
                Weight = run.Bold ? 700 : header ? 600 : 400 }).ToImmutableArray() };
        }
        builder.starts.Add(source.Length);
        var only = parsed.Syntax.Count == 1 ? parsed.Syntax[0] as ListBlock : null;
        var original = only is not null && only.All(item => item is ListItemBlock { Count: 1 } listItem
            && listItem[0] is ParagraphBlock { Inline: not null } paragraph && paragraph.Inline.Descendants().All(inline => inline is LiteralInline or EmphasisInline { DelimiterChar: '*' or '_' }));
        return new(source, builder.text.ToString(), builder.starts.ToImmutableArray(), builder.paragraphs.ToImmutableArray(), only?.IsOrdered ?? false,
            only is null ? 1 : MarkdownFormatting.OrderedStart(only))
        {
            Root = root, OriginalListShape = original, SourceEnds = builder.ends.ToImmutableArray(), Headings = parsed.Headings.ToImmutableArray()
        };
    }
    private SemanticNode Node(string kind, int start, int sourceStart, int sourceEnd, ImmutableArray<SemanticNode> children)
        => new(++id, kind, start, text.Length, Math.Clamp(sourceStart, 0, source.Length), Math.Clamp(sourceEnd, 0, source.Length), children);
    private SemanticNode Container(ContainerBlock block, string kind)
    {
        var start = text.Length; var children = block.Select(Block).ToImmutableArray();
        return Node(kind, start, block.Span.Start, block.Span.End + 1, children);
    }
    private SemanticNode Block(Block block)
    {
        cancellation.ThrowIfCancellationRequested();
        switch (block)
        {
            case HeadingBlock heading: return Paragraph(heading, "Heading") with { Level = heading.Level };
            case ParagraphBlock paragraph: return Paragraph(paragraph, "Paragraph");
            case ListBlock list: return Container(list, "List") with { Ordered = list.IsOrdered, Number = MarkdownFormatting.OrderedStart(list) };
            case ListItemBlock item: return Container(item, "ListItem");
            case QuoteBlock quote: return Container(quote, "Quote");
            case Table table: return Container(table, "Table");
            case TableRow row: return Container(row, "Row") with { Level = row.IsHeader ? 1 : 0 };
            case TableCell cell:
                var tableOwner = cell.Parent?.Parent as Table;
                var column = ((TableRow)cell.Parent!).IndexOf(cell);
                if (cell.ColumnSpan != 1 || cell.RowSpan != 1) throw new NotSupportedException("Spanning table cells require a separate geometry contract.");
                var align = tableOwner is not null && column < tableOwner.ColumnDefinitions.Count ? tableOwner.ColumnDefinitions[column].Alignment : null;
                var savedSeparator = separator; separator = column + 1 == ((TableRow)cell.Parent!).Count ? "\r\n" : "\t";
                var cellNode = Container(cell, "Cell"); separator = savedSeparator;
                return cellNode with { Alignment = align switch { TableColumnAlign.Center => TextAlignment.Center, TableColumnAlign.Right => TextAlignment.Right, _ => TextAlignment.Left } };
            case DocumentDetailsBlock details:
                var begin = text.Length;
                var summaryAt = source.IndexOf(details.Summary, Math.Clamp(details.Span.Start, 0, source.Length), StringComparison.Ordinal);
                var header = Plain(details.Summary, Math.Max(details.Span.Start, summaryAt), "DetailsHeader");
                var body = details.Select(Block).ToImmutableArray();
                return Node("Details", begin, details.Span.Start, details.Span.End + 1, [header, ..body]) with { Expanded = details.InitiallyExpanded, Label = details.Summary };
            case FencedCodeBlock code when string.Equals(code.Info, "mermaid", StringComparison.OrdinalIgnoreCase):
                var diagram = new DiagramService().Render(code.Lines.ToString(), dark, cancellationToken: cancellation);
                if (diagram.Svg is null) throw new IOException(diagram.Error);
                var drawing = new NativeSvgService().Render(diagram.Svg); drawing.Freeze();
                var media = Plain("\uFFFC", code.Span.Start, "Media");
                return media with { Media = drawing, Label = "Mermaid diagram", Target = code.Lines.ToString() };
            case CodeBlock code:
                var content = code.Lines.ToString();
                var at = text.Length; var runs = ImmutableArray.CreateBuilder<StyledRun>();
                syntax.Blocks.TryGetValue(code, out var colours);
                var contentStart = code.Lines.Count > 0 ? code.Lines.Lines[0].Slice.Start : code.Span.Start;
                foreach (var run in CodeSyntaxRuns.Read(content, colours))
                { AddText(run.Text, contentStart, contentStart + run.Text.Length, MarkdownEmphasis.None, true, null, runs, CodeSyntaxPalette.Hex(run.Role, dark)); contentStart += run.Text.Length; }
                return Finish("Code", at, code.Span.Start, code.Span.End + 1, runs);
            case ThematicBreakBlock:
                return Plain("", block.Span.Start, "Rule");
            case HtmlBlock html: return Plain(html.Lines.ToString(), block.Span.Start, "Code");
            default: throw new NotSupportedException("Unsupported experimental block: " + block.GetType().Name);
        }
    }
    private SemanticNode Plain(string value, int sourceStart, string kind)
    {
        var start = text.Length; var runs = ImmutableArray.CreateBuilder<StyledRun>();
        AddText(value, sourceStart, Math.Min(source.Length, sourceStart + value.Length), MarkdownEmphasis.None, kind == "Code", null, runs);
        return Finish(kind, start, sourceStart, Math.Min(source.Length, sourceStart + value.Length), runs);
    }
    private SemanticNode Paragraph(LeafBlock block, string kind)
    {
        var at = text.Length; var runs = ImmutableArray.CreateBuilder<StyledRun>(); var inlineNodes = ImmutableArray.CreateBuilder<SemanticNode>();
        if (block.Inline is not null) Add(block.Inline, MarkdownEmphasis.None, null);
        var node = Finish(kind, at, block.Span.Start, block.Span.End + 1, runs);
        return node with { Children = inlineNodes.ToImmutable() };

        void Add(ContainerInline parent, MarkdownEmphasis emphasis, string? link)
        {
            foreach (var inline in parent)
            {
                cancellation.ThrowIfCancellationRequested();
                switch (inline)
                {
                    case EmphasisInline styled: Add(styled, emphasis | MarkdownFormatting.Emphasis(styled), link); break;
                    case LinkInline image when image.IsImage:
                        if (!images.TryGetValue(image.Url ?? "", out var pixels) || pixels.Pixels is null) throw new IOException(pixels?.Error ?? "Image preparation failed.");
                        var imageStart = text.Length;
                        AddText("\uFFFC", image.Span.Start, image.Span.End + 1, emphasis, false, null, runs);
                        inlineNodes.Add(Node("Image", imageStart, image.Span.Start, image.Span.End + 1, []) with { Media = pixels.Pixels, Label = MarkdownService.InlineText(image), Target = image.Url });
                        break;
                    case LinkInline target:
                        var linkStart = text.Length; Add(target, emphasis, target.Url);
                        inlineNodes.Add(Node("Link", linkStart, target.Span.Start, target.Span.End + 1, []) with { Target = target.Url }); break;
                    case AutolinkInline auto:
                        var autoStart = text.Length; AddText(auto.Url, auto.Span.Start, auto.Span.End + 1, emphasis, false, MarkdownLinks.AutolinkReference(auto), runs);
                        inlineNodes.Add(Node("Link", autoStart, auto.Span.Start, auto.Span.End + 1, []) with { Target = MarkdownLinks.AutolinkReference(auto) }); break;
                    case LiteralInline literal: AddText(literal.Content.ToString(), inline.Span.Start, inline.Span.End + 1, emphasis, false, link, runs); break;
                    case HtmlEntityInline entity: AddText(entity.Transcoded.ToString(), inline.Span.Start, inline.Span.End + 1, emphasis, false, link, runs); break;
                    case CodeInline code: AddText(code.Content, inline.Span.Start, inline.Span.End + 1, emphasis, true, link, runs); break;
                    case LineBreakInline line: AddText(line.IsHard ? "\n" : " ", inline.Span.Start, inline.Span.End + 1, emphasis, false, link, runs); break;
                    case DocumentAnchorInline anchor:
                        var anchorStart = text.Length; AddText(anchor.Text, inline.Span.Start, inline.Span.End + 1, emphasis, false, link, runs);
                        inlineNodes.Add(Node("Anchor", anchorStart, inline.Span.Start, inline.Span.End + 1, []) with { Target = anchor.Id }); break;
                    case DocumentSemanticInline semantic: AddText(semantic.Text, inline.Span.Start, inline.Span.End + 1, emphasis, true, link, runs); break;
                    case HtmlInline html: AddText(html.Tag, inline.Span.Start, inline.Span.End + 1, emphasis, false, link, runs); break;
                    case ContainerInline nested: Add(nested, emphasis, link); break;
                    default: throw new NotSupportedException("Unsupported experimental inline: " + inline.GetType().Name);
                }
            }
        }
    }
    private SemanticNode Finish(string kind, int start, int sourceStart, int sourceEnd, ImmutableArray<StyledRun>.Builder runs)
    {
        var paragraph = paragraphs.Count;
        paragraphs.Add(new(start, text.Length - start, sourceStart, sourceEnd, runs.ToImmutable()));
        text.Append(separator); foreach (var character in separator) { starts.Add(Math.Clamp(sourceEnd, 0, source.Length)); ends.Add(Math.Clamp(sourceEnd, 0, source.Length)); }
        return Node(kind, start, sourceStart, sourceEnd, []) with { Paragraph = paragraph };
    }
    private void AddText(string value, int sourceStart, int sourceEnd, MarkdownEmphasis emphasis, bool code, string? link, ImmutableArray<StyledRun>.Builder runs, string? colour = null)
    {
        sourceStart = Math.Clamp(sourceStart, 0, source.Length); sourceEnd = Math.Clamp(sourceEnd, sourceStart, source.Length);
        var exact = LiteralSourceMapping.Locate(source, sourceStart, Math.Max(sourceStart, sourceEnd - 1), value);
        // Entities/escaped source and collapsed code whitespace own their full parser span.
        // Literal substrings retain exact individual UTF-16 offsets.
        var begin = exact?.Start ?? sourceStart; var finish = exact?.End ?? sourceEnd;
        runs.Add(new(text.Length, value, begin, emphasis.HasFlag(MarkdownEmphasis.Bold), emphasis.HasFlag(MarkdownEmphasis.Italic), code)
            { Emphasis = emphasis, SourceEnd = finish, Link = link, Colour = colour });
        for (var i = 0; i < value.Length; i++) { starts.Add(exact is null ? begin : begin + i); ends.Add(exact is null ? finish : begin + i + 1); }
        text.Append(value);
    }
}
