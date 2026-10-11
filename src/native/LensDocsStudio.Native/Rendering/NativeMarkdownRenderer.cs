using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Diagnostics;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Rendering;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Extensions.Alerts;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MdBlock = Markdig.Syntax.Block;
using WpfBlock = System.Windows.Documents.Block;
using WpfTable = System.Windows.Documents.Table;

namespace LensDocsStudio.Native.Rendering;

public sealed record SourceReference(int Start, int End, int Line);
public sealed record PreviewResult(FlowDocument Document, IReadOnlyDictionary<int, TextElement> SourceElements, IReadOnlyList<string> Warnings);

public sealed class NativeMarkdownRenderer(NativeSvgService svg)
{
    public PreviewResult Render(ParsedDocument parsed, bool dark,
        IReadOnlyDictionary<FencedCodeBlock, DiagramResult> diagrams, Action<string> navigate, Action<string, string> exportDiagram,
        IReadOnlyDictionary<FencedCodeBlock, DrawingImage>? preparedDrawings = null, Action<BitmapSource>? copyImage = null,
        IReadOnlyDictionary<string, PreviewImage>? preparedImages = null, Action<PreviewTextCopy>? copyCode = null, PreparedCodeSyntax? codeSyntax = null, Action<ExportTableSelection>? copyTable = null, Action<ExportTableSelection>? exportTableCsv = null)
        => RenderCoreAsync(parsed, dark, diagrams, navigate, exportDiagram, preparedDrawings, copyImage, preparedImages, false, default, copyCode, codeSyntax, copyTable, exportTableCsv).GetAwaiter().GetResult();

    public Task<PreviewResult> RenderAsync(ParsedDocument parsed, bool dark,
        IReadOnlyDictionary<FencedCodeBlock, DiagramResult> diagrams, Action<string> navigate, Action<string, string> exportDiagram,
        IReadOnlyDictionary<FencedCodeBlock, DrawingImage>? preparedDrawings = null, Action<BitmapSource>? copyImage = null,
        IReadOnlyDictionary<string, PreviewImage>? preparedImages = null, CancellationToken cancellationToken = default, Action<PreviewTextCopy>? copyCode = null, PreparedCodeSyntax? codeSyntax = null, Action<ExportTableSelection>? copyTable = null, Action<ExportTableSelection>? exportTableCsv = null)
        => RenderCoreAsync(parsed, dark, diagrams, navigate, exportDiagram, preparedDrawings, copyImage, preparedImages, true, cancellationToken, copyCode, codeSyntax, copyTable, exportTableCsv);

    private async Task<PreviewResult> RenderCoreAsync(ParsedDocument parsed, bool dark,
        IReadOnlyDictionary<FencedCodeBlock, DiagramResult> diagrams, Action<string> navigate, Action<string, string> exportDiagram,
        IReadOnlyDictionary<FencedCodeBlock, DrawingImage>? preparedDrawings, Action<BitmapSource>? copyImage,
        IReadOnlyDictionary<string, PreviewImage>? preparedImages, bool incremental, CancellationToken cancellationToken, Action<PreviewTextCopy>? copyCode, PreparedCodeSyntax? codeSyntax, Action<ExportTableSelection>? copyTable, Action<ExportTableSelection>? exportTableCsv)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Brush foreground = SystemParameters.HighContrast ? SystemColors.WindowTextBrush : new SolidColorBrush(dark ? Color.FromRgb(232, 234, 237) : Color.FromRgb(32, 33, 36));
        Brush surface = SystemParameters.HighContrast ? SystemColors.WindowBrush : new SolidColorBrush(dark ? Color.FromRgb(41, 43, 47) : Color.FromRgb(243, 244, 246));
        var document = new FlowDocument { FontFamily = new FontFamily("Segoe UI"), FontSize = 15, Foreground = foreground, Background = Brushes.Transparent,
            PagePadding = new Thickness(28), ColumnWidth = double.PositiveInfinity };
        var sourceElements = new Dictionary<int, TextElement>();
        var warnings = new List<string>();
        if (codeSyntax?.Limited == true) warnings.Add("Some code blocks are shown without syntax colouring because preview limits were reached.");
        var batch = Stopwatch.StartNew(); var blocks = 0;
        foreach (var block in parsed.Syntax)
        {
            document.Blocks.Add(Convert(block));
            if (incremental && (++blocks % 32 == 0 || batch.ElapsedMilliseconds >= 8))
            {
                // Keep the old preview visible while native objects are prepared in small batches.
                // Input, cancellation and newer source edits can run between these batches.
                await Dispatcher.Yield(DispatcherPriority.Background);
                cancellationToken.ThrowIfCancellationRequested(); batch.Restart();
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new(document, sourceElements, warnings);

        WpfBlock Convert(MdBlock block)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WpfBlock result;
            switch (block)
            {
                case HeadingBlock heading:
                    var hp = Paragraph(heading.Inline);
                    hp.FontSize = heading.Level switch { 1 => 30, 2 => 24, 3 => 20, _ => 17 };
                    hp.FontWeight = FontWeights.SemiBold; hp.Margin = new Thickness(0, 16, 0, 8); result = hp;
                    break;
                case ParagraphBlock paragraph: result = Paragraph(paragraph.Inline); break;
                case FencedCodeBlock code when diagrams.TryGetValue(code, out var diagram):
                    var panel = new StackPanel { Margin = new Thickness(0, 6, 0, 12) };
                    if (diagram.Success)
                    {
                        try
                        {
                            var drawing = preparedDrawings is not null && preparedDrawings.TryGetValue(code, out var prepared) ? prepared : svg.Render(diagram.Svg!);
                            var image = new Image { Source = drawing, Stretch = Stretch.Uniform, MaxHeight = 900, HorizontalAlignment = HorizontalAlignment.Left };
                            System.Windows.Automation.AutomationProperties.SetName(image, "Mermaid diagram");
                            panel.Children.Add(image);
                            var actions = new StackPanel { Orientation = Orientation.Horizontal };
                            foreach (var format in new[] { "Zoom", "SVG", "PNG", "Copy source" })
                            {
                                var button = new Button { Content = format, Margin = new Thickness(0, 4, 8, 0), Padding = new Thickness(8, 3, 8, 3) };
                                System.Windows.Automation.AutomationProperties.SetAutomationId(button, "DiagramAction" + format.Replace(" ", "", StringComparison.Ordinal));
                                button.Click += (_, _) => exportDiagram(format, format == "Copy source" ? diagram.Source : diagram.Svg!);
                                actions.Children.Add(button);
                            }
                            panel.Children.Add(actions);
                        }
                        catch (Exception ex) when (ex is not OutOfMemoryException)
                        {
                            warnings.Add("Native diagram rendering failed: " + ex.Message);
                            panel.Children.Add(new TextBlock { Text = warnings[^1], TextWrapping = TextWrapping.Wrap, Foreground = foreground });
                        }
                    }
                    else
                    {
                        warnings.Add(diagram.Error!);
                        panel.Children.Add(new TextBlock { Text = diagram.Error, TextWrapping = TextWrapping.Wrap, Foreground = foreground });
                    }
                    result = new BlockUIContainer(panel); break;
                case CodeBlock code:
                    var codeParagraph = new Paragraph { FontFamily = new FontFamily("Consolas"), FontSize = 13, Background = surface, Padding = new Thickness(12) };
                    CodeSyntaxBlock? syntax = null; codeSyntax?.Blocks.TryGetValue(code, out syntax);
                    CodeSyntaxColours.Add(codeParagraph.Inlines, code.Lines.ToString(), syntax, dark, SystemParameters.HighContrast);
                    System.Windows.Automation.AutomationProperties.SetAutomationId(codeParagraph, "CodePreview" + code.Span.Start);
                    result = codeParagraph;
                    result.ContextMenu = CodeContextMenu.Create(code, parsed.Source, copyCode);
                    break;
                case AlertBlock alert:
                    var callout = new Section { BorderBrush = new SolidColorBrush(Color.FromRgb(255, 136, 62)), BorderThickness = new Thickness(3, 0, 0, 0),
                        Background = surface, Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 8, 0, 12) };
                    var label = MarkdownFormatting.CalloutLabel(alert.Kind.ToString());
                    callout.Blocks.Add(new Paragraph(new Run(label)) { FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
                    System.Windows.Automation.AutomationProperties.SetName(callout, label + " callout");
                    foreach (var child in alert) callout.Blocks.Add(Convert(child));
                    result = callout; break;
                case QuoteBlock quote:
                    var section = new Section { BorderBrush = new SolidColorBrush(Color.FromRgb(255, 136, 62)), BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(12, 4, 4, 4) };
                    foreach (var child in quote) section.Blocks.Add(Convert(child)); result = section;
                    break;
                case ListBlock list:
                    var wl = new System.Windows.Documents.List { MarkerStyle = list.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc, Padding = new Thickness(24, 0, 0, 0) };
                    var orderedStart = MarkdownFormatting.OrderedStart(list);
                    if (list.IsOrdered) { wl.StartIndex = Math.Max(1, orderedStart); if (orderedStart == 0) wl.MarkerStyle = TextMarkerStyle.None; }
                    foreach (var item in list.OfType<ListItemBlock>())
                    {
                        var li = new ListItem();
                        foreach (var child in item) li.Blocks.Add(Convert(child));
                        if (list.IsOrdered && wl.MarkerStyle == TextMarkerStyle.None)
                        {
                            if (li.Blocks.FirstBlock is not Paragraph first)
                            { first = new Paragraph(); if (li.Blocks.FirstBlock is { } firstBlock) li.Blocks.InsertBefore(firstBlock, first); else li.Blocks.Add(first); }
                            var marker = new Run(orderedStart++ + ". ");
                            if (first.Inlines.FirstInline is { } initial) first.Inlines.InsertBefore(initial, marker); else first.Inlines.Add(marker);
                        }
                        wl.ListItems.Add(li);
                    }
                    result = wl; break;
                case Markdig.Extensions.Tables.Table table:
                    var wt = new WpfTable { CellSpacing = 0, Margin = new Thickness(0, 8, 0, 12) };
                    wt.ContextMenu = TableContextMenu.Create(table, parsed.Source, copyTable, exportTableCsv);
                    var group = new TableRowGroup(); wt.RowGroups.Add(group);
                    foreach (var row in table.OfType<Markdig.Extensions.Tables.TableRow>())
                    {
                        var wr = new System.Windows.Documents.TableRow { Background = row.IsHeader ? surface : Brushes.Transparent };
                        var column = 0;
                        foreach (var cell in row.OfType<Markdig.Extensions.Tables.TableCell>())
                        {
                            var wc = new System.Windows.Documents.TableCell { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(.5), Padding = new Thickness(8, 6, 8, 6) };
                            wc.ColumnSpan = Math.Clamp(cell.ColumnSpan, 1, 1000); wc.RowSpan = Math.Clamp(cell.RowSpan, 1, 1000);
                            var columnIndex = cell.ColumnIndex >= 0 ? cell.ColumnIndex : column;
                            column += Math.Max(1, cell.ColumnSpan);
                            wc.TextAlignment = columnIndex < table.ColumnDefinitions.Count ? table.ColumnDefinitions[columnIndex].Alignment switch
                            { TableColumnAlign.Center => TextAlignment.Center, TableColumnAlign.Right => TextAlignment.Right, _ => TextAlignment.Left } : TextAlignment.Left;
                            if (row.IsHeader) wc.FontWeight = FontWeights.SemiBold;
                            foreach (var child in cell) wc.Blocks.Add(Convert(child));
                            wr.Cells.Add(wc);
                        }
                        group.Rows.Add(wr);
                    }
                    result = wt; break;
                case ThematicBreakBlock:
                    result = new Paragraph { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0, 0, 0, 1), Margin = new Thickness(0, 12, 0, 12) }; break;
                case DocumentImageFigureBlock figure:
                    var figureSection = new Section { Margin = new Thickness(0, 8, 0, 12) };
                    figureSection.Blocks.Add(Paragraph(figure.Inline));
                    if (figure.Caption.Length > 0) figureSection.Blocks.Add(new Paragraph(new Run(figure.Caption)) { FontSize = 13, Margin = new Thickness(0, 0, 0, 4) });
                    result = figureSection; break;
                case DocumentProgressBlock progress:
                    var progressSection = new Section { Margin = new Thickness(0, 8, 0, 12) };
                    progressSection.Blocks.Add(new Paragraph(new Run(progress.Description)) { Margin = new Thickness(0, 0, 0, 5) });
                    var bar = new ProgressBar { Minimum = 0, Maximum = progress.Maximum, Value = progress.Value, Height = 12,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + progress.ColourHex)), Background = surface,
                        IsIndeterminate = false, IsTabStop = false };
                    System.Windows.Automation.AutomationProperties.SetName(bar, progress.Description);
                    System.Windows.Automation.AutomationProperties.SetAutomationId(bar, "DocumentProgress" + progress.Span.Start);
                    progressSection.Blocks.Add(new BlockUIContainer(bar)); result = progressSection; break;
                case HtmlBlock html:
                    warnings.Add("Embedded HTML is shown as source until its native semantic rendering is supported.");
                    result = new Paragraph(new Run(html.Lines.ToString())) { FontFamily = new FontFamily("Consolas"), FontSize = 12, Background = surface }; break;
                case ContainerBlock container:
                    var nested = new Section();
                    foreach (var child in container) nested.Blocks.Add(Convert(child)); result = nested; break;
                default: result = new Paragraph(new Run(parsed.Source.Substring(Math.Clamp(block.Span.Start, 0, parsed.Source.Length), Math.Clamp(block.Span.Length, 0, parsed.Source.Length - Math.Clamp(block.Span.Start, 0, parsed.Source.Length))))); break;
            }
            result.Tag = new SourceReference(block.Span.Start, block.Span.End, block.Line);
            sourceElements[block.Span.Start] = result;
            return result;
        }

        Paragraph Paragraph(ContainerInline? inline)
        {
            var result = new Paragraph { Margin = new Thickness(0, 4, 0, 10), LineHeight = 23 };
            if (inline is not null) AddInlines(result.Inlines, inline);
            return result;
        }
        void AddInlines(InlineCollection target, ContainerInline container)
        {
            foreach (var inline in container)
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (inline)
                {
                    case DocumentAnchorInline anchor:
                        var anchorSpan = new Span(new Run(anchor.Text)) { Tag = new SourceReference(anchor.Span.Start, anchor.Span.End, anchor.Line) };
                        System.Windows.Automation.AutomationProperties.SetName(anchorSpan, anchor.Text.Length == 0 ? "Anchor: " + anchor.Id : anchor.Text);
                        sourceElements[anchor.Span.Start] = anchorSpan;
                        target.Add(anchorSpan); break;
                    case DocumentSemanticInline semantic:
                        var passive = new Span(new Run(semantic.Text)) { Background = surface,
                            Tag = new SourceReference(semantic.Span.Start, semantic.Span.End, semantic.Line) };
                        if (semantic.Kind == DocumentInlineKind.StatusBadge) { passive.FontWeight = FontWeights.SemiBold; passive.ToolTip = "Status: " + semantic.Text; }
                        else { passive.FontFamily = new FontFamily("Consolas"); passive.ToolTip = "Keyboard shortcut: " + semantic.Text; }
                        System.Windows.Automation.AutomationProperties.SetName(passive, semantic.Text);
                        target.Add(passive); break;
                    case LiteralInline literal: target.Add(new Run(literal.Content.ToString())); break;
                    case HtmlEntityInline entity: target.Add(new Run(entity.Transcoded.ToString())); break;
                    case CodeInline code: target.Add(new Run(code.Content) { FontFamily = new FontFamily("Consolas"), Background = surface }); break;
                    case LineBreakInline line: if (line.IsHard) target.Add(new LineBreak()); else target.Add(new Run(" ")); break;
                    case EmphasisInline emphasis:
                        var formatting = MarkdownFormatting.Emphasis(emphasis);
                        var span = new Span();
                        if (formatting.HasFlag(MarkdownEmphasis.Bold)) span.FontWeight = FontWeights.Bold;
                        if (formatting.HasFlag(MarkdownEmphasis.Italic)) span.FontStyle = FontStyles.Italic;
                        if (formatting.HasFlag(MarkdownEmphasis.Strike)) span.TextDecorations = TextDecorations.Strikethrough;
                        if (formatting.HasFlag(MarkdownEmphasis.Underline)) span.TextDecorations = TextDecorations.Underline;
                        if (formatting.HasFlag(MarkdownEmphasis.Subscript)) { span.BaselineAlignment = BaselineAlignment.Subscript; span.FontSize = 11; }
                        if (formatting.HasFlag(MarkdownEmphasis.Superscript)) { span.BaselineAlignment = BaselineAlignment.Superscript; span.FontSize = 11; }
                        if (formatting.HasFlag(MarkdownEmphasis.Highlight)) span.Background = surface;
                        AddInlines(span.Inlines, emphasis); target.Add(span); break;
                    case TaskList task: target.Add(new Run(task.Checked ? "☑ " : "☐ ")); break;
                    case LinkInline link when link.IsImage:
                        var alt = MarkdownService.InlineText(link);
                        try
                        {
                            if (preparedImages is null || !preparedImages.TryGetValue(link.Url ?? "", out var preparedImage)) throw new IOException("The image was not prepared for preview.");
                            var bitmap = preparedImage.Pixels ?? throw new IOException(preparedImage.Error ?? "The image could not be prepared.");
                            var image = new Image { Source = bitmap, Stretch = Stretch.Uniform, MaxWidth = 720, MaxHeight = 720, ToolTip = alt, Focusable = true };
                            System.Windows.Automation.AutomationProperties.SetName(image, alt.Length == 0 ? "Document image" : alt);
                            System.Windows.Automation.AutomationProperties.SetAutomationId(image, "DocumentImage" + link.Span.Start);
                            var menu = new ContextMenu();
                            var copy = new MenuItem { Header = "Copy image", IsEnabled = copyImage is not null };
                            System.Windows.Automation.AutomationProperties.SetAutomationId(copy, "CopyDocumentImage");
                            copy.Click += (_, _) => copyImage?.Invoke(bitmap);
                            menu.Items.Add(copy); image.ContextMenu = menu;
                            target.Add(new InlineUIContainer(image));
                        }
                        catch (Exception ex) when (ex is IOException or NotSupportedException or ArgumentException or System.Runtime.InteropServices.COMException)
                        { target.Add(new Run("[Image: " + alt + "]")); warnings.Add(ex.Message); }
                        break;
                    case LinkInline link:
                        var hyperlink = new Hyperlink { Foreground = dark ? Brushes.LightSkyBlue : Brushes.RoyalBlue, ToolTip = link.Url };
                        System.Windows.Automation.AutomationProperties.SetName(hyperlink, MarkdownService.InlineText(link));
                        System.Windows.Automation.AutomationProperties.SetAutomationId(hyperlink, "DocumentLink" + link.Span.Start);
                        AddInlines(hyperlink.Inlines, link);
                        hyperlink.Click += (_, _) => navigate(link.Url ?? "");
                        target.Add(hyperlink); break;
                    case HtmlInline html: target.Add(new Run(html.Tag)); break;
                    case ContainerInline nested: AddInlines(target, nested); break;
                }
            }
        }
    }
}
