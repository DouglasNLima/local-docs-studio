using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.Workspace;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
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
    public PreviewResult Render(ParsedDocument parsed, string? sourcePath, string? workspaceRoot, bool dark,
        IReadOnlyDictionary<FencedCodeBlock, DiagramResult> diagrams, Action<string> navigate, Action<string, string> exportDiagram)
    {
        var foreground = new SolidColorBrush(dark ? Color.FromRgb(232, 234, 237) : Color.FromRgb(32, 33, 36));
        var surface = new SolidColorBrush(dark ? Color.FromRgb(41, 43, 47) : Color.FromRgb(243, 244, 246));
        var document = new FlowDocument { FontFamily = new FontFamily("Segoe UI"), FontSize = 15, Foreground = foreground, Background = Brushes.Transparent,
            PagePadding = new Thickness(28), ColumnWidth = double.PositiveInfinity };
        var sourceElements = new Dictionary<int, TextElement>();
        var warnings = new List<string>();
        foreach (var block in parsed.Syntax) document.Blocks.Add(Convert(block));
        return new(document, sourceElements, warnings);

        WpfBlock Convert(MdBlock block)
        {
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
                            var drawing = svg.Render(diagram.Svg!);
                            var image = new Image { Source = drawing, Stretch = Stretch.Uniform, MaxHeight = 900, HorizontalAlignment = HorizontalAlignment.Left };
                            System.Windows.Automation.AutomationProperties.SetName(image, "Mermaid diagram");
                            panel.Children.Add(image);
                            var actions = new StackPanel { Orientation = Orientation.Horizontal };
                            foreach (var format in new[] { "SVG", "PNG", "Copy source" })
                            {
                                var button = new Button { Content = format, Margin = new Thickness(0, 4, 8, 0), Padding = new Thickness(8, 3, 8, 3) };
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
                    result = new Paragraph(new Run(code.Lines.ToString())) { FontFamily = new FontFamily("Consolas"), FontSize = 13, Background = surface, Padding = new Thickness(12) };
                    break;
                case QuoteBlock quote:
                    var section = new Section { BorderBrush = new SolidColorBrush(Color.FromRgb(255, 136, 62)), BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(12, 4, 4, 4) };
                    foreach (var child in quote) section.Blocks.Add(Convert(child)); result = section;
                    break;
                case ListBlock list:
                    var wl = new System.Windows.Documents.List { MarkerStyle = list.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc, Padding = new Thickness(24, 0, 0, 0) };
                    foreach (var item in list.OfType<ListItemBlock>())
                    {
                        var li = new ListItem();
                        foreach (var child in item) li.Blocks.Add(Convert(child));
                        wl.ListItems.Add(li);
                    }
                    result = wl; break;
                case Markdig.Extensions.Tables.Table table:
                    var wt = new WpfTable { CellSpacing = 0, Margin = new Thickness(0, 8, 0, 12) };
                    var group = new TableRowGroup(); wt.RowGroups.Add(group);
                    foreach (var row in table.OfType<Markdig.Extensions.Tables.TableRow>())
                    {
                        var wr = new System.Windows.Documents.TableRow { Background = row.IsHeader ? surface : Brushes.Transparent };
                        foreach (var cell in row.OfType<Markdig.Extensions.Tables.TableCell>())
                        {
                            var wc = new System.Windows.Documents.TableCell { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(.5), Padding = new Thickness(8, 6, 8, 6) };
                            if (row.IsHeader) wc.FontWeight = FontWeights.SemiBold;
                            foreach (var child in cell) wc.Blocks.Add(Convert(child));
                            wr.Cells.Add(wc);
                        }
                        group.Rows.Add(wr);
                    }
                    result = wt; break;
                case ThematicBreakBlock:
                    result = new Paragraph { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0, 0, 0, 1), Margin = new Thickness(0, 12, 0, 12) }; break;
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
                switch (inline)
                {
                    case LiteralInline literal: target.Add(new Run(literal.Content.ToString())); break;
                    case CodeInline code: target.Add(new Run(code.Content) { FontFamily = new FontFamily("Consolas"), Background = surface }); break;
                    case LineBreakInline: target.Add(new LineBreak()); break;
                    case EmphasisInline emphasis:
                        Span span = emphasis.DelimiterCount == 2 ? new Bold() : new Italic();
                        if (emphasis.DelimiterChar == '~') span.TextDecorations = TextDecorations.Strikethrough;
                        AddInlines(span.Inlines, emphasis); target.Add(span); break;
                    case TaskList task: target.Add(new Run(task.Checked ? "☑ " : "☐ ")); break;
                    case LinkInline link when link.IsImage:
                        var alt = MarkdownService.InlineText(link);
                        try
                        {
                            if (sourcePath is null || workspaceRoot is null) throw new IOException("Save the document inside a workspace to resolve images.");
                            var path = new WorkspacePath(workspaceRoot).ResolveLink(sourcePath, link.Url ?? "");
                            if (Path.GetExtension(path).Equals(".svg", StringComparison.OrdinalIgnoreCase)) throw new IOException("User-supplied SVG images are not displayed.");
                            if (new FileInfo(path).Length > 32 * 1024 * 1024) throw new IOException("Image exceeds 32 MB.");
                            using var file = File.OpenRead(path);
                            var bitmap = BitmapFrame.Create(file, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                            bitmap.Freeze();
                            var image = new Image { Source = bitmap, Stretch = Stretch.Uniform, MaxWidth = 720, MaxHeight = 720, ToolTip = alt };
                            System.Windows.Automation.AutomationProperties.SetName(image, alt.Length == 0 ? "Document image" : alt);
                            target.Add(new InlineUIContainer(image));
                        }
                        catch (Exception ex) when (ex is IOException or NotSupportedException or ArgumentException or System.Runtime.InteropServices.COMException)
                        { target.Add(new Run("[Image: " + alt + "]")); warnings.Add(ex.Message); }
                        break;
                    case LinkInline link:
                        var hyperlink = new Hyperlink { Foreground = dark ? Brushes.LightSkyBlue : Brushes.RoyalBlue, ToolTip = link.Url };
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
