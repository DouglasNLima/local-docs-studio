using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using LensDocsStudio.Core.State;
using LensDocsStudio.Native.Controls;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private bool focusMode;
    private bool fullScreen;
    private string? previewBeforeEditorStatus;
    private WindowBounds? previousWindow;
    private WindowStyle previousStyle;
    private ResizeMode previousResize;
    private DesktopPreferences DesktopState => model.Preferences.Desktop ?? new();

    private void ApplyPanels()
    {
        var sidebar = DesktopState.ShowSidebar && !focusMode;
        var outline = model.Preferences.ShowOutline && !focusMode;
        var widths = DesktopPanelSizing.Fit(ActualWidth > 0 ? ActualWidth : Width, model.Layout == "Split",
            sidebar ? DesktopState.SidebarWidth : 0, outline ? DesktopState.OutlineWidth : 0);
        SidebarColumn.MinWidth = sidebar ? 150 : 0; SidebarColumn.MaxWidth = sidebar ? 600 : 0;
        SidebarColumn.Width = new GridLength(widths.Sidebar);
        SidebarPanel.Visibility = SidebarSplitter.Visibility = sidebar ? Visibility.Visible : Visibility.Collapsed;
        SidebarSplitterColumn.Width = new GridLength(sidebar ? 5 : 0);
        OutlineColumn.MinWidth = outline ? 160 : 0; OutlineColumn.MaxWidth = outline ? 600 : 0;
        OutlineColumn.Width = new GridLength(widths.Outline);
        OutlinePanel.Visibility = OutlineSplitter.Visibility = outline ? Visibility.Visible : Visibility.Collapsed;
        OutlineSplitterColumn.Width = new GridLength(outline ? 5 : 0);
        FormattingToolbar.Visibility = PrimaryToolbar.Visibility = focusMode ? Visibility.Collapsed : Visibility.Visible;
        PreviewToolbar.Visibility = focusMode ? Visibility.Collapsed : Visibility.Visible;
    }
    private void DesktopWindowResized(object sender, SizeChangedEventArgs e) => ApplyPanels();
    private async Task PersistDesktop(Func<UserPreferences, UserPreferences> update)
    {
        try { await model.UpdatePreferencesAsync(update); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { model.Status = "Interface preferences could not be saved: " + ex.Message; }
    }
    private async void ToggleSidebar(object sender, RoutedEventArgs e)
    {
        focusMode = false;
        if (SidebarPanel.IsKeyboardFocusWithin) FocusDocumentPane();
        await PersistDesktop(current => current with { Desktop = (current.Desktop ?? new()) with { ShowSidebar = !(current.Desktop?.ShowSidebar ?? true) } });
        ApplyPanels();
    }
    private async void ToggleOutline(object sender, RoutedEventArgs e)
    {
        focusMode = false;
        if (OutlinePanel.IsKeyboardFocusWithin) FocusDocumentPane();
        await PersistDesktop(current => current with { ShowOutline = !current.ShowOutline }); ApplyPanels();
    }
    private async void PanelResized(object sender, DragCompletedEventArgs e)
    {
        if (focusMode || e.Canceled || ReferenceEquals(sender, SidebarSplitter) && !SidebarPanel.IsVisible
            || ReferenceEquals(sender, OutlineSplitter) && !OutlinePanel.IsVisible) return;
        var sidebar = Math.Clamp(SidebarColumn.ActualWidth, 150, 600);
        var outline = Math.Clamp(OutlineColumn.ActualWidth, 160, 600);
        var resizeSidebar = ReferenceEquals(sender, SidebarSplitter);
        await PersistDesktop(current => current with { Desktop = resizeSidebar
            ? (current.Desktop ?? new()) with { SidebarWidth = sidebar }
            : (current.Desktop ?? new()) with { OutlineWidth = outline } });
    }
    private void FocusDocumentPane() { if (previewFocused && Preview.IsVisible) Preview.Focus(); else if (Editor.IsVisible) Editor.Focus(); else Preview.Focus(); }
    private void ToggleFocusMode(object sender, RoutedEventArgs e)
    { focusMode = !focusMode; ApplyPanels(); FocusDocumentPane(); model.Status = focusMode ? "Focus Mode · Ctrl+Alt+Z restores the interface." : "Focus Mode ended."; }
    private void ToggleFullScreen(object sender, RoutedEventArgs e)
    {
        if (!fullScreen)
        {
            previousWindow = WindowStateService.Capture(this); previousStyle = WindowStyle; previousResize = ResizeMode;
            WindowState = WindowState.Normal; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized; fullScreen = true;
        }
        else
        {
            WindowState = WindowState.Normal; WindowStyle = previousStyle; ResizeMode = previousResize;
            WindowStateService.Restore(this, previousWindow); fullScreen = false;
        }
    }
    private void OpenThemeMenu(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = sender as UIElement, Placement = PlacementMode.Bottom };
        foreach (var theme in WorkspaceSettings.Themes)
        {
            var choice = new MenuItem { Header = theme, Tag = theme, IsCheckable = true, IsChecked = model.Theme == theme,
                Icon = new CommandIcon { Kind = "Theme" } };
            choice.Click += ChangeTheme; menu.Items.Add(choice);
        }
        menu.IsOpen = true;
    }
    private void OpenHeadingMenu(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = sender as UIElement, Placement = PlacementMode.Bottom };
        for (var level = 1; level <= 6; level++)
        {
            var choice = new MenuItem { Header = $"Heading {level}", Tag = $"heading:{level}", IsEnabled = model.ActiveDocument is { IsSaving: false } };
            choice.Click += Format; menu.Items.Add(choice);
        }
        menu.IsOpen = true;
    }
    private void OpenQuickOpen(object sender, RoutedEventArgs e)
    {
        static IEnumerable<WorkspaceEntryViewModel> Flatten(IEnumerable<WorkspaceEntryViewModel> nodes) => nodes.SelectMany(node => node.IsDirectory ? Flatten(node.Children) : [node]);
        var root = model.WorkspaceRoot;
        var actions = Flatten(model.Entries).Select(entry => new DesktopAction(entry.Name,
            root is null ? entry.Path : Path.GetRelativePath(root, entry.Path),
            () => _ = model.RunAsync(() => model.OpenPathAsync(entry.Path, root)))).ToArray();
        ShowCommandSearch("Quick Open", actions);
    }
    private void ShowCommandSearch(string title, IReadOnlyList<DesktopAction> actions)
    {
        var focus = Keyboard.FocusedElement;
        var picker = new CommandSearchWindow(title, actions) { Owner = this };
        var confirmed = picker.ShowDialog() == true;
        if (focus is UIElement { IsVisible: true } element) element.Focus(); else FocusDocumentPane();
        if (confirmed) picker.Chosen?.Execute();
    }
    private void OpenCommandPalette(object sender, RoutedEventArgs e)
    {
        var edit = model.ActiveDocument is { IsSaving: false };
        var commands = new List<DesktopAction>();
        void Add(string title, string shortcut, Action action, bool enabled = true) => commands.Add(new(title, shortcut, action, enabled));
        Add("Open workspace", "Ctrl+Shift+O", () => model.OpenFolderCommand.Execute(null), model.OpenFolderCommand.CanExecute(null));
        Add("Open document", "Ctrl+O", () => model.OpenDocumentCommand.Execute(null), model.OpenDocumentCommand.CanExecute(null));
        Add("New document", "Ctrl+N", () => model.NewDocumentCommand.Execute(null), model.NewDocumentCommand.CanExecute(null));
        Add("Save document", "Ctrl+S", () => model.SaveCommand.Execute(null), model.SaveCommand.CanExecute(null));
        Add("Quick Open", "Ctrl+P", () => OpenQuickOpen(sender, e));
        Add("Toggle workspace", "Ctrl+Alt+B", () => ToggleSidebar(sender, e));
        Add("Toggle outline", "Ctrl+Alt+O", () => ToggleOutline(sender, e));
        Add("Full Screen", "F11", () => ToggleFullScreen(sender, e));
        Add("Focus Mode", "Ctrl+Alt+Z", () => ToggleFocusMode(sender, e));
        Add("Follow caret", DesktopState.FollowCaret ? "On" : "Off", () => FollowCaretControl.IsChecked = !FollowCaretControl.IsChecked);
        Add("Synchronise scroll", DesktopState.SyncScroll ? "On" : "Off", () => SyncScrollControl.IsChecked = !SyncScrollControl.IsChecked);
        Add("Synchronise selection", DesktopState.SyncSelection ? "On" : "Off", () => SyncSelectionControl.IsChecked = !SyncSelectionControl.IsChecked);
        Add("Settings", "Ctrl+,", () => OpenSettings(sender, e));
        Add("Find", "Ctrl+F", () => ShowFind(false));
        Add("Back", "Alt+Left", () => NavigateBack(sender, e), BackButton.IsEnabled);
        Add("Forward", "Alt+Right", () => NavigateForward(sender, e), ForwardButton.IsEnabled);
        foreach (var layout in WorkspaceSettings.Layouts)
            Add(layout + " layout", "", () => ChangeLayout(new Button { Tag = layout }, e));
        foreach (var theme in WorkspaceSettings.Themes)
            Add(theme + " theme", "", () => ChangeTheme(new Button { Tag = theme }, e));
        foreach (var mode in WorkspaceSettings.ReadingModes)
            Add(mode + " reading mode", "", () => ReadingModeControl.SelectedItem = mode);
        for (var level = 1; level <= 6; level++)
        {
            var chosen = level;
            Add("Heading " + chosen, "", () => Format(new Button { Tag = "heading:" + chosen }, e), edit);
        }
        foreach (var action in new[] { "heading", "bold", "italic", "strike", "inline-code", "link", "task", "bullet", "numbered", "quote", "code", "mermaid", "table", "rule" })
            Add("Format: " + action.Replace('-', ' '), "", () => Format(new Button { Tag = action }, e), edit);
        Add("Insert workspace document link", "Ctrl+Shift+K", () => InsertDocumentLink(sender, e), edit);
        Add("Insert local image", "", () => InsertImage(sender, e), edit);
        foreach (var format in new[] { "HTML", "DOCX", "PDF" })
            Add("Export " + format, "", () => ExportDocument(new Button { Tag = format }, e), model.ActiveDocument is not null && !model.IsBusy);
        Add("Export Docs Site", "", () => ExportDocsSite(sender, e), model.WorkspaceRoot is not null && !model.IsBusy);
        ShowCommandSearch("Command Palette", commands);
    }
    private bool HandleDesktopKey(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape && (ApplicationMenu.Items.OfType<MenuItem>().Any(item => item.IsSubmenuOpen) || ReadingModeControl.IsDropDownOpen
            || ApplicationToolbar.IsOverflowOpen
            || FormattingToolbar.IsOverflowOpen || PreviewToolbar.IsOverflowOpen
            || Ancestor<ContextMenu>(Keyboard.FocusedElement as DependencyObject) is not null)) return false;
        if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.F11) { ToggleFullScreen(this, e); return true; }
        if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Escape && FindBar.Visibility != Visibility.Visible)
        { if (fullScreen) { ToggleFullScreen(this, e); return true; } if (focusMode) { ToggleFocusMode(this, e); return true; } }
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
        {
            if (key == Key.B) { ToggleSidebar(this, e); return true; }
            if (key == Key.O) { ToggleOutline(this, e); return true; }
            if (key == Key.Z) { ToggleFocusMode(this, e); return true; }
        }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.P) { OpenQuickOpen(this, e); return true; }
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.P) { OpenCommandPalette(this, e); return true; }
        if (Keyboard.Modifiers is ModifierKeys.Control or (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.Tab && model.Documents.Count > 0)
        {
            var next = (model.Documents.IndexOf(model.ActiveDocument!) + (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1) + model.Documents.Count) % model.Documents.Count;
            model.ActiveDocument = model.Documents[next]; FocusDocumentPane(); return true;
        }
        return false;
    }
}
