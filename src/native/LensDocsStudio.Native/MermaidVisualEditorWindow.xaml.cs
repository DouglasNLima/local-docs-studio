using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Native.Rendering;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native;

public partial class MermaidVisualEditorWindow : Window
{
    private readonly MermaidVisualDraft draft;
    private readonly DiagramService diagrams;
    private readonly Func<string, CancellationToken, Task<string?>> applyToDocument;
    private readonly Action? editSource;
    private readonly Action<string, string>? exportDiagram;
    private bool dark;
    private bool highContrast;
    private readonly MermaidSourceHighlighting sourceHighlighting;
    private readonly string diagramTheme;
    private readonly CancellationTokenSource lifetime = new();
    private bool updating, busy, closed, discard;
    private string? connectionSource;
    private IReadOnlyList<ElementChoice> choices = [];
    private object? propertyBaseline;
    private record NodeValues(string Id, string Label, FlowNodeShape Shape, string? Group, string? Class, string Fill, string Stroke, string Colour);
    private record EdgeValues(string? Source, string? Target, string Label, FlowConnectionType Type);
    private record GroupValues(string Label, string? Direction);
    private NodeValues ReadNodeValues() => new(NodeId.Text, NodeLabel.Text, NodeShape.SelectedItem is FlowNodeShape shape ? shape : FlowNodeShape.Process,
        NodeGroup.SelectedItem as string, NodeClass.SelectedItem as string, FillColour.Text, StrokeColour.Text, TextColour.Text);
    private object? PropertyValues => NodeProperties.Visibility == Visibility.Visible ? ReadNodeValues()
        : EdgeProperties.Visibility == Visibility.Visible ? new EdgeValues(EdgeSource.SelectedItem as string, EdgeTarget.SelectedItem as string, EdgeLabel.Text, EdgeType.SelectedItem is FlowConnectionType type ? type : FlowConnectionType.Directed)
        : GroupProperties.Visibility == Visibility.Visible ? new GroupValues(GroupLabel.Text, GroupDirection.SelectedItem as string) : null;
    private bool PropertiesPending => propertyBaseline is not null && !Equals(propertyBaseline, PropertyValues);
    private double sourcePanelHeight = 190;
    private bool fullScreen;
    private WindowStyle previousStyle;
    private WindowState previousState;
    private ResizeMode previousResize;
    private record ElementChoice(string Key, string Description) { public override string ToString() => Description; }
    private string? Selected => (Elements.SelectedItem as ElementChoice)?.Key;
    private bool SourcePending => SourceEditor.Text != draft.Source;
    private bool CanTransform => !busy && !SourcePending && inlineEditor is null && draft.Model.CanEdit;

    public MermaidVisualEditorWindow(string source, DiagramService diagrams, Func<string, CancellationToken, Task<string?>> applyToDocument,
        bool dark = false, string diagramTheme = "Auto", Action? editSource = null, Action<string, string>? exportDiagram = null)
    {
        this.diagrams = diagrams; this.applyToDocument = applyToDocument; this.dark = dark; this.diagramTheme = diagramTheme;
        this.editSource = editSource; this.exportDiagram = exportDiagram; draft = new(source, diagrams);
        highContrast = SystemParameters.HighContrast; sourceHighlighting = new(dark);
        InitializeComponent();
        WindowStateService.FitInitialWindow(this);
        InitialisePaletteDragging();
        foreach (var field in new[] { NodeId, NodeLabel, FillColour, StrokeColour, TextColour, EdgeLabel, GroupLabel })
            field.TextChanged += (_, _) => PropertiesChanged();
        foreach (var field in new[] { NodeShape, NodeGroup, NodeClass, EdgeSource, EdgeTarget, EdgeType, GroupDirection })
            field.SelectionChanged += (_, _) => PropertiesChanged();
        Direction.ItemsSource = GroupDirection.ItemsSource = new[] { "TD", "TB", "BT", "LR", "RL" };
        NodeShape.ItemsSource = Enum.GetValues<FlowNodeShape>(); EdgeType.ItemsSource = Enum.GetValues<FlowConnectionType>();
        SourceEditor.TextArea.TextView.LineTransformers.Add(sourceHighlighting);
        SourceEditor.Options.ConvertTabsToSpaces = false; SourceEditor.Options.EnableHyperlinks = false; SourceEditor.Options.EnableEmailHyperlinks = false;
        EditSourceButton.IsEnabled = editSource is not null;
        Closing += ConfirmClose;
        Closed += (_, _) => { closed = true; connectionUpdate?.Abort(); lifetime.Cancel(); lifetime.Dispose(); };
        Loaded += (_, _) => { Refresh(true); Fit(this, new()); };
    }
    public void ApplyTheme(string theme)
    {
        var nextDark = ThemeService.IsDark(theme);
        if (closed || nextDark == dark && highContrast == SystemParameters.HighContrast) return;
        dark = nextDark; highContrast = SystemParameters.HighContrast; sourceHighlighting.Dark = dark;
        SourceEditor.TextArea.TextView.Redraw();
        UpdateCanvasColours();
        if (!PropertiesPending && inlineEditor is null) { updating = true; try { ShowProperties(); } finally { updating = false; } }
        UpdateActions();
    }

    private void Refresh(bool layout = false)
    {
        updating = true;
        try
        {
            SourceEditor.Text = draft.Source; Direction.SelectedItem = draft.Model.Direction;
            choices = draft.Model.Nodes.Select(node => new ElementChoice("node:" + node.Id, node.Id + " · " + node.Label.ReplaceLineEndings(" ")))
                .Concat(draft.Model.Edges.Select(edge => new ElementChoice(edge.Id, edge.Source.Id + " → " + edge.Target.Id + (edge.Label.Length == 0 ? "" : " · " + edge.Label))))
                .Concat(draft.Model.Subgraphs.Select(group => new ElementChoice("group:" + group.Id, "Subgraph · " + group.Label))).ToArray();
            Elements.ItemsSource = choices;
            Elements.UnselectAll();
            foreach (var choice in choices.Where(choice => draft.Selection.Contains(choice.Key))) Elements.SelectedItems.Add(choice);
            RebuildCanvas(layout); ShowProperties(); UpdateActions();
            Status.Text = draft.Model.Diagnostic ?? "Draft ready. Select a node or use the palette. Choose a connection port, then its destination. Middle-drag pans.";
        }
        finally { updating = false; }
    }
    private void UpdateActions()
    {
        if (SourceEditor is null) return;
        var canEdit = CanTransform;
        NodeProperties.IsEnabled = EdgeProperties.IsEnabled = GroupProperties.IsEnabled = canEdit;
        Palette.IsEnabled = Direction.IsEnabled = canEdit && !PropertiesPending;
        ApplyButton.IsEnabled = canEdit && !PropertiesPending; UndoButton.IsEnabled = !busy && !SourcePending && !PropertiesPending && draft.CanUndo;
        RedoButton.IsEnabled = !busy && !SourcePending && !PropertiesPending && draft.CanRedo;
        DeleteButton.IsEnabled = canEdit && !PropertiesPending && Elements.SelectedItems.Count > 0;
        DuplicateButton.IsEnabled = canEdit && !PropertiesPending && Elements.SelectedItems.OfType<ElementChoice>().Any(choice => choice.Key.StartsWith("node:", StringComparison.Ordinal));
        SourceEditor.IsReadOnly = busy || PropertiesPending || inlineEditor is not null; Title = "Mermaid visual editor" + (draft.IsDirty || SourcePending || PropertiesPending || inlineEditor is not null ? " •" : "");
        ResetPropertiesButton.IsEnabled = !busy && PropertiesPending;
        ValidateSourceButton.IsEnabled = !busy && !PropertiesPending && inlineEditor is null;
        foreach (var port in ports.Values) port.IsEnabled = canEdit && !PropertiesPending;
        ClassButton.IsEnabled = canEdit && NodeClass.Items.Count > 0;
    }
    private async Task<bool> Execute(MermaidEditOperation operation, string? select = null)
        => await ExecuteBatch([operation], select);
    private async Task<bool> ExecuteBatch(IReadOnlyList<MermaidEditOperation> operations, string? select = null)
    {
        if (!CanTransform) return false; busy = true; UpdateActions(); Status.Text = "Validating diagram draft…";
        try
        {
            var result = await draft.ExecuteBatchAsync(operations, lifetime.Token);
            if (closed) return false;
            if (result.Success)
            {
                if (select is not null) draft.Select([select]);
                Refresh(operations.Any(operation => operation is AddMermaidNode or DeleteMermaidNodes or AddMermaidSubgraph or DeleteMermaidSubgraph or MoveMermaidNode or ChangeMermaidDirection));
            }
            else Status.Text = result.Error;
            return result.Success;
        }
        catch (OperationCanceledException) { return false; }
        finally { busy = false; if (!closed) UpdateActions(); }
    }
    private async void InsertNode(object sender, RoutedEventArgs e)
    {
        var tag = sender is FrameworkElement element ? element.Tag as string : null;
        if (!CanTransform || PropertiesPending || !Enum.TryParse<FlowNodeShape>(tag, out var shape)) return;
        var id = MermaidTransformationService.UniqueId(draft.Model);
        await Execute(new AddMermaidNode(id, shape == FlowNodeShape.Terminal ? "Start / End" : shape.ToString(), shape), "node:" + id);
    }
    private async void InsertSubgraph(object sender, RoutedEventArgs e)
    {
        if (!CanTransform || PropertiesPending) return;
        var id = MermaidTransformationService.UniqueId(draft.Model, "Group");
        await Execute(new AddMermaidSubgraph(id, "New subgraph", Selected?.StartsWith("group:", StringComparison.Ordinal) == true ? Selected[6..] : null), "group:" + id);
    }
    private async void UpdateNode(object sender, RoutedEventArgs e)
        => await CommitNodeProperties();
    private async Task CommitNodeProperties(string? force = null)
    {
        if (Selected?.StartsWith("node:", StringComparison.Ordinal) != true || propertyBaseline is not NodeValues before) return;
        var next = ReadNodeValues(); var operations = new List<MermaidEditOperation>();
        if (next.Id != before.Id || next.Label != before.Label || next.Shape != before.Shape)
            operations.Add(new UpdateMermaidNode(before.Id, next.Label, next.Shape, next.Id));
        if (next.Group != before.Group || force == "group") operations.Add(new MoveMermaidNode(next.Id, next.Group == "(Root)" ? null : next.Group));
        if (next.Class is { } className && (next.Class != before.Class || force == "class")) operations.Add(new SetMermaidClass(next.Id, className));
        if (next.Fill != before.Fill || next.Stroke != before.Stroke || next.Colour != before.Colour || force == "style")
            operations.Add(new SetMermaidStyle(next.Id, next.Fill, next.Stroke, next.Colour));
        await ExecuteBatch(operations, "node:" + next.Id);
    }
    private async void MoveNode(object sender, RoutedEventArgs e)
    {
        await CommitNodeProperties("group");
    }
    private async void ApplyClass(object sender, RoutedEventArgs e)
    {
        await CommitNodeProperties("class");
    }
    private async void ApplyStyle(object sender, RoutedEventArgs e)
    {
        await CommitNodeProperties("style");
    }
    private async void UpdateEdge(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } id || EdgeSource.SelectedItem is not string source || EdgeTarget.SelectedItem is not string target || EdgeType.SelectedItem is not FlowConnectionType type) return;
        await Execute(new UpdateMermaidConnection(id, source, target, EdgeLabel.Text, type));
    }
    private async void ReverseEdge(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } id || !id.StartsWith("edge-", StringComparison.Ordinal)) return;
        if (EdgeSource.SelectedItem is string source && EdgeTarget.SelectedItem is string target && EdgeType.SelectedItem is FlowConnectionType type)
            await Execute(new UpdateMermaidConnection(id, target, source, EdgeLabel.Text, type));
    }
    private async void UpdateGroup(object sender, RoutedEventArgs e)
    {
        if (Selected?.StartsWith("group:", StringComparison.Ordinal) != true) return;
        var id = Selected[6..];
        var operations = new List<MermaidEditOperation> { new RenameMermaidSubgraph(id, GroupLabel.Text) };
        if (propertyBaseline is GroupValues before && GroupDirection.SelectedItem is string direction && direction != before.Direction)
            operations.Add(new ChangeMermaidDirection(direction, id));
        await ExecuteBatch(operations);
    }
    private async void DirectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!updating && Direction.SelectedItem is string direction && direction != draft.Model.Direction)
            await Execute(new ChangeMermaidDirection(direction));
    }
    private async void Delete(object sender, RoutedEventArgs e)
    {
        if (!CanTransform || PropertiesPending) return;
        var selected = Elements.SelectedItems.OfType<ElementChoice>().Select(choice => choice.Key).ToArray();
        var nodes = selected.Where(key => key.StartsWith("node:", StringComparison.Ordinal)).Select(key => key[5..]).ToArray();
        var edges = selected.Where(key => key.StartsWith("edge-", StringComparison.Ordinal)).ToArray();
        var groups = selected.Where(key => key.StartsWith("group:", StringComparison.Ordinal)).Select(key => key[6..]).ToArray();
        if (selected.Length == 0) return;
        if (selected.Length == 1 && groups.Length == 1)
        {
            var contents = DeleteGroupContents.IsChecked == true;
            if (MessageBox.Show(this, contents ? "Delete this subgraph, its descendant nodes and their connections?" : "Remove this subgraph and keep its members in the parent?",
                "Delete subgraph", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
                await Execute(new DeleteMermaidSubgraph(groups[0], contents));
            return;
        }
        var incident = draft.Model.Edges.Where(edge => nodes.Contains(edge.Source.Id) || nodes.Contains(edge.Target.Id)).Select(edge => edge.Id).ToHashSet();
        var connectionCount = incident.Concat(edges).Distinct().Count();
        if (nodes.Length > 0 || groups.Length > 0)
        {
            var message = nodes.Length == 1 && groups.Length == 0
                ? $"Delete this node and its {connectionCount} {(connectionCount == 1 ? "connection" : "connections")}?"
                : $"Delete the selected {nodes.Length} {(nodes.Length == 1 ? "node" : "nodes")}, {connectionCount} {(connectionCount == 1 ? "connection" : "connections")} and {groups.Length} subgraph containers? Unselected group members will be kept.";
            if (MessageBox.Show(this, message, "Delete selection", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        }
        var operations = new List<MermaidEditOperation>();
        operations.AddRange(edges.Where(id => !incident.Contains(id)).Select(id => new DeleteMermaidConnection(id)));
        if (nodes.Length > 0) operations.Add(new DeleteMermaidNodes(nodes));
        operations.AddRange(groups.OrderByDescending(id => draft.Model.Subgraph(id).Opening.Start).Select(id => new DeleteMermaidSubgraph(id)));
        await ExecuteBatch(operations);
    }
    private async void Duplicate(object sender, RoutedEventArgs e)
    {
        if (!CanTransform || PropertiesPending) return;
        var ids = Elements.SelectedItems.OfType<ElementChoice>().Where(choice => choice.Key.StartsWith("node:", StringComparison.Ordinal)).Select(choice => choice.Key[5..]).ToHashSet();
        if (ids.Count == 0) return;
        var selected = new MermaidSourceParser().Parse(CopiedSource(ids));
        await InsertCopiedGraph(selected, true);
    }
    private void Undo(object sender, RoutedEventArgs e) { if (!busy && !SourcePending && !PropertiesPending) { draft.Undo(); Refresh(true); } }
    private void Redo(object sender, RoutedEventArgs e) { if (!busy && !SourcePending && !PropertiesPending) { draft.Redo(); Refresh(true); } }
    private void StartConnection(object sender, RoutedEventArgs e)
    {
        if (!CanTransform || PropertiesPending) return;
        connectionSource = Selected?.StartsWith("node:", StringComparison.Ordinal) == true ? Selected[5..] : null;
        Status.Text = connectionSource is null ? "Select a source node first, then choose Connection or its + port." : "Connection from " + connectionSource + ": select the destination node. Escape cancels.";
    }
    private void SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updating) return;
        if (PropertiesPending || inlineEditor is not null)
        {
            updating = true;
            try { Elements.SelectedItems.Clear(); foreach (var choice in choices.Where(choice => draft.Selection.Contains(choice.Key))) Elements.SelectedItems.Add(choice); }
            finally { updating = false; }
            UpdateCanvasSelection(); Status.Text = inlineEditor is null ? "Update the current properties or reset their changes before selecting another element." : "Press Enter to confirm the inline label, or Escape to cancel it."; return;
        }
        draft.Select(Elements.SelectedItems.OfType<ElementChoice>().Select(choice => choice.Key));
        ShowProperties(); UpdateCanvasSelection(); UpdateActions();
    }
    private void SelectElement(string key, bool extend = false)
    {
        if (PropertiesPending || inlineEditor is not null)
        { UpdateCanvasSelection(); inlineEditor?.Focus(); Status.Text = "Confirm or reset the current element's pending edits before changing selection."; return; }
        var choice = choices.FirstOrDefault(item => item.Key == key); if (choice is null) return;
        if (!extend) Elements.SelectedItems.Clear();
        if (extend && Elements.SelectedItems.Contains(choice)) Elements.SelectedItems.Remove(choice); else Elements.SelectedItems.Add(choice);
        Elements.ScrollIntoView(choice);
    }
    private void ShowProperties()
    {
        propertyBaseline = null;
        NodeProperties.Visibility = EdgeProperties.Visibility = GroupProperties.Visibility = Visibility.Collapsed;
        SelectionSummary.Text = Selected is null ? "Select a node, connection or subgraph." : Elements.SelectedItems.Count > 1 ? Elements.SelectedItems.Count + " elements selected" : choices.FirstOrDefault(choice => choice.Key == Selected)?.Description;
        if (Elements.SelectedItems.Count != 1 || Selected is not { } key) return;
        if (key.StartsWith("node:", StringComparison.Ordinal))
        {
            var node = draft.Model.Node(key[5..]); NodeProperties.Visibility = Visibility.Visible;
            NodeId.Text = node.Id; NodeLabel.Text = node.Label; NodeShape.SelectedItem = node.Shape;
            NodeGroup.ItemsSource = new[] { "(Root)" }.Concat(draft.Model.Subgraphs.Select(group => group.Id)); NodeGroup.SelectedItem = node.SubgraphId ?? "(Root)";
            NodeClass.ItemsSource = draft.Model.Styles.Where(style => style.Kind == "classDef").SelectMany(style => style.Targets).Select(target => target.Id).Distinct().ToArray();
            NodeClass.SelectedItem = draft.Model.ClassForNode(node.Id);
            var palette = DiagramThemes.Colours(DiagramThemes.ResolveName(diagramTheme, dark));
            var colours = Colours(node, palette.Surface ?? (dark ? "#303339" : "#F8F9FB"), palette.Border ?? palette.Accent, palette.Foreground);
            FillColour.Text = Hex(colours.Fill); StrokeColour.Text = Hex(colours.Stroke); TextColour.Text = Hex(colours.Text);
        }
        else if (key.StartsWith("edge-", StringComparison.Ordinal))
        {
            var edge = draft.Model.Edge(key); EdgeProperties.Visibility = Visibility.Visible;
            EdgeSource.ItemsSource = EdgeTarget.ItemsSource = draft.Model.Nodes.Select(node => node.Id).ToArray();
            EdgeSource.SelectedItem = edge.Source.Id; EdgeTarget.SelectedItem = edge.Target.Id; EdgeLabel.Text = edge.Label; EdgeType.SelectedItem = edge.Type;
        }
        else
        {
            var group = draft.Model.Subgraph(key[6..]); GroupProperties.Visibility = Visibility.Visible;
            GroupId.Text = group.Id; GroupLabel.Text = group.Label; GroupDirection.SelectedItem = draft.Model.EffectiveDirection(group.Id);
            GroupMembers.Text = "Parent: " + (group.ParentId ?? "Root") + "\nMembers: " + string.Join(", ", draft.Model.Nodes.Where(node => node.SubgraphId == group.Id).Select(node => node.Id));
        }
        propertyBaseline = PropertyValues;
        static string Hex(System.Windows.Media.Brush brush) => brush is System.Windows.Media.SolidColorBrush solid ? $"#{solid.Color.R:X2}{solid.Color.G:X2}{solid.Color.B:X2}" : "#FAFAFA";
    }
    private void PropertiesChanged()
    {
        if (updating || SourceEditor is null) return;
        UpdateActions(); if (PropertiesPending) Status.Text = "Properties changed. Update the selected element to include these values in the draft, or reset the property changes.";
    }
    private void ResetProperties(object sender, RoutedEventArgs e)
    { updating = true; try { ShowProperties(); } finally { updating = false; } UpdateActions(); Status.Text = "Unapplied property changes reset."; }
    private void SourceChanged(object? sender, EventArgs e)
    {
        if (updating || Status is null) return;
        connectionSource = null;
        UpdateActions(); Status.Text = SourcePending ? "Source draft changed. Validate it before continuing visually or applying. Your text is retained if validation fails." : "Source matches the visual draft.";
    }
    private async void ValidateSource(object sender, RoutedEventArgs e)
    {
        if (busy || PropertiesPending || inlineEditor is not null) return;
        busy = true; UpdateActions(); var text = SourceEditor.Text;
        try
        {
            var result = await draft.ValidateAndCommitAsync(text, lifetime.Token); if (closed) return;
            if (result.Success) Refresh(true); else Status.Text = result.Error + " Your source draft has been retained; correct it or use Undo within the source panel.";
        }
        catch (OperationCanceledException) { }
        finally { busy = false; if (!closed) UpdateActions(); }
    }
    private async void Apply(object sender, RoutedEventArgs e)
    {
        if (!CanTransform || PropertiesPending) return; busy = true; UpdateActions(); Status.Text = "Validating source and document identity…";
        try
        {
            var source = draft.Source; var result = await Task.Run(() => diagrams.Render(source, dark, diagramTheme), lifetime.Token);
            if (closed) return; if (!result.Success) { Status.Text = result.Error; return; }
            var error = await applyToDocument(source, lifetime.Token); if (closed) return;
            if (error is not null) { Status.Text = error; return; }
            discard = true; Close();
        }
        catch (OperationCanceledException) { }
        catch (ArgumentException ex) { Status.Text = ex.Message; }
        catch (Exception ex) when (ex is not OutOfMemoryException) { if (!closed) Status.Text = "The diagram could not be applied: " + ex.Message[..Math.Min(ex.Message.Length, 300)]; }
        finally { busy = false; if (!closed) UpdateActions(); }
    }
    private void Cancel(object sender, RoutedEventArgs e) { discard = true; Close(); }
    private void ConfirmClose(object? sender, CancelEventArgs e)
    {
        if (!discard && (draft.IsDirty || SourcePending || PropertiesPending || inlineEditor is not null) && MessageBox.Show(this, "Discard this visual editor's unsaved draft? The document will keep its current source.",
            "Unsaved Mermaid draft", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) e.Cancel = true;
    }
    public bool CloseForOwner()
    {
        if ((draft.IsDirty || SourcePending || PropertiesPending || inlineEditor is not null) && MessageBox.Show(this, "Discard this visual editor's unsaved draft before closing the application?",
            "Unsaved Mermaid draft", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return false;
        discard = true; Close(); return true;
    }
    private void CopySource(object sender, RoutedEventArgs e)
    {
        if (SourceEditor.Text.Length == 0) { Status.Text = "The source draft is empty. Add Mermaid source before copying."; return; }
        try { Clipboard.SetText(SourceEditor.Text); Status.Text = "Mermaid draft source copied."; }
        catch (ExternalException) { Status.Text = "The Windows clipboard is busy. Select the source and try again."; }
    }
    private void EditOwningSource(object sender, RoutedEventArgs e) { editSource?.Invoke(); Status.Text = "The owning document's source is available. This private draft remains open."; }
    private async void ShowRenderedPreview(object sender, RoutedEventArgs e)
    {
        if (busy || SourcePending || PropertiesPending || inlineEditor is not null) return;
        busy = true; UpdateActions();
        try
        {
            var result = await Task.Run(() => diagrams.Render(draft.Source, dark, diagramTheme), lifetime.Token); if (closed) return;
            if (!result.Success) { Status.Text = result.Error; return; }
            new DiagramWindow(new NativeSvgService().Render(result.Svg!), format => exportDiagram?.Invoke(format, result.Svg!)) { Owner = this }.Show();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is not OutOfMemoryException) { if (!closed) Status.Text = "Preview could not be opened: " + ex.Message; }
        finally { busy = false; if (!closed) UpdateActions(); }
    }
    private void Shortcut(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; Apply(sender, e); return; }
        if (e.Key == Key.F11) { ToggleFullScreen(); e.Handled = true; return; }
        var textEntry = Keyboard.FocusedElement is TextBox || SourceEditor.IsKeyboardFocusWithin;
        if (textEntry) return;
        if (Keyboard.Modifiers == ModifierKeys.Shift && e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            if (e.Key == Key.Left) Viewport.ScrollToHorizontalOffset(Viewport.HorizontalOffset - 40);
            else if (e.Key == Key.Right) Viewport.ScrollToHorizontalOffset(Viewport.HorizontalOffset + 40);
            else if (e.Key == Key.Up) Viewport.ScrollToVerticalOffset(Viewport.VerticalOffset - 40);
            else Viewport.ScrollToVerticalOffset(Viewport.VerticalOffset + 40);
            e.Handled = true; return;
        }
        var control = Keyboard.Modifiers == ModifierKeys.Control;
        if (control && e.Key == Key.Z) Undo(sender, e);
        else if (control && e.Key == Key.Y) Redo(sender, e);
        else if (control && e.Key == Key.D) Duplicate(sender, e);
        else if (control && e.Key == Key.A) Elements.SelectAll();
        else if (control && e.Key == Key.C) CopyNodes();
        else if (control && e.Key == Key.V) PasteNodes();
        else if (control && e.Key == Key.D0) ActualSize(sender, e);
        else if (e.Key == Key.Delete) Delete(sender, e);
        else if (e.Key == Key.F2 && Selected?.StartsWith("node:", StringComparison.Ordinal) == true) BeginInlineEdit(Selected[5..]);
        else if (e.Key == Key.F) Fit(sender, e);
        else if (e.Key == Key.Home) ResetView(sender, e);
        else if (e.Key == Key.F4) { if (NodeProperties.IsVisible) NodeLabel.Focus(); else if (EdgeProperties.IsVisible) EdgeLabel.Focus(); else if (GroupProperties.IsVisible) GroupLabel.Focus(); }
        else if (e.Key == Key.Escape) { connectionSource = null; Elements.UnselectAll(); Status.Text = "Selection cleared. The draft remains open."; }
        else if (e.Key == Key.Insert) InsertNode(Palette.Children.OfType<Button>().First(), e);
        else return;
        e.Handled = true;
    }
    private void ToggleFullScreen()
    {
        if (!fullScreen) { previousStyle = WindowStyle; previousState = WindowState; previousResize = ResizeMode; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; WindowState = WindowState.Maximized; }
        else { WindowStyle = previousStyle; ResizeMode = previousResize; WindowState = previousState; }
        fullScreen = !fullScreen;
    }
    private void SourceCollapsed(object sender, RoutedEventArgs e)
    {
        if (sender is not Expander { Parent: Grid grid }) return;
        sourcePanelHeight = grid.RowDefinitions[2].ActualHeight; grid.RowDefinitions[2].Height = GridLength.Auto; grid.RowDefinitions[0].Height = new(1, GridUnitType.Star);
    }
    private void SourceExpanded(object sender, RoutedEventArgs e)
    {
        if (sender is not Expander { Parent: Grid grid } || grid.ActualHeight <= 0) return;
        grid.RowDefinitions[2].Height = new(Math.Max(120, Math.Min(sourcePanelHeight, grid.ActualHeight * .55)));
        grid.RowDefinitions[0].Height = new(1, GridUnitType.Star);
    }
    private void CopyNodes()
    {
        var ids = Elements.SelectedItems.OfType<ElementChoice>().Where(choice => choice.Key.StartsWith("node:", StringComparison.Ordinal)).Select(choice => choice.Key[5..]).ToHashSet();
        if (ids.Count == 0) return;
        var text = CopiedSource(ids);
        try { Clipboard.SetText(text); Status.Text = "Selected nodes, styles and internal connections copied as portable Mermaid source."; }
        catch (ExternalException) { Status.Text = "The Windows clipboard is busy. Try copying again."; }
    }
    private string CopiedSource(ISet<string> ids)
    {
        var classes = ids.Select(draft.Model.ClassForNode).Where(name => name is not null).ToHashSet();
        var text = "flowchart " + draft.Model.Direction + draft.Model.NewLine
            + string.Join(draft.Model.NewLine, draft.Model.Nodes.Where(node => ids.Contains(node.Id)).Select(node => MermaidTransformationService.NodeText(node.Id, node.Label, node.Shape))
                .Concat(draft.Model.Edges.Where(edge => ids.Contains(edge.Source.Id) && ids.Contains(edge.Target.Id))
                    .Select(edge => edge.Source.Id + " " + MermaidTransformationService.ConnectionToken(edge.Type) + (edge.Label.Length == 0 ? "" : "|" + MermaidTransformationService.EscapeLabel(edge.Label) + "|") + " " + edge.Target.Id)));
        foreach (var style in draft.Model.Styles)
        {
            if (style.Kind == "classDef")
            { if (style.Targets.Any(target => classes.Contains(target.Id))) text += draft.Model.NewLine + style.Statement.Read(draft.Source); }
            else
            {
                var targets = style.Targets.Where(target => ids.Contains(target.Id)).Select(target => target.Id).ToArray();
                if (targets.Length > 0) text += draft.Model.NewLine + style.Kind + " " + string.Join(',', targets) + " " + style.Value;
            }
        }
        foreach (var node in draft.Model.Nodes.Where(node => ids.Contains(node.Id)))
            if (draft.Model.ClassForNode(node.Id) is { } className)
                text += draft.Model.NewLine + "class " + node.Id + " " + className;
        return text;
    }
    private async void PasteNodes()
    {
        if (!CanTransform || PropertiesPending) return;
        try
        {
            var copied = new MermaidSourceParser().Parse(Clipboard.GetText());
            if (!copied.CanEdit || copied.Subgraphs.Count != 0) { Status.Text = "Paste a supported Flowchart containing nodes and separate connections. Use source mode for subgraphs."; return; }
            await InsertCopiedGraph(copied, false);
        }
        catch (ExternalException) { Status.Text = "The Windows clipboard is busy. Try pasting again."; }
    }
    private async Task InsertCopiedGraph(MermaidDiagramModel copied, bool keepGroups)
    {
        if (!copied.CanEdit) { Status.Text = copied.Diagnostic; return; }
        var used = draft.Model.Nodes.Select(node => node.Id).Concat(draft.Model.Subgraphs.Select(group => group.Id)).ToHashSet();
        var classNames = draft.Model.Styles.Where(style => style.Kind == "classDef").SelectMany(style => style.Targets).Select(target => target.Id).ToHashSet();
        var classMap = new Dictionary<string, string>(); var map = new Dictionary<string, string>(); var operations = new List<MermaidEditOperation>();
        foreach (var definition in copied.Styles.Where(style => style.Kind == "classDef"))
        {
            var name = definition.Targets[0].Id;
            var existing = draft.Model.Styles.LastOrDefault(style => style.Kind == "classDef" && style.Targets.Any(target => target.Id == name));
            if (existing?.Value == definition.Value) { classMap[name] = name; continue; }
            var newName = existing is null ? name : Allocate(classNames, "CopiedClass"); classMap[name] = newName; classNames.Add(newName);
            operations.Add(new AddMermaidClassDefinition(newName, definition.Value));
        }
        foreach (var node in copied.Nodes)
        {
            var id = Allocate(used, "Node"); map[node.Id] = id;
            operations.Add(new AddMermaidNode(id, node.Label, node.Shape, keepGroups ? draft.Model.Node(node.Id).SubgraphId : null));
        }
        foreach (var edge in copied.Edges) operations.Add(new AddMermaidConnection(map[edge.Source.Id], map[edge.Target.Id], edge.Label, edge.Type));
        foreach (var style in copied.Styles.Where(style => style.Kind is "class" or "style"))
            foreach (var target in style.Targets)
            {
                if (style.Kind == "class") operations.Add(new SetMermaidClass(map[target.Id], classMap.GetValueOrDefault(style.Value, style.Value)));
                else operations.Add(new SetMermaidStyleProperties(map[target.Id], style.Value));
            }
        if (await ExecuteBatch(operations))
        {
            Elements.UnselectAll(); foreach (var pair in map) SelectElement("node:" + pair.Value, true);
            Status.Text = "Copied nodes, supported styles and connections inserted as one visual Undo action.";
        }
        static string Allocate(ISet<string> names, string prefix)
        { for (var index = 1; ; index++) { var id = prefix + index; if (names.Add(id)) return id; } }
    }
}
