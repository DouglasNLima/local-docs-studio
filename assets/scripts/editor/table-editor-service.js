import { createDefaultTable, findMarkdownTableAt, formatMarkdownTable, normaliseTableRows } from '../utils/markdown-table.js';

export function createTableEditorService({ editor, dom, callbacks }) {
  const {
    tableEditorDialog,
    tableEditorGrid,
    tableEditorSummary,
    tableEditorApplyButton,
    tableEditorCancelButton,
    tableEditorAddRowButton,
    tableEditorAddColumnButton,
    tableEditorRemoveRowButton,
    tableEditorRemoveColumnButton,
    tableEditorMoveRowUpButton,
    tableEditorMoveRowDownButton,
    tableEditorMoveColumnLeftButton,
    tableEditorMoveColumnRightButton,
  } = dom;
  const {
    replaceEditorRange,
    setStatus,
  } = callbacks;

  let tableState = {
    rows: createDefaultTable(),
    range: null,
    activeRow: 0,
    activeColumn: 0,
  };

  function installTableEditorHandlers() {
    tableEditorDialog?.addEventListener('cancel', closeTableEditor);
    tableEditorDialog?.addEventListener('click', (event) => {
      if (event.target.closest('[data-table-editor-cancel]')) closeTableEditor(event);
    });
    tableEditorCancelButton?.addEventListener('click', closeTableEditor);
    tableEditorApplyButton?.addEventListener('click', applyTable);
    tableEditorGrid?.addEventListener('input', handleCellInput);
    tableEditorGrid?.addEventListener('focusin', handleCellFocus);
    tableEditorAddRowButton?.addEventListener('click', () => addRow());
    tableEditorAddColumnButton?.addEventListener('click', () => addColumn());
    tableEditorRemoveRowButton?.addEventListener('click', () => removeRow());
    tableEditorRemoveColumnButton?.addEventListener('click', () => removeColumn());
    tableEditorMoveRowUpButton?.addEventListener('click', () => moveRow(-1));
    tableEditorMoveRowDownButton?.addEventListener('click', () => moveRow(1));
    tableEditorMoveColumnLeftButton?.addEventListener('click', () => moveColumn(-1));
    tableEditorMoveColumnRightButton?.addEventListener('click', () => moveColumn(1));
  }

  function openTableEditor() {
    const range = findMarkdownTableAt(editor.value, editor.selectionStart);
    tableState = {
      rows: normaliseTableRows(range?.rows || createDefaultTable()),
      range,
      activeRow: 0,
      activeColumn: 0,
    };
    renderTableGrid();
    tableEditorDialog?.showModal();
    tableEditorGrid?.querySelector('input')?.focus();
  }

  function closeTableEditor(event) {
    event?.preventDefault?.();
    tableEditorDialog?.close();
    editor.focus();
  }

  function renderTableGrid() {
    const rows = tableState.rows;
    const columnCount = rows[0]?.length || 1;
    tableEditorSummary.textContent = tableState.range
      ? `Editing existing table (${rows.length} rows x ${columnCount} columns).`
      : `Creating a new table (${rows.length} rows x ${columnCount} columns).`;
    tableEditorGrid.innerHTML = '';
    tableEditorGrid.style.setProperty('--table-editor-columns', String(columnCount));

    const corner = document.createElement('span');
    corner.className = 'table-editor-corner';
    tableEditorGrid.appendChild(corner);
    for (let column = 0; column < columnCount; column += 1) {
      const label = document.createElement('button');
      label.type = 'button';
      label.className = `table-editor-axis${column === tableState.activeColumn ? ' active' : ''}`;
      label.textContent = String(column + 1);
      label.addEventListener('click', () => {
        tableState.activeColumn = column;
        renderTableGrid();
      });
      tableEditorGrid.appendChild(label);
    }

    rows.forEach((row, rowIndex) => {
      const rowLabel = document.createElement('button');
      rowLabel.type = 'button';
      rowLabel.className = `table-editor-axis${rowIndex === tableState.activeRow ? ' active' : ''}`;
      rowLabel.textContent = rowIndex === 0 ? 'H' : String(rowIndex);
      rowLabel.addEventListener('click', () => {
        tableState.activeRow = rowIndex;
        renderTableGrid();
      });
      tableEditorGrid.appendChild(rowLabel);
      row.forEach((cell, columnIndex) => {
        const input = document.createElement('input');
        input.type = 'text';
        input.value = cell;
        input.dataset.row = String(rowIndex);
        input.dataset.column = String(columnIndex);
        input.setAttribute('aria-label', `Row ${rowIndex + 1}, column ${columnIndex + 1}`);
        tableEditorGrid.appendChild(input);
      });
    });

    updateActionState();
  }

  function updateActionState() {
    const rowCount = tableState.rows.length;
    const columnCount = tableState.rows[0]?.length || 1;
    tableEditorRemoveRowButton.disabled = rowCount <= 2 || tableState.activeRow === 0;
    tableEditorRemoveColumnButton.disabled = columnCount <= 1;
    tableEditorMoveRowUpButton.disabled = tableState.activeRow <= 1;
    tableEditorMoveRowDownButton.disabled = tableState.activeRow <= 0 || tableState.activeRow >= rowCount - 1;
    tableEditorMoveColumnLeftButton.disabled = tableState.activeColumn <= 0;
    tableEditorMoveColumnRightButton.disabled = tableState.activeColumn >= columnCount - 1;
  }

  function handleCellInput(event) {
    const input = event.target.closest('input[data-row][data-column]');
    if (!input) return;
    tableState.rows[Number(input.dataset.row)][Number(input.dataset.column)] = input.value;
  }

  function handleCellFocus(event) {
    const input = event.target.closest('input[data-row][data-column]');
    if (!input) return;
    tableState.activeRow = Number(input.dataset.row);
    tableState.activeColumn = Number(input.dataset.column);
    updateActionState();
  }

  function addRow() {
    const columnCount = tableState.rows[0]?.length || 1;
    const insertAt = Math.max(1, tableState.activeRow + 1);
    tableState.rows.splice(insertAt, 0, Array.from({ length: columnCount }, () => ''));
    tableState.activeRow = insertAt;
    renderTableGrid();
  }

  function addColumn() {
    const insertAt = tableState.activeColumn + 1;
    tableState.rows.forEach((row, rowIndex) => {
      row.splice(insertAt, 0, rowIndex === 0 ? `Column ${insertAt + 1}` : '');
    });
    tableState.activeColumn = insertAt;
    renderTableGrid();
  }

  function removeRow() {
    if (tableState.rows.length <= 2 || tableState.activeRow === 0) return;
    tableState.rows.splice(tableState.activeRow, 1);
    tableState.activeRow = Math.min(tableState.activeRow, tableState.rows.length - 1);
    renderTableGrid();
  }

  function removeColumn() {
    if ((tableState.rows[0]?.length || 1) <= 1) return;
    tableState.rows.forEach((row) => row.splice(tableState.activeColumn, 1));
    tableState.activeColumn = Math.min(tableState.activeColumn, (tableState.rows[0]?.length || 1) - 1);
    renderTableGrid();
  }

  function moveRow(delta) {
    const from = tableState.activeRow;
    const to = from + delta;
    if (from <= 0 || to <= 0 || to >= tableState.rows.length) return;
    const [row] = tableState.rows.splice(from, 1);
    tableState.rows.splice(to, 0, row);
    tableState.activeRow = to;
    renderTableGrid();
  }

  function moveColumn(delta) {
    const width = tableState.rows[0]?.length || 1;
    const from = tableState.activeColumn;
    const to = from + delta;
    if (to < 0 || to >= width) return;
    tableState.rows.forEach((row) => {
      const [cell] = row.splice(from, 1);
      row.splice(to, 0, cell);
    });
    tableState.activeColumn = to;
    renderTableGrid();
  }

  function applyTable(event) {
    event?.preventDefault?.();
    const markdown = formatMarkdownTable(tableState.rows);
    if (tableState.range) {
      replaceEditorRange(tableState.range.start, tableState.range.end, markdown, tableState.range.start, tableState.range.start + markdown.length);
      setStatus('Table updated.', 'ok');
    } else {
      const selectionStart = editor.selectionStart;
      const leadingBreak = selectionStart > 0 && editor.value[selectionStart - 1] !== '\n' ? '\n\n' : '';
      const trailingBreak = editor.selectionEnd < editor.value.length && editor.value[editor.selectionEnd] !== '\n' ? '\n\n' : '';
      const replacement = `${leadingBreak}${markdown}${trailingBreak}`;
      const tableStart = selectionStart + leadingBreak.length;
      replaceEditorRange(editor.selectionStart, editor.selectionEnd, replacement, tableStart, tableStart + markdown.length);
      setStatus('Table inserted.', 'ok');
    }
    closeTableEditor();
  }

  return {
    installTableEditorHandlers,
    openTableEditor,
  };
}
