using System.Globalization;
using System.Text;
using LensDocsStudio.Core.Parsing;
using Markdig.Extensions.Alerts;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace ContinuousViewer.Model;

public enum PieceKind { Text, Heading, ListItem, TableRow, Code, Quote, Details, Diagram, Maths, Image, Callout }
public sealed record TextRun(int Start, string Text, int SourceStart, int SourceEnd, MarkdownEmphasis Style = MarkdownEmphasis.None, string? Link = null, bool Code = false);
public sealed record CellSlice(int Column, int Start, int Length, string Alignment);
public sealed record LogicalPiece(int Id, int Start, int Length, int SourceStart, int SourceEnd, PieceKind Kind,
    int Level, int[] Details, TextRun[] Runs, string? Resource = null, int Group = -1)
{
    public int End => Start + Length;
    public CellSlice[] Cells { get; init; } = [];
    public int TableColumns { get; init; }
}
public sealed record DetailsGroup(int Id, int SourceStart, string Summary, bool InitiallyExpanded);

/// <summary>The complete, source-authoritative document. No WPF objects or viewport state.</summary>
public sealed class LogicalDocument
{
    // A single control must never contain a whole list, table, quote or oversized paragraph.
    public const int PieceCharacters = 512;
    public ParsedDocument Parsed { get; }
    public string Text { get; }
    public LogicalPiece[] Pieces { get; }
    public DetailsGroup[] Groups { get; }
    public int[] Graphemes { get; }
    private LogicalDocument(ParsedDocument parsed, string text, LogicalPiece[] pieces, DetailsGroup[] groups)
    {
        Parsed = parsed; Text = text; Pieces = pieces; Groups = groups;
        Graphemes = [.. StringInfo.ParseCombiningCharacters(text), text.Length];
    }
    public int Snap(int offset, bool forward = false)
    {
        offset = Math.Clamp(offset, 0, Text.Length); var found = Array.BinarySearch(Graphemes, offset);
        return found >= 0 ? Graphemes[found] : Graphemes[Math.Clamp(~found - (forward ? 0 : 1), 0, Graphemes.Length - 1)];
    }
    public int Step(int offset, int direction)
    {
        var index = Array.BinarySearch(Graphemes, Snap(offset));
        return Graphemes[Math.Clamp(index + direction, 0, Graphemes.Length - 1)];
    }
    public LogicalPiece At(int offset)
    {
        if (Pieces.Length == 0) throw new InvalidOperationException("The document has no text pieces.");
        int low = 0, high = Pieces.Length - 1;
        while (low < high) { var mid = (low + high + 1) / 2; if (Pieces[mid].Start <= offset) low = mid; else high = mid - 1; }
        return Pieces[low];
    }
    public LogicalPiece AtSource(int source)
    {
        // Source spans overlap for table rows and split blocks; choose the closest actual run.
        var piece = Pieces.Where(p => p.SourceStart <= source && p.SourceEnd >= source)
            .OrderBy(p => p.Runs.Length == 0 ? int.MaxValue : p.Runs.Min(r => Math.Abs(r.SourceStart - source))).FirstOrDefault();
        return piece ?? Pieces.MinBy(p => Math.Abs(p.SourceStart - source))!;
    }
    public static LogicalDocument Build(ParsedDocument parsed, CancellationToken cancellation = default)
    {
        var text = new StringBuilder(); var pieces = new List<LogicalPiece>(); var groups = new List<DetailsGroup>();
        foreach (var block in parsed.Syntax) Walk(block, [], PieceKind.Text, 0, "");
        cancellation.ThrowIfCancellationRequested(); return new(parsed, text.ToString(), [.. pieces], [.. groups]);

        void Walk(Block block, int[] ancestors, PieceKind context, int level, string prefix)
        {
            cancellation.ThrowIfCancellationRequested();
            switch (block)
            {
                case DocumentDetailsBlock details:
                    var group = groups.Count; groups.Add(new(group, details.Span.Start, details.Summary, details.InitiallyExpanded));
                    Emit([new(0, details.Summary, details.Span.Start, details.Span.End)], block, PieceKind.Details, level, ancestors, group: group);
                    foreach (var child in details) Walk(child, [.. ancestors, group], context, level, "");
                    break;
                case ListBlock list:
                    var index = MarkdownFormatting.OrderedStart(list);
                    foreach (var item in list.OfType<ListItemBlock>())
                    {
                        var marker = new string(' ', level * 2) + (list.IsOrdered ? (index++).ToString(CultureInfo.InvariantCulture) + ". " : "• ");
                        var first = true;
                        foreach (var child in item) { Walk(child, ancestors, PieceKind.ListItem, level + 1, first ? marker : ""); first = false; }
                    }
                    break;
                case Table table:
                    foreach (var row in table.OfType<TableRow>())
                    {
                        var runs = new List<TextRun>(); var cells = new List<CellSlice>(); var rowOffset = 0; var column = 0; var first = true;
                        foreach (var cell in row.OfType<TableCell>())
                        {
                            if (!first) { runs.Add(new(0, "\t", cell.Span.Start, cell.Span.Start)); rowOffset++; } first = false;
                            var cellStart = rowOffset;
                            foreach (var child in cell.OfType<LeafBlock>())
                                if (child.Inline is not null)
                                {
                                    var cellRuns = Inline(child.Inline, row.IsHeader ? MarkdownEmphasis.Bold : MarkdownEmphasis.None);
                                    runs.AddRange(cellRuns); rowOffset += cellRuns.Sum(r => r.Text.Length);
                                }
                            cells.Add(new(column, cellStart, rowOffset - cellStart,
                                column < table.ColumnDefinitions.Count ? table.ColumnDefinitions[column].Alignment?.ToString() ?? "Left" : "Left")); column++;
                        }
                        Emit(runs, row, PieceKind.TableRow, level, ancestors, cells: cells.ToArray());
                    }
                    break;
                case AlertBlock alert:
                    Emit([new(0, MarkdownFormatting.CalloutLabel(alert.Kind.ToString()), block.Span.Start, block.Span.End, MarkdownEmphasis.Bold)], block, PieceKind.Callout, level, ancestors);
                    foreach (var child in alert) Walk(child, ancestors, PieceKind.Callout, level, ""); break;
                case QuoteBlock quote:
                    foreach (var child in quote) Walk(child, ancestors, PieceKind.Quote, level + 1, ""); break;
                case HeadingBlock heading:
                    Emit(Inline(heading.Inline), block, PieceKind.Heading, heading.Level, ancestors); break;
                case MathBlock maths:
                    Emit([new(0, maths.Lines.ToString(), block.Span.Start, block.Span.End)], block, PieceKind.Maths, level, ancestors, maths.Lines.ToString()); break;
                case FencedCodeBlock code when code.Info is "mermaid" or "mmd":
                    Emit([new(0, code.Lines.ToString(), block.Span.Start, block.Span.End, Code: true)], block, PieceKind.Diagram, level, ancestors, code.Lines.ToString()); break;
                case CodeBlock code:
                    Emit([new(0, code.Lines.ToString(), block.Span.Start, block.Span.End, Code: true)], block, PieceKind.Code, level, ancestors); break;
                case LeafBlock leaf:
                    var leafRuns = leaf.Inline is null ? new[] { new TextRun(0, leaf.Lines.ToString(), block.Span.Start, block.Span.End) } : Inline(leaf.Inline);
                    if (prefix.Length > 0) leafRuns = [new(0, prefix, block.Span.Start, block.Span.Start), .. leafRuns];
                    var images = leaf.Inline?.Descendants<LinkInline>().Where(l => l.IsImage).ToArray() ?? [];
                    Emit(leafRuns, block, images.Length > 0 ? PieceKind.Image : context, level, ancestors, images.FirstOrDefault()?.Url);
                    break;
                case ContainerBlock container:
                    foreach (var child in container) Walk(child, ancestors, context, level, ""); break;
                default:
                    Emit([new(0, "---", block.Span.Start, block.Span.End)], block, context, level, ancestors); break;
            }
        }
        TextRun[] Inline(ContainerInline? container, MarkdownEmphasis style = MarkdownEmphasis.None, string? link = null)
        {
            if (container is null) return [];
            var runs = new List<TextRun>();
            foreach (var inline in container)
            {
                cancellation.ThrowIfCancellationRequested();
                string? value = inline switch
                {
                    LiteralInline literal => literal.Content.ToString(), HtmlEntityInline entity => entity.Transcoded.ToString(),
                    CodeInline code => code.Content, LineBreakInline line => line.IsHard ? "\n" : " ",
                    DocumentSemanticInline semantic => semantic.Text, DocumentAnchorInline anchor => anchor.Text,
                    AutolinkInline auto => auto.Url, TaskList task => task.Checked ? "☑ " : "☐ ",
                    MathInline maths => maths.Content.ToString(), HtmlInline html => html.Tag,
                    WikiLinkInline wiki => wiki.Label,
                    LinkInline image when image.IsImage => "[" + MarkdownService.InlineText(image) + "]", _ => null
                };
                if (value is not null)
                    runs.Add(new(0, value, inline.Span.Start, inline.Span.End, style,
                        inline is WikiLinkInline wiki ? "wiki:" + wiki.Target : inline is AutolinkInline auto ? auto.Url : link, inline is CodeInline));
                else if (inline is ContainerInline nested)
                    runs.AddRange(Inline(nested, inline is EmphasisInline emphasis ? style | MarkdownFormatting.Emphasis(emphasis) : style,
                        inline is LinkInline hyperlink ? hyperlink.Url : link));
            }
            return [.. runs];
        }
        void Emit(IEnumerable<TextRun> input, Block block, PieceKind kind, int level, int[] ancestors, string? resource = null, int group = -1, CellSlice[]? cells = null)
        {
            var local = new StringBuilder(); var full = new List<TextRun>();
            foreach (var run in input) { full.Add(run with { Start = local.Length }); local.Append(run.Text); }
            local.Append('\n');
            var value = local.ToString(); var boundaries = StringInfo.ParseCombiningCharacters(value);
            var position = 0;
            while (position < value.Length)
            {
                cancellation.ThrowIfCancellationRequested();
                var end = Math.Min(value.Length, position + PieceCharacters);
                if (end < value.Length)
                {
                    var boundary = Array.BinarySearch(boundaries, end); end = boundaries[boundary >= 0 ? boundary : Math.Max(0, ~boundary - 1)];
                    // A pathological combining sequence is retained as one grapheme; explicitly measured by the stress harness.
                    if (end <= position) end = boundaries.FirstOrDefault(b => b > position, value.Length);
                }
                var selected = full.Where(r => r.Start < end && r.Start + r.Text.Length > position).Select(r =>
                {
                    var start = Math.Max(position, r.Start); var length = Math.Min(end, r.Start + r.Text.Length) - start;
                    var exact = r.SourceEnd - r.SourceStart + 1 == r.Text.Length;
                    return r with { Start = text.Length + start - position, Text = r.Text.Substring(start - r.Start, length),
                        SourceStart = exact ? r.SourceStart + start - r.Start : r.SourceStart,
                        SourceEnd = exact ? r.SourceStart + start - r.Start + length - 1 : r.SourceEnd };
                }).ToArray();
                pieces.Add(new(pieces.Count, text.Length, end - position, block.Span.Start, block.Span.End, kind, level, ancestors, selected,
                    position == 0 ? resource : null, group)
                {
                    TableColumns = cells?.Length ?? 0,
                    Cells = cells?.Where(c => c.Start < end && c.Start + c.Length >= position).Select(c => c with
                    { Start = text.Length + Math.Max(c.Start, position) - position, Length = Math.Max(0, Math.Min(c.Start + c.Length, end) - Math.Max(c.Start, position)) }).ToArray() ?? []
                });
                text.Append(value, position, end - position); position = end;
            }
        }
    }
}
