using System.Windows;
using System.Windows.Controls;
using LensDocsStudio.Core.Editing;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.Editor;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private async void InsertInlineLabel(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string action } || model.ActiveDocument is not { IsSaving: false } tab || Editor.IsReadOnly) return;
        if (Path.GetExtension(tab.EffectivePath)?.ToLowerInvariant() is ".mmd" or ".mermaid")
        { model.Status = "Insert status labels and keyboard shortcuts in a Markdown document."; return; }
        var source = tab.EditorDocument.Text; var start = Editor.SelectionStart; var length = Editor.SelectionLength;
        if (source.Length > 4 * 1024 * 1024) { model.Status = "The label editor supports documents up to 4 million characters."; return; }
        var shortcut = action == "shortcut";
        await model.RunAsync(async () =>
        {
            var document = await Task.Run(() => new MarkdownService().Parse(source), model.OperationToken);
            model.OperationToken.ThrowIfCancellationRequested();
            if (Changed() || Editor.SelectionStart != start || Editor.SelectionLength != length)
            { model.Status = "The document or selection changed; open the label editor again."; return; }
            var existing = InlineLabelEditService.Find(document, start, length, shortcut ? DocumentInlineKind.Shortcut : DocumentInlineKind.StatusBadge);
            var initial = existing?.Text ?? (length is > 0 and <= 512 ? source.Substring(start, length).ReplaceLineEndings(" ")
                : shortcut ? "Ctrl + Enter" : InlineLabelEditService.StatusLabel(action));
            var window = new TextPromptWindow(shortcut ? "Keyboard shortcut" : "Status badge",
                shortcut ? "Keys to display in the document" : "Text for the " + InlineLabelEditService.StatusLabel(action).ToLowerInvariant() + " badge",
                initial, "Apply", shortcut ? "Shortcut keys" : "Badge text",
                shortcut ? "Separate keys with + or commas. Use Plus for the + key." : "The label is kept as literal text in portable Markdown.",
                value => { try { _ = Plan(value); return null; } catch (ArgumentException error) { return error.Message; } }) { Owner = this };
            if (window.ShowDialog() != true) return;
            if (Changed()) { model.Status = "The document changed; the label was not applied. Open the editor again."; return; }
            EditorFormattingService.Apply(Editor, Plan(window.Value));
            model.Status = shortcut ? "Keyboard shortcut applied." : "Status badge applied.";
            MarkdownEdit Plan(string value) => shortcut ? InlineLabelEditService.Shortcut(document, start, length, value)
                : InlineLabelEditService.Badge(document, start, length, action, value);
        });
        bool Changed() => model.ActiveDocument != tab || tab.EditorDocument.Text != source || tab.IsSaving || Editor.IsReadOnly;
    }
}
