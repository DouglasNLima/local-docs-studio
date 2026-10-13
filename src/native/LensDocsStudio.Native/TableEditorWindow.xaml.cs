using System.Data;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using LensDocsStudio.Core.Editing;
using LensDocsStudio.Native.Platform;

namespace LensDocsStudio.Native;

public partial class TableEditorWindow : Window
{
    private readonly TableEditSession session;
    private TableEditGrid draft;
    private DataTable values = new();
    private bool updating, closed;
    private CancellationTokenSource? operation;
    private int row, column;
    public MarkdownEdit? Edit { get; private set; }

    public TableEditorWindow(TableEditSession session, bool replacesSelection = false)
    {
        this.session = session; draft = session.Grid.Copy();
        InitializeComponent(); WindowStateService.FitInitialWindow(this);
        Heading.Text = session.IsExisting ? "Edit Markdown table" : "Insert Markdown table";
        if (!session.IsExisting && replacesSelection) Status.Text = "Apply replaces the selected text with this table in one undoable change. Cancel keeps the selection unchanged.";
        Rebuild();
        Loaded += (_, _) => SelectCell();
        Closed += (_, _) => { closed = true; operation?.Cancel(); values.Dispose(); };
    }
    private void Rebuild()
    {
        updating = true;
        try
        {
            Cells.ItemsSource = null; Cells.Columns.Clear(); values.Dispose(); values = new();
            for (var index = 0; index < draft.ColumnCount; index++)
            {
                values.Columns.Add("C" + index, typeof(string));
                Cells.Columns.Add(new DataGridTextColumn { Header = "Column " + (index + 1), Width = 190, MinWidth = 100,
                    Binding = new Binding("[C" + index + "]") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
                    ElementStyle = (Style)Resources["CellText"], EditingElementStyle = (Style)Resources["CellEditor"] });
            }
            for (var index = 0; index < draft.RowCount; index++) values.Rows.Add(Enumerable.Range(0, draft.ColumnCount).Select(col => (object)draft.Cell(index, col)).ToArray());
            Cells.ItemsSource = values.DefaultView;
            row = Math.Clamp(row, 0, draft.RowCount - 1); column = Math.Clamp(column, 0, draft.ColumnCount - 1);
            Cells.CurrentCell = new(values.DefaultView[row], Cells.Columns[column]);
        }
        finally { updating = false; }
        UpdateActions();
    }
    private void SelectCell()
    {
        var item = values.DefaultView[row]; Cells.CurrentCell = new(item, Cells.Columns[column]);
        Cells.SelectedCells.Clear(); Cells.SelectedCells.Add(Cells.CurrentCell); Cells.ScrollIntoView(item, Cells.Columns[column]); Cells.Focus();
    }
    private bool Capture()
    {
        if (!Cells.CommitEdit(DataGridEditingUnit.Cell, true) || !Cells.CommitEdit(DataGridEditingUnit.Row, true))
        { Status.Text = "Complete the current cell before applying the table."; return false; }
        try
        {
            draft = new(values.Rows.Cast<DataRow>().Select(item => Enumerable.Range(0, draft.ColumnCount).Select(col => item[col]?.ToString() ?? "")),
                Enumerable.Range(0, draft.ColumnCount).Select(draft.Alignment));
            return true;
        }
        catch (ArgumentException error) { Status.Text = error.Message; return false; }
    }
    private void CellChanged(object? sender, EventArgs e)
    {
        if (updating || Cells.CurrentCell.Item is not DataRowView item || Cells.CurrentCell.Column is null) return;
        row = values.Rows.IndexOf(item.Row); column = Cells.CurrentCell.Column.DisplayIndex;
        if (row >= 0 && column >= 0) UpdateActions();
    }
    private void LabelRow(object? sender, DataGridRowEventArgs e)
    {
        var index = e.Row.GetIndex(); e.Row.Header = index == 0 ? "Header" : index.ToString();
        AutomationProperties.SetName(e.Row, index == 0 ? "Table header" : "Table row " + index);
    }
    private void UpdateActions()
    {
        updating = true;
        try
        {
            Summary.Text = $"{draft.RowCount - 1} body rows × {draft.ColumnCount} columns. The first row is the header; column alignment moves with its column.";
            AddRow.IsEnabled = draft.RowCount < TableEditGrid.MaximumRows && (draft.RowCount + 1) * draft.ColumnCount <= TableEditGrid.MaximumCells;
            AddColumn.IsEnabled = draft.ColumnCount < TableEditGrid.MaximumColumns && draft.RowCount * (draft.ColumnCount + 1) <= TableEditGrid.MaximumCells;
            RemoveRow.IsEnabled = row > 0 && draft.RowCount > 2; RemoveColumn.IsEnabled = draft.ColumnCount > 1;
            RowUp.IsEnabled = row > 1; RowDown.IsEnabled = row > 0 && row < draft.RowCount - 1;
            ColumnLeft.IsEnabled = column > 0; ColumnRight.IsEnabled = column < draft.ColumnCount - 1;
            Alignment.SelectedIndex = (int)draft.Alignment(column);
        }
        finally { updating = false; }
    }
    private void ChangeStructure(object sender, RoutedEventArgs e)
    {
        if (operation is not null || sender is not Button { Tag: string action } || !Capture()) return;
        try
        {
            switch (action)
            {
                case "add-row": draft.InsertRow(row); row++; break;
                case "remove-row": draft.RemoveRow(row); break;
                case "row-up": draft.MoveRow(row, -1); row--; break;
                case "row-down": draft.MoveRow(row, 1); row++; break;
                case "add-column": draft.InsertColumn(column); column++; break;
                case "remove-column": draft.RemoveColumn(column); break;
                case "column-left": draft.MoveColumn(column, -1); column--; break;
                case "column-right": draft.MoveColumn(column, 1); column++; break;
            }
            Rebuild(); SelectCell(); Status.Text = "Table draft changed. Apply commits the complete table; Cancel leaves the document unchanged.";
        }
        catch (ArgumentException error) { Status.Text = error.Message; }
    }
    private void ChangeAlignment(object sender, SelectionChangedEventArgs e)
    {
        if (updating || operation is not null || Alignment.SelectedIndex < 0) return;
        draft.SetAlignment(column, (TableColumnAlignment)Alignment.SelectedIndex);
        Status.Text = "Column alignment changed in the draft.";
    }
    private void Shortcut(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; Accept(sender, e); }
    }
    private async void Accept(object sender, RoutedEventArgs e)
    {
        if (operation is not null || !Capture()) return;
        var snapshot = draft.Copy(); operation = new(); var token = operation.Token;
        ApplyButton.IsEnabled = Cells.IsEnabled = Actions.IsEnabled = false; Status.Text = "Checking the complete table…";
        try
        {
            var edit = await Task.Run(() => session.Plan(snapshot), token);
            token.ThrowIfCancellationRequested(); if (closed) return;
            Edit = edit; DialogResult = true;
        }
        catch (OperationCanceledException) { if (!closed) Status.Text = "Table edit cancelled."; }
        catch (Exception error) when (error is ArgumentException or InvalidDataException) { if (!closed) Status.Text = error.Message; }
        finally
        {
            operation.Dispose(); operation = null;
            if (!closed) { ApplyButton.IsEnabled = Cells.IsEnabled = Actions.IsEnabled = true; UpdateActions(); }
        }
    }
}
