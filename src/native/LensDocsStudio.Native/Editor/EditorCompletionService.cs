using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using LensDocsStudio.Core.Editing;
using LensDocsStudio.Core.Parsing;

namespace LensDocsStudio.Native.Editor;

public sealed class EditorCompletionService : IDisposable
{
    private readonly TextEditor editor;
    private readonly Func<bool> standalone;
    private readonly Func<WikiLinkIndex?>? wikiCatalogue;
    private CompletionWindow? window;
    private bool wikiWindow;
    public EditorCompletionService(TextEditor editor, Func<bool> standalone, Func<WikiLinkIndex?>? wikiCatalogue = null)
    {
        this.editor = editor; this.standalone = standalone; this.wikiCatalogue = wikiCatalogue;
        editor.TextArea.TextEntered += TextEntered; editor.TextArea.TextEntering += TextEntering;
        editor.DocumentChanged += DocumentChanged;
    }
    public bool Show(bool force = true)
    {
        if (editor.IsReadOnly || !editor.IsEnabled || editor.SelectionLength != 0) return false;
        if (window is not null) { if (!force) return true; Close(); }
        var source = editor.Text; var caret = editor.CaretOffset;
        if (source.Length > (force ? MermaidCompletionService.ExplicitLimit : MermaidCompletionService.AutomaticLimit)) return false;
        if (!standalone() && WikiLinkCompletionService.HasOpening(source, caret) && wikiCatalogue?.Invoke() is { } catalogue
            && WikiLinkCompletionService.Query(source, caret, catalogue, force) is { } wiki)
            return ShowWiki(wiki);
        var context = MermaidCompletionService.Query(source, caret, standalone(), force);
        if (context is null) return false;
        var document = editor.Document;
        var popup = new CompletionWindow(editor.TextArea)
        {
            StartOffset = context.Start, EndOffset = context.Start + context.Length,
            CloseWhenCaretAtBeginning = false, Width = 330, MaxHeight = 300,
            Background = editor.Background, Foreground = editor.Foreground
        };
        AutomationProperties.SetName(popup, "Mermaid suggestions"); AutomationProperties.SetAutomationId(popup, "MermaidCompletion");
        AutomationProperties.SetName(popup.CompletionList.ListBox, "Mermaid suggestions");
        foreach (var item in context.Items)
            popup.CompletionList.CompletionData.Add(new Completion(item, context.Prefix, () =>
            {
                if (ReferenceEquals(editor.Document, document)) Apply(editor, standalone(), item.Label);
            }));
        window = popup; popup.Closed += (_, _) => { if (ReferenceEquals(window, popup)) window = null; };
        popup.Show(); popup.CompletionList.SelectItem(context.Prefix);
        return true;
    }
    public static bool Apply(TextEditor editor, bool standalone, string label)
    {
        if (editor.IsReadOnly || editor.SelectionLength != 0) return false;
        var edit = MermaidCompletionService.Plan(editor.Text, editor.CaretOffset, standalone, label);
        if (edit is null) return false;
        using (editor.Document.RunUpdate()) editor.Document.Replace(edit.Start, edit.Length, edit.Replacement);
        editor.CaretOffset = edit.SelectionStart;
        return true;
    }
    private bool ShowWiki(WikiLinkCompletionContext context)
    {
        var document = editor.Document;
        var popup = new CompletionWindow(editor.TextArea) { StartOffset = context.Start, EndOffset = editor.CaretOffset,
            CloseWhenCaretAtBeginning = false, Width = 440, MaxHeight = 300, Background = editor.Background, Foreground = editor.Foreground };
        AutomationProperties.SetName(popup, "Workspace link suggestions"); AutomationProperties.SetAutomationId(popup, "WikiLinkCompletion");
        AutomationProperties.SetName(popup.CompletionList.ListBox, "Workspace link suggestions");
        foreach (var path in context.Paths)
            popup.CompletionList.CompletionData.Add(new WikiCompletion(path, () =>
            {
                if (ReferenceEquals(editor.Document, document) && wikiCatalogue?.Invoke() is { } current) ApplyWiki(editor, current, path);
            }));
        window = popup; wikiWindow = true;
        popup.Closed += (_, _) => { if (ReferenceEquals(window, popup)) { window = null; wikiWindow = false; } };
        popup.Show(); popup.CompletionList.SelectItem(context.Prefix); return true;
    }
    public static bool ApplyWiki(TextEditor editor, WikiLinkIndex catalogue, string path)
    {
        if (editor.IsReadOnly || editor.SelectionLength != 0) return false;
        var edit = WikiLinkCompletionService.Plan(editor.Text, editor.CaretOffset, catalogue, path);
        if (edit is null) return false;
        using (editor.Document.RunUpdate()) editor.Document.Replace(edit.Start, edit.Length, edit.Replacement);
        editor.CaretOffset = edit.SelectionStart; return true;
    }
    public void Close() { var previous = window; window = null; wikiWindow = false; previous?.Close(); }
    private void TextEntered(object? sender, TextCompositionEventArgs e) => Show(false);
    private void TextEntering(object? sender, TextCompositionEventArgs e)
    {
        if (wikiWindow ? e.Text.Any(c => c is ']' or '|' or '#' or '\r' or '\n')
            : e.Text.Any(c => !char.IsLetterOrDigit(c) && c is not ('_' or '-' or '.' or '=' or '>'))) Close();
    }
    private void DocumentChanged(object? sender, EventArgs e) => Close();
    public void Dispose()
    {
        Close(); editor.TextArea.TextEntered -= TextEntered; editor.TextArea.TextEntering -= TextEntering;
        editor.DocumentChanged -= DocumentChanged;
    }
    private sealed class Completion(MermaidCompletionItem item, string prefix, Action accept) : ICompletionData
    {
        public ImageSource? Image => null;
        public string Text { get; } = item.Label.Contains(prefix, StringComparison.OrdinalIgnoreCase) ? item.Label
            : item.Triggers.First(t => t.Contains(prefix, StringComparison.OrdinalIgnoreCase));
        public object Content => item.Label;
        public object Description => item.Description;
        public double Priority => item.Label == "flowchart TD" ? 1 : 0;
        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs) => accept();
    }
    private sealed class WikiCompletion(string path, Action accept) : ICompletionData
    {
        public ImageSource? Image => null;
        public string Text => path;
        public object Content => path;
        public object Description => "Link to this workspace document";
        public double Priority => 0;
        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs) => accept();
    }
}
