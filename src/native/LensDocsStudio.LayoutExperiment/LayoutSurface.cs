using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LensDocsStudio.LayoutExperiment;

internal sealed class LayoutSurface : FrameworkElement
{
    public LayoutSnapshot? Snapshot { get; private set; }
    public int Anchor { get; private set; }
    public int Active { get; private set; }
    public int Start => Math.Min(Anchor, Active);
    public int End => Math.Max(Anchor, Active);
    public ScrollViewer? Scroller { get; set; }
    public string LastKey { get; private set; } = "";
    public List<string> KeyHistory { get; } = [];
    public int SelectionSourceStart => Snapshot?.Model.SourceBoundaries[Start] ?? 0;
    public int SelectionSourceEnd => End > Start && Snapshot is { } snapshot ? SourceEnd(snapshot, End) : SelectionSourceStart;
    private static int SourceEnd(LayoutSnapshot snapshot, int end)
    {
        // A range ending after a literal excludes its closing Markdown delimiter.
        foreach (var item in snapshot.Model.Items)
        foreach (var run in item.Runs)
            if (end > run.Start && end <= run.Start + run.Text.Length) return run.SourceStart + end - run.Start;
        return snapshot.Model.SourceBoundaries[end];
    }

    public LayoutSurface()
    {
        Focusable = true; Cursor = Cursors.IBeam;
        CommandBindings.Add(new(ApplicationCommands.SelectAll, (_, _) => Select(0, Snapshot?.Model.Text.Length ?? 0)));
        CommandBindings.Add(new(ApplicationCommands.Copy, (_, _) => Copy()));
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new LayoutAutomationPeer(this);
    public void Install(LayoutSnapshot? snapshot)
    {
        Snapshot = snapshot; Anchor = Active = 0;
        InvalidateMeasure(); InvalidateVisual();
        (UIElementAutomationPeer.FromElement(this) as LayoutAutomationPeer)?.Changed();
    }
    public void Select(int anchor, int active)
    {
        var length = Snapshot?.Model.Text.Length ?? 0;
        Anchor = Math.Clamp(anchor, 0, length); Active = Math.Clamp(active, 0, length);
        InvalidateVisual(); (UIElementAutomationPeer.FromElement(this) as LayoutAutomationPeer)?.SelectionChanged();
    }
    public void Copy()
    {
        if (Snapshot is not { } snapshot || Start == End) return;
        var text = snapshot.Model.Text[Start..End];
        var data = new DataObject(); data.SetText(text, TextDataFormat.UnicodeText); data.SetText(text, TextDataFormat.Text);
        // Preserve actual style boundaries in RTF; clipboard is owned by this dispatcher.
        var rtf = new System.Text.StringBuilder(@"{\rtf1\ansi\deff0{\fonttbl{\f0 Segoe UI;}{\f1 Consolas;}}\uc1\fs23 ");
        var runs = snapshot.Model.Items.SelectMany(item => item.Runs).ToArray(); var runIndex = 0;
        for (var at = Start; at < End; at++)
        {
            while (runIndex < runs.Length && at >= runs[runIndex].Start + runs[runIndex].Text.Length) runIndex++;
            var run = runIndex < runs.Length && at >= runs[runIndex].Start ? runs[runIndex] : null;
            rtf.Append(run?.Bold == true ? @"\b " : @"\b0 ").Append(run?.Italic == true ? @"\i " : @"\i0 ");
            var value = text[at - Start];
            if (value == '\r') continue;
            if (value == '\n') rtf.Append(@"\par ");
            else if (value is '\\' or '{' or '}') rtf.Append('\\').Append(value);
            else if (value > 127) rtf.Append(@"\u").Append((short)value).Append('?');
            else rtf.Append(value);
        }
        data.SetData(DataFormats.Rtf, rtf.Append('}').ToString()); Clipboard.SetDataObject(data, true);
    }
    protected override Size MeasureOverride(Size available) => Snapshot is { } snapshot ? new(snapshot.Width - SystemParameters.VerticalScrollBarWidth, snapshot.Height) : new(0, 0);
    protected override void OnRender(DrawingContext context)
    {
        var snapshot = Snapshot; if (snapshot is null) return;
        var background = snapshot.Appearance.StartsWith("HC:", StringComparison.Ordinal) ? SystemColors.WindowBrush
            : snapshot.Appearance == "Dark" ? new SolidColorBrush(Color.FromRgb(32, 33, 36)) : Brushes.White;
        context.DrawRectangle(background, null, new Rect(RenderSize));
        var top = Scroller?.VerticalOffset ?? 0; var bottom = top + (Scroller?.ViewportHeight ?? ActualHeight);
        foreach (var line in snapshot.Lines)
        {
            if (line.Top + line.Height < top) continue;
            if (line.Top > bottom) break;
            foreach (var cluster in line.Clusters)
                if (cluster.End > Start && cluster.Start < End)
                    foreach (var bounds in cluster.Bounds) context.DrawRectangle(SystemColors.HighlightBrush, null, bounds);
            context.DrawDrawing(line.Drawing);
        }
    }
    public IEnumerable<Rect> Bounds(int start, int end, bool visibleOnly = false)
    {
        if (Snapshot is not { } snapshot) yield break;
        var top = Scroller?.VerticalOffset ?? 0; var bottom = top + (Scroller?.ViewportHeight ?? ActualHeight);
        foreach (var line in snapshot.Lines)
        {
            if (visibleOnly && (line.Top + line.Height < top || line.Top > bottom)) continue;
            if (start == end && start >= line.Start && start <= line.End)
            { yield return new Rect(line.Carets.MinBy(caret => Math.Abs(caret.Offset - start))!.X, line.Top, 1, line.Height); yield break; }
            var rectangles = line.Clusters.Where(cluster => cluster.End > start && cluster.Start < end).SelectMany(cluster => cluster.Bounds).ToArray();
            // Only merge touching rectangles on a common line; bidi disjoint spans remain separate.
            var current = Rect.Empty;
            foreach (var rectangle in rectangles.OrderBy(rectangle => rectangle.Left))
            {
                if (!current.IsEmpty && rectangle.Left > current.Right + .01) { yield return current; current = Rect.Empty; }
                current.Union(rectangle);
            }
            if (!current.IsEmpty) yield return current;
        }
    }
    public int Hit(Point point)
    {
        if (Snapshot is not { } snapshot) return 0;
        var line = snapshot.Lines.MinBy(line => point.Y < line.Top ? line.Top - point.Y : Math.Max(0, point.Y - line.Top - line.Height));
        return line?.Carets.MinBy(caret => Math.Abs(caret.X - point.X))?.Offset ?? 0;
    }
    public void Reveal(int offset)
    {
        if (Snapshot is not { } snapshot) return;
        var line = snapshot.Lines.FirstOrDefault(line => offset >= line.Start && offset <= line.End) ?? snapshot.Lines[^1];
        Scroller?.ScrollToVerticalOffset(Math.Max(0, line.Top - 30));
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    { base.OnMouseLeftButtonDown(e); Focus(); var at = Hit(e.GetPosition(this)); Select(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? Anchor : at, at); CaptureMouse(); e.Handled = true; }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e); if (!IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed) return;
        var point = e.GetPosition(this); Select(Anchor, Hit(point));
        if (Scroller is { } scrolling)
        { if (point.Y < scrolling.VerticalOffset) scrolling.LineUp(); if (point.Y > scrolling.VerticalOffset + scrolling.ViewportHeight) scrolling.LineDown(); }
        e.Handled = true;
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { base.OnMouseLeftButtonUp(e); ReleaseMouseCapture(); e.Handled = true; }
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        LastKey = e.Key + ":" + Keyboard.Modifiers;
        if (KeyHistory.Count < 40) KeyHistory.Add(LastKey);
        base.OnPreviewKeyDown(e); if (Snapshot is not { } snapshot) return;
        var control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control); var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        if (control && e.Key == Key.A) { Select(0, snapshot.Model.Text.Length); e.Handled = true; return; }
        if (control && e.Key == Key.C) { Copy(); e.Handled = true; return; }
        var stops = snapshot.Lines.SelectMany(line => line.Carets.Select(caret => caret.Offset)).Append(snapshot.Model.Text.Length).Distinct().Order().ToArray();
        var line = snapshot.Lines.FirstOrDefault(line => Active >= line.Start && Active <= line.End) ?? snapshot.Lines[^1];
        var next = e.Key switch
        {
            Key.Home => control ? 0 : line.Start,
            Key.End => control ? snapshot.Model.Text.Length : line.End,
            Key.Left => stops.LastOrDefault(value => value < Active, 0),
            Key.Right => stops.FirstOrDefault(value => value > Active, snapshot.Model.Text.Length),
            Key.Up => Hit(new Point(line.Carets.MinBy(caret => Math.Abs(caret.Offset - Active))!.X, line.Top - 11)),
            Key.Down => Hit(new Point(line.Carets.MinBy(caret => Math.Abs(caret.Offset - Active))!.X, line.Top + line.Height + 11)),
            Key.PageUp => Hit(new Point(52, line.Top - (Scroller?.ViewportHeight ?? 300))),
            Key.PageDown => Hit(new Point(52, line.Top + (Scroller?.ViewportHeight ?? 300))),
            _ => -1
        };
        if (next < 0) return; Select(shift ? Anchor : next, next); Reveal(next); e.Handled = true;
    }
}
