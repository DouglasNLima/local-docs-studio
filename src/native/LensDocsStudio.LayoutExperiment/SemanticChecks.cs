using System.Windows.Controls;
using System.Windows.Documents;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.LayoutExperiment;

/// <summary>Independent fixture invariants outside timing; visual and full parity remain separate.</summary>
internal static class SemanticChecks
{
    public static object Check(string adapter, string fixture, string source, FlowDocumentScrollViewer? stock, PreviewResult? result, LayoutSnapshot? snapshot)
    {
        string[] required = fixture switch
        {
            "table" => ["Row1", "Value1", "Row1000", "Value1000"],
            "paragraphs" => ["Paragraph1:", "Evidence", "& review remains selectable.", "Paragraph1200:"],
            "quote" => ["Quoted1", "Quoted1200"], "details" => ["Large section", "Hidden1", "Hidden1200"],
            "mixed" => ["Mixed document", "Mixed item1000", "Value300", "Outer evidence", "Nested evidence", "Quoted300", "Café 😀 é", "Deep target", "Mixed final marker."],
            "list" => ["word1", "word4000", "Fresh revision", "Changed distant content Café 😀."],
            _ => throw new ArgumentException("Unknown fixture.")
        };
        var text = adapter == "candidate" ? snapshot!.Model.Text : result!.ReadText();
        foreach (var value in required) if (!text.Contains(value, StringComparison.Ordinal)) throw new IOException(adapter + " missing actual fixture text: " + value);
        if (adapter == "candidate")
        {
            var model = snapshot!.Model;
            if (model.Source != source || model.SourceBoundaries.Length != text.Length + 1 || model.SourceEnds.Length != text.Length)
                throw new IOException("Complete source ownership failed.");
            for (var index = 0; index < model.Items.Length; index++)
                if (model.Items[index].Length > 0 && !snapshot.Lines.Any(line => line.Item == index && line.End == model.Items[index].Start + model.Items[index].Length))
                    throw new IOException("An off-screen paragraph/cell lacks complete end geometry.");
            var nodes = model.Root.Descendants().ToArray();
            return new { LogicalText = text, snapshot.Height, RequiredText = "PASS", CompleteParagraphCellGeometry = "PASS", FullParity = "INCOMPLETE",
                Structure = nodes.Select(node => new { node.Id, node.Kind, node.Start, node.End, node.SourceStart, node.SourceEnd, node.Level, node.Ordered, node.Number, node.Target }),
                Styles = model.Items.SelectMany(item => item.Runs).Select(run => new { run.Start, run.Text, run.Bold, run.Italic, run.Code, run.Emphasis, run.SourceStart, run.SourceEnd }),
                Lines = snapshot.Lines.Select(line => new { line.Start, line.End, line.Item, line.Top, line.Height, line.Carets }) };
        }
        var scrolling = (ScrollViewer)stock!.Template.FindName("PART_ContentHost", stock);
        return new { LogicalText = text, Height = scrolling.ExtentHeight, RequiredText = "PASS", FullParity = "INCOMPLETE",
            Paragraphs = Blocks(result!.Document.Blocks).OfType<Paragraph>().Select(paragraph => new { Text = new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text,
                Top = paragraph.ContentStart.GetCharacterRect(LogicalDirection.Forward).Top, Height = paragraph.ContentStart.GetCharacterRect(LogicalDirection.Forward).Height }).ToArray() };
    }
    private static IEnumerable<Block> Blocks(BlockCollection blocks)
    {
        foreach (var block in blocks)
        {
            yield return block;
            IEnumerable<Block> nested = block switch
            {
                Section section => Blocks(section.Blocks),
                System.Windows.Documents.List list => list.ListItems.SelectMany(item => Blocks(item.Blocks)),
                Table table => table.RowGroups.SelectMany(group => group.Rows).SelectMany(row => row.Cells).SelectMany(cell => Blocks(cell.Blocks)),
                _ => []
            };
            foreach (var child in nested) yield return child;
        }
    }
}
