using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation.Peers;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Native.Rendering;

/// <summary>Native reading modes with bounded first-page layout and page-sized media.</summary>
public sealed class DocumentPreview : FlowDocumentReader
{
    protected override AutomationPeer OnCreateAutomationPeer() => new DocumentPreviewAutomationPeer(this);

    private sealed class DocumentPreviewAutomationPeer(DocumentPreview owner) : FlowDocumentReaderAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Document;

        public override object? GetPattern(PatternInterface patternInterface)
        {
            if (patternInterface != PatternInterface.Text) return base.GetPattern(patternInterface);
            // Keep the preview's existing TextPattern contract in both native reading modes.
            // The framework reader normally exposes this pattern only on its document child.
            GetChildren();
            if (owner.Document is null) return null;
            var documentPeer = ContentElementAutomationPeer.CreatePeerForElement(owner.Document);
            if (documentPeer is null) return null;
            documentPeer.EventsSource = this;
            return documentPeer.GetPattern(patternInterface);
        }
    }

    private PreviewResult? preview;
    private int sourceCharacters;
    private string readingMode = "Automatic";
    private DynamicDocumentPaginator? paginator;
    private DispatcherOperation? fitting;
    private bool replacing;

    public DocumentPreview()
    {
        DefaultStyleKey = typeof(FlowDocumentReader);
        ViewingMode = FlowDocumentReaderViewingMode.Scroll;
        IsFindEnabled = false; IsPrintEnabled = false; IsTwoPageViewEnabled = false;
        // Find keeps keyboard focus in the query box while revealing its preview match.
        IsInactiveSelectionHighlightEnabled = true;
        Loaded += (_, _) => QueueFit();
        Unloaded += (_, _) => StopPagination();
        SizeChanged += (_, _) => QueueFit();
        IsVisibleChanged += (_, _) => QueueFit();
    }

    public void SetPreview(PreviewResult result, int characters, string preference)
    {
        if (!WorkspaceSettings.ReadingModes.Contains(preference)) throw new ArgumentException("Choose a supported reading mode.");
        replacing = true;
        try
        {
            StopPagination();
            preview = result; sourceCharacters = characters; readingMode = preference;
            ViewingMode = ResolveMode();
            Document = result.Document;
        }
        finally { replacing = false; }
        QueueFit();
    }

    public void ApplyReadingMode(string preference)
    {
        if (!WorkspaceSettings.ReadingModes.Contains(preference)) throw new ArgumentException("Choose a supported reading mode.");
        readingMode = preference;
        ViewingMode = ResolveMode();
        QueueFit();
    }

    private FlowDocumentReaderViewingMode ResolveMode() => readingMode == "Pages"
        || readingMode == "Automatic" && (sourceCharacters >= 256 * 1024 || preview?.SourceElements.Count >= 1000)
        ? FlowDocumentReaderViewingMode.Page : FlowDocumentReaderViewingMode.Scroll;

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (replacing) return;
        if (e.Property == DocumentProperty)
        {
            StopPagination();
            if (preview?.Document != Document) { preview = null; sourceCharacters = 0; }
        }
        if (e.Property == DocumentProperty || e.Property == ViewingModeProperty || e.Property == ZoomProperty) QueueFit();
    }

    private void QueueFit()
    {
        if (replacing || Dispatcher.HasShutdownStarted || fitting?.Status == DispatcherOperationStatus.Pending) return;
        fitting = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, FitMedia);
    }

    private void FitMedia()
    {
        fitting = null;
        if (Document is null || !IsLoaded) { StopPagination(); return; }
        var paged = ViewingMode != FlowDocumentReaderViewingMode.Scroll;
        if (paged)
        {
            if (paginator is null)
            {
                paginator = (DynamicDocumentPaginator)((IDocumentPaginatorSource)Document).DocumentPaginator;
                paginator.PaginationProgress += PaginationProgress;
            }
            paginator.IsBackgroundPaginationEnabled = IsVisible;
        }
        else StopPagination();
        if (preview?.Document != Document) return;
        var host = Template?.FindName("PART_ContentHost", this) as FrameworkElement;
        var scale = Math.Max(Zoom, 1) / 100;
        var width = Math.Max(1, (host?.ActualWidth > 0 ? host.ActualWidth : ActualWidth) / scale - Document.PagePadding.Left - Document.PagePadding.Right);
        var height = Math.Max(1, (host?.ActualHeight > 0 ? host.ActualHeight : ActualHeight) / scale - Document.PagePadding.Top - Document.PagePadding.Bottom);
        foreach (var media in preview.Media)
        {
            if (paged)
            {
                var actionsHeight = 0d;
                if (media.Actions is { } actions)
                {
                    // The page scroller allows horizontal movement, so measure the same
                    // unwrapped toolbar it will arrange instead of reserving fictitious rows.
                    actions.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    actionsHeight = actions.DesiredSize.Height;
                }
                media.Image.MaxWidth = Math.Min(media.ScrollMaximumWidth, width);
                media.Image.MaxHeight = Math.Min(media.ScrollMaximumHeight, Math.Max(1, height - actionsHeight - 32));
                if (media.Host is { } container)
                {
                    if (media.PageScroller is null)
                    {
                        var content = container.Child; container.Child = null;
                        var scroller = new ScrollViewer { Content = content, CanContentScroll = false, IsTabStop = false,
                            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                        scroller.PreviewMouseWheel += PageMediaWheel;
                        media.PageScroller = scroller; container.Child = scroller;
                    }
                    media.PageScroller.MaxWidth = width;
                    media.PageScroller.MaxHeight = Math.Max(1, height - 16);
                }
            }
            else
            {
                media.Image.MaxWidth = media.ScrollMaximumWidth;
                media.Image.MaxHeight = media.ScrollMaximumHeight;
                if (media.PageScroller is { } scroller && media.Host is { } container)
                {
                    var content = (UIElement)scroller.Content; scroller.Content = null;
                    scroller.PreviewMouseWheel -= PageMediaWheel;
                    container.Child = null; container.Child = content; media.PageScroller = null;
                }
            }
        }
    }

    private void PageMediaWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrolling || Keyboard.Modifiers != ModifierKeys.None) return;
        var atEdge = e.Delta > 0 ? scrolling.VerticalOffset <= 0 : scrolling.VerticalOffset >= scrolling.ScrollableHeight;
        if (!atEdge) return;
        var command = e.Delta > 0 ? NavigationCommands.PreviousPage : NavigationCommands.NextPage;
        if (command.CanExecute(null, this)) { e.Handled = true; command.Execute(null, this); }
    }

    private void PaginationProgress(object? sender, PaginationProgressEventArgs e)
    {
        if (preview?.Media.Count > 0) QueueFit();
    }

    private void StopPagination()
    {
        if (paginator is null) return;
        paginator.PaginationProgress -= PaginationProgress;
        paginator.IsBackgroundPaginationEnabled = false;
        paginator = null;
    }
}
