using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using LensDocsStudio.Core.Editing;

namespace LensDocsStudio.Native.Editor;

public sealed class EditorCompletionService : IDisposable
{
    private readonly TextEditor editor;
    private readonly Func<bool> standalone;
    private CompletionWindow? window;
    public EditorCompletionService(TextEditor editor, Func<bool> standalone)
    {
        this.editor = editor; this.standalone = standalone;
        editor.TextArea.TextEntered += TextEntered; editor.TextArea.TextEntering += TextEntering;
        editor.DocumentChanged += DocumentChanged;
    }
    public bool Show(bool force = true)
    {
        if (editor.IsReadOnly || !editor.IsEnabled || editor.SelectionLength != 0) return false;
        if (window is not null) { if (!force) return true; Close(); }
        var context = MermaidCompletionService.Query(editor.Text, editor.CaretOffset, standalone(), force);
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
    public void Close() { var previous = window; window = null; previous?.Close(); }
    private void TextEntered(object? sender, TextCompositionEventArgs e) => Show(false);
    private void TextEntering(object? sender, TextCompositionEventArgs e)
    {
        if (e.Text.Any(c => !char.IsLetterOrDigit(c) && c is not ('_' or '-' or '.' or '=' or '>'))) Close();
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
}
