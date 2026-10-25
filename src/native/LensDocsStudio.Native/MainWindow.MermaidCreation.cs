using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.State;
using LensDocsStudio.Native.Editor;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private MermaidCreationCoordinator? capturedMermaidTarget;
    private long mermaidOwnerGeneration;
    private bool openingMermaid, inspectingMermaidActions;

    private MermaidCreationCoordinator? CaptureMermaidTarget()
    {
        if (model.ActiveDocument is not { IsSaving: false } tab || Editor.IsReadOnly || Editor.Document != tab.EditorDocument)
        { model.Status = "Open an editable document before creating a Mermaid diagram."; return null; }
        try { return new(tab, Editor.SelectionStart, Editor.SelectionLength, Editor.CaretOffset, Editor.TextArea.Selection); }
        catch (ArgumentException error) { model.Status = error.Message; return null; }
    }
    private void CaptureMermaidPointerTarget(object sender, MouseButtonEventArgs e) => capturedMermaidTarget = CaptureMermaidTarget();
    private void CaptureMermaidKeyboardTarget(object sender, KeyboardFocusChangedEventArgs e) => capturedMermaidTarget = CaptureMermaidTarget();
    private void OpenMermaidCreation(object sender, RoutedEventArgs e) => StartMermaidCreation();
    private void NewMermaidCreation(object sender, RoutedEventArgs e) => StartMermaidCreation(newOnly: true);
    private void EditCurrentMermaid(object sender, RoutedEventArgs e) => StartMermaidCreation(editOnly: true);
    private void InsertMermaidFamily(MermaidDiagramFamily family) => StartMermaidCreation(family, newOnly: true);

    private async void OpenMermaidActions(object sender, RoutedEventArgs e)
    {
        if (openingMermaid || inspectingMermaidActions) return;
        if (sender is not Button button || button.ContextMenu is not { } menu) return;
        capturedMermaidTarget ??= CaptureMermaidTarget();
        var context = capturedMermaidTarget;
        var edit = menu.Items.OfType<MenuItem>().Single(item => (string?)item.Tag == "edit-current");
        edit.IsEnabled = false;
        menu.PlacementTarget = button; menu.Placement = PlacementMode.Bottom; menu.IsOpen = true;
        if (context is null) return;
        inspectingMermaidActions = true;
        try
        {
            var target = await context.ResolveAsync();
            if (target.Route == MermaidCreationRoute.ExistingDiagram && target.Block is { } block)
            {
                var inspection = await MermaidDiagramCatalogue.InspectAsync(block.Source, diagrams);
                edit.IsEnabled = inspection.Definition is not null && inspection.Error is null
                    && context.CurrentProblem(model.ActiveDocument, Editor.Document, Editor.IsReadOnly, model.Documents.Contains(context.Owner)) is null;
            }
        }
        catch (Exception error) when (error is ArgumentException or System.Text.RegularExpressions.RegexMatchTimeoutException)
        { model.Status = "This occurrence remains source-only. " + error.Message; }
        finally { inspectingMermaidActions = false; }
    }

    private async void StartMermaidCreation(MermaidDiagramFamily? requestedFamily = null, bool newOnly = false, bool editOnly = false)
    {
        if (openingMermaid) return;
        var context = capturedMermaidTarget ?? CaptureMermaidTarget(); capturedMermaidTarget = null;
        if (context is null) return;
        openingMermaid = true;
        var ownerGeneration = mermaidOwnerGeneration;
        var sourceSelection = context.SourceSelection ?? Editor.TextArea.Selection;
        var vertical = Editor.VerticalOffset; var horizontal = Editor.HorizontalOffset;
        string? Problem() => closing || closePending ? "The owning window is closing. No Mermaid source was changed."
            : ownerGeneration != mermaidOwnerGeneration ? "The active document changed while Mermaid was open. Reopen it from the intended document."
            : context.CurrentProblem(model.ActiveDocument, Editor.Document, Editor.IsReadOnly, model.Documents.Contains(context.Owner));
        void RestoreSourceFocus()
        {
            if (Problem() is not null) return;
            // Select() moves the caret to the selection end, which schedules AvalonEdit's
            // selection validation. Leave an unchanged selection alone and retain its direction.
            if (Editor.CaretOffset != context.CaretOffset) Editor.CaretOffset = context.CaretOffset;
            if (!Equals(Editor.TextArea.Selection, sourceSelection)) Editor.TextArea.Selection = sourceSelection;
            Editor.ScrollToVerticalOffset(vertical); Editor.ScrollToHorizontalOffset(horizontal); Editor.Focus();
        }
        void SourceFallback(string explanation)
        {
            model.Status = explanation;
            RestoreSourceFocus();
            // Source is already visible and focused; the toolbar also exposes an explicit Edit source action.
        }
        try
        {
            if (Problem() is { } initialProblem) { model.Status = initialProblem; return; }
            model.Status = "Inspecting the captured Mermaid target…";
            var target = await context.ResolveAsync();
            if (Problem() is { } changed) { model.Status = changed; return; }
            if (target.Route == MermaidCreationRoute.SourceOnly) { SourceFallback(target.Explanation); return; }
            if (target.Route == MermaidCreationRoute.ExistingDiagram && target.Block is { } block)
            {
                if (newOnly && !context.Standalone) { SourceFallback("Choose a position outside the existing Mermaid fence to create another diagram. Its source is unchanged."); return; }
                var (definition, validation) = await MermaidDiagramCatalogue.InspectAsync(block.Source, diagrams);
                if (Problem() is { } stale) { model.Status = stale; return; }
                if (definition is null || validation is not null) { SourceFallback("Visual editing is protected for this source. " + validation); return; }
                OpenVisualDraft(context.Owner, context.CreateSession(target), block.Source, RestoreSourceFocus, Problem, RestoreSourceFocus);
                return;
            }
            if (editOnly) { SourceFallback("Place the caret inside exactly one supported Mermaid source body, away from its fences."); return; }
            MermaidDiagramDefinition chosen; MermaidStarterVariant variant;
            if (requestedFamily is { } family) { chosen = MermaidDiagramCatalogue.Get(family); variant = chosen.Variants[0]; }
            else
            {
                var picker = new MermaidDiagramPickerWindow(diagrams, model.Theme, WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, target.Explanation) { Owner = this };
                if (picker.ShowDialog() != true) { RestoreSourceFocus(); return; }
                if (Problem() is { } pickerProblem) { model.Status = pickerProblem; return; }
                if (picker.InsertAsCode)
                {
                    RestoreSourceFocus();
                    if (context.Standalone)
                    {
                        context.CreateSession(target).Apply(MermaidDiagramCatalogue.Get(MermaidDiagramFamily.Flowchart).Variants[0].Source, true);
                        model.Status = "Raw Mermaid source inserted. Save the document to persist it."; return;
                    }
                    // Explicit source insertion preserves selected text by placing the legacy action after it.
                    Editor.Select(target.InsertionOffset, 0);
                    Format(new Button { Tag = "mermaid" }, new RoutedEventArgs()); return;
                }
                chosen = picker.SelectedDefinition!; variant = picker.SelectedVariant!;
            }
            if (!chosen.Variants.Contains(variant)) throw new ArgumentException("That starting point does not belong to the selected family.");
            var error = await chosen.ValidateAsync(variant.Source, diagrams, CancellationToken.None);
            if (error is not null) { SourceFallback("The starting point could not be validated. " + error); return; }
            if (Problem() is { } finalProblem) { model.Status = finalProblem; return; }
            var newline = context.Source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var source = variant.Source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", newline, StringComparison.Ordinal);
            OpenVisualDraft(context.Owner, context.CreateSession(target), source, null, Problem, RestoreSourceFocus);
        }
        catch (Exception error) when (error is ArgumentException or System.Text.RegularExpressions.RegexMatchTimeoutException)
        { SourceFallback("Mermaid could not safely use this target. " + error.Message); }
        finally { openingMermaid = false; }
    }
    private void FocusMermaidSource(object sender, RoutedEventArgs e) { capturedMermaidTarget = null; Editor.Focus(); }
    private void InsertMermaidCode(object sender, RoutedEventArgs e)
    {
        capturedMermaidTarget = null;
        Format(sender, e);
    }
}
