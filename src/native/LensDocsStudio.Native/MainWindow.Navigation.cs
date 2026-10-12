using System.Windows;
using ICSharpCode.AvalonEdit.Document;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private readonly NavigationHistory navigation = new();
    private bool restoringNavigation;
    private NavigationLocation? CurrentNavigation() => displayedTab is null ? null :
        new(displayedTab.Id, displayedTab.FilePath, Editor.CaretOffset, Editor.VerticalOffset);
    private void CaptureCurrentNavigation()
    { if (!restoringNavigation && CurrentNavigation() is { } location) navigation.UpdateCurrent(location); }
    private void VisitCurrentNavigation()
    {
        if (!restoringNavigation && CurrentNavigation() is { } location) navigation.Visit(location);
        RefreshNavigationControls();
    }
    private void RefreshNavigationControls()
    {
        if (BackButton is null) return;
        BackButton.IsEnabled = BackMenu.IsEnabled = navigation.CanGoBack;
        ForwardButton.IsEnabled = ForwardMenu.IsEnabled = navigation.CanGoForward;
    }
    private async void NavigateBack(object sender, RoutedEventArgs e) => await MoveNavigationAsync(false);
    private async void NavigateForward(object sender, RoutedEventArgs e) => await MoveNavigationAsync(true);
    private Task MoveNavigationAsync(bool forward) => model.RunAsync(async () =>
    {
        CaptureCurrentNavigation();
        var target = forward ? navigation.Forward : navigation.Back;
        if (target is null) return;
        restoringNavigation = true;
        try
        {
            var tab = model.Documents.FirstOrDefault(d => d.Id == target.DocumentId);
            if (tab is not null) model.ActiveDocument = tab;
            else if (target.Path is not null) await model.OpenPathAsync(target.Path);
            else { model.Status = "This unsaved document has been closed."; return; }
            BindActiveDocument();
            Editor.CaretOffset = Math.Clamp(target.CaretOffset, 0, Editor.Document.TextLength);
            Editor.ScrollToVerticalOffset(Math.Max(0, target.VerticalOffset));
            navigation.CompleteMove(forward, CurrentNavigation()!);
            await UpdatePreviewAsync();
            if (rendered is not null)
            {
                var nearest = rendered.SourceElements.Where(e => e.Key <= Editor.CaretOffset).OrderByDescending(e => e.Key).FirstOrDefault();
                rendered.RevealSource(Editor.CaretOffset);
                nearest.Value?.BringIntoView();
            }
            Editor.Focus();
        }
        finally { restoringNavigation = false; RefreshNavigationControls(); }
    });

    private async void InsertDocumentLink(object sender, RoutedEventArgs e)
    {
        var tab = model.ActiveDocument;
        await model.RunAsync(async () =>
        {
            if (tab is null || !model.Documents.Contains(tab)) return;
            if (tab.FilePath is null && !await model.SaveTabAsync(tab, false)) return;
            var insertion = new DocumentInsertion(tab.EditorDocument, model.ActiveDocument == tab ? Editor.SelectionStart : tab.CaretOffset,
                model.ActiveDocument == tab ? Editor.SelectionLength : 0);
            var workspace = new WorkspacePath(tab.WorkspaceRoot ?? Path.GetDirectoryName(tab.FilePath!)!);
            var listing = await new WorkspaceService().EnumerateAsync(workspace, model.OperationToken);
            var edited = model.Documents.Where(d => d.FilePath is not null).ToDictionary(d => d.FilePath!, d => d.EditorDocument.Text, StringComparer.OrdinalIgnoreCase);
            var picker = new DocumentLinkWindow(workspace, tab.FilePath!, listing.Entries, Path.Combine(model.StateDirectory, "write-recovery"), edited) { Owner = this };
            if (picker.ShowDialog() != true || picker.MarkdownLink is not { } text) return;
            var vertical = Editor.VerticalOffset; var horizontal = Editor.HorizontalOffset;
            var caret = insertion.Insert(text);
            if (model.ActiveDocument == tab)
            {
                Editor.CaretOffset = caret;
                Editor.ScrollToVerticalOffset(vertical); Editor.ScrollToHorizontalOffset(horizontal); Editor.Focus();
            }
            model.Status = "Workspace document link inserted";
        });
    }
}
