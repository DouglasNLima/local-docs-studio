using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LensDocsStudio.Native.Rendering;
using SharpVectors.Runtime;
using Path = System.Windows.Shapes.Path;

namespace LensDocsStudio.Native;

public partial class StateVisualEditorWindow
{
    private readonly Dictionary<string, ToggleButton> buttons = [];
    private readonly Dictionary<string, TranslateTransform> translations = [];
    private readonly Dictionary<string, Point> sessionPositions = [];
    private readonly Dictionary<long, Dictionary<string, Point>> layoutHistory = [];
    private readonly Dictionary<string, EdgeRuntime> edgeRuntime = [];
    private ToggleButton? dragOwner;
    private string? draggingId;
    private Point dragStart, nodeStart;
    private Point? previousPosition;
    private Vector pendingDrag;
    private bool dragging;
    private DispatcherOperation? dragFrame;
    private long dragAllocated, dragTimestamp, frameAllocated;
    private int frameCount;
    private double maximumFrame;
    private static readonly Pen HitPen = CreateHitPen();
    private static readonly DoubleCollection GuideDashes = CreateDashes();
    private sealed record EdgeRuntime(NativeMermaidConnection Canonical, string From, string To, DrawingGroup? Drawing, DrawingGroup? Label,
        LineGeometry Guide, Path Overlay, TranslateTransform LabelTranslation, Point Start, Point End);
    private sealed class DrawingHost(Drawing drawing, Point origin) : FrameworkElement
    {
        protected override void OnRender(DrawingContext context)
        { context.PushTransform(new TranslateTransform(-origin.X, -origin.Y)); context.DrawDrawing(drawing); context.Pop(); }
    }
    private sealed class ElementButton : ToggleButton
    {
        protected override AutomationPeer OnCreateAutomationPeer() => new ElementPeer(this);
    }
    private sealed class ElementPeer(ElementButton owner) : ToggleButtonAutomationPeer(owner), ISelectionItemProvider
    {
        public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.SelectionItem ? this : base.GetPattern(patternInterface);
        bool ISelectionItemProvider.IsSelected => owner.IsChecked == true;
        IRawElementProviderSimple? ISelectionItemProvider.SelectionContainer => null;
        void ISelectionItemProvider.AddToSelection() => Select();
        void ISelectionItemProvider.Select() => Select();
        void ISelectionItemProvider.RemoveFromSelection() => owner.Dispatcher.Invoke(() => owner.IsChecked = false);
        private void Select() => owner.Dispatcher.Invoke(() => { if (!owner.IsEnabled) throw new ElementNotEnabledException(); owner.IsChecked = true; });
    }
    private static Pen CreateHitPen() { var pen = new Pen(Brushes.Black, 12); pen.Freeze(); return pen; }
    private static DoubleCollection CreateDashes() { var dashes = new DoubleCollection { 3, 2 }; dashes.Freeze(); return dashes; }
    private Brush BrushResource(string key) => (Brush)FindResource(key);
    private static IEnumerable<DrawingGroup> DrawingGroups(Drawing drawing)
    { if (drawing is not DrawingGroup group) yield break; yield return group; foreach (var child in group.Children) foreach (var nested in DrawingGroups(child)) yield return nested; }
    private static bool FreezeStatic(Drawing drawing, HashSet<DrawingGroup> mutable)
    {
        if (drawing is not DrawingGroup group) { if (drawing.CanFreeze) drawing.Freeze(); return false; }
        var keep = mutable.Contains(group); foreach (var child in group.Children) keep |= FreezeStatic(child, mutable);
        if (!keep && group.CanFreeze) group.Freeze(); return keep;
    }
    private void BuildCanvas()
    {
        if (presentation is not { } state) return; CancelPointer();
        var view = state.Canonical; Surface.Children.Clear(); buttons.Clear(); translations.Clear(); edgeRuntime.Clear();
        var mutable = view.Drawing.Clone(); var nodeDrawings = new Dictionary<string, DrawingGroup>(); var edges = new Dictionary<int, DrawingGroup>(); var labels = new Dictionary<int, DrawingGroup>();
        var labelled = view.Connections.Where(item => view.Graph.Edges[item.Ordinal].Label is not null).ToArray();
        foreach (var group in DrawingGroups(mutable.Drawing))
        {
            var id = SvgObject.GetId(group);
            if (id?.StartsWith("lens-node-", StringComparison.Ordinal) == true && int.TryParse(id.AsSpan(10), out var ordinal) && ordinal < view.Graph.Nodes.Count)
                nodeDrawings["node:" + view.Graph.Nodes[ordinal].Id] = group;
            if (id?.StartsWith("lens-edge-", StringComparison.Ordinal) == true && int.TryParse(id.AsSpan(10), out var edge) && edge < view.Connections.Count) edges[edge] = group;
            if (id?.StartsWith("lens-label-", StringComparison.Ordinal) == true && int.TryParse(id.AsSpan(11), out var label) && label < labelled.Length) labels[labelled[label].Ordinal] = group;
        }
        foreach (var pair in nodeDrawings)
        {
            if (!state.Elements.TryGetValue(pair.Key, out var identity) || !identity.StartsWith("state:", StringComparison.Ordinal)) continue;
            var translation = new TranslateTransform(); pair.Value.Transform = new TransformGroup { Children = { pair.Value.Transform ?? Transform.Identity, translation } }; translations[identity] = translation;
        }
        var labelTransforms = new Dictionary<int, TranslateTransform>();
        foreach (var pair in labels) { var translation = new TranslateTransform(); pair.Value.Transform = new TransformGroup { Children = { pair.Value.Transform ?? Transform.Identity, translation } }; labelTransforms[pair.Key] = translation; }
        FreezeStatic(mutable.Drawing, nodeDrawings.Values.Concat(edges.Values).Concat(labels.Values).ToHashSet());
        var host = new DrawingHost(mutable.Drawing, view.Drawing.Drawing.Bounds.Location) { Width = view.Drawing.Width, Height = view.Drawing.Height, IsHitTestVisible = false };
        AutomationProperties.SetAutomationId(host, "StateCanonicalDrawing"); Panel.SetZIndex(host, -10); Surface.Children.Add(host);
        var descriptions = Choices().ToDictionary(item => item.Identity, item => item.Description);
        foreach (var element in view.Elements.Values)
        {
            if (!state.Elements.TryGetValue(element.Key, out var identity)) continue;
            var bounds = element.Bounds; var geometry = element.Geometry.Clone();
            geometry.Transform = new TransformGroup { Children = { element.Geometry.Transform ?? Transform.Identity, new TranslateTransform(-bounds.X, -bounds.Y) } }; geometry.Freeze();
            var shape = new Path { Data = geometry, Fill = identity.StartsWith("group:", StringComparison.Ordinal) ? null : Brushes.Transparent, Stroke = Brushes.Transparent, StrokeThickness = 2, Stretch = Stretch.None };
            var description = descriptions.GetValueOrDefault(identity) ?? identity;
            var button = Button(identity, description, shape, bounds); Panel.SetZIndex(button, identity.StartsWith("group:", StringComparison.Ordinal) ? 0 : 10);
            if (translations.TryGetValue(identity, out var translation)) button.RenderTransform = translation;
            button.PreviewMouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true; button.Focus();
                if (canvasPick is not null && identity.StartsWith("state:", StringComparison.Ordinal)) { PickState(identity[6..]); return; }
                Select(identity);
                if (!CanEdit || PropertiesPending || !draft.Model.IsSimple || !translations.ContainsKey(identity) || draft.Selection != identity) return;
                StartDrag(button, identity, e.GetPosition(Surface));
            };
            button.PreviewMouseMove += (_, e) => MoveDrag(e);
            button.PreviewMouseLeftButtonUp += (_, e) => { if (ReferenceEquals(dragOwner, button)) { EndDrag(false); e.Handled = true; } };
            button.LostMouseCapture += (_, _) => { if (ReferenceEquals(dragOwner, button)) EndDrag(true); };
        }
        foreach (var edge in view.Connections)
        {
            var source = draft.Model.Transitions[edge.Ordinal]; var identity = source.Identity;
            var path = new Path { Data = edge.Path, Stroke = Brushes.Transparent, StrokeThickness = 2, IsHitTestVisible = false };
            Panel.SetZIndex(path, 5); Surface.Children.Add(path);
            var flat = edge.Path.GetFlattenedPathGeometry(); flat.GetPointAtFractionLength(0, out var start, out _); flat.GetPointAtFractionLength(1, out var end, out _);
            edgeRuntime[identity] = new(edge, source.Source, source.Destination, edges.GetValueOrDefault(edge.Ordinal), labels.GetValueOrDefault(edge.Ordinal), new(start, end), path, labelTransforms.GetValueOrDefault(edge.Ordinal) ?? new(), start, end);
            var bounds = edge.LabelBounds ?? edge.Path.Bounds; bounds.Inflate(5, 5);
            var button = Button(identity, "Transition " + (edge.Ordinal + 1) + ": " + EndpointName(source.Source) + " to " + EndpointName(source.Destination) + ". " + source.Label, null, bounds);
            // Precise native path tests below handle the pointer. The accessible alternative retains SelectionItem/Toggle and focus.
            button.IsHitTestVisible = edge.LabelBounds is not null; Panel.SetZIndex(button, 6);
            if (edge.LabelBounds is not null) { button.Content = new System.Windows.Shapes.Rectangle { Fill = Brushes.Transparent }; button.RenderTransform = edgeRuntime[identity].LabelTranslation; }
        }
        foreach (var identity in sessionPositions.Keys.Where(item => !translations.ContainsKey(item)).ToArray()) sessionPositions.Remove(identity);
        foreach (var pair in sessionPositions) Position(pair.Key, pair.Value);
        UpdateSessionEdges(); UpdateSelection(); UpdateExtent();
    }
    private ToggleButton Button(string identity, string description, object? content, Rect bounds)
    {
        var button = new ElementButton { Content = content, Width = Math.Max(10, bounds.Width), Height = Math.Max(10, bounds.Height), Style = (Style)FindResource("Mermaid.CanonicalElement"), ToolTip = description };
        AutomationProperties.SetName(button, description); AutomationProperties.SetAutomationId(button, "StateCanvas_" + identity);
        button.Checked += (_, _) => { if (!updating) Select(identity); };
        button.Click += (_, _) => { if (!updating) Select(identity); };
        button.MouseEnter += (_, _) => { if (button.Content is Path shape && button.IsChecked != true) shape.Stroke = BrushResource("BorderBrush"); };
        button.MouseLeave += (_, _) => UpdateSelection();
        Canvas.SetLeft(button, bounds.X); Canvas.SetTop(button, bounds.Y); Surface.Children.Add(button); buttons[identity] = button; return button;
    }
    private void UpdateSelection()
    {
        var previous = updating; updating = true;
        try
        {
            foreach (var pair in buttons) { pair.Value.IsChecked = pair.Key == draft.Selection; if (pair.Value.Content is Path shape) shape.Stroke = pair.Value.IsChecked == true ? BrushResource("AccentBrush") : Brushes.Transparent; }
            foreach (var pair in edgeRuntime) pair.Value.Overlay.Stroke = pair.Key == draft.Selection ? BrushResource("AccentBrush") : pair.Value.Drawing?.Opacity == 0 ? BrushResource("MutedTextBrush") : Brushes.Transparent;
        }
        finally { updating = previous; }
    }
    private void CanvasPointer(object sender, MouseButtonEventArgs e)
    {
        if (presentation is null || busy || e.ChangedButton != MouseButton.Left) return; var point = e.GetPosition(Surface);
        if (presentation.Elements.Any(pair => !pair.Key.StartsWith("group:", StringComparison.Ordinal) && presentation.Canonical.Elements[pair.Key].Geometry.FillContains(point - Offset(pair.Value)))) return;
        var hits = edgeRuntime.Where(pair => pair.Value.Overlay.Data.StrokeContains(HitPen, point) || pair.Value.Canonical.LabelBounds is { } bounds && bounds.Contains(point - new Vector(pair.Value.LabelTranslation.X, pair.Value.LabelTranslation.Y))).Select(pair => pair.Key).ToArray();
        if (hits.Length == 0) return; e.Handled = true;
        if (hits.Length == 1) { Select(hits[0]); return; }
        var menu = new ContextMenu(); foreach (var identity in hits) { var item = new MenuItem { Header = Choices().Single(choice => choice.Identity == identity).Description }; item.Click += (_, _) => Select(identity); menu.Items.Add(item); } menu.IsOpen = true;
    }
    private void PickState(string id)
    {
        if (!CanEdit) return;
        if (canvasPick is "from" or "from-final" or "from-reconnect") { From.SelectedValue = id; canvasPick = canvasPick == "from" ? "to" : null; }
        else { To.SelectedValue = id; canvasPick = null; }
        Status.Text = canvasPick == "to" ? "Choose the destination state; choose the same state for a self-transition." : "Endpoints selected. Update properties validates and commits one private edit."; UpdateActions();
    }
    private Vector Offset(string identity) => translations.TryGetValue(identity, out var transform) ? new(transform.X, transform.Y) : new();
    private void StartDrag(ToggleButton button, string identity, Point start)
    {
        CancelPointer(); dragOwner = button; draggingId = identity; dragStart = start; pendingDrag = new(); dragging = false;
        var element = presentation!.Canonical.Elements["node:" + identity[6..]]; nodeStart = new(element.Bounds.X + element.Bounds.Width / 2, element.Bounds.Y + element.Bounds.Height / 2); nodeStart += Offset(identity);
        previousPosition = sessionPositions.TryGetValue(identity, out var position) ? position : null;
        frameCount = 0; frameAllocated = 0; maximumFrame = 0; dragAllocated = GC.GetAllocatedBytesForCurrentThread(); dragTimestamp = Stopwatch.GetTimestamp();
        if (!button.CaptureMouse()) EndDrag(true);
    }
    private void MoveDrag(MouseEventArgs e)
    {
        if (dragOwner is null) return; if (e.LeftButton != MouseButtonState.Pressed) { EndDrag(false); return; }
        pendingDrag = e.GetPosition(Surface) - dragStart;
        if (!dragging && Math.Abs(pendingDrag.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(pendingDrag.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        dragging = true; e.Handled = true; dragOwner.Cursor = Cursors.SizeAll;
        if (dragFrame is null || dragFrame.Status != DispatcherOperationStatus.Pending) dragFrame = Dispatcher.BeginInvoke(DragFrame, DispatcherPriority.Render);
    }
    private void DragFrame()
    {
        dragFrame = null; if (!dragging || draggingId is null) return;
        var allocated = GC.GetAllocatedBytesForCurrentThread(); var timestamp = Stopwatch.GetTimestamp();
        Position(draggingId, new(Math.Max(10, nodeStart.X + pendingDrag.X), Math.Max(10, nodeStart.Y + pendingDrag.Y))); UpdateSessionEdges(); UpdateSelection();
        frameAllocated += GC.GetAllocatedBytesForCurrentThread() - allocated; maximumFrame = Math.Max(maximumFrame, Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds); frameCount++;
    }
    private void Position(string identity, Point centre)
    {
        if (!translations.TryGetValue(identity, out var transform) || presentation is null) return;
        var bounds = presentation.Canonical.Elements["node:" + identity[6..]].Bounds; transform.X = centre.X - bounds.X - bounds.Width / 2; transform.Y = centre.Y - bounds.Y - bounds.Height / 2; sessionPositions[identity] = centre;
    }
    private void UpdateSessionEdges()
    {
        foreach (var edge in edgeRuntime.Values)
        {
            var from = Offset("state:" + edge.From); var to = Offset("state:" + edge.To); var moved = from.LengthSquared > .001 || to.LengthSquared > .001;
            if (edge.Drawing is not null) edge.Drawing.Opacity = moved ? 0 : 1;
            edge.Guide.StartPoint = edge.Start + from; edge.Guide.EndPoint = edge.End + to; edge.Overlay.Data = moved ? edge.Guide : edge.Canonical.Path;
            edge.Overlay.StrokeDashArray = moved ? GuideDashes : null;
            edge.LabelTranslation.X = (from.X + to.X) / 2; edge.LabelTranslation.Y = (from.Y + to.Y) / 2;
        }
    }
    private void EndDrag(bool cancel)
    {
        dragFrame?.Abort(); dragFrame = null; if (dragOwner is null || draggingId is null) return;
        if (cancel) { Position(draggingId, nodeStart); if (previousPosition is { } previous) sessionPositions[draggingId] = previous; else sessionPositions.Remove(draggingId); UpdateSessionEdges(); }
        else if (dragging) { DragFrame(); SavePositions(); }
        var owner = dragOwner; dragOwner = null; draggingId = null; dragging = false; owner.Cursor = null; if (owner.IsMouseCaptured) owner.ReleaseMouseCapture(); UpdateSelection(); UpdateExtent();
        if (frameCount > 0)
        { Status.Text = cancel ? "Movement cancelled. Source is unchanged." : "Temporary position retained. Apply saves portable source; Auto layout restores canonical positions.";
          AutomationProperties.SetHelpText(Status, $"State drag frames: {frameCount}; maximum update: {maximumFrame:F3} ms; update allocated bytes: {frameAllocated}; UI-thread allocated bytes: {GC.GetAllocatedBytesForCurrentThread() - dragAllocated}; duration: {Stopwatch.GetElapsedTime(dragTimestamp).TotalMilliseconds:F1} ms."); }
    }
    private void CancelPointer() { EndDrag(true); }
    private void UpdateExtent()
    {
        if (presentation is null) return; var width = presentation.Canonical.Drawing.Width; var height = presentation.Canonical.Drawing.Height;
        foreach (var pair in sessionPositions) if (presentation.Canonical.Elements.TryGetValue("node:" + pair.Key[6..], out var element)) { width = Math.Max(width, pair.Value.X + element.Bounds.Width); height = Math.Max(height, pair.Value.Y + element.Bounds.Height); }
        Surface.Width = width; Surface.Height = height;
    }
    private void SavePositions() { layoutHistory[draft.HistoryIdentity] = new(sessionPositions); while (layoutHistory.Count > 100) layoutHistory.Remove(layoutHistory.Keys.Min()); }
    private void RestorePositions() { sessionPositions.Clear(); if (layoutHistory.TryGetValue(draft.HistoryIdentity, out var positions)) foreach (var pair in positions) sessionPositions[pair.Key] = pair.Value; }
}
