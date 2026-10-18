using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using System.ComponentModel;
using System.Collections;

namespace LensDocsStudio.Native.Controls;

/// <summary>Workspace presentation only; no diagram semantics or portable source.</summary>
public static class MermaidWorkspace
{
    public static ICollectionView Navigation(IEnumerable entries)
    {
        var view = CollectionViewSource.GetDefaultView(entries);
        view.GroupDescriptions.Add(new PropertyGroupDescription("Section")); return view;
    }
    public static readonly DependencyProperty AdjustableSourceProperty = DependencyProperty.RegisterAttached(
        "AdjustableSource", typeof(bool), typeof(MermaidWorkspace), new PropertyMetadata(false, SourceChanged));
    private static readonly DependencyProperty ExpandedHeightProperty = DependencyProperty.RegisterAttached(
        "ExpandedHeight", typeof(double), typeof(MermaidWorkspace), new PropertyMetadata(190d));
    private static readonly DependencyProperty InspectorIdentityProperty = DependencyProperty.RegisterAttached(
        "InspectorIdentity", typeof(string), typeof(MermaidWorkspace));
    private static readonly DependencyProperty InspectorScrollProperty = DependencyProperty.RegisterAttached(
        "InspectorScroll", typeof(System.Windows.Threading.DispatcherOperation), typeof(MermaidWorkspace));
    private static readonly DependencyProperty NavigationScrollProperty = DependencyProperty.RegisterAttached(
        "NavigationScroll", typeof(System.Windows.Threading.DispatcherOperation), typeof(MermaidWorkspace));
    public static bool GetAdjustableSource(DependencyObject value) => (bool)value.GetValue(AdjustableSourceProperty);
    public static void SetAdjustableSource(DependencyObject value, bool enabled) => value.SetValue(AdjustableSourceProperty, enabled);
    private static void SourceChanged(DependencyObject value, DependencyPropertyChangedEventArgs args)
    {
        if (value is not Expander panel) return;
        if ((bool)args.OldValue) { panel.Loaded -= SourceLayout; panel.Expanded -= SourceLayout; panel.Collapsed -= SourceLayout; }
        if ((bool)args.NewValue) { panel.Loaded += SourceLayout; panel.Expanded += SourceLayout; panel.Collapsed += SourceLayout; }
    }
    private static void SourceLayout(object sender, RoutedEventArgs args)
    {
        if (sender is not Expander panel || panel.Parent is not Grid grid) return;
        var index = Grid.GetRow(panel); if (index < 1 || index >= grid.RowDefinitions.Count) return;
        var row = grid.RowDefinitions[index];
        if (!panel.IsExpanded && row.ActualHeight > 40) panel.SetValue(ExpandedHeightProperty, row.ActualHeight);
        row.Height = new GridLength(panel.IsExpanded ? Math.Max(96, (double)panel.GetValue(ExpandedHeightProperty)) : 40);
        grid.RowDefinitions[index - 1].Height = new GridLength(panel.IsExpanded ? 6 : 0);
        foreach (var splitter in grid.Children.OfType<GridSplitter>().Where(item => Grid.GetRow(item) == index - 1))
            splitter.Visibility = panel.IsExpanded ? Visibility.Visible : Visibility.Collapsed;
        // Expanding source changes available space, never source/caret, private selection or zoom.
    }
    public static void Inspect(ScrollViewer inspector, string? identity)
    {
        if (Equals(inspector.GetValue(InspectorIdentityProperty), identity)) return;
        CancelInspectorScroll(inspector, new RoutedEventArgs());
        inspector.SetValue(InspectorIdentityProperty, identity); inspector.ScrollToTop();
        inspector.Unloaded -= CancelInspectorScroll; inspector.Unloaded += CancelInspectorScroll;
        var operation = inspector.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
        {
            inspector.ClearValue(InspectorScrollProperty);
            if (inspector.IsLoaded && Equals(inspector.GetValue(InspectorIdentityProperty), identity)) inspector.ScrollToTop();
        });
        inspector.SetValue(InspectorScrollProperty, operation);
    }
    private static void CancelInspectorScroll(object sender, RoutedEventArgs args)
    {
        if (sender is not ScrollViewer inspector) return;
        if (inspector.GetValue(InspectorScrollProperty) is System.Windows.Threading.DispatcherOperation operation) operation.Abort();
        inspector.ClearValue(InspectorScrollProperty);
    }
    public static void RevealNavigation(ListBox list, object item)
    {
        CancelNavigationScroll(list, new RoutedEventArgs());
        list.ScrollIntoView(item); list.Unloaded -= CancelNavigationScroll; list.Unloaded += CancelNavigationScroll;
        var operation = list.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, () =>
        {
            list.ClearValue(NavigationScrollProperty);
            if (!list.IsLoaded || !list.SelectedItems.Contains(item)) return;
            // Grouped, virtualised lists can settle a stale offset after a rejected
            // keyboard selection. Reveal the realised container after that layout.
            list.ScrollIntoView(item);
            if (list.Items.IndexOf(item) == 0) NavigationViewport(list)?.ScrollToTop();
            else if (list.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem entry) entry.BringIntoView();
        });
        list.SetValue(NavigationScrollProperty, operation);
    }
    private static void CancelNavigationScroll(object sender, RoutedEventArgs args)
    {
        if (sender is not ListBox list) return;
        if (list.GetValue(NavigationScrollProperty) is System.Windows.Threading.DispatcherOperation operation) operation.Abort();
        list.ClearValue(NavigationScrollProperty);
    }
    private static ScrollViewer? NavigationViewport(DependencyObject parent)
    {
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, index);
            if (child is ScrollViewer viewport) return viewport;
            if (NavigationViewport(child) is { } nested) return nested;
        }
        return null;
    }
    public static void Reveal(FrameworkElement element, ScrollViewer viewport)
    {
        if (!element.IsDescendantOf(viewport) || !element.IsVisible || element.RenderSize.Width <= 0) return;
        var bounds = element.TransformToAncestor(viewport).TransformBounds(new Rect(element.RenderSize));
        static bool Visible(double start, double length, double extent) => start >= 0 && start + length <= extent
            || length > extent && start <= extent - 24 && start + length >= 24;
        if (!Visible(bounds.Left, bounds.Width, viewport.ViewportWidth))
            viewport.ScrollToHorizontalOffset(viewport.HorizontalOffset + bounds.Left + bounds.Width / 2 - viewport.ViewportWidth / 2);
        if (!Visible(bounds.Top, bounds.Height, viewport.ViewportHeight))
            viewport.ScrollToVerticalOffset(viewport.VerticalOffset + bounds.Top + bounds.Height / 2 - viewport.ViewportHeight / 2);
    }
    public static void ZoomAtPointer(ScrollViewer viewport, Slider zoom, MouseWheelEventArgs args)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        var point = args.GetPosition(viewport); var before = zoom.Value;
        var next = Math.Clamp(before * (args.Delta > 0 ? 1.15 : 1 / 1.15), zoom.Minimum, zoom.Maximum);
        var factor = next / before;
        var horizontal = (viewport.HorizontalOffset + point.X) * factor - point.X;
        var vertical = (viewport.VerticalOffset + point.Y) * factor - point.Y;
        zoom.Value = next;
        viewport.ScrollToHorizontalOffset(horizontal); viewport.ScrollToVerticalOffset(vertical); args.Handled = true;
    }
}
