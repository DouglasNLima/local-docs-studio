using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.StateDiagrams;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public partial class StateVisualEditorWindow : Window
{
    private StateVisualDraft draft = null!;
    private readonly DiagramService diagrams;
    private readonly NativeSvgService svg = new();
    private readonly Func<string, CancellationToken, Task<string?>> applyToDocument;
    private readonly Action? editSource;
    private readonly string diagramTheme;
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim renderGate = new(1, 1);
    private CancellationTokenSource? pendingRender;
    private readonly MermaidSourceHighlighting highlighting;
    private bool dark, updating, busy = true, closed, discard, mapped;
    private int renderGeneration;
    private string mode = "inspect", baseline = "";
    private string? canvasPick;
    private Point? pan;
    private double panHorizontal, panVertical;
    private NativeStatePresentation? presentation;
    private (StateDiagramModel Model, bool Dark, NativeStatePresentation View)? preparedPresentation;
    private record Choice(string Identity, string Description) { public override string ToString() => Description; }
    private bool SourcePending => draft is not null && SourceEditor.Text != draft.Source;
    private string Values => string.Join('\u001f', NodeId.Text, LabelText.Text, From.SelectedValue, To.SelectedValue);
    private bool PropertiesPending => draft is not null && (mode != "inspect" || baseline != Values);
    private bool CanEdit => draft is not null && !busy && !SourcePending && mapped && draft.Model.IsValid;
    public StateVisualEditorWindow(string source, DiagramService diagrams, Func<string, CancellationToken, Task<string?>> applyToDocument,
        bool dark = false, string diagramTheme = "Auto", Action? editSource = null, FontFamily? sourceFont = null, double sourceFontSize = 14)
    {
        this.diagrams = diagrams; this.applyToDocument = applyToDocument; this.dark = dark; this.diagramTheme = diagramTheme; this.editSource = editSource; highlighting = new(dark);
        InitializeComponent(); WindowStateService.FitInitialWindow(this);
        SourceEditor.FontFamily = sourceFont ?? new FontFamily("Cascadia Code, Consolas"); SourceEditor.FontSize = sourceFontSize;
        SourceEditor.Options.EnableHyperlinks = false; SourceEditor.Options.EnableEmailHyperlinks = false; SourceEditor.TextArea.TextView.LineTransformers.Add(highlighting);
        foreach (var field in new[] { NodeId, LabelText }) field.TextChanged += (_, _) => { if (!updating) UpdateActions(); };
        foreach (var field in new[] { From, To }) field.SelectionChanged += (_, _) => { if (!updating) UpdateActions(); };
        EditSourceButton.IsEnabled = editSource is not null; IsEnabled = false;
        Loaded += async (_, _) =>
        {
            try { draft = await Task.Run(() => new StateVisualDraft(source, diagrams, ValidateNativeCandidateAsync), lifetime.Token); if (closed) return; busy = false; IsEnabled = true; await RefreshAsync(true); }
            catch (OperationCanceledException) { }
        };
        Closing += ConfirmClose;
        Closed += (_, _) => { closed = true; renderGeneration++; CancelPointer(); lifetime.Cancel(); };
        Deactivated += (_, _) => { EndPan(); CancelPointer(); };
    }
    public async void ApplyTheme(string theme)
    { dark = ThemeService.IsDark(theme); highlighting.Dark = dark; SourceEditor.TextArea.TextView.Redraw(); if (!closed && draft is not null) await RenderAsync(false); }
    private IEnumerable<Choice> Choices()
    {
        if (!draft.Model.IsValid) { yield return new("invalid", "Invalid source · inspect diagnostics and Edit source"); yield break; }
        foreach (var choice in ScopeChoices(null, 0)) yield return choice;
        foreach (var (edge, index) in draft.Model.Transitions.Select((item, index) => (item, index))) yield return new(edge.Identity, "Transition " + (index + 1) + " · " + EndpointName(edge.Source) + " → " + EndpointName(edge.Destination) + (edge.Label.Length > 0 ? " · " + edge.Label : ""));
        foreach (var statement in draft.Model.Statements.Where(item => item.Kind is StateStatementKind.Note or StateStatementKind.Style or StateStatementKind.Unknown))
            yield return new(statement.Identity, "Protected " + statement.Kind + " · " + statement.Span.Read(draft.Source).ReplaceLineEndings(" "));
        IEnumerable<Choice> ScopeChoices(string? scope, int depth)
        {
            var children = draft.Model.Nodes.Where(item => item.First.Scope == scope).Select(item => (At: item.First.Span.Start, Choice: new Choice(item.Identity, item.Id + " · " + item.Label + (item.Scopes.Count > 1 ? " (shared scopes)" : "")), Group: (string?)null))
                .Concat(draft.Model.Groups.Where(item => item.Parent == scope).Select(item => (At: item.Opening.Span.Start, Choice: new Choice(item.Identity, "Composite · " + item.Id + " · " + item.Label), Group: (string?)item.Id)))
                .Concat(draft.Model.PseudoNodes.Where(item => item.Scope == scope).Select(item => (At: draft.Model.Transitions.First(edge => edge.Source == item.Identity || edge.Destination == item.Identity).Statement.Span.Start, Choice: new Choice(item.Identity, EndpointName(item.Identity)), Group: (string?)null))).OrderBy(item => item.At);
            foreach (var child in children) { yield return child.Choice with { Description = new string(' ', depth * 2) + child.Choice.Description }; if (child.Group is not null) foreach (var nested in ScopeChoices(child.Group, depth + 1)) yield return nested; }
        }
    }
    private string EndpointName(string id) => draft.Model.PseudoNodes.FirstOrDefault(item => item.Identity == id) is { } pseudo ? (pseudo.Initial ? "Initial" : "Final") + " (" + (pseudo.Scope ?? "diagram") + ")" : id;
    private async Task RefreshAsync(bool fit = false)
    {
        updating = true;
        try { SourceEditor.Text = draft.Source; Elements.ItemsSource = Choices().ToArray(); RestoreSelection(); ShowProperties(); }
        finally { updating = false; }
        await RenderAsync(fit); UpdateActions();
    }
    private async Task ValidateNativeCandidateAsync(StateDiagramModel candidate, CancellationToken cancellationToken)
    {
        var version = renderGeneration; var themeDark = dark;
        await renderGate.WaitAsync(cancellationToken);
        try
        {
            var render = await Task.Run(() => diagrams.Render(candidate.Source, themeDark, diagramTheme, cancellationToken), cancellationToken);
            var view = await StaWorker.RunAsync(() => NativeStatePresentation.Create(candidate, render, svg), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (closed || version != renderGeneration) throw new ArgumentException("The State presentation changed during validation. Retry against the current presentation; the draft is unchanged.");
            preparedPresentation = (candidate, themeDark, view);
        }
        finally { renderGate.Release(); }
    }
    private async Task RenderAsync(bool fit)
    {
        CancelPointer(); canvasPick = null; var version = ++renderGeneration; mapped = false; UpdateActions(); var model = draft.Model; var themeDark = dark;
        Status.Text = "Rendering canonical State diagram…"; pendingRender?.Cancel();
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); pendingRender = cancellation; var leased = false;
        try
        {
            await renderGate.WaitAsync(cancellation.Token); leased = true;
            var render = await Task.Run(() => diagrams.Render(model.Source, themeDark, diagramTheme, cancellation.Token), cancellation.Token);
            if (closed || version != renderGeneration) return;
            var ready = preparedPresentation is { } prepared && ReferenceEquals(prepared.Model, model) && prepared.Dark == themeDark ? prepared.View : null;
            preparedPresentation = null;
            var converted = await StaWorker.RunAsync(() =>
            {
                NativeStatePresentation? view = ready; string? diagnostic = null;
                try { view ??= NativeStatePresentation.Create(model, render, svg); }
                catch (Exception error) when (error is ArgumentException or InvalidDataException or FormatException) { diagnostic = error.Message; }
                return (view, Drawing: view?.Canonical.Drawing ?? (render.Success ? svg.Render(render.Svg!) : null), diagnostic);
            }, cancellation.Token);
            if (closed || version != renderGeneration) return;
            presentation = converted.view;
            if (presentation is not null) { mapped = true; BuildCanvas(); }
            else
            {
                Surface.Children.Clear(); buttons.Clear();
                var image = new Image { Source = converted.Drawing, Stretch = Stretch.None, IsHitTestVisible = false };
                Surface.Children.Add(image); Surface.Width = converted.Drawing?.Width ?? 420; Surface.Height = converted.Drawing?.Height ?? 260;
            }
            Status.Text = converted.diagnostic ?? (model.HasUnknownDependencies ? "Protected source retained. Unrecognised dependencies require Edit source." : "Ready. Select a state or transition. Drag states for temporary positioning; Auto layout restores canonical positions.");
            AutomationProperties.SetItemStatus(this, SystemParameters.HighContrast ? "High Contrast" : themeDark ? "Dark" : "Light");
            if (fit) { Surface.UpdateLayout(); Fit(this, new()); }
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is not OutOfMemoryException) { if (!closed && version == renderGeneration) { mapped = false; Status.Text = error.Message; } }
        finally { if (leased) renderGate.Release(); if (ReferenceEquals(pendingRender, cancellation)) pendingRender = null; cancellation.Dispose(); if (!closed && version == renderGeneration) UpdateActions(); }
    }
    private void RestoreSelection() => Elements.SelectedItem = Elements.Items.OfType<Choice>().FirstOrDefault(item => item.Identity == draft.Selection);
    private void SelectionChanged(object sender, SelectionChangedEventArgs e)
    { if (!updating && Elements.SelectedItem is Choice item) Select(item.Identity); }
    private void Select(string? identity)
    {
        if (busy || SourcePending || PropertiesPending) { Status.Text = "Update or discard pending properties and validate source before changing selection."; updating = true; try { RestoreSelection(); UpdateSelection(); } finally { updating = false; } return; }
        draft.Select(identity); updating = true; try { RestoreSelection(); ShowProperties(); UpdateSelection(); } finally { updating = false; } UpdateActions();
    }
    private void ShowProperties()
    {
        canvasPick = null; mode = "inspect"; NodeFields.Visibility = TransitionFields.Visibility = LabelFields.Visibility = Visibility.Collapsed;
        Summary.Text = "Select a state, scoped marker, transition or composite."; Dependencies.Text = "";
        var endpoints = draft.Model.Nodes.Select(item => new Choice(item.Id, item.Id + " · " + item.Label)).ToArray();
        From.ItemsSource = endpoints.Concat(draft.Model.PseudoNodes.Where(item => item.Initial).Select(item => new Choice(item.Identity, EndpointName(item.Identity)))).ToArray();
        To.ItemsSource = endpoints.Concat(draft.Model.PseudoNodes.Where(item => !item.Initial).Select(item => new Choice(item.Identity, EndpointName(item.Identity)))).ToArray();
        if (draft.Model.Nodes.FirstOrDefault(item => item.Identity == draft.Selection) is { } node)
        {
            NodeFields.Visibility = LabelFields.Visibility = Visibility.Visible; NodeId.Text = node.Id; LabelText.Text = node.Label;
            Summary.Text = "State " + node.Id; Dependencies.Text = "Parent: " + string.Join(", ", node.Scopes.Select(item => item.Length == 0 ? "diagram" : item))
                + ". " + draft.Model.Transitions.Count(item => item.Source == node.Id || item.Destination == node.Id) + " incident transitions."
                + (node.Declaration is null ? " The first reference is implicit; changing its label requires an earlier declaration." : "");
        }
        else if (draft.Model.Transitions.FirstOrDefault(item => item.Identity == draft.Selection) is { } transition)
        {
            TransitionFields.Visibility = LabelFields.Visibility = Visibility.Visible; From.SelectedValue = transition.Source; To.SelectedValue = transition.Destination; LabelText.Text = transition.Label;
            Summary.Text = "Transition " + EndpointName(transition.Source) + " → " + EndpointName(transition.Destination);
            Dependencies.Text = "Exact source occurrence " + (draft.Model.Transitions.ToList().IndexOf(transition) + 1) + ". Scope: " + (transition.Statement.Scope ?? "diagram") + ".";
        }
        else if (draft.Model.Groups.FirstOrDefault(item => item.Identity == draft.Selection) is { } group)
        { LabelFields.Visibility = Visibility.Visible; LabelText.Text = group.Label; Summary.Text = "Composite " + group.Id; Dependencies.Text = "Parent: " + (group.Parent ?? "diagram") + ". Membership, movement and structural operations are protected."; }
        else if (draft.Model.PseudoNodes.FirstOrDefault(item => item.Identity == draft.Selection) is { } pseudo)
        { Summary.Text = EndpointName(pseudo.Identity); Dependencies.Text = "Scoped " + (pseudo.Initial ? "initial source" : "final destination") + ". Remove or reconnect its transition; this marker is never a globally renamable state."; }
        else if (draft.Model.Statements.FirstOrDefault(item => item.Identity == draft.Selection) is { } statement)
        { Summary.Text = "Protected " + statement.Kind; Dependencies.Text = statement.Span.Read(draft.Source); }
        baseline = Values;
    }
    private StateEditOperation? UpdateOperation() => mode == "state" ? new AddStateNode(NodeId.Text, LabelText.Text)
        : mode == "transition" ? new AddStateTransition(From.SelectedValue as string ?? "", To.SelectedValue as string ?? "", LabelText.Text)
        : draft.Model.Nodes.FirstOrDefault(item => item.Identity == draft.Selection) is { } node ? new UpdateStateNode(node.Id, NodeId.Text, LabelText.Text)
        : draft.Model.Transitions.FirstOrDefault(item => item.Identity == draft.Selection) is { } edge ? new UpdateStateTransition(edge.Identity, From.SelectedValue as string ?? "", To.SelectedValue as string ?? "", LabelText.Text)
        : draft.Model.Groups.FirstOrDefault(item => item.Identity == draft.Selection) is { } group ? new UpdateStateGroupLabel(group.Id, LabelText.Text) : null;
    private StateEditOperation? DeleteOperation() => draft.Model.Nodes.FirstOrDefault(item => item.Identity == draft.Selection) is { } node ? new DeleteStateNode(node.Id)
        : draft.Model.Transitions.Any(item => item.Identity == draft.Selection) ? new DeleteStateTransition(draft.Selection!) : null;
    private StateEditOperation? ReverseOperation() => draft.Model.Transitions.FirstOrDefault(item => item.Identity == draft.Selection) is { } edge ? new UpdateStateTransition(edge.Identity, edge.Destination, edge.Source, edge.Label) : null;
    private string NextId(string prefix = "State") { var number = 1; while (draft.Model.Nodes.Any(item => item.Id == prefix + number) || draft.Model.Groups.Any(item => item.Id == prefix + number)) number++; return prefix + number; }
    private void UpdateActions()
    {
        if (draft is null) return;
        var clean = !PropertiesPending; var can = CanEdit;
        void Action(Button button, StateEditOperation? operation, bool requireClean = true)
        {
            var capability = operation is null ? new StateOperationCapability(false, "Select an eligible State element.") : StateCapabilityAnalysis.For(draft.Model, operation);
            button.IsEnabled = can && (!requireClean || clean) && capability.Allowed;
            button.ToolTip = capability.Reason; ToolTipService.SetShowOnDisabled(button, true); AutomationProperties.SetHelpText(button, capability.Reason);
        }
        Action(UpdateButton, UpdateOperation(), false); Action(DeleteButton, DeleteOperation()); Action(ReverseButton, ReverseOperation());
        var node = draft.Model.Nodes.FirstOrDefault(item => item.Identity == draft.Selection);
        Action(DuplicateButton, node is null ? null : new DuplicateStateNode(node.Id, NextId("Copy")));
        Action(AddStateButton, new AddStateNode(NextId(), "New state"));
        var ids = draft.Model.Nodes.Select(item => item.Id).ToArray(); var transition = ids.Length > 0 ? new AddStateTransition(ids[0], ids[^1]) : null;
        Action(AddTransitionButton, transition); Action(InitialButton, ids.Length > 0 ? new AddStateTransition("[*]", ids[0]) : null); Action(FinalButton, ids.Length > 0 ? new AddStateTransition(ids[0], "[*]") : null);
        UndoButton.IsEnabled = !busy && clean && !SourcePending && draft.CanUndo; RedoButton.IsEnabled = !busy && clean && !SourcePending && draft.CanRedo;
        ApplyButton.IsEnabled = can && clean; ValidateButton.IsEnabled = !busy && clean; ResetButton.IsEnabled = !busy && !clean;
        NodeId.IsEnabled = can && (mode == "state" || node is not null && StateCapabilityAnalysis.For(draft.Model, new UpdateStateNode(node.Id, NextId("Renamed"), node.Label)).Allowed);
        var edge = draft.Model.Transitions.FirstOrDefault(item => item.Identity == draft.Selection);
        var reconnect = can && (mode == "transition" || edge is not null && draft.Model.IsSimple && !draft.Model.HasUnknownDependencies);
        From.IsEnabled = To.IsEnabled = ReconnectFromButton.IsEnabled = ReconnectToButton.IsEnabled = reconnect;
        var capabilities = new List<string> { "Source: " + (draft.Model.Compatibility == StateSourceCompatibility.PartiallyEditable ? "partially editable" : draft.Model.Compatibility.ToString().ToLowerInvariant()) + "." };
        if (node is not null)
        {
            void Describe(string name, StateEditOperation operation) { var result = StateCapabilityAnalysis.For(draft.Model, operation); capabilities.Add(name + ": " + (result.Allowed ? "editable" : "protected — " + result.Reason)); }
            Describe("Label", new UpdateStateNode(node.Id, node.Id, node.Label)); Describe("ID", new UpdateStateNode(node.Id, NextId("Renamed"), node.Label)); Describe("Delete", new DeleteStateNode(node.Id));
        }
        else if (edge is not null) { capabilities.Add("Label: " + (StateCapabilityAnalysis.For(draft.Model, new UpdateStateTransition(edge.Identity, edge.Source, edge.Destination, edge.Label)).Allowed ? "editable" : "protected")); capabilities.Add("Endpoints / delete: " + (draft.Model.IsSimple && !draft.Model.HasUnknownDependencies ? "validate each change" : "protected by structural dependencies")); }
        Capabilities.Text = string.Join("\n", capabilities);
    }
    private void BeginCreation(string kind)
    {
        if (!CanEdit || PropertiesPending) return; updating = true;
        try { ShowProperties(); mode = kind; Summary.Text = "New " + kind; LabelFields.Visibility = Visibility.Visible; NodeFields.Visibility = kind == "state" ? Visibility.Visible : Visibility.Collapsed; TransitionFields.Visibility = kind == "transition" ? Visibility.Visible : Visibility.Collapsed; NodeId.Text = NextId(); LabelText.Text = kind == "state" ? "New state" : ""; From.SelectedIndex = 0; To.SelectedIndex = Math.Min(1, To.Items.Count - 1); canvasPick = kind == "transition" ? "from" : null; }
        finally { updating = false; } UpdateActions(); LabelText.Focus(); LabelText.SelectAll();
    }
    private void AddState(object sender, RoutedEventArgs e) => BeginCreation("state");
    private void AddTransition(object sender, RoutedEventArgs e) => BeginCreation("transition");
    private void AddInitial(object sender, RoutedEventArgs e) { BeginCreation("transition"); From.ItemsSource = new[] { new Choice("[*]", "Initial (diagram)") }; From.SelectedValue = "[*]"; canvasPick = "to"; UpdateActions(); }
    private void AddFinal(object sender, RoutedEventArgs e) { BeginCreation("transition"); To.ItemsSource = new[] { new Choice("[*]", "Final (diagram)") }; To.SelectedValue = "[*]"; canvasPick = "from-final"; UpdateActions(); }
    private void ReconnectFrom(object sender, RoutedEventArgs e) { canvasPick = "from-reconnect"; Status.Text = "Choose the new source state on the canvas, then Update properties."; }
    private void ReconnectTo(object sender, RoutedEventArgs e) { canvasPick = "to"; Status.Text = "Choose the new destination state on the canvas, then Update properties."; }
    private async void Update(object sender, RoutedEventArgs e) { if (UpdateOperation() is { } operation) await Execute(operation); }
    private async Task Execute(StateEditOperation operation)
    {
        if (!CanEdit) return; CancelPointer(); SavePositions(); busy = true; UpdateActions();
        try
        {
            var result = await draft.ExecuteAsync(operation, lifetime.Token); if (closed) return;
            if (!result.Success) { Status.Text = result.Error; return; }
            if (operation is AddStateNode add) draft.Select("state:" + add.Id);
            else if (operation is UpdateStateNode rename) { draft.Select("state:" + rename.NewId); if (sessionPositions.Remove("state:" + rename.Id, out var point)) sessionPositions["state:" + rename.NewId] = point; }
            else if (operation is DuplicateStateNode duplicate) draft.Select("state:" + duplicate.NewId);
            else if (operation is AddStateTransition) draft.Select(draft.Model.Transitions[^1].Identity);
            SavePositions(); await RefreshAsync();
        }
        finally { busy = false; if (!closed) UpdateActions(); }
    }
    private async void Delete(object sender, RoutedEventArgs e) { if (!PropertiesPending && DeleteOperation() is { } operation) await Execute(operation); }
    private async void Duplicate(object sender, RoutedEventArgs e) { if (!PropertiesPending && draft.Model.Nodes.FirstOrDefault(item => item.Identity == draft.Selection) is { } node) await Execute(new DuplicateStateNode(node.Id, NextId("Copy"))); }
    private async void Reverse(object sender, RoutedEventArgs e) { if (!PropertiesPending && ReverseOperation() is { } operation) await Execute(operation); }
    private void ResetProperties(object sender, RoutedEventArgs e) { if (busy) return; updating = true; try { ShowProperties(); } finally { updating = false; } UpdateActions(); Status.Text = "Pending properties discarded; the validated source is unchanged."; }
    private async void Undo(object sender, RoutedEventArgs e) { if (busy || PropertiesPending || SourcePending) return; CancelPointer(); SavePositions(); draft.Undo(); RestorePositions(); await RefreshAsync(); }
    private async void Redo(object sender, RoutedEventArgs e) { if (busy || PropertiesPending || SourcePending) return; CancelPointer(); SavePositions(); draft.Redo(); RestorePositions(); await RefreshAsync(); }
    private void SourceChanged(object? sender, EventArgs e) { if (draft is not null && !updating && SourceEditor is not null) { UpdateActions(); Status.Text = "Validate source changes before visual editing or Apply."; } }
    private async void ValidateSource(object sender, RoutedEventArgs e)
    {
        if (busy || PropertiesPending) return; SavePositions(); var sourceChanged = SourcePending; busy = true; UpdateActions();
        try { var result = await draft.ValidateSourceAsync(SourceEditor.Text, lifetime.Token); if (closed) return; if (result.Success) { if (sourceChanged) { sessionPositions.Clear(); SavePositions(); } await RefreshAsync(); Status.Text = draft.Model.HasUnknownDependencies ? "Canonical rendering validated. Unrecognised statements remain protected; inspect the retained source." : "Complete source semantics and canonical rendering validated."; } else Status.Text = result.Error; }
        finally { busy = false; if (!closed) UpdateActions(); }
    }
    private async void Apply(object sender, RoutedEventArgs e)
    {
        if (!CanEdit || PropertiesPending) return; CancelPointer(); busy = true; UpdateActions();
        try { var result = await draft.ValidateAsync(lifetime.Token); if (!result.Success) { Status.Text = result.Error; return; } var error = await applyToDocument(draft.Source, lifetime.Token); if (closed) return; if (error is not null) { Status.Text = error; return; } discard = true; Close(); }
        catch (OperationCanceledException) { }
        finally { busy = false; if (!closed) UpdateActions(); }
    }
    private void EditSource(object sender, RoutedEventArgs e) => editSource?.Invoke();
    private void Cancel(object sender, RoutedEventArgs e) => Close();
    public bool CloseForOwner() { Close(); return closed; }
    private void ConfirmClose(object? sender, CancelEventArgs e)
    {
        if (discard || draft is null) return;
        if (busy) { e.Cancel = true; Status.Text = "Wait for validation or Apply to finish before closing."; return; }
        if ((draft.Source != draft.OriginalSource || SourcePending || PropertiesPending) && MessageBox.Show(this, "Discard the private State draft and pending properties? The owning document has not changed.", "Discard State draft", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) e.Cancel = true;
    }
    private void LocateSource(object sender, RoutedEventArgs e)
    {
        var statement = draft.Model.Nodes.FirstOrDefault(item => item.Identity == draft.Selection)?.First ?? draft.Model.Groups.FirstOrDefault(item => item.Identity == draft.Selection)?.Opening ?? draft.Model.Transitions.FirstOrDefault(item => item.Identity == draft.Selection)?.Statement ?? draft.Model.Statements.FirstOrDefault(item => item.Identity == draft.Selection);
        if (statement is null || SourcePending) return; SourcePanel.IsExpanded = true; SourceEditor.Select(statement.Span.Start, statement.Span.Length); SourceEditor.ScrollToLine(SourceEditor.Document.GetLineByOffset(statement.Span.Start).LineNumber); SourceEditor.Focus();
    }
    private void ShowOriginal(object sender, RoutedEventArgs e)
    { new Window { Owner = this, Title = "Original portable Mermaid", Width = 700, Height = 450, Content = new ICSharpCode.AvalonEdit.TextEditor { Text = draft.OriginalSource, IsReadOnly = true, ShowLineNumbers = true, FontFamily = SourceEditor.FontFamily, FontSize = SourceEditor.FontSize, Background = (Brush)FindResource("WindowBrush"), Foreground = (Brush)FindResource("TextBrush") }, WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show(); }
    private void Preview(object sender, RoutedEventArgs e)
    { if (presentation is { } view) new Window { Owner = this, Title = "Rendered State preview", Width = 850, Height = 600, Content = new ScrollViewer { Content = new Image { Source = view.Canonical.Drawing, Stretch = Stretch.None }, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto }, Background = (Brush)FindResource("WindowBrush") }.Show(); }
    private void ZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (Scale is not null) Scale.ScaleX = Scale.ScaleY = Zoom.Value / 100; }
    private void ZoomIn(object sender, RoutedEventArgs e) => Zoom.Value *= 1.2;
    private void ZoomOut(object sender, RoutedEventArgs e) => Zoom.Value /= 1.2;
    private void ActualSize(object sender, RoutedEventArgs e) => Zoom.Value = 100;
    private void Fit(object sender, RoutedEventArgs e) { if (Surface.Width > 0 && Surface.Height > 0) Zoom.Value = Math.Clamp(Math.Min((Viewport.ViewportWidth - 20) / Surface.Width, (Viewport.ViewportHeight - 20) / Surface.Height) * 100, .1, 100); }
    private void AutoLayout(object sender, RoutedEventArgs e) { CancelPointer(); sessionPositions.Clear(); SavePositions(); if (presentation is not null) BuildCanvas(); Fit(sender, e); Status.Text = "Canonical layout restored. Saved Mermaid source is unchanged."; }
    private void Wheel(object sender, MouseWheelEventArgs e) { if (Keyboard.Modifiers != ModifierKeys.Control) return; Zoom.Value *= e.Delta > 0 ? 1.1 : 1 / 1.1; e.Handled = true; }
    private void PanStart(object sender, MouseButtonEventArgs e) { if (e.ChangedButton != MouseButton.Middle) return; CancelPointer(); pan = e.GetPosition(Viewport); panHorizontal = Viewport.HorizontalOffset; panVertical = Viewport.VerticalOffset; Viewport.CaptureMouse(); Viewport.Cursor = Cursors.Hand; e.Handled = true; }
    private void PanMove(object sender, MouseEventArgs e) { if (pan is not { } start) return; var point = e.GetPosition(Viewport); Viewport.ScrollToHorizontalOffset(panHorizontal + start.X - point.X); Viewport.ScrollToVerticalOffset(panVertical + start.Y - point.Y); }
    private void PanEnd(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Middle) EndPan(); }
    private void EndPan() { pan = null; Viewport.ReleaseMouseCapture(); Viewport.Cursor = Cursors.Arrow; }
    private void Shortcut(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { if (dragOwner is not null) CancelPointer(); else if (canvasPick is not null) { canvasPick = null; Status.Text = "Canvas picking cancelled."; } else if (pan is not null) EndPan(); else Close(); e.Handled = true; return; }
        var editing = e.OriginalSource is TextBoxBase || SourceEditor.IsKeyboardFocusWithin || LabelText.IsKeyboardFocusWithin || NodeId.IsKeyboardFocusWithin;
        var modifiers = Keyboard.Modifiers;
        if (e.Key == Key.Enter && modifiers == ModifierKeys.Control) { Apply(sender, new()); e.Handled = true; return; }
        if (editing) return;
        if (modifiers == ModifierKeys.Control && e.Key == Key.Z) Undo(sender, new());
        else if (modifiers == ModifierKeys.Control && e.Key == Key.Y) Redo(sender, new());
        else if (modifiers == ModifierKeys.Control && e.Key == Key.D) Duplicate(sender, new());
        else if (modifiers == ModifierKeys.Control && e.Key == Key.D0) ActualSize(sender, new());
        else if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.S) AddState(sender, new());
        else if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.T) AddTransition(sender, new());
        else if (modifiers == ModifierKeys.None && e.Key == Key.Delete) Delete(sender, new());
        else if (modifiers == ModifierKeys.None && e.Key == Key.F) Fit(sender, new());
        else if (modifiers == ModifierKeys.None && e.Key == Key.F2) { LabelText.Focus(); LabelText.SelectAll(); }
        else return; e.Handled = true;
    }
}
