using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.LayoutExperiment;

/// <summary>Interchange only. No viewer, text layout, selection control or automation provider.</summary>
internal sealed class ClipboardSerialisation
{
    private readonly LayoutSnapshot snapshot;
    private readonly CancellationToken cancellation;
    private readonly List<(StyledRun Run, Inline Element)> spans = [];
    private readonly List<(Item Item, Block Element)> paragraphs = [];
    private readonly Brush foreground, surface, link;
    private ClipboardSerialisation(LayoutSnapshot snapshot, CancellationToken cancellation)
    {
        this.snapshot = snapshot; this.cancellation = cancellation;
        var hc = snapshot.Appearance.StartsWith("HC:", StringComparison.Ordinal); var dark = snapshot.Appearance == "Dark";
        foreground = Brush(hc ? ExperimentAppearance.Slot(snapshot.Appearance, 0) : dark ? "#E8EAED" : "#202124");
        surface = Brush(hc ? ExperimentAppearance.Slot(snapshot.Appearance, 1) : dark ? "#292B2F" : "#F3F4F6");
        link = Brush(hc ? ExperimentAppearance.Slot(snapshot.Appearance, 5) : dark ? "#87CEFA" : "#4169E1");
    }
    private static Brush Brush(string value) { var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)); brush.Freeze(); return brush; }
    public static SelectionClipboard.Payload Prepare(LayoutSnapshot snapshot, int start, int end, CancellationToken cancellation)
    {
        var watch = Stopwatch.StartNew(); var allocated = GC.GetAllocatedBytesForCurrentThread();
        var builder = new ClipboardSerialisation(snapshot, cancellation);
        var document = new FlowDocument { FontFamily = new FontFamily("Segoe UI"), FontSize = 15, Foreground = builder.foreground,
            PagePadding = new Thickness(28), Language = XmlLanguage.GetLanguage(TextCulture.Value.IetfLanguageTag) };
        foreach (var child in snapshot.Model.Root.Children) { cancellation.ThrowIfCancellationRequested(); document.Blocks.Add(builder.Block(child)); }
        if (document.ContentStart.HasValidLayout || document.ContentEnd.HasValidLayout) throw new IOException("Clipboard semantic tree unexpectedly has layout before serialisation.");
        var range = start == 0 && end == snapshot.Model.Text.Length ? new TextRange(document.ContentStart, document.ContentEnd)
            : new TextRange(builder.Pointer(start, false, document), builder.Pointer(end, true, document));
        cancellation.ThrowIfCancellationRequested();
        using var rtf = new MemoryStream(); range.Save(rtf, DataFormats.Rtf);
        using var xaml = new MemoryStream(); range.Save(xaml, DataFormats.Xaml);
        using var package = new MemoryStream();
        var containsMedia = snapshot.Model.Root.Descendants().Any(node => node.Media is not null);
        if (containsMedia) range.Save(package, DataFormats.XamlPackage);
        if (document.ContentStart.HasValidLayout || document.ContentEnd.HasValidLayout) throw new IOException("Clipboard serialisation performed forbidden text layout.");
        cancellation.ThrowIfCancellationRequested();
        var text = range.Text;
        return new(text, Read(rtf), Read(xaml))
        {
            XamlPackage = containsMedia ? package.ToArray().ToImmutableArray() : [],
            Serialisation = new { Kind = "Public WPF interchange serializer; transient unformatted tree", HasValidLayoutBefore = false, HasValidLayoutAfter = false,
                Thread = Environment.CurrentManagedThreadId, Milliseconds = watch.Elapsed.TotalMilliseconds, AllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated }
        };
    }
    private static string Read(MemoryStream stream) { stream.Position = 0; return new StreamReader(stream, Encoding.UTF8, true, leaveOpen: true).ReadToEnd(); }
    private TextPointer Pointer(int offset, bool ending, FlowDocument document)
    {
        var candidates = spans.Where(value => offset >= value.Run.Start && offset <= value.Run.Start + value.Run.Text.Length).ToArray();
        if (candidates.Length > 0)
        { var value = ending ? candidates[0] : candidates[^1]; if (value.Element is LineBreak) return offset > value.Run.Start ? value.Element.ElementEnd : value.Element.ElementStart; return value.Element.ContentStart.GetPositionAtOffset(offset - value.Run.Start)!; }
        var embedded = paragraphs.FirstOrDefault(value => value.Element is BlockUIContainer && offset >= value.Item.Start && offset <= value.Item.Start + value.Item.Length);
        if (embedded.Element is not null) return offset > embedded.Item.Start ? embedded.Element.ContentEnd : embedded.Element.ContentStart;
        var next = paragraphs.FirstOrDefault(value => value.Item.Start >= offset);
        if (next.Element is not null) return next.Element.ContentStart;
        return document.ContentEnd;
    }
    private Block Block(SemanticNode node)
    {
        cancellation.ThrowIfCancellationRequested();
        switch (node.Kind)
        {
            case "Media":
                var panel = new StackPanel { Margin = new Thickness(0, 6, 0, 12) };
                panel.Children.Add(new Image { Source = node.Media, Stretch = Stretch.Uniform, MaxHeight = 900, HorizontalAlignment = HorizontalAlignment.Left });
                var actions = new WrapPanel();
                foreach (var label in new[] { "Zoom", "SVG", "PNG", "Copy source" }) actions.Children.Add(new Button { Content = label, Margin = new Thickness(0, 4, 8, 0), Padding = new Thickness(8, 3, 8, 3) });
                panel.Children.Add(actions); var host = new BlockUIContainer(panel); paragraphs.Add((snapshot.Model.Items[node.Paragraph], host)); return host;
            case "List":
                var list = new System.Windows.Documents.List { MarkerStyle = node.Ordered ? node.Number == 0 ? TextMarkerStyle.None : TextMarkerStyle.Decimal : TextMarkerStyle.Disc, Padding = new Thickness(24, 0, 0, 0), StartIndex = Math.Max(1, node.Number) };
                foreach (var child in node.Children) { var item = new ListItem(); foreach (var block in child.Children) item.Blocks.Add(Block(block)); list.ListItems.Add(item); }
                return list;
            case "Table":
                var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 8, 0, 12) }; table.BeginInit(); var group = new TableRowGroup(); table.RowGroups.Add(group);
                try
                {
                    foreach (var row in node.Children)
                    {
                        var output = new TableRow(); group.Rows.Add(output);
                        foreach (var cell in row.Children)
                        {
                            var value = new TableCell { Padding = new Thickness(8, 6, 8, 6), BorderThickness = new Thickness(.5), BorderBrush = Brushes.Gray, TextAlignment = cell.Alignment };
                            if (row.Level == 1) value.FontWeight = FontWeights.SemiBold;
                            foreach (var child in cell.Children) value.Blocks.Add(Block(child)); output.Cells.Add(value);
                        }
                    }
                }
                finally { table.EndInit(); }
                return table;
            case "Quote":
                var quote = new Section { BorderBrush = Brush("#FF883E"), BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(12, 4, 4, 4) };
                foreach (var child in node.Children) quote.Blocks.Add(Block(child)); return quote;
            case "Details":
                var details = new Section { BorderBrush = surface, BorderThickness = new Thickness(1), Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 8, 0, 12) };
                foreach (var child in node.Children) details.Blocks.Add(Block(child)); return details;
            default: return Paragraph(node);
        }
    }
    private Paragraph Paragraph(SemanticNode node)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 4, 0, 10), LineHeight = 23 };
        if (node.Kind == "Heading") { paragraph.FontSize = node.Level switch { 1 => 30, 2 => 24, 3 => 20, _ => 17 }; paragraph.FontWeight = FontWeights.SemiBold; paragraph.Margin = new(0, 16, 0, 8); }
        if (node.Kind == "DetailsHeader") { paragraph.FontWeight = FontWeights.SemiBold; paragraph.Margin = new(0, 0, 0, 4); }
        if (node.Kind == "Code") { paragraph.FontFamily = new("Consolas"); paragraph.FontSize = 13; paragraph.Background = surface; paragraph.Padding = new(12); }
        var item = snapshot.Model.Items[node.Paragraph]; paragraphs.Add((item, paragraph));
        foreach (var run in item.Runs)
        {
            cancellation.ThrowIfCancellationRequested();
            if (run.Text == "\r\n" && !run.Code) { var line = new LineBreak(); paragraph.Inlines.Add(line); spans.Add((run, line)); continue; }
            var media = node.Kind == "Media" ? node : node.Children.FirstOrDefault(child => child.Start == run.Start && child.Media is not null);
            if (media?.Kind == "Toggle")
            {
                var details = snapshot.Model.Root.Descendants().First(value => value.Kind == "Details" && value.Children[0].Children.Any(child => child.Id == media.Id));
                var toggle = new DetailsToggle { IsExpanded = details.Expanded, Foreground = foreground };
                System.Windows.Automation.AutomationProperties.SetName(toggle, details.Label);
                paragraph.Inlines.Add(new InlineUIContainer(toggle) { BaselineAlignment = BaselineAlignment.Center }); continue;
            }
            if (media?.Media is { } image)
            {
                var rectangle = snapshot.Lines.SelectMany(line => line.Clusters).Where(cluster => cluster.Start <= run.Start && cluster.End > run.Start).SelectMany(cluster => cluster.Bounds).Aggregate(Rect.Empty, Rect.Union);
                paragraph.Inlines.Add(new InlineUIContainer(new Image { Source = image, Width = rectangle.Width, Height = rectangle.Height, Stretch = Stretch.Uniform })); continue;
            }
            var value = new Run(run.Text) { FontFamily = new(run.Code ? "Consolas" : "Segoe UI"), FontSize = run.FontSize,
                FontWeight = FontWeight.FromOpenTypeWeight(run.Weight), FontStyle = run.Italic ? FontStyles.Italic : FontStyles.Normal,
                Foreground = run.Link is not null ? link : run.Colour is not null && !snapshot.Appearance.StartsWith("HC:", StringComparison.Ordinal) ? Brush("#" + run.Colour) : foreground };
            if (run.Code && node.Kind != "Code" || run.Emphasis.HasFlag(MarkdownEmphasis.Highlight)) value.Background = surface;
            if (run.Emphasis.HasFlag(MarkdownEmphasis.Subscript)) value.BaselineAlignment = BaselineAlignment.Subscript;
            if (run.Emphasis.HasFlag(MarkdownEmphasis.Superscript)) value.BaselineAlignment = BaselineAlignment.Superscript;
            var decorations = new TextDecorationCollection();
            if (run.Emphasis.HasFlag(MarkdownEmphasis.Underline)) foreach (var decoration in System.Windows.TextDecorations.Underline) decorations.Add(decoration);
            if (run.Emphasis.HasFlag(MarkdownEmphasis.Strike)) foreach (var decoration in System.Windows.TextDecorations.Strikethrough) decorations.Add(decoration);
            value.TextDecorations = decorations;
            if (run.Link is not null) { var hyperlink = new Hyperlink(value) { Foreground = link, ToolTip = run.Link }; paragraph.Inlines.Add(hyperlink); }
            else paragraph.Inlines.Add(value);
            spans.Add((run, value));
        }
        return paragraph;
    }
}
