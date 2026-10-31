using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LensDocsStudio.ContinuousTextFormatter;

// Immutable, non-visual preparation only. Unsupported semantics fail explicitly.
internal sealed record StyledLiteral(string Text, int SourceStart, MarkdownEmphasis Style);
internal sealed record ListParagraph(int SourceStart, int SourceEnd, int ListId, int ItemId,
    int Depth, bool Ordered, int Number, bool Marker, ImmutableArray<StyledLiteral> Spans)
{
    public string Text => string.Concat(Spans.Select(span => span.Text));
}
internal sealed record ListNode(int Id, int? ParentItem, int Depth, bool Ordered, int Start, int Items);
internal sealed record ListItemNode(int Id, int ListId, int SourceStart, int SourceEnd, int Number);
internal sealed record ListSnapshot(string Source, string Sha256, long Generation,
    ImmutableArray<ListNode> Lists, ImmutableArray<ListItemNode> Items, ImmutableArray<ListParagraph> Paragraphs)
{
    // WPF TextRange includes generated markers and tabs in its logical list text.
    // They have no literal Markdown source interval.
    public string Text => string.Concat(Paragraphs.Select(paragraph =>
        (paragraph.Marker ? (paragraph.Ordered ? paragraph.Number.ToString(System.Globalization.CultureInfo.InvariantCulture) + "." : "•") + "\t" : "")
        + paragraph.Text + "\r\n"));

    public static ListSnapshot Create(ParsedDocument parsed, long generation, CancellationToken token = default)
    {
        var paragraphs = ImmutableArray.CreateBuilder<ListParagraph>();
        var lists = new List<ListNode>();
        var items = ImmutableArray.CreateBuilder<ListItemNode>();
        foreach (var block in parsed.Syntax)
        {
            if (block is not ListBlock list) throw new NotSupportedException("The feasibility spike accepts list blocks only: " + block.GetType().Name);
            AddList(list, 0, null);
        }
        if (lists.Count == 0) throw new NotSupportedException("The feasibility spike requires a list.");
        return new(parsed.Source, Hash(parsed.Source), generation, [.. lists], items.ToImmutable(), paragraphs.ToImmutable());

        void AddList(ListBlock list, int depth, int? parentItem)
        {
            token.ThrowIfCancellationRequested();
            var id = lists.Count;
            var start = list.IsOrdered ? MarkdownFormatting.OrderedStart(list) : 1;
            lists.Add(new(id, parentItem, depth, list.IsOrdered, start, list.Count));
            var number = start;
            foreach (var item in list.OfType<ListItemBlock>())
            {
                token.ThrowIfCancellationRequested();
                var itemId = items.Count;
                items.Add(new(itemId, id, item.Span.Start, item.Span.End + 1, number));
                var first = true;
                foreach (var child in item)
                {
                    if (child is ParagraphBlock paragraph)
                    {
                        var spans = ImmutableArray.CreateBuilder<StyledLiteral>();
                        if (paragraph.Inline is not null) AddInlines(paragraph.Inline, MarkdownEmphasis.None, spans);
                        paragraphs.Add(new(paragraph.Span.Start, paragraph.Span.End + 1, id, itemId,
                            depth, list.IsOrdered, number, first, spans.ToImmutable()));
                        first = false;
                    }
                    else if (child is ListBlock nested && !first) AddList(nested, depth + 1, itemId);
                    else throw new NotSupportedException("Unsupported list child or marker-only parent: " + child.GetType().Name);
                }
                if (first) throw new NotSupportedException("Marker-only items require a separate semantic implementation.");
                number = checked(number + 1);
            }
        }

        void AddInlines(ContainerInline container, MarkdownEmphasis style, ImmutableArray<StyledLiteral>.Builder spans)
        {
            foreach (var inline in container)
            {
                token.ThrowIfCancellationRequested();
                if (inline is LiteralInline literal)
                {
                    var text = literal.Content.ToString();
                    if (text.Length == 0) continue;
                    // No string search: a literal must occupy exactly its parser span.
                    if (literal.Span.Length != text.Length || literal.Span.Start < 0
                        || literal.Span.End >= parsed.Source.Length
                        || !parsed.Source.AsSpan(literal.Span.Start, text.Length).SequenceEqual(text))
                        throw new NotSupportedException("A transformed literal needs an explicit source-boundary map.");
                    spans.Add(new(text, literal.Span.Start, style));
                }
                else if (inline is EmphasisInline emphasis)
                {
                    var added = MarkdownFormatting.Emphasis(emphasis);
                    if ((added & ~(MarkdownEmphasis.Bold | MarkdownEmphasis.Italic)) != 0)
                        throw new NotSupportedException("Only bold and italic styles are implemented in the spike.");
                    AddInlines(emphasis, style | added, spans);
                }
                else throw new NotSupportedException("Unsupported list inline: " + inline.GetType().Name);
            }
        }
    }

    public static string Hash(string source) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
}
