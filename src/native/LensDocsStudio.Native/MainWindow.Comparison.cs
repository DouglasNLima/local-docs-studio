using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;
using LensDocsStudio.Native.ViewModels;
using Microsoft.Win32;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private sealed class ComparisonSlot(DocumentViewModel document, string mode)
    { public DocumentViewModel Document = document; public string Mode = mode; }
    private sealed record ComparisonView(int Start, int Length, double Horizontal, double Vertical, double PreviewTop,
        string Find, string Replacement, bool FindOpen, bool ReplaceOpen, bool SearchPreview, (int Start, int Length)? PreviewSelection = null, int Caret = -1,
        (int Source, double Fraction)? PreviewAnchor = null, ITextSourceVersion? PreviewVersion = null);
    private readonly Dictionary<Guid, ComparisonView> comparisonViews = [];
    private ComparisonSlot[]? comparisonSlots;
    private int comparisonActive;
    private bool comparisonUpdating, comparisonScrollLock;
    private string comparisonOriginalLayout = "Split";
    private DockPanel? comparisonHost;
    private readonly Grid comparisonGrid = new();
    private readonly Grid[] comparisonBodies = [new(), new()];
    private readonly DockPanel[] comparisonPanes = [new(), new()];
    private readonly ComboBox[] comparisonDocuments = [new(), new()];
    private readonly ComboBox[] comparisonModes = [new(), new()];
    private readonly Button[] comparisonActivate = [new(), new()];
    private readonly CheckBox comparisonSync = new() { Content = "Synchronise scroll", Margin = new(8, 4, 8, 4) };
    private readonly GridSplitter comparisonSplitter = new() { Background = Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
    private readonly TextEditor comparisonEditor = new() { IsReadOnly = true, ShowLineNumbers = true, FontFamily = new("Cascadia Mono, Consolas"), FontSize = 14 };
    private readonly DocumentPreview comparisonPreview = new();
    private readonly DispatcherTimer comparisonTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private CancellationTokenSource? comparisonCancellation;
    private Guid? comparisonRenderedId;
    private ITextSourceVersion? comparisonRenderedVersion;
    private string? comparisonRenderedContext;
    private Guid? comparisonRequestedId;
    private ITextSourceVersion? comparisonRequestedVersion;
    private string? comparisonRequestedContext;
    private long comparisonGeneration;
    private bool comparisonPreviewPointer;
    private bool comparisonEditorPointer;
    private PreviewResult? comparisonPreviewResult;

    private async void OpenBeside(object sender, RoutedEventArgs e)
    {
        if (model.ActiveDocument is not { } original) return;
        if (comparisonSlots is not null) { comparisonDocuments[1 - comparisonActive].Focus(); return; }
        var second = model.Documents.FirstOrDefault(document => document != original);
        if (second is null)
        {
            var picker = new OpenFileDialog { Title = "Open document beside", Filter = "Markdown and Mermaid|*.md;*.markdown;*.mmd;*.mermaid", InitialDirectory = original.WorkspaceRoot ?? "" };
            if (picker.ShowDialog(this) != true) return;
            await model.RunAsync(() => model.OpenPathAsync(picker.FileName, original.WorkspaceRoot));
            second = model.ActiveDocument;
            model.ActiveDocument = original;
            if (second == original || second is null) return;
        }
        StartComparison(original, second);
    }
    private void StartComparison(DocumentViewModel first, DocumentViewModel second)
    {
        if (first == second || !model.Documents.Contains(first) || !model.Documents.Contains(second)) return;
        InitialiseComparison(); comparisonOriginalLayout = model.Layout;
        comparisonSlots = [new(first, model.Layout == "Preview" ? "Preview" : "Editor"), new(second, "Preview")]; comparisonActive = 0;
        CaptureComparisonView(); model.ActiveDocument = first;
        comparisonHost!.Children[0].Visibility = Visibility.Visible;
        AttachComparison();
    }
    private void InitialiseComparison()
    {
        if (comparisonHost is not null) return;
        var parent = (DockPanel)ContentGrid.Parent; var position = parent.Children.IndexOf(ContentGrid);
        parent.Children.Remove(ContentGrid); comparisonHost = new(); parent.Children.Insert(position, comparisonHost);
        var controls = new WrapPanel(); DockPanel.SetDock(controls, Dock.Top);
        controls.Children.Add(new TextBlock { Text = "Compare documents", Margin = new(8), VerticalAlignment = VerticalAlignment.Center });
        controls.Children.Add(comparisonSync);
        var choose = new Button { Content = "Choose file…" }; choose.Click += async (_, _) =>
        {
            var picker = new OpenFileDialog { Title = "Choose document for active comparison pane", Filter = "Markdown and Mermaid|*.md;*.markdown;*.mmd;*.mermaid" };
            if (picker.ShowDialog(this) == true) await model.RunAsync(() => model.OpenPathAsync(picker.FileName));
        }; controls.Children.Add(choose);
        var close = new Button { Content = "Close comparison" }; close.Click += (_, _) => CloseComparison(); controls.Children.Add(close);
        AutomationProperties.SetAutomationId(close, "CloseComparison"); AutomationProperties.SetName(comparisonSync, "Synchronise comparison scroll");
        comparisonHost.Children.Add(controls); comparisonHost.Children.Add(comparisonGrid);
        for (var index = 0; index < 2; index++)
        {
            var captured = index; var pane = comparisonPanes[index]; var header = new WrapPanel(); DockPanel.SetDock(header, Dock.Top);
            var documents = comparisonDocuments[index]; documents.Width = 190; documents.DisplayMemberPath = "Title"; documents.ItemsSource = model.Documents;
            documents.SelectionChanged += (_, _) => { if (!comparisonUpdating && comparisonSlots is not null && documents.SelectedItem is DocumentViewModel document) { ActivateComparison(captured, false); model.ActiveDocument = document; } };
            var mode = comparisonModes[index]; mode.ItemsSource = new[] { "Editor", "Preview" }; mode.Width = 90;
            mode.SelectionChanged += (_, _) => { if (!comparisonUpdating && comparisonSlots is not null && mode.SelectedItem is string selected) { comparisonSlots[captured].Mode = selected; if (captured == comparisonActive) SetLayout(selected); AttachComparison(); } };
            var activate = comparisonActivate[index]; activate.Content = "Activate"; activate.Click += (_, _) => ActivateComparison(captured);
            AutomationProperties.SetName(documents, $"Document in {(index == 0 ? "left" : "right")} pane");
            AutomationProperties.SetAutomationId(mode, "ComparisonMode" + index);
            AutomationProperties.SetAutomationId(documents, "ComparisonDocument" + index);
            AutomationProperties.SetName(mode, $"Mode in {(index == 0 ? "left" : "right")} pane");
            AutomationProperties.SetAutomationId(activate, "ActivateComparison" + index);
            header.Children.Add(documents); header.Children.Add(mode); header.Children.Add(activate);
            pane.Children.Add(header); pane.Children.Add(comparisonBodies[index]); comparisonGrid.Children.Add(pane);
        }
        comparisonGrid.Children.Add(comparisonSplitter); comparisonSplitter.Width = 5;
        AutomationProperties.SetName(comparisonSplitter, "Resize comparison panes"); comparisonSplitter.Focusable = true;
        comparisonSplitter.KeyDown += (_, e) =>
        {
            if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down)) return;
            var delta = e.Key is Key.Right or Key.Down ? .1 : -.1;
            if (comparisonGrid.ColumnDefinitions.Count > 1) comparisonGrid.ColumnDefinitions[0].Width = new(Math.Clamp(comparisonGrid.ColumnDefinitions[0].Width.Value + delta, .4, 2), GridUnitType.Star);
            else comparisonGrid.RowDefinitions[0].Height = new(Math.Clamp(comparisonGrid.RowDefinitions[0].Height.Value + delta, .4, 2), GridUnitType.Star);
            e.Handled = true;
        };
        comparisonEditor.SetResourceReference(BackgroundProperty, "WindowBrush"); comparisonEditor.SetResourceReference(ForegroundProperty, "TextBrush");
        comparisonEditor.SetBinding(FontSizeProperty, new System.Windows.Data.Binding(nameof(Editor.FontSize)) { Source = Editor });
        comparisonEditor.SetBinding(FontFamilyProperty, new System.Windows.Data.Binding(nameof(Editor.FontFamily)) { Source = Editor });
        comparisonEditor.TextArea.TextView.LineTransformers.Add(new MarkdownHighlighting { Dark = ThemeService.IsDark(model.Theme), HighContrast = SystemParameters.HighContrast });
        // Keep the reader template stable while moving between pane containers.
        // The style's brushes remain dynamic for theme/High Contrast changes.
        comparisonPreview.Style = (Style)FindResource("DocumentPreviewStyle"); comparisonPreview.SetResourceReference(BackgroundProperty, "WindowBrush");
        AutomationProperties.SetAutomationId(comparisonEditor, "ComparisonSource"); AutomationProperties.SetName(comparisonEditor, "Secondary source; focus to activate document");
        AutomationProperties.SetAutomationId(comparisonPreview, "ComparisonPreview"); AutomationProperties.SetName(comparisonPreview, "Secondary document preview");
        comparisonEditor.PreviewMouseDown += (_, _) => comparisonEditorPointer = true;
        comparisonEditor.GotKeyboardFocus += (_, _) => { if (!comparisonEditorPointer && !comparisonUpdating && comparisonSlots is not null) ActivateComparison(1 - comparisonActive); };
        comparisonEditor.AddHandler(Mouse.MouseUpEvent, new MouseButtonEventHandler((_, _) =>
        {
            comparisonEditorPointer = false;
            if (comparisonSlots is null || comparisonUpdating) return;
            var document = comparisonSlots[1 - comparisonActive].Document;
            var view = comparisonViews.GetValueOrDefault(document.Id) ?? new(0, 0, 0, document.VerticalOffset, 0, "", "", false, false, false);
            comparisonViews[document.Id] = view with { Start = comparisonEditor.SelectionStart, Length = comparisonEditor.SelectionLength,
                Caret = comparisonEditor.CaretOffset, Vertical = comparisonEditor.VerticalOffset, Horizontal = comparisonEditor.HorizontalOffset };
            ActivateComparison(1 - comparisonActive);
        }), true);
        comparisonPreview.PreviewMouseDown += (_, _) => comparisonPreviewPointer = true;
        comparisonPreview.GotKeyboardFocus += (_, _) => { if (!comparisonPreviewPointer && !comparisonUpdating && comparisonSlots is not null) ActivateComparison(1 - comparisonActive); };
        comparisonPreview.AddHandler(Mouse.MouseUpEvent, new MouseButtonEventHandler((_, _) =>
        {
            comparisonPreviewPointer = false;
            if (comparisonSlots is null || comparisonUpdating) return;
            var document = comparisonSlots[1 - comparisonActive].Document;
            if (comparisonRenderedId != document.Id) return;
            var view = comparisonViews.GetValueOrDefault(document.Id) ?? new(0, 0, 0, document.VerticalOffset, 0, "", "", false, false, false);
            comparisonViews[document.Id] = view with { PreviewTop = ComparisonScroller(comparisonPreview)?.VerticalOffset ?? 0,
                PreviewSelection = comparisonPreview.Selection is { IsEmpty: false } selection && comparisonPreviewResult?.TextMap.TrySourceRange(selection.Start, selection.End, out var start, out var length) == true ? (start, length) : null };
            ActivateComparison(1 - comparisonActive);
        }), true);
        foreach (var surface in new UIElement[] { Editor, Preview, comparisonEditor, comparisonPreview })
            surface.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, args) => ComparisonScroll(surface, args)));
        comparisonTimer.Tick += async (_, _) => { comparisonTimer.Stop(); await RenderComparisonAsync(); };
        SizeChanged += (_, _) => LayoutComparison();
        model.DocumentRemoved += _ => { if (comparisonSlots?.Any(slot => !model.Documents.Contains(slot.Document)) == true) CloseComparison(); };
        Closed += (_, _) => { comparisonTimer.Stop(); comparisonCancellation?.Cancel(); comparisonEditor.Document = new(); comparisonPreview.Document = new(); };
    }
    private void CaptureComparisonView()
    {
        if (comparisonSlots is null || displayedTab is null) return;
        var previous = comparisonViews.GetValueOrDefault(displayedTab.Id);
        comparisonViews[displayedTab.Id] = new(Editor.SelectionStart, Editor.SelectionLength, Editor.HorizontalOffset, Editor.VerticalOffset,
            ComparisonScroller(Preview)?.VerticalOffset ?? 0, FindText.Text, ReplaceText.Text, FindBar.Visibility == Visibility.Visible,
            ReplaceText.Visibility == Visibility.Visible, searchPreview,
            renderedDocumentId == displayedTab.Id && Preview.Selection is { IsEmpty: false } selection
                && rendered?.TextMap.TrySourceRange(selection.Start, selection.End, out var start, out var length) == true ? (start, length) : null,
            Editor.CaretOffset, previous?.PreviewAnchor, previous?.PreviewVersion);
        CaptureComparisonReadingPosition(displayedTab, Preview, rendered, renderedDocumentId, renderedVersion);
        var secondary = comparisonSlots[1 - comparisonActive].Document;
        CaptureComparisonReadingPosition(secondary, comparisonPreview, comparisonPreviewResult, comparisonRenderedId, comparisonRenderedVersion);
        foreach (var id in comparisonViews.Keys.Where(id => !model.Documents.Any(document => document.Id == id)).ToArray()) comparisonViews.Remove(id);
    }
    private void CaptureComparisonReadingPosition(DocumentViewModel document, DocumentPreview reader, PreviewResult? result,
        Guid? owner, ITextSourceVersion? version)
    {
        if (owner != document.Id || result is null || reader.Document != result.Document || !reader.IsVisible
            || version is null || !version.BelongsToSameDocumentAs(document.EditorDocument.Version)
            || version.CompareAge(document.EditorDocument.Version) != 0 || !reader.IsMeasureValid || !reader.IsArrangeValid
            || AutomationProperties.GetItemStatus(reader).StartsWith("Busy", StringComparison.Ordinal)
            || result.TextMap.VisibleSource(reader.ActualHeight) is not { } source) return;
        var anchor = PreviewViewportAnchor.Capture(reader, result);
        if (anchor is not null && result.TextMap.TrySourceOffset(anchor.Position, out var anchoredSource)) source = anchoredSource;
        // The secondary reader belongs to its document too. Save its source position
        // before activation replaces the shared reader and its immutable source map.
        document.PreviewSourceOffset = source;
        var view = comparisonViews.GetValueOrDefault(document.Id) ?? new(0, 0, 0, document.VerticalOffset, 0, "", "", false, false, false);
        comparisonViews[document.Id] = view with { PreviewTop = ComparisonScroller(reader)?.VerticalOffset ?? 0,
            PreviewAnchor = (source, anchor?.VerticalFraction ?? 0), PreviewVersion = version,
            PreviewSelection = reader.Selection is { IsEmpty: false } selection
                && result.TextMap.TrySourceRange(selection.Start, selection.End, out var start, out var length) ? (start, length) : null };
    }
    private void RestoreComparisonView(DocumentViewModel document)
    {
        var view = comparisonViews.GetValueOrDefault(document.Id) ?? new(0, 0, 0, document.VerticalOffset, 0, "", "", false, false, false);
        var start = Math.Min(view.Start, Editor.Document.TextLength); Editor.Select(start, Math.Min(view.Length, Editor.Document.TextLength - start));
        if (view.Caret >= 0) Editor.CaretOffset = Math.Min(view.Caret, Editor.Document.TextLength);
        Editor.ScrollToHorizontalOffset(view.Horizontal); Editor.ScrollToVerticalOffset(view.Vertical);
        FindText.Text = view.Find; ReplaceText.Text = view.Replacement; searchPreview = view.SearchPreview;
        FindBar.Visibility = view.FindOpen ? Visibility.Visible : Visibility.Collapsed;
        ReplaceText.Visibility = ReplaceButton.Visibility = view.ReplaceOpen ? Visibility.Visible : Visibility.Collapsed;
        FindContext.Text = searchPreview ? "Find in preview" : "Find in editor";
        InvalidateFind(FindRefreshCause.ActiveDocument);
    }
    private void ComparisonDocumentBound()
    {
        if (comparisonSlots is null || comparisonUpdating || model.ActiveDocument is not { } current) return;
        if (comparisonSlots.Any(slot => !model.Documents.Contains(slot.Document))) { CloseComparison(); return; }
        if (comparisonSlots[1 - comparisonActive].Document == current) comparisonActive = 1 - comparisonActive;
        else comparisonSlots[comparisonActive].Document = current;
        AttachComparison(); RestoreComparisonView(current);
    }
    private void ActivateComparison(int index, bool focus = true)
    {
        if (comparisonSlots is null || comparisonUpdating || index == comparisonActive) return;
        CaptureComparisonView(); model.ActiveDocument = comparisonSlots[index].Document;
        if (focus) { if (model.Layout == "Editor") Editor.Focus(); else Preview.Focus(); }
    }
    private void AttachComparison()
    {
        if (comparisonSlots is null || comparisonUpdating) return;
        comparisonUpdating = true;
        try
        {
            AttachComparisonSurface(ContentGrid, comparisonBodies[comparisonActive]);
            var inactive = 1 - comparisonActive; var slot = comparisonSlots[inactive];
            comparisonEditor.Document = slot.Document.EditorDocument;
            comparisonPreview.Zoom = Preview.Zoom;
            foreach (var highlighting in comparisonEditor.TextArea.TextView.LineTransformers.OfType<MarkdownHighlighting>())
            { highlighting.Dark = ThemeService.IsDark(model.Theme); highlighting.HighContrast = SystemParameters.HighContrast; }
            var view = comparisonViews.GetValueOrDefault(slot.Document.Id);
            if (view is not null) { var start = Math.Min(view.Start, comparisonEditor.Document.TextLength); comparisonEditor.Select(start, Math.Min(view.Length, comparisonEditor.Document.TextLength - start)); if (view.Caret >= 0) comparisonEditor.CaretOffset = Math.Min(view.Caret, comparisonEditor.Document.TextLength); }
            comparisonEditor.ScrollToVerticalOffset(view?.Vertical ?? slot.Document.VerticalOffset);
            AttachComparisonSurface(slot.Mode == "Editor" ? comparisonEditor : comparisonPreview, comparisonBodies[inactive]);
            for (var index = 0; index < 2; index++)
            {
                comparisonDocuments[index].SelectedItem = comparisonSlots[index].Document; comparisonModes[index].SelectedItem = comparisonSlots[index].Mode;
                comparisonActivate[index].Content = index == comparisonActive ? "Active" : "Activate";
                AutomationProperties.SetName(comparisonActivate[index], $"{(index == comparisonActive ? "Active" : "Activate")} {comparisonSlots[index].Document.Title}");
                comparisonPanes[index].SetResourceReference(BackgroundProperty, index == comparisonActive ? "PanelBrush" : "WindowBrush");
            }
            SetLayout(comparisonSlots[comparisonActive].Mode); LayoutComparison(); QueueComparisonPreview();
        }
        finally { comparisonUpdating = false; }
    }
    private static void AttachComparisonSurface(FrameworkElement surface, Grid host)
    {
        if (ReferenceEquals(surface.Parent, host)) return;
        if (surface.Parent is Panel previous) previous.Children.Remove(surface);
        host.Children.Clear(); host.Children.Add(surface);
    }
    private void LayoutComparison()
    {
        if (comparisonSlots is null) return;
        var narrow = ActualWidth > 0 && ActualWidth < 1100;
        if ((comparisonGrid.RowDefinitions.Count > 1) == narrow && comparisonGrid.ColumnDefinitions.Count > 0) return;
        comparisonGrid.RowDefinitions.Clear(); comparisonGrid.ColumnDefinitions.Clear();
        if (narrow)
        {
            comparisonGrid.ColumnDefinitions.Add(new()); comparisonGrid.RowDefinitions.Add(new()); comparisonGrid.RowDefinitions.Add(new() { Height = new(5) }); comparisonGrid.RowDefinitions.Add(new());
            for (var index = 0; index < 2; index++) { Grid.SetRow(comparisonPanes[index], index * 2); Grid.SetColumn(comparisonPanes[index], 0); }
            Grid.SetRow(comparisonSplitter, 1); Grid.SetColumn(comparisonSplitter, 0); comparisonSplitter.Height = 5; comparisonSplitter.Width = double.NaN;
            comparisonSplitter.ResizeDirection = GridResizeDirection.Rows;
        }
        else
        {
            comparisonGrid.RowDefinitions.Add(new()); comparisonGrid.ColumnDefinitions.Add(new()); comparisonGrid.ColumnDefinitions.Add(new() { Width = new(5) }); comparisonGrid.ColumnDefinitions.Add(new());
            for (var index = 0; index < 2; index++) { Grid.SetColumn(comparisonPanes[index], index * 2); Grid.SetRow(comparisonPanes[index], 0); }
            Grid.SetColumn(comparisonSplitter, 1); Grid.SetRow(comparisonSplitter, 0); comparisonSplitter.Width = 5; comparisonSplitter.Height = double.NaN;
            comparisonSplitter.ResizeDirection = GridResizeDirection.Columns;
        }
    }
    private void CloseComparison()
    {
        if (comparisonSlots is null) return;
        // Closing Preview/Preview changes the reader's viewport even when its
        // layout name stays Preview. Capture before detaching the pane.
        var completedPreviewStatus = previewLayoutStatus ?? previewBeforeEditorStatus ?? model.PreviewStatus;
        CapturePreviewLayoutAnchor();
        CancelSynchronisation();
        CaptureComparisonView(); comparisonSlots = null; comparisonGeneration++; comparisonCancellation?.Cancel(); comparisonTimer.Stop();
        foreach (var body in comparisonBodies) body.Children.Clear();
        comparisonGrid.Children.Clear(); comparisonGrid.RowDefinitions.Clear(); comparisonGrid.ColumnDefinitions.Clear();
        comparisonGrid.Children.Add(ContentGrid); Grid.SetRow(ContentGrid, 0); Grid.SetColumn(ContentGrid, 0);
        comparisonHost!.Children[0].Visibility = Visibility.Collapsed;
        // Reopening reuses the controls, but restores both pane containers.
        comparisonEditor.Document = new(); comparisonPreview.Document = new(); comparisonPreviewResult = null; comparisonRenderedId = null; comparisonRequestedId = null;
        SetLayout(comparisonOriginalLayout); if (model.Layout == "Preview") Preview.Focus(); else Editor.Focus();
        if (model.Layout != "Editor" && SyncCurrent)
        {
            CancelPreviewLayout();
            _ = RestorePreviewLayoutAsync(completedPreviewStatus, afterReattachment: true);
        }
    }
    private void QueueComparisonPreview()
    {
        if (comparisonSlots is null) return;
        if (!comparisonGrid.Children.Contains(comparisonPanes[0])) { comparisonGrid.Children.Remove(ContentGrid); comparisonGrid.Children.Add(comparisonPanes[0]); comparisonGrid.Children.Add(comparisonPanes[1]); comparisonGrid.Children.Add(comparisonSplitter); LayoutComparison(); }
        var slot = comparisonSlots[1 - comparisonActive]; var document = slot.Document;
        var context = model.Theme + "|" + CurrentDiagramTheme + "|" + SystemParameters.HighContrast + "|" + document.EffectivePath + "|" + document.EffectiveWorkspace;
        foreach (var highlighting in comparisonEditor.TextArea.TextView.LineTransformers.OfType<MarkdownHighlighting>())
        { highlighting.Dark = ThemeService.IsDark(model.Theme); highlighting.HighContrast = SystemParameters.HighContrast; }
        comparisonEditor.TextArea.TextView.Redraw();
        if (slot.Mode == "Preview" && comparisonRequestedId == document.Id && comparisonRequestedVersion?.CompareAge(document.EditorDocument.Version) == 0 && comparisonRequestedContext == context) return;
        comparisonGeneration++; comparisonCancellation?.Cancel(); comparisonTimer.Stop();
        comparisonRequestedId = slot.Mode == "Preview" ? document.Id : null;
        comparisonRequestedVersion = document.EditorDocument.Version; comparisonRequestedContext = context;
        if (slot.Mode == "Preview") comparisonTimer.Start();
    }
    private static ScrollViewer? ComparisonScroller(DependencyObject root)
    {
        if (root is FlowDocumentScrollViewer reader && reader.Template.FindName("PART_ContentHost", reader) is ScrollViewer scrolling) return scrolling;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++) if (ComparisonScroller(VisualTreeHelper.GetChild(root, index)) is { } result) return result;
        return null;
    }
    private void ComparisonScroll(UIElement source, ScrollChangedEventArgs args)
    {
        if (comparisonSlots is null || comparisonScrollLock || comparisonSync.IsChecked != true || args.VerticalChange == 0 || comparisonUpdating) return;
        var passive = ReferenceEquals(source, comparisonEditor) || ReferenceEquals(source, comparisonPreview);
        var targetIndex = passive ? comparisonActive : 1 - comparisonActive;
        var target = comparisonSlots[targetIndex].Mode == "Editor" ? (FrameworkElement)(passive ? Editor : comparisonEditor) : passive ? Preview : comparisonPreview;
        var from = source is TextEditor text ? text.VerticalOffset / Math.Max(1, text.ExtentHeight - text.ViewportHeight) : ComparisonScroller(source) is { } sourceScroll ? sourceScroll.VerticalOffset / Math.Max(1, sourceScroll.ScrollableHeight) : double.NaN;
        if (!double.IsFinite(from)) return;
        comparisonScrollLock = true;
        if (target is TextEditor editor) editor.ScrollToVerticalOffset(from * Math.Max(0, editor.ExtentHeight - editor.ViewportHeight));
        else if (ComparisonScroller(target) is { } scroll) scroll.ScrollToVerticalOffset(from * scroll.ScrollableHeight);
        Dispatcher.BeginInvoke(() => comparisonScrollLock = false, DispatcherPriority.Background);
    }
}
