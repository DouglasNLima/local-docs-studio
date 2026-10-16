using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public partial class MermaidVisualEditorWindow
{
    // Coordinates are canvas DIPs. WPF GetPosition/TranslatePoint account for layout scale,
    // scrolling, centring and device DPI exactly once. Saved source never contains positions.
    private readonly Dictionary<string, Point> sessionPositions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, Point>> layoutHistory = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TranslateTransform> nodeTransforms = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ConnectionRuntime> connectionRuntime = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> incidentConnections = new(StringComparer.Ordinal);
    private static readonly Pen connectionHitPen = FrozenHitPen();
    private sealed record ConnectionRuntime(NativeMermaidConnection Canonical, Point Start, Point End, LineGeometry Guide);
    private ToggleButton? dragOwner;
    private DispatcherOperation? dragFrame;
    private Vector pendingDrag;
    private bool dragging;
    private string[] affectedConnections = [];
    private long dragAllocated, dragTicks, frameAllocated;
    private int dragFrames;
    private double maximumDragFrame;
    private (string Key, Point Centre)? pendingNodePlacement;
    private Button? sourceHandle, targetHandle;
    private string? reconnectEdge;
    private bool reconnectSource;
    private Button? reconnectOwner;
    private System.Windows.Shapes.Ellipse? insertionMarker;

    private sealed class CanonicalDrawingHost(Drawing drawing, Point origin) : FrameworkElement
    {
        protected override void OnRender(DrawingContext context)
        {
            // Image normalises a DrawingImage's changing bounds on every movement. Keep
            // the canonical origin fixed instead, including when a node exceeds those bounds.
            context.PushTransform(new TranslateTransform(-origin.X, -origin.Y));
            context.DrawDrawing(drawing); context.Pop();
        }
    }
    private static bool FreezeStaticDrawing(Drawing drawing, HashSet<DrawingGroup> mutable)
    {
        if (drawing is not DrawingGroup group) { if (drawing.CanFreeze) drawing.Freeze(); return false; }
        var keepMutable = mutable.Contains(group);
        foreach (var child in group.Children) keepMutable |= FreezeStaticDrawing(child, mutable);
        if (!keepMutable && group.CanFreeze) group.Freeze();
        return keepMutable;
    }
    private static Pen FrozenHitPen() { var pen = new Pen(Brushes.Black, 12); pen.Freeze(); return pen; }
    private string ConnectionKey(NativeMermaidConnection edge) => canonicalMapped ? draft.Model.Edges[edge.Ordinal].Id : "inspect-edge-" + edge.Ordinal;
    private static bool Contains(MermaidLayoutBox box, Point point) => point.X >= box.X && point.X <= box.X + box.Width && point.Y >= box.Y && point.Y <= box.Y + box.Height;

    private void CacheConnection(string key, NativeMermaidConnection edge)
    {
        // Flatten once per canonical render, never during pointer input.
        var geometry = edge.Path.GetFlattenedPathGeometry();
        geometry.GetPointAtFractionLength(0, out var start, out _); geometry.GetPointAtFractionLength(1, out var end, out _);
        connectionRuntime[key] = new(edge, start, end, new(start, end));
        foreach (var node in new[] { "node:" + edge.Source, "node:" + edge.Target })
        {
            if (!incidentConnections.TryGetValue(node, out var edges)) incidentConnections[node] = edges = new(StringComparer.Ordinal);
            edges.Add(key);
        }
    }
    private void StartNodeDrag(ToggleButton owner, Point start)
    {
        CancelPointerInteraction(); dragOwner = owner; dragStart = start; dragging = false;
        dragged = draft.Selection.Where(key => key.StartsWith("node:", StringComparison.Ordinal) && nodeTransforms.ContainsKey(key))
            .ToDictionary(key => key, key => positions[key]);
        affectedConnections = dragged.Keys.Where(incidentConnections.ContainsKey).SelectMany(key => incidentConnections[key]).Distinct().ToArray();
        dragFrames = 0; frameAllocated = 0; maximumDragFrame = 0; dragAllocated = GC.GetAllocatedBytesForCurrentThread(); dragTicks = Stopwatch.GetTimestamp();
        if (!owner.CaptureMouse()) FinishNodeDrag(true);
    }
    private void MoveNodeDrag(MouseEventArgs e)
    {
        if (dragStart is not { } start || dragged is null) return;
        if (e.LeftButton != MouseButtonState.Pressed) { FinishNodeDrag(true); return; }
        pendingDrag = e.GetPosition(DiagramCanvas) - start;
        if (!dragging && Math.Abs(pendingDrag.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(pendingDrag.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        dragging = true; e.Handled = true;
        dragOwner!.Cursor = Cursors.SizeAll;
        if (dragFrame is null || dragFrame.Status != DispatcherOperationStatus.Pending)
            dragFrame = Dispatcher.BeginInvoke(ApplyDragFrame, DispatcherPriority.Render);
    }
    private void ApplyDragFrame()
    {
        dragFrame = null;
        if (!dragging || dragged is null) return;
        var watch = Stopwatch.GetTimestamp(); var allocated = GC.GetAllocatedBytesForCurrentThread();
        foreach (var (key, original) in dragged)
            PositionNode(key, new(Math.Max(original.Width / 2, original.CentreX + pendingDrag.X), Math.Max(original.Height / 2, original.CentreY + pendingDrag.Y)));
        foreach (var key in affectedConnections) UpdateSessionConnection(key);
        UpdateEndpointHandlePositions();
        frameAllocated += GC.GetAllocatedBytesForCurrentThread() - allocated;
        dragFrames++; maximumDragFrame = Math.Max(maximumDragFrame, Stopwatch.GetElapsedTime(watch).TotalMilliseconds);
    }
    private void FinishNodeDrag(bool cancel)
    {
        dragFrame?.Abort(); dragFrame = null;
        if (dragged is null) return;
        if (!cancel && dragging) ApplyDragFrame();
        if (cancel)
        {
            foreach (var (key, original) in dragged) PositionNode(key, new(original.CentreX, original.CentreY));
            foreach (var key in affectedConnections) UpdateSessionConnection(key);
        }
        else if (dragging) SaveSessionLayout();
        var owner = dragOwner; dragged = null; dragOwner = null; dragStart = null; dragging = false; affectedConnections = [];
        if (owner is not null) { owner.Cursor = null; if (owner.IsMouseCaptured) owner.ReleaseMouseCapture(); }
        UpdateExtent(); UpdateCanvasSelection();
        if (dragFrames > 0)
        {
            Status.Text = cancel ? "Movement cancelled. Source is unchanged." : "Session position retained. Auto layout resets positions; Apply saves Mermaid source only.";
            AutomationProperties.SetHelpText(Status, $"Drag frames: {dragFrames}; maximum update: {maximumDragFrame:F3} ms; update allocated bytes: {frameAllocated}; UI-thread allocated bytes: {GC.GetAllocatedBytesForCurrentThread() - dragAllocated}; duration: {Stopwatch.GetElapsedTime(dragTicks).TotalMilliseconds:F1} ms. Includes other dispatcher work between frames.");
        }
    }
    private void PositionNode(string key, Point centre)
    {
        if (presentation is null || !positions.TryGetValue(key, out var box) || !nodeTransforms.TryGetValue(key, out var transform)) return;
        var canonical = presentation.Elements[key].Bounds;
        box = box with { X = centre.X - box.Width / 2, Y = centre.Y - box.Height / 2 };
        positions[key] = box; Canvas.SetLeft(canvasElements[key], box.X); Canvas.SetTop(canvasElements[key], box.Y);
        transform.X = box.X - canonical.X; transform.Y = box.Y - canonical.Y;
        if (ports.TryGetValue(key, out var port)) { Canvas.SetLeft(port, box.X + box.Width - 16); Canvas.SetTop(port, box.CentreY - 16); }
        sessionPositions[key] = centre;
    }
    private void UpdateSessionConnection(string key)
    {
        if (presentation is not { } view || !connectionRuntime.TryGetValue(key, out var runtime)) return;
        var connection = runtime.Canonical;
        var sourceKey = "node:" + connection.Source; var targetKey = "node:" + connection.Target;
        if (!positions.TryGetValue(sourceKey, out var source) || !positions.TryGetValue(targetKey, out var target)) return;
        var from = new Vector(source.X - view.Elements[sourceKey].Bounds.X, source.Y - view.Elements[sourceKey].Bounds.Y);
        var to = new Vector(target.X - view.Elements[targetKey].Bounds.X, target.Y - view.Elements[targetKey].Bounds.Y);
        var moved = from.LengthSquared > .0001 || to.LengthSquared > .0001;
        if (moved) sessionConnections.Add(key); else sessionConnections.Remove(key);
        if (edgeDrawings.TryGetValue(key, out var drawing)) drawing.Opacity = moved ? 0 : 1;
        runtime.Guide.StartPoint = runtime.Start + from; runtime.Guide.EndPoint = runtime.End + to;
        var geometry = moved ? (Geometry)runtime.Guide : connection.Path;
        connectionHitPaths[key].Data = geometry;
        var path = connectionPaths[key]; path.Data = geometry;
        path.StrokeDashArray = moved ? sessionGuideDashes : null;
        path.Stroke = draft.Selection.Contains(key) ? BrushResource("AccentBrush") : moved ? BrushResource("MutedTextBrush") : Brushes.Transparent;
        var labelOffset = (from + to) / 2;
        if (edgeLabelDrawings.TryGetValue(key, out var labelDrawing)) labelDrawing.Transform = new TranslateTransform(labelOffset.X, labelOffset.Y);
        if (connection.LabelBounds is { } label && canvasElements.TryGetValue(key, out var button))
        { Canvas.SetLeft(button, label.X + labelOffset.X); Canvas.SetTop(button, label.Y + labelOffset.Y); }
    }
    private static readonly DoubleCollection sessionGuideDashes = FrozenDashes();
    private static DoubleCollection FrozenDashes() { var dashes = new DoubleCollection([4, 3]); dashes.Freeze(); return dashes; }
    private void SaveSessionLayout()
    {
        layoutHistory[draft.Source] = new(sessionPositions, StringComparer.Ordinal);
        while (layoutHistory.Count > 100 || layoutHistory.Keys.Sum(source => source.Length) > 8_000_000 && layoutHistory.Count > 1) layoutHistory.Remove(layoutHistory.Keys.First());
    }
    private void RestoreSessionPositions()
    {
        foreach (var key in sessionPositions.Keys.Where(key => !nodeTransforms.ContainsKey(key)).ToArray()) sessionPositions.Remove(key);
        foreach (var (key, centre) in sessionPositions.ToArray()) PositionNode(key, centre);
        foreach (var key in sessionPositions.Keys.Where(incidentConnections.ContainsKey).SelectMany(key => incidentConnections[key]).Distinct()) UpdateSessionConnection(key);
    }
    private void RestoreHistoryLayout()
    {
        sessionPositions.Clear();
        if (layoutHistory.TryGetValue(draft.Source, out var saved)) foreach (var pair in saved) sessionPositions[pair.Key] = pair.Value;
    }
    private Point VisibleInsertionPoint() => Viewport.TranslatePoint(new(Viewport.ViewportWidth / 2, Viewport.ViewportHeight / 2), DiagramCanvas);
    private void ShowInsertionMarker(Point point, bool valid)
    {
        if (insertionMarker is null || !DiagramCanvas.Children.Contains(insertionMarker))
        {
            insertionMarker = new System.Windows.Shapes.Ellipse { Width = 20, Height = 20, StrokeThickness = 2, Fill = Brushes.Transparent, IsHitTestVisible = false };
            insertionMarker.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "AccentBrush");
            Panel.SetZIndex(insertionMarker, 50); DiagramCanvas.Children.Add(insertionMarker);
        }
        insertionMarker.Visibility = valid ? Visibility.Visible : Visibility.Collapsed;
        Canvas.SetLeft(insertionMarker, point.X - 10); Canvas.SetTop(insertionMarker, point.Y - 10);
    }
    private string? IntendedGroup(Point point)
    {
        // Only an explicitly selected container with verified membership authorises insertion.
        return Selected?.StartsWith("group:", StringComparison.Ordinal) == true && positions.TryGetValue(Selected, out var box) && Contains(box, point)
            ? Selected[6..] : null;
    }
    private async Task InsertPositionedNode(string id, FlowNodeShape shape, Point point, string? group = null)
    {
        var key = "node:" + id;
        pendingNodePlacement = (key, point);
        try { await Execute(new AddMermaidNode(id, shape == FlowNodeShape.Terminal ? "Start / End" : shape.ToString(), shape, group), key); }
        finally { pendingNodePlacement = null; }
    }
    private async Task InsertPositionedSubgraph(string id, Point point, string? parent)
    {
        pendingNodePlacement = ("group:" + id, point);
        try { await Execute(new AddMermaidSubgraph(id, "New subgraph", parent), "group:" + id); }
        finally { pendingNodePlacement = null; }
    }
    private void CancelPointerInteraction()
    {
        FinishNodeDrag(true); CancelReconnection();
        if (panStart is not null) { panStart = null; if (Viewport.IsMouseCaptured) Viewport.ReleaseMouseCapture(); Viewport.Cursor = null; }
        if (insertionMarker is not null) insertionMarker.Visibility = Visibility.Collapsed;
    }

    private void InstallInspectionChoices(NativeMermaidPresentation view)
    {
        updating = true;
        try
        {
            choices = view.Graph.Nodes.Select(node => new ElementChoice("node:" + node.Id, node.Id + " · " + node.Label.ReplaceLineEndings(" "), "Process"))
                .Concat(view.Connections.Select(edge => new ElementChoice(ConnectionKey(edge), "Connection " + (edge.Ordinal + 1) + " · " + edge.Source + " → " + edge.Target + " · " + edge.Label, "Connection")))
                .Concat(view.Elements.Keys.Where(key => key.StartsWith("group:", StringComparison.Ordinal)).Select(key => new ElementChoice(key, "Subgraph · " + key[6..], "Subgraph"))).ToArray();
            Elements.ItemsSource = choices;
            foreach (var choice in choices.Where(choice => draft.Selection.Contains(choice.Key))) Elements.SelectedItems.Add(choice);
        }
        finally { updating = false; }
    }
    private static string InspectionText(string key, NativeMermaidPresentation view)
    {
        if (key.StartsWith("node:", StringComparison.Ordinal))
        {
            var node = view.Graph.Nodes.Single(node => "node:" + node.Id == key);
            return "Node ID: " + node.Id + "\nLabel: " + node.Label + "\nShape: " + node.Shape;
        }
        if (key.StartsWith("inspect-edge-", StringComparison.Ordinal) && int.TryParse(key.AsSpan(13), out var ordinal))
        {
            var edge = view.Graph.Edges[ordinal];
            return "Connection " + (ordinal + 1) + "\nSource: " + edge.Source + "\nDestination: " + edge.Target + "\nLabel: " + edge.Label + "\nType: " + (edge.HasArrowStart && edge.HasArrowEnd ? "Bidirectional" : edge.HasArrowEnd ? "Directed" : edge.HasArrowStart ? "Arrow at source" : "Undirected") + " · " + edge.Style;
        }
        var group = view.Membership.SingleOrDefault(group => "group:" + group.Id == key);
        return "Subgraph ID: " + key[6..] + "\nLabel: " + InspectionGroups(view.Graph.Groups).Single(item => "group:" + item.Id == key).Label + "\nParent: " + (group?.ParentId ?? "Root") + "\nMembers: " + string.Join(", ", group?.NodeIds ?? []);
    }
    private static IEnumerable<Mermaider.Models.PositionedGroup> InspectionGroups(IEnumerable<Mermaider.Models.PositionedGroup> groups)
    { foreach (var group in groups) { yield return group; foreach (var child in InspectionGroups(group.Children)) yield return child; } }
    private void UpdateEndpointHandles()
    {
        if (sourceHandle is not null) DiagramCanvas.Children.Remove(sourceHandle);
        if (targetHandle is not null) DiagramCanvas.Children.Remove(targetHandle);
        sourceHandle = targetHandle = null;
        if (!CanTransform || PropertiesPending || Selected is not { } key || !connectionRuntime.ContainsKey(key)) return;
        sourceHandle = Handle(true); targetHandle = Handle(false); UpdateEndpointHandlePositions();
        Button Handle(bool source)
        {
            var edge = draft.Model.Edge(key);
            var button = new Button { Content = source ? "S" : "D", Width = 24, Height = 24, MinHeight = 24, Padding = new(0), Margin = new(0), Style = (Style)FindResource("Mermaid.Port"), Tag = true, Opacity = 1,
                Cursor = Cursors.Cross, ToolTip = "Drag the " + (source ? "source" : "destination") + " endpoint to a node. Properties provides the keyboard alternative." };
            AutomationProperties.SetAutomationId(button, source ? "MermaidSourceEndpoint" : "MermaidDestinationEndpoint");
            AutomationProperties.SetName(button, $"Connection {edge.Source.Id} to {edge.Target.Id}, {(source ? "source" : "destination")} endpoint");
            button.PreviewMouseLeftButtonDown += (_, e) =>
            {
                if (!CanTransform || PropertiesPending) return;
                connectionSource = null; CancelPointerInteraction(); reconnectEdge = key; reconnectSource = source; reconnectOwner = button;
                button.CaptureMouse(); e.Handled = true;
                if (connectionGuidePath is not null) connectionGuidePath.Visibility = Visibility.Visible;
                MoveReconnection(e.GetPosition(DiagramCanvas));
                Status.Text = "Reconnect " + (source ? "source" : "destination") + ": drop on a node; Escape cancels.";
            };
            button.PreviewMouseMove += (_, e) => { if (reconnectEdge is null) return; if (e.LeftButton != MouseButtonState.Pressed) { CancelReconnection(); return; } MoveReconnection(e.GetPosition(DiagramCanvas)); e.Handled = true; };
            button.PreviewMouseLeftButtonUp += async (_, e) =>
            {
                if (reconnectEdge is not { } edgeId) return;
                var target = ReconnectionTarget(e.GetPosition(DiagramCanvas)); var sourceEndpoint = reconnectSource;
                CancelReconnection(); e.Handled = true;
                if (target is null || !CanTransform || PropertiesPending) { Status.Text = "Reconnection cancelled. Drop on one editable node; source is unchanged."; return; }
                var original = draft.Model.Edge(edgeId);
                await Execute(new UpdateMermaidConnection(edgeId, sourceEndpoint ? target : original.Source.Id, sourceEndpoint ? original.Target.Id : target, original.Label, original.Type), edgeId);
            };
            button.LostMouseCapture += (_, _) => CancelReconnection();
            Panel.SetZIndex(button, 40); DiagramCanvas.Children.Add(button); return button;
        }
    }
    private string? ReconnectionTarget(Point point)
    {
        var matches = positions.Where(pair => pair.Key.StartsWith("node:", StringComparison.Ordinal) && Contains(pair.Value, point)
            && presentation?.Elements.TryGetValue(pair.Key, out var element) == true
            && element.Geometry.FillContains(point - new Vector(pair.Value.X - element.Bounds.X, pair.Value.Y - element.Bounds.Y))).Take(2).ToArray();
        return matches.Length == 1 ? matches[0].Key[5..] : null;
    }
    private void MoveReconnection(Point point)
    {
        if (reconnectEdge is not { } key || !connectionRuntime.TryGetValue(key, out var edge)) return;
        var geometry = sessionConnections.Contains(key) ? edge.Guide : new LineGeometry(edge.Start, edge.End);
        connectionGuide.StartPoint = reconnectSource ? geometry.EndPoint : geometry.StartPoint; connectionGuide.EndPoint = point;
        var target = ReconnectionTarget(point);
        if (reconnectOwner is not null) reconnectOwner.Cursor = target is null ? Cursors.No : Cursors.Cross;
        foreach (var (nodeKey, button) in canvasElements)
            if (nodeKey.StartsWith("node:", StringComparison.Ordinal) && button.Content is System.Windows.Shapes.Path shape)
                shape.Stroke = nodeKey == "node:" + target || draft.Selection.Contains(nodeKey) ? BrushResource("AccentBrush") : Brushes.Transparent;
    }
    private void UpdateEndpointHandlePositions()
    {
        if (Selected is not { } key || !connectionRuntime.TryGetValue(key, out var runtime)) return;
        var start = sessionConnections.Contains(key) ? runtime.Guide.StartPoint : runtime.Start;
        var end = sessionConnections.Contains(key) ? runtime.Guide.EndPoint : runtime.End;
        if (sourceHandle is not null) { Canvas.SetLeft(sourceHandle, start.X - 12); Canvas.SetTop(sourceHandle, start.Y - 12); }
        if (targetHandle is not null) { Canvas.SetLeft(targetHandle, end.X - 12); Canvas.SetTop(targetHandle, end.Y - 12); }
    }
    private void CancelReconnection()
    {
        var owner = reconnectOwner; reconnectOwner = null; reconnectEdge = null;
        if (owner is not null) { owner.Cursor = Cursors.Cross; if (owner.IsMouseCaptured) owner.ReleaseMouseCapture(); }
        if (connectionGuidePath is not null && connectionSource is null) connectionGuidePath.Visibility = Visibility.Collapsed;
        if (owner is not null)
            foreach (var (key, button) in canvasElements)
                if (key.StartsWith("node:", StringComparison.Ordinal) && button.Content is System.Windows.Shapes.Path shape)
                    shape.Stroke = draft.Selection.Contains(key) ? BrushResource("AccentBrush") : Brushes.Transparent;
    }
}
