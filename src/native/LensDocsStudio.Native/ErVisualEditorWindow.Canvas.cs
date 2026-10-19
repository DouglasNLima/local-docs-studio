using System.Windows;
using System.Diagnostics;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using LensDocsStudio.Core.ErDiagrams;
using LensDocsStudio.Native.Rendering;
using Path = System.Windows.Shapes.Path;

namespace LensDocsStudio.Native;

public partial class ErVisualEditorWindow
{
    private readonly Dictionary<string, ToggleButton> buttons = [];
    private readonly List<FrameworkElement> endpointHandles = [];
    private (FrameworkElement Owner, string Identity, bool First)? endpointDrag;
    private LineGeometry? dragGuide;
    private Path? dragOverlay;
    private int endpointFrames;
    private long endpointAllocated, endpointStarted;
    private long endpointUpdateAllocated;
    private double endpointMaximumUpdate;
    private static readonly Pen HitPen = HitTestPen();
    private static Pen HitTestPen() { var pen = new Pen(Brushes.Black, 12); pen.Freeze(); return pen; }
    private sealed class ElementButton : ToggleButton { protected override AutomationPeer OnCreateAutomationPeer() => new ElementPeer(this); }
    private sealed class ElementPeer(ElementButton owner) : ToggleButtonAutomationPeer(owner), ISelectionItemProvider
    {
        public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.SelectionItem ? this : base.GetPattern(patternInterface);
        bool ISelectionItemProvider.IsSelected => owner.IsChecked == true;
        IRawElementProviderSimple? ISelectionItemProvider.SelectionContainer => null;
        void ISelectionItemProvider.AddToSelection() => Select(); void ISelectionItemProvider.Select() => Select();
        void ISelectionItemProvider.RemoveFromSelection() => owner.Dispatcher.Invoke(() => owner.IsChecked = false);
        private void Select() => owner.Dispatcher.Invoke(() => { if (!owner.IsEnabled) throw new ElementNotEnabledException(); owner.IsChecked = true; });
    }
    private void BuildCanvas()
    {
        using var overlayPhase = LensDocsStudio.Core.Rendering.MermaidDiagnostics.Measure("editor.overlay.Er");
        if (presentation is not { } view) return; CancelPointer(); Surface.Children.Clear(); buttons.Clear(); endpointHandles.Clear();
        if (view.Entities.Count > 0)
        {
            var image = new Image { Source = view.Drawing, Stretch = Stretch.None, IsHitTestVisible = false }; AutomationProperties.SetAutomationId(image, "ErCanonicalDrawing"); Surface.Children.Add(image);
        }
        Surface.Width = view.Entities.Count == 0 ? 320 : view.Drawing.Width; Surface.Height = view.Entities.Count == 0 ? 180 : view.Drawing.Height;
        if (view.Entities.Count == 0) Surface.Children.Add(new TextBlock { Text = "Add an entity to start this ER diagram.", Foreground = (Brush)FindResource("MutedTextBrush"), Margin = new(16), IsHitTestVisible = false });
        var descriptions = Choices().GroupBy(item => item.Identity).ToDictionary(group => group.Key, group => group.First().Description);
        foreach (var item in view.Entities) AddHit(item, descriptions.GetValueOrDefault(item.Identity) ?? item.Identity, 10);
        foreach (var item in view.Attributes) AddHit(item, descriptions.GetValueOrDefault(item.Identity) ?? item.Identity, 20);
        foreach (var relationship in view.Relationships)
        {
            var bounds = relationship.LabelBounds ?? relationship.Path.Bounds; bounds.Inflate(3, 3);
            // The canonical renderer paints labels after entity boxes.
            var button = AddHit(new(relationship.Identity, bounds), descriptions[relationship.Identity], relationship.LabelBounds is null ? 5 : 25);
            button.IsHitTestVisible = relationship.LabelBounds is not null;
        }
        UpdateSelection();
    }
    private ToggleButton AddHit(NativeErHit hit, string description, int z)
    {
        var rectangle = new Rectangle { Fill = Brushes.Transparent, Stroke = Brushes.Transparent, StrokeThickness = 2 };
        var button = new ElementButton { Content = rectangle, Width = Math.Max(10, hit.Bounds.Width), Height = Math.Max(10, hit.Bounds.Height), Style = (Style)FindResource("Mermaid.CanonicalElement"), ToolTip = description };
        AutomationProperties.SetAutomationId(button, "ErCanvas_" + hit.Identity); AutomationProperties.SetName(button, description);
        button.Checked += (_, _) => { if (!updating) Select(hit.Identity); }; button.Click += (_, _) => { if (!updating) Select(hit.Identity); };
        button.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (canvasPick is not null && hit.Identity.StartsWith("entity:", StringComparison.Ordinal)) { PickEntity(hit.Identity[7..]); e.Handled = true; return; }
            Select(hit.Identity); e.Handled = true; button.Focus();
        };
        Canvas.SetLeft(button, hit.Bounds.X); Canvas.SetTop(button, hit.Bounds.Y); Panel.SetZIndex(button, z); Surface.Children.Add(button); buttons[hit.Identity] = button; return button;
    }
    private void UpdateSelection()
    {
        var previous = updating; updating = true;
        try { foreach (var pair in buttons) { pair.Value.IsChecked = pair.Key == draft.Selection; if (pair.Value.Content is Rectangle shape) shape.Stroke = pair.Key == draft.Selection ? (Brush)FindResource("AccentBrush") : Brushes.Transparent; } }
        finally { updating = previous; }
        foreach (var handle in endpointHandles) Surface.Children.Remove(handle); endpointHandles.Clear();
        if (presentation?.Relationships.FirstOrDefault(item => item.Identity == draft.Selection) is not { } selected) return;
        var overlay = new Path { Data = selected.Path, Stroke = (Brush)FindResource("AccentBrush"), StrokeThickness = 2, IsHitTestVisible = false }; Surface.Children.Add(overlay); endpointHandles.Add(overlay); Panel.SetZIndex(overlay, 25);
        AddEndpoint(selected, true); AddEndpoint(selected, false);
    }
    private void AddEndpoint(NativeErConnection relationship, bool first)
    {
        var point = first ? relationship.First : relationship.Second;
        var handle = new Button { Content = new Ellipse { Fill = (Brush)FindResource("PanelBrush"), Stroke = (Brush)FindResource("AccentBrush"), StrokeThickness = 2 }, Width = 14, Height = 14, Padding = new(0), ToolTip = "Drag the " + (first ? "first" : "second") + " endpoint to another entity; its cardinality stays at this relationship endpoint." };
        AutomationProperties.SetAutomationId(handle, first ? "ErFirstEndpoint" : "ErSecondEndpoint"); AutomationProperties.SetName(handle, first ? "Reconnect first relationship endpoint" : "Reconnect second relationship endpoint");
        handle.Click += (_, _) => { canvasPick = first ? "from-reconnect" : "to"; Status.Text = "Choose another entity on the canvas, then Update properties."; };
        handle.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (!CanEdit || PropertiesPending) return; CancelPointer(); endpointDrag = (handle, relationship.Identity, first);
            endpointFrames = 0; endpointAllocated = GC.GetAllocatedBytesForCurrentThread(); endpointStarted = Stopwatch.GetTimestamp();
            endpointUpdateAllocated = 0; endpointMaximumUpdate = 0;
            dragGuide = new(relationship.First, relationship.Second); dragOverlay = new() { Data = dragGuide, Stroke = (Brush)FindResource("AccentBrush"), StrokeThickness = 2, IsHitTestVisible = false };
            Surface.Children.Add(dragOverlay); Panel.SetZIndex(dragOverlay, 40); if (!handle.CaptureMouse()) CancelPointer(); e.Handled = true;
        };
        handle.PreviewMouseMove += (_, e) =>
        {
            if (endpointDrag is not { } drag || dragGuide is null) return; if (e.LeftButton != MouseButtonState.Pressed) { CancelPointer(); return; }
            var allocated = GC.GetAllocatedBytesForCurrentThread(); var started = Stopwatch.GetTimestamp();
            if (drag.First) dragGuide.StartPoint = e.GetPosition(Surface); else dragGuide.EndPoint = e.GetPosition(Surface); endpointFrames++; e.Handled = true;
            endpointUpdateAllocated += GC.GetAllocatedBytesForCurrentThread() - allocated; endpointMaximumUpdate = Math.Max(endpointMaximumUpdate, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        };
        handle.PreviewMouseLeftButtonUp += async (_, e) =>
        {
            if (endpointDrag is not { } drag || presentation is null) return; var pointAtDrop = e.GetPosition(Surface); var hit = presentation.Entities.Where(item => item.Bounds.Contains(pointAtDrop)).ToArray();
            CancelPointer(); e.Handled = true;
            if (hit.Length != 1) { Status.Text = "Drop on exactly one entity. The relationship, source and history are unchanged."; return; }
            var relationshipModel = draft.Model.Relationship(drag.Identity); var id = hit[0].Identity[7..];
            await Execute(new UpdateErRelationship(drag.Identity, drag.First ? id : relationshipModel.Entity1, drag.First ? relationshipModel.Entity2 : id, relationshipModel.Cardinality1, relationshipModel.Cardinality2, relationshipModel.Identifying, relationshipModel.Label));
        };
        handle.LostMouseCapture += (_, _) => { if (endpointDrag is not null) CancelPointer(); };
        Canvas.SetLeft(handle, point.X - 7); Canvas.SetTop(handle, point.Y - 7); Panel.SetZIndex(handle, 30); Surface.Children.Add(handle); endpointHandles.Add(handle);
    }
    private void CancelPointer()
    {
        var owner = endpointDrag?.Owner; endpointDrag = null; if (owner?.IsMouseCaptured == true) owner.ReleaseMouseCapture(); if (dragOverlay is not null) Surface.Children.Remove(dragOverlay); dragOverlay = null; dragGuide = null;
        if (owner is not null && endpointFrames > 0) AutomationProperties.SetHelpText(Status, $"ER endpoint drag updates: {endpointFrames}; maximum update: {endpointMaximumUpdate:F3} ms; update allocated bytes: {endpointUpdateAllocated}; UI-thread allocated bytes: {GC.GetAllocatedBytesForCurrentThread() - endpointAllocated}; duration: {Stopwatch.GetElapsedTime(endpointStarted).TotalMilliseconds:F1} ms. No parsing, layout or SVG conversion during movement.");
    }
    private void CanvasPointer(object sender, MouseButtonEventArgs e)
    {
        if (presentation is null || busy || endpointDrag is not null || e.ChangedButton != MouseButton.Left) return; var point = e.GetPosition(Surface);
        // Endpoint controls own capture. Selecting the underlying path here would rebuild
        // and detach that control before its tunnelling pointer handler can execute.
        if (e.OriginalSource is DependencyObject target && endpointHandles.OfType<Button>().Any(handle => ReferenceEquals(handle, target) || handle.IsAncestorOf(target))) return;
        var hits = presentation.Relationships.Where(item => item.LabelBounds?.Contains(point) == true).ToArray();
        if (hits.Length == 0)
        {
            if (presentation.Entities.Any(item => item.Bounds.Contains(point))) return;
            hits = presentation.Relationships.Where(item => item.Path.StrokeContains(HitPen, point)).ToArray();
        }
        if (hits.Length == 0) return;
        e.Handled = true; if (hits.Length == 1) { Select(hits[0].Identity); return; }
        var menu = new ContextMenu(); foreach (var hit in hits) { var item = new MenuItem { Header = Choices().First(choice => choice.Identity == hit.Identity).Description }; item.Click += (_, _) => Select(hit.Identity); menu.Items.Add(item); } menu.IsOpen = true;
    }
    private void PickEntity(string id)
    {
        if (!CanEdit) return;
        if (canvasPick is "from" or "from-reconnect") { From.SelectedValue = id; canvasPick = canvasPick == "from" ? "to" : null; } else { To.SelectedValue = id; canvasPick = null; }
        Status.Text = canvasPick == "to" ? "Choose the second entity. Selecting the same entity creates a self-relationship." : "Endpoints selected. Update properties validates one private edit."; UpdateActions();
    }
}
