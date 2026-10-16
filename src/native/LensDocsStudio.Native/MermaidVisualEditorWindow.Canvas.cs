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
using LensDocsStudio.Native.Rendering;
using SharpVectors.Runtime;

namespace LensDocsStudio.Native;

public partial class MermaidVisualEditorWindow
{
    private readonly Dictionary<string, MermaidLayoutBox> positions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ToggleButton> canvasElements = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> ports = new(StringComparer.Ordinal);
    private readonly Dictionary<string, System.Windows.Shapes.Path> connectionPaths = new(StringComparer.Ordinal);
    private readonly Dictionary<string, System.Windows.Shapes.Path> connectionHitPaths = new(StringComparer.Ordinal);
    private Point? dragStart, panStart;
    private Dictionary<string, MermaidLayoutBox>? dragged;
    private Point panOffsets;
    private TextBox? inlineEditor;
    private bool synchronisingCanvasSelection;
    private Point? paletteStart;

    private void InitialisePaletteDragging()
    {
        Palette.PreviewMouseLeftButtonDown += (_, e) => paletteStart = e.GetPosition(Palette);
        Palette.PreviewMouseLeftButtonUp += (_, _) => paletteStart = null;
        Viewport.PreviewDragOver += (_, e) =>
        {
            e.Effects = CanTransform && !PropertiesPending && e.Data.GetDataPresent("Lens.MermaidElement") ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
    }

    private NativeMermaidPresentation? presentation;
    private DiagramResult? canonicalRender;
    private DrawingImage? canonicalDrawing;
    private readonly Dictionary<string, DrawingGroup> nodeDrawings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DrawingGroup> edgeDrawings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DrawingGroup> edgeLabelDrawings = new(StringComparer.Ordinal);
    private readonly HashSet<string> sessionConnections = new(StringComparer.Ordinal);
    private Image? canonicalImage;

    private readonly NativeSvgService canvasSvg = new();
    private readonly SemaphoreSlim canvasRenderGate = new(1, 1);
    private CancellationTokenSource? canvasRender;
    private int canvasGeneration;
    private bool renderingCanvas, canonicalMapped, initialFit = true;
    private string? canvasError;

    private async void RebuildCanvas(bool layout)
    {
        var source = draft.Source; var themeDark = dark; var generation = ++canvasGeneration;
        canvasRender?.Cancel();
        if (canonicalRender?.Source == source && canonicalRender.Theme == DiagramThemes.ResolveName(diagramTheme, themeDark)
            && presentation is not null && (!draft.Model.CanEdit || presentation.Maps(draft.Model)))
        {
            renderingCanvas = false; canonicalMapped = presentation.Maps(draft.Model);
            UpdateCanvasSelection(); UpdateActions(); return;
        }
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); canvasRender = cancellation;
        renderingCanvas = true; canonicalMapped = false; UpdateActions();
        var leased = false;
        try
        {
            // SharpVectors has no interruptible conversion API. Serialise work so obsolete
            // conversions can finish without accumulating threads, drawings or font geometry.
            await canvasRenderGate.WaitAsync(cancellation.Token); leased = true;
            var rendered = await Task.Run(() => diagrams.Render(source, themeDark, diagramTheme, cancellation.Token), cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (!rendered.Success) throw new InvalidDataException(rendered.Error);
            DrawingImage? inspection = null;
            string? mappingDiagnostic = null;
            var next = await LensDocsStudio.Native.Platform.StaWorker.RunAsync(() =>
            {
                try { return NativeMermaidPresentation.Create(rendered, canvasSvg); }
                catch (Exception error) when (error is InvalidDataException or ArgumentException or FormatException)
                {
                    // A valid drawing remains inspectable even if its interaction metadata cannot be mapped.
                    inspection = canvasSvg.Render(rendered.Svg!); mappingDiagnostic = error.Message; return null;
                }
            }, cancellation.Token);
            if (closed || generation != canvasGeneration || draft.Source != source || dark != themeDark) return;
            presentation = next; canvasError = null; canonicalMapped = next?.Maps(draft.Model) == true;
            canonicalRender = rendered; canonicalDrawing = next?.Drawing ?? inspection;
            if (next is not null) BuildCanonicalCanvas();
            else
            {
                DiagramCanvas.Children.Clear(); positions.Clear(); canvasElements.Clear(); ports.Clear(); connectionPaths.Clear(); connectionHitPaths.Clear();
                var drawing = new Image { Source = inspection, Width = inspection!.Width, Height = inspection.Height, Stretch = Stretch.None, IsHitTestVisible = false };
                AutomationProperties.SetAutomationId(drawing, "MermaidCanonicalDrawing"); DiagramCanvas.Children.Add(drawing);
                DiagramCanvas.Width = inspection.Width; DiagramCanvas.Height = inspection.Height;
            }
            Status.Text = !draft.Model.CanEdit ? "Visual editing is limited for this diagram. Its original layout is preserved. Edit the source to change unsupported syntax."
                : !canonicalMapped ? "The rendered elements cannot all be mapped safely. Inspect the original diagram or use Edit source."
                : draft.IsDirty ? "Draft modified. Apply when ready." : "Ready. Select an element or add one from the palette.";
            Status.ToolTip = mappingDiagnostic ?? draft.Model.Diagnostic ?? Status.Text;
            if (initialFit)
            {
                initialFit = false; Viewport.UpdateLayout(); Fit(this, new());
                if (Zoom.Value < 60)
                {
                    Zoom.Value = 100; Viewport.UpdateLayout();
                    var first = draft.Model.Nodes.FirstOrDefault();
                    if (first is not null && positions.TryGetValue("node:" + first.Id, out var anchor))
                    {
                        Viewport.ScrollToHorizontalOffset(Math.Max(0, anchor.CentreX - Viewport.ViewportWidth / 2));
                        Viewport.ScrollToVerticalOffset(Math.Max(0, anchor.CentreY - Viewport.ViewportHeight / 2));
                    }
                    Status.Text += " Opened at actual size for readable labels; Fit shows the whole graph.";
                }
                else if (Zoom.Value > 100) Zoom.Value = 100;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (closed || generation != canvasGeneration) return;
            presentation = null; canonicalRender = null; canonicalDrawing = null; canonicalMapped = false; canvasError = error.Message;
            DiagramCanvas.Children.Clear(); positions.Clear(); canvasElements.Clear(); ports.Clear(); connectionPaths.Clear(); connectionHitPaths.Clear();
            var diagnostic = new TextBlock { Text = "The original diagram could not be rendered. " + error.Message + " Edit the source to correct it.", TextWrapping = TextWrapping.Wrap, MaxWidth = 520, Margin = new(20) };
            AutomationProperties.SetAutomationId(diagnostic, "MermaidCanvasDiagnostic");
            DiagramCanvas.Children.Add(diagnostic); DiagramCanvas.Width = 560; DiagramCanvas.Height = 140; Status.Text = diagnostic.Text;
        }
        finally
        {
            if (leased) canvasRenderGate.Release();
            cancellation.Dispose(); if (ReferenceEquals(canvasRender, cancellation)) canvasRender = null;
            if (!closed && generation == canvasGeneration) { renderingCanvas = false; UpdateActions(); }
        }
    }
    private void BuildCanonicalCanvas()
    {
        if (presentation is not { } view) return;
        DiagramCanvas.Children.Clear(); positions.Clear(); canvasElements.Clear(); ports.Clear(); connectionPaths.Clear(); connectionHitPaths.Clear(); inlineEditor = null;
        var mutableDrawing = view.Drawing.Clone(); nodeDrawings.Clear(); edgeDrawings.Clear(); edgeLabelDrawings.Clear(); sessionConnections.Clear();
        var labelled = view.Connections.Where(connection => view.Graph.Edges[connection.Ordinal].Label is not null).ToArray();
        foreach (var group in DrawingGroups(mutableDrawing.Drawing))
        {
            var id = SvgObject.GetId(group);
            if (id?.StartsWith("lens-node-", StringComparison.Ordinal) == true && int.TryParse(id.AsSpan(10), out var ordinal) && ordinal < view.Graph.Nodes.Count)
                nodeDrawings["node:" + view.Graph.Nodes[ordinal].Id] = group;
            if (id?.StartsWith("lens-edge-", StringComparison.Ordinal) == true && int.TryParse(id.AsSpan(10), out var edgeOrdinal) && edgeOrdinal < view.Connections.Count)
                edgeDrawings[canonicalMapped ? draft.Model.Edges[view.Connections[edgeOrdinal].Ordinal].Id : id] = group;
            if (id?.StartsWith("lens-label-", StringComparison.Ordinal) == true && int.TryParse(id.AsSpan(11), out var labelOrdinal) && labelOrdinal < labelled.Length && canonicalMapped)
                edgeLabelDrawings[draft.Model.Edges[labelled[labelOrdinal].Ordinal].Id] = group;
        }
        var image = new Image { Source = mutableDrawing, Width = view.Drawing.Width, Height = view.Drawing.Height, Stretch = Stretch.None, IsHitTestVisible = false };
        canonicalImage = image;
        AutomationProperties.SetAutomationId(image, "MermaidCanonicalDrawing");
        Panel.SetZIndex(image, -10); DiagramCanvas.Children.Add(image);
        foreach (var element in view.Elements.Values)
        {
            var key = element.Key;
            // Protected sources may have only a partial lexical model. Never invent editable identities from SVG.
            var choice = choices.FirstOrDefault(item => item.Key == key); if (choice is null) continue;
            var box = element.Bounds; positions[key] = new(box.X, box.Y, box.Width, box.Height);
            var geometry = element.Geometry.Clone();
            geometry.Transform = new TransformGroup { Children = { element.Geometry.Transform, new TranslateTransform(-box.X, -box.Y) } }; geometry.Freeze();
            var outline = new System.Windows.Shapes.Path { Data = geometry, Fill = Brushes.Transparent, StrokeThickness = 2, Stretch = Stretch.None };
            var button = ElementButton(key, choice.Description, outline, positions[key]);
            Panel.SetZIndex(button, key.StartsWith("node:", StringComparison.Ordinal) ? 20 : 0);
            if (key.StartsWith("node:", StringComparison.Ordinal))
            {
                var id = key[5..];
                button.Click += async (_, _) => await PickNode(id);
                button.PreviewMouseLeftButtonDown += async (_, e) =>
                {
                    e.Handled = true;
                    if (PropertiesPending) { SelectElement(key); return; }
                    if (e.ClickCount == 2) { SelectElement(key); BeginInlineEdit(id); return; }
                    if (connectionSource is not null) { await PickNode(id); return; }
                    SelectElement(key, Keyboard.Modifiers == ModifierKeys.Control);
                    if (!CanTransform || !nodeDrawings.ContainsKey(key)) return;
                    dragStart = e.GetPosition(DiagramCanvas);
                    dragged = draft.Selection.Where(item => item.StartsWith("node:", StringComparison.Ordinal) && positions.ContainsKey(item)).ToDictionary(item => item, item => positions[item]);
                    button.CaptureMouse();
                };
                button.PreviewMouseMove += (_, e) =>
                {
                    if (dragStart is not { } start || dragged is null || e.LeftButton != MouseButtonState.Pressed) return;
                    var delta = e.GetPosition(DiagramCanvas) - start; if (delta.Length < 4) return;
                    foreach (var (nodeKey, original) in dragged)
                    {
                        var canonical = view.Elements[nodeKey].Bounds;
                        var box = original with { X = Math.Max(0, original.X + delta.X), Y = Math.Max(0, original.Y + delta.Y) };
                        positions[nodeKey] = box; Canvas.SetLeft(canvasElements[nodeKey], box.X); Canvas.SetTop(canvasElements[nodeKey], box.Y);
                        nodeDrawings[nodeKey].Transform = new TranslateTransform(box.X - canonical.X, box.Y - canonical.Y);
                        if (ports.TryGetValue(nodeKey, out var nodePort)) { Canvas.SetLeft(nodePort, box.X + box.Width - 16); Canvas.SetTop(nodePort, box.CentreY - 16); }
                    }
                    UpdateSessionConnections(); UpdateExtent(); e.Handled = true;
                };
                button.PreviewMouseLeftButtonUp += (_, e) => { dragStart = null; dragged = null; button.ReleaseMouseCapture(); e.Handled = true; };
                button.LostMouseCapture += (_, _) => { dragStart = null; dragged = null; };
                AutomationProperties.SetHelpText(button, "Select to edit properties. F2 edits the label. Dragging changes this session only; connection guides return to canonical routing after Auto layout or an edit. Space+drag pans.");
                var port = ConnectionPort(id, button);
                AutomationProperties.SetName(port, "Connect from node " + id); AutomationProperties.SetAutomationId(port, "MermaidPort_" + id);
                port.Click += (_, e) => { if (PropertiesPending || !CanTransform) return; SelectElement(key); connectionSource = id; UpdateCanvasSelection(); Status.Text = "Connection from " + id + ": choose a destination node. Escape cancels."; e.Handled = true; };
                Canvas.SetLeft(port, box.Right - 16); Canvas.SetTop(port, box.Top + box.Height / 2 - 16); Panel.SetZIndex(port, 30);
                DiagramCanvas.Children.Add(port); ports[key] = port;
            }
            else button.Click += (_, _) => SelectElement(key, Keyboard.Modifiers == ModifierKeys.Control);
        }
        if (canonicalMapped)
        foreach (var edge in view.Connections)
        {
            var key = draft.Model.Edges[edge.Ordinal].Id;
            var hit = new System.Windows.Shapes.Path { Data = edge.Path, Stroke = Brushes.Transparent, StrokeThickness = 12 };
            hit.MouseLeftButtonDown += (_, e) =>
            {
                var point = e.GetPosition(DiagramCanvas);
                var overlaps = connectionHitPaths.Count(pair => pair.Value.Data.StrokeContains(new Pen(Brushes.Black, 12), point));
                if (overlaps > 1) Status.Text = "Connections overlap here. Choose the intended connection in the element list.";
                else SelectElement(key);
                e.Handled = true;
            };
            Panel.SetZIndex(hit, 4); DiagramCanvas.Children.Add(hit); connectionHitPaths[key] = hit;
            var selection = new System.Windows.Shapes.Path { Data = edge.Path, StrokeThickness = 3, IsHitTestVisible = false, Opacity = .65 };
            Panel.SetZIndex(selection, 5); DiagramCanvas.Children.Add(selection); connectionPaths[key] = selection;
            if (edge.LabelBounds is { } box)
            {
                var button = ElementButton(key, choices.Single(choice => choice.Key == key).Description, new Rectangle { Fill = Brushes.Transparent }, new(box.X, box.Y, box.Width, box.Height));
                button.Click += (_, _) => SelectElement(key); Panel.SetZIndex(button, 10);
            }
            else
            {
                var targetBounds = edge.Path.Bounds; targetBounds.Inflate(6, 6);
                var local = edge.Path.Clone();
                local.Transform = new TransformGroup { Children = { edge.Path.Transform, new TranslateTransform(-targetBounds.X, -targetBounds.Y) } };
                var target = new System.Windows.Shapes.Path { Data = local, Stroke = Brushes.Transparent, StrokeThickness = 12 };
                var button = ElementButton(key, choices.Single(choice => choice.Key == key).Description, target, new(targetBounds.X, targetBounds.Y, targetBounds.Width, targetBounds.Height));
                button.PreviewMouseLeftButtonDown += (_, e) =>
                {
                    var point = e.GetPosition(DiagramCanvas);
                    if (connectionHitPaths.Count(pair => pair.Value.Data.StrokeContains(new Pen(Brushes.Black, 12), point)) > 1)
                        Status.Text = "Connections overlap here. Choose the intended connection in the element list.";
                    else SelectElement(key);
                    e.Handled = true;
                };
                Panel.SetZIndex(button, 4);
            }
        }
        AddConnectionGuide(); UpdateCanvasSelection(); UpdateExtent(); UpdateActions();
    }
    private ToggleButton ElementButton(string key, string name, object? content, MermaidLayoutBox box)
    {
        var button = new ToggleButton { Content = content, Width = box.Width, Height = box.Height, Style = (Style)FindResource("Mermaid.CanonicalElement"), Foreground = BrushResource("TextBrush") };
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
        if (PropertiesPending) { UpdateCanvasSelection(); Status.Text = "Update or reset pending property changes before connecting nodes."; return; }
        if (connectionSource is { } source && CanTransform)
        {
            connectionSource = null; var existing = draft.Model.Edges.Select(edge => edge.Id).ToHashSet();
            if (await Execute(new AddMermaidConnection(source, id)))
            { var created = draft.Model.Edges.Single(edge => !existing.Contains(edge.Id)); SelectElement(created.Id); EdgeLabel.Focus(); Status.Text = "Connection created. Set its optional label and type in Properties."; }
        }
        else SelectElement("node:" + id, Keyboard.Modifiers == ModifierKeys.Control);
    }
    private void UpdateCanvasSelection()
    {
        UpdateConnectionGuide(); synchronisingCanvasSelection = true;
        try
        {
            foreach (var (key, button) in canvasElements)
            {
                button.IsChecked = draft.Selection.Contains(key);
                if (!key.StartsWith("edge-", StringComparison.Ordinal) && button.Content is System.Windows.Shapes.Path shape)
                    shape.Stroke = button.IsChecked == true ? BrushResource("AccentBrush") : Brushes.Transparent;
            }
            foreach (var (key, port) in ports) port.Tag = draft.Selection.Contains(key) || connectionSource == key[5..] || canvasElements[key].IsMouseOver;
            foreach (var (key, path) in connectionPaths) path.Stroke = draft.Selection.Contains(key) ? BrushResource("AccentBrush")
                : sessionConnections.Contains(key) ? BrushResource("MutedTextBrush") : Brushes.Transparent;
        }
        finally { synchronisingCanvasSelection = false; }
    }
    private void UpdateExtent()
    {
        DiagramCanvas.Width = Math.Max(1, Math.Max(presentation?.Drawing.Width ?? 1, positions.Values.Select(box => box.X + box.Width).DefaultIfEmpty(1).Max()));
        DiagramCanvas.Height = Math.Max(1, Math.Max(presentation?.Drawing.Height ?? 1, positions.Values.Select(box => box.Y + box.Height).DefaultIfEmpty(1).Max()));
        if (canonicalImage?.Source is DrawingImage image) { canonicalImage.Width = image.Width; canonicalImage.Height = image.Height; }
    }
    private static IEnumerable<DrawingGroup> DrawingGroups(Drawing drawing)
    {
        if (drawing is not DrawingGroup group) yield break;
        yield return group;
        foreach (var child in group.Children) foreach (var nested in DrawingGroups(child)) yield return nested;
    }
    private void UpdateSessionConnections()
    {
        if (presentation is not { } view) return;
        foreach (var connection in view.Connections)
        {
            var key = draft.Model.Edges[connection.Ordinal].Id;
            if (!positions.TryGetValue("node:" + connection.Source, out var source) || !positions.TryGetValue("node:" + connection.Target, out var target)) continue;
            var originalSource = view.Elements["node:" + connection.Source].Bounds; var originalTarget = view.Elements["node:" + connection.Target].Bounds;
            var from = new Vector(source.X - originalSource.X, source.Y - originalSource.Y); var to = new Vector(target.X - originalTarget.X, target.Y - originalTarget.Y);
            var moved = from.Length > .01 || to.Length > .01;
            if (!moved && !sessionConnections.Contains(key)) continue;
            if (moved) sessionConnections.Add(key); else sessionConnections.Remove(key);
            if (edgeDrawings.TryGetValue(key, out var drawing)) drawing.Opacity = moved ? 0 : 1;
            var geometry = connection.Path.GetFlattenedPathGeometry().Clone();
            foreach (var figure in geometry.Figures)
            {
                var points = new List<Point> { figure.StartPoint };
                foreach (var segment in figure.Segments)
                { if (segment is PolyLineSegment line) points.AddRange(line.Points); else if (segment is LineSegment single) points.Add(single.Point); }
                if (points.Count < 2) continue;
                var transformed = points.Select((point, index) => point + from * (1 - index / (double)(points.Count - 1)) + to * (index / (double)(points.Count - 1))).ToArray();
                figure.StartPoint = transformed[0]; figure.Segments.Clear(); figure.Segments.Add(new PolyLineSegment(transformed.Skip(1), true));
            }
            connectionHitPaths[key].Data = geometry;
            var path = connectionPaths[key]; path.Data = geometry; path.StrokeDashArray = moved ? new DoubleCollection([4, 3]) : null;
            path.Stroke = moved ? BrushResource("MutedTextBrush") : Brushes.Transparent;
            var labelOffset = (from + to) / 2;
            if (edgeLabelDrawings.TryGetValue(key, out var labelDrawing)) labelDrawing.Transform = new TranslateTransform(labelOffset.X, labelOffset.Y);
            if (connection.LabelBounds is { } labelBounds && canvasElements.TryGetValue(key, out var labelButton))
            { Canvas.SetLeft(labelButton, labelBounds.X + labelOffset.X); Canvas.SetTop(labelButton, labelBounds.Y + labelOffset.Y); }
            else if (canvasElements.TryGetValue(key, out var pathButton) && pathButton.Content is System.Windows.Shapes.Path targetPath)
            {
                var bounds = geometry.Bounds; bounds.Inflate(6, 6); var local = geometry.Clone();
                local.Transform = new TransformGroup { Children = { geometry.Transform, new TranslateTransform(-bounds.X, -bounds.Y) } };
                targetPath.Data = local; pathButton.Width = bounds.Width; pathButton.Height = bounds.Height;
                Canvas.SetLeft(pathButton, bounds.X); Canvas.SetTop(pathButton, bounds.Y);
            }
        }
        Status.Text = "Session positions only. Dashed connection guides are temporary; Auto layout or a source edit restores the original Mermaider layout.";
    }
    private Brush BrushResource(string key) => (Brush)FindResource(key);
    private void UpdateCanvasColours() => RebuildCanvas(false);
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
    private void BeginInlineEdit(string id)
    {
        if (PropertiesPending) { Status.Text = "Update or reset pending property changes before inline label editing."; return; }
        if (!CanTransform || !positions.TryGetValue("node:" + id, out var box)) return;
        if (inlineEditor is not null) DiagramCanvas.Children.Remove(inlineEditor);
        var node = draft.Model.Node(id); var editor = new TextBox { Text = node.Label, MaxLength = 4096, Width = box.Width - 16, MinHeight = 36, TextWrapping = TextWrapping.Wrap, Padding = new(5) };
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
        if (presentation is null)
        { Status.Text = canvasError ?? "The original drawing remains in protected visual inspection. Use Edit source to change it."; return; }
        BuildCanonicalCanvas(); Status.Text = "Canonical Mermaider layout restored. No coordinates were written to source.";
    }
    private void Fit(object sender, RoutedEventArgs e)
    {
        if (Viewport is null || DiagramCanvas is null || Viewport.ViewportWidth <= 0 || Viewport.ViewportHeight <= 0) return;
        var fit = Math.Min(Viewport.ViewportWidth / DiagramCanvas.Width, Viewport.ViewportHeight / DiagramCanvas.Height) * 100;
        Zoom.Minimum = Math.Min(1, fit); Zoom.Value = Math.Min(fit, Zoom.Maximum);
        Viewport.ScrollToHorizontalOffset(0); Viewport.ScrollToVerticalOffset(0);
    }
    private void ActualSize(object sender, RoutedEventArgs e) => Zoom.Value = 100;
    private void ResetView(object sender, RoutedEventArgs e) { Zoom.Value = 100; Viewport.ScrollToHorizontalOffset(0); Viewport.ScrollToVerticalOffset(0); }
    private void ZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    { if (CanvasScale is null) return; CanvasScale.ScaleX = CanvasScale.ScaleY = e.NewValue / 100; ZoomLabel.Text = (e.NewValue < 1 ? e.NewValue.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : Math.Round(e.NewValue).ToString()) + "%"; }
    private void CanvasWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return; e.Handled = true;
        var pointer = e.GetPosition(Viewport); var anchor = e.GetPosition(DiagramCanvas);
        Zoom.Value = Math.Clamp(Zoom.Value * (e.Delta > 0 ? 1.15 : 1 / 1.15), Zoom.Minimum, Zoom.Maximum); Viewport.UpdateLayout();
        var after = DiagramCanvas.TranslatePoint(anchor, Viewport);
        Viewport.ScrollToHorizontalOffset(Viewport.HorizontalOffset + after.X - pointer.X); Viewport.ScrollToVerticalOffset(Viewport.VerticalOffset + after.Y - pointer.Y);
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
            await Execute(new AddMermaidSubgraph(groupId, "New subgraph", group), "group:" + groupId);
            return;
        }
        if (!Enum.TryParse<FlowNodeShape>(text, out var shape)) return;
        var id = MermaidTransformationService.UniqueId(draft.Model);
        await Execute(new AddMermaidNode(id, shape.ToString(), shape, group), "node:" + id);
        static bool Contains(MermaidLayoutBox box, Point position) => position.X >= box.X && position.X <= box.X + box.Width && position.Y >= box.Y && position.Y <= box.Y + box.Height;
    }
}
