using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LensDocsStudio.Core.Arrangement;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public sealed record ArrangementDocumentContext(string Root, string RelativePath, string Source, bool Standalone, int Offset);

/// <summary>A source-owned presentation window. It has no source editor or source Apply command.</summary>
public partial class DiagramArrangementWindow : Window
{
    private readonly string source;
    private readonly DiagramService diagrams;
    private bool dark;
    private readonly string theme;
    private readonly Func<ArrangementDocumentContext?> ownerContext;
    private readonly CancellationTokenSource lifetime = new();
    private readonly NativeSvgService svg = new();
    private ArrangementEngine engine = null!;
    private ArrangementSession session = null!;
    private ArrangementSnapshot? displayed, proposed, checkpoint;
    private DiagramInspection? inspection;
    private ArrangementStoreRead? stored;
    private Guid? savedId;
    private string? selected;
    private bool busy, closed, updating;
    private string? pendingAppearance;
    private Point? pointer;
    private Point pointerSurface;
    private double horizontal, vertical;
    private bool dragging, panning;
    private MouseButton pointerButton;
    private string? movingId;
    private ArrangementPosition movingFrom;
    private sealed record Choice(string Identity, string Description);
    private sealed record Candidate(ArrangementSnapshot Snapshot, string Description);
    private sealed record SavedChoice(SavedArrangement Layout, string Description);

    public DiagramArrangementWindow(string source, DiagramService diagrams, Func<ArrangementDocumentContext?> ownerContext, bool dark = false, string theme = "Auto")
    {
        this.source = source; this.diagrams = diagrams; this.ownerContext = ownerContext; this.dark = dark; this.theme = theme;
        InitializeComponent(); WindowStateService.FitInitialWindow(this);
        Loaded += async (_, _) => await Run(async () =>
        {
            var render = await Task.Run(() => diagrams.Render(source, dark, theme, lifetime.Token), lifetime.Token);
            engine = await Task.Run(() => new ArrangementEngine(render), lifetime.Token); session = new(engine.Canonical); checkpoint = session.Current;
            await Present(session.Current); await LoadSaved(false, true); Fit(this, new());
        });
        NavigateMode.Checked += (_, _) => CancelPointer(); ArrangeMode.Checked += (_, _) => CancelPointer();
        Deactivated += (_, _) => CancelPointer();
        Closing += (_, e) =>
        {
            if (session?.IsDirty == true && MessageBox.Show(this, "Discard the unsaved arrangement? Mermaid source is unchanged.", "Unsaved arrangement", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) e.Cancel = true;
        };
        Closed += (_, _) => { closed = true; CancelPointer(); lifetime.Cancel(); };
    }
    private async Task Run(Func<Task> action)
    {
        if (busy || closed) return;
        busy = true; Commands.IsEnabled = false; CancelPointer(); UpdateActions();
        try { await action(); }
        catch (OperationCanceledException) { if (!closed) Status.Text = "Arrangement work was cancelled or reached its bounded deadline. The accepted layout is retained."; }
        catch (Exception error) when (error is not OutOfMemoryException) { if (!closed) Status.Text = error.Message; }
        finally { busy = false; if (!closed) { Commands.IsEnabled = true; UpdateActions(); if (pendingAppearance is not null) RefreshAppearance(); } }
    }
    public void ApplyTheme(string appearance) { pendingAppearance = appearance; if (!busy && session is not null) RefreshAppearance(); }
    private async void RefreshAppearance()
    {
        if (busy || session is null || pendingAppearance is not { } appearance) return;
        pendingAppearance = null;
        await Run(async () =>
        {
            var canonicalView = ReferenceEquals(displayed, engine.Canonical); dark = ThemeService.IsDark(appearance);
            var next = await Task.Run(() => new ArrangementEngine(diagrams.Render(source, dark, theme, lifetime.Token)), lifetime.Token);
            var replacement = await Task.Run(() => next.Repaint(session.Current, lifetime.Token), lifetime.Token); CheckOwner();
            engine = next; session.UpdatePresentation(replacement, engine.Canonical); proposed = null; CandidatePanel.Visibility = Visibility.Collapsed;
            await Present(canonicalView ? engine.Canonical : session.Current);
        });
    }
    private async Task PresentCurrent()
    {
        var replacement = await Task.Run(() => engine.Repaint(session.Current, lifetime.Token), lifetime.Token);
        session.UpdatePresentation(replacement, engine.Canonical); await Present(session.Current);
    }
    private void CheckOwner() { _ = ownerContext(); lifetime.Token.ThrowIfCancellationRequested(); }
    private async Task Present(ArrangementSnapshot snapshot)
    {
        var view = await StaWorker.RunAsync(() => DiagramInspection.CreateArranged(snapshot, svg, lifetime.Token), lifetime.Token)
            ?? throw new InvalidDataException("The arranged drawing and inspection geometry could not be verified. No arranged export is available.");
        CheckOwner();
        inspection = view; displayed = snapshot; Drawing.Source = view.Drawing; Overlay.Inspection = view;
        updating = true;
        try
        {
            Elements.ItemsSource = view.Entities.Select(e => new Choice(e.Identity, "Node · " + e.Description + (snapshot.State.Positions.GetValueOrDefault(NodeId(e.Identity) ?? "").Pinned ? " · pinned" : "")))
                .Concat(view.Connections.Select(e => new Choice(e.Identity, "Connection · " + e.Description))).ToArray();
            Elements.SelectedValue = selected;
        }
        finally { updating = false; }
        Overlay.Update(selected, null, Zoom.Value / 100); UpdateActions();
    }
    private void UpdateActions()
    {
        if (session is null) return;
        UndoButton.IsEnabled = !busy && session.CanUndo; RedoButton.IsEnabled = !busy && session.CanRedo;
        PinButton.IsEnabled = !busy && NodeId(selected) is not null && proposed is null;
        var arranged = displayed is not null && ReferenceEquals(displayed, session.Current) && displayed.State.Strategy != ArrangementStrategy.Canonical && proposed is null;
        ExportSvgButton.IsEnabled = ExportPngButton.IsEnabled = !busy && arranged;
        StateLabel.Text = (session.IsDirty ? "Unsaved arrangement" : "Layout unchanged or saved") + " · "
            + (proposed is not null ? "Candidate preview — accept explicitly" : ReferenceEquals(displayed, engine.Canonical) ? "Canonical view" : "Arranged view")
            + (busy ? " · Working…" : "");
    }
    private static string? NodeId(string? identity) => identity?.StartsWith("entity:", StringComparison.Ordinal) == true ? identity[7..]
        : identity?.StartsWith("node:", StringComparison.Ordinal) == true ? identity[5..] : null;
    private void SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updating) return; selected = Elements.SelectedValue as string; Overlay.Update(selected, null, Zoom.Value / 100); UpdateActions();
    }
    private void Select(string? identity)
    { selected = identity; updating = true; try { Elements.SelectedValue = identity; } finally { updating = false; } Overlay.Update(identity, null, Zoom.Value / 100); UpdateActions(); }
    private async Task ApplyState(ArrangementState state)
    {
        var generation = session.Generation;
        var candidate = await Task.Run(() => engine.Arrange(state, lifetime.Token), lifetime.Token);
        CheckOwner(); await Present(candidate);
        if (!session.Accept(candidate, generation)) { await Present(session.Current); throw new InvalidDataException("A newer arrangement superseded this result."); }
        proposed = null; Status.Text = "Arrangement accepted. Mermaid source is unchanged. Save layout to make it portable.";
        UpdateActions();
    }
    private async void TogglePin(object sender, RoutedEventArgs e) { if (NodeId(selected) is { } id) await Run(() => ApplyState(session.Current.State.TogglePin(id))); }
    private async void UndoLayout(object sender, RoutedEventArgs e) => await Run(async () => { proposed = null; CandidatePanel.Visibility = Visibility.Collapsed; session.Undo(); await PresentCurrent(); });
    private async void RedoLayout(object sender, RoutedEventArgs e) => await Run(async () => { proposed = null; CandidatePanel.Visibility = Visibility.Collapsed; session.Redo(); await PresentCurrent(); });
    private async void ResetLayout(object sender, RoutedEventArgs e) => await Run(async () => { proposed = null; CandidatePanel.Visibility = Visibility.Collapsed; session.Reset(); await Present(session.Current); });
    private async void ViewCanonical(object sender, RoutedEventArgs e) => await Run(async () => { proposed = null; CandidatePanel.Visibility = Visibility.Collapsed; await Present(engine.Canonical); });
    private async void ViewArranged(object sender, RoutedEventArgs e) => await Run(async () => { proposed = null; CandidatePanel.Visibility = Visibility.Collapsed; await Present(session.Current); });
    private async void Revert(object sender, RoutedEventArgs e) => await Run(async () => { proposed = null; CandidatePanel.Visibility = Visibility.Collapsed; session.Accept(checkpoint!, session.Generation); await PresentCurrent(); Status.Text = "Reverted to the arrangement from the last load or save."; });
    private async void GenerateCandidates(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        Status.Text = "Comparing bounded deterministic candidates…";
        var candidates = await Task.Run(() => engine.Candidates(session.Current.State, lifetime.Token), lifetime.Token); CheckOwner();
        CandidateChoices.ItemsSource = candidates.Select(c => new Candidate(c, $"{(c.State.Strategy == ArrangementStrategy.FewerCrossings ? "Fewer crossings" : "Compact")} · crossings {c.Quality.Crossings} · area {c.Quality.Area:0} · bends {c.Quality.Bends}")).ToArray();
        CandidatePanel.Visibility = candidates.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        Status.Text = candidates.Count > 0 ? $"Current: {session.Current.Quality.Crossings} crossings; area {session.Current.Quality.Area:0}. Select a candidate to preview; Accept candidate changes the layout."
            : "No safe improvement was found within the bounded search. Pins are retained. Automatic movement of compound subgraphs is currently unavailable.";
    });
    private async void PreviewCandidate(object sender, SelectionChangedEventArgs e)
    { if (CandidateChoices.SelectedItem is Candidate candidate) await Run(async () => { proposed = candidate.Snapshot; await Present(proposed); }); }
    private async void AcceptCandidate(object sender, RoutedEventArgs e)
    { if (proposed is { } candidate) await Run(async () => { CheckOwner(); session.Accept(candidate, session.Generation); proposed = null; CandidatePanel.Visibility = Visibility.Collapsed; await Present(session.Current); }); }
    private async void CancelCandidate(object sender, RoutedEventArgs e) => await Run(async () => { proposed = null; CandidatePanel.Visibility = Visibility.Collapsed; await Present(session.Current); });

    private void PointerDown(object sender, MouseButtonEventArgs e)
    {
        if (busy || inspection is null || e.ChangedButton is not (MouseButton.Left or MouseButton.Middle)) return;
        for (var element = e.OriginalSource as DependencyObject; element is not null && element != Viewport; element = VisualTreeHelper.GetParent(element))
            if (element is System.Windows.Controls.Primitives.ScrollBar) return;
        Viewport.Focus(); pointer = e.GetPosition(Viewport); pointerSurface = e.GetPosition(Drawing); pointerButton = e.ChangedButton;
        horizontal = Viewport.HorizontalOffset; vertical = Viewport.VerticalOffset;
        var hit = inspection.Hit(pointerSurface, Zoom.Value / 100);
        panning = pointerButton == MouseButton.Middle || Keyboard.IsKeyDown(Key.Space) || ArrangeMode.IsChecked != true || hit is null;
        movingId = !panning && proposed is null && ReferenceEquals(displayed, session.Current) ? NodeId(hit) : null;
        if (movingId is not null && session.Current.State.Positions.TryGetValue(movingId, out var position)) { movingFrom = position; Select(hit); }
        else movingId = null;
        if (!Viewport.CaptureMouse()) CancelPointer(); e.Handled = true;
    }
    private void PointerMove(object sender, MouseEventArgs e)
    {
        if (pointer is not { } start) { if (!busy) Overlay.Update(selected, inspection?.Hit(e.GetPosition(Drawing), Zoom.Value / 100), Zoom.Value / 100); return; }
        if (pointerButton == MouseButton.Left && e.LeftButton != MouseButtonState.Pressed || pointerButton == MouseButton.Middle && e.MiddleButton != MouseButtonState.Pressed) { CancelPointer(); return; }
        var current = e.GetPosition(Viewport); var delta = current - start;
        if (!dragging && !DiagramViewportMath.BeginsPan(delta.X, delta.Y)) return; dragging = true;
        if (movingId is { } id)
        {
            var bounds = inspection!.Entities.Single(item => NodeId(item.Identity) == id).Shape.Bounds;
            DragGhost.Width = bounds.Width; DragGhost.Height = bounds.Height;
            Canvas.SetLeft(DragGhost, bounds.X + delta.X / Scale.ScaleX); Canvas.SetTop(DragGhost, bounds.Y + delta.Y / Scale.ScaleY); DragGhost.Visibility = Visibility.Visible;
        }
        else if (panning) { Viewport.ScrollToHorizontalOffset(horizontal - delta.X); Viewport.ScrollToVerticalOffset(vertical - delta.Y); }
        e.Handled = true;
    }
    private async void PointerUp(object sender, MouseButtonEventArgs e)
    {
        if (pointer is not { } start || e.ChangedButton != pointerButton) return;
        var id = movingId; var moved = dragging; var delta = e.GetPosition(Viewport) - start; var origin = movingFrom;
        var hit = inspection?.Hit(e.GetPosition(Drawing), Zoom.Value / 100); CancelPointer(); e.Handled = true;
        if (!moved) { Select(hit); return; }
        if (id is not null) await Run(() => ApplyState(session.Current.State.Move(id, Math.Max(32, origin.X + delta.X / Scale.ScaleX), Math.Max(32, origin.Y + delta.Y / Scale.ScaleY))));
    }
    private void CancelPointer()
    { pointer = null; movingId = null; dragging = panning = false; if (DragGhost is not null) DragGhost.Visibility = Visibility.Collapsed; if (Viewport?.IsMouseCaptured == true) Viewport.ReleaseMouseCapture(); }
    private void CaptureLost(object sender, MouseEventArgs e) => CancelPointer();
    private void PointerLeave(object sender, MouseEventArgs e) { if (pointer is null) Overlay.Update(selected, null, Zoom.Value / 100); }
    private async void KeyPressed(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        { if (pointer is not null) CancelPointer(); else if (proposed is not null) CancelCandidate(this, new()); else Select(null); e.Handled = true; return; }
        if (Viewport.IsKeyboardFocused && !busy && ArrangeMode.IsChecked == true && proposed is null && ReferenceEquals(displayed, session.Current) && NodeId(selected) is { } id && e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 1 : 10; var position = session.Current.State.Positions[id];
            var x = position.X + (e.Key == Key.Left ? -step : e.Key == Key.Right ? step : 0);
            var y = position.Y + (e.Key == Key.Up ? -step : e.Key == Key.Down ? step : 0);
            e.Handled = true; await Run(() => ApplyState(session.Current.State.Move(id, Math.Max(32, x), Math.Max(32, y))));
        }
    }
    private void ZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (Scale is null) return; CancelPointer(); Scale.ScaleX = Scale.ScaleY = e.NewValue / 100; Overlay.Update(selected, null, Scale.ScaleX); }
    private void Fit(object sender, RoutedEventArgs e)
    { if (Drawing.Source is null) return; var fit = Math.Min((Viewport.ViewportWidth - 48) / Drawing.Source.Width, (Viewport.ViewportHeight - 48) / Drawing.Source.Height) * 100; if (fit <= 0) return; Zoom.Minimum = Math.Min(1, fit); Zoom.Value = Math.Clamp(fit, Zoom.Minimum, 100); }
    private void Wheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
        var pointerPosition = e.GetPosition(Viewport); var before = Zoom.Value; var x = Viewport.HorizontalOffset; var y = Viewport.VerticalOffset;
        Zoom.Value = Math.Clamp(before * (e.Delta > 0 ? 1.1 : 1 / 1.1), Zoom.Minimum, Zoom.Maximum);
        Viewport.UpdateLayout();
        Viewport.ScrollToHorizontalOffset(DiagramViewportMath.ZoomOffset(x, pointerPosition.X, Viewport.Padding.Left, before, Zoom.Value));
        Viewport.ScrollToVerticalOffset(DiagramViewportMath.ZoomOffset(y, pointerPosition.Y, Viewport.Padding.Top, before, Zoom.Value));
        e.Handled = true;
    }

    private (ArrangementStore Store, ArrangementDocumentContext Context, ArrangementBlockIdentity Identity) Storage()
    {
        var context = ownerContext() ?? throw new IOException("Save the owning document in a workspace before saving a portable layout. The arrangement remains in this window.");
        var parsed = new MarkdownService().Parse(context.Source, standaloneMermaid: context.Standalone);
        var block = MermaidBlockSource.Find(parsed, context.Standalone).SingleOrDefault(b => context.Offset >= b.Body.Start && context.Offset < b.Body.End);
        if (block is null || block.Source != source) throw new IOException("The owning diagram changed. Reopen it before loading or saving an arrangement.");
        return (new(new(context.Root)), context, ArrangementIdentity.Capture(parsed, block));
    }
    private async Task LoadSaved(bool backup, bool automatic)
    {
        if (ownerContext() is null) { Status.Text = "This arrangement is session-only until the owning document is saved in a workspace."; return; }
        var (store, context, identity) = Storage();
        var read = backup ? await store.ReadBackupAsync(lifetime.Token) : await store.ReadAsync(lifetime.Token); CheckOwner(); stored = read;
        var records = read.File.Layouts.Where(l => l.Document.Equals(context.RelativePath, StringComparison.OrdinalIgnoreCase)).ToArray();
        SavedChoices.ItemsSource = records.Select(l => new SavedChoice(l, l.Block.Family + " · " + l.Id.ToString("N")[..8] + (l.Block.SourceFingerprint == identity.SourceFingerprint ? " · matching source" : " · stale — explicit re-association required"))).ToArray();
        var parsed = new MarkdownService().Parse(context.Source, standaloneMermaid: context.Standalone);
        var exact = records.Where(l => l.Block.SourceFingerprint == identity.SourceFingerprint && l.Block.Family == identity.Family && ArrangementIdentity.Match(parsed, l.Block, context.Standalone).Kind == ArrangementMatchKind.Exact).ToArray();
        if (automatic && exact.Length == 1)
        {
            var record = exact[0]; await ApplyState(new(record.Positions, record.Strategy, record.LayoutVariant)); savedId = record.Id; session.MarkSaved(); checkpoint = session.Current;
            Status.Text = "Saved workspace arrangement restored. Mermaid source and canonical exports are unchanged.";
        }
        else Status.Text = backup ? "Backup loaded for review. Save layout explicitly to recover it; unreadable current bytes will be retained."
            : records.Length > 0 ? "Saved layouts are available. Ambiguous or stale associations require explicit selection and re-association."
            : "Navigate pans and inspects. Select Arrange to move nodes; arrow keys move the selected node, Shift uses one-pixel steps. No layout file has been created.";
    }
    private async void ReloadSaved(object sender, RoutedEventArgs e) => await Run(() => LoadSaved(false, false));
    private async void LoadBackup(object sender, RoutedEventArgs e) => await Run(() => LoadSaved(true, false));
    private async void ApplySaved(object sender, RoutedEventArgs e)
    {
        if (SavedChoices.SelectedItem is not SavedChoice choice) return;
        await Run(async () =>
        {
            if (Storage().Identity.Family != choice.Layout.Block.Family) throw new InvalidDataException("The saved layout belongs to another diagram family.");
            await ApplyState(new(choice.Layout.Positions, ArrangementStrategy.Manual)); savedId = choice.Layout.Id;
            Status.Text = "Saved positions explicitly re-associated with this diagram. Review them, then Save layout to persist the association.";
        });
    }
    private async void SaveLayout(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        var (store, context, identity) = Storage();
        if (stored is null) throw new IOException("Reload workspace layouts before saving so external changes can be detected.");
        var record = new SavedArrangement(savedId ?? Guid.NewGuid(), context.RelativePath, identity, session.Current.State.Strategy, session.Current.State.Positions, session.Current.State.LayoutVariant);
        var file = new ArrangementFile(1, stored.File.Layouts.Where(l => l.Id != record.Id).Append(record).ToArray());
        stored = await store.SaveAsync(file, stored, lifetime.Token); CheckOwner(); savedId = record.Id; session.MarkSaved(); checkpoint = session.Current;
        Status.Text = "Layout saved to .lens-docs/diagram-layouts.json. Copy the workspace folder to share this arrangement.";
    });
    private async void ExportArranged(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        if (displayed is null || !ReferenceEquals(displayed, session.Current) || proposed is not null || displayed.State.Strategy == ArrangementStrategy.Canonical || inspection is null)
            throw new IOException("Display an accepted arranged view before exporting it.");
        CheckOwner(); var format = (string)((Button)sender).Tag; var snapshot = displayed; var drawing = inspection.Drawing;
        var dialog = new Microsoft.Win32.SaveFileDialog { Title = "Export arranged " + format, FileName = "arranged-diagram." + format.ToLowerInvariant(), Filter = format + " image|*." + format.ToLowerInvariant(), AddExtension = true, OverwritePrompt = true };
        if (dialog.ShowDialog(this) != true) return;
        CheckOwner(); WorkspacePath.RejectReparsePoints(dialog.FileName);
        var bytes = format == "SVG" ? System.Text.Encoding.UTF8.GetBytes(snapshot.Render.Svg!) : await StaWorker.RunAsync(() => NativeSvgService.ToExportPng(drawing), lifetime.Token);
        await File.WriteAllBytesAsync(dialog.FileName, bytes, lifetime.Token); Status.Text = "Verified arranged " + format + " exported.";
    });
}
