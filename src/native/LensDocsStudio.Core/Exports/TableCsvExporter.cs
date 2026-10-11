using System.Text;
using LensDocsStudio.Core.Parsing;
using Markdig.Extensions.TaskLists;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LensDocsStudio.Core.Exports;

public sealed record ExportedTableCsv(byte[] Bytes, int TableNumber);

/// <summary>Exports the selected table's text without reading linked resources or changing cell values.</summary>
public sealed class TableCsvExporter
{
    public const int MaximumSourceCharacters = 4 * 1024 * 1024;
    public const int MaximumBytes = 16 * 1024 * 1024;
    public const int MaximumCells = 100_000;

    public ExportedTableCsv Export(string source, ExportTableSelection selection, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (source.Length > MaximumSourceCharacters) throw new IOException("Table CSV export supports documents up to 4 million characters.");
        var document = new MarkdownService().Parse(source);
        cancellationToken.ThrowIfCancellationRequested();
        var table = selection.Resolve(document);
        var number = document.Syntax.Descendants<Table>().TakeWhile(candidate => candidate != table).Count() + 1;
        var output = new StringBuilder(); var cells = 0; var rows = 0;
        foreach (var row in table.OfType<TableRow>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (rows++ > 0) Append(output, "\r\n");
            var column = 0;
            foreach (var cell in row.OfType<TableCell>())
            {
                if (++cells > MaximumCells) throw new IOException("Table CSV export is limited to 100,000 cells.");
                if (column++ > 0) Append(output, ",");
                var text = new StringBuilder(); ReadBlock(cell, text, 0);
                var normalised = Normalise(text.ToString());
                var quoted = normalised.IndexOfAny([',', '"', '\n']) >= 0;
                if (quoted) Append(output, "\"");
                Append(output, quoted ? normalised.Replace("\"", "\"\"", StringComparison.Ordinal) : normalised);
                if (quoted) Append(output, "\"");
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        var csv = output.ToString();
        if (Encoding.UTF8.GetByteCount(csv) > MaximumBytes) throw new IOException("The table CSV exceeds the 16 MB limit.");
        return new(Encoding.UTF8.GetBytes(csv), number);

        void Append(StringBuilder target, string value)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((long)target.Length + value.Length > MaximumBytes) throw new IOException("The table CSV exceeds the 16 MB limit.");
            target.Append(value);
        }
        void ReadBlock(Block block, StringBuilder text, int depth)
        {
            CheckDepth(depth);
            if (block is LeafBlock { Inline: { } inline }) ReadInlines(inline, text, depth + 1);
            else if (block is LeafBlock leaf) Append(text, leaf.Lines.ToString());
            else if (block is ContainerBlock container)
            {
                var first = true;
                foreach (var child in container)
                {
                    if (!first) Append(text, "\n"); first = false;
                    ReadBlock(child, text, depth + 1);
                }
            }
            else AppendSource(text, block.Span);
        }
        void ReadInlines(ContainerInline container, StringBuilder text, int depth)
        {
            CheckDepth(depth);
            foreach (var inline in container)
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (inline)
                {
                    case DocumentSemanticInline semantic: Append(text, semantic.Text); break;
                    case DocumentAnchorInline anchor: Append(text, anchor.Text); break;
                    case LiteralInline literal: Append(text, literal.Content.ToString()); break;
                    case HtmlEntityInline entity: Append(text, entity.Transcoded.ToString()); break;
                    case CodeInline code: Append(text, code.Content); break;
                    case LineBreakInline line: Append(text, line.IsHard ? "\n" : " "); break;
                    case AutolinkInline link: Append(text, link.Url); break;
                    case TaskList task: Append(text, task.Checked ? "[x] " : "[ ] "); break;
                    // Images contribute their alt text; no URL is opened and missing files cannot block CSV.
                    case ContainerInline nested: ReadInlines(nested, text, depth + 1); break;
                    case HtmlInline html: Append(text, html.Tag); break;
                    default: AppendSource(text, inline.Span); break;
                }
            }
        }
        void AppendSource(StringBuilder target, SourceSpan span)
        {
            if (span.Start < 0 || span.Length < 0 || span.End >= source.Length) throw new IOException("A table element has invalid source positions.");
            Append(target, source.Substring(span.Start, span.Length));
        }
        void CheckDepth(int depth)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (depth > 64) throw new IOException("The selected table contains excessively nested content.");
        }
    }

    private static string Normalise(string value) => string.Join('\n', value.Replace('\u00a0', ' ').Replace("\r\n", "\n", StringComparison.Ordinal)
        .Replace('\r', '\n').Split('\n').Select(line => line.Trim(' ', '\t'))).Trim();
}
