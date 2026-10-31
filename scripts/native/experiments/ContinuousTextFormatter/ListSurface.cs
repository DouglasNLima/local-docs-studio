using System.Globalization;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;
using LensDocsStudio.Core.Parsing;

namespace LensDocsStudio.ContinuousTextFormatter;

// Diagnostic only: this is deliberately not a document selection or UIA control.
internal sealed class ListSurface : FrameworkElement, IDisposable
{
    internal sealed record InkGeometry(double Extent, double OverhangLeading, double OverhangTrailing,
        double OverhangAfter, double MarkerHeight, double MarkerBaseline);
    internal sealed record Line(ListParagraph Paragraph, string DisplayText, TextLine Native, int Start, int Length, Point Origin,
        InkGeometry Ink, IReadOnlyList<TextBounds> TextBounds);
    private readonly List<Line> lines = [];
    private TextFormatter? formatter;
    private ListSnapshot? snapshot;
    private long lastGeneration = -1;
    private bool disposed;
    private double layoutWidth = double.NaN;
    public IReadOnlyList<Line> Lines => lines;
    public double FullHeight { get; private set; }
    public int MarkerCount { get; private set; }
    public int FormatCalls { get; private set; }
    public double FormattingMilliseconds { get; private set; }
    private double viewportOffset;
    private double viewportHeight = 300;
    public bool Complete { get; private set; }
    public ListSnapshot? Snapshot => snapshot;

    public void SetViewport(double offset, double height)
    {
        VerifyAccess(); viewportOffset = offset; viewportHeight = height;
        InvalidateVisual();
    }

    public void Attach(ListSnapshot value)
    {
        VerifyAccess();
        ObjectDisposedException.ThrowIf(disposed, this);
        if (value.Generation <= lastGeneration) throw new InvalidOperationException("An older layout generation cannot replace the current snapshot.");
        DisposeLayout();
        snapshot = value; lastGeneration = value.Generation;
        InvalidateMeasure(); InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 640;
        if (snapshot is not null && (layoutWidth != width || !Complete)) Layout(snapshot, width);
        return new(width, FullHeight);
    }

    private void Layout(ListSnapshot source, double width)
    {
        DisposeLayout();
        var y = 28d;
        var foreground = new SolidColorBrush(Color.FromRgb(32, 33, 36)); foreground.Freeze();
        var properties = new Dictionary<MarkdownEmphasis, RunProperties>();
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        RunProperties Props(MarkdownEmphasis style)
        {
            if (!properties.TryGetValue(style, out var result))
                properties[style] = result = new(style, foreground, dpi);
            return result;
        }
        try
        {
            // Formatter creation and all line/marker formatting belong to the measured layout.
            formatter = TextFormatter.Create(TextFormattingMode.Ideal);
            foreach (var paragraph in source.Paragraphs)
            {
                var x = 28 + 24 * (paragraph.Depth + 1);
                var text = new ParagraphSource(paragraph, Props);
                var length = text.Text.Length;
                var position = 0;
                TextLineBreak? previous = null;
                y += 4;
                try
                {
                    do
                    {
                        var first = position == 0;
                        var paragraphProperties = new ParagraphProperties(Props(MarkdownEmphasis.None), first);
                        if (first && paragraph.Marker)
                        {
                            if (paragraph.Ordered && paragraph.Number < 1)
                                throw new NotSupportedException("Zero-based native marker parity is not implemented.");
                            paragraphProperties.Marker = new TextSimpleMarkerProperties(
                                paragraph.Ordered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
                                -12, paragraph.Number, paragraphProperties);
                            MarkerCount++;
                        }
                        var formattingStart = Stopwatch.GetTimestamp();
                        var line = formatter.FormatLine(text, position, Math.Max(1, width - x - 28), paragraphProperties, previous);
                        FormattingMilliseconds += Stopwatch.GetElapsedTime(formattingStart).TotalMilliseconds;
                        // Extent/overhang access forces WPF's lazy ink-bound computation
                        // for every offscreen line, including its marker. Retain the real
                        // text bounds now rather than hide geometry work in later probes.
                        var visibleLength = Math.Min(line.Length, length - position);
                        InkGeometry ink;
                        IReadOnlyList<TextBounds> bounds;
                        try
                        {
                            ink = new(line.Extent, line.OverhangLeading, line.OverhangTrailing, line.OverhangAfter,
                                line.MarkerHeight, line.MarkerBaseline);
                            bounds = visibleLength > 0 ? line.GetTextBounds(position, visibleLength).ToArray() : [];
                        }
                        catch { line.Dispose(); throw; }
                        lines.Add(new(paragraph, text.Text, line, position, visibleLength, new(x, y), ink, bounds));
                        FormatCalls++;
                        previous?.Dispose(); previous = line.GetTextLineBreak();
                        y += line.Height;
                        if (line.Length <= 0) throw new InvalidDataException("Native text layout made no progress.");
                        position += line.Length;
                    }
                    while (position < length);
                }
                finally { previous?.Dispose(); }
                y += 10;
            }
            FullHeight = y + 28;
            layoutWidth = width;
            Complete = true;
        }
        catch { DisposeLayout(); throw; }
    }

    protected override void OnRender(DrawingContext context)
    {
        context.DrawRectangle(Brushes.White, null, new Rect(RenderSize));
        // Every line already owns native text geometry. Offscreen drawing alone is
        // recycled; scrolling and hit tests never call FormatLine.
        var low = 0; var high = lines.Count;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (lines[middle].Origin.Y + lines[middle].Native.Height < viewportOffset) low = middle + 1;
            else high = middle;
        }
        for (var index = low; index < lines.Count && lines[index].Origin.Y <= viewportOffset + viewportHeight; index++)
            lines[index].Native.Draw(context, lines[index].Origin, InvertAxes.None);
    }

    public int HitSource(Line line, double distance, bool forwardAffinity = true)
    {
        VerifyAccess();
        var hit = line.Native.GetCharacterHitFromDistance(distance);
        var offset = Math.Clamp(hit.FirstCharacterIndex + hit.TrailingLength, 0, line.Paragraph.Text.Length);
        foreach (var span in line.Paragraph.Spans)
        {
            if (offset < span.Text.Length || !forwardAffinity && offset == span.Text.Length) return span.SourceStart + offset;
            offset -= span.Text.Length;
        }
        if (offset == 0 && line.Paragraph.Spans.Length > 0)
        {
            var last = line.Paragraph.Spans[^1];
            return last.SourceStart + last.Text.Length;
        }
        throw new InvalidDataException("The hit lies outside retained source intervals.");
    }

    public void Dispose() { VerifyAccess(); DisposeLayout(); snapshot = null; disposed = true; }
    private void DisposeLayout()
    {
        foreach (var line in lines) line.Native.Dispose();
        formatter?.Dispose(); formatter = null;
        lines.Clear(); FullHeight = 0; MarkerCount = 0; FormatCalls = 0; FormattingMilliseconds = 0;
        Complete = false; layoutWidth = double.NaN;
    }

    private sealed class RunProperties : TextRunProperties
    {
        private readonly Typeface typeface;
        private readonly Brush foreground;
        public RunProperties(MarkdownEmphasis style, Brush foreground, double pixelsPerDip)
        {
            typeface = new(new FontFamily("Segoe UI"), style.HasFlag(MarkdownEmphasis.Italic) ? FontStyles.Italic : FontStyles.Normal,
                style.HasFlag(MarkdownEmphasis.Bold) ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal);
            this.foreground = foreground; PixelsPerDip = pixelsPerDip;
        }
        public override Typeface Typeface => typeface;
        public override double FontRenderingEmSize => 15;
        public override double FontHintingEmSize => 15;
        public override TextDecorationCollection? TextDecorations => null;
        public override Brush ForegroundBrush => foreground;
        public override Brush? BackgroundBrush => null;
        public override CultureInfo CultureInfo => CultureInfo.GetCultureInfo("en-GB");
        public override TextEffectCollection? TextEffects => null;
    }

    private sealed class ParagraphProperties(TextRunProperties properties, bool first) : TextParagraphProperties
    {
        public TextMarkerProperties? Marker { get; set; }
        public override FlowDirection FlowDirection => FlowDirection.LeftToRight;
        public override TextAlignment TextAlignment => TextAlignment.Left;
        public override double LineHeight => 23;
        public override bool FirstLineInParagraph => first;
        public override TextRunProperties DefaultTextRunProperties => properties;
        public override TextWrapping TextWrapping => TextWrapping.Wrap;
        public override TextMarkerProperties? TextMarkerProperties => Marker;
        public override double Indent => 0;
    }

    private sealed class ParagraphSource : TextSource
    {
        private readonly ListParagraph paragraph;
        private readonly Func<MarkdownEmphasis, RunProperties> properties;
        public string Text { get; }
        public ParagraphSource(ListParagraph paragraph, Func<MarkdownEmphasis, RunProperties> properties)
        { this.paragraph = paragraph; this.properties = properties; Text = paragraph.Text; }
        public override TextRun GetTextRun(int index)
        {
            if (index >= Text.Length) return new TextEndOfParagraph(1);
            var offset = 0;
            foreach (var span in paragraph.Spans)
            {
                if (index < offset + span.Text.Length)
                    return new TextCharacters(Text, index, offset + span.Text.Length - index, properties(span.Style));
                offset += span.Text.Length;
            }
            throw new InvalidDataException("Text store interval is missing.");
        }
        public override TextSpan<CultureSpecificCharacterBufferRange> GetPrecedingText(int limit)
            => new(limit, new(CultureInfo.GetCultureInfo("en-GB"), new CharacterBufferRange(Text, 0, Math.Min(limit, Text.Length))));
        public override int GetTextEffectCharacterIndexFromTextSourceCharacterIndex(int index) => index;
    }
}
