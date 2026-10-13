using System.Diagnostics;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ContinuousViewer.Model;
using LensDocsStudio.Core.Parsing;

namespace ContinuousViewer;

public sealed record ViewAnchor(int Piece, double RelativeTop, int Anchor, int Focus);

/// <summary>Experimental, separate native viewport. Logical text and selection outlive every recycled control.</summary>
public sealed class VirtualDocumentView : ListBox, IDisposable
{
    public LogicalDocument? Document { get; private set; }
    public LogicalSelection? Selection { get; private set; }
    public IReadOnlyDictionary<int, PiecePresenter> Realised => realised;
    public IReadOnlyDictionary<int, ImageSource> Media { get; private set; } = new Dictionary<int, ImageSource>();
    public double MaximumMaterialisationMilliseconds { get; internal set; }
    public int PeakRealised { get; private set; }
    public long Materialisations { get; internal set; }
    public bool Dark { get; private set; }
    public double TextSize { get; private set; } = 15;
    public event Action<int>? SourceActivated;
    public event Action<string>? LinkActivated;
    private readonly Dictionary<int, PiecePresenter> realised = [];
    private readonly HashSet<int> expanded = [];
    private readonly Stack<ViewAnchor> back = [], forward = [];
    private readonly DispatcherTimer dragTimer;
    private int revision;
    private bool dragging;

    public VirtualDocumentView()
    {
        Focusable = true; Background = Brushes.White; BorderThickness = new Thickness(0); Padding = new Thickness(20, 8, 20, 8);
        ScrollViewer.SetCanContentScroll(this, true);
        ScrollViewer.SetHorizontalScrollBarVisibility(this, ScrollBarVisibility.Auto);
        VirtualizingPanel.SetIsVirtualizing(this, true);
        VirtualizingPanel.SetVirtualizationMode(this, VirtualizationMode.Recycling);
        VirtualizingPanel.SetScrollUnit(this, ScrollUnit.Pixel);
        VirtualizingPanel.SetCacheLength(this, new VirtualizationCacheLength(0.5));
        VirtualizingPanel.SetCacheLengthUnit(this, VirtualizationCacheLengthUnit.Page);
        ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel)));
        var presenter = new FrameworkElementFactory(typeof(PiecePresenter));
        presenter.SetBinding(PiecePresenter.ItemProperty, new Binding());
        ItemTemplate = new DataTemplate { VisualTree = presenter };
        var style = new Style(typeof(ListBoxItem));
        style.Setters.Add(new Setter(PaddingProperty, new Thickness(0)));
        style.Setters.Add(new Setter(MarginProperty, new Thickness(0)));
        style.Setters.Add(new Setter(HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        style.Setters.Add(new Setter(FocusableProperty, false));
        ItemContainerStyle = style;
        PreviewKeyDown += OnKey;
        PreviewMouseLeftButtonDown += OnDown;
        PreviewMouseLeftButtonUp += (_, e) => { if (dragging) { dragging = false; dragTimer!.Stop(); ReleaseMouseCapture(); e.Handled = true; } };
        PreviewMouseMove += (_, e) => { if (dragging && Selection is not null) { Selection.Set(Selection.Anchor, Hit(e.GetPosition(this))); e.Handled = true; } };
        dragTimer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(35) };
        dragTimer.Tick += (_, _) =>
        {
            var point = Mouse.GetPosition(this); var scroll = Scroller;
            if (scroll is null || Selection is null) return;
            if (point.Y < 20) scroll.ScrollToVerticalOffset(Math.Max(0, scroll.VerticalOffset - 25));
            else if (point.Y > ActualHeight - 20) scroll.ScrollToVerticalOffset(scroll.VerticalOffset + 25);
            else return;
            UpdateLayout(); Selection.Set(Selection.Anchor, Hit(point));
        };
    }
    public ScrollViewer? Scroller => Descendants<ScrollViewer>(this).FirstOrDefault();
    public void SetDocument(LogicalDocument document, IReadOnlyDictionary<int, ImageSource>? media = null)
    {
        revision++; dragging = false; dragTimer.Stop(); ReleaseMouseCapture();
        if (Selection is not null) Selection.Changed -= LogicalSelectionChanged;
        ItemsSource = null; realised.Clear(); back.Clear(); forward.Clear(); expanded.Clear();
        Document = document; Media = media ?? new Dictionary<int, ImageSource>();
        Selection = new(document); Selection.Changed += LogicalSelectionChanged;
        foreach (var group in document.Groups.Where(g => g.InitiallyExpanded)) expanded.Add(group.Id);
        MaximumMaterialisationMilliseconds = 0; Materialisations = 0; PeakRealised = 0;
        RefreshItems(); Scroller?.ScrollToTop();
    }
    private void RefreshItems() => ItemsSource = Document?.Pieces.Where(p => p.Details.All(expanded.Contains)).ToArray();
    public bool IsExpanded(int group) => expanded.Contains(group);
    public void Toggle(int group)
    {
        var anchor = CaptureAnchor(); revision++;
        if (!expanded.Remove(group)) expanded.Add(group);
        RefreshItems(); RestoreAnchor(anchor);
    }
    public ViewAnchor CaptureAnchor()
    {
        var first = realised.Values.Where(p => p.IsLoaded && p.Piece is not null)
            .Select(p => (p.Piece!, Top: p.TranslatePoint(new Point(), this).Y)).Where(p => p.Top + 1 >= 0).OrderBy(p => p.Top).FirstOrDefault();
        return new(first.Item1?.Id ?? 0, first.Top, Selection?.Anchor ?? 0, Selection?.Focus ?? 0);
    }
    public void RestoreAnchor(ViewAnchor anchor)
    {
        if (Document is null || Selection is null || Document.Pieces.Length == 0) return;
        Selection.Set(anchor.Anchor, anchor.Focus);
        var piece = Document.Pieces[Math.Clamp(anchor.Piece, 0, Document.Pieces.Length - 1)];
        ScrollIntoView(piece); UpdateLayout();
        if (realised.TryGetValue(piece.Id, out var presenter) && Scroller is { } scroll)
        {
            var delta = presenter.TranslatePoint(new Point(), this).Y - anchor.RelativeTop;
            scroll.ScrollToVerticalOffset(Math.Max(0, scroll.VerticalOffset + delta)); UpdateLayout();
        }
    }
    public async Task<bool> NavigateAsync(int source, bool select = false, CancellationToken cancellation = default, bool history = true)
    {
        var document = Document; if (document is null || document.Pieces.Length == 0) return false;
        var piece = document.AtSource(source); if (history) { back.Push(CaptureAnchor()); forward.Clear(); }
        var request = ++revision;
        var reveal = false; foreach (var group in piece.Details) reveal |= expanded.Add(group);
        if (reveal) RefreshItems();
        cancellation.ThrowIfCancellationRequested(); ScrollIntoView(piece);
        await Dispatcher.Yield(DispatcherPriority.Render);
        cancellation.ThrowIfCancellationRequested(); if (request != revision || Document != document) return false;
        UpdateLayout();
        if (!realised.TryGetValue(piece.Id, out var presenter)) return false;
        if (select) Selection!.Set(piece.Start, piece.End);
        var top = presenter.TranslatePoint(new Point(), this).Y;
        return top < ActualHeight && top + presenter.ActualHeight > 0;
    }
    public Task<bool> NavigateAnchorAsync(string anchor, CancellationToken cancellation = default)
    {
        var target = Document?.Parsed.Targets.FirstOrDefault(t => t.Id == anchor.TrimStart('#'));
        return target is null ? Task.FromResult(false) : NavigateAsync(target.SourceStart, cancellation: cancellation);
    }
    public void History(bool previous)
    {
        var origin = previous ? back : forward; var destination = previous ? forward : back;
        if (origin.TryPop(out var anchor)) { destination.Push(CaptureAnchor()); RestoreAnchor(anchor); }
    }
    public void SetAppearance(bool dark, double fontSize)
    {
        var anchor = CaptureAnchor(); Dark = dark; TextSize = Math.Clamp(fontSize, 10, 32);
        Background = dark ? new SolidColorBrush(Color.FromRgb(32, 33, 36)) : Brushes.White;
        foreach (var presenter in realised.Values.ToArray()) presenter.Rebuild();
        RestoreAnchor(anchor);
    }
    internal void Register(PiecePresenter presenter, LogicalPiece piece)
    { realised[piece.Id] = presenter; PeakRealised = Math.Max(PeakRealised, realised.Count); }
    internal void Unregister(PiecePresenter presenter, LogicalPiece? piece)
    { if (piece is not null && realised.GetValueOrDefault(piece.Id) == presenter) realised.Remove(piece.Id); }
    private void LogicalSelectionChanged(object? sender, EventArgs args)
    {
        foreach (var presenter in realised.Values.ToArray()) presenter.Highlight();
        if (UIElementAutomationPeer.FromElement(this) is VirtualDocumentPeer peer)
            peer.RaiseAutomationEvent(AutomationEvents.TextPatternOnTextSelectionChanged);
    }
    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        if (Document is null || Selection is null || Document.Pieces.Length == 0) return;
        if (Ancestor<Button>(e.OriginalSource as DependencyObject) is not null) return;
        var offset = Hit(e.GetPosition(this));
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) { SourceActivated?.Invoke(SourceAt(offset)); e.Handled = true; return; }
        Focus(); Selection.Set((Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? Selection.Anchor : offset, offset);
        dragging = true; CaptureMouse(); dragTimer.Start(); e.Handled = true;
    }
    public int SourceAt(int offset)
    {
        var piece = Document!.At(offset); var run = piece.Runs.FirstOrDefault(r => offset >= r.Start && offset <= r.Start + r.Text.Length);
        return run is null ? piece.SourceStart : run.SourceEnd - run.SourceStart + 1 == run.Text.Length
            ? Math.Min(run.SourceEnd, run.SourceStart + offset - run.Start) : run.SourceStart;
    }
    public int Hit(Point point)
    {
        if (Selection is null) return 0;
        var nearest = realised.Values.Where(p => p.Piece is not null).OrderBy(p =>
            Math.Abs(p.TranslatePoint(new Point(0, p.ActualHeight / 2), this).Y - point.Y)).FirstOrDefault();
        return nearest?.Hit(TranslatePoint(point, nearest)) ?? Selection.Focus;
    }
    public void ActivateLinkAt(int offset)
    {
        var run = Document?.At(offset).Runs.FirstOrDefault(r => offset >= r.Start && offset < r.Start + r.Text.Length && r.Link is not null);
        if (run?.Link is { } link) { if (link.StartsWith('#')) _ = NavigateAnchorAsync(link); else LinkActivated?.Invoke(link); }
    }
    private void OnKey(object sender, KeyEventArgs e)
    {
        if (Selection is null || Document is null || Document.Pieces.Length == 0) return;
        var shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0; var control = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        switch (e.Key)
        {
            case Key.A when control: Selection.Set(0, Document.Text.Length); break;
            case Key.C when control: CopyToClipboard(); break;
            case Key.Left: Selection.Move(-1, shift); RevealFocus(); break;
            case Key.Right: Selection.Move(1, shift); RevealFocus(); break;
            case Key.Home: Selection.Set(shift ? Selection.Anchor : 0, 0); RevealFocus(); break;
            case Key.End: Selection.Set(shift ? Selection.Anchor : Document.Text.Length, Document.Text.Length); RevealFocus(); break;
            case Key.Up: case Key.Down:
                var id = Math.Clamp(Document.At(Selection.Focus).Id + (e.Key == Key.Up ? -1 : 1), 0, Document.Pieces.Length - 1);
                var next = Document.Pieces[id].Start; Selection.Set(shift ? Selection.Anchor : next, next); RevealFocus(); break;
            case Key.PageDown: case Key.PageUp:
                var scroll = Scroller; if (scroll is null) return;
                scroll.ScrollToVerticalOffset(Math.Max(0, scroll.VerticalOffset + (e.Key == Key.PageDown ? 1 : -1) * scroll.ViewportHeight));
                UpdateLayout(); if (shift) Selection.Set(Selection.Anchor, Hit(new Point(40, ActualHeight / 2))); break;
            case Key.Enter: ActivateLinkAt(Selection.Focus); break;
            default: return;
        }
        e.Handled = true;
    }
    private void RevealFocus()
    {
        var piece = Document!.At(Selection!.Focus); foreach (var group in piece.Details) expanded.Add(group);
        if (!Items.Contains(piece)) RefreshItems(); ScrollIntoView(piece);
    }
    public DataObject ClipboardObject()
    {
        var payload = Selection!.Copy(); var data = new DataObject();
        data.SetData(DataFormats.UnicodeText, payload.UnicodeText); data.SetData(DataFormats.Rtf, payload.Rtf);
        data.SetData(DataFormats.Html, payload.CfHtml); data.SetData("text/markdown", payload.Markdown); return data;
    }
    public void CopyToClipboard() => Clipboard.SetDataObject(ClipboardObject(), true);
    protected override AutomationPeer OnCreateAutomationPeer() => new VirtualDocumentPeer(this);
    public static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index); if (child is T value) yield return value;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
    private static T? Ancestor<T>(DependencyObject? element) where T : DependencyObject
    {
        while (element is not null) { if (element is T match) return match; element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element); }
        return null;
    }
    public void Dispose()
    {
        revision++; dragTimer.Stop(); ReleaseMouseCapture();
        if (Selection is not null) Selection.Changed -= LogicalSelectionChanged;
        ItemsSource = null; realised.Clear(); Document = null; Selection = null; Media = new Dictionary<int, ImageSource>();
    }
}

public sealed class PiecePresenter : FrameworkElement
{
    public static readonly DependencyProperty ItemProperty = DependencyProperty.Register("Item", typeof(object), typeof(PiecePresenter),
        new FrameworkPropertyMetadata(null, (owner, args) => ((PiecePresenter)owner).ItemChanged(args.NewValue)));
    public LogicalPiece? Piece { get; private set; }
    public RichTextBox? TextControl { get; private set; }
    private VirtualDocumentView? viewer;
    private readonly List<(int Offset, Run Run, RichTextBox Owner)> runs = [];
    private readonly List<RichTextBox> textControls = [];
    public PiecePresenter()
    {
        Loaded += (_, _) => { FindViewer(); Rebuild(); };
        Unloaded += (_, _) => viewer?.Unregister(this, Piece);
    }
    private void ItemChanged(object? newContent)
    {
        viewer?.Unregister(this, Piece); Piece = newContent as LogicalPiece;
        if (Piece is null) return; FindViewer(); Rebuild();
    }
    private void FindViewer() => viewer ??= ItemsControl.ItemsControlFromItemContainer(FindContainer(this)) as VirtualDocumentView;
    private static DependencyObject FindContainer(DependencyObject element)
    {
        var parent = VisualTreeHelper.GetParent(element);
        while (parent is not null && parent is not ListBoxItem) { element = parent; parent = VisualTreeHelper.GetParent(parent); }
        return parent ?? element;
    }
    public void Rebuild()
    {
        if (viewer?.Document is not { } logical || Piece is not { } piece) return;
        var watch = Stopwatch.StartNew(); runs.Clear(); textControls.Clear();
        var panel = new StackPanel();
        if (piece.Kind == PieceKind.Details)
        {
            var button = new Button { Content = (viewer.IsExpanded(piece.Group) ? "− " : "+ ") + logical.Text[piece.Start..piece.End].TrimEnd(),
                HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(6, 2, 6, 2) };
            button.Click += (_, _) => viewer.Toggle(piece.Group); panel.Children.Add(button);
        }
        if (piece.Kind == PieceKind.TableRow && piece.TableColumns > 0)
        {
            var grid = new Grid();
            for (var column = 0; column < piece.TableColumns; column++) grid.ColumnDefinitions.Add(new ColumnDefinition { MinWidth = 140, Width = new GridLength(1, GridUnitType.Star) });
            foreach (var cell in piece.Cells)
            {
                var cellText = CreateText(cell.Start, cell.Start + cell.Length, cell.Alignment switch { "Center" => TextAlignment.Center, "Right" => TextAlignment.Right, _ => TextAlignment.Left });
                var frame = new Border { BorderBrush = viewer.Dark ? Brushes.DimGray : Brushes.LightGray, BorderThickness = new Thickness(0.5), Padding = new Thickness(5), Child = cellText };
                Grid.SetColumn(frame, cell.Column); grid.Children.Add(frame);
            }
            panel.Children.Add(grid);
        }
        else
        {
            var end = piece.End; if (end > piece.Start && logical.Text[end - 1] == '\n') end--;
            panel.Children.Add(CreateText(piece.Start, end, TextAlignment.Left));
        }
        TextControl = textControls.FirstOrDefault();
        if (viewer.Media.TryGetValue(piece.Id, out var image)) panel.Children.Add(new Image { Source = image, MaxHeight = 220, MaxWidth = 560, Stretch = Stretch.Uniform });
        // Keep the data item as Content; the bounded native presentation is owned by this template.
        var border = new Border { Child = panel };
        AddVisualChildReplacement(border);
        viewer.Register(this, piece); viewer.Materialisations++;
        viewer.MaximumMaterialisationMilliseconds = Math.Max(viewer.MaximumMaterialisationMilliseconds, watch.Elapsed.TotalMilliseconds);

        RichTextBox CreateText(int begin, int end, TextAlignment alignment)
        {
            var paragraph = new Paragraph { Margin = new Thickness(0), LineHeight = viewer.TextSize * 1.4, TextAlignment = alignment };
            var document = new FlowDocument(paragraph) { FontFamily = new FontFamily(piece.Kind is PieceKind.Code or PieceKind.TableRow ? "Consolas" : "Segoe UI"),
                FontSize = piece.Kind == PieceKind.Heading ? viewer.TextSize + 10 - piece.Level : viewer.TextSize,
                Foreground = viewer.Dark ? Brushes.WhiteSmoke : Brushes.Black, Background = Brushes.Transparent,
                PagePadding = new Thickness(0), ColumnWidth = double.PositiveInfinity };
            var text = new RichTextBox(document) { IsReadOnly = true, IsDocumentEnabled = false, BorderThickness = new Thickness(0), Padding = new Thickness(0),
                Background = Brushes.Transparent, Focusable = false, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, MinHeight = viewer.TextSize * 1.4,
                Margin = new Thickness(piece.Kind is PieceKind.Quote or PieceKind.ListItem ? Math.Min(piece.Level, 12) * 12 : 0, 2, 0, 4) };
            System.Windows.Automation.AutomationProperties.SetName(text, logical.Text[begin..end]);
            System.Windows.Automation.AutomationProperties.SetAutomationId(text, "LogicalPiece" + piece.Id);
            textControls.Add(text);
            var cursor = begin;
            foreach (var run in piece.Runs.Where(r => r.Start < end && r.Start + r.Text.Length > begin))
            {
                if (run.Start > cursor) Add(logical.Text[cursor..Math.Min(run.Start, end)], cursor, null);
                var start = Math.Max(cursor, run.Start); var stop = Math.Min(end, run.Start + run.Text.Length);
                if (stop > start) Add(logical.Text[start..stop], start, run); cursor = stop;
            }
            if (cursor < end) Add(logical.Text[cursor..end], cursor, null);
            return text;
            void Add(string value, int offset, TextRun? style)
            {
                var selection = viewer.Selection!;
                var boundaries = new[] { 0, value.Length, Math.Clamp(selection.Start - offset, 0, value.Length), Math.Clamp(selection.End - offset, 0, value.Length) }.Distinct().Order().ToArray();
                for (var index = 0; index < boundaries.Length - 1; index++)
                {
                    var begin = boundaries[index]; var stop = boundaries[index + 1];
                    var native = new Run(value[begin..stop]);
                    if (style is not null)
                    {
                        if (style.Style.HasFlag(MarkdownEmphasis.Bold)) native.FontWeight = FontWeights.Bold;
                        if (style.Style.HasFlag(MarkdownEmphasis.Italic)) native.FontStyle = FontStyles.Italic;
                        if (style.Style.HasFlag(MarkdownEmphasis.Strike)) native.TextDecorations = TextDecorations.Strikethrough;
                        if (style.Style.HasFlag(MarkdownEmphasis.Underline) || style.Link is not null) native.TextDecorations = TextDecorations.Underline;
                        if (style.Code) native.FontFamily = new FontFamily("Consolas");
                    }
                    if (offset + begin < selection.End && offset + stop > selection.Start)
                        native.Background = new SolidColorBrush(Color.FromArgb(110, 255, 136, 62));
                    paragraph.Inlines.Add(native); runs.Add((offset + begin, native, text));
                }
            }
        }
    }
    private UIElement? presentation;
    private void AddVisualChildReplacement(UIElement element)
    {
        if (presentation is not null) { RemoveVisualChild(presentation); RemoveLogicalChild(presentation); }
        presentation = element; AddVisualChild(element); AddLogicalChild(element); InvalidateMeasure();
    }
    protected override int VisualChildrenCount => presentation is null ? 0 : 1;
    protected override Visual GetVisualChild(int index) => index == 0 && presentation is not null ? presentation : throw new ArgumentOutOfRangeException(nameof(index));
    protected override Size MeasureOverride(Size availableSize)
    {
        // Creating empty zero-height items until Loaded lets the panel walk the entire document.
        // Materialise synchronously inside the actual bounded item's first measure.
        FindViewer(); if (presentation is null && Piece is not null) Rebuild();
        var width = Math.Max(120, (viewer?.ActualWidth > 0 ? viewer.ActualWidth : HarnessSupport.Width) - 64);
        if (Piece?.Kind == PieceKind.TableRow) width = Math.Max(width, Piece.TableColumns * 140);
        presentation?.Measure(new Size(width, availableSize.Height));
        return new Size(width, presentation?.DesiredSize.Height ?? 24);
    }
    protected override Size ArrangeOverride(Size finalSize) { presentation?.Arrange(new Rect(finalSize)); return finalSize; }
    public int Hit(Point point)
    {
        if (Piece is null || TextControl is null) return 0;
        var text = textControls.OrderBy(c => Math.Abs(c.TranslatePoint(new Point(c.ActualWidth / 2, c.ActualHeight / 2), this).X - point.X)).First();
        var position = text.GetPositionFromPoint(TranslatePoint(point, text), true);
        if (position is null) return Piece.Start;
        foreach (var (offset, run, owner) in runs.Where(r => r.Owner == text))
        {
            if (position.CompareTo(run.ContentStart) < 0) return offset;
            if (position.CompareTo(run.ContentEnd) <= 0) return offset + Math.Clamp(run.ContentStart.GetOffsetToPosition(position), 0, run.Text.Length);
        }
        return Piece.End;
    }
    public Rect RangeRectangle(int start, int end)
    {
        if (Piece is null || TextControl is null) return Rect.Empty;
        var result = Rect.Empty;
        foreach (var (offset, run, owner) in runs.Where(r => r.Offset < end && r.Offset + r.Run.Text.Length > start))
        {
            var first = run.ContentStart.GetPositionAtOffset(Math.Max(0, start - offset))!.GetCharacterRect(LogicalDirection.Forward);
            var last = run.ContentStart.GetPositionAtOffset(Math.Min(run.Text.Length - 1, end - offset - 1))!.GetCharacterRect(LogicalDirection.Forward);
            if (first.IsEmpty || last.IsEmpty) continue; first.Union(last);
            result.Union(new Rect(owner.TranslatePoint(first.TopLeft, viewer!), first.Size));
        }
        return result;
    }
    private TextPointer? Pointer(int offset)
    {
        foreach (var (at, run, owner) in runs) if (offset >= at && offset <= at + run.Text.Length) return run.ContentStart.GetPositionAtOffset(offset - at);
        return TextControl?.Document.ContentEnd;
    }
    public void Highlight()
    {
        // Formatting a partial native Run can split it and invalidate pointer/source associations.
        // Rebuild only the bounded realised pieces from the authoritative logical selection instead.
        Rebuild();
    }
}
