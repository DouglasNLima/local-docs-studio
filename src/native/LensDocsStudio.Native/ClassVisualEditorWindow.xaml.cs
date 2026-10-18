using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using LensDocsStudio.Core.ClassDiagrams;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;
using Mermaider.Models;
using Point = System.Windows.Point;

namespace LensDocsStudio.Native;

public partial class ClassVisualEditorWindow : Window
{
    private ClassDiagramVisualDraft draft = null!;
    private readonly DiagramService diagrams;
    private readonly NativeSvgService svg = new();
    private readonly Func<string, CancellationToken, Task<string?>> applyToDocument;
    private readonly Action? editSource;
    private readonly string diagramTheme;
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? pendingRender;
    private readonly SemaphoreSlim renderGate = new(1, 1);
    private readonly MermaidSourceHighlighting highlighting;
    private bool dark, updating, busy = true, closed, discard, mapped, memberMethod;
    private int renderGeneration;
    private string mode = "inspect", baseline = "";
    private string? attributeOwner, canvasPick;
    private Point? pan;
    private double panHorizontal, panVertical;
    private NativeClassPresentation? presentation;
    private (ClassDiagramModel Model, bool Dark, NativeClassPresentation View)? preparedPresentation;
    private record Choice(string Identity, string Description, string Section = "Classes and members") { public override string ToString() => Description; }
    private record TypeChoice(ClassRelationType Value, string Description);
    private bool SourcePending => draft is not null && SourceEditor.Text != draft.Source;
    private string Values => string.Join('\u001f', NodeId.Text, GenericText.Text, AnnotationText.Text, LabelText.Text, From.SelectedValue, To.SelectedValue, FirstCardinality.Text, SecondCardinality.Text, RelationshipType.SelectedValue, MarkerAt.SelectedItem, AttributeType.Text, AttributeName.Text, Parameters.Text, MemberVisibility.SelectedItem, StaticMember.IsChecked, AbstractMember.IsChecked);
    private bool PropertiesPending => draft is not null && (mode != "inspect" || baseline != Values);
    private bool CanEdit => draft is not null && !busy && !SourcePending && mapped && draft.Model.IsValid;
    public ClassVisualEditorWindow(string source, DiagramService diagrams, Func<string, CancellationToken, Task<string?>> applyToDocument,
        bool dark = false, string diagramTheme = "Auto", Action? editSource = null, FontFamily? sourceFont = null, double sourceFontSize = 14)
    {
        this.diagrams = diagrams; this.applyToDocument = applyToDocument; this.dark = dark; this.diagramTheme = diagramTheme; this.editSource = editSource; highlighting = new(dark);
        InitializeComponent(); WindowStateService.FitInitialWindow(this);
        SourceEditor.FontFamily = sourceFont ?? new FontFamily("Cascadia Code, Consolas"); SourceEditor.FontSize = sourceFontSize;
        SourceEditor.Options.EnableHyperlinks = false; SourceEditor.Options.EnableEmailHyperlinks = false; SourceEditor.TextArea.TextView.LineTransformers.Add(highlighting);
        foreach (var field in new[] { NodeId, GenericText, AnnotationText, LabelText, AttributeType, AttributeName, Parameters, FirstCardinality, SecondCardinality }) field.TextChanged += (_, _) => { if (!updating) UpdateActions(); };
        foreach (var field in new[] { From, To, RelationshipType, MarkerAt, MemberVisibility }) field.SelectionChanged += (_, _) => { if (!updating) UpdateActions(); };
        foreach (var field in new[] { StaticMember, AbstractMember }) { field.Checked += (_, _) => { if (!updating) UpdateActions(); }; field.Unchecked += (_, _) => { if (!updating) UpdateActions(); }; }
        RelationshipType.ItemsSource = Enum.GetValues<ClassRelationType>().Where(value => value != ClassRelationType.Lollipop).Select(value => new TypeChoice(value, value == ClassRelationType.Realization ? "Realisation" : value.ToString())).ToArray();
        MarkerAt.ItemsSource = Enum.GetValues<ClassMarkerAt>(); MemberVisibility.ItemsSource = Enum.GetValues<ClassVisibility>();
        EditSourceButton.IsEnabled = editSource is not null; IsEnabled = false;
        Loaded += async (_, _) => { try { draft = await Task.Run(() => new ClassDiagramVisualDraft(source, diagrams, ValidateNativeCandidateAsync, lifetime.Token), lifetime.Token); if (closed) return; busy = false; IsEnabled = true; await RefreshAsync(true); } catch (OperationCanceledException) { } };
        Closing += ConfirmClose; Closed += async (_, _) =>
        {
            closed = true; renderGeneration++; CancelPointer(); pendingRender?.Cancel(); lifetime.Cancel();
            await renderGate.WaitAsync(); renderGate.Release(); renderGate.Dispose(); lifetime.Dispose();
        };
        Deactivated += (_, _) => { EndPan(); CancelPointer(); };
    }
    public async void ApplyTheme(string theme) { dark = ThemeService.IsDark(theme); highlighting.Dark = dark; SourceEditor.TextArea.TextView.Redraw(); if (!closed && draft is not null) await RenderAsync(false); }
    private IEnumerable<Choice> Choices()
    {
        foreach (var cls in draft.Model.Classes)
        {
            yield return new(cls.Identity, "Class " + cls.Label + (cls.Implicit ? " (implicit)" : ""));
            foreach (var (member, index) in cls.Members.Select((item, i) => (item, i))) yield return new(member.Identity, "  " + cls.Id + " · " + (member.Semantic.IsMethod ? "method " : "attribute ") + (index + 1) + " · " + member.Statement.Read(draft.Source, "member"));
        }
        foreach (var (relationship, index) in draft.Model.Relationships.Select((item, i) => (item, i))) yield return new(relationship.Identity, "Relationship " + (index + 1) + " · " + relationship.Semantic.From + " → " + relationship.Semantic.To + " · " + relationship.Semantic.Label, "Relationships");
        foreach (var item in draft.Model.Namespaces) yield return new(item.Identity, "Namespace " + item.Name, "Namespaces");
        foreach (var item in draft.Model.Notes) yield return new(item.Identity, "Note · " + item.Semantic.Text);
        foreach (var statement in draft.Model.Statements.Where(item => item.Protected)) yield return new(statement.Identity, "Protected · " + statement.Span.Read(draft.Source), "Protected source");
    }
    private async Task RefreshAsync(bool fit = false)
    {
        updating = true; try { if (SourceEditor.Text != draft.Source) SourceEditor.Text = draft.Source; Elements.ItemsSource = Controls.MermaidWorkspace.Navigation(Choices().ToArray()); RestoreSelection(); ShowProperties(); } finally { updating = false; }
        await RenderAsync(fit); if (!closed && !fit) await Dispatcher.InvokeAsync(RevealSelection, System.Windows.Threading.DispatcherPriority.Loaded); UpdateActions();
    }
    private async Task ValidateNativeCandidateAsync(ClassDiagramModel candidate, CancellationToken cancellationToken)
    {
        var version = renderGeneration; var themeDark = dark; await renderGate.WaitAsync(cancellationToken);
        try
        {
            var render = await Task.Run(() => diagrams.Render(candidate.Source, themeDark, diagramTheme, cancellationToken), cancellationToken);
            var view = await StaWorker.RunAsync(() => NativeClassPresentation.Create(candidate, render, svg, cancellationToken), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested(); if (closed || version != renderGeneration) throw new ArgumentException("The Class presentation changed during validation. Retry against the current presentation; the draft is unchanged.");
            preparedPresentation = (candidate, themeDark, view);
        }
        finally { renderGate.Release(); }
    }
    private async Task RenderAsync(bool fit)
    {
        pendingRender?.Cancel(); var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); pendingRender = cancellation;
        CancelPointer(); canvasPick = null; var version = ++renderGeneration; mapped = false; UpdateActions(); var current = draft.Model; var themeDark = dark;
        Status.Text = "Rendering canonical Class diagram…"; var leased = false;
        try
        {
            await renderGate.WaitAsync(cancellation.Token); leased = true;
            var render = await Task.Run(() => diagrams.Render(current.Source, themeDark, diagramTheme, cancellation.Token), cancellation.Token);
            var ready = preparedPresentation is { } prepared && ReferenceEquals(prepared.Model, current) && prepared.Dark == themeDark ? prepared.View : null; preparedPresentation = null;
            var converted = await StaWorker.RunAsync(() =>
            {
                NativeClassPresentation? view = ready; string? diagnostic = null;
                try { view ??= NativeClassPresentation.Create(current, render, svg, cancellation.Token); } catch (Exception error) when (error is ArgumentException or InvalidDataException or FormatException) { diagnostic = error.Message; }
                return (view, Drawing: view?.Drawing ?? (render.Success ? svg.RenderCanonical(render, svg.Prepare(render.Svg!), cancellationToken: cancellation.Token) : null), diagnostic);
            }, cancellation.Token);
            if (closed || version != renderGeneration) return; presentation = converted.view;
            if (presentation is not null) { mapped = true; BuildCanvas(); }
            else { Surface.Children.Clear(); buttons.Clear(); Surface.Children.Add(new Image { Source = converted.Drawing, Stretch = Stretch.None, IsHitTestVisible = false }); Surface.Width = converted.Drawing?.Width ?? 420; Surface.Height = converted.Drawing?.Height ?? 260; }
            Status.Text = converted.diagnostic ?? (current.HasUnknownDependencies ? "Unknown source retained exactly. Inspect individual operation permissions."
                : presentation is { } view && view.Members.Count < current.Classes.Sum(item => item.Members.Count) ? "Some member rows are available through the element list; their canvas positions cannot be mapped confidently."
                : "Ready. Select a class, member row or relationship. Canonical layout determines positions.");
            AutomationProperties.SetItemStatus(this, SystemParameters.HighContrast ? "High Contrast" : themeDark ? "Dark" : "Light");
            if (fit) { Surface.UpdateLayout(); Fit(this, new()); if (Zoom.Value < 40) { Zoom.Value = 100; Viewport.ScrollToHorizontalOffset(0); Viewport.ScrollToVerticalOffset(0); } }
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is not OutOfMemoryException) { if (!closed && version == renderGeneration) { mapped = false; Status.Text = error.Message; } }
        finally { if (leased) renderGate.Release(); if (ReferenceEquals(pendingRender, cancellation)) pendingRender = null; cancellation.Dispose(); if (!closed && version == renderGeneration) UpdateActions(); }
    }
    private void RevealSelection()
    { if (draft.Selection is { } identity && buttons.TryGetValue(identity, out var target)) Controls.MermaidWorkspace.Reveal(target, Viewport); }
    private void RestoreSelection() => Elements.SelectedItem = Elements.Items.OfType<Choice>().FirstOrDefault(item => item.Identity == draft.Selection);
    private void SelectionChanged(object sender, SelectionChangedEventArgs e) { if (!updating && Elements.SelectedItem is Choice item) { Select(item.Identity); if (draft.Selection == item.Identity && !PropertiesPending) RevealSelection(); } }
    private void AttributeSelected(object sender, SelectionChangedEventArgs e) { if (!updating && AttributeRows.SelectedItem is Choice item) Select(item.Identity); }
    private void Select(string? identity)
    {
        if (busy || SourcePending || PropertiesPending) { Status.Text = "Update or discard pending properties and validate source before changing selection."; updating = true; try { RestoreSelection(); UpdateSelection(); } finally { updating = false; } return; }
        draft.Select(identity); updating = true; try { RestoreSelection(); ShowProperties(); UpdateSelection(); } finally { updating = false; } UpdateActions();
    }
    private void ShowProperties()
    {
        Controls.MermaidWorkspace.Inspect(InspectorViewport, draft.Selection);
        canvasPick = null; mode = "inspect"; NodeFields.Visibility = TransitionFields.Visibility = LabelFields.Visibility = AttributesPanel.Visibility = AttributeFields.Visibility = Visibility.Collapsed;
        Summary.Text = "Select a class, attribute, method or relationship."; Dependencies.Text = ""; attributeOwner = null;
        var endpoints = draft.Model.Classes.Select(item => new Choice(item.Id, item.Label)).ToArray(); From.ItemsSource = endpoints; To.ItemsSource = endpoints;
        var member = draft.Model.Classes.SelectMany(item => item.Members).FirstOrDefault(item => item.Identity == draft.Selection);
        var cls = draft.Model.Classes.FirstOrDefault(item => item.Identity == draft.Selection) ?? (member is null ? null : draft.Model.Class(member.ClassId));
        if (cls is not null)
        {
            attributeOwner = cls.Id; AttributesPanel.Visibility = Visibility.Visible;
            AttributeRows.ItemsSource = cls.Members.Select(item => new Choice(item.Identity, (item.Semantic.IsMethod ? "Method · " : "Attribute · ") + item.Statement.Read(draft.Source, "member"))).ToArray();
            AttributeRows.SelectedItem = AttributeRows.Items.OfType<Choice>().FirstOrDefault(item => item.Identity == member?.Identity);
            Summary.Text = "Class " + cls.Label;
            Dependencies.Text = (cls.Implicit ? "Implicit class. Adding a member safely materialises a declaration after its first occurrence. " : "") + "Relationships: " + string.Join(", ", draft.Model.Relationships.Where(item => item.Semantic.From == cls.Id || item.Semantic.To == cls.Id).Select(item => item.Semantic.From + " → " + item.Semantic.To + " (" + item.Semantic.Label + ")"));
            if (member is null)
            { NodeFields.Visibility = Visibility.Visible; NodeId.Text = cls.Id; GenericText.Text = cls.Label == cls.Id ? "" : cls.Label.StartsWith(cls.Id + "<", StringComparison.Ordinal) ? cls.Label[(cls.Id.Length + 1)..^1] : ""; AnnotationText.Text = cls.Annotation ?? ""; }
            else
            {
                AttributeFields.Visibility = Visibility.Visible; memberMethod = member.Semantic.IsMethod; Summary.Text = cls.Id + " · " + (memberMethod ? "method" : "attribute") + " occurrence " + (cls.Members.ToList().IndexOf(member) + 1);
                AttributeType.Text = member.Semantic.Type ?? ""; AttributeName.Text = member.Semantic.Name; Parameters.Text = member.Semantic.Params ?? ""; Parameters.IsEnabled = memberMethod;
                MemberVisibility.SelectedItem = member.Semantic.Visibility; StaticMember.IsChecked = member.Semantic.IsStatic; AbstractMember.IsChecked = member.Semantic.IsAbstract;
            }
        }
        else if (draft.Model.Relationships.FirstOrDefault(item => item.Identity == draft.Selection) is { } relationship)
        {
            var value = relationship.Semantic; TransitionFields.Visibility = LabelFields.Visibility = Visibility.Visible;
            From.SelectedValue = value.From; To.SelectedValue = value.To; FirstCardinality.Text = value.FromCardinality ?? ""; SecondCardinality.Text = value.ToCardinality ?? ""; LabelText.Text = value.Label ?? ""; RelationshipType.SelectedValue = value.Type; MarkerAt.SelectedItem = value.MarkerAt;
            Summary.Text = "Relationship occurrence " + (draft.Model.Relationships.ToList().IndexOf(relationship) + 1); Dependencies.Text = "Marker at " + value.MarkerAt + ". Cardinalities remain attached to their actual class when reversing.";
        }
        else if (draft.Model.Namespaces.FirstOrDefault(item => item.Identity == draft.Selection) is { } ns) { Summary.Text = "Namespace " + ns.Name; Dependencies.Text = "Protected membership: " + string.Join(", ", ns.ClassIds); }
        else if (draft.Model.Notes.FirstOrDefault(item => item.Identity == draft.Selection) is { } note) { Summary.Text = "Note for " + (note.Semantic.TargetClassId ?? "diagram"); Dependencies.Text = note.Semantic.Text + "\nRead-only source-backed note."; }
        else if (draft.Model.Statements.FirstOrDefault(item => item.Identity == draft.Selection) is { } statement) { Summary.Text = "Protected source occurrence"; Dependencies.Text = statement.Span.Read(draft.Source); }
        ContextDescription.Text = Choices().FirstOrDefault(item => item.Identity == draft.Selection)?.Description ?? "";
        baseline = Values;
    }
    private static string? Optional(string value) => value.Length == 0 ? null : value;
    private ClassMember MemberValue() => new(MemberVisibility.SelectedItem is ClassVisibility visibility ? visibility : ClassVisibility.None, AttributeName.Text, Optional(AttributeType.Text), StaticMember.IsChecked == true, AbstractMember.IsChecked == true, memberMethod, memberMethod ? Optional(Parameters.Text) : null);
    private ClassRelationship RelationshipValue()
    {
        var original = mode == "inspect" ? draft.Model.Relationships.FirstOrDefault(item => item.Identity == draft.Selection)?.Semantic : null;
        // An existing quoted empty cardinality differs from an absent cardinality.
        // An unrelated property edit must retain that exact source form.
        static string? Cardinality(string value, string? before) => value.Length == 0 && before == "" ? "" : Optional(value);
        return new(From.SelectedValue as string ?? "", To.SelectedValue as string ?? "", RelationshipType.SelectedValue is ClassRelationType type ? type : ClassRelationType.Association, MarkerAt.SelectedItem is ClassMarkerAt marker ? marker : ClassMarkerAt.To, Optional(LabelText.Text), Cardinality(FirstCardinality.Text, original?.FromCardinality), Cardinality(SecondCardinality.Text, original?.ToCardinality));
    }
    private ClassDiagramEditOperation? UpdateOperation() => mode == "class" ? new AddClass(NodeId.Text, Optional(GenericText.Text), Optional(AnnotationText.Text))
        : mode is "attribute" or "method" ? new AddClassMember(attributeOwner ?? "", MemberValue()) : mode == "relationship" ? new AddClassRelationship(RelationshipValue())
        : draft.Model.Classes.FirstOrDefault(item => item.Identity == draft.Selection) is { } cls ? new UpdateClass(cls.Id, NodeId.Text, Optional(GenericText.Text), Optional(AnnotationText.Text))
        : draft.Model.Classes.SelectMany(item => item.Members).Any(item => item.Identity == draft.Selection) ? new UpdateClassMember(draft.Selection!, MemberValue())
        : draft.Model.Relationships.Any(item => item.Identity == draft.Selection) ? new UpdateClassRelationship(draft.Selection!, RelationshipValue()) : null;
    private ClassDiagramEditOperation? DeleteOperation() => draft.Model.Classes.FirstOrDefault(item => item.Identity == draft.Selection) is { } cls ? new DeleteClass(cls.Id) : draft.Model.Relationships.Any(item => item.Identity == draft.Selection) ? new DeleteClassRelationship(draft.Selection!) : draft.Model.Classes.SelectMany(item => item.Members).Any(item => item.Identity == draft.Selection) ? new DeleteClassMember(draft.Selection!) : null;
    private ClassDiagramEditOperation? DuplicateOperation() => draft.Model.Classes.FirstOrDefault(item => item.Identity == draft.Selection) is { } cls ? new DuplicateClass(cls.Id, NextId("Copy")) : draft.Model.Relationships.Any(item => item.Identity == draft.Selection) ? new DuplicateClassRelationship(draft.Selection!) : draft.Model.Classes.SelectMany(item => item.Members).Any(item => item.Identity == draft.Selection) ? new DuplicateClassMember(draft.Selection!) : null;
    private string NextId(string prefix = "Class") { var index = 1; while (draft.Model.Classes.Any(item => item.Id == prefix + index) || draft.Model.Notes.Any(item => item.Semantic.TargetClassId == prefix + index)) index++; return prefix + index; }
    private void UpdateActions()
    {
        if (draft is null || UpdateButton is null) return; var can = CanEdit; var clean = !PropertiesPending;
        void Action(Button button, ClassDiagramEditOperation? operation, bool requireClean = true)
        {
            var capability = operation is null ? new ClassOperationCapability(false, "Select an applicable element.") : ClassDiagramCapabilityAnalysis.For(draft.Model, operation);
            button.IsEnabled = can && (!requireClean || clean) && capability.Allowed; button.ToolTip = capability.Reason; ToolTipService.SetShowOnDisabled(button, true); AutomationProperties.SetHelpText(button, capability.Reason);
        }
        Action(UpdateButton, UpdateOperation(), false); Action(DeleteButton, DeleteOperation()); Action(DuplicateButton, DuplicateOperation());
        Action(ReverseButton, draft.Model.Relationships.Any(item => item.Identity == draft.Selection) ? new ReverseClassRelationship(draft.Selection!) : null);
        Action(AddClassButton, new AddClass(NextId()));
        var ids = draft.Model.Classes.Select(item => item.Id).ToArray(); Action(AddRelationshipButton, ids.Length > 0 ? new AddClassRelationship(new(ids[0], ids[^1], ClassRelationType.Association, ClassMarkerAt.To)) : null);
        Action(AddAttributeButton, attributeOwner is null ? null : new AddClassMember(attributeOwner, new(ClassVisibility.Public, "NewAttribute", "string")));
        Action(AddMethodButton, attributeOwner is null ? null : new AddClassMember(attributeOwner, new(ClassVisibility.Public, "NewMethod", "bool", false, false, true)));
        var member = draft.Model.Classes.SelectMany(item => item.Members).FirstOrDefault(item => item.Identity == draft.Selection);
        Action(MoveUpButton, member is null ? null : new MoveClassMember(member.Identity, -1)); Action(MoveDownButton, member is null ? null : new MoveClassMember(member.Identity, 1));
        UndoButton.IsEnabled = !busy && clean && !SourcePending && draft.CanUndo; RedoButton.IsEnabled = !busy && clean && !SourcePending && draft.CanRedo;
        ApplyButton.IsEnabled = can && clean; ValidateButton.IsEnabled = !busy && clean; ResetButton.IsEnabled = !busy && !clean;
        var cls = draft.Model.Classes.FirstOrDefault(item => item.Identity == draft.Selection); var relationship = draft.Model.Relationships.FirstOrDefault(item => item.Identity == draft.Selection);
        ClassDiagramEditOperation? inspection = cls is not null ? new UpdateClass(cls.Id, cls.Id, ClassDiagramTransformationService.Generic(cls), cls.Annotation)
            : member is not null ? new UpdateClassMember(member.Identity, member.Semantic) : relationship is not null ? new UpdateClassRelationship(relationship.Identity, relationship.Semantic) : null;
        var authorised = can && inspection is not null && ClassDiagramCapabilityAnalysis.For(draft.Model, inspection).Allowed;
        NodeFields.IsEnabled = can && (mode == "class" || authorised); LabelFields.IsEnabled = AttributeFields.IsEnabled = TransitionFields.IsEnabled = authorised || can && mode != "inspect";
        var renameCapability = cls is null ? new ClassOperationCapability(false, "Select a class to inspect its ID references.")
            : ClassDiagramCapabilityAnalysis.For(draft.Model, new UpdateClass(cls.Id, NextId("Renamed"), ClassDiagramTransformationService.Generic(cls), cls.Annotation));
        NodeId.IsEnabled = can && (mode == "class" || renameCapability.Allowed); NodeId.ToolTip = renameCapability.Reason; ToolTipService.SetShowOnDisabled(NodeId, true); AutomationProperties.SetHelpText(NodeId, renameCapability.Reason);
        StaticMember.IsEnabled = !memberMethod; AbstractMember.IsEnabled = false;
        var markerReason = "Portable method classifier placement is not faithfully represented by Mermaider 0.15.1. Existing static/abstract method markers and abstract attributes remain protected; use Edit source.";
        StaticMember.ToolTip = memberMethod ? markerReason : "Portable static attribute marker ($)."; AbstractMember.ToolTip = markerReason;
        ToolTipService.SetShowOnDisabled(StaticMember, true); ToolTipService.SetShowOnDisabled(AbstractMember, true);
        AutomationProperties.SetHelpText(StaticMember, StaticMember.ToolTip.ToString()!); AutomationProperties.SetHelpText(AbstractMember, markerReason);
        var reconnect = can && !draft.Model.HasUnknownDependencies;
        From.IsEnabled = To.IsEnabled = ReconnectFromButton.IsEnabled = ReconnectToButton.IsEnabled = reconnect;
        foreach (var handle in endpointHandles.OfType<Button>()) { handle.IsEnabled = reconnect && clean && relationship?.Statement.Protected == false; AutomationProperties.SetHelpText(handle, reconnect ? "Drag to a class, or choose an endpoint in the keyboard-accessible inspector." : "Unknown dependencies protect endpoint reconnection."); }
        foreach (var panel in new[] { NodeFields, LabelFields, AttributeFields, TransitionFields }) { panel.ToolTip = UpdateButton.ToolTip; ToolTipService.SetShowOnDisabled(panel, true); AutomationProperties.SetHelpText(panel, UpdateButton.ToolTip?.ToString() ?? ""); }
        var state = draft.Model.Compatibility == ClassCompatibility.PartiallyEditable ? "Partially editable" : draft.Model.Compatibility.ToString();
        Capabilities.Text = "Source: " + state + ".\nUpdate: " + UpdateButton.ToolTip + "\nDelete: " + DeleteButton.ToolTip + "\nDuplicate: " + DuplicateButton.ToolTip + (cls is null ? "" : "\nClass ID: " + renameCapability.Reason);
        InspectorState.Text = PropertiesPending ? "Pending properties" : SourcePending ? "Source validation required" : !mapped ? "Apply unavailable" : draft.Selection is null ? "Ready" : UpdateButton.IsEnabled ? "Editable" : "Protected source";
    }
    private void BeginCreation(string kind)
    {
        if (!CanEdit || PropertiesPending) return; updating = true;
        try
        {
            ShowProperties(); mode = kind; Summary.Text = "New " + kind; ContextDescription.Text = ""; NodeFields.Visibility = kind == "class" ? Visibility.Visible : Visibility.Collapsed; TransitionFields.Visibility = kind == "relationship" ? Visibility.Visible : Visibility.Collapsed;
            LabelFields.Visibility = kind == "relationship" ? Visibility.Visible : Visibility.Collapsed; AttributeFields.Visibility = kind is "attribute" or "method" ? Visibility.Visible : Visibility.Collapsed; AttributesPanel.Visibility = Visibility.Collapsed;
            NodeId.Text = NextId(); GenericText.Text = AnnotationText.Text = LabelText.Text = FirstCardinality.Text = SecondCardinality.Text = ""; From.SelectedIndex = 0; To.SelectedIndex = Math.Min(1, To.Items.Count - 1); RelationshipType.SelectedValue = ClassRelationType.Association; MarkerAt.SelectedItem = ClassMarkerAt.To;
            memberMethod = kind == "method"; AttributeType.Text = memberMethod ? "bool" : "string"; AttributeName.Text = memberMethod ? "NewMethod" : "NewAttribute"; Parameters.Text = ""; Parameters.IsEnabled = memberMethod; MemberVisibility.SelectedItem = ClassVisibility.Public; StaticMember.IsChecked = AbstractMember.IsChecked = false; canvasPick = kind == "relationship" ? "from" : null;
        }
        finally { updating = false; } UpdateActions();
    }
    private void AddClass(object sender, RoutedEventArgs e) => BeginCreation("class");
    private void AddRelationship(object sender, RoutedEventArgs e) => BeginCreation("relationship");
    private void AddAttribute(object sender, RoutedEventArgs e) => BeginCreation("attribute");
    private void AddMethod(object sender, RoutedEventArgs e) => BeginCreation("method");
    private void ReconnectFrom(object sender, RoutedEventArgs e) { canvasPick = "from-reconnect"; Status.Text = "Choose the new first class, then Update properties."; }
    private void ReconnectTo(object sender, RoutedEventArgs e) { canvasPick = "to"; Status.Text = "Choose the new second class, then Update properties."; }
    private async void Update(object sender, RoutedEventArgs e) { if (UpdateOperation() is { } operation) await Execute(operation); }
    private async Task Execute(ClassDiagramEditOperation operation)
    {
        if (!CanEdit) return; CancelPointer(); busy = true; UpdateActions();
        try
        {
            var result = await draft.ExecuteAsync(operation, lifetime.Token); if (closed) return; if (!result.Success) { Status.Text = result.Error; return; }
            if (operation is AddClass add) draft.Select(draft.Model.Class(add.Id).Identity); else if (operation is DuplicateClass duplicate) draft.Select(draft.Model.Class(duplicate.NewId).Identity);
            else if (operation is AddClassRelationship or DuplicateClassRelationship) draft.Select(draft.Model.Relationships[^1].Identity);
            else if (operation is AddClassMember member) draft.Select((member.Member.IsMethod ? draft.Model.Class(member.ClassId).Methods : draft.Model.Class(member.ClassId).Attributes)[^1].Identity);
            await RefreshAsync();
            RevealSelection();
        }
        finally { busy = false; if (!closed) UpdateActions(); }
    }
    private async void Delete(object sender, RoutedEventArgs e) { if (!PropertiesPending && DeleteOperation() is { } operation) await Execute(operation); }
    private async void Duplicate(object sender, RoutedEventArgs e) { if (!PropertiesPending && DuplicateOperation() is { } operation) await Execute(operation); }
    private async void Reverse(object sender, RoutedEventArgs e) { if (!PropertiesPending && draft.Model.Relationships.Any(item => item.Identity == draft.Selection)) await Execute(new ReverseClassRelationship(draft.Selection!)); }
    private async void MoveUp(object sender, RoutedEventArgs e) { if (!PropertiesPending && draft.Selection is { } identity) await Execute(new MoveClassMember(identity, -1)); }
    private async void MoveDown(object sender, RoutedEventArgs e) { if (!PropertiesPending && draft.Selection is { } identity) await Execute(new MoveClassMember(identity, 1)); }
    private void ResetProperties(object sender, RoutedEventArgs e) { if (busy) return; updating = true; try { ShowProperties(); } finally { updating = false; } UpdateActions(); Status.Text = "Pending properties discarded; validated source is unchanged."; }
    private async void Undo(object sender, RoutedEventArgs e) { if (busy || PropertiesPending || SourcePending) return; CancelPointer(); draft.Undo(); await RefreshAsync(); }
    private async void Redo(object sender, RoutedEventArgs e) { if (busy || PropertiesPending || SourcePending) return; CancelPointer(); draft.Redo(); await RefreshAsync(); }
    private void SourceChanged(object? sender, EventArgs e) { if (draft is not null && !updating) { UpdateActions(); Status.Text = "Validate source changes before visual editing or Apply."; } }
    private async void ValidateSource(object sender, RoutedEventArgs e)
    {
        if (busy || PropertiesPending) return; busy = true; UpdateActions();
        try { var result = await draft.ValidateSourceAsync(SourceEditor.Text, lifetime.Token); if (closed) return; if (result.Success) { await RefreshAsync(); Status.Text = "Complete Class semantics and canonical rendering validated. Unknown source remains protected."; } else Status.Text = result.Error; }
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
        if ((draft.Source != draft.OriginalSource || SourcePending || PropertiesPending) && MessageBox.Show(this, "Discard the private Class draft and pending properties? Current changes in the owning document will be kept.", "Discard Class draft", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) e.Cancel = true;
    }
    private void LocateSource(object sender, RoutedEventArgs e)
    {
        var statement = draft.Model.Classes.FirstOrDefault(item => item.Identity == draft.Selection)?.Declarations.FirstOrDefault() ?? draft.Model.Relationships.FirstOrDefault(item => item.Identity == draft.Selection)?.Statement ?? draft.Model.Classes.SelectMany(item => item.Members).FirstOrDefault(item => item.Identity == draft.Selection)?.Statement ?? draft.Model.Statements.FirstOrDefault(item => item.Identity == draft.Selection);
        if (statement is null || SourcePending) return; SourcePanel.IsExpanded = true; SourceEditor.Select(statement.Span.Start, statement.Span.Length); SourceEditor.ScrollToLine(SourceEditor.Document.GetLineByOffset(statement.Span.Start).LineNumber); SourceEditor.Focus();
    }
    private void ShowOriginal(object sender, RoutedEventArgs e) { new Window { Owner = this, Title = "Original portable Mermaid", Width = 700, Height = 450, Content = new ICSharpCode.AvalonEdit.TextEditor { Text = draft.OriginalSource, IsReadOnly = true, ShowLineNumbers = true, FontFamily = SourceEditor.FontFamily, FontSize = SourceEditor.FontSize, Background = (Brush)FindResource("WindowBrush"), Foreground = (Brush)FindResource("TextBrush") }, WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show(); }
    private void CopySource(object sender, RoutedEventArgs e)
    { try { Clipboard.SetText(SourceEditor.Text); Status.Text = "Mermaid draft source copied."; } catch (System.Runtime.InteropServices.ExternalException) { Status.Text = "The clipboard is unavailable. Select and copy the source directly."; } }
    private void ToggleSource(object sender, RoutedEventArgs e) => SourcePanel.IsExpanded = !SourcePanel.IsExpanded;
    private void Preview(object sender, RoutedEventArgs e)
    { if (presentation is { } view) new Window { Owner = this, Title = "Rendered Class preview", Width = 850, Height = 600, Content = new ScrollViewer { Content = new Image { Source = view.Drawing, Stretch = Stretch.None }, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto }, Background = (Brush)FindResource("WindowBrush") }.Show(); }
    private void ZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (Scale is not null) Scale.ScaleX = Scale.ScaleY = Zoom.Value / 100; }
    private void ZoomIn(object sender, RoutedEventArgs e) => Zoom.Value *= 1.15;
    private void ZoomOut(object sender, RoutedEventArgs e) => Zoom.Value /= 1.15;
    private void ActualSize(object sender, RoutedEventArgs e) => Zoom.Value = 100;
    private void Fit(object sender, RoutedEventArgs e) { if (Surface.Width > 0 && Surface.Height > 0) Zoom.Value = Math.Clamp(Math.Min((Viewport.ViewportWidth - 20) / Surface.Width, (Viewport.ViewportHeight - 20) / Surface.Height) * 100, .1, 100); }
    private void AutoLayout(object sender, RoutedEventArgs e) { CancelPointer(); Fit(sender, e); Viewport.ScrollToHorizontalOffset(0); Viewport.ScrollToVerticalOffset(0); Status.Text = "Canonical Class layout centred. Portable source is unchanged."; }
    private void Wheel(object sender, MouseWheelEventArgs e) => Controls.MermaidWorkspace.ZoomAtPointer(Viewport, Zoom, e);
    private void PanStart(object sender, MouseButtonEventArgs e) { if (e.ChangedButton != MouseButton.Middle) return; CancelPointer(); pan = e.GetPosition(Viewport); panHorizontal = Viewport.HorizontalOffset; panVertical = Viewport.VerticalOffset; Viewport.CaptureMouse(); Viewport.Cursor = Cursors.Hand; e.Handled = true; }
    private void PanMove(object sender, MouseEventArgs e) { if (pan is not { } start) return; var point = e.GetPosition(Viewport); Viewport.ScrollToHorizontalOffset(panHorizontal + start.X - point.X); Viewport.ScrollToVerticalOffset(panVertical + start.Y - point.Y); }
    private void PanEnd(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Middle) EndPan(); }
    private void EndPan() { pan = null; if (Viewport.IsMouseCaptured) Viewport.ReleaseMouseCapture(); Viewport.Cursor = Cursors.Arrow; }
    private void Shortcut(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && new[] { From, To, RelationshipType, MarkerAt, MemberVisibility }.Any(combo => combo.IsDropDownOpen)) return;
        if (e.Key == Key.Escape) { if (endpointDrag is not null) CancelPointer(); else if (canvasPick is not null) { canvasPick = null; Status.Text = "Canvas picking cancelled."; } else if (pan is not null) EndPan(); else Close(); e.Handled = true; return; }
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { Apply(sender, new()); e.Handled = true; return; }
        if (e.OriginalSource is TextBoxBase || SourceEditor.IsKeyboardFocusWithin) return;
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z) Undo(sender, new()); else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y) Redo(sender, new());
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.D) Duplicate(sender, new()); else if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Delete) Delete(sender, new());
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.D0) ActualSize(sender, new()); else if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.F) Fit(sender, new()); else return; e.Handled = true;
    }
}
