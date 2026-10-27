using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private readonly DispatcherTimer findTimer = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private CancellationTokenSource? findCancellation;
    private FindMatches? findMatches;
    private PreviewFindIndex? previewFindIndex;
    private int findCurrent = -1;
    private int? findReplacementAnchor;
    private long findGeneration;

    private void InitialiseFind()
    {
        findTimer.Tick += async (_, _) => { findTimer.Stop(); await RefreshFindAsync(); };
        Editor.TextChanged += (_, _) => InvalidateFind();
        Closed += (_, _) => { findTimer.Stop(); findCancellation?.Cancel(); };
    }
    private void OpenFind(object sender, RoutedEventArgs e) => ShowFind(false);
    private void OpenReplace(object sender, RoutedEventArgs e) => ShowFind(true);
    private void ShowFind(bool replace)
    {
        searchPreview = previewFocused && !replace;
        FindContext.Text = searchPreview ? "Find in preview" : "Find in editor";
        FindContext.ToolTip = searchPreview ? "Rendered text and formula source. Mermaid diagram labels are not searchable." : "Case-insensitive literal source text";
        ReplaceText.Visibility = ReplaceButton.Visibility = replace ? Visibility.Visible : Visibility.Collapsed;
        FindBar.Visibility = Visibility.Visible; FindText.Focus(); FindText.SelectAll();
        InvalidateFind();
    }
    private void CloseFind(object sender, RoutedEventArgs e) => HideFind();
    private void HideFind()
    {
        FindBar.Visibility = Visibility.Collapsed; InvalidateFind();
        if (searchPreview) Preview.Focus(); else Editor.Focus();
    }
    private void FindQueryChanged(object sender, TextChangedEventArgs e) => InvalidateFind();
    private void InvalidateFind()
    {
        findGeneration++; findTimer.Stop(); findCancellation?.Cancel();
        if (Preview is not null) PreviewNavigationService.CancelPending(Preview);
        if (Editor is not null) EditorNavigationService.CancelPending(Editor);
        findMatches = null; previewFindIndex = null; findCurrent = -1; findReplacementAnchor = null;
        if (FindCounter is null) return;
        FindCounter.Text = "0 of 0";
        FindCounter.ToolTip = "Enter text to find";
        if (FindBar.Visibility == Visibility.Visible && FindText.Text.Length > 0 && !closing)
        { FindCounter.ToolTip = "Updating matches…"; findTimer.Start(); }
    }
    private async Task<bool> RefreshFindAsync()
    {
        findTimer.Stop(); findCancellation?.Cancel();
        var cancellation = new CancellationTokenSource(); findCancellation = cancellation;
        var token = cancellation.Token; var generation = findGeneration;
        var query = FindText.Text; var preview = searchPreview;
        var document = Editor.Document; var version = document.Version; var flow = Preview.Document;
        try
        {
            if (query.Length == 0 || FindBar.Visibility != Visibility.Visible) return false;
            FindMatches matches; PreviewFindIndex? index = null;
            if (preview)
            {
                if (flow is null || model.ActiveDocument is not null && renderedDocumentId != model.ActiveDocument.Id)
                { FindCounter.ToolTip = "The preview is still updating."; return false; }
                rendered?.ExpandDetails();
                index = await PreviewFindIndex.CreateAsync(flow, rendered?.Formulae ?? [], token);
                matches = index.Complete ? await Task.Run(() => FindMatches.Collect(index.Text, query, token), token) : new([], query.Length, false);
            }
            else
            {
                var snapshot = document.CreateSnapshot();
                matches = await Task.Run(() => FindMatches.Collect(snapshot.Text, query, token), token);
            }
            if (token.IsCancellationRequested || generation != findGeneration || preview != searchPreview
                || document != Editor.Document || version.CompareAge(document.Version) != 0 || preview && flow != Preview.Document) return false;
            findMatches = matches; previewFindIndex = index;
            findCurrent = findReplacementAnchor is { } anchor ? matches.AtOrAfter(anchor) : matches.Move(-1, 1);
            findReplacementAnchor = null;
            UpdateFindCounter();
            await RevealFindAsync();
            return true;
        }
        catch (OperationCanceledException) { return false; }
        finally { if (ReferenceEquals(findCancellation, cancellation)) findCancellation = null; cancellation.Dispose(); }
    }
    private void UpdateFindCounter()
    {
        var count = findMatches?.Offsets.Count ?? 0;
        FindCounter.Text = $"{(findCurrent >= 0 && findCurrent < count ? findCurrent + 1 : 0)} of {count}";
        FindCounter.ToolTip = findMatches?.Complete == false
            ? "Search limit reached. Use a shorter document or a more specific query; no partial count is shown."
            : count == 0 ? "No matches found." : searchPreview ? "Rendered text and formula source; diagram labels are excluded." : "Matches in editor source";
        if (findMatches?.Complete == false) model.Status = (string)FindCounter.ToolTip;
    }
    private Task RevealFindAsync()
    {
        if (findMatches is null || findCurrent < 0) return Task.CompletedTask;
        var offset = findMatches.Offsets[findCurrent];
        if (searchPreview && previewFindIndex is { } index)
        {
            var range = index.Range(offset, findMatches.Length);
            if (range.Formula) model.Status = "Matching formula source; its LaTeX is available from the context menu.";
            return PreviewNavigationService.BringIntoViewAsync(Preview, range.Start, range.End);
        }
        Editor.Select(offset, findMatches.Length);
        return EditorNavigationService.BringIntoViewAsync(Editor, offset);
    }
    private async Task NavigateFindAsync(int direction)
    {
        if (findMatches is null) { await RefreshFindAsync(); return; }
        findCurrent = findMatches.Move(findCurrent, direction); UpdateFindCounter(); await RevealFindAsync();
    }
    private async void FindNext(object sender, RoutedEventArgs e) => await NavigateFindAsync(1);
    private async void FindPrevious(object sender, RoutedEventArgs e) => await NavigateFindAsync(-1);
    private async void FindKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        { e.Handled = true; await NavigateFindAsync((Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? -1 : 1); }
        else if (e.Key == Key.Escape) { e.Handled = true; HideFind(); }
    }
    private async void ReplaceOne(object sender, RoutedEventArgs e)
    {
        if (Editor.IsReadOnly || searchPreview) return;
        if (string.Equals(Editor.SelectedText, FindText.Text, StringComparison.OrdinalIgnoreCase) && FindText.Text.Length != 0)
        {
            var anchor = Editor.SelectionStart + ReplaceText.Text.Length;
            Editor.Document.Replace(Editor.SelectionStart, Editor.SelectionLength, ReplaceText.Text);
            findReplacementAnchor = anchor;
            await RefreshFindAsync();
        }
        else await NavigateFindAsync(1);
    }
}
