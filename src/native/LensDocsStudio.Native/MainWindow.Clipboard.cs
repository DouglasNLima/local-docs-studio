using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using LensDocsStudio.Core.Imports;
using LensDocsStudio.Native.Editor;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private void PrepareEditorContext(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.ContextMenu menu) EditorContextCommands.Prepare(menu, Editor, model.ActiveDocument is not null);
    }
    private void CopyCodeBlock(Rendering.PreviewTextCopy content)
    {
        if (content.Text.Length > Rendering.CodeContextMenu.MaximumCopyCharacters)
        { model.Status = "Copy supports code blocks up to 4 million characters."; return; }
        try
        {
            Clipboard.SetText(content.Text, TextDataFormat.UnicodeText);
            model.Status = content.Markdown ? "Code block Markdown source copied." : "Code copied as plain text.";
        }
        catch (System.Runtime.InteropServices.ExternalException) { model.Status = "The clipboard is busy. Try copying again."; }
    }
    private static ClipboardPayload ReadClipboardText(IDataObject data)
    {
        var text = data.GetDataPresent(DataFormats.UnicodeText, false) ? data.GetData(DataFormats.UnicodeText, false) as string : data.GetData(DataFormats.Text) as string;
        var html = data.GetDataPresent(DataFormats.Html, false) ? data.GetData(DataFormats.Html, false) as string : null;
        return new(text ?? "", html ?? "");
    }
    private static bool NeedsConversion(ClipboardPayload payload) => payload.Html.Length > 0 || payload.Text.Contains('\t')
        || payload.Text.Contains("data:image/", StringComparison.OrdinalIgnoreCase);

    private async void PasteSpecial(object sender, RoutedEventArgs e)
        => await PasteClipboardAsAsync(Enum.Parse<PasteMode>((string)((FrameworkElement)sender).Tag));

    private async Task PasteClipboardAsAsync(PasteMode mode)
    {
        if (Editor.IsReadOnly || model.ActiveDocument is null) return;
        try
        {
            if (Clipboard.GetDataObject() is not { } data) { model.Status = "The clipboard is empty."; return; }
            await PasteTextAsync(ReadClipboardText(data), mode);
        }
        catch (System.Runtime.InteropServices.ExternalException) { model.Status = "The clipboard is busy. Try pasting again."; }
    }

    private async void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (Editor.IsReadOnly) { e.CancelCommand(); return; }
        try
        {
            var payload = ReadClipboardText(e.DataObject);
            if (NeedsConversion(payload)) { e.CancelCommand(); await PasteTextAsync(payload, PasteMode.Auto); }
            else if (payload.Text.Length == 0 && e.DataObject.GetData(DataFormats.Bitmap) is BitmapSource bitmap)
            { e.CancelCommand(); await PasteBitmapAsync(bitmap); }
        }
        catch (System.Runtime.InteropServices.ExternalException) { e.CancelCommand(); model.Status = "The clipboard is busy. Try pasting again."; }
    }
    private void CanPasteClipboard(object sender, CanExecuteRoutedEventArgs e)
    {
        if (e.Command != ApplicationCommands.Paste || Editor.IsReadOnly) return;
        try
        {
            if (Clipboard.ContainsImage() || Clipboard.ContainsData(DataFormats.Html)) { e.CanExecute = true; e.Handled = true; }
        }
        catch (System.Runtime.InteropServices.ExternalException) { }
    }
    private async void ExecuteClipboardPaste(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Command != ApplicationCommands.Paste || Editor.IsReadOnly) return;
        try
        {
            if (Clipboard.GetDataObject() is not { } data) return;
            var payload = ReadClipboardText(data);
            if (NeedsConversion(payload)) { e.Handled = true; await PasteTextAsync(payload, PasteMode.Auto); }
            else if (payload.Text.Length == 0 && data.GetData(DataFormats.Bitmap) is BitmapSource bitmap)
            { e.Handled = true; await PasteBitmapAsync(bitmap); }
        }
        catch (System.Runtime.InteropServices.ExternalException) { e.Handled = true; model.Status = "The clipboard is busy. Try pasting again."; }
    }
    private async Task PasteTextAsync(ClipboardPayload payload, PasteMode mode)
    {
        if (model.ActiveDocument is not { } tab || Editor.IsReadOnly) return;
        if (payload.Text.Length == 0 && payload.Html.Length == 0) { model.Status = "The clipboard contains no text or HTML for this paste format."; return; }
        var insertion = new DocumentInsertion(tab.EditorDocument, Editor.SelectionStart, Editor.SelectionLength);
        await model.RunAsync(async () =>
        {
            model.Status = "Preparing clipboard content…";
            var result = await Task.Run(() => new ClipboardPasteService().Convert(payload, mode, model.OperationToken), model.OperationToken);
            if (!model.Documents.Contains(tab)) { model.Status = "Paste cancelled because its document was closed."; return; }
            var source = result.Document.Markdown;
            if (result.Document.Assets.Count > 0)
            {
                model.EnsureDraftWorkspace(tab);
                var root = tab.EffectiveWorkspace ?? Path.GetDirectoryName(tab.EffectivePath!)!;
                // Persist and validate every image before changing the source or selected text.
                foreach (var asset in result.Document.Assets)
                {
                    var reference = await assets.ImportAsync(root, tab.EffectivePath!, asset.Bytes, asset.SuggestedName, model.OperationToken);
                    source = source.Replace(asset.Reference, reference, StringComparison.Ordinal);
                }
            }
            model.OperationToken.ThrowIfCancellationRequested();
            if (source.Length == 0 || result.Block && string.IsNullOrWhiteSpace(source)) { model.Status = "No supported clipboard content was found; the selection was retained."; return; }
            var vertical = Editor.VerticalOffset; var horizontal = Editor.HorizontalOffset;
            var caret = insertion.Insert(source, result.Block);
            if (model.ActiveDocument == tab)
            {
                Editor.CaretOffset = caret;
                Editor.ScrollToVerticalOffset(vertical); Editor.ScrollToHorizontalOffset(horizontal); Editor.Focus();
            }
            await model.PersistSessionAsync();
            model.Status = result.Status + (result.Document.Warnings.Count > 0 ? " " + string.Join(" ", result.Document.Warnings) : "");
        });
    }
}
