using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Controls;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private bool updatingOutline;
    private readonly Dictionary<Guid, HashSet<string>> collapsedOutlineSections = [];
    private HashSet<string> CollapsedOutlineSections
    {
        get
        {
            if (model.ActiveDocument is not { } document) return [];
            if (!collapsedOutlineSections.TryGetValue(document.Id, out var sections)) collapsedOutlineSections[document.Id] = sections = [];
            return sections;
        }
    }
    private static T? Ancestor<T>(DependencyObject? origin) where T : DependencyObject
    {
        while (origin is not null)
        {
            if (origin is T match) return match;
            origin = origin is Visual ? VisualTreeHelper.GetParent(origin) : LogicalTreeHelper.GetParent(origin);
        }
        return null;
    }
    private void WorkspaceRightClicked(object sender, MouseButtonEventArgs e)
    {
        if (Ancestor<TreeViewItem>(e.OriginalSource as DependencyObject) is not { DataContext: WorkspaceEntryViewModel entry } row) return;
        row.IsSelected = true; e.Handled = true;
        OpenWorkspaceContext(row, entry.Path, entry.IsDirectory);
    }
    private void OpenWorkspaceRootContext(object sender, RoutedEventArgs e)
    { if (model.WorkspaceRoot is { } root) OpenWorkspaceContext((UIElement)sender, root, true); }
    private string ValidateWorkspaceContext(string root, string path)
    {
        if (model.WorkspaceRoot != root) throw new IOException("The workspace changed. Open the item's menu again.");
        var workspace = new WorkspacePath(root);
        return Path.TrimEndingDirectorySeparator(path).Equals(root, StringComparison.OrdinalIgnoreCase)
            ? workspace.Root : workspace.Resolve(Path.GetRelativePath(root, path));
    }
    private void OpenWorkspaceContext(UIElement target, string path, bool directory)
    {
        if (model.WorkspaceRoot is not { } root) return;
        var isRoot = Path.TrimEndingDirectorySeparator(path).Equals(root, StringComparison.OrdinalIgnoreCase);
        var menu = new ContextMenu { PlacementTarget = target, Placement = PlacementMode.MousePoint };
        void Add(string title, string kind, Action<string> action, bool enabled = true)
        {
            var item = new MenuItem { Header = title, Icon = new CommandIcon { Kind = kind }, IsEnabled = enabled && !model.IsBusy };
            System.Windows.Automation.AutomationProperties.SetAutomationId(item, "WorkspaceContext" + title.Replace(" ", "").Replace("…", ""));
            item.Click += (_, _) => { try { action(ValidateWorkspaceContext(root, path)); } catch (Exception error) when (error is not OutOfMemoryException) { dialogs.Error(error.Message); } };
            menu.Items.Add(item);
        }
        Add("Open", directory ? "Folder" : "Open", safe =>
        {
            if (directory && target is TreeViewItem folder) folder.IsExpanded = true;
            else if (!directory) _ = model.RunAsync(() => model.OpenPathAsync(safe, root));
        });
        Add("Open in a new tab", "Document", safe => _ = model.RunAsync(() => model.OpenPathAsync(safe, root)), !directory);
        Add("Reveal in File Explorer", "Folder", safe => Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, Arguments = "/select,\"" + safe + "\"" }));
        Add("Open containing folder", "Folder", safe => Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, Arguments = "\"" + (isRoot ? safe : Path.GetDirectoryName(safe)) + "\"" }));
        menu.Items.Add(new Separator());
        Add("Copy absolute path", "Link", Clipboard.SetText);
        Add("Copy workspace-relative path", "Link", safe => Clipboard.SetText(Path.GetRelativePath(root, safe).Replace('\\', '/')));
        Add("Copy Markdown link", "Link", safe =>
        {
            var source = model.ActiveDocument?.FilePath;
            if (source is null || !WorkspacePath.Contains(root, source)) source = Path.Combine(root, "document.md");
            var label = Path.GetFileNameWithoutExtension(safe).Replace("\\", "\\\\").Replace("[", "\\[").Replace("]", "\\]");
            Clipboard.SetText("[" + label + "](" + MarkdownLinks.RelativeReference(source, safe) + ")");
        }, !directory);
        menu.Items.Add(new Separator());
        Add("Rename…", "Document", safe => _ = MoveContextItemAsync(root, safe, directory), !isRoot);
        Add("Move…", "Folder", safe => _ = MoveContextItemAsync(root, safe, directory), !isRoot);
        Add("Delete…", "Close", safe =>
        {
            if (directory) model.DeleteFolderCommand.Execute(new WorkspaceEntryViewModel(new(Path.GetFileName(safe), safe, true, [])));
            else _ = DeleteContextDocumentAsync(root, safe);
        }, !isRoot);
        Add("Refresh", "Refresh", _ => model.RefreshWorkspaceCommand.Execute(null));
        menu.IsOpen = true;
    }
    private async Task MoveContextItemAsync(string root, string path, bool directory)
    {
        if (directory)
        {
            await model.MoveFolderCommand.ExecuteAsync(new WorkspaceEntryViewModel(new(Path.GetFileName(path), path, true, [])));
            return;
        }
        await model.RunAsync(() => model.OpenPathAsync(path, root));
        if (model.WorkspaceRoot == root && model.ActiveDocument?.FilePath == path) await model.MoveDocumentCommand.ExecuteAsync(null);
    }
    private async Task DeleteContextDocumentAsync(string root, string path)
    {
        await model.RunAsync(() => model.OpenPathAsync(path, root));
        if (model.WorkspaceRoot == root && model.ActiveDocument?.FilePath == path) await model.DeleteDocumentCommand.ExecuteAsync(null);
    }
    private void TabMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TabItem { DataContext: DocumentViewModel document } tab) return;
        if (e.ChangedButton == MouseButton.Middle)
        { if (!document.IsPinned) model.CloseDocumentCommand.Execute(document); e.Handled = true; return; }
        if (e.ChangedButton != MouseButton.Right) return;
        e.Handled = true;
        OpenTabContext(tab, document);
    }
    private void TabStripResized(object sender, SizeChangedEventArgs e)
    {
        var document = model.ActiveDocument;
        Dispatcher.BeginInvoke(() =>
        {
            if (model.ActiveDocument == document && document is not null
                && DocumentTabs.ItemContainerGenerator.ContainerFromItem(document) is TabItem tab) tab.BringIntoView();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }
    private void TabContextKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Apps && !(e.Key == Key.F10 && Keyboard.Modifiers == ModifierKeys.Shift)) return;
        if (sender is not TabItem { DataContext: DocumentViewModel document } tab) return;
        e.Handled = true; OpenTabContext(tab, document);
    }
    private void OpenTabContext(TabItem tab, DocumentViewModel document)
    {
        var menu = new ContextMenu { PlacementTarget = tab, Placement = PlacementMode.MousePoint };
        void Add(string title, string kind, Action action, bool enabled = true)
        {
            var item = new MenuItem { Header = title, Icon = new CommandIcon { Kind = kind }, IsEnabled = enabled };
            System.Windows.Automation.AutomationProperties.SetAutomationId(item, "TabContext" + title.Replace(" ", "").Replace("/", ""));
            item.Click += (_, _) => { if (model.Documents.Contains(document)) action(); }; menu.Items.Add(item);
        }
        Add("Close", "Close", () => model.CloseDocumentCommand.Execute(document), !document.IsPinned);
        Add("Close others", "Close", () => model.CloseOthersCommand.Execute(document));
        Add("Close tabs to the right", "Close", () => model.CloseToRightCommand.Execute(document));
        menu.Items.Add(new Separator());
        Add(document.IsPinned ? "Unpin" : "Pin", "Pin", () => model.TogglePinCommand.Execute(document));
        Add("Copy path", "Link", () => CopyTabPath(document), document.FilePath is not null);
        Add("Reveal in Explorer", "Folder", () => RevealTab(document), document.FilePath is not null);
        menu.IsOpen = true;
    }
    private void CopyTabPath(DocumentViewModel document)
    { try { if (document.FilePath is { } path) Clipboard.SetText(path); } catch (System.Runtime.InteropServices.COMException error) { model.Status = "The path could not be copied: " + error.Message; } }
    private void RevealTab(DocumentViewModel document)
    {
        if (document.FilePath is not { } path) return;
        try { WorkspacePath.RejectReparsePoints(path); Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, Arguments = "/select,\"" + path + "\"" }); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { dialogs.Error(error.Message); }
    }
    private void OutlineFilterChanged(object sender, TextChangedEventArgs e) => RefreshOutline();
    private void RefreshOutline()
    {
        if (Outline is null) return;
        updatingOutline = true;
        try
        {
            var query = OutlineFilter?.Text ?? "";
            var visible = new List<DocumentHeading>(); int? closedLevel = null;
            foreach (var heading in parsed?.Headings ?? [])
            {
                if (query.Length > 0)
                { if (heading.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)) visible.Add(heading); continue; }
                if (closedLevel is { } level && heading.Level <= level) closedLevel = null;
                if (closedLevel is not null) continue;
                visible.Add(heading);
                if (CollapsedOutlineSections.Contains(heading.Anchor)) closedLevel = heading.Level;
            }
            Outline.ItemsSource = visible;
            HighlightCurrentHeading();
        }
        finally { updatingOutline = false; }
    }
    private void HighlightCurrentHeading()
    {
        if (parsed is null || parsedVersion is null || parsedDocumentId != model.ActiveDocument?.Id || Editor.Document.Version.CompareAge(parsedVersion) != 0 || !OutlinePanel.IsVisible) return;
        var heading = Outline.Items.OfType<DocumentHeading>().LastOrDefault(item => item.SourceStart <= Editor.CaretOffset);
        var wasUpdating = updatingOutline; updatingOutline = true;
        try { Outline.SelectedItem = heading; }
        finally { updatingOutline = wasUpdating; }
    }
    private void ExpandOutlineGroups(object sender, RoutedEventArgs e) { CollapsedOutlineSections.Clear(); RefreshOutline(); }
    private void CollapseOutlineGroups(object sender, RoutedEventArgs e)
    {
        if (parsed is null) return;
        for (var index = 0; index + 1 < parsed.Headings.Count; index++)
            if (parsed.Headings[index + 1].Level > parsed.Headings[index].Level) CollapsedOutlineSections.Add(parsed.Headings[index].Anchor);
        RefreshOutline();
    }
    private void OutlineKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Left or Key.Right) || Outline.SelectedItem is not DocumentHeading heading) return;
        if (e.Key == Key.Left) CollapsedOutlineSections.Add(heading.Anchor); else CollapsedOutlineSections.Remove(heading.Anchor);
        e.Handled = true; RefreshOutline();
        // Replacing the filtered item collection removes its focused container.
        // Keep subsequent group keys in the outline without forcing preview layout.
        Outline.Focus();
        var document = model.ActiveDocument;
        Dispatcher.BeginInvoke(() =>
        {
            if (model.ActiveDocument == document && Outline.IsKeyboardFocusWithin && ReferenceEquals(Outline.SelectedItem, heading)
                && Outline.ItemContainerGenerator.ContainerFromItem(heading) is ListBoxItem { IsVisible: true } row) row.Focus();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }
}
