import { formatMarkdownTable } from '../utils/markdown-table.js';

export const pasteModes = {
  auto: 'auto',
  table: 'table',
  text: 'text',
  code: 'code',
  quote: 'quote',
  htmlMarkdown: 'htmlMarkdown',
  list: 'list',
  checklist: 'checklist',
  numberedList: 'numberedList',
  mermaid: 'mermaid',
};

const tableMessages = {
  ok: 'Table pasted as Markdown.',
  flattened: 'Table pasted as Markdown. Merged cells were flattened.',
};

export function getClipboardPayloadFromEvent(event) {
  const data = event?.clipboardData;
  if (!data) return { html: '', text: '', types: [] };

  return {
    html: data.getData('text/html') || '',
    text: data.getData('text/plain') || '',
    types: [...(data.types || [])],
  };
}

export async function readClipboardPayload() {
  const clipboard = navigator.clipboard;
  if (!clipboard) {
    throw new Error('Clipboard API is unavailable.');
  }

  const payload = { html: '', text: '', types: [] };
  let readError = null;

  if (typeof clipboard.read === 'function') {
    try {
      const items = await clipboard.read();
      for (const item of items) {
        for (const type of item.types || []) {
          if (type !== 'text/html' && type !== 'text/plain') continue;
          const blob = await item.getType(type);
          const value = await blob.text();
          if (type === 'text/html' && !payload.html) payload.html = value;
          if (type === 'text/plain' && !payload.text) payload.text = value;
          payload.types.push(type);
        }
      }

      if (payload.html || payload.text) {
        return payload;
      }
    } catch (error) {
      readError = error;
    }
  }

  if (typeof clipboard.readText === 'function') {
    try {
      const text = await clipboard.readText();
      if (text) {
        return { html: '', text, types: ['text/plain'] };
      }
    } catch (error) {
      throw readError || error;
    }
  }

  throw readError || new Error('Clipboard did not contain readable text.');
}

export function resolvePasteReplacement(payload, mode = pasteModes.auto) {
  if (mode === pasteModes.text) {
    const text = getPlainTextFromPayload(payload);
    return text ? {
      text,
      block: false,
      status: 'Plain text pasted.',
      statusType: 'ok',
    } : null;
  }

  if (mode === pasteModes.code) {
    const text = getPlainTextFromPayload(payload);
    return text ? {
      text: buildCodeBlock(text),
      block: true,
      status: 'Code block pasted.',
      statusType: 'ok',
    } : null;
  }

  if (mode === pasteModes.quote) {
    const text = getPlainTextFromPayload(payload);
    return text ? {
      text: buildQuoteBlock(text),
      block: true,
      status: 'Quote pasted.',
      statusType: 'ok',
    } : null;
  }

  if (mode === pasteModes.htmlMarkdown) {
    const text = htmlToMarkdown(payload?.html || '');
    return text ? {
      text,
      block: true,
      status: 'HTML pasted as Markdown.',
      statusType: 'ok',
    } : null;
  }

  if ([pasteModes.list, pasteModes.checklist, pasteModes.numberedList].includes(mode)) {
    const items = getListItemsFromPayload(payload);
    if (!items.length) return null;

    return {
      text: buildListBlock(items, mode),
      block: true,
      status: getListStatus(mode),
      statusType: 'ok',
    };
  }

  if (mode === pasteModes.mermaid) {
    const text = getPlainTextFromPayload(payload);
    return text ? {
      text: buildMermaidBlock(text),
      block: true,
      status: 'Mermaid block pasted.',
      statusType: 'ok',
    } : null;
  }

  if (mode === pasteModes.table || mode === pasteModes.auto) {
    const table = resolveTableRows(payload, { allowCsv: mode === pasteModes.table });
    if (!table) return null;

    return {
      text: formatMarkdownTable(table.rows),
      block: true,
      status: table.flattened ? tableMessages.flattened : tableMessages.ok,
      statusType: table.flattened ? 'warning' : 'ok',
    };
  }

  return null;
}

function resolveTableRows(payload, { allowCsv = false } = {}) {
  const htmlTable = parseHtmlTable(payload?.html || '');
  if (htmlTable) return htmlTable;

  const text = normaliseLineEndings(payload?.text || '').replace(/\n+$/, '');
  if (!text.trim()) return null;

  if (text.includes('\t')) {
    const rows = parseDelimitedRows(text, '\t');
    return isUsefulTable(rows) ? { rows, flattened: false } : null;
  }

  if (allowCsv && text.includes(',')) {
    const rows = parseDelimitedRows(text, ',');
    return isUsefulTable(rows) ? { rows, flattened: false } : null;
  }

  return null;
}

function parseHtmlTable(html) {
  if (!html || !/<table[\s>]/i.test(html)) return null;

  const document = new DOMParser().parseFromString(html, 'text/html');
  const table = document.querySelector('table');
  if (!table) return null;

  const rows = [];
  const rowspans = [];
  let flattened = false;

  table.querySelectorAll('tr').forEach((tableRow) => {
    const row = [];
    let columnIndex = 0;

    columnIndex = fillActiveRowspans(row, rowspans, columnIndex);

    tableRow.querySelectorAll('th,td').forEach((cell) => {
      columnIndex = fillActiveRowspans(row, rowspans, columnIndex);

      const colspan = Math.max(parseInt(cell.getAttribute('colspan') || '1', 10) || 1, 1);
      const rowspan = Math.max(parseInt(cell.getAttribute('rowspan') || '1', 10) || 1, 1);
      flattened = flattened || colspan > 1 || rowspan > 1;

      row[columnIndex] = normaliseCellText(cell.textContent || '');
      for (let offset = 0; offset < colspan; offset += 1) {
        if (offset > 0) row[columnIndex + offset] = '';
        if (rowspan > 1) rowspans[columnIndex + offset] = Math.max(rowspans[columnIndex + offset] || 0, rowspan - 1);
      }
      columnIndex += colspan;
    });

    fillTrailingRowspans(row, rowspans, columnIndex);
    rows.push(row);
  });

  return isUsefulTable(rows) ? { rows, flattened } : null;
}

function fillActiveRowspans(row, rowspans, columnIndex) {
  while (rowspans[columnIndex] > 0) {
    row[columnIndex] = '';
    rowspans[columnIndex] -= 1;
    columnIndex += 1;
  }
  return columnIndex;
}

function fillTrailingRowspans(row, rowspans, columnIndex) {
  for (let index = columnIndex; index < rowspans.length; index += 1) {
    if (rowspans[index] > 0) {
      row[index] = '';
      rowspans[index] -= 1;
    }
  }
}

function parseDelimitedRows(source, delimiter) {
  const text = normaliseLineEndings(source).replace(/\n+$/, '');
  const rows = [];
  let row = [];
  let cell = '';
  let quoted = false;

  for (let index = 0; index < text.length; index += 1) {
    const char = text[index];

    if (quoted) {
      if (char === '"' && text[index + 1] === '"') {
        cell += '"';
        index += 1;
      } else if (char === '"') {
        quoted = false;
      } else {
        cell += char;
      }
      continue;
    }

    if (char === '"' && cell === '') {
      quoted = true;
    } else if (char === delimiter) {
      row.push(normaliseCellText(cell));
      cell = '';
    } else if (char === '\n') {
      row.push(normaliseCellText(cell));
      rows.push(row);
      row = [];
      cell = '';
    } else {
      cell += char;
    }
  }

  row.push(normaliseCellText(cell));
  rows.push(row);
  return rows.filter((items) => items.some((item) => item.length));
}

function isUsefulTable(rows) {
  if (!rows.length) return false;
  const width = Math.max(...rows.map((row) => row.length), 0);
  return width > 1 && rows.some((row) => row.some((cell) => String(cell || '').trim()));
}

function getPlainTextFromPayload(payload) {
  const text = payload?.text || htmlToPlainText(payload?.html || '');
  return normaliseLineEndings(text);
}

function htmlToPlainText(html) {
  if (!html) return '';
  return new DOMParser().parseFromString(html, 'text/html').body.textContent || '';
}

function htmlToMarkdown(html) {
  if (!html?.trim()) return '';
  const document = new DOMParser().parseFromString(html, 'text/html');
  return cleanMarkdownDocument(markdownFromChildren(document.body));
}

function markdownFromChildren(node) {
  return [...(node?.childNodes || [])].map(markdownFromNode).join('');
}

function markdownFromNode(node) {
  if (node.nodeType === 3) return normaliseInlineText(node.textContent || '');
  if (node.nodeType !== 1) return '';

  const element = node;
  const tag = element.tagName.toLowerCase();
  const children = () => markdownFromChildren(element);
  const text = () => cleanMarkdownDocument(children() || element.textContent || '');

  if (/^h[1-6]$/.test(tag)) {
    const level = Number(tag.slice(1));
    return block(`${'#'.repeat(level)} ${text()}`);
  }

  if (['p', 'div', 'section', 'article', 'header', 'footer', 'main', 'aside'].includes(tag)) {
    return block(text());
  }

  if (tag === 'br') return '\n';
  if (tag === 'strong' || tag === 'b') return `**${children()}**`;
  if (tag === 'em' || tag === 'i') return `*${children()}*`;
  if (tag === 's' || tag === 'del' || tag === 'strike') return `~~${children()}~~`;

  if (tag === 'code' && element.closest('pre')) {
    return element.textContent || '';
  }

  if (tag === 'code') {
    return formatInlineCode(element.textContent || '');
  }

  if (tag === 'pre') {
    const source = element.textContent || '';
    return source.trim() ? block(buildCodeBlock(source)) : '';
  }

  if (tag === 'blockquote') {
    return block(buildQuoteBlock(text()));
  }

  if (tag === 'ul' || tag === 'ol') {
    const ordered = tag === 'ol';
    const items = [...element.children]
      .filter((child) => child.tagName?.toLowerCase() === 'li')
      .map((item) => cleanMarkdownDocument(markdownFromChildren(item) || item.textContent || ''))
      .filter(Boolean);
    return items.length ? block(items.map((item, index) => `${ordered ? `${index + 1}.` : '-'} ${item}`).join('\n')) : '';
  }

  if (tag === 'table') {
    const table = parseHtmlTable(element.outerHTML);
    return table ? block(formatMarkdownTable(table.rows)) : '';
  }

  if (tag === 'a') {
    const href = element.getAttribute('href') || '';
    const label = cleanInlineMarkdown(children() || element.textContent || href);
    return href ? `[${label}](${href})` : label;
  }

  if (tag === 'img') {
    const src = element.getAttribute('src') || '';
    if (!src) return '';
    const alt = element.getAttribute('alt') || 'image';
    return `![${cleanInlineMarkdown(alt)}](${src})`;
  }

  if (tag === 'hr') return block('---');

  return children();
}

function buildCodeBlock(value) {
  const text = normaliseLineEndings(value);
  const longestFence = Math.max(2, ...[...text.matchAll(/`+/g)].map((match) => match[0].length));
  const fence = '`'.repeat(Math.max(3, longestFence + 1));
  return `${fence}\n${text}${text.endsWith('\n') ? '' : '\n'}${fence}`;
}

function buildQuoteBlock(value) {
  const lines = normaliseLineEndings(value).replace(/\n+$/, '').split('\n');
  return lines.map((line) => line.trim() ? `> ${line}` : '>').join('\n');
}

function getListItemsFromPayload(payload) {
  if (payload?.html && /<li[\s>]/i.test(payload.html)) {
    const document = new DOMParser().parseFromString(payload.html, 'text/html');
    const htmlItems = [...document.querySelectorAll('li')]
      .map((item) => normaliseListItem(item.textContent || ''))
      .filter(Boolean);
    if (htmlItems.length) return htmlItems;
  }

  return normaliseLineEndings(getPlainTextFromPayload(payload))
    .split('\n')
    .map(normaliseListItem)
    .filter(Boolean);
}

function normaliseListItem(value) {
  return String(value || '')
    .replace(/\u00a0/g, ' ')
    .trim()
    .replace(/^\s*(?:(?:[-*+]|\d+[.)])\s+)?(?:\[[ xX]\]\s+)?/, '')
    .trim();
}

function buildListBlock(items, mode) {
  return items.map((item, index) => {
    if (mode === pasteModes.checklist) return `- [ ] ${item}`;
    if (mode === pasteModes.numberedList) return `${index + 1}. ${item}`;
    return `- ${item}`;
  }).join('\n');
}

function getListStatus(mode) {
  if (mode === pasteModes.checklist) return 'Checklist pasted.';
  if (mode === pasteModes.numberedList) return 'Numbered list pasted.';
  return 'List pasted.';
}

function buildMermaidBlock(value) {
  const source = extractMermaidSource(value);
  return `\`\`\`mermaid\n${source}${source.endsWith('\n') ? '' : '\n'}\`\`\``;
}

function extractMermaidSource(value) {
  const text = normaliseLineEndings(value).trim();
  const fenced = text.match(/^```(?:mermaid|mmd)?\s*\n([\s\S]*?)\n```$/i);
  if (fenced) return fenced[1].trim();

  const devops = text.match(/^:::\s*mermaid\s*\n([\s\S]*?)\n:::$/i);
  if (devops) return devops[1].trim();

  return text;
}

function formatInlineCode(value) {
  const text = String(value || '');
  const longestFence = Math.max(0, ...[...text.matchAll(/`+/g)].map((match) => match[0].length));
  const fence = '`'.repeat(longestFence + 1 || 1);
  const padding = text.startsWith('`') || text.endsWith('`') ? ' ' : '';
  return `${fence}${padding}${text}${padding}${fence}`;
}

function block(value) {
  const text = cleanMarkdownDocument(value);
  return text ? `\n\n${text}\n\n` : '';
}

function cleanMarkdownDocument(value) {
  return normaliseLineEndings(value)
    .split('\n')
    .map((line) => line.replace(/[ \t]+$/g, ''))
    .join('\n')
    .replace(/\n{3,}/g, '\n\n')
    .trim();
}

function cleanInlineMarkdown(value) {
  return normaliseInlineText(value).replace(/[\[\]]/g, '\\$&');
}

function normaliseInlineText(value) {
  return String(value || '').replace(/\s+/g, ' ');
}

function normaliseLineEndings(value) {
  return String(value || '').replace(/\r\n?/g, '\n');
}

function normaliseCellText(value) {
  return normaliseLineEndings(value)
    .replace(/\u00a0/g, ' ')
    .replace(/[ \t]+\n/g, '\n')
    .replace(/\n[ \t]+/g, '\n')
    .trim();
}
