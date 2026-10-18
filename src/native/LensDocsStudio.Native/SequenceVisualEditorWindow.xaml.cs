using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.Sequence;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public partial class SequenceVisualEditorWindow : Window
{
    private SequenceVisualDraft draft = null!;
    private readonly DiagramService diagrams;
    private readonly NativeSvgService svg = new();
    private readonly Func<string, CancellationToken, Task<string?>> applyToDocument;
    private readonly Action? editSource;
    private readonly string diagramTheme;
    private readonly CancellationTokenSource lifetime = new();
    private readonly TaskCompletionSource closedCleanup = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly SemaphoreSlim renderGate = new(1, 1);
    private CancellationTokenSource? pendingRender;
    private readonly MermaidSourceHighlighting highlighting;
    private readonly Dictionary<ToggleButton, string> targets = [];
    private bool dark, updating, busy = true, closed, discard, mapped;
    private int renderGeneration;
    private string mode = "inspect", baseline = "", markerBaseline = "";
    private string? anchor;
    private Point? pan;
    private double panHorizontal, panVertical;
    private record Choice(string Identity, string Description) { public override string ToString() => Description; }
    private bool SourcePending => draft is not null && SourceEditor.Text != draft.Source;
    private string Values => string.Join('\u001f', ParticipantId.Text, Actor.IsChecked, LabelText.Text, Sender.SelectedItem,
        Recipient.SelectedItem, Arrow.SelectedItem, Marker.SelectedItem, Placement.SelectedItem, First.SelectedItem, Second.SelectedItem, Insertion.SelectedIndex);
    private bool PropertiesPending => draft is not null && (mode != "inspect" || baseline != Values);
    private bool CanEdit => draft is not null && !busy && !SourcePending && mapped && draft.Model.CanEdit;

    public SequenceVisualEditorWindow(string source, DiagramService diagrams, Func<string, CancellationToken, Task<string?>> applyToDocument,
        bool dark = false, string diagramTheme = "Auto", Action? editSource = null, FontFamily? sourceFont = null, double sourceFontSize = 14)
    {
        this.diagrams = diagrams; this.applyToDocument = applyToDocument; this.dark = dark; this.diagramTheme = diagramTheme; this.editSource = editSource;
        highlighting = new(dark);
        InitializeComponent(); WindowStateService.FitInitialWindow(this);
        SourceEditor.FontFamily = sourceFont ?? new FontFamily("Cascadia Code, Consolas"); SourceEditor.FontSize = sourceFontSize;
        SourceEditor.Options.EnableHyperlinks = false; SourceEditor.Options.EnableEmailHyperlinks = false;
        SourceEditor.TextArea.TextView.LineTransformers.Add(highlighting);
        Arrow.ItemsSource = SequenceSourceParser.Arrows; Marker.ItemsSource = new[] { "None", "Activate (+)", "Deactivate (-)" };
        Placement.ItemsSource = new[] { "left of", "right of", "over" }; Insertion.ItemsSource = new[] { "At end", "Before selection", "After selection" };
        foreach (var field in new[] { ParticipantId, LabelText }) field.TextChanged += (_, _) => { if (!updating) UpdateActions(); };
        foreach (var field in new[] { Sender, Recipient, Arrow, Marker, Placement, First, Second, Insertion }) field.SelectionChanged += (_, _) => { if (!updating) UpdateActions(); };
        Actor.Checked += (_, _) => { if (!updating) UpdateActions(); }; Actor.Unchecked += (_, _) => { if (!updating) UpdateActions(); };
        EditSourceButton.IsEnabled = editSource is not null;
        IsEnabled = false;
        Loaded += async (_, _) =>
        {
            try
            {
                draft = await Task.Run(() => new SequenceVisualDraft(source, diagrams), lifetime.Token);
                if (closed) return; busy = false; IsEnabled = true; await RefreshAsync(true);
            }
            catch (OperationCanceledException) { }
        };
        Closing += ConfirmClose;
        Closed += async (_, _) =>
        {
            using var cleanupPhase = LensDocsStudio.Core.Rendering.MermaidDiagnostics.Measure("editor.cleanup.Sequence");
            closed = true; renderGeneration++; pendingRender?.Cancel(); lifetime.Cancel();
            await renderGate.WaitAsync(); renderGate.Release(); renderGate.Dispose(); lifetime.Dispose(); closedCleanup.SetResult();
        };
        Deactivated += (_, _) => EndPan();
    }
    public async void ApplyTheme(string theme)
    {
        dark = ThemeService.IsDark(theme); highlighting.Dark = dark; SourceEditor.TextArea.TextView.Redraw();
        if (!closed && draft is not null) await RenderAsync(false);
    }
    private async Task RefreshAsync(bool fit = false)
    {
        updating = true;
        try
        {
            if (SourceEditor.Text != draft.Source) SourceEditor.Text = draft.Source;
            Participants.ItemsSource = draft.Model.Participants.Select(item => new Choice("participant:" + item.Id, item.Id + " · " + item.Label)).ToArray();
            var descriptions = draft.Model.Messages.Select((item, index) => (item.Identity, Text: "Message " + (index + 1) + " · " + item.Sender + " → " + item.Recipient + ": " + item.Text)).ToDictionary(item => item.Identity, item => item.Text);
            Interactions.ItemsSource = draft.Model.Statements.Where(item => item.Kind is SequenceStatementKind.Message or SequenceStatementKind.Note or SequenceStatementKind.Activation or SequenceStatementKind.Block)
                .Select(item => new Choice(item.Identity, descriptions.TryGetValue(item.Identity, out var description) ? description : item.Kind + " · " + item.Span.Read(draft.Source))).ToArray();
            RestoreSelection(); ShowProperties();
        }
        finally { updating = false; }
        await RenderAsync(fit); if (!closed && !fit) await Dispatcher.InvokeAsync(RevealSelection, System.Windows.Threading.DispatcherPriority.Loaded); UpdateActions();
    }
    private async Task RenderAsync(bool fit)
    {
        var version = ++renderGeneration; mapped = false; UpdateActions(); var model = draft.Model; var themeDark = dark;
        Status.Text = "Rendering canonical Sequence diagram…";
        pendingRender?.Cancel(); var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); pendingRender = cancellation;
        var leased = false;
        try
        {
            // The converter cannot be interrupted mid-drawing. Serialise STA conversion, then reject stale generations.
            await renderGate.WaitAsync(cancellation.Token); leased = true;
            var render = await Task.Run(() => diagrams.Render(model.Source, themeDark, diagramTheme, cancellation.Token), cancellation.Token);
            if (closed || version != renderGeneration) return;
            targets.Clear(); Overlay.Children.Clear();
            if (model.Participants.Count == 0 && model.CanEdit)
            { CanonicalImage.Source = null; Surface.Width = 420; Surface.Height = 260; mapped = true; Status.Text = "Add participants, then add a message to begin."; return; }
            var converted = await StaWorker.RunAsync(() =>
            {
                NativeSequencePresentation? presentation = null; string? diagnostic = null;
                if (model.CanEdit)
                {
                    try { presentation = NativeSequencePresentation.Create(model, render, svg, cancellation.Token); }
                    catch (Exception error) when (error is ArgumentException or InvalidDataException or FormatException) { diagnostic = error.Message; }
                }
                var drawing = presentation?.Drawing ?? (render.Success ? svg.RenderCanonical(render, svg.Prepare(render.Svg!), cancellationToken: cancellation.Token) : null);
                return (presentation, drawing, diagnostic);
            }, cancellation.Token);
            if (closed || version != renderGeneration) return;
            var (presentation, drawing, diagnostic) = converted;
            CanonicalImage.Source = drawing; CanonicalImage.Width = drawing?.Width ?? 420; CanonicalImage.Height = drawing?.Height ?? 260;
            Surface.Width = CanonicalImage.Width; Surface.Height = CanonicalImage.Height;
            if (presentation is not null)
            {
                using var overlayPhase = LensDocsStudio.Core.Rendering.MermaidDiagnostics.Measure("editor.overlay.Sequence");
                foreach (var target in presentation.Targets)
                {
                    var button = new ToggleButton { Width = Math.Max(10, target.Bounds.Width), Height = Math.Max(10, target.Bounds.Height),
                        Style = (Style)FindResource("Sequence.HitTarget"), ToolTip = target.Description, Tag = target.Identity };
                    AutomationProperties.SetName(button, target.Description); AutomationProperties.SetAutomationId(button, "SequenceCanvas_" + target.Identity);
                    if (targets.Values.Contains(target.Identity)) AutomationProperties.SetAutomationId(button, "SequenceCanvas_" + target.Identity + "_" + targets.Count);
                    button.Click += (_, _) => Select(target.Identity);
                    button.Checked += (_, _) => { if (!updating) Select(target.Identity); };
                    Canvas.SetLeft(button, target.Bounds.X); Canvas.SetTop(button, target.Bounds.Y); Overlay.Children.Add(button); targets.Add(button, target.Identity);
                }
                mapped = true;
            }
            UpdateSelectionOverlay();
            Status.Text = !model.CanEdit ? string.Join(" ", model.Diagnostics) : "Ready. Select an element or add an interaction. Middle mouse pans; source order controls layout.";
            if (render.Error is not null) Status.Text = render.Error;
            else if (diagnostic is not null) Status.Text = diagnostic;
            else if (presentation?.Diagnostics.Count > 0) Status.Text = string.Join(" ", presentation.Diagnostics);
            if (fit) { Surface.UpdateLayout(); Fit(this, new()); if (Zoom.Value < 40) { Zoom.Value = 100; Viewport.ScrollToHorizontalOffset(0); Viewport.ScrollToVerticalOffset(0); } }
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (!closed && version == renderGeneration)
            { CanonicalImage.Source = null; targets.Clear(); Overlay.Children.Clear(); Status.Text = error.Message; }
        }
        finally
        {
            if (leased) renderGate.Release();
            if (ReferenceEquals(pendingRender, cancellation)) pendingRender = null;
            cancellation.Dispose();
            if (!closed && version == renderGeneration) UpdateActions();
        }
    }
    private void RevealSelection()
    { var target = targets.FirstOrDefault(item => item.Value == draft.Selection).Key; if (target is not null) Controls.MermaidWorkspace.Reveal(target, Viewport); }
    private void RestoreSelection()
    {
        foreach (var list in new[] { Participants, Interactions })
        {
            var selected = list.Items.OfType<Choice>().FirstOrDefault(item => item.Identity == draft.Selection);
            var changed = !Equals(list.SelectedItem, selected); list.SelectedItem = selected;
            if (changed && selected is not null) Controls.MermaidWorkspace.RevealNavigation(list, selected);
        }
    }
    private void SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updating || sender is not ListBox list || list.SelectedItem is not Choice item) return; Select(item.Identity); if (draft.Selection == item.Identity && !PropertiesPending) RevealSelection();
    }
    private void Select(string identity)
    {
        using var selectionPhase = LensDocsStudio.Core.Rendering.MermaidDiagnostics.Measure("editor.selection.Sequence");
        if (busy || SourcePending || PropertiesPending)
        { Status.Text = "Update or discard pending properties and validate source before changing selection."; updating = true; try { RestoreSelection(); UpdateSelectionOverlay(); } finally { updating = false; } return; }
        draft.Select(identity); updating = true;
        try { RestoreSelection(); ShowProperties(); UpdateSelectionOverlay(); } finally { updating = false; }
        UpdateActions();
    }
    private void UpdateSelectionOverlay()
    { var previous = updating; updating = true; try { foreach (var pair in targets) pair.Key.IsChecked = pair.Value == draft.Selection; } finally { updating = previous; } }
    private void ShowProperties()
    {
        Controls.MermaidWorkspace.Inspect(InspectorViewport, draft.Selection);
        mode = "inspect"; ParticipantFields.Visibility = MessageFields.Visibility = NoteFields.Visibility = LabelFields.Visibility = InsertionFields.Visibility = Visibility.Collapsed;
        Dependencies.Text = ""; Summary.Text = "Select a participant, message or note.";
        var ids = draft.Model.Participants.Select(item => item.Id).ToArray();
        Sender.ItemsSource = Recipient.ItemsSource = First.ItemsSource = ids; Second.ItemsSource = new[] { "" }.Concat(ids).ToArray();
        if (draft.Selection?.StartsWith("participant:", StringComparison.Ordinal) == true)
        {
            var participant = draft.Model.Participants.SingleOrDefault(item => "participant:" + item.Id == draft.Selection);
            if (participant is not null)
            {
                ParticipantFields.Visibility = LabelFields.Visibility = Visibility.Visible;
                ParticipantId.Text = participant.Id; LabelText.Text = SequenceSourceParser.NormaliseLabel(participant.Label); Actor.IsChecked = participant.Actor;
                Summary.Text = (participant.Actor ? "Actor " : "Participant ") + participant.Id;
                Dependencies.Text = "Position " + (Array.IndexOf(ids, participant.Id) + 1) + " · " + draft.Model.Messages.Count(item => item.Sender == participant.Id || item.Recipient == participant.Id) + " referencing messages · "
                    + draft.Model.Notes.Count(item => item.Participants.Contains(participant.Id)) + " notes · " + draft.Model.Activations.Count(item => item.Participant == participant.Id) + " activation commands/markers."
                    + (participant.Declaration is null ? " Implicit participant: edit its declaration in source." : "");
            }
        }
        else
        {
            var activation = draft.Model.Activations.FirstOrDefault(item => item.Identity == draft.Selection);
            var message = draft.Model.Messages.FirstOrDefault(item => item.Identity == (activation?.MessageIdentity ?? draft.Selection));
            var note = draft.Model.Notes.FirstOrDefault(item => item.Identity == draft.Selection);
            if (message is not null)
            {
                MessageFields.Visibility = LabelFields.Visibility = Visibility.Visible;
                Sender.SelectedItem = message.Sender; Recipient.SelectedItem = message.Recipient; Arrow.SelectedItem = message.Arrow;
                LabelText.Text = SequenceSourceParser.NormaliseLabel(message.Text);
                var active = draft.Model.Activations.FirstOrDefault(item => item.MessageIdentity == message.Identity);
                Marker.SelectedIndex = active is null ? 0 : active.Activate ? 1 : 2;
                Summary.Text = (activation is null ? "Message " : "Activation · ") + message.Sender + " → " + message.Recipient;
                Dependencies.Text = active is null ? "Message identity is retained privately; source order is authoritative."
                    : (active.Activate ? "Activates " : "Deactivates ") + active.Participant + (active.Standalone ? " using the original standalone command." : " using the message marker.");
                if (message.Statement.Depth > 0) Dependencies.Text += " Structural boundaries protect insertion, deletion, reordering and activation changes.";
            }
            else if (note is not null)
            {
                NoteFields.Visibility = LabelFields.Visibility = Visibility.Visible; Placement.SelectedItem = note.Placement; First.SelectedItem = note.Participants[0]; Second.SelectedItem = note.Participants.ElementAtOrDefault(1) ?? "";
                LabelText.Text = SequenceSourceParser.NormaliseLabel(note.Text); Summary.Text = "Note " + note.Placement + " " + string.Join(", ", note.Participants);
            }
            else if (draft.Model.Statements.FirstOrDefault(item => item.Identity == draft.Selection) is { } statement)
            { Summary.Text = "Protected " + statement.Kind; Dependencies.Text = statement.Span.Read(draft.Source) + ". Use Edit source for structural changes."; }
        }
        markerBaseline = Marker.SelectedItem as string ?? "None"; ContextDescription.Text = Participants.Items.OfType<Choice>().Concat(Interactions.Items.OfType<Choice>()).FirstOrDefault(item => item.Identity == draft.Selection)?.Description ?? "";
        baseline = Values;
    }
    private string? MessageIdentity => draft.Model.Activations.FirstOrDefault(item => item.Identity == draft.Selection)?.MessageIdentity ?? draft.Selection;
    private void UpdateActions()
    {
        if (draft is null || SourceEditor is null || UpdateButton is null) return;
        var ready = CanEdit; var pending = PropertiesPending;
        SourceEditor.IsReadOnly = busy || pending; ApplyButton.IsEnabled = ready && !pending;
        UndoButton.IsEnabled = !busy && !pending && !SourcePending && draft.CanUndo; RedoButton.IsEnabled = !busy && !pending && !SourcePending && draft.CanRedo;
        AddParticipantButton.IsEnabled = ready && !pending; AddMessageButton.IsEnabled = AddNoteButton.IsEnabled = ready && !pending && draft.Model.Participants.Count > 0;
        UpdateButton.IsEnabled = ready && pending && LabelFields.Visibility == Visibility.Visible;
        ActivationButton.Visibility = mode == "inspect" ? Visibility.Visible : Visibility.Collapsed;
        ActivationButton.IsEnabled = ready && Marker.SelectedItem as string != markerBaseline && OtherMessageValuesUnchanged();
        ResetButton.IsEnabled = !busy && pending; ValidateButton.IsEnabled = !busy && !pending;
        ParticipantFields.IsEnabled = MessageFields.IsEnabled = NoteFields.IsEnabled = LabelFields.IsEnabled = ready;
        var implicitParticipant = draft.Model.Participants.FirstOrDefault(item => "participant:" + item.Id == draft.Selection)?.Declaration is null && ParticipantFields.Visibility == Visibility.Visible;
        if (implicitParticipant) { ParticipantFields.IsEnabled = LabelFields.IsEnabled = false; UpdateButton.IsEnabled = false; }
        SetAction(DeleteButton, DeleteOperation()); SetAction(DuplicateButton, draft.Model.Messages.Any(item => item.Identity == draft.Selection) ? new DuplicateSequenceMessage(draft.Selection!) : null);
        SetAction(EarlierButton, MoveOperation(-1)); SetAction(LaterButton, MoveOperation(1));
        InspectorState.Text = pending ? "Pending properties" : SourcePending ? "Source validation required" : !ready ? "Apply unavailable" : draft.Selection is null ? "Ready" : LabelFields.Visibility == Visibility.Visible && LabelFields.IsEnabled ? "Editable" : "Protected source";
        Title = "Mermaid Visual Editor / Sequence" + (draft.Source != draft.OriginalSource || pending || SourcePending ? " •" : "");
        void SetAction(Button button, SequenceEditOperation? operation)
        {
            button.IsEnabled = false; var reason = "Select an eligible element.";
            if (operation is not null && ready && !pending)
            { try { _ = new SequenceTransformationService().Plan(draft.Model, operation); button.IsEnabled = true; reason = "Validate and apply this private draft operation."; } catch (ArgumentException error) { reason = error.Message; } }
            button.ToolTip = reason; ToolTipService.SetShowOnDisabled(button, true); AutomationProperties.SetHelpText(button, reason);
        }
    }
    private bool OtherMessageValuesUnchanged()
    {
        var message = draft.Model.Messages.FirstOrDefault(item => item.Identity == MessageIdentity);
        return message is not null && Sender.SelectedItem as string == message.Sender && Recipient.SelectedItem as string == message.Recipient
            && Arrow.SelectedItem as string == message.Arrow && LabelText.Text == SequenceSourceParser.NormaliseLabel(message.Text);
    }
    private SequenceEditOperation? DeleteOperation() => draft.Selection?.StartsWith("participant:", StringComparison.Ordinal) == true ? new DeleteSequenceParticipant(draft.Selection[12..])
        : draft.Selection is not null && draft.Model.Statements.Any(item => item.Identity == draft.Selection && item.Kind is SequenceStatementKind.Message or SequenceStatementKind.Note) ? new DeleteSequenceInteraction(draft.Selection) : null;
    private SequenceEditOperation? MoveOperation(int direction) => draft.Selection?.StartsWith("participant:", StringComparison.Ordinal) == true ? new MoveSequenceParticipant(draft.Selection[12..], direction)
        : draft.Model.Messages.Any(item => item.Identity == draft.Selection) ? new MoveSequenceMessage(draft.Selection!, direction) : null;
    private void BeginCreation(string kind)
    {
        if (!CanEdit || PropertiesPending) return;
        updating = true; try
        {
            anchor = draft.Model.Statements.Any(item => item.Identity == draft.Selection && item.Kind is SequenceStatementKind.Message or SequenceStatementKind.Note) ? draft.Selection : null;
            ShowProperties(); mode = kind; Summary.Text = "New " + kind; ContextDescription.Text = ""; LabelFields.Visibility = Visibility.Visible;
            ParticipantFields.Visibility = kind == "participant" ? Visibility.Visible : Visibility.Collapsed;
            MessageFields.Visibility = kind == "message" ? Visibility.Visible : Visibility.Collapsed;
            NoteFields.Visibility = kind == "note" ? Visibility.Visible : Visibility.Collapsed;
            InsertionFields.Visibility = kind == "participant" ? Visibility.Collapsed : Visibility.Visible;
            var number = 1; while (draft.Model.Participants.Any(item => item.Id == "Participant" + number)) number++;
            ParticipantId.Text = "Participant" + number; Actor.IsChecked = false;
            LabelText.Text = kind == "participant" ? ParticipantId.Text : kind == "message" ? "Message" : "Note";
            Sender.SelectedIndex = First.SelectedIndex = 0; Recipient.SelectedIndex = Math.Min(1, Recipient.Items.Count - 1); Second.SelectedIndex = 0;
            Arrow.SelectedItem = "->>"; Marker.SelectedIndex = 0; Placement.SelectedItem = "over"; Insertion.SelectedIndex = anchor is null ? 0 : 2;
        } finally { updating = false; }
        UpdateActions(); LabelText.Focus(); LabelText.SelectAll();
    }
    private void AddParticipant(object sender, RoutedEventArgs e) => BeginCreation("participant");
    private void AddMessage(object sender, RoutedEventArgs e) => BeginCreation("message");
    private void AddNote(object sender, RoutedEventArgs e) => BeginCreation("note");
    private async void Update(object sender, RoutedEventArgs e)
    {
        if (!CanEdit) return;
        var operations = new List<SequenceEditOperation>(); var position = Insertion.SelectedIndex == 0 ? null : anchor; var before = Insertion.SelectedIndex == 1;
        if (mode == "participant") operations.Add(new AddSequenceParticipant(ParticipantId.Text, LabelText.Text, Actor.IsChecked == true));
        else if (mode == "message") operations.Add(new AddSequenceMessage(Sender.SelectedItem as string ?? "", Recipient.SelectedItem as string ?? "", LabelText.Text, Arrow.SelectedItem as string ?? "", position, before));
        else if (mode == "note") operations.Add(new AddSequenceNote(Placement.SelectedItem as string ?? "", First.SelectedItem as string ?? "", Second.SelectedItem as string, LabelText.Text, position, before));
        else if (ParticipantFields.Visibility == Visibility.Visible && draft.Selection is { } participant)
            operations.Add(new UpdateSequenceParticipant(participant[12..], ParticipantId.Text, LabelText.Text, Actor.IsChecked == true));
        else if (MessageFields.Visibility == Visibility.Visible && MessageIdentity is { } message)
        {
            operations.Add(new UpdateSequenceMessage(message, Sender.SelectedItem as string ?? "", Recipient.SelectedItem as string ?? "", LabelText.Text, Arrow.SelectedItem as string ?? ""));
            if (Marker.SelectedItem as string != markerBaseline) operations.Add(new SetSequenceActivation(message, Marker.SelectedIndex == 1 ? "+" : Marker.SelectedIndex == 2 ? "-" : ""));
        }
        else if (NoteFields.Visibility == Visibility.Visible && draft.Selection is { } note)
            operations.Add(new UpdateSequenceNote(note, Placement.SelectedItem as string ?? "", First.SelectedItem as string ?? "", Second.SelectedItem as string, LabelText.Text));
        if (operations.Count > 0) await Execute(operations);
    }
    private async Task Execute(IReadOnlyList<SequenceEditOperation> operations)
    {
        if (!CanEdit) return; busy = true; UpdateActions();
        var existingStatements = draft.Model.Statements.Select(item => item.Identity).ToHashSet(StringComparer.Ordinal);
        try
        {
            var result = await draft.ExecuteBatchAsync(operations, lifetime.Token); if (closed) return;
            if (result.Success)
            {
                if (operations.FirstOrDefault() is UpdateSequenceParticipant rename) draft.Select("participant:" + rename.NewId);
                else if (operations.FirstOrDefault() is AddSequenceParticipant add) draft.Select("participant:" + add.Id);
                else if (operations.Any(item => item is AddSequenceMessage or AddSequenceNote or DuplicateSequenceMessage))
                {
                    var inserted = draft.Model.Statements.Where(item => !existingStatements.Contains(item.Identity) && item.Kind is SequenceStatementKind.Message or SequenceStatementKind.Note).ToArray();
                    if (inserted.Length == 1) draft.Select(inserted[0].Identity);
                }
                await RefreshAsync();
            }
            else Status.Text = result.Error;
        }
        finally { busy = false; if (!closed) UpdateActions(); }
    }
    private async void UpdateActivation(object sender, RoutedEventArgs e)
    { if (MessageIdentity is { } id && OtherMessageValuesUnchanged()) await Execute([new SetSequenceActivation(id, Marker.SelectedIndex == 1 ? "+" : Marker.SelectedIndex == 2 ? "-" : "")]); }
    private async void Delete(object sender, RoutedEventArgs e) { if (!PropertiesPending && DeleteOperation() is { } operation) await Execute([operation]); }
    private async void Duplicate(object sender, RoutedEventArgs e) { if (!PropertiesPending && draft.Selection is { } id) await Execute([new DuplicateSequenceMessage(id)]); }
    private async void Earlier(object sender, RoutedEventArgs e) { if (!PropertiesPending && MoveOperation(-1) is { } operation) await Execute([operation]); }
    private async void Later(object sender, RoutedEventArgs e) { if (!PropertiesPending && MoveOperation(1) is { } operation) await Execute([operation]); }
    private void ResetProperties(object sender, RoutedEventArgs e) { if (busy) return; updating = true; try { ShowProperties(); } finally { updating = false; } UpdateActions(); Status.Text = "Pending properties discarded; the validated source is unchanged."; }
    private async void Undo(object sender, RoutedEventArgs e) { if (busy || PropertiesPending || SourcePending) return; draft.Undo(); await RefreshAsync(); }
    private async void Redo(object sender, RoutedEventArgs e) { if (busy || PropertiesPending || SourcePending) return; draft.Redo(); await RefreshAsync(); }
    private void SourceChanged(object? sender, EventArgs e) { if (draft is not null && !updating && SourceEditor is not null) { UpdateActions(); Status.Text = "Source changes require validation before visual editing or Apply."; } }
    private async void ValidateSource(object sender, RoutedEventArgs e)
    {
        if (busy || PropertiesPending) return; busy = true; UpdateActions();
        try { var result = await draft.ValidateSourceAsync(SourceEditor.Text, lifetime.Token); if (closed) return; if (result.Success) await RefreshAsync(); else Status.Text = result.Error; }
        finally { busy = false; if (!closed) UpdateActions(); }
    }
    private async void Apply(object sender, RoutedEventArgs e)
    {
        if (!CanEdit || PropertiesPending) return; busy = true; UpdateActions();
        try
        {
            var result = await draft.ValidateAsync(lifetime.Token); if (!result.Success) { Status.Text = result.Error; return; }
            var error = await applyToDocument(draft.Source, lifetime.Token); if (closed) return;
            if (error is not null) { Status.Text = error; return; } discard = true; Close();
        }
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
        if ((draft.Source != draft.OriginalSource || SourcePending || PropertiesPending)
            && MessageBox.Show(this, "Discard the private Sequence draft and pending properties? The owning document has not changed.", "Discard Sequence draft", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) e.Cancel = true;
    }
    private void LocateSource(object sender, RoutedEventArgs e)
    {
        var statement = draft.Model.Participants.FirstOrDefault(item => "participant:" + item.Id == draft.Selection)?.Declaration
            ?? draft.Model.Activations.FirstOrDefault(item => item.Identity == draft.Selection)?.Statement
            ?? draft.Model.Statements.FirstOrDefault(item => item.Identity == draft.Selection);
        if (statement is null || SourcePending) return; SourcePanel.IsExpanded = true; SourceEditor.Select(statement.Span.Start, statement.Span.Length);
        SourceEditor.ScrollToLine(SourceEditor.Document.GetLineByOffset(statement.Span.Start).LineNumber); SourceEditor.Focus();
    }
    private void ShowOriginal(object sender, RoutedEventArgs e)
    {
        var editor = new ICSharpCode.AvalonEdit.TextEditor { Text = draft.OriginalSource, IsReadOnly = true, ShowLineNumbers = true, FontFamily = SourceEditor.FontFamily, FontSize = SourceEditor.FontSize,
            Background = (Brush)FindResource("WindowBrush"), Foreground = (Brush)FindResource("TextBrush") };
        new Window { Owner = this, Title = "Original portable Mermaid", Width = 700, Height = 450, Content = editor, WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
    }
    private void CopySource(object sender, RoutedEventArgs e)
    { try { Clipboard.SetText(SourceEditor.Text); Status.Text = "Mermaid draft source copied."; } catch (System.Runtime.InteropServices.ExternalException) { Status.Text = "The clipboard is unavailable. Select and copy the source directly."; } }
    private void ToggleSource(object sender, RoutedEventArgs e) => SourcePanel.IsExpanded = !SourcePanel.IsExpanded;
    private void Preview(object sender, RoutedEventArgs e)
    { if (CanonicalImage.Source is { } drawing) new Window { Owner = this, Title = "Rendered Sequence preview", Width = 850, Height = 600, Content = new ScrollViewer { Content = new Image { Source = drawing, Stretch = Stretch.None }, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto }, Background = (Brush)FindResource("WindowBrush") }.Show(); }
    private void ZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (Scale is not null) Scale.ScaleX = Scale.ScaleY = Zoom.Value / 100; }
    private void ZoomIn(object sender, RoutedEventArgs e) => Zoom.Value *= 1.15;
    private void ZoomOut(object sender, RoutedEventArgs e) => Zoom.Value /= 1.15;
    private void ActualSize(object sender, RoutedEventArgs e) => Zoom.Value = 100;
    private void Fit(object sender, RoutedEventArgs e)
    { if (Surface.Width > 0 && Surface.Height > 0) Zoom.Value = Math.Clamp(Math.Min((Viewport.ViewportWidth - 20) / Surface.Width, (Viewport.ViewportHeight - 20) / Surface.Height) * 100, .1, 100); }
    private void Wheel(object sender, MouseWheelEventArgs e) => Controls.MermaidWorkspace.ZoomAtPointer(Viewport, Zoom, e);
    private void PanStart(object sender, MouseButtonEventArgs e)
    { if (e.ChangedButton != MouseButton.Middle) return; pan = e.GetPosition(Viewport); panHorizontal = Viewport.HorizontalOffset; panVertical = Viewport.VerticalOffset; Viewport.CaptureMouse(); Viewport.Cursor = Cursors.Hand; e.Handled = true; }
    private void PanMove(object sender, MouseEventArgs e)
    { if (pan is not { } start) return; var point = e.GetPosition(Viewport); Viewport.ScrollToHorizontalOffset(panHorizontal + start.X - point.X); Viewport.ScrollToVerticalOffset(panVertical + start.Y - point.Y); }
    private void PanEnd(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Middle) EndPan(); }
    private void EndPan() { pan = null; Viewport.ReleaseMouseCapture(); Viewport.Cursor = Cursors.Arrow; }
    private void Shortcut(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { if (pan is not null) EndPan(); else Close(); e.Handled = true; return; }
        var editing = e.OriginalSource is TextBoxBase || SourceEditor.IsKeyboardFocusWithin || LabelText.IsKeyboardFocusWithin || ParticipantId.IsKeyboardFocusWithin;
        var modifiers = Keyboard.Modifiers;
        if (e.Key == Key.Enter && modifiers == ModifierKeys.Control) { Apply(sender, new()); e.Handled = true; return; }
        if (editing) return;
        if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.P) AddParticipant(sender, new());
        else if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.M) AddMessage(sender, new());
        else if (modifiers == ModifierKeys.Control && e.Key == Key.Z) Undo(sender, new());
        else if (modifiers == ModifierKeys.Control && e.Key == Key.Y) Redo(sender, new());
        else if (modifiers == ModifierKeys.Control && e.Key == Key.D) Duplicate(sender, new());
        else if (modifiers == ModifierKeys.Control && e.Key == Key.D0) ActualSize(sender, new());
        else if (modifiers == ModifierKeys.Alt && e.Key == Key.Up) Earlier(sender, new());
        else if (modifiers == ModifierKeys.Alt && e.Key == Key.Down) Later(sender, new());
        else if (modifiers == ModifierKeys.None && e.Key == Key.Delete) Delete(sender, new());
        else if (modifiers == ModifierKeys.None && e.Key == Key.F) Fit(sender, new());
        else if (modifiers == ModifierKeys.None && e.Key == Key.F2) { LabelText.Focus(); LabelText.SelectAll(); }
        else return;
        e.Handled = true;
    }
}
