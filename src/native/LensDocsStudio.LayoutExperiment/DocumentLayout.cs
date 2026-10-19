using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;
using LensDocsStudio.Core.Parsing;

namespace LensDocsStudio.LayoutExperiment;

/// <summary>Complete eager block/cell layout on the owning STA. No formatter survives a snapshot.</summary>
internal static class DocumentLayout
{
    public static LayoutSnapshot Format(ListModel model, double width, long version, string appearance, CancellationToken cancellation)
    {
        using var session = new Session(model, appearance, cancellation);
        var watch = Stopwatch.StartNew();
        var bottom = session.Block(model.Root, 28, 28, width - 56 - SystemParameters.VerticalScrollBarWidth, true);
        cancellation.ThrowIfCancellationRequested(); session.Decorations.Freeze();
        var snapshot = new LayoutSnapshot(model, session.Lines.ToImmutableArray(), width, bottom + 28, Environment.CurrentManagedThreadId,
            watch.Elapsed.TotalMilliseconds, version, appearance, new Dictionary<string, double>
            { ["FormatLineMilliseconds"] = session.FormatTicks * 1000d / Stopwatch.Frequency,
                ["CompleteCaretAndRangeGeometryMilliseconds"] = session.GeometryTicks * 1000d / Stopwatch.Frequency }.ToImmutableDictionary())
        { NodeBounds = session.Bounds.ToImmutableDictionary(), Decorations = session.Decorations, Controls = session.Controls.Where(box => !session.HiddenControlNodes.Contains(box.Node)).ToImmutableArray() };
        var projected = VisibilityProjection.Apply(snapshot, cancellation);
        return projected with { LayoutMilliseconds = watch.Elapsed.TotalMilliseconds };
    }
    private sealed class Session : IDisposable
    {
        private readonly ListModel model;
        private readonly CancellationToken cancellation;
        private readonly TextFormatter formatter = TextFormatter.Create(TextFormattingMode.Ideal);
        private readonly Brush foreground, surface, linkBrush;
        private readonly bool highContrast;
        private readonly string appearance;
        private readonly Dictionary<(bool Bold, bool Italic, bool Code, MarkdownEmphasis Emphasis, bool Link, string? Colour, double Size, bool Heading), Properties> properties = [];
        private readonly Dictionary<(bool Ordered, int Number, double X), DrawingGroup> markers = [];
        public readonly List<Line> Lines = [];
        public readonly Dictionary<int, Rect> Bounds = [];
        public readonly List<ControlBox> Controls = [];
        public readonly HashSet<int> HiddenControlNodes = [];
        public readonly DrawingGroup Decorations = new();
        public long FormatTicks, GeometryTicks;
        private bool hidden;
        public Session(ListModel model, string appearance, CancellationToken cancellation)
        {
            this.model = model; this.cancellation = cancellation; this.appearance = appearance; highContrast = appearance.StartsWith("HC:", StringComparison.Ordinal);
            foreground = Brush(highContrast ? ExperimentAppearance.Slot(appearance, 0) : appearance == "Dark" ? "#E8EAED" : "#202124");
            surface = Brush(highContrast ? ExperimentAppearance.Slot(appearance, 1) : appearance == "Dark" ? "#292B2F" : "#F3F4F6");
            linkBrush = highContrast ? Brush(ExperimentAppearance.Slot(appearance, 5)) : Brush(appearance == "Dark" ? "#87CEFA" : "#4169E1");
        }
        private static SolidColorBrush Brush(string colour) { var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colour)); brush.Freeze(); return brush; }
        private void Rectangle(Brush? fill, Pen? pen, Rect rectangle)
        { if (hidden) return; using var dc = Decorations.Append(); dc.DrawRectangle(fill, pen, rectangle); }
        public double Block(SemanticNode node, double x, double top, double width, bool first = false, (bool Ordered, int Number)? marker = null, TextAlignment alignment = TextAlignment.Left)
        {
            cancellation.ThrowIfCancellationRequested(); var begin = top;
            switch (node.Kind)
            {
                case "Document": case "ListItem": case "Cell":
                    top = Flow(node.Children, x, top, width, true, node.Kind == "Document", marker, alignment, docTop: first && node.Kind == "Document");
                    break;
                case "List":
                    top += ListMargin; var number = node.Number;
                    foreach (var child in node.Children) top = Block(child, x + 24, top, width - 24, true, node.Ordered && node.Number == 0 ? null : (node.Ordered, number++));
                    top += Math.Max(0, ListMargin - (node.Children.LastOrDefault()?.Children.LastOrDefault() is { } last ? After(last) : 0));
                    break;
                case "Quote":
                    top += 4; top = Flow(node.Children, x + 15, top, width - 19, first, false, docTop: first);
                    Rectangle(null, new Pen(Brush("#FF883E"), 3), new Rect(x + 1.5, begin, 0, top - begin + 4)); top += 4; break;
                case "Details":
                    top += 15;
                    top = Block(node.Children[0], x + 11, top, width - 22, true);
                    var headerBottom = top; var wasHidden = hidden; hidden |= !node.Expanded;
                    top = Flow(node.Children.Skip(1).ToImmutableArray(), x + 11, top, width - 22, false, false, initialMargin: 4);
                    hidden = wasHidden; if (!node.Expanded) top = headerBottom;
                    top += 7; Rectangle(null, new Pen(surface, 1), new Rect(x + .5, begin + 8.5, width - 1, top - begin - 8)); top += 12; break;
                case "Table": top = Table(node, x, top + 8, width) + After(node); break;
                case "Media":
                    var media = node.Media ?? throw new IOException("Prepared media is missing.");
                    if (hidden) HiddenControlNodes.Add(node.Id);
                    top += 6;
                    var scale = Math.Min(width / media.Width, 900 / media.Height);
                    var rectangle = new Rect(x, top, media.Width * scale, media.Height * scale);
                    if (!hidden) { using var dc = Decorations.Append(); dc.DrawImage(media, rectangle); }
                    var item = model.Items[node.Paragraph];
                    var drawing = new DrawingGroup(); drawing.Freeze();
                    Lines.Add(new(item.Start, item.Start + item.Length, node.Paragraph, top, rectangle.Height, rectangle.Height, drawing,
                        [new(item.Start, x), new(item.Start + 1, rectangle.Right)], [new(item.Start, item.Start + 1, [rectangle])]) { Hidden = hidden });
                    top = rectangle.Bottom;
                    var actionX = x; var actionTop = top; var actionBottom = top;
                    foreach (var label in new[] { "Zoom", "SVG", "PNG", "Copy source" })
                    {
                        var labelRun = new StyledRun(0, label, 0, false, false, false);
                        var labelModel = model with { Text = label, Items = [new(0, label.Length, 0, 0, [labelRun])] };
                        var labelNode = node with { Paragraph = 0, Children = [] };
                        var labelProps = new Properties(labelRun, 15, false, foreground, surface);
                        var labelSource = new ParagraphSource(labelModel, labelModel.Items[0], labelNode, _ => labelProps, width, labelProps);
                        using var labelLine = formatter.FormatLine(labelSource, 0, 10000, new ParagraphProperties(labelProps, TextAlignment.Left), null);
                        var slotWidth = labelLine.Width + 26; var slotHeight = labelLine.Height + 12;
                        if (actionX > x && actionX + slotWidth > x + width) { actionX = x; actionTop = actionBottom; }
                        Controls.Add(new(node.Id, label, new Rect(actionX, actionTop, slotWidth, slotHeight)));
                        actionX += slotWidth; actionBottom = Math.Max(actionBottom, actionTop + slotHeight);
                    }
                    top = actionBottom + 12; break;
                case "Rule": Rectangle(null, new Pen(Brush("#808080"), 1), new Rect(x, top + 12, width, 0)); top += 25; break;
                default:
                    var heading = node.Kind == "Heading";
                    var code = node.Kind == "Code";
                    var size = heading ? node.Level switch { 1 => 30d, 2 => 24d, 3 => 20d, _ => 17d } : code ? 13 : 15;
                    top += Before(node);
                    var paragraphTop = top;
                    if (code) top += 12;
                    top = Paragraph(node, x + (code ? 12 : 0), top, width - (code ? 24 : 0), size, heading || node.Kind == "DetailsHeader", marker, alignment);
                    if (code) { top += 12; Rectangle(surface, null, new Rect(x, paragraphTop, width, top - paragraphTop)); }
                    top += After(node);
                    break;
            }
            Bounds[node.Id] = new Rect(x, begin, Math.Max(0, width), Math.Max(0, top - begin)); return top;
        }
        private static double CodeMargin => Math.Round(new FontFamily("Consolas").LineSpacing * 13 * 300) / 300;
        private static double ListMargin => Math.Round(new FontFamily("Segoe UI").LineSpacing * 15 * 300) / 300;
        private static double Before(SemanticNode node) => node.Kind switch { "Heading" => 16, "Table" or "Details" => 8, "Rule" => 12, "List" => ListMargin, "Code" => CodeMargin, "Quote" or "DetailsHeader" or "Media" => 0, _ => node.Paragraph >= 0 ? 4 : 0 };
        private static double After(SemanticNode node) => node.Kind switch { "Heading" => 8, "Table" or "Details" => 12, "Rule" => 12, "Code" => CodeMargin, "Quote" or "Media" => 0, "DetailsHeader" => 4, "List" => ListMargin, _ => node.Paragraph >= 0 ? 10 : 0 };
        private double Flow(ImmutableArray<SemanticNode> children, double x, double top, double width, bool suppressFirst, bool trimLast,
            (bool Ordered, int Number)? marker = null, TextAlignment alignment = TextAlignment.Left, bool docTop = false, double initialMargin = 0)
        {
            var previous = initialMargin;
            for (var index = 0; index < children.Length; index++)
            {
                var child = children[index];
                // The stock table-to-container boundary resolves the inherited
                // natural text margin before entering that container's border.
                if (index > 0 && children[index - 1].Kind == "Table" && child.Kind is "Details" or "Quote" or "List") top += Math.Max(0, ListMargin - previous);
                top -= index == 0 && suppressFirst ? Before(child) : Math.Min(previous, Before(child));
                top = Block(child, x, top, width, docTop && index == 0, index == 0 ? marker : null, alignment); previous = After(child);
            }
            return top - (trimLast ? previous : 0);
        }
        private double Table(SemanticNode table, double x, double top, double width)
        {
            var count = table.Children.Select(row => row.Children.Length).DefaultIfEmpty(0).Max();
            if (count == 0) return top;
            // Stock creates no explicit TableColumns: its implicit columns share
            // the available width equally. Shape every cell at that actual width.
            var columns = Enumerable.Repeat(width / count, count).ToArray();
            foreach (var row in table.Children)
            {
                var rowTop = top; var bottom = top; var left = x;
                for (var column = 0; column < row.Children.Length; column++)
                {
                    var cell = row.Children[column]; var cellTop = rowTop + 6.5;
                    foreach (var paragraph in cell.Children)
                    { cellTop += 4; cellTop = Paragraph(paragraph, left + 8.5, cellTop, Math.Max(1, columns[column] - 17), 15, row.Level == 1, null, cell.Alignment) + 10; }
                    bottom = Math.Max(bottom, cellTop + 6.5); left += columns[column];
                }
                left = x;
                for (var column = 0; column < row.Children.Length; column++)
                {
                    var rectangle = new Rect(left, rowTop, columns[column], bottom - rowTop);
                    Bounds[row.Children[column].Id] = rectangle;
                    Rectangle(row.Level == 1 ? surface : null, new Pen(Brush("#808080"), .5), rectangle);
                    left += columns[column];
                }
                Bounds[row.Id] = new Rect(x, rowTop, width, bottom - rowTop); top = bottom;
            }
            return top;
        }
        private ParagraphSource Source(SemanticNode node, double size, bool heading, double width)
        {
            var item = model.Items[node.Paragraph];
            Properties Props(StyledRun run)
            {
                var key = (run.Bold, run.Italic, run.Code, run.Emphasis, run.Link is not null, run.Colour, size, heading);
                if (!properties.TryGetValue(key, out var value))
                {
                    var brush = run.Link is not null ? linkBrush : !highContrast && run.Colour is not null ? Brush("#" + run.Colour) : foreground;
                    properties.Add(key, value = new(run, size, heading, brush, surface));
                }
                return value;
            }
            return new(model, item, node, Props, width, Props(new(0, "", 0, false, false, node.Kind == "Code")));
        }
        private double Paragraph(SemanticNode node, double x, double top, double width, double size, bool heading,
            (bool Ordered, int Number)? marker, TextAlignment alignment)
        {
            var source = Source(node, size, heading, width); var item = model.Items[node.Paragraph];
            var paragraph = new ParagraphProperties(source.Default, alignment);
            var position = 0; TextLineBreak? previous = null;
            try
            {
                do
                {
                    cancellation.ThrowIfCancellationRequested(); var phase = Stopwatch.GetTimestamp();
                    using var line = formatter.FormatLine(source, position, Math.Max(1, width), paragraph, previous);
                    FormatTicks += Stopwatch.GetTimestamp() - phase;
                    previous?.Dispose(); previous = line.GetTextLineBreak();
                    var end = Math.Min(item.Length, position + line.Length - line.NewlineLength);
                    var drawing = new DrawingGroup();
                    using (var dc = drawing.Open())
                    {
                        line.Draw(dc, new Point(x, top), InvertAxes.None);
                        if (position == 0 && marker is { } value)
                        {
                            var key = (value.Ordered, value.Ordered ? value.Number : 0, x);
                            if (!markers.TryGetValue(key, out var mark))
                            {
                                var gap = source.Default.Typeface.FontFamily.LineSpacing * size / 2;
                                var native = new TextSimpleMarkerProperties(value.Ordered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc, -gap, Math.Max(1, value.Number), paragraph);
                                using var markerLine = formatter.FormatLine(native.TextSource, 0, 100, paragraph, null);
                                mark = new DrawingGroup(); using (var mc = mark.Open()) markerLine.Draw(mc, new Point(x - gap - markerLine.Width, -markerLine.Baseline), InvertAxes.None);
                                mark.Freeze(); markers[key] = mark;
                            }
                            dc.PushTransform(new TranslateTransform(0, top + line.Baseline)); dc.DrawDrawing(mark); dc.Pop();
                        }
                    }
                    drawing.Freeze(); phase = Stopwatch.GetTimestamp();
                    var carets = ImmutableArray.CreateBuilder<Caret>(); var clusters = ImmutableArray.CreateBuilder<Cluster>();
                    var hit = new CharacterHit(position, 0); carets.Add(new(item.Start + position, x + line.GetDistanceFromCharacterHit(hit)));
                    var whole = end > position ? line.GetTextBounds(position, end - position) : [];
                    var simple = whole.Count == 1 && whole[0].FlowDirection == FlowDirection.LeftToRight && item.Runs.All(run => !run.Code && run.FontSize == item.Runs[0].FontSize) && node.Children.All(child => child.Media is null);
                    while (hit.FirstCharacterIndex + hit.TrailingLength < end)
                    {
                        cancellation.ThrowIfCancellationRequested(); var next = line.GetNextCaretCharacterHit(hit);
                        var begin = hit.FirstCharacterIndex + hit.TrailingLength; var finish = Math.Min(end, next.FirstCharacterIndex + next.TrailingLength);
                        if (finish <= begin) throw new IOException($"The native formatter failed to advance: {node.Kind}, source {node.SourceStart}, line {position}, end {end}, length {line.Length}, newline {line.NewlineLength}, hit {hit.FirstCharacterIndex}/{hit.TrailingLength}, next {next.FirstCharacterIndex}/{next.TrailingLength}.");
                        var nextX = x + line.GetDistanceFromCharacterHit(next); var oldX = carets[^1].X;
                        var rectangles = simple ? ImmutableArray.Create(new Rect(Math.Min(oldX, nextX), top + whole[0].Rectangle.Top, Math.Abs(nextX - oldX), whole[0].Rectangle.Height))
                            : line.GetTextBounds(begin, finish - begin).SelectMany(bounds => bounds.TextRunBounds is { Count: > 0 } runs ? runs.Select(run => run.Rectangle) : [bounds.Rectangle]).Select(rect => { rect.Offset(x, top); return rect; }).ToImmutableArray();
                        clusters.Add(new(item.Start + begin, item.Start + finish, rectangles)); carets.Add(new(item.Start + finish, nextX)); hit = next;
                    }
                    GeometryTicks += Stopwatch.GetTimestamp() - phase;
                    Lines.Add(new(item.Start + position, item.Start + end, node.Paragraph, top, line.Height, line.Baseline, drawing, carets.ToImmutable(), clusters.ToImmutable()) { Hidden = hidden });
                    top += node.Kind == "Code" ? line.Height : Math.Max(line.Height, 23 - 1d / 300); position += line.Length;
                } while (position < item.Length);
            }
            finally { previous?.Dispose(); }
            return top;
        }
        public void Dispose() => formatter.Dispose();
    }
    private sealed class Properties : TextRunProperties
    {
        private readonly StyledRun run;
        private readonly double size;
        private readonly Brush brush, surface;
        public Properties(StyledRun run, double size, bool heading, Brush brush, Brush surface)
        {
            this.run = run; this.size = size; this.brush = brush; this.surface = surface;
            Typeface = new(new FontFamily(run.Code ? "Consolas" : "Segoe UI"), run.Italic ? FontStyles.Italic : FontStyles.Normal,
                run.Bold ? FontWeights.Bold : heading ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal);
        }
        public override Typeface Typeface { get; }
        public override double FontRenderingEmSize => run.Emphasis.HasFlag(MarkdownEmphasis.Subscript) || run.Emphasis.HasFlag(MarkdownEmphasis.Superscript) ? 11 : size;
        public override BaselineAlignment BaselineAlignment => run.Emphasis.HasFlag(MarkdownEmphasis.Subscript) ? BaselineAlignment.Subscript : run.Emphasis.HasFlag(MarkdownEmphasis.Superscript) ? BaselineAlignment.Superscript : BaselineAlignment.Baseline;
        public override double FontHintingEmSize => size;
        public override TextDecorationCollection? TextDecorations => run.Emphasis.HasFlag(MarkdownEmphasis.Strike) ? System.Windows.TextDecorations.Strikethrough
            : run.Link is not null || run.Emphasis.HasFlag(MarkdownEmphasis.Underline) ? System.Windows.TextDecorations.Underline : null;
        public override Brush ForegroundBrush => brush;
        public override Brush? BackgroundBrush => run.Code || run.Emphasis.HasFlag(MarkdownEmphasis.Highlight) ? surface : null;
        public override CultureInfo CultureInfo => TextCulture.Value;
        public override TextEffectCollection? TextEffects => null;
    }
    private sealed class ParagraphProperties(TextRunProperties normal, TextAlignment alignment) : TextParagraphProperties
    {
        public override FlowDirection FlowDirection => FlowDirection.LeftToRight;
        public override TextAlignment TextAlignment => alignment;
        public override double LineHeight => 0;
        public override bool FirstLineInParagraph => true;
        public override TextRunProperties DefaultTextRunProperties => normal;
        public override TextWrapping TextWrapping => TextWrapping.Wrap;
        public override TextMarkerProperties? TextMarkerProperties => null;
        public override double Indent => 0;
    }
    private sealed class ParagraphSource(ListModel model, Item item, SemanticNode node, Func<StyledRun, Properties> props, double width, Properties normal) : TextSource
    {
        public Properties Default => normal;
        public override TextRun GetTextRun(int index)
        {
            if (index >= item.Length) return new TextEndOfParagraph(1);
            var offset = item.Start + index;
            foreach (var run in item.Runs)
            {
                if (offset < run.Start || offset >= run.Start + run.Text.Length) continue;
                if (node.Children.FirstOrDefault(child => child.Start == offset && child.Media is not null) is { Media: { } media }) return new Embedded(media, props(run), width, node.Children.First(child => child.Start == offset && child.Media is not null).Kind == "Toggle");
                var local = offset - run.Start; return new TextCharacters(run.Text, local, run.Text.Length - local, props(run));
            }
            throw new IOException("A complete semantic run is missing.");
        }
        public override TextSpan<CultureSpecificCharacterBufferRange> GetPrecedingText(int index) => new(index, new(TextCulture.Value, new CharacterBufferRange(model.Text, item.Start, index)));
        public override int GetTextEffectCharacterIndexFromTextSourceCharacterIndex(int index) => index;
    }
    private sealed class Embedded(ImageSource media, TextRunProperties properties, double width, bool centred) : TextEmbeddedObject
    {
        private readonly double scale = centred ? 1 : Math.Min(Math.Min(720, width) / media.Width, 720 / media.Height);
        private double Baseline => centred ? media.Height * scale / 2 + (properties.Typeface.FontFamily.Baseline - properties.Typeface.FontFamily.LineSpacing / 2) * properties.FontRenderingEmSize : media.Height * scale;
        public override int Length => 1;
        public override TextRunProperties Properties => properties;
        public override CharacterBufferReference CharacterBufferReference => new();
        public override LineBreakCondition BreakBefore => LineBreakCondition.BreakPossible;
        public override LineBreakCondition BreakAfter => LineBreakCondition.BreakPossible;
        public override bool HasFixedSize => true;
        public override TextEmbeddedObjectMetrics Format(double remainingParagraphWidth) => new(media.Width * scale, media.Height * scale, Baseline);
        public override Rect ComputeBoundingBox(bool rightToLeft, bool sideways) => new(0, -Baseline, media.Width * scale, media.Height * scale);
        public override void Draw(DrawingContext context, Point origin, bool rightToLeft, bool sideways) => context.DrawImage(media, new Rect(origin.X, origin.Y - Baseline, media.Width * scale, media.Height * scale));
    }
}
