using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static void CheckInlineLabels(Window owner, AutomationElement editor, UIA3Automation automation,
        int process, string output, string sourcePath)
    {
        var original = ReadEditorText(editor); var saved = File.ReadAllBytes(sourcePath);
        End(); var cancelled = Badge("InsertReviewBadge");
        Find(cancelled, "WorkspacePathInput").AsTextBox().Text = "Cancelled label";
        Keyboard.Type(VirtualKeyShort.ESCAPE);
        Wait(() => Prompt("Status badge") is null && ReadEditorText(editor) == original, "Cancelling the label editor changed source.");
        var badge = Badge("InsertReviewBadge"); var input = Find(badge, "WorkspacePathInput").AsTextBox();
        if (input.Name != "Badge text" || input.Text != "In review") throw new IOException("The badge dialogue lost its accessible field or status default.");
        input.Text = ""; Find(badge, "ConfirmWorkspacePath").AsButton().Invoke();
        Wait(() => Prompt("Status badge") is not null && badge.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
            .Any(item => item.Name == "Enter a single line of between 1 and 512 characters."), "The badge dialogue did not retain and explain invalid input.");
        if (ReadEditorText(editor) != original) throw new IOException("Invalid label input changed source.");
        input.Text = "Ready & reviewed"; Find(badge, "ConfirmWorkspacePath").AsButton().Invoke();
        const string firstMarkup = "<span data-status-badge data-status-kind=\"review\" aria-label=\"Status: Ready &amp; reviewed\">Ready &amp; reviewed</span>";
        Wait(() => ReadEditorText(editor) == original + firstMarkup, "The badge menu changed literal label text or surrounding source.");
        CheckPreviewText("Ready & reviewed");
        var first = ReadEditorText(editor);
        // The command keeps the inserted span selected, allowing a later status choice to edit it.
        var changed = Badge("InsertBetaBadge");
        var changedInput = Find(changed, "WorkspacePathInput").AsTextBox();
        if (changedInput.Text != "Ready & reviewed") throw new IOException("The badge editor did not restore its existing label.");
        changedInput.Text = "Reviewed"; Find(changed, "ConfirmWorkspacePath").AsButton().Invoke();
        const string secondMarkup = "<span data-status-badge data-status-kind=\"beta\" aria-label=\"Status: Reviewed\">Reviewed</span>";
        Wait(() => ReadEditorText(editor) == original + secondMarkup, "Changing badge status nested or duplicated its source span.");
        CheckPreviewText("Reviewed"); owner.CaptureToFile(Path.Combine(output, "native-inline-labels.png"));
        Undo(first); Undo(original); Redo(first); Undo(original);

        End(); Find(owner, "InsertMenu").Click(); Find(owner, "InsertKeyboardShortcut").Patterns.Invoke.Pattern.Invoke();
        var shortcut = WaitPrompt("Keyboard shortcut"); var keys = Find(shortcut, "WorkspacePathInput").AsTextBox();
        if (keys.Name != "Shortcut keys") throw new IOException("Shortcut keys lack an accessible label.");
        keys.Text = "Ctrl, Shift + K"; Find(shortcut, "ConfirmWorkspacePath").AsButton().Invoke();
        const string shortcutMarkup = "<span data-shortcut aria-label=\"Keyboard shortcut Ctrl Shift K\"><kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>K</kbd></span>";
        Wait(() => ReadEditorText(editor) == original + shortcutMarkup, "The keyboard shortcut menu changed keys or surrounding source.");
        CheckPreviewText("Ctrl + Shift + K"); var shortcutSource = ReadEditorText(editor);
        Undo(original); Redo(shortcutSource); Undo(original);

        End(); Find(owner, "InsertMenu").Click(); Find(owner, "InsertHorizontalRule").Patterns.Invoke.Pattern.Invoke();
        var beforeRules = new MarkdownService().Parse(original).Syntax.Descendants<ThematicBreakBlock>().Count();
        Wait(() => ReadEditorText(editor).StartsWith(original, StringComparison.Ordinal)
            && new MarkdownService().Parse(ReadEditorText(editor)).Syntax.Descendants<ThematicBreakBlock>().Count() == beforeRules + 1,
            "The horizontal rule menu did not insert a separate Markdown block.");
        var ruleSource = ReadEditorText(editor); Undo(original); Redo(ruleSource); Undo(original);
        if (!File.ReadAllBytes(sourcePath).SequenceEqual(saved)) throw new IOException("Inline label commands changed saved source.");
        File.WriteAllText(Path.Combine(output, "native-ui-inline-labels.json"), JsonSerializer.Serialize(new
        { Passed = true, CancelPreservedSource = true, InvalidInputRetained = true, StatusKindAndLiteralLabel = true,
            ExistingBadgeReopened = true, ShortcutKeys = true, NativePreviewText = true, HorizontalRule = true,
            CompleteUndoRedo = true, SavedSourceUnchanged = true }, new JsonSerializerOptions { WriteIndented = true }));

        void End() { FocusWindow(owner); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END); }
        Window Badge(string action)
        {
            Find(owner, "InsertMenu").Click(); Find(owner, "StatusBadgeMenu").Patterns.ExpandCollapse.Pattern.Expand();
            Find(owner, action).Patterns.Invoke.Pattern.Invoke(); return WaitPrompt("Status badge");
        }
        Window? Prompt(string title) => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByName(title)))?.AsWindow();
        Window WaitPrompt(string title)
        {
            Window? prompt = null; Wait(() => (prompt = Prompt(title)) is not null, "The inline label dialogue did not open: " + title); return prompt!;
        }
        void Undo(string expected)
        { FocusWindow(owner); editor.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z); Wait(() => ReadEditorText(editor) == expected, "One Undo did not restore complete label source."); }
        void Redo(string expected)
        { Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y); Wait(() => ReadEditorText(editor) == expected, "One Redo did not restore complete label source."); }
        void CheckPreviewText(string expected)
        {
            Wait(() =>
            {
                var text = Find(owner, "DocumentPreview").Patterns.Text;
                if (!text.IsSupported) return false;
                try { return text.Pattern.DocumentRange.GetText(-1).Contains(expected, StringComparison.Ordinal); }
                catch (FlaUI.Core.Exceptions.PatternNotSupportedException) { return false; }
            }, "The native preview did not retain the literal inline label: " + expected);
        }
    }
}
