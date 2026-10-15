using System.Windows;
using ICSharpCode.AvalonEdit.Document;
using LensDocsStudio.Core.Mermaid;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.State;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native;

public partial class MainWindow
{
    private void OpenMermaidVisualEditor(DocumentViewModel tab, ParsedDocument parsedDocument, ITextSourceVersion version, int offset)
    {
        if (model.ActiveDocument != tab || Editor.Document != tab.EditorDocument || Editor.IsReadOnly || tab.IsSaving
            || tab.EditorDocument.Text != parsedDocument.Source || version.CompareAge(tab.EditorDocument.Version) != 0)
        { model.Status = "The document changed. Wait for its current preview before editing visually."; return; }
        if (parsedDocument.Source.Length > MermaidDocumentEditSession.MaximumDocumentCharacters)
        { model.Status = "Visual authoring supports documents up to 4 million characters. Use Edit source for this document."; return; }
        var standalone = IsStandaloneMermaid(tab);
        if (standalone != parsedDocument.StandaloneMermaid) { model.Status = "The file format changed. Wait for the current preview before editing visually."; return; }
        var target = MermaidBlockSource.Find(parsedDocument, standalone).SingleOrDefault(block => offset >= block.Container.Start && offset < block.Container.End);
        if (target is null) { model.Status = "This Mermaid container cannot be mapped safely. Use Edit source."; return; }
        var session = new MermaidDocumentEditSession(tab, target);
        OpenVisualDraft(tab, session, target.Source, () =>
        {
            try { EditDiagramSource(tab, tab.EditorDocument.Text, session.SourceOffset()); }
            catch (ArgumentException error) { model.Status = error.Message; }
        });
    }
    private void InsertFlowchart(object sender, RoutedEventArgs e)
    {
        if (model.ActiveDocument is not { IsSaving: false } tab || Editor.IsReadOnly) return;
        if (tab.EditorDocument.TextLength > MermaidDocumentEditSession.MaximumDocumentCharacters)
        { model.Status = "Visual authoring supports documents up to 4 million characters. Insert Mermaid through the source editor for this document."; return; }
        if (IsStandaloneMermaid(tab) && !string.IsNullOrWhiteSpace(tab.EditorDocument.Text))
        {
            OpenMermaidVisualEditor(tab, markdown.Parse(tab.EditorDocument.Text, true), tab.EditorDocument.Version, 0); return;
        }
        var source = tab.EditorDocument.Text; var standalone = IsStandaloneMermaid(tab);
        var start = standalone ? 0 : Editor.SelectionStart; var length = standalone ? source.Length : Editor.SelectionLength;
        var target = new MermaidBlockSource(new(start, length), new(start, length), source.Substring(start, length), standalone) { IsInsertion = true };
        var newline = source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var session = new MermaidDocumentEditSession(tab, target, standalone ? null : (draft, current, span) =>
        {
            var prefix = span.Start == 0 || current[..span.Start].EndsWith(newline + newline, StringComparison.Ordinal) ? "" : current[..span.Start].EndsWith(newline, StringComparison.Ordinal) ? newline : newline + newline;
            var suffix = span.End == current.Length || current[span.End..].StartsWith(newline + newline, StringComparison.Ordinal) ? "" : current[span.End..].StartsWith(newline, StringComparison.Ordinal) ? newline : newline + newline;
            return prefix + "```mermaid" + newline + draft.TrimEnd('\r', '\n') + newline + "```" + suffix;
        });
        OpenVisualDraft(tab, session, "flowchart TD" + newline, null);
    }
    private static bool IsStandaloneMermaid(DocumentViewModel tab) => Path.GetExtension(tab.EffectivePath ?? tab.DisplayName)?.ToLowerInvariant() is ".mmd" or ".mermaid";
    private void OpenVisualDraft(DocumentViewModel tab, MermaidDocumentEditSession session, string source, Action? editSource)
    {
        var window = new MermaidVisualEditorWindow(source, diagrams, async (draft, cancellationToken) =>
        {
            try
            {
                if (model.ActiveDocument == tab && Editor.IsReadOnly) return "The owning document is read-only.";
                var edit = await session.PrepareAsync(draft, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested(); session.Commit(edit, model.Documents.Contains(tab));
                model.Status = "Mermaid source applied as one undoable change. Save the document to persist it.";
                if (model.ActiveDocument == tab) QueuePreview();
                return null;
            }
            catch (ArgumentException error) { return error.Message; }
        }, ThemeService.IsDark(model.Theme), WorkspaceSettings.From(model.Preferences).Preview.DiagramTheme, editSource, ExportDiagram, sourceFont: Editor.FontFamily, sourceFontSize: Editor.FontSize) { Owner = this };
        window.Show();
    }
}
