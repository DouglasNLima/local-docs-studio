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
    private readonly PreviewImageService previewImages = new();
    private readonly NativeMathService previewMaths = new();
    private readonly EditorCompletionService completion;
    private readonly MarkdownHighlighting editorHighlighting = new() { HighContrast = SystemParameters.HighContrast };
    private readonly SemaphoreSlim previewGate = new(1, 1);
    private readonly DispatcherTimer previewTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private readonly DispatcherTimer recoveryTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private CancellationTokenSource? previewCancellation;
    private PreviewResult? rendered;
    private ParsedDocument? parsed;
    private Guid? renderedDocumentId;
    private DocumentViewModel? displayedTab;
    private bool previewFocused;
    private bool searchPreview;
    private bool closing;
    private bool closePending;
    private bool recoveryBusy;

    public MainWindow(WorkspaceViewModel model, DesktopDialogs dialogs, string[] startupPaths)
    {
        this.model = model; this.dialogs = dialogs; this.startupPaths = startupPaths;
        InitializeComponent(); DataContext = model;
        InitialiseArtifactContext();
        completion = new(Editor, () => Path.GetExtension(model.ActiveDocument?.FilePath)?.ToLowerInvariant() is ".mmd" or ".mermaid", WikiCompletionCatalogue);
        Closed += (_, _) => completion.Dispose();
        WindowStateService.FitInitialWindow(this);
        SystemEvents.UserPreferenceChanged += WindowsPreferenceChanged;
        Closed += (_, _) => SystemEvents.UserPreferenceChanged -= WindowsPreferenceChanged;
        Editor.TextArea.TextView.LineTransformers.Insert(0, editorHighlighting);
        Editor.TextArea.Caret.PositionChanged += (_, _) => CaretStatus.Text = $"Ln {Editor.TextArea.Caret.Line}, Col {Editor.TextArea.Caret.Column} · UTF-8";
        DataObject.AddPastingHandler(Editor, OnPaste);
        CommandManager.AddPreviewCanExecuteHandler(Editor, CanPasteClipboard);
        CommandManager.AddPreviewExecutedHandler(Editor, ExecuteClipboardPaste);
        model.DocumentChanged += (_, _) => { BindActiveDocument(); QueuePreview(); recoveryTimer.Start(); };
        model.DocumentRelocated += (id, path) => navigation.RelocateDocument(id, path);
        model.DocumentRemoved += id => { navigation.ForgetDocument(id); RefreshNavigationControls(); };
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
    private void WindowsPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (Dispatcher.HasShutdownStarted) return;
        Dispatcher.BeginInvoke(() =>
        {
            if (!closing) { ApplyTheme(); QueuePreview(); }
        });
    }

    private async void WindowLoaded(object sender, RoutedEventArgs e)
    {
        await model.InitialiseAsync(startupPaths);
        WindowStateService.Restore(this, model.Preferences.Window);
        ApplyTheme(); ApplyReadingPreferences();
        SetLayout(model.Layout); BindActiveDocument(); QueuePreview(); Editor.Focus();
        InitialiseUpdates();
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
        SaveViewport(); CaptureCurrentNavigation(); displayedTab = model.ActiveDocument;
        Editor.Document = displayedTab?.EditorDocument ?? new TextDocument();
        Editor.IsEnabled = displayedTab is not null;
        if (displayedTab is not null)
        {
            Editor.CaretOffset = Math.Clamp(displayedTab.CaretOffset, 0, Editor.Document.TextLength);
            Editor.ScrollToVerticalOffset(displayedTab.VerticalOffset);
        }
        if (!restoringNavigation && CurrentNavigation() is { } location) navigation.Visit(location);
        RefreshNavigationControls();
    }
    private void TabChanged(object sender, SelectionChangedEventArgs e) { if (e.Source == DocumentTabs) { BindActiveDocument(); QueuePreview(); } }
    private void QueuePreview() { previewCancellation?.Cancel(); previewTimer.Stop(); previewTimer.Start(); }
    private async Task UpdatePreviewAsync(bool forcePreview = false)
    {
        previewTimer.Stop();
        if (model.ActiveDocument is not { } tab)
        { rendered = null; parsed = null; renderedDocumentId = null; Outline.ItemsSource = null; Preview.Document = new FlowDocument(); return; }
        var text = tab.EditorDocument.Text;
        var preparePreview = forcePreview || model.Layout != "Editor";
        var sourcePath = tab.EffectivePath; var workspaceRoot = tab.EffectiveWorkspace;
        previewCancellation?.Cancel(); previewCancellation?.Dispose(); previewCancellation = new();
        var cancellation = previewCancellation.Token;
        var dark = ThemeService.IsDark(model.Theme);
        var standalone = Path.GetExtension(tab.EffectivePath ?? tab.DisplayName)?.ToLowerInvariant() is ".mmd" or ".mermaid";
        var acquired = false;
        try
        {
            await previewGate.WaitAsync(cancellation); acquired = true;
            var watch = Stopwatch.StartNew();
            var prepared = await Task.Run(() =>
            {
                var source = markdown.Parse(text, standalone);
                var results = new Dictionary<FencedCodeBlock, DiagramResult>();
                foreach (var code in preparePreview ? source.Syntax.Descendants<FencedCodeBlock>() : [])
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (code.Info is "mermaid" or "mmd") results[code] = diagrams.Render(code.Lines.ToString(), dark);
                }
                return (source, results, syntax: preparePreview ? CodeSyntaxService.Prepare(source, cancellation) : null);
            }, cancellation);
            if (cancellation.IsCancellationRequested || model.ActiveDocument != tab) return;
            if (!preparePreview)
            {
                parsed = prepared.source; rendered = null; renderedDocumentId = null;
                Preview.Document = new FlowDocument(); Outline.ItemsSource = parsed.Headings;
                model.PreviewStatus = "Preview paused in Editor layout";
                return;
            }
            var nativeDrawings = await StaWorker.RunAsync(() =>
            {
                var drawings = new Dictionary<FencedCodeBlock, DrawingImage>();
                foreach (var (code, diagram) in prepared.results.ToArray())
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (!diagram.Success) continue;
                    try { drawings[code] = svg.Render(diagram.Svg!); }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    { prepared.results[code] = new(diagram.Source, null, "Native diagram rendering failed: " + ex.Message); }
                }
                var images = previewImages.Prepare(prepared.source, sourcePath, workspaceRoot, cancellation);
                var formulae = previewMaths.Prepare(prepared.source, dark, cancellation);
                return (drawings, images, formulae);
            }, cancellation);
            if (cancellation.IsCancellationRequested || model.ActiveDocument != tab) return;
            var completed = await new NativeMarkdownRenderer(svg).RenderAsync(prepared.source, dark, prepared.results, Navigate, ExportDiagram,
                nativeDrawings.drawings, CopyDocumentImage, nativeDrawings.images, cancellation, CopyCodeBlock, prepared.syntax,
                selection => _ = CopyDocumentAsync(false, selection, prepared.source.Source, tab.Id),
                selection => _ = ExportTableCsvAsync(selection, prepared.source.Source, tab.Id), nativeDrawings.formulae,
                target => NavigateWikiLink(tab.Id, prepared.source.Source, target));
            if (cancellation.IsCancellationRequested || model.ActiveDocument != tab) return;
            parsed = prepared.source; rendered = completed; renderedDocumentId = tab.Id;
            Preview.Document = rendered.Document;
            Outline.ItemsSource = parsed.Headings;
            model.PreviewStatus = model.Layout == "Editor" ? "Preview paused in Editor layout"
                : rendered.Warnings.Count != 0 ? rendered.Warnings[0] : $"Preview updated in {watch.ElapsedMilliseconds} ms";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is not OutOfMemoryException) { model.PreviewStatus = "Preview could not be updated: " + ex.Message; }
        finally { if (acquired) previewGate.Release(); }
    }

    private async void OpenTreeEntry(object sender, MouseButtonEventArgs e) => await OpenSelectedEntry();
    private async void TreeKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { e.Handled = true; await OpenSelectedEntry(); } }
    private Task OpenSelectedEntry() => WorkspaceTree.SelectedItem is WorkspaceEntryViewModel { IsDirectory: false } entry ? model.RunAsync(() => model.OpenPathAsync(entry.Path)) : Task.CompletedTask;
    private async Task RenameTreeEntryAsync()
    {
        if (WorkspaceTree.SelectedItem is WorkspaceEntryViewModel { IsDirectory: true } folder)
        { model.MoveFolderCommand.Execute(folder); return; }
        if (WorkspaceTree.SelectedItem is not WorkspaceEntryViewModel { IsDirectory: false } entry)
        { model.Status = "Select a document or folder to rename or move."; return; }
        var opened = false;
        await model.RunAsync(async () => { await model.OpenPathAsync(entry.Path); opened = true; });
        if (opened) model.MoveDocumentCommand.Execute(null);
    }
    private void EditorFocused(object sender, KeyboardFocusChangedEventArgs e) => previewFocused = false;
    private void PreviewFocused(object sender, KeyboardFocusChangedEventArgs e) => previewFocused = true;
    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        previewFocused = true;
        if (e.ClickCount != 2 || rendered is null) return;
        for (var element = e.OriginalSource as DependencyObject; element is not null; element = LogicalTreeHelper.GetParent(element))
            if (element is TextElement { Tag: SourceReference source }) { JumpToSource(source.Start); break; }
    }
    private void JumpToSource(int offset, bool focusEditor = true)
    {
        CaptureCurrentNavigation();
        Editor.CaretOffset = Math.Clamp(offset, 0, Editor.Document.TextLength);
        Editor.ScrollToLine(Editor.Document.GetLineByOffset(Editor.CaretOffset).LineNumber);
        if (focusEditor) Editor.Focus();
        VisitCurrentNavigation();
    }
    private void OutlineSelected(object sender, SelectionChangedEventArgs e)
    {
        if (Outline.SelectedItem is not DocumentHeading heading) return;
        if (parsed?.Source != Editor.Document.Text) { model.Status = "The outline is updating. Select the heading again shortly."; QueuePreview(); return; }
        JumpToSource(heading.SourceStart, focusEditor: model.Layout == "Editor");
        if (Preview.Visibility == Visibility.Visible && rendered?.SourceElements.TryGetValue(heading.SourceStart, out var element) == true)
        { rendered.RevealSource(heading.SourceStart); _ = PreviewNavigationService.BringIntoViewAsync(Preview, element.ContentStart); }
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
                var target = parsed is null ? null : DocumentAnchors.Find(parsed, fragment);
                if (target is not null)
                {
                    JumpToSource(target.SourceStart, focusEditor: model.Layout == "Editor");
                    if (Preview.Visibility == Visibility.Visible && rendered?.SourceElements.TryGetValue(target.SourceStart, out var element) == true)
                    { rendered.RevealSource(target.SourceStart); await PreviewNavigationService.BringIntoViewAsync(Preview, element.ContentStart); }
                }
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
            rendered?.ExpandDetails();
            var start = Preview.Selection.IsEmpty ? Preview.Document.ContentStart : Preview.Selection.End;
            if (!FindPreview(start, query) && !FindPreview(Preview.Document.ContentStart, query) && rendered is not null)
            {
                var match = rendered.Formulae.FirstOrDefault(formula => formula.Source.Contains(query, StringComparison.CurrentCultureIgnoreCase));
                if (match is not null)
                {
                    Preview.Selection.Select(match.Element.ElementStart, match.Element.ElementEnd); _ = PreviewNavigationService.BringIntoViewAsync(Preview, match.Element.ElementStart);
                    model.Status = "Matching formula selected; its LaTeX source is available from the context menu.";
                }
            }
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
            _ = PreviewNavigationService.BringIntoViewAsync(Preview, matchStart); return true;
        }
        model.Status = "No matches found."; return false;
    }
    private void ReplaceOne(object sender, RoutedEventArgs e)
    {
        if (Editor.IsReadOnly) return;
        if (string.Equals(Editor.SelectedText, FindText.Text, StringComparison.CurrentCultureIgnoreCase) && FindText.Text.Length != 0)
            Editor.Document.Replace(Editor.SelectionStart, Editor.SelectionLength, ReplaceText.Text);
        FindNext(sender, e);
    }
    private void Format(object sender, RoutedEventArgs e)
    {
        if (model.ActiveDocument is not { IsSaving: false }) return;
        var tag = (sender as FrameworkElement)?.Tag as string;
        if (tag is not null) EditorFormattingService.Apply(Editor, tag);
    }
    private void ShowCompletion(object sender, RoutedEventArgs e)
    {
        Editor.Focus();
        if (!completion.Show()) model.Status = "No Mermaid suggestions at this position. Completion supports up to 4 million characters.";
    }
    private void InsertText(string value)
    {
        if (Editor.IsReadOnly) return;
        var vertical = Editor.VerticalOffset; var horizontal = Editor.HorizontalOffset;
        var start = Editor.SelectionStart;
        using (Editor.Document.RunUpdate()) Editor.Document.Replace(start, Editor.SelectionLength, value);
        Editor.CaretOffset = start + value.Length;
        Editor.ScrollToVerticalOffset(vertical); Editor.ScrollToHorizontalOffset(horizontal); Editor.Focus();
    }
    private async void InsertImage(object sender, RoutedEventArgs e)
    {
        if (dialogs.OpenImage() is { } path) await AddImageFileAsync(path);
    }
    private async Task AddImageFileAsync(string path)
    {
        if (model.ActiveDocument is not { } tab) return;
        var insertion = new DocumentInsertion(tab.EditorDocument, Editor.SelectionStart, Editor.SelectionLength);
        try
        {
            WorkspacePath.RejectReparsePoints(path);
            byte[] bytes;
            await using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true))
            {
                if (input.Length > 32 * 1024 * 1024) throw new IOException("Images are limited to 32 MB.");
                bytes = new byte[checked((int)input.Length)];
                await input.ReadExactlyAsync(bytes);
            }
            await AddImageAsync(bytes, Path.GetFileName(path), tab, insertion);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { dialogs.Error(ex.Message); }
    }
    private async Task AddImageAsync(byte[] bytes, string name, DocumentViewModel? targetTab = null, DocumentInsertion? insertion = null)
    {
        var tab = targetTab ?? model.ActiveDocument;
        if (tab is null) return;
        insertion ??= new DocumentInsertion(tab.EditorDocument, Editor.SelectionStart, Editor.SelectionLength);
        await model.RunAsync(() => PersistImageAsync(bytes, name, tab, insertion));
    }
    private async void InsertRemoteImage(object sender, RoutedEventArgs e)
    {
        if (model.ActiveDocument is not { } tab) return;
        var insertion = new DocumentInsertion(tab.EditorDocument, Editor.SelectionStart, Editor.SelectionLength);
        var prompt = new TextPromptWindow("Insert image from URL", "HTTPS image address. This downloads the image when you choose Download and insert.", "https://", "Download and insert", "HTTPS image address", null) { Owner = this };
        if (prompt.ShowDialog() != true) return;
        await model.RunAsync(async () =>
        {
            if (!model.Documents.Contains(tab)) return;
            model.Status = "Downloading image…";
            using var client = RemoteImageService.CreateClient();
            var bytes = await new RemoteImageService(client).DownloadAsync(prompt.Value, model.OperationToken);
            await PersistImageAsync(bytes, "downloaded-image.png", tab, insertion);
        });
    }
    private async Task PersistImageAsync(byte[] bytes, string name, DocumentViewModel tab, DocumentInsertion insertion)
    {
        if (!model.Documents.Contains(tab)) { model.Status = "Image insertion cancelled because its document was closed."; return; }
        model.EnsureDraftWorkspace(tab);
        var root = tab.EffectiveWorkspace ?? Path.GetDirectoryName(tab.EffectivePath!)!;
        var reference = await assets.ImportAsync(root, tab.EffectivePath!, bytes, name, model.OperationToken);
        var value = "![" + Path.GetFileNameWithoutExtension(name).Replace("\\", "\\\\").Replace("[", "\\[").Replace("]", "\\]") + "](" + reference + ")";
        var vertical = Editor.VerticalOffset; var horizontal = Editor.HorizontalOffset;
        var caret = insertion.Insert(value);
        if (model.ActiveDocument == tab)
        {
            Editor.CaretOffset = caret;
            Editor.ScrollToVerticalOffset(vertical); Editor.ScrollToHorizontalOffset(horizontal); Editor.Focus();
        }
        await model.PersistSessionAsync();
        model.Status = tab.FilePath is null ? "Image retained with the recovered draft; Save will copy it beside the document." : "Portable image inserted";
    }
    private void CopyDocumentImage(BitmapSource bitmap)
    {
        try
        {
            Clipboard.SetImage(bitmap);
            model.Status = "Image copied. Paste it into a document to create a portable local asset.";
        }
        catch (System.Runtime.InteropServices.ExternalException) { model.Status = "The clipboard is busy. Try copying again."; }
    }
    private async Task PasteBitmapAsync(BitmapSource bitmap)
    {
        if (model.ActiveDocument is not { } tab) return;
        var insertion = new DocumentInsertion(tab.EditorDocument, Editor.SelectionStart, Editor.SelectionLength);
        try
        {
            if ((long)bitmap.PixelWidth * bitmap.PixelHeight > 64_000_000) throw new IOException("The clipboard image exceeds the 64 megapixel limit.");
            if (!bitmap.IsFrozen) { bitmap = bitmap.Clone(); bitmap.Freeze(); }
            var bytes = await StaWorker.RunAsync(() =>
            {
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var output = new MemoryStream(); encoder.Save(output); return output.ToArray();
            });
            await AddImageAsync(bytes, "pasted-image.png", tab, insertion);
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or ArgumentException or System.Runtime.InteropServices.ExternalException) { dialogs.Error(ex.Message); }
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
                await AddImageFileAsync(path);
        }
    }
    private async void ExportDiagram(string format, string value)
    {
        try
        {
            if (format == "Zoom") { new DiagramWindow(svg.Render(value), selected => ExportDiagram(selected, value)) { Owner = this }.Show(); return; }
            if (format == "Copy source") { Clipboard.SetText(value); model.Status = "Diagram source copied"; return; }
            var extension = format.ToLowerInvariant();
            var dialog = new SaveFileDialog { Filter = format + " diagram|*." + extension, FileName = "diagram." + extension };
            if (dialog.ShowDialog(this) != true) return;
            await model.RunAsync(async () =>
            {
                var expected = await LensDocsStudio.Core.Exports.ExportFileStore.ReadExistingAsync(dialog.FileName, model.OperationToken);
                var bytes = format == "SVG" ? System.Text.Encoding.UTF8.GetBytes(svg.Prepare(value)) : NativeSvgService.ToPng(svg.Render(value));
                await WriteExportAsync(dialog.FileName, bytes, expected, model.OperationToken);
                model.Status = format + " diagram exported";
            });
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Runtime.InteropServices.ExternalException) { dialogs.Error(ex.Message); }
    }
    private async Task<string?> PreparePreviewTextAsync()
    {
        if (model.ActiveDocument is not { } tab) return null;
        var source = tab.EditorDocument.Text;
        if (renderedDocumentId != tab.Id || parsed?.Source != source || rendered is null)
            await UpdatePreviewAsync(forcePreview: true);
        if (model.ActiveDocument != tab || tab.EditorDocument.Text != source || renderedDocumentId != tab.Id || parsed?.Source != source || rendered is null)
        { model.Status = "Current preview text is unavailable. Wait for the preview to update and copy again."; return null; }
        return rendered.ReadText();
    }
    private async void CopyPreview(object sender, RoutedEventArgs e)
    {
        try
        {
            var sequence = NativeClipboardService.Sequence;
            var text = await PreparePreviewTextAsync();
            if (text is null) return;
            if (NativeClipboardService.Sequence != sequence)
            { model.Status = "The clipboard changed while text was being prepared. Copy again to replace it."; return; }
            Clipboard.SetText(text); model.Status = "Preview text copied";
        }
        catch (System.Runtime.InteropServices.ExternalException) { model.Status = "The clipboard is busy. Try copying again."; }
    }
    private async void CopyRenderedContent(object sender, RoutedEventArgs e) => await CopyDocumentAsync(false);
    private async void CopyMarkdownWithImages(object sender, RoutedEventArgs e) => await CopyDocumentAsync(true);
    private async Task CopyDocumentAsync(bool markdownSource, LensDocsStudio.Core.Exports.ExportTableSelection? table = null, string? expectedSource = null, Guid? expectedDocument = null)
    {
        if (model.ActiveDocument is not { } tab) return;
        var source = tab.EditorDocument.Text;
        if (table is not null && (expectedDocument != tab.Id || expectedSource != source))
        { model.Status = "The preview is out of date. Wait for the updated table and copy again."; return; }
        var sourcePath = tab.EffectivePath; var workspace = tab.EffectiveWorkspace;
        var dark = ThemeService.IsDark(model.Theme);
        var sequence = NativeClipboardService.Sequence;
        await model.RunAsync(async () =>
        {
            model.Status = markdownSource ? "Preparing Markdown and clipboard images…" : "Preparing formatted clipboard content…";
            var content = await new Exports.ClipboardExportService().PrepareAsync(source, sourcePath, workspace, dark, model.OperationToken, markdownSource, table);
            model.OperationToken.ThrowIfCancellationRequested();
            NativeClipboardService.WriteRendered(content, sequence);
            model.Status = table is not null ? "Table copied as formatted content and tab-separated text."
                : markdownSource ? "Markdown copied with embedded images. Attachment handling depends on the destination."
                : "Rendered content copied as HTML and Unicode text. Images are embedded; destination formatting may vary.";
        });
    }
    private void RevealFile(object sender, RoutedEventArgs e)
    {
        if (model.ActiveDocument?.FilePath is { } path) Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, Arguments = "/select,\"" + path + "\"" });
    }
    private async void ChangeTheme(object sender, RoutedEventArgs e)
    {
        model.Theme = (string)((FrameworkElement)sender).Tag;
        ApplyTheme(); QueuePreview(); await SavePreferences();
    }
    private void ApplyTheme()
    {
        ThemeService.Apply(Application.Current.Resources, model.Theme);
        helpWindow?.ApplyTheme(model.Theme);
        editorHighlighting.Dark = ThemeService.IsDark(model.Theme); editorHighlighting.HighContrast = SystemParameters.HighContrast;
        Editor.TextArea.SelectionBrush = (Brush)Application.Current.Resources["AccentBrush"];
        Editor.TextArea.SelectionForeground = (Brush)Application.Current.Resources["AccentTextBrush"];
        Editor.TextArea.TextView.Redraw();
    }
    private async void ChangeLayout(object sender, RoutedEventArgs e) { SetLayout((string)((FrameworkElement)sender).Tag); await SavePreferences(); }
    private void SetLayout(string layout)
    {
        var changed = model.Layout != layout;
        model.Layout = layout;
        EditorColumn.Width = layout == "Preview" ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        PreviewColumn.Width = layout == "Editor" ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        SplitColumn.Width = layout == "Split" ? new GridLength(5) : new GridLength(0);
        Editor.Visibility = layout == "Preview" ? Visibility.Collapsed : Visibility.Visible;
        Preview.Visibility = layout == "Editor" ? Visibility.Collapsed : Visibility.Visible;
        if (layout != "Split") previewFocused = layout == "Preview";
        if (changed)
        {
            FindBar.Visibility = Visibility.Collapsed;
            QueuePreview();
        }
    }
    private async Task SavePreferences()
    {
        try { await model.UpdatePreferencesAsync(current => current with { Theme = model.Theme, Layout = model.Layout }); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { model.Status = "Preferences could not be saved: " + ex.Message; }
    }
    private void WindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F1 && Keyboard.Modifiers == ModifierKeys.None) { OpenHelp(sender, e); e.Handled = true; return; }
        if (e.Key == Key.F2 && Keyboard.Modifiers == ModifierKeys.None)
        { if (WorkspaceTree.IsKeyboardFocusWithin) _ = RenameTreeEntryAsync(); else model.MoveDocumentCommand.Execute(null); e.Handled = true; return; }
        if (e.Key == Key.Escape && FindBar.Visibility == Visibility.Visible) { HideFind(); e.Handled = true; return; }
        if (Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey is Key.Left or Key.Right)
        { _ = MoveNavigationAsync(e.SystemKey == Key.Right); e.Handled = true; return; }
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        var shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        switch (e.Key)
        {
            case Key.OemComma: OpenSettings(sender, e); break;
            case Key.F: ShowFind(false); break;
            case Key.H: ShowFind(true); break;
            case Key.S: (shift ? model.SaveAsCommand : model.SaveCommand).Execute(null); break;
            case Key.O: (shift ? model.OpenFolderCommand : model.OpenDocumentCommand).Execute(null); break;
            case Key.N: model.NewDocumentCommand.Execute(null); break;
            case Key.W: model.CloseDocumentCommand.Execute(model.ActiveDocument); break;
            case Key.K when shift: InsertDocumentLink(sender, e); break;
            case Key.V when shift && Editor.IsKeyboardFocusWithin: _ = PasteClipboardAsAsync(LensDocsStudio.Core.Imports.PasteMode.Text); break;
            case Key.B when Editor.IsKeyboardFocusWithin: Format(new Button { Tag = "bold" }, e); break;
            case Key.I when Editor.IsKeyboardFocusWithin: Format(new Button { Tag = "italic" }, e); break;
            case Key.Space when Editor.IsKeyboardFocusWithin: ShowCompletion(sender, e); break;
            default: return;
        }
        e.Handled = true;
    }
    private void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (closing) return;
        e.Cancel = true;
        if (closePending) return;
        if (model.IsBusy) { model.Status = "Wait for the current operation to finish, or cancel it before closing."; return; }
        closePending = true;
        // Leave the native Closing event before any nested dialogue or asynchronous save.
        Dispatcher.BeginInvoke(async () =>
        {
            IsEnabled = false;
            try
            {
                SaveViewport();
                if (!await model.PrepareCloseAsync()) return;
                await model.UpdatePreferencesAsync(p => p with { Window = WindowStateService.Capture(this) });
                CompleteClose();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { dialogs.Error(ex.Message); }
            finally { closePending = false; IsEnabled = true; }
        });
    }
    private void Exit(object sender, RoutedEventArgs e) => Close();
    private void About(object sender, RoutedEventArgs e) => MessageBox.Show(this, "Lens Docs Studio — Native Preview\nLocal Markdown, Mermaid, and documentation studio.\n\n.NET 10 · WPF · AvalonEdit · Markdig · Mermaider\n\nInternal development build. Functional parity, installation and production signing are still being validated.", "About Lens Docs Studio");
}
