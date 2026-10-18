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
using Markdig.Extensions.Mathematics;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MdBlock = Markdig.Syntax.Block;
using WpfBlock = System.Windows.Documents.Block;
using WpfTable = System.Windows.Documents.Table;

namespace LensDocsStudio.Native.Rendering;

public sealed record SourceReference(int Start, int End, int Line);
public sealed record PreviewDetails(int SourceStart, int SourceEnd, Expander Toggle);
public sealed record PreviewMaths(string Source, InlineUIContainer Element);
public sealed record PreviewMedia(Image Image, double ScrollMaximumHeight, FrameworkElement? Actions = null)
{
    public double ScrollMaximumWidth { get; } = Image.MaxWidth;
    public BlockUIContainer? Host { get; set; }
    public ScrollViewer? PageScroller { get; set; }
}
public sealed record PreviewResult(FlowDocument Document, IReadOnlyDictionary<int, TextElement> SourceElements, IReadOnlyList<string> Warnings)
{
    public PreviewSourceMap TextMap { get; init; } = new([]);
    public IReadOnlyList<PreviewDetails> Details { get; init; } = [];
    public IReadOnlyList<PreviewMaths> Formulae { get; init; } = [];
    public IReadOnlyList<PreviewMedia> Media { get; init; } = [];
    public void RevealSource(int offset)
    { foreach (var section in Details) if (offset >= section.SourceStart && offset <= section.SourceEnd) section.Toggle.IsExpanded = true; }
    public void ExpandDetails() { foreach (var section in Details) section.Toggle.IsExpanded = true; }
    public string ReadText()
    {
        ExpandDetails(); var text = new System.Text.StringBuilder(); var position = Document.ContentStart;
        foreach (var formula in Formulae)
        {
            text.Append(new TextRange(position, formula.Element.ElementStart).Text).Append(formula.Source);
            position = formula.Element.ElementEnd;
        }
        return text.Append(new TextRange(position, Document.ContentEnd).Text).ToString();
    }
}

public sealed class NativeMarkdownRenderer(NativeSvgService svg)
{
    public PreviewResult Render(ParsedDocument parsed, bool dark,
        IReadOnlyDictionary<FencedCodeBlock, DiagramResult> diagrams, Action<string> navigate, Action<string, string> exportDiagram,
        IReadOnlyDictionary<FencedCodeBlock, DrawingImage>? preparedDrawings = null, Action<BitmapSource>? copyImage = null,
        IReadOnlyDictionary<string, PreviewImage>? preparedImages = null, Action<PreviewTextCopy>? copyCode = null, PreparedCodeSyntax? codeSyntax = null, Action<ExportTableSelection>? copyTable = null, Action<ExportTableSelection>? exportTableCsv = null,
        IReadOnlyDictionary<int, PreviewFormula>? preparedFormulae = null, Action<string>? navigateWikiLink = null, Action<int>? editDiagramSource = null, Action<int>? editDiagramVisually = null)
        => RenderCoreAsync(parsed, dark, diagrams, navigate, exportDiagram, preparedDrawings, copyImage, preparedImages, false, default, copyCode, codeSyntax, copyTable, exportTableCsv, preparedFormulae, navigateWikiLink, editDiagramSource, editDiagramVisually).GetAwaiter().GetResult();

    public Task<PreviewResult> RenderAsync(ParsedDocument parsed, bool dark,
        IReadOnlyDictionary<FencedCodeBlock, DiagramResult> diagrams, Action<string> navigate, Action<string, string> exportDiagram,
        IReadOnlyDictionary<FencedCodeBlock, DrawingImage>? preparedDrawings = null, Action<BitmapSource>? copyImage = null,
        IReadOnlyDictionary<string, PreviewImage>? preparedImages = null, CancellationToken cancellationToken = default, Action<PreviewTextCopy>? copyCode = null, PreparedCodeSyntax? codeSyntax = null, Action<ExportTableSelection>? copyTable = null, Action<ExportTableSelection>? exportTableCsv = null,
        IReadOnlyDictionary<int, PreviewFormula>? preparedFormulae = null, Action<string>? navigateWikiLink = null, Action<int>? editDiagramSource = null, Action<int>? editDiagramVisually = null)
        => RenderCoreAsync(parsed, dark, diagrams, navigate, exportDiagram, preparedDrawings, copyImage, preparedImages, true, cancellationToken, copyCode, codeSyntax, copyTable, exportTableCsv, preparedFormulae, navigateWikiLink, editDiagramSource, editDiagramVisually);

    private async Task<PreviewResult> RenderCoreAsync(ParsedDocument parsed, bool dark,
        IReadOnlyDictionary<FencedCodeBlock, DiagramResult> diagrams, Action<string> navigate, Action<string, string> exportDiagram,
        IReadOnlyDictionary<FencedCodeBlock, DrawingImage>? preparedDrawings, Action<BitmapSource>? copyImage,
        IReadOnlyDictionary<string, PreviewImage>? preparedImages, bool incremental, CancellationToken cancellationToken, Action<PreviewTextCopy>? copyCode, PreparedCodeSyntax? codeSyntax, Action<ExportTableSelection>? copyTable, Action<ExportTableSelection>? exportTableCsv,
        IReadOnlyDictionary<int, PreviewFormula>? preparedFormulae, Action<string>? navigateWikiLink, Action<int>? editDiagramSource, Action<int>? editDiagramVisually)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PreviewConstructionEvents.Log.Stage("started");
        var highContrast = SystemParameters.HighContrast;
        Brush foreground = highContrast ? SystemColors.WindowTextBrush : new SolidColorBrush(dark ? Color.FromRgb(232, 234, 237) : Color.FromRgb(32, 33, 36));
        Brush surface = highContrast ? SystemColors.WindowBrush : new SolidColorBrush(dark ? Color.FromRgb(41, 43, 47) : Color.FromRgb(243, 244, 246));
        foreground.Freeze(); surface.Freeze();
        var document = new FlowDocument { FontFamily = new FontFamily("Segoe UI"), FontSize = 15, Foreground = foreground, Background = Brushes.Transparent,
            PagePadding = new Thickness(28), ColumnWidth = double.PositiveInfinity, TextAlignment = TextAlignment.Left };
        var sourceElements = new Dictionary<int, TextElement>();
        var detailSections = new List<PreviewDetails>();
        var formulaSources = new List<PreviewMaths>();
        var media = new List<PreviewMedia>();
        var warnings = new List<string>();
        var literalSpans = new List<PreviewLiteral>();
        var visualTargets = editDiagramVisually is null || parsed.Source.Length > Editor.MermaidDocumentEditSession.MaximumDocumentCharacters
            ? Array.Empty<Core.Mermaid.MermaidBlockSource>() : Core.Mermaid.MermaidBlockSource.Find(parsed, parsed.StandaloneMermaid);
        if (codeSyntax?.Limited == true) warnings.Add("Some code blocks are shown without syntax colouring because preview limits were reached.");
        var batch = Stopwatch.StartNew(); var units = 0;
        PreviewConstructionEvents.Log.Stage("document-ready");
        foreach (var block in parsed.Syntax)
            await Convert(block, document.Blocks);
        cancellationToken.ThrowIfCancellationRequested();
        PreviewConstructionEvents.Log.Stage("completed");
        return new(document, sourceElements, warnings) { TextMap = new(literalSpans), Details = detailSections.AsReadOnly(), Formulae = formulaSources.AsReadOnly(), Media = media.AsReadOnly() };

        Run Literal(MarkdownObject node, string text)
        {
            var run = new Run(text);
            if (LiteralSourceMapping.Locate(parsed.Source, node.Span.Start, node.Span.End, text) is { } span)
                literalSpans.Add(new(run, span));
            return run;
        }

        async ValueTask Cooperate()
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (incremental && (++units % 32 == 0 || batch.ElapsedMilliseconds >= 8))
            {
                // Large lists, tables and nested sections share the same input/cancellation budget.
                // The current preview remains visible until this complete result is ready.
                await Dispatcher.Yield(DispatcherPriority.Background);
                cancellationToken.ThrowIfCancellationRequested(); batch.Restart();
            }
        }

        async ValueTask<WpfBlock> Convert(MdBlock block, BlockCollection? target = null)
        {
            await Cooperate();
            var attached = false;
            // Populate the private document in place. Attaching a completed rich subtree
            // repeats its text-container work synchronously after the cooperative traversal.
            void Attach(WpfBlock value) { target?.Add(value); attached = true; }
            WpfBlock result;
            switch (block)
            {
                case HeadingBlock heading:
                    result = await Paragraph(heading.Inline, target, heading.Level); attached = true;
                    break;
                case ParagraphBlock paragraph: result = await Paragraph(paragraph.Inline, target); attached = true; break;
                case MathBlock formula:
                    result = new Paragraph(Formula(formula, formula.Lines.ToString(), true)) { Margin = new Thickness(0, 12, 0, 12) }; break;
                case FencedCodeBlock code when diagrams.TryGetValue(code, out var diagram):
                    var panel = new StackPanel { Margin = new Thickness(0, 6, 0, 12) };
                    var actions = new WrapPanel { Orientation = Orientation.Horizontal };
                    var diagramFailure = diagram.Error;
                    PreviewMedia? diagramMedia = null;
                    if (diagram.Success)
                    {
                        try
                        {
                            var drawing = preparedDrawings is not null && preparedDrawings.TryGetValue(code, out var prepared) ? prepared : svg.Render(diagram.Svg!);
                            var image = new Image { Source = drawing, Stretch = Stretch.Uniform, MaxHeight = 900, HorizontalAlignment = HorizontalAlignment.Left };
                            System.Windows.Automation.AutomationProperties.SetName(image, "Mermaid diagram");
                            System.Windows.Automation.AutomationProperties.SetAutomationId(image, "DiagramPreview" + code.Span.Start);
                            if (diagram.Theme is { } theme) System.Windows.Automation.AutomationProperties.SetHelpText(image, "Diagram theme: " + theme);
                            panel.Children.Add(image);
                            diagramMedia = new(image, image.MaxHeight, actions); media.Add(diagramMedia);
                        }
                        catch (Exception ex) when (ex is not OutOfMemoryException)
                        {
                            diagramFailure = "Native diagram rendering failed: " + ex.Message;
                        }
                    }
                    if (diagramMedia is null)
                    {
                        diagramFailure ??= "The diagram could not be rendered. Edit its source and try again.";
                        warnings.Add(diagramFailure);
                        var error = new Label { Content = new TextBlock { Text = diagramFailure, TextWrapping = TextWrapping.Wrap },
                            Padding = new Thickness(0), Foreground = foreground };
                        System.Windows.Automation.AutomationProperties.SetName(error, diagramFailure);
                        System.Windows.Automation.AutomationProperties.SetAutomationId(error, "DiagramError" + code.Span.Start);
                        panel.Children.Add(error);
                    }
                    foreach (var format in diagramMedia is null ? new[] { "Copy source" } : new[] { "Zoom", "SVG", "PNG", "Copy source" })
                    {
                        var button = new Button { Content = format, Margin = new Thickness(0, 4, 8, 0), Padding = new Thickness(8, 3, 8, 3) };
                        System.Windows.Automation.AutomationProperties.SetAutomationId(button, diagramMedia is null
                            ? "DiagramErrorCopySource" + code.Span.Start : "DiagramAction" + format.Replace(" ", "", StringComparison.Ordinal));
                        button.Click += (_, _) => exportDiagram(format, format == "Copy source" ? diagram.Source : diagram.Svg!);
                        actions.Children.Add(button);
                    }
                    if (editDiagramVisually is not null && visualTargets.Any(target => code.Span.Start >= target.Container.Start && code.Span.Start < target.Container.End)
                        && (new Core.Mermaid.MermaidSourceParser().Parse(diagram.Source, cancellationToken).DiagramType == "Flowchart"
                            || Core.Sequence.SequenceSourceParser.IsSequence(diagram.Source)
                            || Core.StateDiagrams.StateDiagramSourceParser.IsState(diagram.Source)
                            || Core.ErDiagrams.ErDiagramSourceParser.IsEr(diagram.Source)
                            || Core.ClassDiagrams.ClassDiagramSourceParser.IsClass(diagram.Source)))
                    {
                        var visual = new Button { Content = "Visual Edit", ToolTip = "Open this diagram in its native visual editor.", Margin = new Thickness(0, 4, 8, 0), Padding = new Thickness(8, 3, 8, 3) };
                        System.Windows.Automation.AutomationProperties.SetAutomationId(visual, "DiagramEditVisually" + code.Span.Start);
                        visual.Click += (_, _) => editDiagramVisually(code.Span.Start); actions.Children.Insert(0, visual);
                    }
                    if (editDiagramSource is not null)
                    {
                        var edit = new Button { Content = "Edit source", Margin = new Thickness(0, 4, 8, 0), Padding = new Thickness(8, 3, 8, 3) };
                        System.Windows.Automation.AutomationProperties.SetAutomationId(edit, (diagramMedia is null ? "DiagramErrorEditSource" : "DiagramEditSource") + code.Span.Start);
                        edit.Click += (_, _) => editDiagramSource(code.Span.Start);
                        actions.Children.Add(edit);
                    }
                    panel.Children.Add(actions);
                    var diagramHost = new BlockUIContainer(panel);
                    if (diagramMedia is not null) diagramMedia.Host = diagramHost;
                    result = diagramHost; break;
                case CodeBlock code:
                    var codeParagraph = new Paragraph { FontFamily = new FontFamily("Consolas"), FontSize = 13, Background = surface, Padding = new Thickness(12) };
                    Attach(codeParagraph);
                    CodeSyntaxBlock? syntax = null; codeSyntax?.Blocks.TryGetValue(code, out syntax);
                    await CodeSyntaxColours.AddAsync(codeParagraph.Inlines, code.Lines.ToString(), syntax, dark, highContrast, Cooperate);
                    System.Windows.Automation.AutomationProperties.SetAutomationId(codeParagraph, "CodePreview" + code.Span.Start);
                    result = codeParagraph;
                    result.ContextMenu = CodeContextMenu.Create(code, parsed.Source, copyCode);
                    break;
                case AlertBlock alert:
                    var callout = new Section { BorderBrush = new SolidColorBrush(Color.FromRgb(255, 136, 62)), BorderThickness = new Thickness(3, 0, 0, 0),
                        Background = surface, Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 8, 0, 12) };
                    Attach(callout);
                    var label = MarkdownFormatting.CalloutLabel(alert.Kind.ToString());
                    callout.Blocks.Add(new Paragraph(new Run(label)) { FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
                    System.Windows.Automation.AutomationProperties.SetName(callout, label + " callout");
                    foreach (var child in alert) await Convert(child, callout.Blocks);
                    result = callout; break;
                case QuoteBlock quote:
                    var section = new Section { BorderBrush = new SolidColorBrush(Color.FromRgb(255, 136, 62)), BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(12, 4, 4, 4) };
                    Attach(section);
                    foreach (var child in quote) await Convert(child, section.Blocks); result = section;
                    break;
                case ListBlock list:
                    var wl = new System.Windows.Documents.List { MarkerStyle = list.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc, Padding = new Thickness(24, 0, 0, 0) };
                    var orderedStart = MarkdownFormatting.OrderedStart(list);
                    if (list.IsOrdered) { wl.StartIndex = Math.Max(1, orderedStart); if (orderedStart == 0) wl.MarkerStyle = TextMarkerStyle.None; }
                    Attach(wl);
                    foreach (var item in list.OfType<ListItemBlock>())
                    {
                        await Cooperate();
                        var li = new ListItem();
                        wl.ListItems.Add(li);
                        foreach (var child in item) await Convert(child, li.Blocks);
                        if (list.IsOrdered && wl.MarkerStyle == TextMarkerStyle.None)
                        {
                            if (li.Blocks.FirstBlock is not Paragraph first)
                            { first = new Paragraph(); if (li.Blocks.FirstBlock is { } firstBlock) li.Blocks.InsertBefore(firstBlock, first); else li.Blocks.Add(first); }
                            var marker = new Run(orderedStart++ + ". ");
                            if (first.Inlines.FirstInline is { } initial) first.Inlines.InsertBefore(initial, marker); else first.Inlines.Add(marker);
                        }
                    }
                    result = wl; break;
                case Markdig.Extensions.Tables.Table table:
                    var wt = new WpfTable { CellSpacing = 0, Margin = new Thickness(0, 8, 0, 12) };
                    wt.ContextMenu = TableContextMenu.Create(table, parsed.Source, copyTable, exportTableCsv);
                    Attach(wt);
                    // WPF otherwise revalidates all preceding rows for every cell/row
                    // insertion. This complete private table has one structural commit.
                    wt.BeginInit();
                    try
                    {
                        var group = new TableRowGroup(); wt.RowGroups.Add(group);
                        foreach (var row in table.OfType<Markdig.Extensions.Tables.TableRow>())
                        {
                            await Cooperate();
                            var wr = new System.Windows.Documents.TableRow { Background = row.IsHeader ? surface : Brushes.Transparent };
                            group.Rows.Add(wr);
                            var column = 0;
                            foreach (var cell in row.OfType<Markdig.Extensions.Tables.TableCell>())
                            {
                                await Cooperate();
                                var wc = new System.Windows.Documents.TableCell { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(.5), Padding = new Thickness(8, 6, 8, 6) };
                                wc.ColumnSpan = Math.Clamp(cell.ColumnSpan, 1, 1000); wc.RowSpan = Math.Clamp(cell.RowSpan, 1, 1000);
                                var columnIndex = cell.ColumnIndex >= 0 ? cell.ColumnIndex : column;
                                column += Math.Max(1, cell.ColumnSpan);
                                wc.TextAlignment = columnIndex < table.ColumnDefinitions.Count ? table.ColumnDefinitions[columnIndex].Alignment switch
                                { TableColumnAlign.Center => TextAlignment.Center, TableColumnAlign.Right => TextAlignment.Right, _ => TextAlignment.Left } : TextAlignment.Left;
                                if (row.IsHeader) wc.FontWeight = FontWeights.SemiBold;
                                wr.Cells.Add(wc);
                                foreach (var child in cell) await Convert(child, wc.Blocks);
                            }
                        }
                    }
                    finally
                    {
                        PreviewConstructionEvents.Log.Stage("table-validation-start");
                        wt.EndInit();
                        PreviewConstructionEvents.Log.Stage("table-validation-completed");
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    result = wt; break;
                case ThematicBreakBlock:
                    result = new Paragraph { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0, 0, 0, 1), Margin = new Thickness(0, 12, 0, 12) }; break;
                case DocumentDetailsBlock details:
                    PreviewConstructionEvents.Log.Stage("details-start", details.Span.Start);
                    var detailsSection = new Section { BorderBrush = surface, BorderThickness = new Thickness(1), Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 8, 0, 12) };
                    Attach(detailsSection);
                    PreviewConstructionEvents.Log.Stage("details-container-attached", details.Span.Start);
                    var toggle = new DetailsToggle { IsExpanded = details.InitiallyExpanded, Foreground = foreground, VerticalAlignment = VerticalAlignment.Center };
                    PreviewConstructionEvents.Log.Stage("details-toggle-created", details.Span.Start);
                    System.Windows.Automation.AutomationProperties.SetName(toggle, details.Summary);
                    System.Windows.Automation.AutomationProperties.SetAutomationId(toggle, "DetailsToggle" + details.Span.Start);
                    var detailsHeader = new Paragraph { FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
                    detailsHeader.Inlines.Add(new InlineUIContainer(toggle) { BaselineAlignment = BaselineAlignment.Center });
                    detailsHeader.Inlines.Add(new Run(details.Summary)); detailsSection.Blocks.Add(detailsHeader);
                    PreviewConstructionEvents.Log.Stage("details-header-attached", details.Span.Start);
                    detailSections.Add(new(details.Span.Start, details.Span.End, toggle));
                    // Move one body instead of repeatedly changing the displayed document.
                    // Keep inherited typography stable while this body detaches.
                    var detailsBody = new Section
                    {
                        FontFamily = detailsSection.FontFamily, FontSize = detailsSection.FontSize, Foreground = detailsSection.Foreground,
                        FontStyle = detailsSection.FontStyle, FontWeight = detailsSection.FontWeight, FontStretch = detailsSection.FontStretch
                    };
                    if (details.InitiallyExpanded) detailsSection.Blocks.Add(detailsBody);
                    foreach (var child in details) await Convert(child, detailsBody.Blocks);
                    PreviewConstructionEvents.Log.Stage("details-children-ready", details.Span.Start);
                    void SynchroniseDetails()
                    {
                        if (toggle.IsExpanded && !detailsSection.Blocks.Contains(detailsBody)) detailsSection.Blocks.Add(detailsBody);
                        else if (!toggle.IsExpanded && detailsSection.Blocks.Contains(detailsBody)) detailsSection.Blocks.Remove(detailsBody);
                    }
                    toggle.Expanded += (_, _) => SynchroniseDetails(); toggle.Collapsed += (_, _) => SynchroniseDetails(); SynchroniseDetails();
                    PreviewConstructionEvents.Log.Stage("details-synchronised", details.Span.Start);
                    result = detailsSection; break;
                case DocumentImageFigureBlock figure:
                    var figureSection = new Section { Margin = new Thickness(0, 8, 0, 12) };
                    Attach(figureSection);
                    await Paragraph(figure.Inline, figureSection.Blocks);
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
                    Attach(nested);
                    foreach (var child in container) await Convert(child, nested.Blocks); result = nested; break;
                default: result = new Paragraph(new Run(parsed.Source.Substring(Math.Clamp(block.Span.Start, 0, parsed.Source.Length), Math.Clamp(block.Span.Length, 0, parsed.Source.Length - Math.Clamp(block.Span.Start, 0, parsed.Source.Length))))); break;
            }
            result.Tag = new SourceReference(block.Span.Start, block.Span.End, block.Line);
            sourceElements[block.Span.Start] = result;
            if (!attached) target?.Add(result);
            return result;
        }

        async ValueTask<Paragraph> Paragraph(ContainerInline? inline, BlockCollection? target, int headingLevel = 0)
        {
            var result = new Paragraph { Margin = new Thickness(0, 4, 0, 10), LineHeight = 23 };
            if (headingLevel > 0)
            {
                result.FontSize = headingLevel switch { 1 => 30, 2 => 24, 3 => 20, _ => 17 };
                result.FontWeight = FontWeights.SemiBold; result.Margin = new Thickness(0, 16, 0, 8);
            }
            target?.Add(result);
            if (inline is not null) await AddInlines(result.Inlines, inline);
            return result;
        }
        async ValueTask AddInlines(InlineCollection target, ContainerInline container)
        {
            for (var inline = container.FirstChild; inline is not null; inline = inline.NextSibling)
            {
                // A single paragraph or link label can contain thousands of formatted runs.
                await Cooperate();
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
                    case LiteralInline or HtmlEntityInline or LineBreakInline { IsHard: false }:
                        if (inline.NextSibling is not (LiteralInline or HtmlEntityInline or LineBreakInline { IsHard: false }))
                        {
                            if (inline is LiteralInline literal) target.Add(Literal(literal, literal.Content.ToString()));
                            else target.Add(new Run(inline is HtmlEntityInline entity ? entity.Transcoded.ToString() : " "));
                            break;
                        }
                        var plain = new System.Text.StringBuilder();
                        var run = new Run(); target.Add(run);
                        while (true)
                        {
                            await Cooperate();
                            var text = inline switch { LiteralInline literal => literal.Content.ToString(), HtmlEntityInline entity => entity.Transcoded.ToString(), _ => " " };
                            if (inline is LiteralInline && LiteralSourceMapping.Locate(parsed.Source, inline.Span.Start, inline.Span.End, text) is { } mapping)
                                literalSpans.Add(new(run, mapping, plain.Length));
                            plain.Append(text);
                            if (inline.NextSibling is not (LiteralInline or HtmlEntityInline or LineBreakInline { IsHard: false })) break;
                            inline = inline.NextSibling;
                        }
                        run.Text = plain.ToString();
                        break;
                    case AutolinkInline auto:
                        var autoReference = MarkdownLinks.AutolinkReference(auto);
                        var autoLink = new Hyperlink(Literal(auto, auto.Url)) { Foreground = dark ? Brushes.LightSkyBlue : Brushes.RoyalBlue, ToolTip = autoReference };
                        System.Windows.Automation.AutomationProperties.SetName(autoLink, auto.Url);
                        System.Windows.Automation.AutomationProperties.SetAutomationId(autoLink, "DocumentLink" + auto.Span.Start);
                        autoLink.Click += (_, _) => navigate(autoReference);
                        target.Add(autoLink); break;
                    case CodeInline code:
                        var codeRun = Literal(code, code.Content); codeRun.FontFamily = new FontFamily("Consolas"); codeRun.Background = surface;
                        target.Add(codeRun); break;
                    case MathInline formula: target.Add(Formula(formula, formula.Content.ToString(), formula.DelimiterCount == 2)); break;
                    case LineBreakInline line: if (line.IsHard) target.Add(new LineBreak()); else target.Add(new Run(" ")); break;
                    case EmphasisInline emphasis:
                        var formatting = MarkdownFormatting.Emphasis(emphasis);
                        // A single literal needs one native text element, rather than a
                        // formatting container and a second element with identical text.
                        var literalChild = emphasis.FirstChild as LiteralInline;
                        var span = literalChild is not null && literalChild.NextSibling is null
                            ? (System.Windows.Documents.Inline)Literal(literalChild, literalChild.Content.ToString()) : new Span();
                        if (formatting.HasFlag(MarkdownEmphasis.Bold)) span.FontWeight = FontWeights.Bold;
                        if (formatting.HasFlag(MarkdownEmphasis.Italic)) span.FontStyle = FontStyles.Italic;
                        if (formatting.HasFlag(MarkdownEmphasis.Strike)) span.TextDecorations = TextDecorations.Strikethrough;
                        if (formatting.HasFlag(MarkdownEmphasis.Underline)) span.TextDecorations = TextDecorations.Underline;
                        if (formatting.HasFlag(MarkdownEmphasis.Subscript)) { span.BaselineAlignment = BaselineAlignment.Subscript; span.FontSize = 11; }
                        if (formatting.HasFlag(MarkdownEmphasis.Superscript)) { span.BaselineAlignment = BaselineAlignment.Superscript; span.FontSize = 11; }
                        if (formatting.HasFlag(MarkdownEmphasis.Highlight)) span.Background = surface;
                        target.Add(span);
                        if (span is Span containerSpan) await AddInlines(containerSpan.Inlines, emphasis);
                        break;
                    case TaskList task: target.Add(new Run(task.Checked ? "☑ " : "☐ ")); break;
                    case LinkInline link when link.IsImage:
                        var alt = MarkdownService.InlineText(link);
                        try
                        {
                            if (preparedImages is null || !preparedImages.TryGetValue(link.Url ?? "", out var preparedImage)) throw new IOException("The image was not prepared for preview.");
                            var bitmap = preparedImage.Pixels ?? throw new IOException(preparedImage.Error ?? "The image could not be prepared.");
                            var image = new Image { Source = bitmap, Stretch = Stretch.Uniform, MaxWidth = 720, MaxHeight = 720, ToolTip = alt, Focusable = true };
                            media.Add(new(image, image.MaxHeight));
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
                    case WikiLinkInline wiki:
                        if (navigateWikiLink is null) { target.Add(new Run(wiki.Label)); break; }
                        var wikiLink = new Hyperlink(new Run(wiki.Label)) { Foreground = dark ? Brushes.LightSkyBlue : Brushes.RoyalBlue, ToolTip = "[[" + wiki.Target + "]]" };
                        System.Windows.Automation.AutomationProperties.SetName(wikiLink, wiki.Label);
                        System.Windows.Automation.AutomationProperties.SetAutomationId(wikiLink, "WikiLink" + wiki.Span.Start);
                        wikiLink.Click += (_, _) => navigateWikiLink(wiki.Target);
                        target.Add(wikiLink); break;
                    case LinkInline link:
                        var hyperlink = new Hyperlink { Foreground = dark ? Brushes.LightSkyBlue : Brushes.RoyalBlue, ToolTip = link.Url };
                        System.Windows.Automation.AutomationProperties.SetName(hyperlink, MarkdownService.InlineText(link));
                        System.Windows.Automation.AutomationProperties.SetAutomationId(hyperlink, "DocumentLink" + link.Span.Start);
                        target.Add(hyperlink);
                        await AddInlines(hyperlink.Inlines, link);
                        hyperlink.Click += (_, _) => navigate(link.Url ?? "");
                        break;
                    case HtmlInline html: target.Add(new Run(html.Tag)); break;
                    case ContainerInline nested: await AddInlines(target, nested); break;
                }
            }
        }
        System.Windows.Documents.Inline Formula(Markdig.Syntax.MarkdownObject node, string source, bool display)
        {
            var sourceStart = node.Span.Start;
            PreviewFormula? prepared = null; preparedFormulae?.TryGetValue(sourceStart, out prepared);
            if (prepared?.Drawing is not { } drawing)
            {
                warnings.Add(prepared?.Error ?? "The formula is shown as source because its native drawing was unavailable.");
                return new Run(source) { FontFamily = new FontFamily("Consolas"), ToolTip = warnings[^1] };
            }
            var image = new Image { Source = drawing, Width = Math.Min(720, drawing.Width), Height = Math.Min(720 / drawing.Width, 1) * drawing.Height,
                Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left, ToolTip = source, Focusable = true };
            media.Add(new(image, image.MaxHeight));
            System.Windows.Automation.AutomationProperties.SetName(image, "Formula: " + source);
            System.Windows.Automation.AutomationProperties.SetAutomationId(image, "DocumentFormula" + sourceStart);
            var copy = new MenuItem { Header = "Copy LaTeX source", IsEnabled = copyCode is not null };
            System.Windows.Automation.AutomationProperties.SetAutomationId(copy, "CopyFormulaSource");
            copy.Click += (_, _) => copyCode?.Invoke(new(source, false));
            image.ContextMenu = new ContextMenu(); image.ContextMenu.Items.Add(copy);
            var inline = new InlineUIContainer(image) { BaselineAlignment = display ? BaselineAlignment.Center : BaselineAlignment.TextBottom,
                Tag = new SourceReference(sourceStart, node.Span.End, node.Line) };
            sourceElements[sourceStart] = inline; formulaSources.Add(new(source, inline)); return inline;
        }
    }
}
