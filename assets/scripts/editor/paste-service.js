import { formatMarkdownTable } from '../utils/markdown-table.js';
import {
  buildCodeBlock,
  buildQuoteBlock,
  htmlToMarkdown,
  htmlToPlainText,
  isUsefulTable,
  normaliseLineEndings,
  parseHtmlTable,
} from '../utils/html-markdown.js';

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
    return resolveHtmlMarkdown(payload);
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
    if (!table) return mode === pasteModes.auto ? resolveHtmlMarkdown(payload) : null;

    return {
      text: formatMarkdownTable(table.rows),
      block: true,
      status: table.flattened ? tableMessages.flattened : tableMessages.ok,
      statusType: table.flattened ? 'warning' : 'ok',
    };
  }

  return null;
}

function resolveHtmlMarkdown(payload) {
  const text = htmlToMarkdown(payload?.html || '');
  return text ? {
    text,
    block: true,
    status: 'HTML pasted as Markdown.',
    statusType: 'ok',
  } : null;
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

function getPlainTextFromPayload(payload) {
  const text = payload?.text || htmlToPlainText(payload?.html || '');
  return normaliseLineEndings(text);
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

function normaliseCellText(value) {
  return normaliseLineEndings(value)
    .replace(/\u00a0/g, ' ')
    .replace(/[ \t]+\n/g, '\n')
    .replace(/\n[ \t]+/g, '\n')
    .trim();
}
