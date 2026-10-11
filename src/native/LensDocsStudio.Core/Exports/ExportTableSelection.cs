using LensDocsStudio.Core.Parsing;
using Markdig.Extensions.Tables;
using Markdig.Syntax;

namespace LensDocsStudio.Core.Exports;

public sealed record ExportTableSelection(int Start, int Length)
{
    public Table Resolve(ParsedDocument document)
    {
        if (Start < 0 || Length <= 0 || (long)Start + Length > document.Source.Length)
            throw new IOException("The selected table has invalid source positions.");
        return document.Syntax.Descendants<Table>().FirstOrDefault(table => table.Span.Start == Start && table.Span.Length == Length)
            ?? throw new IOException("The table no longer matches this document. Update the preview and select it again.");
    }
}
