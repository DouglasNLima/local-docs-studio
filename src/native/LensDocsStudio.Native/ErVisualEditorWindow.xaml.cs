using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using LensDocsStudio.Core.ErDiagrams;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;
using Mermaider.Models;
using Point = System.Windows.Point;

namespace LensDocsStudio.Native;

public partial class ErVisualEditorWindow : Window
{
    private ErVisualDraft draft = null!;
    private readonly DiagramService diagrams;
    private readonly NativeSvgService svg = new();
    private readonly Func<string, CancellationToken, Task<string?>> applyToDocument;
    private readonly Action? editSource;
    private readonly string diagramTheme;
    private readonly CancellationTokenSource lifetime = new();
    private readonly TaskCompletionSource closedCleanup = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenSource? pendingRender;
    private readonly SemaphoreSlim renderGate = new(1, 1);
    private readonly MermaidSourceHighlighting highlighting;
    private bool dark, updating, busy = true, closed, discard, mapped;
    private int renderGeneration;
    private string mode = "inspect", baseline = "";
    private string? attributeOwner, canvasPick;
    private Point? pan;
    private double panHorizontal, panVertical;
    private NativeErPresentation? presentation;
    private (ErDiagramModel Model, bool Dark, NativeErPresentation View)? preparedPresentation;
    private record Choice(string Identity, string Description, string Section = "Entities and attributes") { public override string ToString() => Description; }
    private record CardinalityChoice(ErCardinality Value, string Description);
    private record TypeChoice(bool Value, string Description);
    private record AttributeRow(string Identity, string Type, string Name, string Keys, string? Comment) { public override string ToString() => Type + " " + Name + " " + Keys + " " + Comment; }
    private bool SourcePending => draft is not null && SourceEditor.Text != draft.Source;
    private string Values => string.Join('\u001f', NodeId.Text, LabelText.Text, From.SelectedValue, To.SelectedValue, FirstCardinality.SelectedValue, SecondCardinality.SelectedValue, RelationshipType.SelectedValue, AttributeType.Text, AttributeName.Text, AttributeComment.Text, PrimaryKey.IsChecked, ForeignKey.IsChecked, UniqueKey.IsChecked);
    private bool PropertiesPending => draft is not null && (mode != "inspect" || baseline != Values);
    private bool CanEdit => draft is not null && !busy && !SourcePending && mapped && draft.Model.IsValid;
    public ErVisualEditorWindow(string source, DiagramService diagrams, Func<string, CancellationToken, Task<string?>> applyToDocument,
        bool dark = false, string diagramTheme = "Auto", Action? editSource = null, FontFamily? sourceFont = null, double sourceFontSize = 14)
    {
        this.diagrams = diagrams; this.applyToDocument = applyToDocument; this.dark = dark; this.diagramTheme = diagramTheme; this.editSource = editSource; highlighting = new(dark);
        InitializeComponent(); WindowStateService.FitInitialWindow(this);
        SourceEditor.FontFamily = sourceFont ?? new FontFamily("Cascadia Code, Consolas"); SourceEditor.FontSize = sourceFontSize;
        SourceEditor.Options.EnableHyperlinks = false; SourceEditor.Options.EnableEmailHyperlinks = false; SourceEditor.TextArea.TextView.LineTransformers.Add(highlighting);
        foreach (var field in new[] { NodeId, LabelText, AttributeType, AttributeName, AttributeComment }) field.TextChanged += (_, _) => { if (!updating) UpdateActions(); };
        foreach (var field in new[] { From, To, FirstCardinality, SecondCardinality, RelationshipType }) field.SelectionChanged += (_, _) => { if (!updating) UpdateActions(); };
        foreach (var field in new[] { PrimaryKey, ForeignKey, UniqueKey }) { field.Checked += (_, _) => { if (!updating) UpdateActions(); }; field.Unchecked += (_, _) => { if (!updating) UpdateActions(); }; }
        FirstCardinality.ItemsSource = Enum.GetValues<ErCardinality>().Select(value => new CardinalityChoice(value, CardinalityName(value) + "   " + ErDiagramSourceParser.Notation(value, true))).ToArray();
        SecondCardinality.ItemsSource = Enum.GetValues<ErCardinality>().Select(value => new CardinalityChoice(value, CardinalityName(value) + "   " + ErDiagramSourceParser.Notation(value, false))).ToArray();
        RelationshipType.ItemsSource = new[] { new TypeChoice(true, "Identifying   --"), new TypeChoice(false, "Non-identifying   ..") };
        EditSourceButton.IsEnabled = editSource is not null; IsEnabled = false;
        Loaded += async (_, _) => { try { draft = await Task.Run(() => new ErVisualDraft(source, diagrams, ValidateNativeCandidateAsync), lifetime.Token); if (closed) return; busy = false; IsEnabled = true; await RefreshAsync(true); } catch (OperationCanceledException) { } };
        Closing += ConfirmClose; Closed += async (_, _) =>
        {
            using var cleanupPhase = LensDocsStudio.Core.Rendering.MermaidDiagnostics.Measure("editor.cleanup.Er");
            closed = true; renderGeneration++; CancelPointer(); pendingRender?.Cancel(); lifetime.Cancel();
            await renderGate.WaitAsync(); renderGate.Release(); renderGate.Dispose(); lifetime.Dispose(); closedCleanup.SetResult();
        };
        Deactivated += (_, _) => { EndPan(); CancelPointer(); };
    }
    private static string CardinalityName(ErCardinality value) => value switch { ErCardinality.One => "Exactly one", ErCardinality.ZeroOne => "Zero or one", ErCardinality.Many => "One or more", _ => "Zero or more" };
    public async void ApplyTheme(string theme) { dark = ThemeService.IsDark(theme); highlighting.Dark = dark; SourceEditor.TextArea.TextView.Redraw(); if (!closed && draft is not null) await RenderAsync(false); }
    private IEnumerable<Choice> Choices()
    {
        foreach (var entity in draft.Model.Entities)
        {
            yield return new(entity.Identity, entity.Id + " · " + entity.Label + (entity.Implicit ? " (implicit)" : ""));
            foreach (var (attribute, index) in entity.Attributes.Select((item, index) => (item, index))) yield return new(attribute.Identity, "  " + entity.Id + " · attribute " + (index + 1) + " · " + attribute.Type + " " + attribute.Name);
        }
        foreach (var (relationship, index) in draft.Model.Relationships.Select((item, index) => (item, index))) yield return new(relationship.Identity, "Relationship " + (index + 1) + " · " + relationship.Entity1 + " → " + relationship.Entity2 + " · " + relationship.Label, "Relationships");
        foreach (var statement in draft.Model.Statements.Where(item => item.Protected)) yield return new(statement.Identity, "Protected · " + statement.Span.Read(draft.Source), "Protected source");
    }
    private async Task RefreshAsync(bool fit = false)
    {
        updating = true; try { if (SourceEditor.Text != draft.Source) SourceEditor.Text = draft.Source; Elements.ItemsSource = Controls.MermaidWorkspace.Navigation(Choices().ToArray()); RestoreSelection(); ShowProperties(); } finally { updating = false; }
        await RenderAsync(fit); if (!closed && !fit) await Dispatcher.InvokeAsync(RevealSelection, System.Windows.Threading.DispatcherPriority.Loaded); UpdateActions();
    }
    private async Task ValidateNativeCandidateAsync(ErDiagramModel candidate, CancellationToken cancellationToken)
    {
        var version = renderGeneration; var themeDark = dark; await renderGate.WaitAsync(cancellationToken);
        try
        {
            var render = await Task.Run(() => diagrams.Render(candidate.Source, themeDark, diagramTheme, cancellationToken), cancellationToken);
            var view = await StaWorker.RunAsync(() => NativeErPresentation.Create(candidate, render, svg, cancellationToken), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested(); if (closed || version != renderGeneration) throw new ArgumentException("The ER presentation changed during validation. Retry against the current presentation; the draft is unchanged.");
            preparedPresentation = (candidate, themeDark, view);
        }
        finally { renderGate.Release(); }
    }
    private async Task RenderAsync(bool fit)
    {
        pendingRender?.Cancel(); var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); pendingRender = cancellation;
        CancelPointer(); canvasPick = null; var version = ++renderGeneration; mapped = false; UpdateActions(); var current = draft.Model; var themeDark = dark;
        Status.Text = "Rendering canonical ER diagram…"; var leased = false;
        try
        {
            await renderGate.WaitAsync(cancellation.Token); leased = true;
            var render = await Task.Run(() => diagrams.Render(current.Source, themeDark, diagramTheme, cancellation.Token), cancellation.Token);
            var ready = preparedPresentation is { } prepared && ReferenceEquals(prepared.Model, current) && prepared.Dark == themeDark ? prepared.View : null; preparedPresentation = null;
            var converted = await StaWorker.RunAsync(() =>
            {
                NativeErPresentation? view = ready; string? diagnostic = null;
                try { view ??= NativeErPresentation.Create(current, render, svg, cancellation.Token); } catch (Exception error) when (error is ArgumentException or InvalidDataException or FormatException) { diagnostic = error.Message; }
                return (view, Drawing: view?.Drawing ?? (render.Success ? svg.RenderCanonical(render, svg.Prepare(render.Svg!), cancellationToken: cancellation.Token) : null), diagnostic);
            }, cancellation.Token);
            if (closed || version != renderGeneration) return; presentation = converted.view;
            if (presentation is not null) { mapped = true; BuildCanvas(); }
            else { Surface.Children.Clear(); buttons.Clear(); Surface.Children.Add(new Image { Source = converted.Drawing, Stretch = Stretch.None, IsHitTestVisible = false }); Surface.Width = converted.Drawing?.Width ?? 420; Surface.Height = converted.Drawing?.Height ?? 260; }
            Status.Text = converted.diagnostic ?? (current.HasUnknownDependencies ? "Unknown source retained exactly. Inspect individual operation permissions." : "Ready. Select an entity, attribute row or relationship. Canonical layout determines positions.");
            AutomationProperties.SetItemStatus(this, SystemParameters.HighContrast ? "High Contrast" : themeDark ? "Dark" : "Light");
            if (fit) { Surface.UpdateLayout(); Fit(this, new()); if (Zoom.Value < 40) { Zoom.Value = 100; Viewport.ScrollToHorizontalOffset(0); Viewport.ScrollToVerticalOffset(0); } }
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is not OutOfMemoryException) { if (!closed && version == renderGeneration) { mapped = false; Status.Text = error.Message; } }
        finally { if (leased) renderGate.Release(); if (ReferenceEquals(pendingRender, cancellation)) pendingRender = null; cancellation.Dispose(); if (!closed && version == renderGeneration) UpdateActions(); }
    }
    private void RevealSelection()
    { if (draft.Selection is { } identity && buttons.TryGetValue(identity, out var target)) Controls.MermaidWorkspace.Reveal(target, Viewport); }
    private void RestoreSelection()
    {
        var selected = Elements.Items.OfType<Choice>().FirstOrDefault(item => item.Identity == draft.Selection);
        var changed = !Equals(Elements.SelectedItem, selected); Elements.SelectedItem = selected;
        if (changed && selected is not null) Controls.MermaidWorkspace.RevealNavigation(Elements, selected);
    }
    private void SelectionChanged(object sender, SelectionChangedEventArgs e) { if (!updating && Elements.SelectedItem is Choice item) { Select(item.Identity); if (draft.Selection == item.Identity && !PropertiesPending) RevealSelection(); } }
    private void AttributeSelected(object sender, SelectionChangedEventArgs e) { if (!updating && AttributeRows.SelectedItem is AttributeRow item) Select(item.Identity); }
    private void Select(string? identity)
    {
        using var selectionPhase = LensDocsStudio.Core.Rendering.MermaidDiagnostics.Measure("editor.selection.Er");
        if (busy || SourcePending || PropertiesPending) { Status.Text = "Update or discard pending properties and validate source before changing selection."; Controls.MermaidWorkspace.DeferNavigationRollback(Elements, () => { updating = true; try { RestoreSelection(); UpdateSelection(); } finally { updating = false; } }); return; }
        draft.Select(identity); updating = true; try { RestoreSelection(); ShowProperties(); UpdateSelection(); } finally { updating = false; } UpdateActions();
    }
    private void ShowProperties()
    {
        Controls.MermaidWorkspace.Inspect(InspectorViewport, draft.Selection);
        canvasPick = null; mode = "inspect"; NodeFields.Visibility = TransitionFields.Visibility = LabelFields.Visibility = AttributesPanel.Visibility = AttributeFields.Visibility = Visibility.Collapsed;
        Summary.Text = "Select an entity, attribute or relationship."; Dependencies.Text = ""; attributeOwner = null;
        var endpoints = draft.Model.Entities.Select(item => new Choice(item.Id, item.Id + " · " + item.Label)).ToArray(); From.ItemsSource = endpoints; To.ItemsSource = endpoints;
        var attribute = draft.Model.Entities.SelectMany(item => item.Attributes).FirstOrDefault(item => item.Identity == draft.Selection);
        var entity = draft.Model.Entities.FirstOrDefault(item => item.Identity == draft.Selection) ?? (attribute is null ? null : draft.Model.Entity(attribute.EntityId));
        if (entity is not null)
        {
            attributeOwner = entity.Id; AttributesPanel.Visibility = Visibility.Visible;
            AttributeRows.ItemsSource = entity.Attributes.Select(item => new AttributeRow(item.Identity, item.Type, item.Name, string.Join(' ', item.Keys), item.Comment)).ToArray();
            AttributeRows.SelectedItem = AttributeRows.Items.OfType<AttributeRow>().FirstOrDefault(item => item.Identity == attribute?.Identity);
            Summary.Text = "Entity " + entity.Id; Dependencies.Text = (entity.Implicit ? "Implicit entity. Adding an attribute or updating its label creates an explicit block. " : "") + "Incident relationships: " + string.Join(", ", draft.Model.Relationships.Where(item => item.Entity1 == entity.Id || item.Entity2 == entity.Id).Select(item => item.Entity1 + " → " + item.Entity2 + " (" + item.Label + ")"));
            if (attribute is null) { NodeFields.Visibility = LabelFields.Visibility = Visibility.Visible; NodeId.Text = entity.Id; LabelText.Text = entity.Label; }
            else { AttributeFields.Visibility = Visibility.Visible; Summary.Text = entity.Id + " · attribute " + (entity.Attributes.ToList().IndexOf(attribute) + 1); AttributeType.Text = attribute.Type; AttributeName.Text = attribute.Name; AttributeComment.Text = attribute.Comment ?? ""; PrimaryKey.IsChecked = attribute.Keys.Contains(ErKeyType.PK); ForeignKey.IsChecked = attribute.Keys.Contains(ErKeyType.FK); UniqueKey.IsChecked = attribute.Keys.Contains(ErKeyType.UK); }
        }
        else if (draft.Model.Relationships.FirstOrDefault(item => item.Identity == draft.Selection) is { } relationship)
        {
            TransitionFields.Visibility = LabelFields.Visibility = Visibility.Visible; From.SelectedValue = relationship.Entity1; To.SelectedValue = relationship.Entity2; LabelText.Text = relationship.Label;
            FirstCardinality.SelectedValue = relationship.Cardinality1; SecondCardinality.SelectedValue = relationship.Cardinality2; RelationshipType.SelectedValue = relationship.Identifying;
            Summary.Text = "Relationship " + (draft.Model.Relationships.ToList().IndexOf(relationship) + 1); Dependencies.Text = relationship.Entity1 + " (" + CardinalityName(relationship.Cardinality1) + ") → " + relationship.Entity2 + " (" + CardinalityName(relationship.Cardinality2) + "). " + (relationship.Identifying ? "Identifying." : "Non-identifying.");
        }
        else if (draft.Model.Statements.FirstOrDefault(item => item.Identity == draft.Selection) is { } statement) { Summary.Text = "Protected source"; Dependencies.Text = statement.Span.Read(draft.Source); }
        ContextDescription.Text = Choices().FirstOrDefault(item => item.Identity == draft.Selection)?.Description ?? "";
        baseline = Values;
    }
    private IReadOnlyList<ErKeyType> Keys()
    {
        var selected = new[] { (ErKeyType.PK, PrimaryKey.IsChecked), (ErKeyType.FK, ForeignKey.IsChecked), (ErKeyType.UK, UniqueKey.IsChecked) }.Where(item => item.Item2 == true).Select(item => item.Item1).ToArray();
        var original = draft.Model.Entities.SelectMany(item => item.Attributes).FirstOrDefault(item => item.Identity == draft.Selection)?.Keys ?? [];
        return original.Where(selected.Contains).Concat(selected.Where(item => !original.Contains(item))).ToArray();
    }
    private string? CommentValue() => draft.Model.Entities.SelectMany(item => item.Attributes).FirstOrDefault(item => item.Identity == draft.Selection) is { Comment: "" } && AttributeComment.Text.Length == 0 ? "" : AttributeComment.Text.Length == 0 ? null : AttributeComment.Text;
    private ErEditOperation? UpdateOperation() => mode == "entity" ? new AddErEntity(NodeId.Text, LabelText.Text)
        : mode == "attribute" ? new AddErAttribute(attributeOwner ?? "", AttributeType.Text, AttributeName.Text, Keys(), CommentValue())
        : mode == "relationship" ? NewRelationship()
        : draft.Model.Entities.FirstOrDefault(item => item.Identity == draft.Selection) is { } entity ? new UpdateErEntity(entity.Id, NodeId.Text, LabelText.Text)
        : draft.Model.Entities.SelectMany(item => item.Attributes).Any(item => item.Identity == draft.Selection) ? new UpdateErAttribute(draft.Selection!, AttributeType.Text, AttributeName.Text, Keys(), CommentValue())
        : draft.Model.Relationships.FirstOrDefault(item => item.Identity == draft.Selection) is { } relationship ? new UpdateErRelationship(relationship.Identity, From.SelectedValue as string ?? "", To.SelectedValue as string ?? "", FirstCardinality.SelectedValue is ErCardinality first ? first : ErCardinality.One, SecondCardinality.SelectedValue is ErCardinality second ? second : ErCardinality.ZeroMany, RelationshipType.SelectedValue is true, LabelText.Text) : null;
    private AddErRelationship NewRelationship() => new(From.SelectedValue as string ?? "", To.SelectedValue as string ?? "", FirstCardinality.SelectedValue is ErCardinality first ? first : ErCardinality.One, SecondCardinality.SelectedValue is ErCardinality second ? second : ErCardinality.ZeroMany, RelationshipType.SelectedValue is true, LabelText.Text);
    private ErEditOperation? DeleteOperation() => draft.Model.Entities.FirstOrDefault(item => item.Identity == draft.Selection) is { } entity ? new DeleteErEntity(entity.Id) : draft.Model.Relationships.Any(item => item.Identity == draft.Selection) ? new DeleteErRelationship(draft.Selection!) : draft.Model.Entities.SelectMany(item => item.Attributes).Any(item => item.Identity == draft.Selection) ? new DeleteErAttribute(draft.Selection!) : null;
    private ErEditOperation? DuplicateOperation() => draft.Model.Entities.FirstOrDefault(item => item.Identity == draft.Selection) is { } entity ? new DuplicateErEntity(entity.Id, NextId("Copy")) : draft.Model.Relationships.Any(item => item.Identity == draft.Selection) ? new DuplicateErRelationship(draft.Selection!) : draft.Model.Entities.SelectMany(item => item.Attributes).Any(item => item.Identity == draft.Selection) ? new DuplicateErAttribute(draft.Selection!) : null;
    private string NextId(string prefix = "Entity") { var index = 1; while (draft.Model.Entities.Any(item => item.Id == prefix + index)) index++; return prefix + index; }
    private void UpdateActions()
    {
        if (draft is null || UpdateButton is null) return; var can = CanEdit; var clean = !PropertiesPending;
        void Action(Button button, ErEditOperation? operation, bool requireClean = true)
        {
            var capability = operation is null ? new ErOperationCapability(false, "Select an applicable element.") : ErCapabilityAnalysis.For(draft.Model, operation);
            button.IsEnabled = can && (!requireClean || clean) && capability.Allowed; button.ToolTip = capability.Reason; ToolTipService.SetShowOnDisabled(button, true); AutomationProperties.SetHelpText(button, capability.Reason);
        }
        Action(UpdateButton, UpdateOperation(), false); Action(DeleteButton, DeleteOperation()); Action(DuplicateButton, DuplicateOperation());
        Action(ReverseButton, draft.Model.Relationships.Any(item => item.Identity == draft.Selection) ? new ReverseErRelationship(draft.Selection!) : null);
        Action(AddEntityButton, new AddErEntity(NextId(), "New entity"));
        var ids = draft.Model.Entities.Select(item => item.Id).ToArray(); Action(AddRelationshipButton, ids.Length > 0 ? new AddErRelationship(ids[0], ids[^1]) : null);
        Action(AddAttributeButton, attributeOwner is null ? null : new AddErAttribute(attributeOwner, "string", "new_attribute", []));
        var attribute = draft.Model.Entities.SelectMany(item => item.Attributes).FirstOrDefault(item => item.Identity == draft.Selection);
        Action(MoveUpButton, attribute is null ? null : new MoveErAttribute(attribute.Identity, -1)); Action(MoveDownButton, attribute is null ? null : new MoveErAttribute(attribute.Identity, 1));
        UndoButton.IsEnabled = !busy && clean && !SourcePending && draft.CanUndo; RedoButton.IsEnabled = !busy && clean && !SourcePending && draft.CanRedo;
        ApplyButton.IsEnabled = can && clean; ValidateButton.IsEnabled = !busy && clean; ResetButton.IsEnabled = !busy && !clean;
        var entity = draft.Model.Entities.FirstOrDefault(item => item.Identity == draft.Selection);
        var relationship = draft.Model.Relationships.FirstOrDefault(item => item.Identity == draft.Selection);
        ErEditOperation? inspectionUpdate = entity is not null ? new UpdateErEntity(entity.Id, entity.Id, entity.Label)
            : attribute is not null ? new UpdateErAttribute(attribute.Identity, attribute.Type, attribute.Name, attribute.Keys, attribute.Comment)
            : relationship is not null ? new UpdateErRelationship(relationship.Identity, relationship.Entity1, relationship.Entity2, relationship.Cardinality1, relationship.Cardinality2, relationship.Identifying, relationship.Label) : null;
        // Invalid pending input must remain correctable. Field permission comes from the selected source occurrence.
        var authorised = can && inspectionUpdate is not null && ErCapabilityAnalysis.For(draft.Model, inspectionUpdate).Allowed;
        NodeFields.IsEnabled = can && (mode == "entity" || entity is not null && ErCapabilityAnalysis.For(draft.Model, new UpdateErEntity(entity.Id, NextId("Renamed"), entity.Label)).Allowed);
        LabelFields.IsEnabled = AttributeFields.IsEnabled = authorised || can && mode != "inspect";
        TransitionFields.IsEnabled = authorised || can && mode == "relationship";
        var reconnect = can && !draft.Model.Statements.Any(item => item.Protected && item.Owner is null);
        From.IsEnabled = To.IsEnabled = ReconnectFromButton.IsEnabled = ReconnectToButton.IsEnabled = reconnect;
        foreach (var handle in endpointHandles.OfType<Button>())
        {
            handle.IsEnabled = reconnect && clean && relationship?.Statement.Protected == false;
            var explanation = !clean ? "Update or discard pending properties before dragging an endpoint." : !reconnect ? "Validate the current source; unknown dependencies require Edit source for reconnection." : "Drag this endpoint to another entity, or use the keyboard-accessible inspector.";
            handle.ToolTip = explanation; ToolTipService.SetShowOnDisabled(handle, true); AutomationProperties.SetHelpText(handle, explanation);
        }
        foreach (var panel in new[] { NodeFields, LabelFields, AttributeFields, TransitionFields }) { panel.ToolTip = UpdateButton.ToolTip; ToolTipService.SetShowOnDisabled(panel, true); }
        var sourceState = draft.Model.Compatibility == ErCompatibility.PartiallyEditable ? "Partially editable" : draft.Model.Compatibility.ToString();
        Capabilities.Text = "Source: " + sourceState + ".\nUpdate: " + (UpdateButton.IsEnabled ? "Available." : UpdateButton.ToolTip) + "\nDelete: " + DeleteButton.ToolTip + "\nDuplicate: " + DuplicateButton.ToolTip;
        InspectorState.Text = PropertiesPending ? "Pending properties" : SourcePending ? "Source validation required" : !mapped ? "Apply unavailable" : draft.Selection is null ? "Ready" : UpdateButton.IsEnabled ? "Editable" : "Protected source";
    }
    private void BeginCreation(string kind)
    {
        if (!CanEdit || PropertiesPending) return; updating = true;
        try
        {
            ShowProperties(); mode = kind; Summary.Text = "New " + kind; ContextDescription.Text = ""; NodeFields.Visibility = kind == "entity" ? Visibility.Visible : Visibility.Collapsed; TransitionFields.Visibility = kind == "relationship" ? Visibility.Visible : Visibility.Collapsed;
            LabelFields.Visibility = kind == "attribute" ? Visibility.Collapsed : Visibility.Visible; AttributeFields.Visibility = kind == "attribute" ? Visibility.Visible : Visibility.Collapsed; AttributesPanel.Visibility = Visibility.Collapsed;
            NodeId.Text = NextId(); LabelText.Text = kind == "entity" ? "New entity" : ""; From.SelectedIndex = 0; To.SelectedIndex = Math.Min(1, To.Items.Count - 1); FirstCardinality.SelectedValue = ErCardinality.One; SecondCardinality.SelectedValue = ErCardinality.ZeroMany; RelationshipType.SelectedValue = true;
            AttributeType.Text = "string"; AttributeName.Text = "new_attribute"; AttributeComment.Text = ""; PrimaryKey.IsChecked = ForeignKey.IsChecked = UniqueKey.IsChecked = false; canvasPick = kind == "relationship" ? "from" : null;
        }
        finally { updating = false; } UpdateActions();
    }
    private void AddEntity(object sender, RoutedEventArgs e) => BeginCreation("entity");
    private void AddRelationship(object sender, RoutedEventArgs e) => BeginCreation("relationship");
    private void AddAttribute(object sender, RoutedEventArgs e) => BeginCreation("attribute");
    private void ReconnectFrom(object sender, RoutedEventArgs e) { canvasPick = "from-reconnect"; Status.Text = "Choose the new first entity, then Update properties."; }
    private void ReconnectTo(object sender, RoutedEventArgs e) { canvasPick = "to"; Status.Text = "Choose the new second entity, then Update properties."; }
    private async void Update(object sender, RoutedEventArgs e) { if (UpdateOperation() is { } operation) await Execute(operation); }
    private async Task Execute(ErEditOperation operation)
    {
        if (!CanEdit) return; CancelPointer(); busy = true; UpdateActions();
        try
        {
            var result = await draft.ExecuteAsync(operation, lifetime.Token); if (closed) return; if (!result.Success) { Status.Text = result.Error; return; }
            if (operation is AddErEntity add) draft.Select("entity:" + add.Id); else if (operation is UpdateErEntity rename) draft.Select("entity:" + rename.NewId); else if (operation is DuplicateErEntity duplicate) draft.Select("entity:" + duplicate.NewId);
            else if (operation is AddErRelationship or DuplicateErRelationship) draft.Select(draft.Model.Relationships[^1].Identity);
            else if (operation is AddErAttribute attribute) draft.Select(draft.Model.Entity(attribute.EntityId).Attributes[^1].Identity);
            await RefreshAsync();
            if ((operation is AddErEntity or DuplicateErEntity or AddErAttribute) && draft.Selection is { } selection && buttons.TryGetValue(selection, out var added)) { Surface.UpdateLayout(); added.BringIntoView(); }
        }
        finally { busy = false; if (!closed) UpdateActions(); }
    }
    private async void Delete(object sender, RoutedEventArgs e) { if (!PropertiesPending && DeleteOperation() is { } operation) await Execute(operation); }
    private async void Duplicate(object sender, RoutedEventArgs e) { if (!PropertiesPending && DuplicateOperation() is { } operation) await Execute(operation); }
    private async void Reverse(object sender, RoutedEventArgs e) { if (!PropertiesPending && draft.Model.Relationships.Any(item => item.Identity == draft.Selection)) await Execute(new ReverseErRelationship(draft.Selection!)); }
    private async void MoveUp(object sender, RoutedEventArgs e) { if (!PropertiesPending && draft.Selection is { } identity) await Execute(new MoveErAttribute(identity, -1)); }
    private async void MoveDown(object sender, RoutedEventArgs e) { if (!PropertiesPending && draft.Selection is { } identity) await Execute(new MoveErAttribute(identity, 1)); }
    private void ResetProperties(object sender, RoutedEventArgs e) { if (busy) return; updating = true; try { ShowProperties(); } finally { updating = false; } UpdateActions(); Status.Text = "Pending properties discarded; validated source is unchanged."; }
    private async void Undo(object sender, RoutedEventArgs e) { if (busy || PropertiesPending || SourcePending) return; CancelPointer(); draft.Undo(); await RefreshAsync(); }
    private async void Redo(object sender, RoutedEventArgs e) { if (busy || PropertiesPending || SourcePending) return; CancelPointer(); draft.Redo(); await RefreshAsync(); }
    private void SourceChanged(object? sender, EventArgs e) { if (draft is not null && !updating) { UpdateActions(); Status.Text = "Validate source changes before visual editing or Apply."; } }
    private async void ValidateSource(object sender, RoutedEventArgs e)
    {
        if (busy || PropertiesPending) return; busy = true; UpdateActions();
        try { var result = await draft.ValidateSourceAsync(SourceEditor.Text, lifetime.Token); if (closed) return; if (result.Success) { await RefreshAsync(); Status.Text = "Complete ER semantics and canonical rendering validated. Unknown source remains protected."; } else Status.Text = result.Error; }
        finally { busy = false; if (!closed) UpdateActions(); }
    }
    private async void Apply(object sender, RoutedEventArgs e)
    {
        if (!CanEdit || PropertiesPending) return; CancelPointer(); busy = true; UpdateActions();
        try { var result = await draft.ValidateAsync(lifetime.Token); if (!result.Success) { Status.Text = result.Error; return; } var error = await applyToDocument(draft.Source, lifetime.Token); if (closed) return; if (error is not null) { Status.Text = error; return; } discard = true; Close(); }
        catch (OperationCanceledException) { } finally { busy = false; if (!closed) UpdateActions(); }
    }
    private void EditSource(object sender, RoutedEventArgs e) => editSource?.Invoke();
    private void Cancel(object sender, RoutedEventArgs e) => Close();
    public bool CloseForOwner() { Close(); return closed; }
    private void ConfirmClose(object? sender, CancelEventArgs e)
    {
        if (discard || draft is null) return; if (busy) { e.Cancel = true; Status.Text = "Wait for validation or Apply to finish before closing."; return; }
        if ((draft.Source != draft.OriginalSource || SourcePending || PropertiesPending) && MessageBox.Show(this, "Discard the private ER draft and pending properties? Current changes in the owning document will be kept.", "Discard ER draft", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) e.Cancel = true;
    }
    private void LocateSource(object sender, RoutedEventArgs e)
    {
        var statement = draft.Model.Entities.FirstOrDefault(item => item.Identity == draft.Selection)?.Declarations.FirstOrDefault() ?? draft.Model.Relationships.FirstOrDefault(item => item.Identity == draft.Selection)?.Statement ?? draft.Model.Entities.SelectMany(item => item.Attributes).FirstOrDefault(item => item.Identity == draft.Selection)?.Statement ?? draft.Model.Statements.FirstOrDefault(item => item.Identity == draft.Selection);
        if (statement is null || SourcePending) return; SourcePanel.IsExpanded = true; SourceEditor.Select(statement.Span.Start, statement.Span.Length); SourceEditor.ScrollToLine(SourceEditor.Document.GetLineByOffset(statement.Span.Start).LineNumber); SourceEditor.Focus();
    }
    private void ShowOriginal(object sender, RoutedEventArgs e) { new Window { Owner = this, Title = "Original portable Mermaid", Width = 700, Height = 450, Content = new ICSharpCode.AvalonEdit.TextEditor { Text = draft.OriginalSource, IsReadOnly = true, ShowLineNumbers = true, FontFamily = SourceEditor.FontFamily, FontSize = SourceEditor.FontSize, Background = (Brush)FindResource("WindowBrush"), Foreground = (Brush)FindResource("TextBrush") }, WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show(); }
    private void CopySource(object sender, RoutedEventArgs e)
    { try { Clipboard.SetText(SourceEditor.Text); Status.Text = "Mermaid draft source copied."; } catch (System.Runtime.InteropServices.ExternalException) { Status.Text = "The clipboard is unavailable. Select and copy the source directly."; } }
    private void ToggleSource(object sender, RoutedEventArgs e) => SourcePanel.IsExpanded = !SourcePanel.IsExpanded;
    private void Preview(object sender, RoutedEventArgs e)
    {
        if (presentation is not { } view) return;
        if (view.Entities.Count == 0) { Status.Text = "This ER draft has no entities to preview. Add an entity or undo the deletion."; return; }
        new Window { Owner = this, Title = "Rendered ER preview", Width = 850, Height = 600, Content = new ScrollViewer { Content = new Image { Source = view.Drawing, Stretch = Stretch.None }, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto }, Background = (Brush)FindResource("WindowBrush") }.Show();
    }
    private void ZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (Scale is not null) Scale.ScaleX = Scale.ScaleY = Zoom.Value / 100; }
    private void ZoomIn(object sender, RoutedEventArgs e) => Zoom.Value *= 1.15;
    private void ZoomOut(object sender, RoutedEventArgs e) => Zoom.Value /= 1.15;
    private void ActualSize(object sender, RoutedEventArgs e) => Zoom.Value = 100;
    private void Fit(object sender, RoutedEventArgs e) { if (Surface.Width > 0 && Surface.Height > 0) Zoom.Value = Math.Clamp(Math.Min((Viewport.ViewportWidth - 20) / Surface.Width, (Viewport.ViewportHeight - 20) / Surface.Height) * 100, .1, 100); }
    private void AutoLayout(object sender, RoutedEventArgs e) { CancelPointer(); Fit(sender, e); Viewport.ScrollToHorizontalOffset(0); Viewport.ScrollToVerticalOffset(0); Status.Text = "Canonical ER layout centred. Portable source is unchanged."; }
    private void Wheel(object sender, MouseWheelEventArgs e) => Controls.MermaidWorkspace.ZoomAtPointer(Viewport, Zoom, e);
    private void PanStart(object sender, MouseButtonEventArgs e) { if (e.ChangedButton != MouseButton.Middle) return; CancelPointer(); pan = e.GetPosition(Viewport); panHorizontal = Viewport.HorizontalOffset; panVertical = Viewport.VerticalOffset; Viewport.CaptureMouse(); Viewport.Cursor = Cursors.Hand; e.Handled = true; }
    private void PanMove(object sender, MouseEventArgs e) { if (pan is not { } start) return; var point = e.GetPosition(Viewport); Viewport.ScrollToHorizontalOffset(panHorizontal + start.X - point.X); Viewport.ScrollToVerticalOffset(panVertical + start.Y - point.Y); }
    private void PanEnd(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Middle) EndPan(); }
    private void EndPan() { pan = null; if (Viewport.IsMouseCaptured) Viewport.ReleaseMouseCapture(); Viewport.Cursor = Cursors.Arrow; }
    private void Shortcut(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { if (endpointDrag is not null) CancelPointer(); else if (canvasPick is not null) { canvasPick = null; Status.Text = "Canvas picking cancelled."; } else if (pan is not null) EndPan(); else Close(); e.Handled = true; return; }
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { Apply(sender, new()); e.Handled = true; return; }
        if (e.OriginalSource is TextBoxBase || SourceEditor.IsKeyboardFocusWithin) return;
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z) Undo(sender, new()); else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y) Redo(sender, new());
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.D) Duplicate(sender, new()); else if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Delete) Delete(sender, new());
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.D0) ActualSize(sender, new()); else if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.F) Fit(sender, new()); else return; e.Handled = true;
    }
}
