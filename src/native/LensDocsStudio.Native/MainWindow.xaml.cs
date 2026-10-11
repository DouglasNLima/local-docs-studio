using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Document;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;
using LensDocsStudio.Native.ViewModels;
using Markdig.Syntax;
using Microsoft.Win32;

namespace LensDocsStudio.Native;

public partial class MainWindow : Window
{
    private readonly WorkspaceViewModel model;
    private readonly DesktopDialogs dialogs;
    private readonly string[] startupPaths;
    private readonly MarkdownService markdown = new();
    private readonly DiagramService diagrams = new();
    private readonly NativeSvgService svg = new();
    private readonly ImageAssetService assets = new();
    private readonly DispatcherTimer previewTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private readonly DispatcherTimer recoveryTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private CancellationTokenSource? previewCancellation;
    private PreviewResult? rendered;
    private ParsedDocument? parsed;
    private DocumentViewModel? displayedTab;
    private bool previewFocused;
    private bool searchPreview;
    private bool closing;
    private bool recoveryBusy;

    public MainWindow(WorkspaceViewModel model, DesktopDialogs dialogs, string[] startupPaths)
    {
        this.model = model; this.dialogs = dialogs; this.startupPaths = startupPaths;
        InitializeComponent(); DataContext = model;
        Editor.TextArea.TextView.LineTransformers.Add(new MarkdownHighlighting());
        Editor.TextArea.Caret.PositionChanged += (_, _) => CaretStatus.Text = $"Ln {Editor.TextArea.Caret.Line}, Col {Editor.TextArea.Caret.Column} · UTF-8";
        DataObject.AddPastingHandler(Editor, OnPaste);
        model.DocumentChanged += (_, _) => { BindActiveDocument(); QueuePreview(); recoveryTimer.Start(); };
        previewTimer.Tick += async (_, _) => { previewTimer.Stop(); await UpdatePreviewAsync(); };
        recoveryTimer.Tick += async (_, _) =>
        {
            if (recoveryBusy) return;
            recoveryTimer.Stop(); recoveryBusy = true;
            try { SaveViewport(); await model.PersistSessionAsync(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { model.Status = "Recovery could not be saved: " + ex.Message; }
            finally { recoveryBusy = false; }
        };
    }

    private async void WindowLoaded(object sender, RoutedEventArgs e)
    {
        await model.InitialiseAsync(startupPaths);
        ThemeService.Apply(Application.Current.Resources, model.Theme);
        SetLayout(model.Layout); BindActiveDocument(); QueuePreview(); Editor.Focus();
    }
    private void SaveViewport()
    {
        if (displayedTab is null) return;
        displayedTab.CaretOffset = Editor.CaretOffset;
        displayedTab.VerticalOffset = Editor.VerticalOffset;
    }
    private void BindActiveDocument()
    {
        if (Editor is null || displayedTab == model.ActiveDocument) return;
        SaveViewport(); displayedTab = model.ActiveDocument;
        Editor.Document = displayedTab?.EditorDocument ?? new TextDocument();
        Editor.IsEnabled = displayedTab is not null;
        if (displayedTab is not null)
        {
            Editor.CaretOffset = Math.Clamp(displayedTab.CaretOffset, 0, Editor.Document.TextLength);
            Editor.ScrollToVerticalOffset(displayedTab.VerticalOffset);
        }
    }
    private void TabChanged(object sender, SelectionChangedEventArgs e) { if (e.Source == DocumentTabs) { BindActiveDocument(); QueuePreview(); } }
    private void QueuePreview() { previewCancellation?.Cancel(); previewTimer.Stop(); previewTimer.Start(); }
    private async Task UpdatePreviewAsync()
    {
        if (model.ActiveDocument is not { } tab) { Preview.Document = new FlowDocument(); return; }
        var text = tab.EditorDocument.Text;
        previewCancellation?.Cancel(); previewCancellation?.Dispose(); previewCancellation = new();
        var cancellation = previewCancellation.Token;
        var dark = ThemeService.IsDark(model.Theme);
        var standalone = Path.GetExtension(tab.FilePath)?.ToLowerInvariant() is ".mmd" or ".mermaid";
        try
        {
            var watch = Stopwatch.StartNew();
            var prepared = await Task.Run(() =>
            {
                var source = markdown.Parse(text, standalone);
                var results = new Dictionary<FencedCodeBlock, DiagramResult>();
                foreach (var code in source.Syntax.Descendants<FencedCodeBlock>())
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (code.Info is "mermaid" or "mmd") results[code] = diagrams.Render(code.Lines.ToString(), dark);
                }
                return (source, results);
            }, cancellation);
            if (cancellation.IsCancellationRequested || model.ActiveDocument != tab) return;
            parsed = prepared.source;
            rendered = new NativeMarkdownRenderer(svg).Render(parsed, tab.FilePath, tab.WorkspaceRoot, dark, prepared.results, Navigate, ExportDiagram);
            Preview.Document = rendered.Document;
            Outline.ItemsSource = parsed.Headings;
            if (rendered.Warnings.Count != 0) model.Status = rendered.Warnings[0];
            else if (!tab.ExternalChange) model.Status = $"Preview updated in {watch.ElapsedMilliseconds} ms";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is not OutOfMemoryException) { model.Status = "Preview could not be updated: " + ex.Message; }
    }

    private async void OpenTreeEntry(object sender, MouseButtonEventArgs e) => await OpenSelectedEntry();
    private async void TreeKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { e.Handled = true; await OpenSelectedEntry(); } }
    private Task OpenSelectedEntry() => WorkspaceTree.SelectedItem is WorkspaceEntry { IsDirectory: false } entry ? model.RunAsync(() => model.OpenPathAsync(entry.Path)) : Task.CompletedTask;
    private void EditorFocused(object sender, KeyboardFocusChangedEventArgs e) => previewFocused = false;
    private void PreviewFocused(object sender, KeyboardFocusChangedEventArgs e) => previewFocused = true;
    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        previewFocused = true;
        if (e.ClickCount != 2 || rendered is null) return;
        for (var element = e.OriginalSource as DependencyObject; element is not null; element = LogicalTreeHelper.GetParent(element))
            if (element is TextElement { Tag: SourceReference source }) { JumpToSource(source.Start); break; }
    }
    private void JumpToSource(int offset)
    {
        Editor.CaretOffset = Math.Clamp(offset, 0, Editor.Document.TextLength);
        Editor.ScrollToLine(Editor.Document.GetLineByOffset(Editor.CaretOffset).LineNumber); Editor.Focus();
    }
    private void OutlineSelected(object sender, SelectionChangedEventArgs e)
    {
        if (Outline.SelectedItem is DocumentHeading heading && rendered?.SourceElements.TryGetValue(heading.SourceStart, out var element) == true)
        { element.BringIntoView(); Editor.CaretOffset = Math.Clamp(heading.SourceStart, 0, Editor.Document.TextLength); }
    }
    private async void Navigate(string reference)
    {
        await model.RunAsync(async () =>
        {
            var fragment = reference.Contains('#') ? Uri.UnescapeDataString(reference.Split('#', 2)[1]) : null;
            if (!reference.StartsWith('#'))
            {
                if (Uri.TryCreate(reference, UriKind.Absolute, out var uri))
                {
                    if (uri.Scheme is "https" or "http" or "mailto")
                        if (dialogs.Confirm("Open this external link in its default application?\n\n" + reference, "External link")) Process.Start(new ProcessStartInfo(reference) { UseShellExecute = true });
                    return;
                }
                if (model.ActiveDocument is not { FilePath: not null, WorkspaceRoot: not null } tab) throw new IOException("Open a workspace to follow relative links.");
                var path = new WorkspacePath(tab.WorkspaceRoot).ResolveLink(tab.FilePath, reference);
                if (!WorkspaceService.IsDocument(path)) throw new IOException("This link is not a supported document.");
                await model.OpenPathAsync(path); BindActiveDocument(); await UpdatePreviewAsync();
            }
            if (fragment is not null)
            {
                var heading = parsed?.Headings.FirstOrDefault(h => h.Anchor == fragment);
                if (heading is not null && rendered?.SourceElements.TryGetValue(heading.SourceStart, out var element) == true) element.BringIntoView();
                else model.Status = "The linked section was not found; the document remains open.";
            }
        });
    }
    private void OpenFind(object sender, RoutedEventArgs e) => ShowFind(false);
    private void OpenReplace(object sender, RoutedEventArgs e) => ShowFind(true);
    private void ShowFind(bool replace)
    {
        searchPreview = previewFocused && !replace;
        FindContext.Text = searchPreview ? "Find in preview" : "Find in editor";
        ReplaceText.Visibility = ReplaceButton.Visibility = replace ? Visibility.Visible : Visibility.Collapsed;
        FindBar.Visibility = Visibility.Visible; FindText.Focus(); FindText.SelectAll();
    }
    private void CloseFind(object sender, RoutedEventArgs e) => HideFind();
    private void HideFind() { FindBar.Visibility = Visibility.Collapsed; if (searchPreview) Preview.Focus(); else Editor.Focus(); }
    private void FindKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { FindNext(sender, e); e.Handled = true; } else if (e.Key == Key.Escape) { HideFind(); e.Handled = true; } }
    private void FindNext(object sender, RoutedEventArgs e)
    {
        var query = FindText.Text;
        if (query.Length == 0) return;
        if (searchPreview)
        {
            var start = Preview.Selection.IsEmpty ? Preview.Document.ContentStart : Preview.Selection.End;
            if (!FindPreview(start, query)) FindPreview(Preview.Document.ContentStart, query);
            return;
        }
        var text = Editor.Document.Text;
        var offset = text.IndexOf(query, Math.Min(Editor.SelectionStart + Editor.SelectionLength, text.Length), StringComparison.CurrentCultureIgnoreCase);
        if (offset < 0) offset = text.IndexOf(query, StringComparison.CurrentCultureIgnoreCase);
        if (offset >= 0) { Editor.Select(offset, query.Length); Editor.ScrollToLine(Editor.Document.GetLineByOffset(offset).LineNumber); }
        else model.Status = "No matches found.";
    }
    private bool FindPreview(TextPointer start, string query)
    {
        for (var pointer = start; pointer is not null && pointer.CompareTo(Preview.Document.ContentEnd) < 0; pointer = pointer.GetNextContextPosition(LogicalDirection.Forward))
        {
            if (pointer.GetPointerContext(LogicalDirection.Forward) != TextPointerContext.Text) continue;
            var run = pointer.GetTextInRun(LogicalDirection.Forward);
            var index = run.IndexOf(query, StringComparison.CurrentCultureIgnoreCase);
            if (index < 0) continue;
            var matchStart = pointer.GetPositionAtOffset(index)!;
            Preview.Selection.Select(matchStart, matchStart.GetPositionAtOffset(query.Length)!);
            (matchStart.Parent as FrameworkContentElement)?.BringIntoView(); return true;
        }
        model.Status = "No matches found."; return false;
    }
    private void ReplaceOne(object sender, RoutedEventArgs e)
    {
        if (string.Equals(Editor.SelectedText, FindText.Text, StringComparison.CurrentCultureIgnoreCase) && FindText.Text.Length != 0)
            Editor.Document.Replace(Editor.SelectionStart, Editor.SelectionLength, ReplaceText.Text);
        FindNext(sender, e);
    }
    private void Format(object sender, RoutedEventArgs e)
    {
        var tag = (sender as FrameworkElement)?.Tag as string;
        var selected = Editor.SelectedText;
        var value = tag switch
        {
            "bold" => "**" + (selected.Length == 0 ? "bold text" : selected) + "**",
            "italic" => "*" + (selected.Length == 0 ? "italic text" : selected) + "*",
            "heading" => "## " + (selected.Length == 0 ? "Heading" : selected),
            "link" => "[" + (selected.Length == 0 ? "Link text" : selected) + "](document.md)",
            "table" => "| Column | Description |\n|---|---|\n| Value | Details |\n",
            "task" => "- [ ] " + (selected.Length == 0 ? "Task" : selected),
            "code" => "```text\n" + selected + "\n```\n",
            "mermaid" => "```mermaid\nflowchart LR\n  A[Draft] --> B[Review]\n  B --> C[Publish]\n```\n",
            _ => selected
        };
        InsertText(value);
    }
    private void InsertText(string value)
    {
        var vertical = Editor.VerticalOffset; var horizontal = Editor.HorizontalOffset;
        var start = Editor.SelectionStart;
        using (Editor.Document.RunUpdate()) Editor.Document.Replace(start, Editor.SelectionLength, value);
        Editor.CaretOffset = start + value.Length;
        Editor.ScrollToVerticalOffset(vertical); Editor.ScrollToHorizontalOffset(horizontal); Editor.Focus();
    }
    private async void InsertImage(object sender, RoutedEventArgs e)
    {
        if (dialogs.OpenImage() is { } path) await AddImageAsync(await File.ReadAllBytesAsync(path), Path.GetFileName(path));
    }
    private async Task AddImageAsync(byte[] bytes, string name)
    {
        await model.RunAsync(async () =>
        {
            if (model.ActiveDocument is not { } tab) return;
            if (tab.FilePath is null && !await model.SaveTabAsync(tab, false)) return;
            var root = tab.WorkspaceRoot ?? Path.GetDirectoryName(tab.FilePath!)!;
            var reference = await assets.ImportAsync(root, tab.FilePath!, bytes, name);
            if (model.ActiveDocument == tab) InsertText("![" + Path.GetFileNameWithoutExtension(name).Replace("]", "\\]") + "](" + reference + ")");
        });
    }
    private async void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(DataFormats.Bitmap)) return;
        if (e.DataObject.GetData(DataFormats.Bitmap) is not BitmapSource bitmap) return;
        e.CancelCommand();
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = new MemoryStream(); encoder.Save(output);
        await AddImageAsync(output.ToArray(), "pasted-image.png");
    }
    private void EditorDragOver(object sender, DragEventArgs e) { if (e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Effects = DragDropEffects.Copy; e.Handled = true; } }
    private async void EditorDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        e.Handled = true;
        foreach (var path in paths)
        {
            if (WorkspaceService.IsDocument(path)) await model.RunAsync(() => model.OpenPathAsync(path));
            else if (Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp")
                await AddImageAsync(await File.ReadAllBytesAsync(path), Path.GetFileName(path));
        }
    }
    private void ExportDiagram(string format, string value)
    {
        try
        {
            if (format == "Copy source") { Clipboard.SetText(value); model.Status = "Diagram source copied"; return; }
            var extension = format.ToLowerInvariant();
            var dialog = new SaveFileDialog { Filter = format + " diagram|*." + extension, FileName = "diagram." + extension };
            if (dialog.ShowDialog(this) != true) return;
            if (format == "SVG") File.WriteAllText(dialog.FileName, svg.Prepare(value));
            else File.WriteAllBytes(dialog.FileName, NativeSvgService.ToPng(svg.Render(value)));
            model.Status = format + " diagram exported";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Runtime.InteropServices.ExternalException) { dialogs.Error(ex.Message); }
    }
    private void CopyPreview(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(new TextRange(Preview.Document.ContentStart, Preview.Document.ContentEnd).Text); model.Status = "Preview text copied"; }
        catch (System.Runtime.InteropServices.ExternalException) { model.Status = "The clipboard is busy. Try copying again."; }
    }
    private void RevealFile(object sender, RoutedEventArgs e)
    {
        if (model.ActiveDocument?.FilePath is { } path) Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, Arguments = "/select,\"" + path + "\"" });
    }
    private async void ChangeTheme(object sender, RoutedEventArgs e)
    {
        model.Theme = (string)((FrameworkElement)sender).Tag;
        ThemeService.Apply(Application.Current.Resources, model.Theme); QueuePreview(); await SavePreferences();
    }
    private async void ChangeLayout(object sender, RoutedEventArgs e) { SetLayout((string)((FrameworkElement)sender).Tag); await SavePreferences(); }
    private void SetLayout(string layout)
    {
        model.Layout = layout;
        EditorColumn.Width = layout == "Preview" ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        PreviewColumn.Width = layout == "Editor" ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        SplitColumn.Width = layout == "Split" ? new GridLength(5) : new GridLength(0);
        Editor.Visibility = layout == "Preview" ? Visibility.Collapsed : Visibility.Visible;
        Preview.Visibility = layout == "Editor" ? Visibility.Collapsed : Visibility.Visible;
    }
    private async Task SavePreferences()
    {
        try { await new SessionStore(model.StateDirectory).WriteAsync("preferences.json", new UserPreferences(Theme: model.Theme, Layout: model.Layout)); }
        catch (IOException ex) { model.Status = "Preferences could not be saved: " + ex.Message; }
    }
    private void WindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && FindBar.Visibility == Visibility.Visible) { HideFind(); e.Handled = true; return; }
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        var shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        switch (e.Key)
        {
            case Key.F: ShowFind(false); break;
            case Key.H: ShowFind(true); break;
            case Key.S: (shift ? model.SaveAsCommand : model.SaveCommand).Execute(null); break;
            case Key.O: (shift ? model.OpenFolderCommand : model.OpenDocumentCommand).Execute(null); break;
            case Key.N: model.NewDocumentCommand.Execute(null); break;
            case Key.W: model.CloseDocumentCommand.Execute(model.ActiveDocument); break;
            case Key.B when Editor.IsKeyboardFocusWithin: Format(new Button { Tag = "bold" }, e); break;
            case Key.I when Editor.IsKeyboardFocusWithin: Format(new Button { Tag = "italic" }, e); break;
            default: return;
        }
        e.Handled = true;
    }
    private async void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (closing) return;
        e.Cancel = true;
        try
        {
            SaveViewport();
            if (!await model.PrepareCloseAsync()) return;
            closing = true; previewCancellation?.Cancel(); previewTimer.Stop(); recoveryTimer.Stop(); model.Dispose(); Close();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { dialogs.Error(ex.Message); }
    }
    private void Exit(object sender, RoutedEventArgs e) => Close();
    private void About(object sender, RoutedEventArgs e) => MessageBox.Show(this, "Lens Docs Studio — Native Preview\nLocal Markdown, Mermaid, and documentation studio.\n\n.NET 10 · WPF · AvalonEdit · Markdig · Mermaider\n\nInternal development build. Functional parity, installation and production signing are still being validated.", "About Lens Docs Studio");
}
