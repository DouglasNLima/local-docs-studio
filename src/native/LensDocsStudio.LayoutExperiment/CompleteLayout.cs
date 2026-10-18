using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LensDocsStudio.LayoutExperiment;

internal sealed record StyledRun(int Start, string Text, int SourceStart, bool Bold, bool Italic, bool Code);
internal sealed record Item(int Start, int Length, int SourceStart, int SourceEnd, ImmutableArray<StyledRun> Runs);
internal sealed record ListModel(string Source, string Text, ImmutableArray<int> SourceBoundaries, ImmutableArray<Item> Items, bool Ordered, int StartIndex);
internal sealed record Caret(int Offset, double X);
internal sealed record Cluster(int Start, int End, ImmutableArray<Rect> Bounds);
internal sealed record Line(int Start, int End, int Item, double Top, double Height, double Baseline,
    DrawingGroup Drawing, ImmutableArray<Caret> Carets, ImmutableArray<Cluster> Clusters);
internal sealed record LayoutSnapshot(ListModel Model, ImmutableArray<Line> Lines, double Width, double Height,
    int WorkerThread, double LayoutMilliseconds, long Version, string Appearance, ImmutableDictionary<string, double> Stages);

/// <summary>Diagnostic list-only engine. No candidate FlowDocument or hidden viewer.</summary>
internal static class CompleteLayout
{
    public static ListModel Parse(string source, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var parsed = new MarkdownService().Parse(source);
        if (parsed.Syntax.Count != 1 || parsed.Syntax[0] is not ListBlock list)
            throw new NotSupportedException("The feasibility gate accepts exactly one semantic list.");
        var text = new StringBuilder(); var map = new List<int>(); var items = ImmutableArray.CreateBuilder<Item>();
        foreach (var item in list.OfType<ListItemBlock>())
        {
            cancellation.ThrowIfCancellationRequested();
            if (item.Count != 1 || item[0] is not ParagraphBlock paragraph || paragraph.Inline is null)
                throw new NotSupportedException("The feasibility gate requires one paragraph in each item.");
            var start = text.Length; var runs = ImmutableArray.CreateBuilder<StyledRun>();
            Add(paragraph.Inline, false, false);
            var length = text.Length - start;
            // Separators are explicit semantic characters. Their source boundaries span
            // the closing delimiters/newline/opening marker, rather than invent offsets.
            text.Append("\r\n"); map.Add(Math.Min(source.Length, paragraph.Span.End + 1)); map.Add(Math.Min(source.Length, item.Span.End + 1));
            items.Add(new(start, length, item.Span.Start, item.Span.End + 1, runs.ToImmutable()));

            void Add(ContainerInline parent, bool bold, bool italic)
            {
                foreach (var inline in parent)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (inline is EmphasisInline emphasis)
                    { Add(emphasis, bold || emphasis.DelimiterCount == 2, italic || emphasis.DelimiterCount == 1); continue; }
                    var content = inline switch { LiteralInline literal => literal.Content.ToString(), CodeInline code => code.Content, _ => null };
                    if (content is null) throw new NotSupportedException("Unsupported inline in the feasibility gate: " + inline.GetType().Name);
                    var exact = LiteralSourceMapping.Locate(source, inline.Span.Start, inline.Span.End, content)
                        ?? throw new NotSupportedException("An exact literal source mapping is required.");
                    runs.Add(new(text.Length, content, exact.Start, bold, italic, inline is CodeInline));
                    text.Append(content); for (var i = 0; i < content.Length; i++) map.Add(exact.Start + i);
                }
            }
        }
        map.Add(source.Length);
        return new(source, text.ToString(), map.ToImmutableArray(), items.ToImmutable(), list.IsOrdered, MarkdownFormatting.OrderedStart(list));
    }

    public static LayoutSnapshot Format(ListModel model, double width, long version, string appearance, CancellationToken cancellation)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var colour = appearance == "Dark" ? Color.FromRgb(232, 234, 237) : Color.FromRgb(32, 33, 36);
        if (appearance.StartsWith("HC:", StringComparison.Ordinal)) colour = (Color)ColorConverter.ConvertFromString(appearance[3..]);
        var foreground = new SolidColorBrush(colour); foreground.Freeze();
        using var formatter = TextFormatter.Create(TextFormattingMode.Ideal);
        var normal = new RunProperties(false, false, false, foreground);
        // Match the stock List's default marker gap: half its inherited natural
        // line height. The paragraph's explicit 23-DIP advance is a separate value.
        var markerGap = normal.Typeface.FontFamily.LineSpacing * normal.FontRenderingEmSize / 2;
        var styles = new Dictionary<(bool Bold, bool Italic, bool Code), RunProperties>();
        var plainParagraph = new ParagraphProperties(normal, false, false, 1);
        var markers = new Dictionary<int, DrawingGroup>();
        long formattingTicks = 0, drawingTicks = 0, geometryTicks = 0;
        var lines = ImmutableArray.CreateBuilder<Line>(); var top = 28d;
        for (var itemIndex = 0; itemIndex < model.Items.Length; itemIndex++)
        {
            cancellation.ThrowIfCancellationRequested();
            var item = model.Items[itemIndex];
            top += itemIndex == 0 ? 0 : 10; // Stock suppresses the first paragraph's top margin in a list.
            var source = new ParagraphSource(model, item, run =>
            {
                var key = (run.Bold, run.Italic, run.Code);
                if (!styles.TryGetValue(key, out var properties)) styles.Add(key, properties = new(run.Bold, run.Italic, run.Code, foreground));
                return properties;
            });
            var position = 0; TextLineBreak? previous = null;
            try
            {
                do
                {
                    cancellation.ThrowIfCancellationRequested();
                    var phaseStart = System.Diagnostics.Stopwatch.GetTimestamp();
                    using var line = formatter.FormatLine(source, position, width - 56 - 24 - SystemParameters.VerticalScrollBarWidth, plainParagraph, previous);
                    formattingTicks += System.Diagnostics.Stopwatch.GetTimestamp() - phaseStart;
                    previous?.Dispose(); previous = line.GetTextLineBreak();
                    var end = Math.Min(item.Length, position + line.Length);
                    var drawing = new DrawingGroup();
                    phaseStart = System.Diagnostics.Stopwatch.GetTimestamp();
                    using (var context = drawing.Open())
                    {
                        line.Draw(context, new Point(52, top), InvertAxes.None);
                        if (position == 0)
                        {
                            var markerKey = model.Ordered ? model.StartIndex + itemIndex : 0;
                            if (!markers.TryGetValue(markerKey, out var markerDrawing))
                            {
                                // Public marker source and formatter, scoped to this one
                                // snapshot. No private framework or decorative bullet text.
                                var marker = new TextSimpleMarkerProperties(model.Ordered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
                                    -markerGap, Math.Max(1, markerKey), plainParagraph);
                                using var markerLine = formatter.FormatLine(marker.TextSource, 0, 100, plainParagraph, null);
                                markerDrawing = new DrawingGroup();
                                using (var markerContext = markerDrawing.Open())
                                    markerLine.Draw(markerContext, new Point(52 - markerGap - markerLine.Width, line.Baseline - markerLine.Baseline), InvertAxes.None);
                                markerDrawing.Freeze(); markers.Add(markerKey, markerDrawing);
                            }
                            context.PushTransform(new TranslateTransform(0, top)); context.DrawDrawing(markerDrawing); context.Pop();
                        }
                    }
                    drawing.Freeze();
                    drawingTicks += System.Diagnostics.Stopwatch.GetTimestamp() - phaseStart;
                    phaseStart = System.Diagnostics.Stopwatch.GetTimestamp();
                    var carets = ImmutableArray.CreateBuilder<Caret>(); var clusters = ImmutableArray.CreateBuilder<Cluster>();
                    var hit = new CharacterHit(position, 0);
                    carets.Add(new(item.Start + position, 52 + line.GetDistanceFromCharacterHit(hit)));
                    var wholeBounds = end > position ? line.GetTextBounds(position, end - position) : [];
                    // A single-direction, same-size line has one exact measured band.
                    // Other text keeps the general per-cluster TextFormatter query.
                    var simpleBand = wholeBounds.Count == 1 && wholeBounds[0].FlowDirection == FlowDirection.LeftToRight
                        && item.Runs.All(run => !run.Code);
                    while (hit.FirstCharacterIndex + hit.TrailingLength < end)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        var next = line.GetNextCaretCharacterHit(hit);
                        var begin = hit.FirstCharacterIndex + hit.TrailingLength;
                        var finish = Math.Min(end, next.FirstCharacterIndex + next.TrailingLength);
                        if (finish <= begin) throw new InvalidOperationException("TextFormatter did not advance its caret.");
                        var x = 52 + line.GetDistanceFromCharacterHit(next);
                        var previousX = carets[^1].X;
                        var bounds = simpleBand ? ImmutableArray.Create(new Rect(Math.Min(previousX, x), top + wholeBounds[0].Rectangle.Top,
                            Math.Abs(x - previousX), wholeBounds[0].Rectangle.Height))
                            : line.GetTextBounds(begin, finish - begin).Select(value =>
                            { var rectangle = value.Rectangle; rectangle.Offset(52, top); return rectangle; }).ToImmutableArray();
                        clusters.Add(new(item.Start + begin, item.Start + finish, bounds));
                        carets.Add(new(item.Start + finish, x));
                        hit = next;
                    }
                    geometryTicks += System.Diagnostics.Stopwatch.GetTimestamp() - phaseStart;
                    lines.Add(new(item.Start + position, item.Start + end, itemIndex, top, line.Height, line.Baseline,
                        drawing, carets.ToImmutable(), clusters.ToImmutable()));
                    // WPF's stock MaxHeight strategy uses natural glyph height, then
                    // advances by at least LineHeight in its 1/300 DIP text units.
                    top += Math.Max(line.Height, 23 - 1d / 300); position += line.Length;
                } while (position < item.Length);
            }
            finally { previous?.Dispose(); }
        }
        cancellation.ThrowIfCancellationRequested();
        return new(model, lines.ToImmutable(), width, top + 28, Environment.CurrentManagedThreadId,
            watch.Elapsed.TotalMilliseconds, version, appearance, new Dictionary<string, double>
            {
                ["FormatLineMilliseconds"] = formattingTicks * 1000d / System.Diagnostics.Stopwatch.Frequency,
                ["DrawAndFreezeMilliseconds"] = drawingTicks * 1000d / System.Diagnostics.Stopwatch.Frequency,
                ["CompleteCaretAndRangeGeometryMilliseconds"] = geometryTicks * 1000d / System.Diagnostics.Stopwatch.Frequency
            }.ToImmutableDictionary());
    }

    private sealed class RunProperties(bool bold, bool italic, bool code, Brush brush) : TextRunProperties
    {
        public override Typeface Typeface { get; } = new(new FontFamily(code ? "Consolas" : "Segoe UI"), italic ? FontStyles.Italic : FontStyles.Normal, bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal);
        public override double FontRenderingEmSize => code ? 13 : 15;
        public override double FontHintingEmSize => FontRenderingEmSize;
        public override TextDecorationCollection? TextDecorations => null;
        public override Brush ForegroundBrush => brush;
        public override Brush? BackgroundBrush => null;
        public override CultureInfo CultureInfo => CultureInfo.GetCultureInfo("en-GB");
        public override TextEffectCollection? TextEffects => null;
    }

    private sealed class ParagraphProperties(TextRunProperties normal, bool first, bool ordered, int number) : TextParagraphProperties
    {
        public override FlowDirection FlowDirection => FlowDirection.LeftToRight;
        public override TextAlignment TextAlignment => TextAlignment.Left;
        public override double LineHeight => 0;
        public override bool FirstLineInParagraph => first;
        public override TextRunProperties DefaultTextRunProperties => normal;
        public override TextWrapping TextWrapping => TextWrapping.Wrap;
        public override TextMarkerProperties? TextMarkerProperties => first ? new TextSimpleMarkerProperties(ordered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc, -7.5, Math.Max(1, number), new ParagraphProperties(normal, false, false, 1)) : null;
        public override double Indent => 0;
    }

    private sealed class ParagraphSource(ListModel model, Item item, Func<StyledRun, RunProperties> propertiesForRun) : TextSource
    {
        private readonly ImmutableArray<(StyledRun Run, RunProperties Properties)> runs = item.Runs.Select(run =>
            (run, propertiesForRun(run))).ToImmutableArray();
        public override TextRun GetTextRun(int index)
        {
            if (index >= item.Length) return new TextEndOfParagraph(1);
            var offset = item.Start + index;
            foreach (var (run, properties) in runs)
                if (offset >= run.Start && offset < run.Start + run.Text.Length)
                    return new TextCharacters(run.Text, offset - run.Start, run.Text.Length - (offset - run.Start), properties);
            throw new InvalidOperationException("A styled run is missing.");
        }
        public override TextSpan<CultureSpecificCharacterBufferRange> GetPrecedingText(int index) =>
            new(index, new(CultureInfo.GetCultureInfo("en-GB"), new CharacterBufferRange(model.Text, item.Start, index)));
        public override int GetTextEffectCharacterIndexFromTextSourceCharacterIndex(int index) => index;
    }
}
