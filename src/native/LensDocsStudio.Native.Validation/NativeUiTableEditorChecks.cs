using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using LensDocsStudio.Core.Parsing;
using Markdig.Extensions.Tables;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static void CheckTableEditor(Window owner, AutomationElement editor, UIA3Automation automation,
        int process, string output, string sourcePath)
    {
        var original = ReadEditorText(editor); var saved = File.ReadAllBytes(sourcePath);
        var oldTable = new MarkdownService().Parse(original).Syntax.Descendants<Table>().Single();
        if (!original.EndsWith("| Clipboard | Ready |\n", StringComparison.Ordinal)) throw new IOException("The table editing fixture changed.");
        End(inside: true); var cancelled = Open(); CheckSize(cancelled, 2, 2);
        if (Find(cancelled, "TableRemoveRow").IsEnabled || Find(cancelled, "TableRowDown").IsEnabled)
            throw new IOException("The native grid allows deletion or movement of the table header.");
        SetCell(cancelled, 1, 1, "Cancelled"); Find(cancelled, "CancelTable").AsButton().Invoke();
        Wait(() => Prompt() is null && ReadEditorText(editor) == original, "Cancelling a changed table discarded source.");

        End(inside: true); var window = Open();
        if (Cell(window, 1, 1).Patterns.Value.Pattern.Value.Value != "Ready") throw new IOException("Cancelling changed the next table draft.");
        SetCell(window, 1, 1, "Reviewed"); Cell(window, 1, 1).Click();
        var alignment = Find(window, "TableAlignment").AsComboBox(); alignment.Select("Centre"); alignment.Collapse();
        Find(window, "TableAddRow").AsButton().Invoke(); CheckSize(window, 3, 2);
        SetCell(window, 2, 0, "Added"); Cell(window, 2, 0).Click();
        Find(window, "TableRowUp").AsButton().Invoke(); Find(window, "TableRowDown").AsButton().Invoke();
        Find(window, "TableAddColumn").AsButton().Invoke(); CheckSize(window, 3, 3);
        SetCell(window, 2, 1, "Extra"); Cell(window, 2, 1).Click();
        Find(window, "TableColumnLeft").AsButton().Invoke(); Find(window, "TableColumnRight").AsButton().Invoke();
        window.CaptureToFile(Path.Combine(output, "native-table-editor.png"));
        Find(window, "ApplyTable").AsButton().Invoke();
        const string expandedTable = "| Area | Column 2 | Status |\n| --- | --- | :---: |\n| Clipboard |  | Reviewed |\n| Added | Extra |  |";
        var expanded = original[..oldTable.Span.Start] + expandedTable + original[(oldTable.Span.End + 1)..];
        Wait(() => Prompt() is null && ReadEditorText(editor) == expanded, "The native table grid lost cells, alignment or surrounding source when applying.");
        CheckPreview();
        var reopened = Open(); CheckSize(reopened, 3, 3);
        if (Cell(reopened, 2, 1).Patterns.Value.Pattern.Value.Value != "Extra") throw new IOException("The table grid did not restore the saved draft cells.");
        Cell(reopened, 2, 1).Click(); Find(reopened, "TableRemoveRow").AsButton().Invoke();
        Find(reopened, "TableRemoveColumn").AsButton().Invoke(); CheckSize(reopened, 2, 2);
        Find(reopened, "ApplyTable").AsButton().Invoke();
        const string reducedTable = "| Area | Status |\n| --- | :---: |\n| Clipboard | Reviewed |";
        var reduced = original[..oldTable.Span.Start] + reducedTable + original[(oldTable.Span.End + 1)..];
        Wait(() => ReadEditorText(editor) == reduced, "Removing a row and column changed retained cells or alignment.");
        Undo(expanded); Undo(original); Redo(expanded); Undo(original);

        End(inside: false); var inserted = Open(); CheckSize(inserted, 3, 3); SetCell(inserted, 1, 0, "New table");
        Find(inserted, "ApplyTable").AsButton().Invoke();
        Wait(() => ReadEditorText(editor).StartsWith(original, StringComparison.Ordinal) && new MarkdownService().Parse(ReadEditorText(editor)).Syntax.Descendants<Table>().Count() == 2,
            "Insert table did not create a separate Markdown table after the current source.");
        if (!ReadEditorText(editor).Contains("New table", StringComparison.Ordinal)) throw new IOException("New table content was lost.");
        Undo(original);
        if (!File.ReadAllBytes(sourcePath).SequenceEqual(saved)) throw new IOException("Table editing changed the saved file without Save.");
        File.WriteAllText(Path.Combine(output, "native-ui-table-editor.json"), JsonSerializer.Serialize(new
        { Passed = true, CancelPreservedSource = true, KeyboardCellEdit = true, RowAndColumnActions = true, AlignmentRetained = true,
            ExistingTableReopened = true, NewTableInserted = true, NativePreviewText = true, WholeUndoRedo = true, SavedSourceUnchanged = true },
            new JsonSerializerOptions { WriteIndented = true }));

        void End(bool inside)
        { FocusWindow(owner); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END); if (inside) Keyboard.Type(VirtualKeyShort.LEFT); }
        Window? Prompt() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByName("Markdown table")))?.AsWindow();
        Window Open()
        {
            Find(owner, "InsertMenu").Click(); Find(owner, "EditTable").Patterns.Invoke.Pattern.Invoke();
            Window? prompt = null; Wait(() => (prompt = Prompt()) is not null, "The native table editor did not open."); return prompt!;
        }
        AutomationElement Cell(Window dialog, int row, int column) => Find(dialog, "TableCells").Patterns.Grid.Pattern.GetItem(row, column);
        void CheckSize(Window dialog, int rows, int columns) => Wait(() => Find(dialog, "TableCells").Patterns.Grid.Pattern.RowCount.Value == rows
            && Find(dialog, "TableCells").Patterns.Grid.Pattern.ColumnCount.Value == columns, "The native table dimensions differ from the requested edit.");
        void SetCell(Window dialog, int row, int column, string text)
        {
            Cell(dialog, row, column).Click(); Keyboard.Type(VirtualKeyShort.F2);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A); Keyboard.Type(text); Keyboard.Type(VirtualKeyShort.RETURN);
            Wait(() => Cell(dialog, row, column).Patterns.Value.Pattern.Value.Value == text, "Keyboard editing did not commit the exact table cell text.");
        }
        void Undo(string expected)
        { FocusWindow(owner); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z); Wait(() => ReadEditorText(editor) == expected, "One Undo did not restore complete table source."); }
        void Redo(string expected)
        { Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y); Wait(() => ReadEditorText(editor) == expected, "One Redo did not restore the complete table edit."); }
        void CheckPreview() => Wait(() =>
        {
            var text = Find(owner, "DocumentPreview").Patterns.Text;
            if (!text.IsSupported) return false;
            try { var value = text.Pattern.DocumentRange.GetText(-1); return new[] { "Reviewed", "Added", "Extra" }.All(word => value.Contains(word, StringComparison.Ordinal)); }
            catch (FlaUI.Core.Exceptions.PatternNotSupportedException) { return false; }
        }, "The native preview lost edited table cells.");
    }
}
