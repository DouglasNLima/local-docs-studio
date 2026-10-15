using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Rendering;

namespace LensDocsStudio.Native;

public partial class MermaidVisualEditorWindow
{
    private readonly Dictionary<string, MermaidLayoutBox> positions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ToggleButton> canvasElements = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> ports = new(StringComparer.Ordinal);
    private readonly Dictionary<string, System.Windows.Shapes.Path> connectionPaths = new(StringComparer.Ordinal);
    private DispatcherOperation? connectionUpdate;
    private Point? dragStart, panStart;
    private Dictionary<string, MermaidLayoutBox>? dragged;
    private Point panOffsets;
    private bool dragging;
    private TextBox? inlineEditor;
    private bool synchronisingCanvasSelection;
    private Point? paletteStart;

    private void InitialisePaletteDragging()
    {
        Palette.PreviewMouseLeftButtonDown += (_, e) => paletteStart = e.GetPosition(Palette);
        Palette.PreviewMouseLeftButtonUp += (_, _) => paletteStart = null;
        DiagramCanvas.PreviewDragOver += (_, e) =>
        {
            e.Effects = CanTransform && !PropertiesPending && e.Data.GetDataPresent("Lens.MermaidElement") ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
    }

    private void RebuildCanvas(bool layout)
    {
        var arranged = MermaidGraphLayout.Arrange(draft.Model);
        if (layout) positions.Clear();
        foreach (var key in positions.Keys.Where(key => !arranged.ContainsKey(key)).ToArray()) positions.Remove(key);
        foreach (var (key, box) in arranged) if (!positions.ContainsKey(key) || key.StartsWith("group:", StringComparison.Ordinal)) positions[key] = box;
        DiagramCanvas.Children.Clear(); canvasElements.Clear(); ports.Clear(); connectionPaths.Clear(); inlineEditor = null;
        foreach (var group in draft.Model.Subgraphs)
        {
            var key = "group:" + group.Id; if (!positions.TryGetValue(key, out var box)) continue;
            var content = new Border { BorderThickness = new(1), CornerRadius = new(6), BorderBrush = BrushResource("BorderBrush"),
                Background = BrushResource("PanelBrush"), Child = new TextBlock { Text = group.Label, Margin = new(12, 6, 12, 0), VerticalAlignment = VerticalAlignment.Top, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis } };
            var button = ElementButton(key, "Subgraph " + group.Id + ": " + group.Label, content, box);
            button.Click += (_, _) => SelectElement(key, Keyboard.Modifiers == ModifierKeys.Control); Panel.SetZIndex(button, 0);
        }
        DrawConnections();
        foreach (var node in draft.Model.Nodes)
        {
            var key = "node:" + node.Id; if (!positions.TryGetValue(key, out var box)) continue;
            var palette = DiagramThemes.Colours(DiagramThemes.ResolveName(diagramTheme, dark));
            var (fill, stroke, text) = Colours(node, palette.Surface ?? palette.Background, palette.Border ?? palette.Accent, palette.Foreground);
            var grid = new Grid();
            grid.Children.Add(new System.Windows.Shapes.Path { Data = NodeGeometry(node.Shape, box.Width - 10, box.Height - 10),
                Stretch = Stretch.Fill, Fill = fill, Stroke = stroke, StrokeThickness = 1.5, Margin = new(3) });
            grid.Children.Add(new TextBlock { Text = node.Label, FontSize = 13, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = text, Margin = new(node.Shape is FlowNodeShape.Decision or FlowNodeShape.Hexagon ? 30 : 14, 8, node.Shape is FlowNodeShape.Decision or FlowNodeShape.Hexagon ? 30 : 14, 8), VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center, MaxHeight = box.Height - 20 });
            var button = ElementButton(key, "Node " + node.Id + ": " + node.Label + ", " + node.Shape, grid, box);
            AutomationProperties.SetHelpText(button, "Select to edit properties. F2 edits the label. Dragging changes this session's view only. Use its connection port to connect nodes.");
            Panel.SetZIndex(button, 20);
            button.Click += async (_, _) => await PickNode(node.Id);
            button.PreviewMouseLeftButtonDown += async (_, e) =>
            {
                e.Handled = true;
                if (e.ClickCount == 2) { SelectElement(key); BeginInlineEdit(node.Id); return; }
                if (connectionSource is not null) { await PickNode(node.Id); return; }
                SelectElement(key, Keyboard.Modifiers == ModifierKeys.Control);
                if (!CanTransform) return;
                dragStart = e.GetPosition(DiagramCanvas); dragging = false;
                dragged = draft.Selection.Where(item => item.StartsWith("node:", StringComparison.Ordinal) && positions.ContainsKey(item)).ToDictionary(item => item, item => positions[item]);
                button.CaptureMouse();
            };
            button.PreviewMouseMove += (_, e) =>
            {
                if (dragStart is not { } start || dragged is null || e.LeftButton != MouseButtonState.Pressed) return;
                var delta = e.GetPosition(DiagramCanvas) - start;
                if (!dragging && delta.Length < 4) return; dragging = true;
                foreach (var (id, original) in dragged)
                {
                    positions[id] = original with { X = Math.Max(0, original.X + delta.X), Y = Math.Max(0, original.Y + delta.Y) };
                    Canvas.SetLeft(canvasElements[id], positions[id].X); Canvas.SetTop(canvasElements[id], positions[id].Y);
                    if (ports.TryGetValue(id, out var port)) { Canvas.SetLeft(port, positions[id].X + positions[id].Width - 16); Canvas.SetTop(port, positions[id].CentreY - 16); }
                }
                QueueConnections(); e.Handled = true;
            };
            button.PreviewMouseLeftButtonUp += (_, e) => { dragStart = null; dragged = null; button.ReleaseMouseCapture(); e.Handled = true; };
            button.LostMouseCapture += (_, _) => { dragStart = null; dragged = null; };
            var portButton = ConnectionPort(node.Id, button);
            AutomationProperties.SetName(portButton, "Connect from node " + node.Id); AutomationProperties.SetAutomationId(portButton, "MermaidPort_" + node.Id);
            portButton.Click += (_, e) => { SelectElement(key); connectionSource = node.Id; UpdateCanvasSelection(); Status.Text = "Connection from " + node.Id + ": choose a destination node. Escape cancels."; e.Handled = true; };
            Canvas.SetLeft(portButton, box.X + box.Width - 16); Canvas.SetTop(portButton, box.CentreY - 16); Panel.SetZIndex(portButton, 30);
            DiagramCanvas.Children.Add(portButton); ports[key] = portButton;
        }
        AddConnectionGuide(); UpdateCanvasSelection(); UpdateExtent();
    }
    private ToggleButton ElementButton(string key, string name, object content, MermaidLayoutBox box)
    {
        var button = new ToggleButton { Content = content, Width = box.Width, Height = box.Height, Style = (Style)FindResource("CanvasElement"), Foreground = BrushResource("TextBrush") };
        AutomationProperties.SetName(button, name); AutomationProperties.SetAutomationId(button, "MermaidCanvas_" + key.Replace(':', '_'));
        button.Checked += (_, _) => { if (!synchronisingCanvasSelection && !updating) SelectElement(key, Keyboard.Modifiers == ModifierKeys.Control); };
        button.Unchecked += (_, _) =>
        {
            if (synchronisingCanvasSelection || updating) return;
            var choice = choices.FirstOrDefault(item => item.Key == key); if (choice is not null) Elements.SelectedItems.Remove(choice);
        };
        Canvas.SetLeft(button, box.X); Canvas.SetTop(button, box.Y); DiagramCanvas.Children.Add(button); canvasElements[key] = button; return button;
    }
    private async Task PickNode(string id)
    {
        if (connectionSource is { } source && CanTransform)
        {
            connectionSource = null;
            var existing = draft.Model.Edges.Select(edge => edge.Id).ToHashSet();
            if (await Execute(new AddMermaidConnection(source, id)))
            {
                var created = draft.Model.Edges.Single(edge => !existing.Contains(edge.Id)); SelectElement(created.Id);
                EdgeLabel.Focus(); Status.Text = "Connection created. Set its optional label and type in Properties.";
            }
        }
        else SelectElement("node:" + id, Keyboard.Modifiers == ModifierKeys.Control);
    }
    private void DrawConnections()
    {
        foreach (var edge in draft.Model.Edges)
        {
            if (!positions.TryGetValue("node:" + edge.Source.Id, out var from) || !positions.TryGetValue("node:" + edge.Target.Id, out var to)) continue;
            var selected = draft.Selection.Contains(edge.Id); var colour = BrushResource(selected ? "AccentBrush" : "MutedTextBrush");
            var start = Boundary(from, to); var end = Boundary(to, from); var geometry = new StreamGeometry();
            using (var drawing = geometry.Open())
            {
                if (edge.Source.Id == edge.Target.Id)
                { start = new(from.X + from.Width * .75, from.Y); end = new(from.X + from.Width, from.Y + from.Height * .3); drawing.BeginFigure(start, false, false); drawing.BezierTo(new(start.X, start.Y - 65), new(end.X + 65, end.Y), end, true, false); }
                else { drawing.BeginFigure(start, false, false); drawing.LineTo(end, true, false); }
                if (edge.Type != FlowConnectionType.Undirected) Arrow(drawing, end, start);
                if (edge.Type == FlowConnectionType.Bidirectional) Arrow(drawing, start, end);
            }
            geometry.Freeze();
            if (!connectionPaths.TryGetValue(edge.Id, out var path))
            {
                path = new System.Windows.Shapes.Path { StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, StrokeDashArray = edge.Type == FlowConnectionType.Dotted ? new DoubleCollection([3, 3]) : null };
                path.MouseLeftButtonDown += (_, e) => { SelectElement(edge.Id); e.Handled = true; }; Panel.SetZIndex(path, 5);
                DiagramCanvas.Children.Add(path); connectionPaths.Add(edge.Id, path);
            }
            path.Data = geometry; path.Stroke = colour; path.StrokeThickness = selected ? 3 : edge.Type == FlowConnectionType.Thick ? 3 : 1.5;
            if (!canvasElements.TryGetValue(edge.Id, out var button))
            {
                var label = new TextBlock { Text = edge.Label.Length == 0 ? "·" : edge.Label, MaxWidth = 200, TextWrapping = TextWrapping.Wrap,
                    Padding = new(5, 2, 5, 2), Background = BrushResource("WindowBrush"), Foreground = BrushResource("TextBrush") };
                button = ElementButton(edge.Id, "Connection " + edge.Source.Id + " to " + edge.Target.Id + ": " + edge.Label + ", " + edge.Type,
                    label, new(0, 0, Math.Clamp(20 + edge.Label.Length * 7, 36, 200), 34));
                button.ToolTip = edge.Source.Id + " → " + edge.Target.Id; button.Click += (_, _) => SelectElement(edge.Id); Panel.SetZIndex(button, 10);
            }
            Canvas.SetLeft(button, (start.X + end.X) / 2 - button.Width / 2); Canvas.SetTop(button, (start.Y + end.Y) / 2 - button.Height / 2);
        }
        UpdateCanvasSelection();
    }
    private void QueueConnections()
    {
        if (connectionUpdate?.Status == DispatcherOperationStatus.Pending || closed) return;
        connectionUpdate = Dispatcher.InvokeAsync(() =>
        { connectionUpdate = null; if (!closed) { DrawConnections(); UpdateExtent(); } }, DispatcherPriority.Render);
    }
    private static void Arrow(StreamGeometryContext drawing, Point tip, Point other)
    {
        var direction = tip - other; if (direction.Length < .01) direction = new(1, 0); direction.Normalize();
        var side = new Vector(-direction.Y, direction.X); drawing.BeginFigure(tip - direction * 11 + side * 5, false, false);
        drawing.LineTo(tip, true, false); drawing.LineTo(tip - direction * 11 - side * 5, true, false);
    }
    private static Point Boundary(MermaidLayoutBox box, MermaidLayoutBox other)
    {
        var dx = other.CentreX - box.CentreX; var dy = other.CentreY - box.CentreY;
        var factor = Math.Min(Math.Abs(dx) < .01 ? double.MaxValue : box.Width / 2 / Math.Abs(dx), Math.Abs(dy) < .01 ? double.MaxValue : box.Height / 2 / Math.Abs(dy));
        if (factor == double.MaxValue) return new(box.CentreX, box.Y);
        return new(box.CentreX + dx * factor, box.CentreY + dy * factor);
    }
    private void UpdateCanvasSelection()
    {
        UpdateConnectionGuide();
        synchronisingCanvasSelection = true;
        try
        {
            foreach (var (key, button) in canvasElements) button.IsChecked = draft.Selection.Contains(key);
            foreach (var (key, port) in ports) port.Tag = draft.Selection.Contains(key) || connectionSource == key[5..] || canvasElements[key].IsMouseOver;
            foreach (var edge in draft.Model.Edges)
                if (connectionPaths.TryGetValue(edge.Id, out var path))
                {
                    var selected = draft.Selection.Contains(edge.Id); path.Stroke = BrushResource(selected ? "AccentBrush" : "MutedTextBrush");
                    path.StrokeThickness = selected ? 3 : edge.Type == FlowConnectionType.Thick ? 3 : 1.5;
                }
        }
        finally { synchronisingCanvasSelection = false; }
    }
    private void UpdateExtent()
    {
        DiagramCanvas.Width = Math.Max(600, positions.Values.Select(box => box.X + box.Width + 80).DefaultIfEmpty(600).Max());
        DiagramCanvas.Height = Math.Max(400, positions.Values.Select(box => box.Y + box.Height + 80).DefaultIfEmpty(400).Max());
    }
    private Brush BrushResource(string key) => (Brush)FindResource(key);
    private void UpdateCanvasColours()
    {
        var palette = DiagramThemes.Colours(DiagramThemes.ResolveName(diagramTheme, dark));
        foreach (var (key, button) in canvasElements)
        {
            button.Foreground = BrushResource("TextBrush");
            if (key.StartsWith("node:", StringComparison.Ordinal) && button.Content is Grid grid)
            {
                var colours = Colours(draft.Model.Node(key[5..]), palette.Surface ?? palette.Background, palette.Border ?? palette.Accent, palette.Foreground);
                var shape = grid.Children.OfType<System.Windows.Shapes.Path>().Single(); shape.Fill = colours.Fill; shape.Stroke = colours.Stroke;
                grid.Children.OfType<TextBlock>().Single().Foreground = colours.Text;
            }
            else if (button.Content is Border group)
            { group.Background = BrushResource("PanelBrush"); group.BorderBrush = BrushResource("BorderBrush"); }
            else if (button.Content is TextBlock label)
            { label.Background = BrushResource("WindowBrush"); label.Foreground = BrushResource("TextBrush"); }
        }
        UpdateCanvasSelection();
    }
    private (Brush Fill, Brush Stroke, Brush Text) Colours(MermaidNode node, string fill, string stroke, string text)
    {
        if (SystemParameters.HighContrast) return (SystemColors.WindowBrush, SystemColors.WindowTextBrush, SystemColors.WindowTextBrush);
        var className = draft.Model.ClassForNode(node.Id);
        var values = draft.Model.Styles.Where(style => style.Kind == "classDef" && style.Targets.Any(target => target.Id == className))
            .Concat(draft.Model.Styles.Where(style => style.Kind == "style" && style.Targets.Any(target => target.Id == node.Id)));
        foreach (var value in values.SelectMany(style => style.Value.Split(',')))
        {
            var parts = value.Split(':', 2, StringSplitOptions.TrimEntries); if (parts.Length != 2) continue;
            var colour = parts[1].TrimEnd(';'); if (colour.Length != 7 || colour[0] != '#' || colour[1..].Any(character => !Uri.IsHexDigit(character))) continue;
            if (parts[0] == "fill") fill = colour; else if (parts[0] == "stroke") stroke = colour; else if (parts[0] == "color") text = colour;
        }
        return ((Brush)new BrushConverter().ConvertFromString(fill)!, (Brush)new BrushConverter().ConvertFromString(stroke)!, (Brush)new BrushConverter().ConvertFromString(text)!);
    }
    private static Geometry NodeGeometry(FlowNodeShape shape, double width, double height)
    {
        if (shape == FlowNodeShape.Decision) return Polygon(new(width / 2, 0), new(width, height / 2), new(width / 2, height), new(0, height / 2));
        if (shape == FlowNodeShape.Hexagon) return Polygon(new(20, 0), new(width - 20, 0), new(width, height / 2), new(width - 20, height), new(20, height), new(0, height / 2));
        if (shape is FlowNodeShape.Circle or FlowNodeShape.DoubleCircle)
        { var ellipse = new EllipseGeometry(new Rect(0, 0, width, height)); if (shape == FlowNodeShape.Circle) return ellipse; return new GeometryGroup { Children = { ellipse, new EllipseGeometry(new Rect(6, 6, width - 12, height - 12)) } }; }
        var radius = shape == FlowNodeShape.Terminal ? height / 2 : shape == FlowNodeShape.Rounded ? 14 : 2;
        var rectangle = new RectangleGeometry(new Rect(0, 0, width, height), radius, radius);
        if (shape == FlowNodeShape.Subroutine) return new GeometryGroup { Children = { rectangle, new LineGeometry(new(10, 0), new(10, height)), new LineGeometry(new(width - 10, 0), new(width - 10, height)) } };
        if (shape == FlowNodeShape.Cylinder) return new GeometryGroup { Children = { new RectangleGeometry(new Rect(0, 10, width, height - 20)), new EllipseGeometry(new Rect(0, 0, width, 20)), new EllipseGeometry(new Rect(0, height - 20, width, 20)) } };
        return rectangle;
    }
    private static Geometry Polygon(params Point[] points)
    { var geometry = new StreamGeometry(); using (var context = geometry.Open()) { context.BeginFigure(points[0], true, true); context.PolyLineTo(points.Skip(1).ToArray(), true, true); } geometry.Freeze(); return geometry; }

    private void BeginInlineEdit(string id)
    {
        if (!CanTransform || !positions.TryGetValue("node:" + id, out var box)) return;
        if (inlineEditor is not null) DiagramCanvas.Children.Remove(inlineEditor);
        var node = draft.Model.Node(id); var editor = new TextBox { Text = node.Label, Width = box.Width - 16, MinHeight = 36, TextWrapping = TextWrapping.Wrap, Padding = new(5) };
        inlineEditor = editor; AutomationProperties.SetName(editor, "Edit label for node " + id); AutomationProperties.SetAutomationId(editor, "MermaidInlineLabel");
        Canvas.SetLeft(editor, box.X + 8); Canvas.SetTop(editor, box.CentreY - 20); Panel.SetZIndex(editor, 50); DiagramCanvas.Children.Add(editor);
        editor.PreviewKeyDown += async (_, e) =>
        {
            if (e.Key is not (Key.Enter or Key.Escape)) return; e.Handled = true;
            var text = editor.Text; DiagramCanvas.Children.Remove(editor); inlineEditor = null;
            if (e.Key == Key.Enter) await Execute(new UpdateMermaidNode(id, text, node.Shape, id), "node:" + id);
            UpdateActions();
            Elements.Focus();
        };
        editor.Focus(); editor.SelectAll();
        UpdateActions(); Status.Text = "Edit the label, then press Enter to confirm or Escape to cancel. The node ID stays unchanged.";
    }
    private void AutoLayout(object sender, RoutedEventArgs e)
    {
        if (inlineEditor is not null) { inlineEditor.Focus(); Status.Text = "Confirm or cancel the inline label before changing layout."; return; }
        RebuildCanvas(true); Status.Text = "Automatic layout restored from the semantic graph. No coordinates were written to source.";
    }
    private void Fit(object sender, RoutedEventArgs e)
    {
        if (Viewport is null || DiagramCanvas is null || Viewport.ViewportWidth <= 0 || Viewport.ViewportHeight <= 0) return;
        Zoom.Value = Math.Clamp(Math.Min(Viewport.ViewportWidth / DiagramCanvas.Width, Viewport.ViewportHeight / DiagramCanvas.Height) * 100, 1, 400);
        Viewport.ScrollToHorizontalOffset(0); Viewport.ScrollToVerticalOffset(0);
    }
    private void ActualSize(object sender, RoutedEventArgs e) => Zoom.Value = 100;
    private void ResetView(object sender, RoutedEventArgs e) { Zoom.Value = 100; Viewport.ScrollToHorizontalOffset(0); Viewport.ScrollToVerticalOffset(0); }
    private void ZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    { if (CanvasScale is null) return; CanvasScale.ScaleX = CanvasScale.ScaleY = e.NewValue / 100; ZoomLabel.Text = Math.Round(e.NewValue) + "%"; }
    private void CanvasWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return; e.Handled = true;
        var pointer = e.GetPosition(Viewport); var scale = Zoom.Value / 100;
        var x = (Viewport.HorizontalOffset + pointer.X) / scale; var y = (Viewport.VerticalOffset + pointer.Y) / scale;
        Zoom.Value = Math.Clamp(Zoom.Value * (e.Delta > 0 ? 1.15 : 1 / 1.15), 1, 400); Viewport.UpdateLayout();
        Viewport.ScrollToHorizontalOffset(x * Zoom.Value / 100 - pointer.X); Viewport.ScrollToVerticalOffset(y * Zoom.Value / 100 - pointer.Y);
    }
    private void PanStarted(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle && !(e.ChangedButton == MouseButton.Left && Keyboard.IsKeyDown(Key.Space))) return;
        panStart = e.GetPosition(Viewport); panOffsets = new(Viewport.HorizontalOffset, Viewport.VerticalOffset); Viewport.CaptureMouse(); Viewport.Cursor = Cursors.ScrollAll; e.Handled = true;
    }
    private void PanMoved(object sender, MouseEventArgs e)
    { if (panStart is not { } start) return; var delta = e.GetPosition(Viewport) - start; Viewport.ScrollToHorizontalOffset(panOffsets.X - delta.X); Viewport.ScrollToVerticalOffset(panOffsets.Y - delta.Y); e.Handled = true; }
    private void PanFinished(object sender, MouseButtonEventArgs e) { if (panStart is null) return; Viewport.ReleaseMouseCapture(); panStart = null; Viewport.Cursor = null; e.Handled = true; }
    private void PanCaptureLost(object sender, MouseEventArgs e) { panStart = null; Viewport.Cursor = null; }
    private void DeselectCanvas(object sender, MouseButtonEventArgs e) { if (e.OriginalSource == DiagramCanvas) { Elements.UnselectAll(); e.Handled = true; } }
    private void PaletteDrag(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || !CanTransform || paletteStart is not { } origin) return;
        var delta = e.GetPosition(Palette) - origin;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var current = e.OriginalSource as DependencyObject;
        while (current is not null && current is not Button) current = VisualTreeHelper.GetParent(current);
        if (current is Button { Tag: string shape } && (Enum.TryParse<FlowNodeShape>(shape, out _) || shape is "Subgraph" or "Connection"))
        { paletteStart = null; DragDrop.DoDragDrop((DependencyObject)sender, new DataObject("Lens.MermaidElement", shape), DragDropEffects.Copy); }
    }
    private async void CanvasDrop(object sender, DragEventArgs e)
    {
        if (!CanTransform || PropertiesPending || e.Data.GetData("Lens.MermaidElement") is not string text) return;
        var point = e.GetPosition(DiagramCanvas); e.Effects = DragDropEffects.Copy; e.Handled = true;
        var group = positions.Where(pair => pair.Key.StartsWith("group:", StringComparison.Ordinal) && Contains(pair.Value, point))
            .OrderBy(pair => pair.Value.Width * pair.Value.Height).Select(pair => pair.Key[6..]).FirstOrDefault();
        if (text == "Connection")
        {
            var target = positions.FirstOrDefault(pair => pair.Key.StartsWith("node:", StringComparison.Ordinal) && Contains(pair.Value, point)).Key;
            if (target is null) { Status.Text = "Drop the connection on an existing node; no node has been created."; return; }
            connectionSource ??= Selected?.StartsWith("node:", StringComparison.Ordinal) == true ? Selected[5..] : null;
            if (connectionSource is null) { SelectElement(target); StartConnection(this, e); }
            else await PickNode(target[5..]);
            return;
        }
        if (text == "Subgraph")
        {
            var groupId = MermaidTransformationService.UniqueId(draft.Model, "Group");
            if (await Execute(new AddMermaidSubgraph(groupId, "New subgraph", group), "group:" + groupId))
            {
                var key = "group:" + groupId; var box = positions[key]; positions[key] = box with { X = Math.Max(0, point.X), Y = Math.Max(0, point.Y) };
                Canvas.SetLeft(canvasElements[key], positions[key].X); Canvas.SetTop(canvasElements[key], positions[key].Y); UpdateExtent();
            }
            return;
        }
        if (!Enum.TryParse<FlowNodeShape>(text, out var shape)) return;
        var id = MermaidTransformationService.UniqueId(draft.Model);
        if (await Execute(new AddMermaidNode(id, shape.ToString(), shape, group), "node:" + id))
        {
            var box = positions["node:" + id]; positions["node:" + id] = box with { X = Math.Max(0, point.X), Y = Math.Max(0, point.Y) }; RebuildCanvas(false);
        }
        static bool Contains(MermaidLayoutBox box, Point position) => position.X >= box.X && position.X <= box.X + box.Width && position.Y >= box.Y && position.Y <= box.Y + box.Height;
    }
}
