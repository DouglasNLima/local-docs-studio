export function createDefaultTable(rows = 3, columns = 3) {
  return Array.from({ length: Math.max(rows, 2) }, (_, rowIndex) => (
    Array.from({ length: Math.max(columns, 1) }, (_, columnIndex) => (
      rowIndex === 0 ? `Column ${String.fromCharCode(65 + columnIndex)}` : ''
    ))
  ));
}

export function formatMarkdownTable(rows) {
  const normalisedRows = normaliseTableRows(rows);
  const width = Math.max(...normalisedRows.map((row) => row.length), 1);
  const padded = normalisedRows.map((row) => Array.from({ length: width }, (_, index) => formatMarkdownCell(row[index] || '')));
  const header = padded[0] || Array.from({ length: width }, () => '');
  const body = padded.slice(1);
  const separator = Array.from({ length: width }, () => '---');

  return [
    formatMarkdownRow(header),
    formatMarkdownRow(separator),
    ...body.map(formatMarkdownRow),
  ].join('\n');
}

export function parseMarkdownTable(source) {
  const lines = String(source || '').replace(/\r\n?/g, '\n').split('\n');
  if (lines.length < 2 || !isSeparatorLine(lines[1])) return null;

  const rows = [];
  rows.push(parseMarkdownRow(lines[0]));
  for (let index = 2; index < lines.length; index += 1) {
    if (!looksLikeTableRow(lines[index])) break;
    rows.push(parseMarkdownRow(lines[index]));
  }

  return rows.length ? normaliseTableRows(rows) : null;
}

export function findMarkdownTableAt(source, position) {
  const text = String(source || '').replace(/\r\n?/g, '\n');
  const safePosition = Math.max(0, Math.min(Number(position) || 0, text.length));
  const lineStarts = [0];
  for (let index = 0; index < text.length; index += 1) {
    if (text[index] === '\n') lineStarts.push(index + 1);
  }
  const lineIndex = findLineIndex(lineStarts, safePosition);
  const lines = text.split('\n');

  let startLine = lineIndex;
  while (startLine > 0 && looksLikeTableRow(lines[startLine - 1])) startLine -= 1;

  let endLine = lineIndex;
  while (endLine + 1 < lines.length && looksLikeTableRow(lines[endLine + 1])) endLine += 1;

  for (let candidate = startLine; candidate <= Math.min(lineIndex, endLine - 1); candidate += 1) {
    if (!isSeparatorLine(lines[candidate + 1])) continue;
    const blockStart = candidate;
    let blockEnd = candidate + 1;
    while (blockEnd + 1 < lines.length && looksLikeTableRow(lines[blockEnd + 1])) blockEnd += 1;
    if (lineIndex < blockStart || lineIndex > blockEnd) continue;

    const start = lineStarts[blockStart];
    const end = blockEnd + 1 < lineStarts.length ? lineStarts[blockEnd + 1] - 1 : text.length;
    const tableText = text.slice(start, end);
    const rows = parseMarkdownTable(tableText);
    if (rows) {
      return { start, end, rows, text: tableText };
    }
  }

  return null;
}

export function normaliseTableRows(rows) {
  const safeRows = Array.isArray(rows) && rows.length ? rows : createDefaultTable();
  const width = Math.max(...safeRows.map((row) => Array.isArray(row) ? row.length : 0), 1);
  return safeRows.map((row) => Array.from({ length: width }, (_, index) => String(row?.[index] ?? '')));
}

function formatMarkdownRow(cells) {
  return `| ${cells.join(' | ')} |`;
}

function formatMarkdownCell(value) {
  return String(value || '')
    .replace(/\r\n?/g, '\n')
    .trim()
    .replaceAll('|', '\\|')
    .replace(/\n/g, '<br>');
}

function parseMarkdownRow(line) {
  const raw = String(line || '').trim();
  const content = raw.startsWith('|') ? raw.slice(1) : raw;
  const trimmed = content.endsWith('|') ? content.slice(0, -1) : content;
  const cells = [];
  let current = '';
  let escaped = false;

  for (const char of trimmed) {
    if (escaped) {
      current += char;
      escaped = false;
      continue;
    }
    if (char === '\\') {
      escaped = true;
      continue;
    }
    if (char === '|') {
      cells.push(unformatMarkdownCell(current));
      current = '';
      continue;
    }
    current += char;
  }
  cells.push(unformatMarkdownCell(current));
  return cells;
}

function unformatMarkdownCell(value) {
  return String(value || '').trim().replace(/<br\s*\/?>/gi, '\n');
}

function looksLikeTableRow(line) {
  return /^\s*\|?.+\|.+\|?\s*$/.test(String(line || ''));
}

function isSeparatorLine(line) {
  const cells = parseMarkdownRow(line);
  return cells.length > 0 && cells.every((cell) => /^:?-{3,}:?$/.test(cell.trim()));
}

function findLineIndex(lineStarts, position) {
  let low = 0;
  let high = lineStarts.length - 1;
  while (low <= high) {
    const mid = Math.floor((low + high) / 2);
    if (lineStarts[mid] <= position && (mid === lineStarts.length - 1 || lineStarts[mid + 1] > position)) return mid;
    if (lineStarts[mid] > position) high = mid - 1;
    else low = mid + 1;
  }
  return 0;
}
