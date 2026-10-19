using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LensDocsStudio.LayoutExperiment;

internal sealed class LayoutSurface : Panel
{
    public LayoutSnapshot? Snapshot { get; private set; }
    public int Anchor { get; private set; }
    public int Active { get; private set; }
    private int rawAnchor, rawActive;
    private bool rawPreviousCellEnd;
    public int Start => Math.Min(Anchor, Active);
    public int End => Math.Max(Anchor, Active);
    public ScrollViewer? Scroller { get; set; }
    public string LastKey { get; private set; } = "";
    public List<string> KeyHistory { get; } = [];
    public List<object> MouseHistory { get; } = [];
    public List<object> InteractionStages { get; } = [];
    public int DroppedInteractionObservations { get; private set; }
    public void RecordInteraction(object observation)
    {
        if (InteractionStages.Count >= 2048) { InteractionStages.RemoveRange(0, 1024); DroppedInteractionObservations += 1024; }
        InteractionStages.Add(observation);
    }
    public void ViewportChanged() { InvalidateVisual(); selectionOverlay?.InvalidateVisual(); }
    private (SemanticNode Node, Point Point)? pressedLink;
    private SelectionOverlay? selectionOverlay;
    internal object? LastPointForChecks { get; set; }
    private SemanticNode[] selectedCells = [];
    private readonly HashSet<int> selectedCellParagraphs = [];
    private readonly HashSet<int> cellRangeParagraphs = [];
    private int tableArrowBoundary = -1;
    private bool tableArrowRight;
    private sealed class SelectionOverlay(LayoutSurface owner) : FrameworkElement
    { protected override void OnRender(DrawingContext context) => owner.DrawSelection(context); }
    private static Brush SelectionBrush(string appearance, double opacity = .3)
    {
        // DocumentPreview.xaml owns a 0.3 accent wash in the product. Retain the
        // actual text/image underneath it, including native media commands.
        var brush = new SolidColorBrush(appearance.StartsWith("HC:", StringComparison.Ordinal)
            ? (Color)ColorConverter.ConvertFromString(ExperimentAppearance.Slot(appearance, 3)) : Color.FromRgb(255, 136, 62)) { Opacity = opacity };
        brush.Freeze(); return brush;
    }
    private void DrawSelection(DrawingContext context)
    {
        if (Snapshot is not { } snapshot || Start == End) return;
        // Native selection composites one translucent visual, rather than
        // per-brush opacity. These paths differ by one colour level in HC.
        var brush = SelectionBrush(snapshot.Appearance, 1);
        var top = Scroller?.VerticalOffset ?? 0; var bottom = top + (Scroller?.ViewportHeight ?? ActualHeight);
        var mediaParagraphs = snapshot.Controls.Select(control => control.Node).Distinct().Select(id => snapshot.Model.Root.Descendants().First(node => node.Id == id).Paragraph).ToHashSet();
        foreach (var line in snapshot.Lines)
        {
            if (line.Hidden || line.Top + line.Height < top || line.Top > bottom || mediaParagraphs.Contains(line.Item)
                || selectedCellParagraphs.Contains(line.Item) || cellRangeParagraphs.Contains(line.Item)) continue;
            var topSpace = Start < snapshot.Model.Items[line.Item].Start ? line.SelectionTopSpace : 0;
            var wholeLine = Start <= line.Start && (End > line.End || !line.HasBreak && End >= line.End) && !line.SelectionBounds.IsEmpty;
            if (wholeLine)
            {
                var rectangle = line.SelectionBounds; rectangle.Y -= topSpace; rectangle.Height += topSpace;
                if (End > line.End) rectangle.Width += line.EndMarkerWidth;
                context.DrawRectangle(brush, null, rectangle);
            }
            else
            {
                if (End > line.End && Start <= line.End && !line.SelectionBounds.IsEmpty && line.EndMarkerWidth > 0)
                    context.DrawRectangle(brush, null, new Rect(line.SelectionBounds.Right, line.Top - topSpace, line.EndMarkerWidth, line.Height + topSpace));
                foreach (var bounds in line.Clusters.Where(cluster => cluster.End > Start && cluster.Start < End).SelectMany(cluster => cluster.Bounds))
                { var rectangle = bounds; rectangle.Y -= topSpace; rectangle.Height += topSpace; context.DrawRectangle(brush, null, rectangle); }
            }
        }
        foreach (var cell in selectedCells)
            if (snapshot.NodeBounds.TryGetValue(cell.Id, out var rectangle) && rectangle.Bottom >= top && rectangle.Top <= bottom) context.DrawRectangle(brush, null, rectangle);
        foreach (var id in snapshot.Controls.Select(control => control.Node).Distinct())
        {
            var node = snapshot.Model.Root.Descendants().First(value => value.Id == id);
            if (node.Start < End && node.End > Start && !selectedCells.Any(cell => node.Start >= cell.Start && node.End <= cell.End) && snapshot.NodeBounds.TryGetValue(id, out var bounds)) context.DrawRectangle(brush, null, bounds);
        }
    }
    private ClipboardDispatcher? clipboard;
    private CancellationTokenSource? selectedCopy;
    private (LayoutSnapshot Snapshot, int Start, int End, int CopyStart, int CopyEnd, uint Sequence, long Requested)? pendingCopy;
    private bool copyRunning, copyClosed;
    public ClipboardDispatcher ClipboardWorker => clipboard ??= new();
    public void CloseClipboard() { copyClosed = true; pendingCopy = null; selectedCopy?.Cancel(); clipboard?.Dispose(); }
    public event Action<SemanticNode>? Activated;
    public event Action<ControlBox>? MediaAction;
    public event Action? SelectionUpdated;
    public event Action<int>? SourceNavigationRequested;
    public event Action<string>? SearchRequested;
    public Func<LayoutSnapshot, bool>? IsCurrent { get; set; }
    private bool Current(LayoutSnapshot snapshot) => ReferenceEquals(Snapshot, snapshot) && (IsCurrent?.Invoke(snapshot) ?? true);
    public void CancelCopyPreparation() { selectedCopy?.Cancel(); pendingCopy = null; }
    public void ReleaseOwner()
    {
        // A remote client may retain an invalidated range/provider after closure.
        // It must not keep a visual-parent chain, source editor or studio alive.
        Activated = null; MediaAction = null; SelectionUpdated = null;
        SourceNavigationRequested = null; SearchRequested = null; IsCurrent = null;
        ContextMenu = null; Scroller = null;
    }
    private readonly List<(int Start, int End)> matches = [];
    public void Search(string text)
    {
        matches.Clear(); SearchRequested?.Invoke(text);
        if (Snapshot is { } snapshot && text.Length > 0)
            for (var at = 0; at <= snapshot.Model.Text.Length - text.Length;)
            {
                var found = snapshot.Model.Text.IndexOf(text, at, StringComparison.OrdinalIgnoreCase); if (found < 0) break;
                matches.Add((found, found + text.Length)); at = found + Math.Max(1, text.Length);
            }
        InvalidateVisual();
    }
    public void FindNext(bool next)
    {
        if (matches.Count == 0) return;
        var match = next ? matches.FirstOrDefault(match => match.Start > Start, matches[0]) : matches.LastOrDefault(match => match.Start < Start, matches[^1]);
        Select(match.Start, match.End); Reveal(match.Start);
    }
    public void Activate(SemanticNode node) { if (Snapshot is { } snapshot && Current(snapshot)) Activated?.Invoke(node); }
    public (int Source, double Delta) ViewportSourceAnchor()
    {
        if (Snapshot is not { } snapshot || snapshot.Lines.Length == 0) return (0, 0);
        var top = Scroller?.VerticalOffset ?? 0;
        var line = snapshot.Lines.FirstOrDefault(line => line.Top + line.Height >= top) ?? snapshot.Lines[^1];
        return (snapshot.Model.SourceBoundaries[line.Start], top - line.Top);
    }
    public int LogicalSource(int source)
    {
        if (Snapshot is not { } snapshot) return 0;
        var model = snapshot.Model;
        var paragraph = model.Items.Where(item => item.SourceStart <= source && item.SourceEnd >= source).MinBy(item => item.SourceEnd - item.SourceStart);
        if (paragraph is not null)
        {
            for (var at = paragraph.Start; at < paragraph.Start + paragraph.Length; at++)
                if (model.SourceBoundaries[at] <= source && model.SourceEnds[at] > source || model.SourceBoundaries[at] >= source) return at;
            return paragraph.Start + paragraph.Length;
        }
        var next = model.Items.FirstOrDefault(item => item.SourceStart >= source); return next?.Start ?? model.Text.Length;
    }
    public void SelectSource(int start, int end) => Select(LogicalSource(start), LogicalSource(end));
    public void NavigateSource(int source) => SourceNavigationRequested?.Invoke(source);
    public void NavigateSourceCore(int source) { var logical = LogicalSource(source); Select(logical, logical); Reveal(logical); }
    public void RestoreViewportSourceAnchor((int Source, double Delta) anchor)
    {
        var at = LogicalSource(anchor.Source);
        if (Snapshot?.Lines.FirstOrDefault(line => at >= line.Start && at <= line.End) is { } line) Scroller?.ScrollToVerticalOffset(Math.Max(0, line.Top + anchor.Delta));
    }
    public int SelectionSourceStart => Snapshot?.Model.SourceBoundaries[Start] ?? 0;
    public int SelectionSourceEnd => End > Start && Snapshot is { } snapshot ? SourceEnd(snapshot, End) : SelectionSourceStart;
    private static int SourceEnd(LayoutSnapshot snapshot, int end)
    {
        // A range ending after a literal excludes its closing Markdown delimiter.
        return snapshot.Model.SourceEnds[end - 1];
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
        LastPointForChecks = null;
        var previousAppearance = Snapshot?.Appearance;
        selectedCopy?.Cancel(); pendingCopy = null; pressedLink = null; matches.Clear(); selectedCells = []; selectedCellParagraphs.Clear(); cellRangeParagraphs.Clear(); tableArrowBoundary = -1;
        foreach (var button in Children.OfType<Button>()) button.IsEnabled = false;
        Children.Clear(); selectionOverlay = null;
        if (snapshot is not null)
            foreach (var box in snapshot.Controls)
            {
                var button = new Button { Content = box.Label, Tag = box, FontFamily = new("Segoe UI"), FontSize = 15,
                    Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 4, 8, 0) };
                System.Windows.Automation.AutomationProperties.SetAutomationId(button, "ExperimentalDiagram" + box.Label.Replace(" ", "", StringComparison.Ordinal) + "-" + snapshot.Version + "-" + box.Node);
                button.Click += (_, _) => MediaAction?.Invoke(box); Children.Add(button);
            }
        if (snapshot is not null) { selectionOverlay = new SelectionOverlay(this) { IsHitTestVisible = false, Focusable = false, Opacity = .3 }; Children.Add(selectionOverlay); }
        Snapshot = snapshot; rawAnchor = rawActive = Anchor = Active = 0; rawPreviousCellEnd = false;
        if (snapshot is not null && Scroller is not null)
        {
            var colour = snapshot.Appearance.StartsWith("HC:", StringComparison.Ordinal) ? ExperimentAppearance.Slot(snapshot.Appearance, 1)
                : snapshot.Appearance == "Dark" ? "#202124" : "#FFFFFF";
            var background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colour)); background.Freeze(); Scroller.Background = background;
            if (previousAppearance != snapshot.Appearance && (previousAppearance?.StartsWith("HC:", StringComparison.Ordinal) == true || snapshot.Appearance.StartsWith("HC:", StringComparison.Ordinal)))
            {
                // Recreating the ScrollViewer template alone retained normal
                // scrollbar ink in actual HC captures. Apply only the public
                // built-in Classic scrollbar style used by stock HC chrome.
                if (snapshot.Appearance.StartsWith("HC:", StringComparison.Ordinal))
                {
                    var contrastTheme = new ResourceDictionary { Source = new Uri("/PresentationFramework.Classic;component/themes/Classic.xaml", UriKind.Relative) };
                    Scroller.Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)] = contrastTheme[typeof(System.Windows.Controls.Primitives.ScrollBar)];
                }
                else Scroller.Resources.Remove(typeof(System.Windows.Controls.Primitives.ScrollBar));
                var currentTheme = new ScrollViewer(); currentTheme.ApplyTemplate();
                Scroller.Template = null; Scroller.Template = currentTheme.Template;
            }
        }
        InvalidateMeasure(); InvalidateVisual();
        (UIElementAutomationPeer.FromElement(this) as LayoutAutomationPeer)?.Changed();
    }
    public void Select(int anchor, int active, bool preferPreviousRow = false, bool previousCellEnd = false)
    {
        selectedCopy?.Cancel(); pendingCopy = null;
        var length = Snapshot?.Model.Text.Length ?? 0;
        Anchor = Math.Clamp(anchor, 0, length); Active = Math.Clamp(active, 0, length);
        rawAnchor = Anchor; rawActive = Active;
        rawPreviousCellEnd = previousCellEnd;
        if (Snapshot is { } snapshot)
        {
            var cellTable = TableSelection.CellRangeTable(snapshot, Start, End, previousCellEnd);
            var rawCells = cellTable is not null ? TableSelection.PaintedCells(snapshot, Start, End, previousCellEnd) : null;
            (Anchor, Active) = TableSelection.Normalise(snapshot, Anchor, Active, preferPreviousRow, previousCellEnd);
            selectedCells = rawCells ?? TableSelection.PaintedCells(snapshot, Start, End); selectedCellParagraphs.Clear();
            cellRangeParagraphs.Clear();
            if ((cellTable ?? TableSelection.CellRangeTable(snapshot, Start, End)) is { } table)
                foreach (var node in table.Node.Descendants()) if (node.Paragraph >= 0) cellRangeParagraphs.Add(node.Paragraph);
            foreach (var cell in selectedCells) foreach (var node in cell.Descendants()) if (node.Paragraph >= 0) selectedCellParagraphs.Add(node.Paragraph);
        }
        SelectionUpdated?.Invoke();
        InvalidateVisual(); (UIElementAutomationPeer.FromElement(this) as LayoutAutomationPeer)?.SelectionChanged();
        selectionOverlay?.InvalidateVisual();
    }
    public void Copy()
    {
        if (Snapshot is not { } snapshot || !Current(snapshot) || Start == End || copyClosed) return;
        selectedCopy?.Cancel();
        var copyStart = Math.Min(rawAnchor, rawActive); var copyEnd = Math.Max(rawAnchor, rawActive);
        if (TableSelection.CellRangeTable(snapshot, copyStart, copyEnd, rawPreviousCellEnd) is null) { copyStart = Start; copyEnd = End; }
        else if (rawPreviousCellEnd)
        {
            // GetSelection ends after the selected cell's logical separator.
            // The public interchange pointer must stay inside that cell, rather
            // than resolving its separator to the following cell's first run.
            copyEnd -= copyEnd >= 2 && snapshot.Model.Text.AsSpan(copyEnd - 2, 2).SequenceEqual("\r\n") ? 2 : 1;
        }
        pendingCopy = (snapshot, Start, End, copyStart, copyEnd, LensDocsStudio.Native.Platform.NativeClipboardService.Sequence, System.Diagnostics.Stopwatch.GetTimestamp());
        if (!copyRunning) ProcessCopy();
    }
    private async void ProcessCopy()
    {
        copyRunning = true;
        try
        {
            while (pendingCopy is { } request && !copyClosed)
            {
                pendingCopy = null; var owner = new CancellationTokenSource(); selectedCopy = owner;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var snapshot = request.Snapshot; var start = request.Start; var end = request.End; var sequence = request.Sequence;
                try
                {
                    var payload = await ExperimentWindow.Worker(() => ClipboardSerialisation.Prepare(snapshot, request.CopyStart, request.CopyEnd, owner.Token));
                    var prepared = watch.Elapsed.TotalMilliseconds;
                    await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
                    if (!Current(snapshot) || Start != start || End != end || owner.IsCancellationRequested)
                    { RecordInteraction(new { Stage = "copy-stale-rejected", Thread = Environment.CurrentManagedThreadId }); continue; }
                    var currentSequence = LensDocsStudio.Native.Platform.NativeClipboardService.Sequence;
                    var previous = ClipboardWorker.LastCommit;
                    if (sequence != currentSequence && previous is not null && previous.EndTimestamp > request.Requested && previous.Sequence == currentSequence) sequence = currentSequence;
                    if (sequence != currentSequence) throw new IOException("The clipboard changed while the copy was being prepared.");
                    var grant = System.Diagnostics.Stopwatch.GetTimestamp();
                    var commit = await ClipboardWorker.Selected(payload, sequence, owner.Token);
                    RecordInteraction(new { Stage = "copy", PreparedMilliseconds = prepared, UiGrantTimestamp = grant,
                        Serialisation = payload.Serialisation, NativeCommit = commit, NativeCommitMilliseconds = (commit.EndTimestamp - commit.StartTimestamp) * 1000d / System.Diagnostics.Stopwatch.Frequency,
                        TotalMilliseconds = watch.Elapsed.TotalMilliseconds, CurrentOnCompletion = Current(snapshot) && !owner.IsCancellationRequested, Thread = Environment.CurrentManagedThreadId });
                }
                catch (Exception error) when (error is IOException or OperationCanceledException or System.Runtime.InteropServices.ExternalException)
                { RecordInteraction(new { Stage = "copy-rejected-or-failed", Error = error.Message, Thread = Environment.CurrentManagedThreadId }); }
                finally { if (ReferenceEquals(selectedCopy, owner)) selectedCopy = null; owner.Dispose(); }
            }
        }
        finally { copyRunning = false; }
    }
    protected override Size MeasureOverride(Size available)
    {
        foreach (var button in Children.OfType<Button>()) { var box = (ControlBox)button.Tag; button.Measure(box.Bounds.Size); }
        selectionOverlay?.Measure(new Size(Snapshot?.Width ?? 0, Snapshot?.Height ?? 0));
        return Snapshot is { } snapshot ? new(snapshot.Width - snapshot.Model.Theme.ScrollbarWidth, snapshot.Height) : new(0, 0);
    }
    protected override Size ArrangeOverride(Size finalSize)
    { foreach (var button in Children.OfType<Button>()) button.Arrange(((ControlBox)button.Tag).Bounds); selectionOverlay?.Arrange(new Rect(finalSize)); return finalSize; }
    protected override void OnRender(DrawingContext context)
    {
        var snapshot = Snapshot; if (snapshot is null) return;
        var background = snapshot.Appearance.StartsWith("HC:", StringComparison.Ordinal) ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(ExperimentAppearance.Slot(snapshot.Appearance, 1)))
            : snapshot.Appearance == "Dark" ? new SolidColorBrush(Color.FromRgb(32, 33, 36)) : Brushes.White;
        context.DrawRectangle(background, null, new Rect(RenderSize));
        if (snapshot.Decorations is not null) context.DrawDrawing(snapshot.Decorations);
        var top = Scroller?.VerticalOffset ?? 0; var bottom = top + (Scroller?.ViewportHeight ?? ActualHeight);
        var searchBrush = SelectionBrush(snapshot.Appearance, .12);
        foreach (var line in snapshot.Lines)
        {
            if (line.Hidden || line.Top + line.Height < top || line.Top > bottom) continue;
            context.DrawDrawing(line.Drawing);
            foreach (var cluster in line.Clusters)
                if (!(cluster.End > Start && cluster.Start < End) && matches.Any(match => match.End > cluster.Start && match.Start < cluster.End))
                    foreach (var bounds in cluster.Bounds) context.DrawRectangle(searchBrush, null, bounds);
        }
    }
    public IEnumerable<Rect> Bounds(int start, int end, bool visibleOnly = false)
    {
        if (Snapshot is not { } snapshot) yield break;
        var top = Scroller?.VerticalOffset ?? 0; var bottom = top + (Scroller?.ViewportHeight ?? ActualHeight);
        var viewport = new Rect(Scroller?.HorizontalOffset ?? 0, top, Scroller?.ViewportWidth ?? ActualWidth, bottom - top);
        foreach (var line in RangeLines(snapshot, start, end))
        {
            if (line.Hidden) continue;
            if (visibleOnly && (line.Top + line.Height < top || line.Top > bottom)) continue;
            if (start == end && start >= line.Start && start <= line.End)
            { var caretBounds = new Rect(line.Carets.MinBy(caret => Math.Abs(caret.Offset - start))!.X, line.Top, 1, line.Height); if (visibleOnly) caretBounds.Intersect(viewport); if (!caretBounds.IsEmpty) yield return caretBounds; yield break; }
            var rectangles = line.Clusters.Where(cluster => cluster.End > start && cluster.Start < end).SelectMany(cluster => cluster.Bounds).ToArray();
            // Only merge touching rectangles on a common line; bidi disjoint spans remain separate.
            var current = Rect.Empty;
            foreach (var rectangle in rectangles.OrderBy(rectangle => rectangle.Left))
            {
                if (!current.IsEmpty && rectangle.Left > current.Right + .01) { var output = current; if (visibleOnly) output.Intersect(viewport); if (!output.IsEmpty) yield return output; current = Rect.Empty; }
                current.Union(rectangle);
            }
            if (!current.IsEmpty) { if (visibleOnly) current.Intersect(viewport); if (!current.IsEmpty) yield return current; }
        }
    }
    private static IEnumerable<Line> RangeLines(LayoutSnapshot snapshot, int start, int end)
    {
        // Lines are in logical document order even when adjacent table columns share Y.
        var left = 0; var right = snapshot.Lines.Length;
        while (left < right) { var middle = left + (right - left) / 2; if (snapshot.Lines[middle].End < start) left = middle + 1; else right = middle; }
        for (var index = left; index < snapshot.Lines.Length; index++)
        { var line = snapshot.Lines[index]; if (line.Start > end) yield break; yield return line; }
    }
    public int Hit(Point point)
    {
        if (Snapshot is not { } snapshot) return 0;
        var line = snapshot.Lines.Where(line => !line.Hidden).MinBy(line => Math.Pow(point.Y < line.Top ? line.Top - point.Y : Math.Max(0, point.Y - line.Top - line.Height), 2)
            + Math.Pow(point.X < line.Carets.Min(caret => caret.X) ? line.Carets.Min(caret => caret.X) - point.X
                : Math.Max(0, point.X - line.Carets.Max(caret => caret.X)), 2));
        return line?.Carets.MinBy(caret => Math.Abs(caret.X - point.X))?.Offset ?? 0;
    }
    public void Reveal(int offset)
    {
        if (Snapshot is not { } snapshot) return;
        var line = snapshot.Lines.FirstOrDefault(line => offset >= line.Start && offset <= line.End) ?? snapshot.Lines[^1];
        Scroller?.ScrollToVerticalOffset(Math.Max(0, line.Top - 30));
        Scroller?.UpdateLayout();
    }
    public void ScrollRange(int start, int end, bool alignToTop)
    {
        var rectangles = Bounds(start, end).ToArray(); if (rectangles.Length == 0 || Scroller is null) return;
        var target = alignToTop ? rectangles.Min(rectangle => rectangle.Top) : rectangles.Max(rectangle => rectangle.Bottom) - Scroller.ViewportHeight;
        Scroller.ScrollToVerticalOffset(Math.Max(0, target)); Scroller.UpdateLayout();
    }
    private int Vertical(Line current, bool down)
    {
        var lines = Snapshot!.Lines.Where(line => !line.Hidden && (down ? line.Top > current.Top + .01 : line.Top < current.Top - .01)).ToArray();
        if (lines.Length == 0) return Active;
        var y = down ? lines.Min(line => line.Top) : lines.Max(line => line.Top);
        var x = current.Carets.MinBy(caret => Math.Abs(caret.Offset - Active))!.X;
        return lines.Where(line => Math.Abs(line.Top - y) < .01).SelectMany(line => line.Carets).MinBy(caret => Math.Abs(caret.X - x))!.Offset;
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e); Focus(); var at = Hit(e.GetPosition(this));
        if (MouseHistory.Count < 40) MouseHistory.Add(new { Event = "down", Point = e.GetPosition(this).ToString(), Hit = at });
        if (Snapshot?.Model.Root.Descendants().FirstOrDefault(node => node.Kind is "Link" or "Toggle" && at >= node.Start && at < node.End) is { } target
            && (target.Kind == "Toggle" || Keyboard.Modifiers.HasFlag(ModifierKeys.Control)))
        {
            if (target.Kind == "Toggle") target = Snapshot.Model.Root.Descendants().Single(node => node.Kind == "Details" && node.Children[0].Children.Any(child => child.Id == target.Id));
            Activate(target); e.Handled = true; return;
        }
        pressedLink = Snapshot?.Model.Root.Descendants().FirstOrDefault(node => node.Kind == "Link" && at >= node.Start && at < node.End) is { } link
            ? (link, e.GetPosition(this)) : null;
        Select(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? rawAnchor : at, at); CaptureMouse(); e.Handled = true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e); if (!IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed) return;
        var point = e.GetPosition(this); if (MouseHistory.Count < 40) MouseHistory.Add(new { Event = "move", Point = point.ToString(), Hit = Hit(point) }); Select(rawAnchor, Hit(point));
        if (pressedLink is { } link && (point - link.Point).Length > 4) pressedLink = null;
        if (Scroller is { } scrolling)
        { if (point.Y < scrolling.VerticalOffset) scrolling.LineUp(); if (point.Y > scrolling.VerticalOffset + scrolling.ViewportHeight) scrolling.LineDown(); }
        e.Handled = true;
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        // Windows may coalesce mouse moves before delivering button-up. Commit
        // the actual release point instead of retaining an intermediate move.
        if (IsMouseCaptured) { var point = e.GetPosition(this); Select(rawAnchor, Hit(point)); }
        ReleaseMouseCapture(); var clicked = pressedLink; pressedLink = null; e.Handled = true;
        if (clicked is { } link && (e.GetPosition(this) - link.Point).Length <= 4) Activate(link.Node);
    }
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        LastKey = e.Key + ":" + Keyboard.Modifiers;
        if (KeyHistory.Count < 512) KeyHistory.Add(LastKey);
        base.OnPreviewKeyDown(e); if (Snapshot is not { } snapshot) return;
        var control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control); var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        if (control && e.Key == Key.A) { Select(0, snapshot.Model.Text.Length); e.Handled = true; return; }
        if (control && e.Key == Key.C) { Copy(); e.Handled = true; return; }
        // The stock read-only viewer leaves navigation without Shift to its
        // ScrollViewer. Shift extends the selected insertion point.
        if (!shift) return;
        var stops = snapshot.Indices.Characters;
        var line = snapshot.Lines.FirstOrDefault(line => Active >= line.Start && Active <= line.End) ?? snapshot.Lines[^1];
        var next = e.Key switch
        {
            Key.Home => control ? 0 : line.Start,
            Key.End => control ? snapshot.Model.Text.Length : line.End == snapshot.Model.Items[line.Item].Start + snapshot.Model.Items[line.Item].Length
                ? line.Item + 1 < snapshot.Model.Items.Length ? snapshot.Model.Items[line.Item + 1].Start : snapshot.Model.Text.Length : line.End,
            Key.Left => SnapshotIndices.Adjacent(control ? snapshot.Indices.Words : stops, Active, false),
            Key.Right => SnapshotIndices.Adjacent(control ? snapshot.Indices.Words : stops, Active, true),
            Key.Up => Vertical(line, false),
            Key.Down => Vertical(line, true),
            Key.PageUp => Hit(new Point(52, line.Top - (Scroller?.ViewportHeight ?? 300))),
            Key.PageDown => Hit(new Point(52, line.Top + (Scroller?.ViewportHeight ?? 300))),
            _ => -1
        };
        if (shift && !control && e.Key is Key.Left or Key.Right)
        {
            var tableNext = TableSelection.Arrow(snapshot, rawAnchor, Active, e.Key == Key.Right, ref tableArrowBoundary, ref tableArrowRight);
            if (tableNext >= 0) next = tableNext;
        }
        else if (next >= 0) tableArrowBoundary = -1;
        if (next < 0) return; Select(shift ? rawAnchor : next, next, shift && e.Key is Key.Left or Key.Up or Key.Home or Key.PageUp); Reveal(Active); e.Handled = true;
    }
}
