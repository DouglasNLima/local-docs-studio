using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.SourceAuthoring;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

/// <summary>Shared presentation/ownership shell for the three source-based editors. Family semantics stay in their adapters.</summary>
public partial class SourceVisualEditorWindow : Window
{
    protected record Choice(string Identity, string Description) { public override string ToString() => Description; }
    protected ISourceDraft Draft = null!;
    protected readonly DiagramService Diagrams;
    protected readonly CancellationTokenSource Lifetime = new();
    private readonly Func<string, CancellationToken, Task<string?>> apply;
    private readonly Action? editSource;
    private readonly NativeSvgService svg = new();
    private readonly string family, diagramTheme;
    private readonly MermaidSourceHighlighting highlighting;
    private readonly SemaphoreSlim renderGate = new(1, 1);
    private CancellationTokenSource? pending;
    private DrawingImage? drawing;
    private readonly Dictionary<string, FrameworkElement> hits = [];
    private bool busy = true, updating, mapped, closed, discard, dark;
    private int generation;
    private string baseline = "";
    private Point? pan;
    private double panX, panY;
    protected bool PendingProperties => baseline != Values;
    protected bool PendingSource => Draft is not null && SourceEditor.Text != Draft.Source;
    protected bool Ready => Draft is not null && !busy && !PendingSource;
    protected virtual string Values => "";
    protected virtual IEnumerable<Choice> Choices() => [];
    protected virtual void ShowProperties() { }
    protected virtual Task<SourceAuthoringResult> UpdateProperties() => Task.FromResult(new SourceAuthoringResult(false, "Select an editable occurrence."));
    protected virtual Task<SourceAuthoringResult> DeleteSelection() => Task.FromResult(new SourceAuthoringResult(false, "Select a removable occurrence."));
    protected virtual Task<SourceAuthoringResult> MoveSelection(int direction) => Task.FromResult(new SourceAuthoringResult(false, "Select a movable occurrence."));
    protected virtual IReadOnlyList<NativeSourceHit> Correspondence(string source, DiagramResult render) => [];
    protected SourceVisualEditorWindow(string family, DiagramService diagrams, Func<string, CancellationToken, Task<string?>> apply,
        bool dark, string diagramTheme, Action? editSource, FontFamily? sourceFont, double sourceFontSize)
    {
        this.family = family; Diagrams = diagrams; this.apply = apply; this.dark = dark; this.diagramTheme = diagramTheme; this.editSource = editSource;
        highlighting = new(dark); InitializeComponent(); WindowStateService.FitInitialWindow(this);
        Title = "Mermaid Visual Editor / " + family; FamilyHeading.Text = family.ToUpperInvariant();
        AutomationProperties.SetAutomationId(this, family.Replace(" ", "") + "VisualEditor");
        foreach (var control in new FrameworkElement[] { UndoButton, RedoButton, DeleteButton, MoveUpButton, MoveDownButton, Zoom, FitButton, Status, EditSourceButton, CancelButton, ApplyButton, Elements, Viewport, SourcePanel, ValidateButton, OriginalButton, SourceEditor, Summary, InspectorState, InspectorViewport, Capabilities, LocateButton, UpdateButton, ResetButton })
            AutomationProperties.SetAutomationId(control, family.Replace(" ", "") + control.Name.Replace("Button", "", StringComparison.Ordinal));
        SourceEditor.FontFamily = sourceFont ?? new FontFamily("Cascadia Code, Consolas"); SourceEditor.FontSize = sourceFontSize;
        SourceEditor.Options.EnableHyperlinks = SourceEditor.Options.EnableEmailHyperlinks = false; SourceEditor.TextArea.TextView.LineTransformers.Add(highlighting);
        EditSourceButton.IsEnabled = editSource is not null;
        Closing += ConfirmClose;
        Closed += async (_, _) => { closed = true; generation++; pending?.Cancel(); Lifetime.Cancel(); EndPan(); await renderGate.WaitAsync(); renderGate.Release(); renderGate.Dispose(); Lifetime.Dispose(); };
        Deactivated += (_, _) => EndPan();
    }
    protected void Start(ISourceDraft draft)
    {
        Draft = draft;
        Loaded += async (_, _) => { busy = false; await Refresh(true); };
    }
    protected TextBox Field(string caption, string id)
    {
        var field = new TextBox { Style = (Style)FindResource("Mermaid.PropertyInput") }; Identify(field, id, caption);
        InspectorFields.Children.Add(new Label { Content = caption, Target = field, Style = (Style)FindResource("Form.Label") }); InspectorFields.Children.Add(field);
        field.TextChanged += (_, _) => { if (!updating) UpdateActions(); }; return field;
    }
    protected Button ActionButton(string caption, string id, Func<Task<SourceAuthoringResult>> action, Panel? panel = null)
    {
        var button = new Button { Content = caption, ToolTip = caption, Style = (Style)FindResource("Mermaid.Action") }; Identify(button, id, caption);
        button.Click += async (_, _) => { if (Ready && !PendingProperties) await Execute(action); else Status.Text = "Update or reset pending properties and validate source first."; };
        (panel ?? Palette).Children.Add(button); return button;
    }
    protected void Identify(DependencyObject control, string id, string caption)
    { AutomationProperties.SetAutomationId(control, family.Replace(" ", "") + id); AutomationProperties.SetName(control, caption); }
    protected void FieldsChanged() { if (!updating) UpdateActions(); }
    protected void Describe(string summary, string reason) { Summary.Text = summary; Capabilities.Text = reason; }
    protected void PropertiesLoaded() => baseline = Values;
    protected async Task ValidateNative(SourceText text, CancellationToken token)
    {
        var version = generation; var appearance = dark; await renderGate.WaitAsync(token);
        try
        {
            var render = await Task.Run(() => Diagrams.Render(text.Source, appearance, diagramTheme, token), token);
            await StaWorker.RunAsync(() => { var prepared = svg.Prepare(render.Svg ?? throw new ArgumentException(render.Error)); return svg.RenderCanonical(render, prepared, cancellationToken: token); }, token);
            token.ThrowIfCancellationRequested(); if (closed || version != generation) throw new ArgumentException("The appearance changed during validation. Retry; the draft is unchanged.");
        }
        finally { renderGate.Release(); }
    }
    public async void ApplyTheme(string theme)
    { dark = ThemeService.IsDark(theme); highlighting.Dark = dark; SourceEditor.TextArea.TextView.Redraw(); if (!closed && Draft is not null) await Render(false); }
    private async Task Refresh(bool fit = false)
    {
        updating = true;
        try
        {
            if (SourceEditor.Text != Draft.Source) SourceEditor.Text = Draft.Source;
            Elements.ItemsSource = Choices().ToArray(); RestoreSelection(); ShowProperties(); PropertiesLoaded();
        }
        finally { updating = false; }
        await Render(fit); UpdateActions();
    }
    private void RestoreSelection() => Elements.SelectedItem = Elements.Items.OfType<Choice>().FirstOrDefault(item => item.Identity == Draft.Selection);
    private void SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updating || Draft is null || Elements.SelectedItem is not Choice choice) return;
        Select(choice.Identity);
    }
    private void Select(string identity)
    {
        if (!Ready || PendingProperties) { Status.Text = "Update or reset pending properties and validate source before changing selection."; Controls.MermaidWorkspace.DeferNavigation(Elements, () => { updating = true; RestoreSelection(); updating = false; }); return; }
        Draft.Select(identity); updating = true;
        try { RestoreSelection(); Controls.MermaidWorkspace.Inspect(InspectorViewport, identity); ShowProperties(); PropertiesLoaded(); }
        finally { updating = false; }
        Highlight(); UpdateActions();
    }
    private async Task Render(bool fit)
    {
        pending?.Cancel(); var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Lifetime.Token); pending = cancellation;
        var version = ++generation; var source = Draft.Source; var appearance = dark; mapped = false; UpdateActions(); var leased = false;
        try
        {
            await renderGate.WaitAsync(cancellation.Token); leased = true;
            var result = await Task.Run(() => Diagrams.Render(source, appearance, diagramTheme, cancellation.Token), cancellation.Token);
            var presentation = await StaWorker.RunAsync(() =>
            {
                var prepared = svg.Prepare(result.Svg ?? throw new ArgumentException(result.Error));
                return (Drawing: svg.RenderCanonical(result, prepared, cancellationToken: cancellation.Token), Hits: Correspondence(source, result));
            }, cancellation.Token);
            if (closed || version != generation || source != Draft.Source) return;
            drawing = presentation.Drawing; Surface.Children.Clear(); hits.Clear(); Surface.Width = drawing.Width; Surface.Height = drawing.Height;
            Surface.Children.Add(new Image { Source = drawing, Stretch = Stretch.None, IsHitTestVisible = false });
            foreach (var hit in presentation.Hits)
            {
                if (hit.Shape is { } geometry)
                {
                    var shape = new System.Windows.Shapes.Path { Data = geometry, Fill = Brushes.Transparent, Stroke = Brushes.Transparent, StrokeThickness = 2, ToolTip = hit.Description };
                    Identify(shape, "Preview" + hit.Identity, hit.Description); shape.MouseLeftButtonDown += (_, args) => { Select(hit.Identity); args.Handled = true; };
                    Surface.Children.Add(shape); hits[hit.Identity] = shape; continue;
                }
                var button = new Button { Width = hit.Bounds.Width, Height = hit.Bounds.Height, Background = Brushes.Transparent, BorderThickness = new Thickness(1), Style = (Style)FindResource("Mermaid.CanonicalElement"), ToolTip = hit.Description };
                Identify(button, "Preview" + hit.Identity, hit.Description); button.Click += (_, _) => Select(hit.Identity);
                Canvas.SetLeft(button, hit.Bounds.X); Canvas.SetTop(button, hit.Bounds.Y); Surface.Children.Add(button); hits[hit.Identity] = button;
            }
            renderGate.Release(); leased = false;
            var validation = await Draft.ValidateAsync(cancellation.Token);
            if (closed || version != generation) return;
            mapped = validation.Success; Status.Text = validation.Error ?? "Ready. Source occurrences and canonical preview validated.";
            AutomationProperties.SetItemStatus(this, SystemParameters.HighContrast ? "High Contrast" : appearance ? "Dark" : "Light");
            Highlight();
            if (fit) { Surface.UpdateLayout(); Fit(this, new()); if (Zoom.Value < Controls.MermaidWorkspace.MinimumInitialFitPercent) Zoom.Value = 100; }
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is not OutOfMemoryException) { if (!closed && version == generation) { Status.Text = error.Message; Surface.Children.Clear(); hits.Clear(); drawing = null; } }
        finally { if (leased) renderGate.Release(); if (ReferenceEquals(pending, cancellation)) pending = null; cancellation.Dispose(); if (!closed && version == generation) UpdateActions(); }
    }
    private void Highlight()
    {
        foreach (var (key, element) in hits)
        {
            var colour = key == Draft.Selection ? (Brush)FindResource("AccentBrush") : Brushes.Transparent;
            if (element is Button button) button.BorderBrush = colour; else if (element is System.Windows.Shapes.Path shape) shape.Stroke = colour;
        }
        if (Draft.Selection is { } selected && hits.TryGetValue(selected, out var target)) Controls.MermaidWorkspace.Reveal(target, Viewport);
    }
    private void UpdateActions()
    {
        if (Draft is null) return; var clean = !PendingProperties && !PendingSource;
        UndoButton.IsEnabled = !busy && clean && Draft.CanUndo; RedoButton.IsEnabled = !busy && clean && Draft.CanRedo;
        ApplyButton.IsEnabled = Ready && clean && mapped; UpdateButton.IsEnabled = Ready && PendingProperties;
        ResetButton.IsEnabled = !busy && PendingProperties; ValidateButton.IsEnabled = !busy && !PendingProperties;
        DeleteButton.IsEnabled = MoveUpButton.IsEnabled = MoveDownButton.IsEnabled = Ready && clean && Draft.Selection is not null;
        InspectorState.Text = busy ? "Validating…" : PendingProperties ? "Pending properties" : PendingSource ? "Source validation required" : mapped ? "Ready · inspect operation protection below" : "Apply protected · repair source or add valid content";
    }
    protected async Task Execute(Func<Task<SourceAuthoringResult>> action)
    {
        if (!Ready) return; busy = true; UpdateActions();
        try { var result = await action(); if (closed) return; if (!result.Success) { Status.Text = result.Error; AutomationProperties.SetHelpText(InspectorState, result.Error ?? ""); return; } await Refresh(); }
        finally { busy = false; if (!closed) UpdateActions(); }
    }
    private async void Update(object sender, RoutedEventArgs e) { if (Ready && PendingProperties) await Execute(UpdateProperties); }
    private async void Delete(object sender, RoutedEventArgs e) { if (Ready && !PendingProperties && !PendingSource) await Execute(DeleteSelection); }
    private async void MoveUp(object sender, RoutedEventArgs e) { if (Ready && !PendingProperties) await Execute(() => MoveSelection(-1)); }
    private async void MoveDown(object sender, RoutedEventArgs e) { if (Ready && !PendingProperties) await Execute(() => MoveSelection(1)); }
    private async void Undo(object sender, RoutedEventArgs e) { if (!Ready || PendingProperties) return; Draft.Undo(); await Refresh(); }
    private async void Redo(object sender, RoutedEventArgs e) { if (!Ready || PendingProperties) return; Draft.Redo(); await Refresh(); }
    private void ResetProperties(object sender, RoutedEventArgs e) { if (busy) return; updating = true; ShowProperties(); PropertiesLoaded(); updating = false; UpdateActions(); }
    private void SourceChanged(object? sender, EventArgs e) { if (!updating && Draft is not null) UpdateActions(); }
    private async void ValidateSource(object sender, RoutedEventArgs e) { if (Ready && !PendingProperties || !busy && !PendingProperties) await ExecuteSource(); }
    private async Task ExecuteSource()
    {
        busy = true; UpdateActions();
        try { var result = await Draft.ValidateSourceAsync(SourceEditor.Text, Lifetime.Token); if (closed) return; if (result.Success) await Refresh(); else Status.Text = result.Error; }
        finally { busy = false; if (!closed) UpdateActions(); }
    }
    private async void Apply(object sender, RoutedEventArgs e)
    {
        if (!Ready || PendingProperties || !mapped) return; busy = true; UpdateActions();
        try { var result = await Draft.ValidateAsync(Lifetime.Token); if (!result.Success) { Status.Text = result.Error; return; } var error = await apply(Draft.Source, Lifetime.Token); if (closed) return; if (error is not null) { Status.Text = error; return; } discard = true; Close(); }
        catch (OperationCanceledException) { } finally { busy = false; if (!closed) UpdateActions(); }
    }
    private void ConfirmClose(object? sender, CancelEventArgs e)
    {
        if (discard || Draft is null) return;
        if (busy) { e.Cancel = true; Status.Text = "Wait for validation or Apply to finish before closing."; return; }
        if ((Draft.Source != Draft.OriginalSource || PendingSource || PendingProperties) && MessageBox.Show(this, "Discard this private draft and pending properties? The owning document will be kept.", "Discard " + family + " draft", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) e.Cancel = true;
    }
    public bool CloseForOwner() { Close(); return closed; }
    private void Cancel(object sender, RoutedEventArgs e) => Close();
    private void EditSource(object sender, RoutedEventArgs e) => editSource?.Invoke();
    private void ToggleSource(object sender, RoutedEventArgs e) => SourcePanel.IsExpanded = !SourcePanel.IsExpanded;
    private void LocateSource(object sender, RoutedEventArgs e)
    { if (Draft.Selection is not { } key || PendingSource) return; var span = Draft.Text.Span(key); SourcePanel.IsExpanded = true; SourceEditor.Select(span.Start, span.Length); SourceEditor.ScrollToLine(SourceEditor.Document.GetLineByOffset(span.Start).LineNumber); SourceEditor.Focus(); }
    private void ShowOriginal(object sender, RoutedEventArgs e) => new Window { Owner = this, Title = "Original portable Mermaid", Width = 700, Height = 450, Content = new ICSharpCode.AvalonEdit.TextEditor { Text = Draft.OriginalSource, IsReadOnly = true, ShowLineNumbers = true, FontFamily = SourceEditor.FontFamily, FontSize = SourceEditor.FontSize, Background = (Brush)FindResource("WindowBrush"), Foreground = (Brush)FindResource("TextBrush") }, WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
    private void CopySource(object sender, RoutedEventArgs e) { try { Clipboard.SetText(SourceEditor.Text); Status.Text = "Mermaid draft source copied."; } catch (System.Runtime.InteropServices.ExternalException) { Status.Text = "Clipboard unavailable. Select and copy the source directly."; } }
    private void Preview(object sender, RoutedEventArgs e) { if (drawing is not null) new Window { Owner = this, Title = "Rendered " + family + " preview", Width = 850, Height = 600, Content = new ScrollViewer { Content = new Image { Source = drawing, Stretch = Stretch.None }, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto }, Background = (Brush)FindResource("WindowBrush") }.Show(); }
    private void ZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (Scale is not null) Scale.ScaleX = Scale.ScaleY = Zoom.Value / 100; }
    private void ZoomIn(object sender, RoutedEventArgs e) => Zoom.Value *= 1.15;
    private void ZoomOut(object sender, RoutedEventArgs e) => Zoom.Value /= 1.15;
    private void ActualSize(object sender, RoutedEventArgs e) => Zoom.Value = 100;
    private void Fit(object sender, RoutedEventArgs e) { if (Surface.Width > 0 && Surface.Height > 0) Zoom.Value = Math.Clamp(Math.Min((Viewport.ViewportWidth - 20) / Surface.Width, (Viewport.ViewportHeight - 20) / Surface.Height) * 100, .1, 100); }
    private void Centre(object sender, RoutedEventArgs e) { Fit(sender, e); Viewport.ScrollToHorizontalOffset(0); Viewport.ScrollToVerticalOffset(0); }
    private void Wheel(object sender, MouseWheelEventArgs e) => Controls.MermaidWorkspace.ZoomAtPointer(Viewport, Zoom, e);
    private void PanStart(object sender, MouseButtonEventArgs e) { if (e.ChangedButton != MouseButton.Middle) return; pan = e.GetPosition(Viewport); panX = Viewport.HorizontalOffset; panY = Viewport.VerticalOffset; Viewport.CaptureMouse(); Viewport.Cursor = Cursors.Hand; e.Handled = true; }
    private void PanMove(object sender, MouseEventArgs e) { if (pan is not { } start) return; var point = e.GetPosition(Viewport); Viewport.ScrollToHorizontalOffset(panX + start.X - point.X); Viewport.ScrollToVerticalOffset(panY + start.Y - point.Y); }
    private void PanEnd(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Middle) EndPan(); }
    private void EndPan() { pan = null; if (Viewport.IsMouseCaptured) Viewport.ReleaseMouseCapture(); Viewport.Cursor = Cursors.Arrow; }
    private void Shortcut(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Close(); e.Handled = true; return; }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Enter) { Apply(sender, new()); e.Handled = true; return; }
        if (e.OriginalSource is TextBoxBase || SourceEditor.IsKeyboardFocusWithin) return;
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z) Undo(sender, new()); else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y) Redo(sender, new());
        else if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Delete) Delete(sender, new()); else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.D0) ActualSize(sender, new()); else if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.F) Fit(sender, new()); else return; e.Handled = true;
    }
}
